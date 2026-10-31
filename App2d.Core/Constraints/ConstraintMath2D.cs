using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Constraints;

public readonly record struct DistanceConstraintEvaluation2D(
    Vector2 Direction,
    float Distance,
    ConstraintLimitEvaluation1D Limit);

public readonly record struct AxisConstraintEvaluation2D(Vector2 Axis, float Translation, ConstraintLimitEvaluation1D Limit, Vector2 ProjectedPoint);

/// <summary>Pure geometric constraint calculations with no bodies or solver state.</summary>
public static class ConstraintMath2D
{
    private const float MinimumDirectionLengthSquared = 1e-8f;

    public static DistanceConstraintEvaluation2D EvaluateDistance(Vector2 first, Vector2 second, Interval<float> limits, float activationTolerance = 0f, Vector2 fallbackDirection = default)
    {
        ArgGuard.Finite(first);
        ArgGuard.Finite(second);
        ArgGuard.Finite(fallbackDirection);
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

        return new DistanceConstraintEvaluation2D(direction, distance, ConstraintMath1D.EvaluateLimit(limits, distance, activationTolerance));
    }

    public static Vector2 ProjectPointToDistance(Vector2 anchor, Vector2 requestedPoint, Interval<float> limits, Vector2 fallbackDirection = default)
    {
        var evaluation = EvaluateDistance(anchor, requestedPoint, limits, fallbackDirection: fallbackDirection);
        return anchor + evaluation.Direction * evaluation.Limit.Target;
    }

    public static AxisConstraintEvaluation2D EvaluateAxis(Vector2 anchor, Vector2 requestedPoint, Vector2 axis, Interval<float> translationLimits, float activationTolerance = 0f)
    {
        ArgGuard.Finite(anchor);
        ArgGuard.Finite(requestedPoint);
        ArgGuard.NonZero(axis);
        axis = Vector2.Normalize(axis);
        var translation = Vector2.Dot(requestedPoint - anchor, axis);
        var limit = ConstraintMath1D.EvaluateLimit(translationLimits, translation, activationTolerance);
        return new AxisConstraintEvaluation2D(axis, translation, limit, anchor + axis * limit.Target);
    }

    public static Vector2 ProjectPointToAxis(Vector2 anchor, Vector2 requestedPoint, Vector2 axis, Interval<float> translationLimits) => EvaluateAxis(anchor, requestedPoint, axis, translationLimits).ProjectedPoint;

    /// <summary>
    /// Projects a point onto a fixed-radius arc. Angles are unwrapped radians
    /// relative to +X; wrapping intervals across -PI/PI are intentionally not inferred.
    /// </summary>
    public static Vector2 ProjectPointToPolarArc(Vector2 anchor, Vector2 requestedPoint, float radius, Interval<float> angleLimits, float fallbackAngle = 0f)
    {
        ArgGuard.Finite(anchor);
        ArgGuard.Finite(requestedPoint);
        ArgGuard.Positive(radius);
        ArgGuard.Finite(fallbackAngle);
        var delta = requestedPoint - anchor;
        var angle = delta.LengthSquared() > MinimumDirectionLengthSquared ? delta.AngleRadians : fallbackAngle;
        angle = angleLimits.Clamp(angle);
        return anchor + Polar2D.ToCartesian(radius, angle);
    }

    private static void ValidateDistanceLimits(Interval<float> limits)
    {
        if (limits.IsBoundedBelow && limits.Min < 0f)
        {
            ArgGuard.ThrowOutOfRange(limits, "A distance minimum cannot be negative.");
        }

        if (limits.IsBoundedAbove && limits.Max < 0f)
        {
            ArgGuard.ThrowOutOfRange(limits, "A distance maximum cannot be negative.");
        }
    }
}

