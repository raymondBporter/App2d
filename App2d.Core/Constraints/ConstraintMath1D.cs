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

public readonly record struct ConstraintLimitEvaluation1D(float Coordinate, float Target, float Error, ConstraintLimitState1D State)
{
    public bool IsActive => State != ConstraintLimitState1D.Inactive;
    public bool IsSatisfied => Error == 0f;
    public float Correction => -Error;
}

/// <summary>Scalar constraint calculations with no bodies or solver state.</summary>
public static class ConstraintMath1D
{
    /// <summary>
    /// Evaluates a finite coordinate against an interval. Activation tolerance marks a
    /// unilateral limit active slightly before penetration for velocity solving;
    /// it does not alter the exact target or error.
    /// </summary>
    public static ConstraintLimitEvaluation1D EvaluateLimit(Interval<float> limits, float coordinate, float activationTolerance = 0f)
    {
        ArgGuard.Finite(coordinate);
        ArgGuard.NonNegative(activationTolerance);
        var target = limits.Clamp(coordinate);
        var state = ConstraintLimitState1D.Inactive;
        if (limits.IsSingleton)
        {
            state = ConstraintLimitState1D.Locked;
        }
        else if (limits.IsBoundedBelow && coordinate <= limits.Min + activationTolerance)
        {
            state = ConstraintLimitState1D.Lower;
        }
        else if (limits.IsBoundedAbove && coordinate >= limits.Max - activationTolerance)
        {
            state = ConstraintLimitState1D.Upper;
        }

        return new ConstraintLimitEvaluation1D(coordinate, target, coordinate - target, state);
    }
}
