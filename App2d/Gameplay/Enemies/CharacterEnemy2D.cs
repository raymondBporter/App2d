using App2d.Contracts.Combat;
using App2d.Contracts.Enemies;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters;
using App2d.Core.Collision;
using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Physics.Queries;
using App2d.Core.Spatial;
using App2d.Gameplay.Characters;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.World;
using System.Collections.Immutable;

namespace App2d.Gameplay.Enemies;

/// <summary>
/// An enemy compiled from an authored entity, in the terrain and streaming simulation. Its
/// <see cref="EntityAnimator"/> produces the one final pose per tick: hurt and attack regions are derived from it here,
/// and the presentation draws that same pose from <see cref="EnemyState2D.Pose"/> rather than re-sampling.
/// Movement belongs to the physics body; gait phase follows the ground distance the body actually covered. The entity's
/// attached colliders (hurt, attack, guard, trigger) are registered once in the world's collision system with this enemy's
/// id and layer and synchronized from the final pose at the end of every <see cref="SyncAfterPhysics"/> (and on the damage
/// tick), so physics movement never leaves them stale before the enemy's own attacks resolve; the player's melee query of
/// the same step reads them as the previous sync left them, exactly as the legacy hurt scan read the previous pose. An
/// action's "fire" event launches its projectile from the equipped prop's muzzle, unless terrain crosses the barrel; bolts
/// sweep in small steps so thin walls stop them, and their current values are included in enemy observations.
/// </summary>
public sealed class CharacterEnemy2D : IEnemyActor2D, IEnemyAttackSource2D, ICombatant2D, IRegisteredHurt2D, ICombatGuard2D
{
    private const float Scale = GameWorldUnits2D.WorldUnitsPerAuthoredUnit;
    private readonly CharacterAnimator _animator;
    private readonly HitLedger _ledger = new();
    private readonly Dictionary<(EntityId2D Source, string Group), int> _hitHistory = [];
    private readonly List<EnemyEvent2D> _events = [];
    private readonly List<CharacterEvent> _scratch = [];
    private readonly List<EntityBoltState2D> _bolts = [];
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly List<AcceptedContact> _contacts = [];
    private readonly CollisionSystem2D _collision;
    private readonly PhysicsWorld2D _physics;
    private readonly SpatialObject2D _groundProbe = new(AxisAlignedRectangle2D.Builder.CreateFromSize(4, 8));
    private readonly uint _worldLayer;
    private bool _enabled;
    private float _cooldown, _hurt, _dt;
    private readonly CharacterReaction _reaction;
    private int _facing = -1;
    private Vector2f _rootBefore;
    private Vector2f _attackTarget;
    private float _retreatRemaining;
    private bool _retreatUsed;
    private bool _chargeBlocked;
    private bool IsChargeAction => _animator.Action?.Name == CharacterAnimator.Charge;
    private float ChargeStart => _animator.Action?.Cue("rush")?.Seconds ?? Type.Control.ChargeStartSeconds;
    private float ChargeEnd => _animator.Action?.Cue("brake")?.Seconds ?? Type.Control.ChargeEndSeconds;
    private readonly EnemyFlightController2D? _flight;
    /// <summary>The movement shape's local bounds: feet at the origin, +X toward facing, authored units.</summary>
    private readonly Rect2f _movement;

    public CharacterEnemy2D(EntityId2D id, EnemyType type, PhysicsWorld2D physics, Vector2f position, uint worldLayer, uint enemyLayer)
    {
        Id = id;
        Type = type;
        _animator = new(type.Character);
        _reaction = new(type.StaggerSeconds);
        _collision = physics.CollisionSystem;
        _physics = physics;
        _worldLayer = worldLayer;
        _flight = type.Control.Flight is { } flight ? new(flight) : null;
        _movement = ShapeBounds2D.Calculate(type.Character.Movement);
        WorldObject = new(AxisAlignedRectangle2D.Builder.CreateFromSize(_movement.Size * Scale))
        {
            Position = position
        };
        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.EntityId = id;
        Body.CollisionLayer = enemyLayer;
        Body.CollisionMask = worldLayer;
        Body.Restitution = 0;
        Body.GravityScale = _flight is null ? 1 : 0;
        Health = new(type.Health);
        Colliders = new(type.Character, physics.CollisionSystem, id, enemyLayer, Scale);
        _rootBefore = Root;
        Evaluate(0, true);
    }

    /// <summary>The registered attached colliders, as the collision system holds them (world units).</summary>
    public CharacterColliders Colliders { get; }

    public EnemyType Type { get; }
    public Combatant Character => Type.Character;
    public EntityId2D Id { get; }
    public SpatialObject2D WorldObject { get; }
    public RigidBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction => CombatFaction2D.Enemy;
    public bool IsAlive => Health.IsAlive;
    public ICombatant2D Combatant => this;
    public CharacterPose Pose => _animator.Pose;
    /// <summary>The feet origin in world pixels; the animator works in model units from here.</summary>
    private Vector2f Root => WorldObject.Position - new Vector2f(_movement.Center.X * _facing, _movement.Center.Y) * Scale;
    private string? Expression => !IsAlive ? "knocked-out" : _hurt > 0 ? "hurt" : null;

    public void SetSimulationEnabled(bool isEnabled)
    {
        _enabled = isEnabled;
        Body.IsCollider = isEnabled && IsAlive;
        Body.MotionType = Body.IsCollider ? BodyMotionType2D.Dynamic : BodyMotionType2D.Static;
        Colliders.SetActive(isEnabled);
        if (!isEnabled)
        {
            Body.LinearVelocity = Vector2f.Zero;
            _bolts.Clear();
        }
        else
        {
            SyncColliders();
        }
    }

    public void Update(float dt, Vector2f targetPosition)
    {
        if (!_enabled)
        {
            return;
        }

        _dt = dt;
        _rootBefore = Root;
        _cooldown = MathF.Max(0, _cooldown - dt);
        _hurt = MathF.Max(0, _hurt - dt);
        Body.AngularVelocity = 0;
        WorldObject.Rotation = 0;
        if (!IsAlive)
        {
            return;
        }

        var config = Type.Control;
        if (_reaction.Tick(dt))
        {
            _cooldown = MathF.Max(_cooldown, config.Cooldown);
        }
        // Staggered: no steering, so the knockback carries.
        if (_reaction.Staggered)
        {
            return;
        }

        if (_flight is not null)
        {
            var velocity = _flight.Update(dt, WorldObject.Position, targetPosition,
                _animator.Action, (float)_animator.ActionTime, Type.Control, ref _facing, _chargeBlocked, RecoveryAltitude);
            if (_animator.Action is null && _flight.Ready && _cooldown <= 0 && CanFly(targetPosition - WorldObject.Position) &&
                _animator.TryStart(CharacterAnimator.Attack))
            {
                _cooldown = _animator.Action!.Clip.Duration + config.Cooldown;
                _flight.Begin(WorldObject.Position);
                _chargeBlocked = false;
                velocity = Vector2f.Zero;
            }
            if (!CanFly(velocity * dt))
            {
                if (_animator.Action is not null && !_chargeBlocked)
                {
                    _events.Add(new EntityCue2D(Id, Root, "flight-stop"));
                }

                if (_animator.Action is not null)
                {
                    _chargeBlocked = true;
                }

                velocity = Vector2f.Zero;
            }
            Body.LinearVelocity = velocity;
            return;
        }
        if (_animator.Action is not null)
        {
            var at = (float)_animator.ActionTime;
            var usesChargeMotion = config.ChargeSpeed > 0 && (config.ChargeRange <= 0 || IsChargeAction);
            var speed = usesChargeMotion && at >= ChargeStart && !_chargeBlocked
                ? config.ChargeSpeed * Realf.Clamp01(1 - (at - ChargeEnd) / config.BrakeSeconds) : 0;
            if (speed > 0 && config.RespectTerrain && !CanAdvance(dt, _facing, speed))
            {
                _chargeBlocked = true;
                speed = 0;
                _events.Add(new EntityCue2D(Id, Root, "charge-stop"));
            }
            Body.LinearVelocity = new(_facing * speed * Scale, Body.LinearVelocity.Y);
            return;
        }
        var delta = targetPosition - WorldObject.Position;
        if (Math.Abs(delta.X) > 1)
        {
            _facing = Math.Sign(delta.X);
        }

        if (config.RetreatRange > 0 && !_retreatUsed && Math.Abs(delta.X) < config.RetreatRange * Scale)
        {
            _retreatUsed = true;
            _retreatRemaining = config.RetreatSeconds + config.RetreatPause;
        }
        if (_retreatRemaining > 0)
        {
            _retreatRemaining = MathF.Max(0, _retreatRemaining - dt);
            var retreat = _retreatRemaining > config.RetreatPause ? -_facing : 0;
            if (config.RespectTerrain && retreat != 0 && !CanAdvance(dt, retreat))
            {
                retreat = 0;
            }

            Body.LinearVelocity = new(retreat * config.WalkSpeed * Scale, Body.LinearVelocity.Y);
            return;
        }
        var horizontalDistance = MathF.Abs(delta.X);
        var inHeight = MathF.Abs(delta.Y) < config.VerticalRange * Scale;
        var inRange = horizontalDistance <= config.Range * Scale && inHeight;
        var canCharge = !inRange && _cooldown <= 0 && config.ChargeRange > 0 && horizontalDistance <= config.ChargeRange * Scale && inHeight;
        if (config.RespectTerrain && inRange && BarrelBlocked(targetPosition))
        {
            inRange = false;
        }

        if (_cooldown <= 0 && (inRange || (canCharge && ChargeLaneClear(dt, delta))) &&
            _animator.TryStart(inRange ? CharacterAnimator.Attack : CharacterAnimator.Charge))
        {
            _cooldown = _animator.Action!.Clip.Duration + config.Cooldown;
            _attackTarget = targetPosition;
            _retreatUsed = false;
            _chargeBlocked = false;
            Body.LinearVelocity = new(0, Body.LinearVelocity.Y);
            return;
        }
        var move = inRange || horizontalDistance > 14 * Scale ? 0 : _facing;
        if (config.RespectTerrain && move != 0 && !CanAdvance(dt, move))
        {
            move = 0;
        }

        Body.LinearVelocity = new(move * config.WalkSpeed * Scale, Body.LinearVelocity.Y);
    }

    private bool ChargeLaneClear(float dt, Vector2f toTarget) => CanAdvance(dt, _facing) &&
        !_physics.Raycast(new Ray2D(WorldObject.Position, toTarget), toTarget.Length(), out _, _worldLayer, includeSensors: false);

    private float RecoveryAltitude(Vector2f target)
    {
        var probe = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(4, 4));
        for (var down = 0; down <= 800; down += 4)
        {
            probe.Position = target - new Vector2f(0, down);
            if (_collision.Overlap(probe, _overlaps, _worldLayer, includeSensors: false) > 0)
            {
                return _overlaps.Max(o => o.Collider.WorldObject.WorldBounds.Max.Y) + 26;
            }
        }
        return target.Y;
    }

    private bool CanFly(Vector2f displacement)
    {
        var steps = Math.Max(1, (int)MathF.Ceiling(displacement.Length() / 4));
        var probe = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(_movement.Size * Scale));
        for (var i = 1; i <= steps; i++)
        {
            probe.Position = WorldObject.Position + displacement * (i / (float)steps);
            if (_collision.Overlap(probe, _overlaps, _worldLayer, includeSensors: false) > 0)
            {
                return false;
            }
        }
        return true;
    }

    // Look beyond the leading foot by this tick's travel: do not walk off ledges or into walls.
    private bool CanAdvance(float dt, int direction, float? speed = null)
    {
        var bounds = WorldObject.WorldBounds;
        var x = WorldObject.Position.X + direction *
            (_movement.Width * Scale / 2 + (speed ?? Type.Control.WalkSpeed) * Scale * dt + 3);
        _groundProbe.Position = new(x, bounds.Min.Y - 3);
        if (_collision.Overlap(_groundProbe, _overlaps, _worldLayer, includeSensors: false) == 0)
        {
            return false;
        }

        _groundProbe.Position = new(x, bounds.Min.Y + 8);
        return _collision.Overlap(_groundProbe, _overlaps, _worldLayer, includeSensors: false) == 0;
    }

    public void SyncAfterPhysics()
    {
        if (!_enabled)
        {
            return;
        }

        Evaluate(_dt, false);
        foreach (var e in _scratch)
        {
            if (e.Sound is { } sound && !(IsChargeAction && _chargeBlocked && e.Name == "brake"))
            {
                _events.Add(new EntityCue2D(Id, Root, sound));
            }

            if (IsAlive && e.IsCue && e.Name == CharacterAnimator.Fire && _animator.Action?.Projectile is { } shot)
            {
                Fire(shot);
            }
        }
        if (_animator.ActionComplete)
        {
            _animator.EndAction();
        }
    }

    private void Evaluate(float dt, bool initial)
    {
        _scratch.Clear();
        // Grounded means still relative to the ground, so a rising or sinking platform counts; planted feet ride with it.
        // In the air nothing is held from one step to the next, so the feet plant afresh where they land. The platform
        // carried the body before _rootBefore was taken, so the distance walked is already the body's own.
        var ground = initial ? null : GroundSupport2D.Velocity(_physics.LastContacts, Body, CanStandOn);
        var root = Root;
        var grounded = MathF.Abs(Body.LinearVelocity.Y - (ground?.Y ?? 0)) < 1;
        var airborne = _flight is not null || !initial && !grounded;
        if (airborne)
        {
            _animator.Lift();
        }
        else if (ground is { } carry && carry != Vector2f.Zero)
        {
            _animator.Carry(carry * dt / Scale);
        }

        var moved = MathF.Abs(root.X - _rootBefore.X) / Scale;
        var role = IsAlive && grounded && _flight is null && MathF.Abs(Body.LinearVelocity.X) > 1 ? CharacterAnimator.Walk : CharacterAnimator.Idle;
        if (IsChargeAction)
        {
            var at = _animator.ActionTime;
            role = _chargeBlocked || at >= ChargeEnd ? CharacterAnimator.ChargeBrake :
                at >= ChargeStart ? CharacterAnimator.ChargeRush : CharacterAnimator.ChargeWindup;
        }
        // A reaction role wins; otherwise dead, or airborne without an action, holds the current pose as the explicit fallback.
        var reaction = _reaction.Role(Character, IsAlive);
        var hold = !initial && reaction is null && (!IsAlive || _flight is null && !grounded && _animator.Action is null);
        _animator.Step(dt, root / Scale, _facing, reaction ?? (hold ? _animator.Role : role), grounded && _flight is null ? moved : 0, hold, _scratch, Expression);
        if (airborne)
        {
            _animator.Lift();
        }

        SyncColliders();
    }

    /// <summary>The pose/collider synchronization point: the registered colliders follow the final pose the animator just produced.</summary>
    private void SyncColliders() => Colliders.Sync(Pose, _chargeBlocked ? null : _animator.ActiveHits(), IsAlive, GuardUp);

    /// <summary>Whether the shield is up: alive, not staggered and the action has not passed its guard-open event.</summary>
    private bool GuardUp
    {
        get
        {
            if (!IsAlive || _reaction.Staggered || Type.Guard < 0)
            {
                return false;
            }

            var open = _animator.Action?.Cue(Type.GuardOpenCue);
            return open is null || _animator.ActionTime < open.Seconds;
        }
    }

    private bool CanStandOn(RigidBody2D other) => other.MotionType != BodyMotionType2D.Dynamic && !other.IsSensor && _physics.CanCollide(Body, other);

    private void Fire(Projectile shot)
    {
        var (point, axis) = Character.Muzzle(Pose);
        var muzzle = point * Scale;
        if (BarrelBlocked(muzzle))
        {
            _events.Add(new EntityCue2D(Id, muzzle, "blocked"));
            return;
        }
        var gravity = shot.Gravity * Scale;
        var velocity = shot.FlightSeconds > 0
            ? (_attackTarget - muzzle) / shot.FlightSeconds + new Vector2f(0, gravity * shot.FlightSeconds / 2)
            : axis * shot.Speed * Scale;
        _bolts.Add(new(muzzle, velocity, shot.Size * Scale, shot.Lifetime)
        {
            Gravity = gravity
        });
    }

    /// <summary>Terrain between the body's centre line and the muzzle: a gun poked through a wall never fires beyond it.</summary>
    private bool BarrelBlocked(Vector2f muzzle)
    {
        var start = new Vector2f(WorldObject.Position.X, muzzle.Y);
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector2f.Distance(start, muzzle) / 2));
        var probe = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(4, 4));
        for (var i = 0; i <= steps; i++)
        {
            probe.Position = Vector2f.Lerp(start, muzzle, i / (float)steps);
            if (_collision.Overlap(probe, _overlaps, _worldLayer, includeSensors: false) > 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Moves each bolt in small swept steps: terrain stops it, the player takes its damage once, and old bolts expire.</summary>
    private void AdvanceBolts(Person2D player)
    {
        if (_bolts.Count == 0)
        {
            return;
        }

        var flying = _bolts.ToArray();
        _bolts.Clear();
        var damage = Character.Actions.FirstOrDefault(a => a.Projectile is not null)?.Projectile!.Strike.Damage ?? 0;
        foreach (var bolt in flying)
        {
            var lifetime = bolt.Lifetime - _dt;
            if (lifetime <= 0)
            {
                continue;
            }

            var acceleration = new Vector2f(0, -bolt.Gravity);
            var distance = bolt.Velocity * _dt + acceleration * (_dt * _dt / 2);
            var steps = Math.Max(1, (int)MathF.Ceiling(distance.Length() / 4));
            var shape = new SpatialObject2D(AxisAlignedRectangle2D.Builder.CreateFromSize(bolt.Size));
            var position = bolt.Position;
            var hit = false;
            for (var step = 1; step <= steps; step++)
            {
                var t = _dt * step / steps;
                position = bolt.Position + bolt.Velocity * t + acceleration * (t * t / 2);
                shape.Position = position;
                if (_collision.Overlap(shape, _overlaps, _worldLayer, includeSensors: false) > 0)
                {
                    hit = true;
                    _events.Add(new EntityCue2D(Id, position, bolt.Gravity > 0 ? "rock-impact" : "impact"));
                    break;
                }
                if (player.IsAlive && shape.WorldBounds.Intersects(player.WorldObject.WorldBounds))
                {
                    player.TryTakeDamageFromX(damage, bolt.Position.X);
                    hit = true;
                    _events.Add(new EntityCue2D(Id, position, "hit"));
                    break;
                }
            }
            if (!hit)
            {
                _bolts.Add(bolt with
                {
                    Position = position,
                    Velocity = bolt.Velocity + acceleration * _dt,
                    Lifetime = lifetime
                });
            }
        }
    }

    public bool TryResolvePlayerHit(Person2D player)
    {
        if (!_enabled)
        {
            return false;
        }

        AdvanceBolts(player); // bolts already in flight keep going after their shooter falls
        if (!IsAlive || _chargeBlocked)
        {
            return !player.IsAlive;
        }

        // The shared resolution against the player's body collider: once per attack instance and hit group; the player's own
        // reception (invulnerability, dashing, knockback from the attacker's side) decides whether the contact is accepted.
        CombatContacts.Resolve(Colliders, _animator.ActionSequence, _animator.ActiveHits(), player.Body.CollisionLayer, _ledger,
            (_, collider) => collider.EntityId == player.Id && player.IsAlive ? (player.Id, false) : null, _contacts, _overlaps);
        foreach (var contact in _contacts)
        {
            if (player.TryTakeDamageFromX(contact.Strike.Damage, WorldObject.Position.X))
            {
                _events.Add(new EntityCue2D(Id, contact.Position, contact.Strike.Sound ?? "hit"));
            }
        }
        return !player.IsAlive;
    }

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        if (!_enabled || !IsAlive || _chargeBlocked)
        {
            yield break;
        }

        foreach (var hit in _animator.ActiveHits())
        {
            yield return Colliders.At(hit.Collider).Object;
        }
    }

    public bool TryRegisterHit(EntityId2D source, int attack, string? hitGroup = null)
    {
        var key = (source, hitGroup ?? "");
        if (_hitHistory.GetValueOrDefault(key, -1) == attack)
        {
            return false;
        }

        _hitHistory[key] = attack;
        return true;
    }

    /// <summary>The registered guard collider's bounds while it is enabled (up), as the last synchronization left it.</summary>
    private Rect2f? GuardBounds()
    {
        if (!_enabled || Type.Guard < 0)
        {
            return null;
        }

        var shield = Colliders.At(Type.Guard);
        return shield.Enabled ? shield.Bounds : null;
    }

    public bool OverlapsGuard(Rect2f attackBounds) => GuardBounds() is { } bounds && bounds.Intersects(attackBounds);

    public bool CanBlock(Rect2f attackBounds, Vector2f incomingDirection, Vector2f? attackerPosition)
    {
        var fromFront = attackerPosition is { } source
            ? (source.X - WorldObject.Position.X) * _facing > 0
            : incomingDirection.X != 0 ? incomingDirection.X * _facing < 0
            : (attackBounds.Center.X - WorldObject.Position.X) * _facing > 0;
        if (!fromFront)
        {
            return false;
        }

        return OverlapsGuard(attackBounds);
    }

    public bool TryBlock(Rect2f attackBounds, Vector2f incomingDirection, Vector2f? attackerPosition)
    {
        if (!CanBlock(attackBounds, incomingDirection, attackerPosition))
        {
            return false;
        }

        var bounds = GuardBounds()!.Value;
        var point = (Vector2f.Max(bounds.Min, attackBounds.Min) + Vector2f.Min(bounds.Max, attackBounds.Max)) / 2;
        if (Type.BlockSound is { } blockSound)
        {
            _events.Add(new EntityCue2D(Id, point, blockSound));
        }
        return true;
    }

    public bool TakeDamage(int damage, Vector2f knockback)
    {
        if (!Health.Damage(damage))
        {
            return false;
        }

        _hurt = Type.HurtSeconds;
        if (IsAlive && !_chargeBlocked && _animator.Action is { } action && _animator.ActionTime < action.UninterruptibleUntil)
        {
            return true;
        }

        // A hit interrupts an attack and staggers; contacts are released and re-captured from the next pose.
        if (IsAlive)
        {
            _reaction.Hit(_animator, Math.Sign(knockback.X));
        }
        else
        {
            _reaction.Die(_animator, Math.Sign(knockback.X));
        }
        // Publish the reaction pose on the damage tick; collision and rendering continue to share it.
        Evaluate(0, false);
        Body.LinearVelocity = IsAlive ? knockback / Type.KnockbackResistance : Vector2f.Zero;
        if (!IsAlive)
        {
            Body.IsCollider = false;
            Body.MotionType = BodyMotionType2D.Static;
        }
        return true;
    }

    public EnemyState2D CaptureState() => new(Id, EnemyKind2D.Character, WorldObject.Position, Body.LinearVelocity, 0, _facing, _enabled, IsAlive)
    {
        TypeId = Character.Name,
        ActionId = _animator.Action?.Name ?? _animator.Role,
        ActionSeconds = (float)(_animator.Action is null ? _animator.RoleTime : _animator.ActionTime),
        IsAttacking = _animator.Action?.Name == CharacterAnimator.Attack || IsChargeAction,
        AttackElapsedSeconds = (float)_animator.ActionTime,
        MoveSpeed = Type.Control.WalkSpeed * Scale,
        Type = Type,
        Pose = Pose,
        Bolts = [.. _bolts],
    };

    public ImmutableArray<EnemyEvent2D> DrainEvents()
    {
        var events = _events.ToImmutableArray();
        _events.Clear();
        return events;
    }
}
