using App2d.Collision;
using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Simulation;
using App2d.Physics;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

/// <summary>
/// An enemy compiled from an authored entity, in the real terrain/streaming/rollback simulation. Its
/// <see cref="EntityAnimator"/> produces the one final pose per tick: hurt and attack regions are derived from it here,
/// and the presentation draws that same pose from <see cref="EnemyState2D.AuthoredPose"/> rather than re-sampling.
/// Movement belongs to the physics body; gait phase follows the ground distance the body actually covered. An action's
/// "fire" event launches its projectile from the equipped prop's muzzle, unless terrain crosses the barrel; bolts sweep in
/// small steps so thin walls stop them, and they are part of the rollback snapshot.
/// </summary>
public sealed class AuthoredEntityEnemy2D : IEnemyActor2D, IEnemyAttackSource2D, ICombatant2D, IAuthoredHurt2D
{
    private const float Scale = AuthoredWorld.PixelsPerUnit, HurtSeconds = .45f;
    private readonly EntityAnimator _animator;
    private readonly HitLedger _ledger = new();
    private readonly Dictionary<EntityId2D, int> _hitHistory = [];
    private readonly List<EnemyEvent2D> _events = [];
    private readonly List<AnimationEvent> _scratch = [];
    private readonly List<EntityBoltState2D> _bolts = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly CollisionSystem2D _collision;
    private readonly uint _worldLayer;
    private bool _enabled;
    private float _cooldown, _hurt, _dt;
    private readonly EntityReaction _reaction = new();
    private int _facing = -1;
    private Vector2 _rootBefore;

    public AuthoredEntityEnemy2D(EntityId2D id, ResolvedEntity entity, PhysicsWorld2D physics, Vector2 position, uint worldLayer, uint enemyLayer)
    {
        Id = id; Entity = entity; _animator = new(entity); _collision = physics.CollisionSystem; _worldLayer = worldLayer;
        var box = entity.Asset.Movement;
        WorldObject = new(AxisAlignedRectangle2D.FromSize(new Vector2(box.Width, box.Height) * Scale));
        WorldObject.Transform.Position = position;
        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.EntityId = id; Body.CollisionLayer = enemyLayer; Body.CollisionMask = worldLayer; Body.Restitution = 0;
        Health = new(entity.Asset.Health);
        _rootBefore = Root; Evaluate(0, true);
    }

    public ResolvedEntity Entity { get; }
    public EntityId2D Id { get; }
    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction => CombatFaction2D.Enemy;
    public bool IsAlive => Health.IsAlive;
    public ICombatant2D Combatant => this;
    public ActorPose Pose => _animator.Pose;
    /// <summary>The feet origin in world pixels; the animator works in model units from here.</summary>
    private Vector2 Root => WorldObject.Transform.Position - new Vector2(Entity.Asset.Movement.OffsetX * _facing, Entity.Asset.Movement.Height / 2) * Scale;
    private string? Expression => !IsAlive ? "knocked-out" : _hurt > 0 ? "hurt" : null;

    public void SetSimulationEnabled(bool isEnabled)
    {
        _enabled = isEnabled;
        Body.IsCollider = isEnabled && IsAlive;
        Body.MotionType = Body.IsCollider ? BodyMotionType2D.Dynamic : BodyMotionType2D.Static;
        if (!isEnabled) { Body.LinearVelocity = Vector2.Zero; _bolts.Clear(); }
    }

    public void Update(float dt, Vector2 targetPosition)
    {
        if (!_enabled) return;
        _dt = dt; _rootBefore = Root;
        _cooldown = MathF.Max(0, _cooldown - dt); _hurt = MathF.Max(0, _hurt - dt);
        Body.AngularVelocity = 0; WorldObject.Transform.Rotation = 0;
        if (!IsAlive) return;
        var config = Entity.Asset.Controller;
        if (_reaction.Tick(dt)) _cooldown = MathF.Max(_cooldown, config.Cooldown);
        // Staggered: no steering, so the knockback carries.
        if (_reaction.Staggered) return;
        if (_animator.Action is not null) { Body.LinearVelocity = new(0, Body.LinearVelocity.Y); return; }
        var delta = targetPosition - WorldObject.Transform.Position;
        if (Math.Abs(delta.X) > 1) _facing = Math.Sign(delta.X);
        var inRange = Math.Abs(delta.X) <= config.Range * Scale && Math.Abs(delta.Y) < 2 * Scale;
        if (inRange && _cooldown <= 0 && _animator.TryStart(EntityControllers.Attack))
        {
            _cooldown = _animator.Current!.Clip.Duration + config.Cooldown;
            Body.LinearVelocity = new(0, Body.LinearVelocity.Y);
            return;
        }
        var move = !Entity.Controller.Moves || inRange || Math.Abs(delta.X) > 14 * Scale ? 0 : _facing;
        Body.LinearVelocity = new(move * config.WalkSpeed * Scale, Body.LinearVelocity.Y);
    }

    public void SyncAfterPhysics()
    {
        if (!_enabled) return;
        Evaluate(_dt, false);
        foreach (var e in _scratch)
        {
            if (e.Sound is { } sound) _events.Add(new EntityCue2D(Id, Root, sound));
            if (IsAlive && e.Kind == AnimationEvent.EventKind && e.Id == EntityControllers.Fire && _animator.Current?.Projectile is { } shot) Fire(shot);
        }
        if (_animator.ActionComplete) _animator.EndAction();
    }

    private void Evaluate(float dt, bool initial)
    {
        _scratch.Clear();
        var root = Root; var grounded = MathF.Abs(Body.LinearVelocity.Y) < 1;
        var moved = MathF.Abs(root.X - _rootBefore.X) / Scale;
        var role = IsAlive && grounded && MathF.Abs(Body.LinearVelocity.X) > 1 ? EntityControllers.Walk : EntityControllers.Idle;
        // A reaction role wins; otherwise dead, or airborne without an action, holds the current pose as the explicit fallback.
        var reaction = _reaction.Role(Entity, IsAlive);
        var hold = !initial && reaction is null && (!IsAlive || !grounded && _animator.Action is null);
        _animator.Step(dt, root / Scale, _facing, reaction ?? (hold ? _animator.Role : role), grounded ? moved : 0, hold, _scratch, Expression);
    }

    private void Fire(ProjectileDef shot)
    {
        var (point, axis) = EntityCollision.Muzzle(Entity, Pose);
        var muzzle = new Vector2(point.X, point.Y) * Scale;
        if (BarrelBlocked(muzzle)) { _events.Add(new EntityCue2D(Id, muzzle, "blocked")); return; }
        _bolts.Add(new(muzzle, axis * shot.Speed * Scale, new Vector2(shot.Width, shot.Height) * Scale, shot.Lifetime));
    }

    /// <summary>Terrain between the body's centre line and the muzzle: a gun poked through a wall never fires beyond it.</summary>
    private bool BarrelBlocked(Vector2 muzzle)
    {
        var start = new Vector2(WorldObject.Transform.Position.X, muzzle.Y);
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(start, muzzle) / 2));
        var probe = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(new(4)));
        for (var i = 0; i <= steps; i++)
        {
            probe.Transform.Position = Vector2.Lerp(start, muzzle, i / (float)steps);
            if (_collision.Overlap(probe, _overlaps, _worldLayer, includeSensors: false) > 0) return true;
        }
        return false;
    }

    /// <summary>Moves each bolt in small swept steps: terrain stops it, the player takes its damage once, and old bolts expire.</summary>
    private void AdvanceBolts(Person2D player)
    {
        if (_bolts.Count == 0) return;
        var flying = _bolts.ToArray(); _bolts.Clear();
        var damage = Entity.Actions.Values.FirstOrDefault(a => a.Projectile is not null)?.Projectile?.Damage ?? 0;
        foreach (var bolt in flying)
        {
            var lifetime = bolt.Lifetime - _dt;
            if (lifetime <= 0) continue;
            var distance = bolt.Velocity * _dt;
            var steps = Math.Max(1, (int)MathF.Ceiling(distance.Length() / 4));
            var shape = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(bolt.Size)); var position = bolt.Position; var hit = false;
            for (var step = 1; step <= steps; step++)
            {
                position = bolt.Position + distance * (step / (float)steps); shape.Transform.Position = position;
                if (_collision.Overlap(shape, _overlaps, _worldLayer, includeSensors: false) > 0) { hit = true; _events.Add(new EntityCue2D(Id, position, "impact")); break; }
                if (player.IsAlive && shape.WorldBounds.Intersects(player.WorldObject.WorldBounds))
                { player.TryTakeDamageFromX(damage, bolt.Position.X); hit = true; _events.Add(new EntityCue2D(Id, position, "hit")); break; }
            }
            if (!hit) _bolts.Add(bolt with { Position = position, Lifetime = lifetime });
        }
    }

    private static EntityRegion ToWorld(EntityRegion region) => new(region.Id, region.Points.Select(p => p * Scale).ToArray());

    public bool TryResolvePlayerHit(Person2D player)
    {
        if (!_enabled) return false;
        AdvanceBolts(player); // bolts already in flight keep going after their shooter falls
        if (!IsAlive) return !player.IsAlive;
        var bounds = player.WorldObject.WorldBounds; var target = EntityRegion.Box("player", bounds.Center, bounds.Size);
        foreach (var hit in _animator.ActiveHits())
        {
            var region = ToWorld(EntityCollision.Attack(Entity, Pose, hit));
            if (!region.Overlaps(target, Vector2.Zero, Vector2.Zero) || !_ledger.TryHit(_animator.ActionSequence, hit.Window.Id, 0)) continue;
            player.TryTakeDamageFromX(hit.Window.Damage, WorldObject.Transform.Position.X);
            _events.Add(new EntityCue2D(Id, player.Position, hit.Window.Sound ?? "hit"));
        }
        return !player.IsAlive;
    }

    public bool OverlapsHurt(Bounds2D hit)
    {
        if (!_enabled || !IsAlive) return false;
        var box = EntityRegion.Box("attack", hit.Center, hit.Size);
        return EntityCollision.Hurt(Entity, Pose).Any(region => ToWorld(region).Overlaps(box, Vector2.Zero, Vector2.Zero));
    }

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        if (!_enabled || !IsAlive) yield break;
        foreach (var hit in _animator.ActiveHits())
            yield return new SpatialObject2D(new ConvexPolygon2D(ToWorld(EntityCollision.Attack(Entity, Pose, hit)).Points.ToArray()));
    }

    public bool TryRegisterHit(EntityId2D source, int attack)
    { if (_hitHistory.GetValueOrDefault(source, -1) == attack) return false; _hitHistory[source] = attack; return true; }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        if (!Health.Damage(damage)) return false;
        _hurt = HurtSeconds;
        // A hit interrupts an attack and staggers; contacts are released and re-captured from the next pose.
        if (IsAlive) _reaction.Hit(_animator); else _reaction.Die(_animator);
        Body.LinearVelocity = IsAlive ? knockback / Entity.Asset.Mass : Vector2.Zero;
        if (!IsAlive) { Body.IsCollider = false; Body.MotionType = BodyMotionType2D.Static; }
        return true;
    }

    public EnemyState2D CaptureState() => new(Id, EnemyKind2D.Authored, WorldObject.Transform.Position, Body.LinearVelocity, 0, _facing, _enabled, IsAlive)
    {
        TypeId = Entity.Id, ActionId = _animator.Action ?? _animator.Role, ActionSeconds = (float)(_animator.Action is null ? _animator.RoleTime : _animator.ActionTime),
        IsAttacking = _animator.Action == EntityControllers.Attack, AttackElapsedSeconds = (float)_animator.ActionTime,
        MoveSpeed = Entity.Asset.Controller.WalkSpeed * Scale, AuthoredEntity = Entity, AuthoredPose = Pose, Bolts = [.. _bolts],
    };

    public ImmutableArray<EnemyEvent2D> DrainEvents() { var events = _events.ToImmutableArray(); _events.Clear(); return events; }

    private sealed record Snapshot(bool Enabled, float Cooldown, float Hurt, float Stagger, int Facing, int Health, Vector2 RootBefore, AnimatorState Animator,
        ImmutableArray<(int, string, int)> Ledger, ImmutableDictionary<EntityId2D, int> Hits, ImmutableArray<EnemyEvent2D> Events, ImmutableArray<EntityBoltState2D> Bolts) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new Snapshot(_enabled, _cooldown, _hurt, _reaction.Capture(), _facing, Health.Current, _rootBefore, _animator.Capture(),
        _ledger.Capture(), _hitHistory.ToImmutableDictionary(), _events.ToImmutableArray(), [.. _bolts]);

    public void RestoreSimulation(SimulationState2D state)
    {
        var s = (Snapshot)state;
        _enabled = s.Enabled; _cooldown = s.Cooldown; _hurt = s.Hurt; _reaction.Restore(s.Stagger); _facing = s.Facing; _rootBefore = s.RootBefore;
        Health.RestoreSimulation(s.Health); _ledger.Restore(s.Ledger);
        _hitHistory.Clear(); foreach (var pair in s.Hits) _hitHistory.Add(pair.Key, pair.Value);
        _events.Clear(); _events.AddRange(s.Events);
        _bolts.Clear(); _bolts.AddRange(s.Bolts);
        _animator.Restore(s.Animator, Root / Scale, Expression);
    }
}
