using System.Numerics;

namespace App2d.Engine.Physics;

public sealed class PhysicsBody2D(WorldObject2D worldObject, BodyMotionType2D motionType)
{
    private float _mass = 1f;
    private float _momentOfInertia = 1f;
    private float _oneWaySlop = 2f;

    public WorldObject2D WorldObject { get; } = worldObject;
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
        set => _oneWaySlop = ValidateNonNegativeFinite(value, nameof(OneWaySlop));
    }

    public float Mass
    {
        get => _mass;
        set => _mass = ValidatePositiveFinite(value, nameof(Mass));
    }

    public float MomentOfInertia
    {
        get => _momentOfInertia;
        set => _momentOfInertia = ValidatePositiveFinite(value, nameof(MomentOfInertia));
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

    internal bool CanCollideWith(PhysicsBody2D other) =>
        IsCollider && other.IsCollider &&
        (CollisionMask & other.CollisionLayer) != 0u &&
        (other.CollisionMask & CollisionLayer) != 0u;

    private static float ValidatePositiveFinite(float value, string propertyName)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(propertyName, "Value must be positive and finite.");
        return value;
    }

    private static float ValidateNonNegativeFinite(float value, string propertyName)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(propertyName, "Value must be finite and non-negative.");
        return value;
    }
}
