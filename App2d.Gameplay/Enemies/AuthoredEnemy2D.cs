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

/// <summary>Type-driven actor in the real terrain/streaming/rollback simulation.</summary>
public sealed class AuthoredEnemy2D : IEnemyActor2D, IEnemyAttackSource2D, ICombatant2D, IAuthoredHurt2D
{
    private const float Scale = EntityCatalog.WorldUnits;
    private readonly CollisionSystem2D _collision;
    private readonly uint _worldLayer;
    private readonly EntityPose _pose;
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly Dictionary<EntityId2D, int> _hitHistory = [];
    private readonly List<EnemyEvent2D> _events = [];
    private readonly List<EntityBoltState2D> _bolts = [];
    private readonly List<(Vector2 Start, EntityBoltState2D Bolt)> _pendingBolts = [];
    private bool _enabled, _connected, _fired, _cue;
    private float _elapsed, _previousElapsed, _cooldown, _facing = 1, _dt;
    private string _action = "idle";
    public EntityTypeDefinition Type { get; }
    public EntityId2D Id { get; }
    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction => CombatFaction2D.Enemy;
    public bool IsAlive => Health.IsAlive;
    public ICombatant2D Combatant => this;
    private EntityAction Action => Type.Actions[_action];
    private Vector2 Root => WorldObject.Transform.Position - new Vector2(Type.Movement.OffsetX * _facing * Scale, Type.Movement.Height * Scale / 2);

    public AuthoredEnemy2D(EntityId2D id, EntityCatalog catalog, string typeId, CollisionSystem2D collision,
        PhysicsWorld2D physics, Vector2 position, uint worldLayer, uint enemyLayer)
    {
        Id = id; Type = catalog.Types[typeId].Copy(); _collision = collision; _worldLayer = worldLayer;
        _pose = new(catalog.Libraries[Type.Library]);
        WorldObject = new(AxisAlignedRectangle2D.FromSize(new Vector2(Type.Movement.Width, Type.Movement.Height) * Scale));
        WorldObject.Transform.Position = position;
        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.EntityId = id; Body.CollisionLayer = enemyLayer; Body.CollisionMask = worldLayer;
        Body.Mass = Type.Id == "maul" ? 3 : 1; Body.Restitution = 0;
        Health = new(Type.Health); Evaluate();
    }
    public void SetSimulationEnabled(bool isEnabled)
    {
        _enabled = isEnabled;
        Body.IsCollider = isEnabled && IsAlive;
        Body.MotionType = Body.IsCollider ? BodyMotionType2D.Dynamic : BodyMotionType2D.Static;
        if (!isEnabled) { Body.LinearVelocity = Vector2.Zero; _bolts.Clear(); _pendingBolts.Clear(); }
    }
    private void Start(string action)
    {
        if (_action == action && action is "walk" or "idle") return;
        _action = action; _elapsed = _previousElapsed = 0; _connected = _fired = _cue = false;
    }
    public void Update(float dt, Vector2 targetPosition)
    {
        if (!_enabled) return;
        _dt = dt; _previousElapsed = _elapsed; _elapsed += dt; _cooldown = Math.Max(0, _cooldown - dt);
        if (!IsAlive) { Evaluate(); return; }
        Body.AngularVelocity = 0; WorldObject.Transform.Rotation = 0;
        if (_action is "attack" or "hit")
        {
            if (_elapsed >= Action.Duration) { _cooldown = Type.Cooldown; Start("idle"); }
            else { if (_action == "attack") Body.LinearVelocity = new(0, Body.LinearVelocity.Y); Evaluate(); return; }
        }
        var delta = targetPosition - WorldObject.Transform.Position;
        if (Math.Abs(delta.X) > 1) _facing = Math.Sign(delta.X);
        var inRange = Math.Abs(delta.X) <= Type.PreferredRange * Scale && Math.Abs(delta.Y) < 2 * Scale;
        if (Type.Behavior != "passive" && inRange && _cooldown <= 0) { Start("attack"); Body.LinearVelocity = new(0, Body.LinearVelocity.Y); }
        else
        {
            var move = Type.Behavior == "passive" || inRange || Math.Abs(delta.X) > 14 * Scale ? 0 : _facing;
            Body.LinearVelocity = new(move * Type.MoveSpeed * Scale, Body.LinearVelocity.Y);
            Start(move == 0 ? "idle" : "walk");
        }
        Evaluate();
    }
    private void Evaluate() => _pose.Evaluate(Type, Action, _elapsed, _facing < 0);
    public void SyncAfterPhysics()
    {
        Evaluate();
        if (!_enabled) return;
        if (IsAlive && !_cue && _elapsed >= Action.CueTime * Action.Duration)
        { _cue = true; if (Action.Cue != "none") _events.Add(new EntityCue2D(Id, Root, Action.Cue)); }
        if (IsAlive && _action == "attack" && Action.AttackKind == "projectile" && !_fired && _elapsed >= Action.Contact * Action.Duration)
        {
            _fired = true;
            var muzzle = Root + _pose.Muzzle * Scale;
            if (!BarrelBlocked(muzzle))
                _bolts.Add(new(muzzle, _pose.Aim * Action.ProjectileSpeed * Scale,
                    new Vector2(Action.HitWidth, Action.HitHeight) * Scale, 3));
        }
        _pendingBolts.Clear();
        foreach (var bolt in _bolts) _pendingBolts.Add((bolt.Position, bolt with { Lifetime = bolt.Lifetime - _dt }));
        _bolts.Clear();
    }
    public bool TryResolvePlayerHit(Person2D player)
    {
        if (!_enabled) return false;
        if (IsAlive && _action == "attack" && Action.AttackKind == "melee" && !_connected &&
            _elapsed >= Action.ActiveStart * Action.Duration && _previousElapsed < Action.ActiveEnd * Action.Duration &&
            RegionWorld(_pose.Hit).Overlaps(PlayerRegion(player), Vector2.Zero, Vector2.Zero))
        {
            _connected = true; player.TryTakeDamageFromX(Action.Damage, WorldObject.Transform.Position.X);
            if (Action.ImpactCue != "none") _events.Add(new EntityCue2D(Id, player.Position, Action.ImpactCue));
        }
        // Small swept steps stop at terrain before testing the target, avoiding thin-wall tunneling.
        foreach (var (start, bolt) in _pendingBolts)
        {
            if (bolt.Lifetime <= 0) continue;
            var distance = bolt.Velocity * _dt;
            var steps = Math.Max(1, (int)MathF.Ceiling(distance.Length() / 4));
            var hit = false; var position = start;
            var shape = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(bolt.Size));
            for (var step = 0; step <= steps; step++)
            {
                position = start + distance * (step / (float)steps); shape.Transform.Position = position;
                if (_collision.Overlap(shape, _overlaps, _worldLayer, includeSensors: false) > 0) { hit = true; break; }
                if (shape.WorldBounds.Intersects(player.WorldObject.WorldBounds))
                { player.TryTakeDamageFromX(Type.Actions["attack"].Damage, start.X); hit = true; break; }
            }
            if (!hit) _bolts.Add(bolt with { Position = position });
        }
        _pendingBolts.Clear();
        return !player.IsAlive;
    }
    private static EntityRegion PlayerRegion(Person2D player) => EntityRegion.Box("player", player.WorldObject.WorldBounds.Center, player.WorldObject.WorldBounds.Size);
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
    private EntityRegion RegionWorld(EntityRegion region) => new(region.Id, region.Points.Select(p => Root + p * Scale).ToArray());
    public bool OverlapsHurt(Bounds2D hit) => _enabled && IsAlive && _pose.Hurt.Any(region =>
        RegionWorld(region).Overlaps(EntityRegion.Box("attack", hit.Center, hit.Size), Vector2.Zero, Vector2.Zero));
    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        if (_enabled && IsAlive && _action == "attack" && Action.AttackKind == "melee" && Action.Active(_elapsed))
            yield return new SpatialObject2D(new ConvexPolygon2D(RegionWorld(_pose.Hit).Points.ToArray()));
    }
    public bool TryRegisterHit(EntityId2D source, int attack)
    { if (_hitHistory.GetValueOrDefault(source, -1) == attack) return false; _hitHistory[source] = attack; return true; }
    public bool TakeDamage(int damage, Vector2 knockback)
    {
        if (!Health.Damage(damage)) return false;
        Start(IsAlive ? "hit" : "death");
        Body.LinearVelocity = IsAlive ? knockback : Vector2.Zero;
        if (!IsAlive) { Body.IsCollider = false; Body.MotionType = BodyMotionType2D.Static; }
        Evaluate(); return true;
    }
    public EnemyState2D CaptureState() => new(Id, EnemyKind2D.Authored, WorldObject.Transform.Position, Body.LinearVelocity, 0, _facing, _enabled, IsAlive)
    { TypeId = Type.Id, ActionId = _action, ActionSeconds = _elapsed, IsAttacking = _action == "attack", AttackElapsedSeconds = _elapsed, MoveSpeed = Type.MoveSpeed * Scale, Bolts = _bolts.ToImmutableArray() };
    public ImmutableArray<EnemyEvent2D> DrainEvents() { var events = _events.ToImmutableArray(); _events.Clear(); return events; }
    private sealed record Snapshot(bool Enabled, bool Connected, bool Fired, bool Cue, float Elapsed, float Previous,
        float Cooldown, float Facing, string Action, int Health, ImmutableDictionary<EntityId2D, int> Hits,
        ImmutableArray<EntityBoltState2D> Bolts, ImmutableArray<EnemyEvent2D> Events) : SimulationState2D;
    public SimulationState2D CaptureSimulation() => new Snapshot(_enabled, _connected, _fired, _cue, _elapsed, _previousElapsed,
        _cooldown, _facing, _action, Health.Current, _hitHistory.ToImmutableDictionary(), _bolts.ToImmutableArray(), _events.ToImmutableArray());
    public void RestoreSimulation(SimulationState2D state)
    {
        var s = (Snapshot)state; _enabled = s.Enabled; _connected = s.Connected; _fired = s.Fired; _cue = s.Cue;
        _elapsed = s.Elapsed; _previousElapsed = s.Previous; _cooldown = s.Cooldown; _facing = s.Facing; _action = s.Action;
        Health.RestoreSimulation(s.Health); _hitHistory.Clear(); foreach (var pair in s.Hits) _hitHistory.Add(pair.Key, pair.Value);
        _bolts.Clear(); _bolts.AddRange(s.Bolts); _events.Clear(); _events.AddRange(s.Events); _pendingBolts.Clear(); Evaluate();
    }
}
