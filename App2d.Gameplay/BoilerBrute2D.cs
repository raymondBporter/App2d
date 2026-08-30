using App2d.Collision;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using System.Numerics;

namespace App2d.Gameplay;

public sealed class BoilerBrute2D : IEnemyActor2D, IEnemyAttackSource2D
{
    private const int HammerImpactFrame = 5;
    private const float AttackRangeX = 155f;
    private const float AttackRangeY = 90f;
    private const float AttackCooldownSeconds = 1.1f;
    private static readonly Vector2 VisualCanvasSize = new(196f, 196f);
    private static readonly Vector2 VisualOffset = new(0f, 32f);
    private static readonly Vector2 HammerHitboxSize = new(108f, 76f);

    private readonly AnimationClip2D<Texture2D> _walkAnimation;
    private readonly AnimationClip2D<Texture2D> _hammerAnimation;
    private readonly AnimationPlayer2D<Texture2D> _animation = new();
    private readonly SpriteShader2D _spriteShader;
    private readonly WorldObject2D _visual;
    private readonly SpatialObject2D _hammerHitbox;
    private readonly CollisionSystem2D _collision;
    private readonly ISoundEffectSink2D _sounds;
    private float _attackCooldownSeconds = 0.35f;
    private float _facing = 1f;
    private bool _isAttacking;
    private bool _hammerConnected;
    private bool _hammerImpactPlayed;
    private bool _simulationEnabled = true;

    public BoilerBrute2D(
        Scene2D scene,
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TextureCache2D textures,
        Vector2 position,
        float patrolMinX,
        float patrolMaxX,
        uint worldLayer,
        uint enemyLayer,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(scene);
        _collision = ArgGuard.RequireNotNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNotFinite(position);
        _sounds = ArgGuard.RequireNotNull(sounds);

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

        _walkAnimation = CharacterAnimationAssets2D.LoadClip(
            textures,
            "boiler-brute",
            "walk");
        _hammerAnimation = CharacterAnimationAssets2D.LoadClip(
            textures,
            "boiler-brute",
            "hammer-attack");
        _animation.Play(_walkAnimation);
        _spriteShader = new SpriteShader2D(_animation.CurrentFrame);
        _visual = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(VisualCanvasSize),
            _spriteShader);
        scene.Add(_visual);

        _hammerHitbox = new SpatialObject2D(
            AxisAlignedRectangle2D.FromSize(HammerHitboxSize));
        SyncPresentation();
    }

    public PatrolEnemy2D Enemy { get; }
    public IEnemyCombatant2D Combatant => Enemy;
    public bool IsHammerActive =>
        _simulationEnabled &&
        Enemy.IsAlive &&
        _isAttacking &&
        _animation.CurrentFrameIndex == HammerImpactFrame;

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Enemy.SetSimulationEnabled(isEnabled);
        _visual.IsVisible = isEnabled && Enemy.IsAlive;
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(targetPosition);

        Enemy.Update(deltaSeconds);
        if (!_simulationEnabled || !Enemy.IsAlive)
        {
            _visual.IsVisible = false;
            return;
        }

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
            _animation.Update(deltaSeconds);
            if (IsHammerActive && !_hammerImpactPlayed)
            {
                _hammerImpactPlayed = true;
                _sounds.Play(SoundEffect2D.HammerImpact);
            }
            if (_animation.IsFinished)
                FinishAttack(AttackCooldownSeconds);
        }
        else if (CanAttack(targetPosition))
        {
            StartAttack(targetPosition);
            _animation.Update(deltaSeconds);
        }
        else
        {
            _facing = Enemy.Facing;
            _animation.Play(_walkAnimation);
            _animation.PlaybackSpeed = Math.Clamp(
                MathF.Abs(Enemy.Body.LinearVelocity.X) / Enemy.Speed,
                0.65f,
                1.15f);
            _animation.Update(deltaSeconds);
        }

        SyncPresentation();
    }

    public bool TryResolveHammerHit(PlayerCharacter2D player)
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
        var dealtDamage = player.TryTakeDamage(
            damage: 2,
            sourceX: Enemy.WorldObject.Transform.Position.X);
        return dealtDamage && !player.Health.IsAlive;
    }

    public bool TryResolvePlayerHit(PlayerCharacter2D player) =>
        TryResolveHammerHit(player);

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        if (IsHammerActive)
            yield return _hammerHitbox;
    }

    public void SyncAfterPhysics()
    {
        if (_simulationEnabled && Enemy.IsAlive)
            SyncPresentation();
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
        _hammerImpactPlayed = false;
        _animation.Play(_hammerAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
        Enemy.Body.LinearVelocity = new Vector2(0f, Enemy.Body.LinearVelocity.Y);
        _sounds.Play(SoundEffect2D.HammerWindup);
    }

    private void FinishAttack(float cooldownSeconds)
    {
        _isAttacking = false;
        _attackCooldownSeconds = cooldownSeconds;
        _animation.Play(_walkAnimation, restart: true);
        _animation.PlaybackSpeed = 1f;
    }

    private void SyncPresentation()
    {
        _spriteShader.Texture = _animation.CurrentFrame;
        _spriteShader.FlipX = _facing < 0f;
        _visual.Transform.Position = Enemy.WorldObject.Transform.Position + VisualOffset;
        _visual.IsVisible = _simulationEnabled && Enemy.IsAlive;
        _hammerHitbox.Transform.Position =
            Enemy.WorldObject.Transform.Position + new Vector2(_facing * 62f, -10f);
    }

}
