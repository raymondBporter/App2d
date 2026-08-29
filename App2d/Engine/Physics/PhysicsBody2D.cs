using System.Numerics;
using App2d.Engine.Collision;

namespace App2d.Engine.Physics;

public sealed class PhysicsBody2D
{
    private HashSet<PhysicsBody2D>? _ignoredOneWayPlatforms;
    private float _mass = 1f;
    private float _momentOfInertia = 1f;
    private float _oneWaySlop = 2f;

    private BodyMotionType2D _motionType;

    internal PhysicsBody2D(
        SpatialObject2D worldObject,
        BodyMotionType2D motionType,
        Collider2D collider)
    {
        WorldObject = ArgGuard.RequireNotNull(worldObject);
        Collider = ArgGuard.RequireNotNull(collider);
        _motionType = motionType;
        Collider.Mobility = ToColliderMobility(motionType);
        PreviousPosition = worldObject.Transform.Position;
        PreviousRotation = worldObject.Transform.Rotation;
    }

    public SpatialObject2D WorldObject { get; }
    public Collider2D Collider { get; }
    public BodyMotionType2D MotionType
    {
        get => _motionType;
        set
        {
            _motionType = value;
            Collider.Mobility = ToColliderMobility(value);
        }
    }
    public Vector2 LinearVelocity { get; set; }
    public float AngularVelocity { get; set; }
    public Vector2 AccumulatedForce { get; private set; }
    public float AccumulatedTorque { get; private set; }
    public Vector2 PreviousPosition { get; internal set; }
    public float PreviousRotation { get; internal set; }
    public float GravityScale { get; set; } = 1f;
    public float Restitution { get; set; } = 0.5f;
    public bool IsCollider
    {
        get => Collider.IsEnabled;
        set => Collider.IsEnabled = value;
    }
    public bool IsSensor
    {
        get => Collider.IsSensor;
        set => Collider.IsSensor = value;
    }
    public bool IsOneWayPlatform { get; set; }
    public uint CollisionLayer
    {
        get => Collider.CollisionLayer;
        set => Collider.CollisionLayer = value;
    }
    public uint CollisionMask
    {
        get => Collider.CollisionMask;
        set => Collider.CollisionMask = value;
    }
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
        Collider.CanCollideWith(other.Collider);

    private static ColliderMobility2D ToColliderMobility(BodyMotionType2D motionType) =>
        motionType == BodyMotionType2D.Static
            ? ColliderMobility2D.Static
            : ColliderMobility2D.Dynamic;
}
