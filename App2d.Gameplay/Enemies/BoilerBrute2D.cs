using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Physics;
using System.Numerics;
using System.Collections.Immutable;

namespace App2d.Gameplay.Enemies;

public sealed partial class BoilerBrute2D : IEnemyActor2D, IEnemyAttackSource2D
{
    // Gameplay timing is fixed independently of sprite count or playback speed.
    public const float AttackDurationSeconds = BoilerBruteTiming2D.AttackDurationSeconds;
    public const float DamageStartSeconds = BoilerBruteTiming2D.DamageStartSeconds;
    public const float DamageEndSeconds = BoilerBruteTiming2D.DamageEndSeconds;
    private float _attackElapsedSeconds;
    private readonly List<EnemyEvent2D> _events = [];
    private const float AttackRangeX = 155f;
    private const float AttackRangeY = 90f;
    private const float AttackCooldownSeconds = 1.1f;
    private static readonly Vector2 HammerHitboxSize = new(108f, 76f);

    private readonly SpatialObject2D _hammerHitbox;
    private readonly CollisionSystem2D _collision;
    private float _attackCooldownSeconds = 0.35f;
    private float _facing = 1f;
    private bool _isAttacking;
    private bool _hammerConnected;
    private bool _strikeReported;
    private bool _simulationEnabled = true;

    public BoilerBrute2D(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        Vector2 position,
        float patrolMinX,
        float patrolMaxX,
        uint worldLayer,
        uint enemyLayer)
    {
        _collision = ArgGuard.RequireNotNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNotFinite(position);

        var collider = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(new Vector2(68f, 98f)));
        collider.Transform.Position = position;

        var body = physics.AddBody(collider, BodyMotionType2D.Dynamic);
        body.Restitution = 0f;
        body.Mass = 3.2f;
        body.CollisionLayer = enemyLayer;
        body.CollisionMask = worldLayer;

        Enemy = new PatrolEnemy2D(
            collider,
            body,
            patrolMinX,
            patrolMaxX,
            speed: 62f,
            health: 8);

        _hammerHitbox = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(HammerHitboxSize));
        SyncHitbox();
    }

    public PatrolEnemy2D Enemy { get; }
    public ICombatant2D Combatant => Enemy;
    public bool IsHammerActive =>
        _simulationEnabled &&
        Enemy.IsAlive &&
        _isAttacking &&
        _attackElapsedSeconds >= DamageStartSeconds && _attackElapsedSeconds < DamageEndSeconds;

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Enemy.SetSimulationEnabled(isEnabled);
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(targetPosition);

        if (!_simulationEnabled || !Enemy.IsAlive) return;
        Enemy.Update(deltaSeconds);

        _attackCooldownSeconds = Math.Max(
            0f,
            _attackCooldownSeconds - deltaSeconds);

        if (_isAttacking && Enemy.IsStunned)
            FinishAttack(0.45f);

        if (_isAttacking)
        {
            Enemy.Body.LinearVelocity = new Vector2(
                0f,
                Enemy.Body.LinearVelocity.Y);
            AdvanceAttack(deltaSeconds);
        }
        else if (CanAttack(targetPosition))
        {
            StartAttack(targetPosition);
            AdvanceAttack(deltaSeconds);
        }
        else _facing = Enemy.Facing;

        SyncHitbox();
    }

    public bool TryResolveHammerHit(Person2D player)
    {
        ArgGuard.ThrowIfNull(player);
        if (!IsHammerActive || _hammerConnected ||
            !_collision.TryGetContact(
                _hammerHitbox,
                player.Body.Collider,
                out _))
        {
            return false;
        }

        _hammerConnected = true;
        var dealtDamage = player.TryTakeDamageFromX(
            damage: 2,
            sourceX: Enemy.WorldObject.Transform.Position.X);
        return dealtDamage && !player.Health.IsAlive;
    }

    public bool TryResolvePlayerHit(Person2D player) =>
        TryResolveHammerHit(player);

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        if (IsHammerActive)
            yield return _hammerHitbox;
    }

    public void SyncAfterPhysics()
    {
        if (_simulationEnabled && Enemy.IsAlive)
            SyncHitbox();
    }

    private bool CanAttack(Vector2 targetPosition)
    {
        if (_attackCooldownSeconds > 0f || Enemy.IsStunned)
            return false;

        var offset = targetPosition - Enemy.WorldObject.Transform.Position;
        return MathF.Abs(offset.X) <= AttackRangeX &&
               MathF.Abs(offset.Y) <= AttackRangeY;
    }

    private void StartAttack(Vector2 targetPosition)
    {
        var targetOffsetX = targetPosition.X - Enemy.WorldObject.Transform.Position.X;
        if (MathF.Abs(targetOffsetX) > 0.01f)
            _facing = MathF.Sign(targetOffsetX);

        _isAttacking = true;
        _hammerConnected = false;
        _strikeReported = false;
        _attackElapsedSeconds = 0f;
        Enemy.Body.LinearVelocity = new Vector2(0f, Enemy.Body.LinearVelocity.Y);
        _events.Add(new HammerStarted2D(Enemy.Id, Enemy.WorldObject.Transform.Position));
    }

    private void FinishAttack(float cooldownSeconds)
    {
        _isAttacking = false;
        _attackCooldownSeconds = cooldownSeconds;
        _attackElapsedSeconds = 0f;
    }

    private void AdvanceAttack(float dt)
    {
        _attackElapsedSeconds += dt;
        if (_attackElapsedSeconds >= DamageStartSeconds && !_strikeReported)
        {
            _strikeReported = true;
            _events.Add(new HammerStruck2D(Enemy.Id,
                Enemy.WorldObject.Transform.Position + new Vector2(_facing * 62f, -10f)));
        }
        if (_attackElapsedSeconds >= AttackDurationSeconds) FinishAttack(AttackCooldownSeconds);
    }

    public EnemyState2D CaptureState() => new(Enemy.Id, EnemyKind2D.BoilerBrute,
        Enemy.WorldObject.Transform.Position, Enemy.Body.LinearVelocity, 0f, _facing,
        _simulationEnabled, Enemy.IsAlive)
    {
        MoveSpeed = Enemy.Speed, IsAttacking = _isAttacking,
        AttackElapsedSeconds = _attackElapsedSeconds
    };

    public ImmutableArray<EnemyEvent2D> DrainEvents()
    {
        var result = _events.ToImmutableArray();
        _events.Clear();
        return result;
    }

    private void SyncHitbox() => _hammerHitbox.Transform.Position =
        Enemy.WorldObject.Transform.Position + new Vector2(_facing * 62f, -10f);
}
