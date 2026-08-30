using System.Numerics;

namespace App2d.Engine.Physics.Constraints;

public sealed class DistanceConstraint2D : IPhysicsConstraint2D
{
    private const float MinimumDirectionLengthSquared = 1e-8f;
    private float _restLength;
    private float _positionStrength = 1f;
    private float _velocityStrength = 1f;
    private float _positionTolerance = 0.01f;

    public DistanceConstraint2D(PhysicsBody2D first, PhysicsBody2D second, float restLength)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        ArgGuard.ThrowIfSameReference(first, second, "A distance constraint requires two different bodies.");

        First = first;
        Second = second;
        RestLength = restLength;
    }

    public PhysicsBody2D First { get; }
    public PhysicsBody2D Second { get; }
    public bool IsEnabled { get; set; } = true;
    public DistanceConstraintMode2D Mode { get; set; } = DistanceConstraintMode2D.Rod;

    public float RestLength
    {
        get => _restLength;
        set
        {
            ArgGuard.ThrowIfNegativeOrNotFinite(value, nameof(RestLength));
            _restLength = value;
        }
    }

    // One means a full local projection. Smaller values make a softer constraint.
    public float PositionStrength
    {
        get => _positionStrength;
        set
        {
            ArgGuard.ThrowIfNotInClosedRange(value, 0f, 1f, nameof(PositionStrength));
            _positionStrength = value;
        }
    }

    // One removes all relative velocity along the constraint axis.
    public float VelocityStrength
    {
        get => _velocityStrength;
        set
        {
            ArgGuard.ThrowIfNotInClosedRange(value, 0f, 1f, nameof(VelocityStrength));
            _velocityStrength = value;
        }
    }

    public float PositionTolerance
    {
        get => _positionTolerance;
        set
        {
            ArgGuard.ThrowIfNegativeOrNotFinite(value, nameof(PositionTolerance));
            _positionTolerance = value;
        }
    }

    public void SolveVelocity(float deltaSeconds)
    {
        if (!IsEnabled)
            return;

        var delta = Second.WorldObject.Transform.Position - First.WorldObject.Transform.Position;
        var lengthSquared = delta.LengthSquared();
        var inverseMassSum = First.InverseMass + Second.InverseMass;
        if (lengthSquared <= MinimumDirectionLengthSquared || inverseMassSum <= 0f)
            return;

        var length = MathF.Sqrt(lengthSquared);
        var direction = delta / length;
        var relativeSpeed = Vector2.Dot(Second.LinearVelocity - First.LinearVelocity, direction);
        if (Mode == DistanceConstraintMode2D.Rope &&
            (length < RestLength || relativeSpeed <= 0f))
        {
            return;
        }

        var correctionImpulse = direction * (relativeSpeed * VelocityStrength / inverseMassSum);
        First.LinearVelocity += correctionImpulse * First.InverseMass;
        Second.LinearVelocity -= correctionImpulse * Second.InverseMass;
    }

    public bool SolvePosition(float deltaSeconds)
    {
        if (!IsEnabled)
            return false;

        var delta = Second.WorldObject.Transform.Position - First.WorldObject.Transform.Position;
        var lengthSquared = delta.LengthSquared();
        var inverseMassSum = First.InverseMass + Second.InverseMass;

        if (lengthSquared <= MinimumDirectionLengthSquared || inverseMassSum <= 0f)
            return false;

        var length = MathF.Sqrt(lengthSquared);
        var error = length - RestLength;

        if (Mode == DistanceConstraintMode2D.Rope ? error <= PositionTolerance : MathF.Abs(error) <= PositionTolerance)
            return false;

        var direction = delta / length;
        var correction = direction * (error * PositionStrength / inverseMassSum);
        First.WorldObject.Transform.Position += correction * First.InverseMass;
        Second.WorldObject.Transform.Position -= correction * Second.InverseMass;
        return true;
    }
}
