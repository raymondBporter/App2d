using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;
namespace App2d.Core.Constraints;

public enum ConstraintLimitState1D
{
    Inactive,
    Lower,
    Upper,
    Locked
}

public readonly record struct ConstraintLimitEvaluation1D(
    float Coordinate,
    float Target,
    float Error,
    ConstraintLimitState1D State)
{
    public bool IsActive => State != ConstraintLimitState1D.Inactive;
    public bool IsSatisfied => Error == 0f;
    public float Correction => -Error;
}

/// <summary>
/// Optional lower and upper bounds for one scalar constraint coordinate.
/// Negative/positive infinity represent disabled lower/upper limits.
/// </summary>
public readonly record struct ConstraintLimit1D : IInterval<float>
{
    public ConstraintLimit1D(float minimum, float maximum)
    {
        ArgGuard.ThrowIfNan(minimum, nameof(minimum));
        if (minimum == float.PositiveInfinity)
        {
            ArgGuard.ThrowOutOfRange(minimum, "The minimum must be finite or negative infinity.");
        }

        ArgGuard.ThrowIfNan(maximum, nameof(minimum));
        if (maximum == float.NegativeInfinity)
        {
            ArgGuard.ThrowOutOfRange(maximum, "The maximum must be finite or positive infinity.");
        }

        ArgGuard.ThrowIf(minimum > maximum, "The minimum limit cannot exceed the maximum limit.");

        Minimum = minimum;
        Maximum = maximum;
    }

    public float Minimum
    {
        get;
    }
    public float Maximum
    {
        get;
    }
    float IInterval<float>.Min => Minimum;
    float IInterval<float>.Max => Maximum;
    public bool HasMinimum => !float.IsNegativeInfinity(Minimum);
    public bool HasMaximum => !float.IsPositiveInfinity(Maximum);
    public bool IsFree => !HasMinimum && !HasMaximum;
    public bool IsLocked => Minimum == Maximum;

    public static ConstraintLimit1D Free { get; } = new(float.NegativeInfinity, float.PositiveInfinity);
    public static ConstraintLimit1D AtLeast(float minimum)
    {
        ArgGuard.ThrowIfNotFinite(minimum);
        return new ConstraintLimit1D(minimum, float.PositiveInfinity);
    }

    public static ConstraintLimit1D AtMost(float maximum)
    {
        ArgGuard.ThrowIfNotFinite(maximum);
        return new ConstraintLimit1D(float.NegativeInfinity, maximum);
    }

    public static ConstraintLimit1D Between(float minimum, float maximum)
    {
        ArgGuard.ThrowIfNotFinite(minimum);
        ArgGuard.ThrowIfNotFinite(maximum);
        return new ConstraintLimit1D(minimum, maximum);
    }

    public static ConstraintLimit1D Locked(float value)
    {
        ArgGuard.ThrowIfNotFinite(value);
        return new ConstraintLimit1D(value, value);
    }

    public float Clamp(float coordinate)
    {
        ArgGuard.ThrowIfNotFinite(coordinate);
        return IntervalMath<float>.Clamp(this, coordinate);
    }

    /// <summary>
    /// Evaluates a coordinate against the limits. Activation tolerance marks a
    /// unilateral limit active slightly before penetration for velocity solving;
    /// it does not alter the exact target or error.
    /// </summary>
    public ConstraintLimitEvaluation1D Evaluate(float coordinate, float activationTolerance = 0f)
    {
        ArgGuard.ThrowIfNotFinite(coordinate);
        ArgGuard.ThrowIfNotFiniteOrNegative(activationTolerance);
        var target = IntervalMath<float>.Clamp(this, coordinate);
        var state = ConstraintLimitState1D.Inactive;
        if (IsLocked)
        {
            state = ConstraintLimitState1D.Locked;
        }
        else if (HasMinimum && coordinate <= Minimum + activationTolerance)
        {
            state = ConstraintLimitState1D.Lower;
        }
        else if (HasMaximum && coordinate >= Maximum - activationTolerance)
        {
            state = ConstraintLimitState1D.Upper;
        }

        return new ConstraintLimitEvaluation1D(coordinate, target, coordinate - target, state);
    }
}

