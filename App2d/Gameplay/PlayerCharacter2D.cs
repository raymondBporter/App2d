using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Contacts;
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
    private readonly ISoundEffectSink2D _sounds;
    private float _footstepSeconds;

    public PlayerCharacter2D(PhysicsWorld2D physics, TraversalMetrics2D traversal, Vector2 spawnPoint, uint playerLayer, uint worldLayer, ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(traversal);
        _sounds = ArgGuard.RequireNotNull(sounds);

        WorldObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(traversal.PlayerColliderSize));
        WorldObject.Transform.Position = spawnPoint;

        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.Restitution = 0f;
        Body.Mass = 1f;
        Body.CollisionLayer = playerLayer;
        Body.CollisionMask = worldLayer;
        _motor = new CharacterMotor2D(physics, Body, traversal);
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
    public bool IsDucking => _motor.IsDucking;

    public void BeginFrame(float deltaSeconds)
    {
        LandingSpeedThisFrame = 0f;
        InvulnerabilitySeconds = Math.Max(0f, InvulnerabilitySeconds - deltaSeconds);
    }

    public void UpdateBeforePhysics(PlayerIntent2D intent, float deltaSeconds)
    {
        if (MathF.Abs(intent.MoveX) > 0.01f)
            Facing = MathF.Sign(intent.MoveX);
        _motor.UpdateBeforePhysics(intent, deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds)
    {
        _motor.UpdateAfterPhysics(deltaSeconds);
        UpdateFootsteps(deltaSeconds);
    }

    public void Face(float direction)
    {
        if (MathF.Abs(direction) > 0.01f)
            Facing = MathF.Sign(direction);
    }

    public bool ResolveEnemyTouches(IEnumerable<IEnemyCombatant2D> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (!enemy.IsAlive ||
                !WorldObject.WorldBounds.Intersects(enemy.WorldObject.WorldBounds) ||
                !ShapeCollision2D.TryGetContact(WorldObject, enemy.WorldObject, out var contact))
            {
                continue;
            }

            WorldObject.Transform.Position += contact.MinimumTranslationVector;
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
        if (InvulnerabilitySeconds > 0f || !Health.IsAlive)
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
        _footstepSeconds = 0f;
        LandingSpeedThisFrame = 0f;
        InvulnerabilitySeconds = Math.Max(InvulnerabilitySeconds, 0.35f);
    }

    private void UpdateFootsteps(float deltaSeconds)
    {
        if (!IsGrounded || MathF.Abs(Body.LinearVelocity.X) < FootstepSpeedThreshold)
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
