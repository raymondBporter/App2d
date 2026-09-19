using System.Numerics;

namespace App2d.Core.Constraints;

public readonly record struct DistanceConstraintEvaluation2D(
    Vector2 Direction,
    float Distance,
    ConstraintLimitEvaluation1D Limit);

public readonly record struct AxisConstraintEvaluation2D(
    Vector2 Axis,
    float Translation,
    ConstraintLimitEvaluation1D Limit,
    Vector2 ProjectedPoint);

/// <summary>Pure geometric constraint calculations with no bodies or solver state.</summary>
public static class ConstraintMath2D
{
    private const float MinimumDirectionLengthSquared = 1e-8f;

    public static DistanceConstraintEvaluation2D EvaluateDistance(
        Vector2 first,
        Vector2 second,
        ConstraintLimit1D limits,
        float activationTolerance = 0f,
        Vector2 fallbackDirection = default)
    {
        ArgGuard.ThrowIfNotFinite(first);
        ArgGuard.ThrowIfNotFinite(second);
        ArgGuard.ThrowIfNotFinite(fallbackDirection);
        ValidateDistanceLimits(limits);

        var delta = second - first;
        var lengthSquared = delta.LengthSquared();
        float distance;
        Vector2 direction;
        if (lengthSquared > MinimumDirectionLengthSquared)
        {
            distance = MathF.Sqrt(lengthSquared);
            direction = delta / distance;
        }
        else
        {
            distance = MathF.Sqrt(lengthSquared);
            direction = fallbackDirection.LengthSquared() > MinimumDirectionLengthSquared
                ? Vector2.Normalize(fallbackDirection)
                : Vector2.UnitX;
        }

        return new DistanceConstraintEvaluation2D(
            direction,
            distance,
            limits.Evaluate(distance, activationTolerance));
    }

    public static Vector2 ProjectPointToDistance(
        Vector2 anchor,
        Vector2 requestedPoint,
        ConstraintLimit1D limits,
        Vector2 fallbackDirection = default)
    {
        var evaluation = EvaluateDistance(anchor, requestedPoint, limits, fallbackDirection: fallbackDirection);
        return anchor + evaluation.Direction * evaluation.Limit.Target;
    }

    public static AxisConstraintEvaluation2D EvaluateAxis(
        Vector2 anchor,
        Vector2 requestedPoint,
        Vector2 axis,
        ConstraintLimit1D translationLimits,
        float activationTolerance = 0f)
    {
        ArgGuard.ThrowIfNotFinite(anchor);
        ArgGuard.ThrowIfNotFinite(requestedPoint);
        ArgGuard.ThrowIfNotFiniteOrZero(axis);
        axis = Vector2.Normalize(axis);
        var translation = Vector2.Dot(requestedPoint - anchor, axis);
        var limit = translationLimits.Evaluate(translation, activationTolerance);
        return new AxisConstraintEvaluation2D(
            axis,
            translation,
            limit,
            anchor + axis * limit.Target);
    }

    public static Vector2 ProjectPointToAxis(
        Vector2 anchor,
        Vector2 requestedPoint,
        Vector2 axis,
        ConstraintLimit1D translationLimits) =>
        EvaluateAxis(anchor, requestedPoint, axis, translationLimits).ProjectedPoint;

    /// <summary>
    /// Projects a point onto a fixed-radius arc. Angles are unwrapped radians
    /// relative to +X; wrapping intervals across -PI/PI are intentionally not inferred.
    /// </summary>
    public static Vector2 ProjectPointToPolarArc(
        Vector2 anchor,
        Vector2 requestedPoint,
        float radius,
        ConstraintLimit1D angleLimits,
        float fallbackAngle = 0f)
    {
        ArgGuard.ThrowIfNotFinite(anchor);
        ArgGuard.ThrowIfNotFinite(requestedPoint);
        ArgGuard.ThrowIfNotPositive(radius);
        ArgGuard.ThrowIfNotFinite(fallbackAngle);
        var delta = requestedPoint - anchor;
        var angle = delta.LengthSquared() > MinimumDirectionLengthSquared
            ? MathF.Atan2(delta.Y, delta.X)
            : fallbackAngle;
        angle = angleLimits.Clamp(angle);
        return anchor + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    private static void ValidateDistanceLimits(ConstraintLimit1D limits)
    {
        if (limits.HasMinimum && limits.Minimum < 0f)
            throw new ArgumentOutOfRangeException(nameof(limits), limits,
                "A distance minimum cannot be negative.");
        if (limits.HasMaximum && limits.Maximum < 0f)
            throw new ArgumentOutOfRangeException(nameof(limits), limits,
                "A distance maximum cannot be negative.");
    }
}

