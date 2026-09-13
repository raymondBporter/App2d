using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons.Actions;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.Persons;

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
    private readonly Dictionary<EntityId2D, int> _lastAttackIds = [];
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
        float mass = 1f,
        IChunkedTileMap2D? tileMap = null)
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
        Body.EntityId = Id;
        Health = new Health2D(maximumHealth);
        Faction = faction;

        _motor = new PersonLocomotion2D(collision, physics, Body, traversal, tileMap);
        _motor.JumpStarted += () => JumpStarted?.Invoke();
        _motor.Landed += speed =>
        {
            LandingSpeedThisFrame = speed;
            Landed?.Invoke(speed);
            _footstepSeconds = FootstepIntervalSeconds * 0.5f;
        };
    }

    public EntityId2D Id { get; } = EntityId2D.Create();
    public SpatialObject2D WorldObject { get; }
    public PhysicsBody2D Body { get; }
    public Health2D Health { get; }
    public CombatFaction2D Faction { get; }
    public Vector2 Position => WorldObject.Transform.Position;
    public float Facing { get; private set; } = 1f;
    public float InvulnerabilitySeconds { get; private set; }
    public float LandingSpeedThisFrame { get; private set; }
    public bool DownAttackBouncedThisFrame { get; private set; }
    public bool IsGrounded => _motor.IsGrounded;
    public int BalanceDirection => IsAlive && _simulationEnabled ? _motor.BalanceDirection : 0;
    public bool IsWallGripping => _motor.IsWallGripping;
    public bool IsDashing => _motor.IsDashing;
    public bool IsClimbingLadder => _motor.IsClimbingLadder;
    public bool IsSustainingJump => _motor.IsSustainingJump;
    public float JumpPower => _motor.JumpPower;
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
        DownAttackBouncedThisFrame = false;
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

        if (command.SwitchEquipment)
            _actions?.SelectNext();

        if (MathF.Abs(command.Movement.MoveX) > 0.01f)
            Face(command.Movement.MoveX);
        var previousWallDirection = _motor.IsWallGripping ? _motor.WallDirection : 0f;
        _motor.UpdateBeforePhysics(command.Movement, Facing, deltaSeconds);
        _actions?.SetPrimaryInput(command.PrimaryActionHeld, canCharge: true,
            released: command.PrimaryActionReleased);
        if ((command.UsePrimaryAction || command.UseSecondaryAction) &&
            _actions is not null)
        {
            var isWallAttack = _motor.IsWallGripping;
            var attackFacing = isWallAttack
                ? -_motor.WallDirection
                : Facing;
            var aimTarget = isWallAttack
                ? null
                : command.AimTarget;
            if (command.UsePrimaryAction)
            {
                var isDownAttack = command.DownHeld && !_motor.IsGrounded &&
                    !isWallAttack && !_motor.IsClimbingLadder && !_motor.IsDashing;
                Face(isDownAttack
                    ? _actions.UseDownwardPrimary(aimTarget, attackFacing)
                    : _actions.UsePrimary(aimTarget, attackFacing));
            }
            if (command.UseSecondaryAction)
                Face(_actions.UseSecondary(aimTarget, attackFacing));
        }
        if (_actions?.IsChargingPrimary == true)
        {
            var wallDirection = _motor.IsWallGripping ? _motor.WallDirection : previousWallDirection;
            if (wallDirection != 0f)
                Face(-wallDirection);
        }
        _actions?.UpdateBeforePhysics(deltaSeconds);
    }

    public void UpdateAfterPhysics(float deltaSeconds)
    {
        if (!_simulationEnabled)
            return;

        if (!IsAlive)
        {
            _motor.UpdatePassiveAfterPhysics();
            return;
        }

        _motor.UpdateAfterPhysics(deltaSeconds);
        _actions?.UpdateAfterPhysics(deltaSeconds, Facing);
        if (_actions?.ConsumeDownAttackBounce() == true)
        {
            _motor.BounceFromDownAttack();
            DownAttackBouncedThisFrame = true;
        }
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

    public bool TryRegisterHit(EntityId2D attackSourceId, int attackId)
    {
        if (!attackSourceId.IsValid)
            throw new ArgumentException("An attack source ID is required.", nameof(attackSourceId));
        if (_lastAttackIds.TryGetValue(attackSourceId, out var lastAttackId) &&
            lastAttackId == attackId)
        {
            return false;
        }

        _lastAttackIds[attackSourceId] = attackId;
        return true;
    }

    public bool TakeDamage(int damage, Vector2 knockback)
    {
        ArgGuard.ThrowIfNotPositive(damage);
        ArgGuard.ThrowIfNotFinite(knockback);
        if (IsDashing || InvulnerabilitySeconds > 0f || !IsAlive)
            return false;

        Health.Damage(damage);
        _actions?.InterruptPrimary();
        _motor.DetachFromLadder();
        InvulnerabilitySeconds = 0.9f;
        Body.LinearVelocity = knockback;
        Damaged?.Invoke();
        if (!IsAlive)
        {
            Body.LinearVelocity = Vector2.Zero;
            _motor.EnterPassiveState();
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
        DownAttackBouncedThisFrame = false;
        _simulationEnabled = enabled;
        Body.IsCollider = enabled;
        Body.MotionType = enabled
            ? BodyMotionType2D.Dynamic
            : BodyMotionType2D.Static;
        if (!enabled)
        {
            _motor.DetachFromLadder();
            Body.LinearVelocity = Vector2.Zero;
            _actions?.Reset();
        }
    }

    public void Reset(Vector2 spawnPoint, int? hitPoints = null)
    {
        DownAttackBouncedThisFrame = false;
        ArgGuard.ThrowIfNotFinite(spawnPoint);
        if (hitPoints is { } savedHitPoints)
            Health.Reset(savedHitPoints);
        else
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
