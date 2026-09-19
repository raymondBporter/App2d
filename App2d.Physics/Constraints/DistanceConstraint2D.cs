using App2d.Core;
using App2d.Core.Constraints;
using System.Numerics;

namespace App2d.Physics.Constraints;

public sealed class DistanceConstraint2D : IPhysicsConstraint2D
{
    private ConstraintLimit1D _limits;
    private float _positionStrength = 1f;
    private float _velocityStrength = 1f;
    private float _positionTolerance = 0.01f;

    public DistanceConstraint2D(PhysicsBody2D first, PhysicsBody2D second, float restLength)
        : this(first, second, ConstraintLimit1D.Locked(restLength))
    {
    }

    public DistanceConstraint2D(
        PhysicsBody2D first,
        PhysicsBody2D second,
        ConstraintLimit1D limits)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        ArgGuard.ThrowIfSameReference(first, second, "A distance constraint requires two different bodies.");

        First = first;
        Second = second;
        Limits = limits;
    }

    public PhysicsBody2D First { get; }
    public PhysicsBody2D Second { get; }
    public bool IsEnabled { get; set; } = true;
    public ConstraintLimit1D Limits
    {
        get => _limits;
        set
        {
            if (value.HasMinimum && value.Minimum < 0f || value.HasMaximum && value.Maximum < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Distance limits cannot be negative.");
            _limits = value;
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

        var inverseMassSum = First.InverseMass + Second.InverseMass;
        if (inverseMassSum <= 0f)
            return;

        var evaluation = ConstraintMath2D.EvaluateDistance(
            First.WorldObject.Transform.Position,
            Second.WorldObject.Transform.Position,
            Limits,
            PositionTolerance);
        var relativeSpeed = Vector2.Dot(
            Second.LinearVelocity - First.LinearVelocity,
            evaluation.Direction);
        var shouldCorrect = evaluation.Limit.State switch
        {
            ConstraintLimitState1D.Locked => true,
            ConstraintLimitState1D.Lower => relativeSpeed < 0f,
            ConstraintLimitState1D.Upper => relativeSpeed > 0f,
            _ => false
        };
        if (!shouldCorrect)
            return;

        var correctionImpulse = evaluation.Direction * (relativeSpeed * VelocityStrength / inverseMassSum);
        First.LinearVelocity += correctionImpulse * First.InverseMass;
        Second.LinearVelocity -= correctionImpulse * Second.InverseMass;
    }

    public bool SolvePosition(float deltaSeconds)
    {
        if (!IsEnabled)
            return false;

        var inverseMassSum = First.InverseMass + Second.InverseMass;
        if (inverseMassSum <= 0f)
            return false;

        var evaluation = ConstraintMath2D.EvaluateDistance(
            First.WorldObject.Transform.Position,
            Second.WorldObject.Transform.Position,
            Limits,
            PositionTolerance);
        if (MathF.Abs(evaluation.Limit.Error) <= PositionTolerance)
            return false;

        var correction = evaluation.Direction * (evaluation.Limit.Error * PositionStrength / inverseMassSum);
        First.WorldObject.Transform.Position += correction * First.InverseMass;
        Second.WorldObject.Transform.Position -= correction * Second.InverseMass;
        return true;
    }
}
