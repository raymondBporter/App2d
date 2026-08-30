using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

public sealed class PlayerCharacter2D
{
    private const float HardLandingSpeed = 650f;
    private const float FootstepSpeedThreshold = 65f;
    private const float FootstepIntervalSeconds = 0.29f;
    private const float DamageKnockbackX = 220f;
    private const float DamageKnockbackY = 170f;

    private readonly CharacterMotor2D _motor;
    private readonly CollisionSystem2D _collision;
    private readonly uint _enemyLayer;
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly ISoundEffectSink2D _sounds;
    private float _footstepSeconds;

    public PlayerCharacter2D(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TraversalMetrics2D traversal,
        Vector2 spawnPoint,
        uint playerLayer,
        uint worldLayer,
        uint enemyLayer,
        ISoundEffectSink2D sounds)
    {
        _collision = ArgGuard.RequireNotNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(traversal);
        _sounds = ArgGuard.RequireNotNull(sounds);
        _enemyLayer = enemyLayer;

        WorldObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(
            traversal.PlayerColliderSize,
            new Vector2(traversal.PlayerColliderCenterOffsetX, 0f)));
        WorldObject.Transform.Position = spawnPoint;

        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.Restitution = 0f;
        Body.Mass = 1f;
        Body.CollisionLayer = playerLayer;
        Body.CollisionMask = worldLayer;
        _motor = new CharacterMotor2D(collision, physics, Body, traversal);
        _motor.JumpStarted += () => _sounds.Play(SoundEffect2D.PlayerJump);
        _motor.Landed += speed =>
        {
            LandingSpeedThisFrame = speed;
            _sounds.Play(speed >= HardLandingSpeed ? SoundEffect2D.PlayerLandHard : SoundEffect2D.PlayerLandSoft);
            _footstepSeconds = FootstepIntervalSeconds * 0.5f;
        };
    }

    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; } = new(5);
    public event Action? Damaged;
    public Vector2 Position => WorldObject.Transform.Position;
    public float Facing { get; private set; } = 1f;
    public float InvulnerabilitySeconds { get; private set; }
    public float LandingSpeedThisFrame { get; private set; }
    public bool IsGrounded => _motor.IsGrounded;
    public bool IsWallGripping => _motor.IsWallGripping;
    public bool IsDashing => _motor.IsDashing;

    public void BeginFrame(float deltaSeconds)
    {
        LandingSpeedThisFrame = 0f;
        InvulnerabilitySeconds = Math.Max(0f, InvulnerabilitySeconds - deltaSeconds);
    }

    public void UpdateBeforePhysics(PlayerIntent2D intent, float deltaSeconds)
    {
        if (MathF.Abs(intent.MoveX) > 0.01f)
            SetFacing(intent.MoveX);
        _motor.UpdateBeforePhysics(intent, Facing, deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds)
    {
        _motor.UpdateAfterPhysics(deltaSeconds);
        UpdateFootsteps(deltaSeconds);
    }

    public void Face(float direction)
    {
        SetFacing(direction);
    }

    public bool ResolveEnemyTouches()
    {
        if (IsDashing)
            return false;

        _collision.Overlap(
            WorldObject,
            _overlaps,
            _enemyLayer,
            includeSensors: true,
            excluded: Body.Collider);
        foreach (var overlap in _overlaps)
        {
            if (overlap.Collider.UserData is not PhysicsBody2D
                {
                    UserData: IEnemyCombatant2D { IsAlive: true } enemy
                })
            {
                continue;
            }

            WorldObject.Transform.Position += overlap.Contact.MinimumTranslationVector;
            if (TryTakeDamage(1, enemy.WorldObject.Transform.Position.X))
                return !Health.IsAlive;
        }

        return false;
    }

    public bool TryTakeDamage(
        int damage,
        float sourceX,
        float horizontalKnockback = DamageKnockbackX,
        float verticalKnockback = DamageKnockbackY)
    {
        ArgGuard.ThrowIfNotPositive(damage);
        ArgGuard.ThrowIfNotFinite(sourceX);
        ArgGuard.ThrowIfNotPositive(horizontalKnockback);
        ArgGuard.ThrowIfNotPositive(verticalKnockback);
        if (IsDashing || InvulnerabilitySeconds > 0f || !Health.IsAlive)
            return false;

        Health.Damage(damage);
        InvulnerabilitySeconds = 0.9f;
        float knockbackDirection = MathF.Sign(Position.X - sourceX);
        if (knockbackDirection == 0f)
            knockbackDirection = -Facing;
        Body.LinearVelocity = new Vector2(
            knockbackDirection * horizontalKnockback,
            verticalKnockback);
        _sounds.Play(SoundEffect2D.PlayerHurt);
        Damaged?.Invoke();
        return true;
    }

    public void Reset(Vector2 spawnPoint)
    {
        WorldObject.Transform.Position = spawnPoint;
        Body.LinearVelocity = Vector2.Zero;
        Body.AngularVelocity = 0f;
        _motor.Reset();
        SetFacing(Facing);
        _footstepSeconds = 0f;
        LandingSpeedThisFrame = 0f;
        InvulnerabilitySeconds = Math.Max(InvulnerabilitySeconds, 0.35f);
    }

    private void SetFacing(float direction)
    {
        if (MathF.Abs(direction) <= 0.01f)
            return;

        Facing = MathF.Sign(direction);
        var scale = WorldObject.Transform.Scale;
        WorldObject.Transform.Scale = new Vector2(Facing, scale.Y);
    }

    private void UpdateFootsteps(float deltaSeconds)
    {
        if (IsDashing ||
            !IsGrounded ||
            MathF.Abs(Body.LinearVelocity.X) < FootstepSpeedThreshold)
        {
            _footstepSeconds = 0f;
            return;
        }

        _footstepSeconds -= deltaSeconds;
        if (_footstepSeconds > 0f)
            return;

        _sounds.Play(SoundEffect2D.PlayerFootstep);
        var speedRatio = Math.Clamp(
            MathF.Abs(Body.LinearVelocity.X) / _motor.Metrics.RunSpeed,
            0.65f,
            1.15f);
        _footstepSeconds = FootstepIntervalSeconds / speedRatio;
    }
}
