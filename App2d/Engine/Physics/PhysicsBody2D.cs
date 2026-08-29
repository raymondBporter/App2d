using System.Numerics;

namespace App2d.Engine.Physics;

public sealed class PhysicsBody2D(SpatialObject2D worldObject, BodyMotionType2D motionType)
{
    private HashSet<PhysicsBody2D>? _ignoredOneWayPlatforms;
    private float _mass = 1f;
    private float _momentOfInertia = 1f;
    private float _oneWaySlop = 2f;

    public SpatialObject2D WorldObject { get; } = ArgGuard.RequireNotNull(worldObject);
    public BodyMotionType2D MotionType { get; set; } = motionType;
    public Vector2 LinearVelocity { get; set; }
    public float AngularVelocity { get; set; }
    public Vector2 AccumulatedForce { get; private set; }
    public float AccumulatedTorque { get; private set; }
    public Vector2 PreviousPosition { get; internal set; } = worldObject.Transform.Position;
    public float PreviousRotation { get; internal set; } = worldObject.Transform.Rotation;
    public float GravityScale { get; set; } = 1f;
    public float Restitution { get; set; } = 0.5f;
    public bool IsCollider { get; set; } = true;
    public bool IsSensor { get; set; }
    public bool IsOneWayPlatform { get; set; }
    public uint CollisionLayer { get; set; } = 1u;
    public uint CollisionMask { get; set; } = uint.MaxValue;
    public object? UserData { get; set; }

    public float OneWaySlop
    {
        get => _oneWaySlop;
        set
        {
            ArgGuard.ThrowIfNegativeOrNotFinite(value, nameof(OneWaySlop));
            _oneWaySlop = value;
        }
    }

    public float Mass
    {
        get => _mass;
        set
        {
            ArgGuard.ThrowIfNotPositive(value, nameof(Mass));
            _mass = value;
        }
    }

    public float MomentOfInertia
    {
        get => _momentOfInertia;
        set
        {
            ArgGuard.ThrowIfNotPositive(value, nameof(MomentOfInertia));
            _momentOfInertia = value;
        }
    }

    public float InverseMass => MotionType == BodyMotionType2D.Dynamic ? 1f / Mass : 0f;
    public float InverseInertia => MotionType == BodyMotionType2D.Dynamic ? 1f / MomentOfInertia : 0f;

    public void AddForce(Vector2 force) => AccumulatedForce += force;
    public void AddTorque(float torque) => AccumulatedTorque += torque;

    public void AddImpulse(Vector2 impulse)
    {
        if (MotionType == BodyMotionType2D.Dynamic)
            LinearVelocity += impulse * InverseMass;
    }

    public void AddAngularImpulse(float impulse)
    {
        if (MotionType == BodyMotionType2D.Dynamic)
            AngularVelocity += impulse * InverseInertia;
    }

    public void ClearAccumulators()
    {
        AccumulatedForce = Vector2.Zero;
        AccumulatedTorque = 0f;
    }

    public void IgnoreOneWayPlatform(PhysicsBody2D platform)
    {
        ArgGuard.ThrowIfNull(platform);
        (_ignoredOneWayPlatforms ??= []).Add(platform);
    }

    public bool IsIgnoringOneWayPlatform(PhysicsBody2D platform)
    {
        ArgGuard.ThrowIfNull(platform);
        return _ignoredOneWayPlatforms?.Contains(platform) == true;
    }

    public void ClearIgnoredOneWayPlatforms() =>
        _ignoredOneWayPlatforms?.Clear();

    public int IgnoredOneWayPlatformCount =>
        _ignoredOneWayPlatforms?.Count ?? 0;

    public void RemoveIgnoredOneWayPlatformsWhere(
        Predicate<PhysicsBody2D> match)
    {
        ArgGuard.ThrowIfNull(match);
        _ignoredOneWayPlatforms?.RemoveWhere(match);
    }

    public bool CanCollideWith(PhysicsBody2D other) =>
        IsCollider && other.IsCollider &&
        (CollisionMask & other.CollisionLayer) != 0u &&
        (other.CollisionMask & CollisionLayer) != 0u;
}
