using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay;

/// <summary>
/// Shared humanoid simulation. Input, AI, rendering, audio, respawning, and
/// encounter policy live outside this type.
/// </summary>
public sealed class Person2D : ICombatant2D
{
    private const float FootstepSpeedThreshold = 65f;
    private const float FootstepIntervalSeconds = 0.29f;
    private const float DefaultDamageKnockbackX = 220f;
    private const float DefaultDamageKnockbackY = 170f;

    private readonly PersonLocomotion2D _motor;
    private readonly Dictionary<object, int> _lastAttackIds =
        new(ReferenceEqualityComparer.Instance);
    private IPersonActionSet2D? _actions;
    private float _footstepSeconds;
    private bool _simulationEnabled = true;

    public Person2D(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TraversalMetrics2D traversal,
        Vector2 spawnPoint,
        uint bodyLayer,
        uint worldLayer,
        CombatFaction2D faction,
        int maximumHealth = 5,
        float mass = 1f)
    {
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(traversal);
        ArgGuard.ThrowIfNotFinite(spawnPoint);
        ArgGuard.ThrowIfNotPositive(maximumHealth);
        ArgGuard.ThrowIfNotPositive(mass);

        WorldObject = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(
            traversal.PlayerColliderSize,
            new Vector2(traversal.PlayerColliderCenterOffsetX, 0f)));
        WorldObject.Transform.Position = spawnPoint;

        Body = physics.AddBody(WorldObject, BodyMotionType2D.Dynamic);
        Body.Restitution = 0f;
        Body.Mass = mass;
        Body.CollisionLayer = bodyLayer;
        Body.CollisionMask = worldLayer;
        Body.UserData = this;
        Health = new Health2D(maximumHealth);
        Faction = faction;

        _motor = new PersonLocomotion2D(collision, physics, Body, traversal);
        _motor.JumpStarted += () => JumpStarted?.Invoke();
        _motor.Landed += speed =>
        {
            LandingSpeedThisFrame = speed;
            Landed?.Invoke(speed);
            _footstepSeconds = FootstepIntervalSeconds * 0.5f;
        };
    }

    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction { get; }
    public Vector2 Position => WorldObject.Transform.Position;
    public float Facing { get; private set; } = 1f;
    public float InvulnerabilitySeconds { get; private set; }
    public float LandingSpeedThisFrame { get; private set; }
    public bool IsGrounded => _motor.IsGrounded;
    public bool IsWallGripping => _motor.IsWallGripping;
    public bool IsDashing => _motor.IsDashing;
    public bool IsAlive => Health.IsAlive;
    public bool IsSimulationEnabled => _simulationEnabled;
    public IPersonActionSet2D? Actions => _actions;

    public event Action? JumpStarted;
    public event Action<float>? Landed;
    public event Action? Footstep;
    public event Action? Damaged;
    public event Action? Died;

    public void BeginFrame(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        LandingSpeedThisFrame = 0f;
        InvulnerabilitySeconds = Math.Max(0f, InvulnerabilitySeconds - deltaSeconds);
        _actions?.BeginFrame(deltaSeconds);
    }

    public void AttachActions(IPersonActionSet2D actions)
    {
        ArgGuard.ThrowIfNull(actions);
        StateGuard.ThrowIf(
            _actions is not null,
            "The person already has an action set.");
        _actions = actions;
    }

    public void ApplyCommand(PersonCommand2D command, float deltaSeconds)
    {
        if (!_simulationEnabled || !IsAlive)
            return;

        if (command.SwitchWeapon)
            _actions?.SelectNext();

        if (MathF.Abs(command.Movement.MoveX) > 0.01f)
            Face(command.Movement.MoveX);
        _motor.UpdateBeforePhysics(command.Movement, Facing, deltaSeconds);
        if (command.UseWeapon && _actions is not null)
            Face(_actions.UsePrimary(command.AimTarget, Facing));
        _actions?.UpdateBeforePhysics(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds)
    {
        if (!_simulationEnabled || !IsAlive)
            return;

        _motor.UpdateAfterPhysics(deltaSeconds);
        _actions?.UpdateAfterPhysics(deltaSeconds, Facing);
        UpdateFootsteps(deltaSeconds);
    }

    public void Face(float direction)
    {
        if (MathF.Abs(direction) <= 0.01f)
            return;

        Facing = MathF.Sign(direction);
        var scale = WorldObject.Transform.Scale;
        WorldObject.Transform.Scale = new Vector2(Facing, scale.Y);
    }

    public bool TryRegisterHit(object attackSource, int attackId)
    {
        ArgGuard.ThrowIfNull(attackSource);
        if (_lastAttackIds.TryGetValue(attackSource, out var lastAttackId) &&
            lastAttackId == attackId)
        {
            return false;
        }

        _lastAttackIds[attackSource] = attackId;
        return true;
    }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        ArgGuard.ThrowIfNotPositive(damage);
        ArgGuard.ThrowIfNotFinite(knockback);
        if (IsDashing || InvulnerabilitySeconds > 0f || !IsAlive)
            return false;

        Health.Damage(damage);
        InvulnerabilitySeconds = 0.9f;
        Body.LinearVelocity = knockback;
        Damaged?.Invoke();
        if (!IsAlive)
        {
            Body.IsCollider = false;
            Body.LinearVelocity = Vector2.Zero;
            _actions?.Reset();
            Died?.Invoke();
        }
        return true;
    }

    public bool TryTakeDamageFromX(
        int damage,
        float sourceX,
        float horizontalKnockback = DefaultDamageKnockbackX,
        float verticalKnockback = DefaultDamageKnockbackY)
    {
        ArgGuard.ThrowIfNotPositive(damage);
        ArgGuard.ThrowIfNotFinite(sourceX);
        ArgGuard.ThrowIfNotPositive(horizontalKnockback);
        ArgGuard.ThrowIfNotPositive(verticalKnockback);

        float direction = MathF.Sign(Position.X - sourceX);
        if (direction == 0f)
            direction = -Facing;
        return TakeDamage(
            damage,
            new Vector2(direction * horizontalKnockback, verticalKnockback));
    }

    public void SetSimulationEnabled(bool enabled)
    {
        _simulationEnabled = enabled;
        if (!IsAlive)
            return;

        Body.IsCollider = enabled;
        Body.MotionType = enabled
            ? BodyMotionType2D.Dynamic
            : BodyMotionType2D.Static;
        if (!enabled)
        {
            Body.LinearVelocity = Vector2.Zero;
            _actions?.Reset();
        }
    }

    public void Reset(Vector2 spawnPoint)
    {
        ArgGuard.ThrowIfNotFinite(spawnPoint);
        Health.Reset();
        WorldObject.Transform.Position = spawnPoint;
        Body.IsCollider = _simulationEnabled;
        Body.MotionType = _simulationEnabled
            ? BodyMotionType2D.Dynamic
            : BodyMotionType2D.Static;
        Body.LinearVelocity = Vector2.Zero;
        Body.AngularVelocity = 0f;
        _motor.Reset();
        _actions?.Reset();
        _lastAttackIds.Clear();
        Face(Facing);
        _footstepSeconds = 0f;
        LandingSpeedThisFrame = 0f;
        InvulnerabilitySeconds = Math.Max(InvulnerabilitySeconds, 0.35f);
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

        Footstep?.Invoke();
        var speedRatio = Math.Clamp(
            MathF.Abs(Body.LinearVelocity.X) / _motor.Metrics.RunSpeed,
            0.65f,
            1.15f);
        _footstepSeconds = FootstepIntervalSeconds / speedRatio;
    }
}
