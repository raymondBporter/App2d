using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Constraints;

public readonly record struct DistanceConstraintEvaluation2D(
    Vector2f Direction,
    float Distance,
    ConstraintLimitEvaluation1D Limit);

public readonly record struct AxisConstraintEvaluation2D(Vector2f Axis, float Translation, ConstraintLimitEvaluation1D Limit, Vector2f ProjectedPoint);

/// <summary>Pure geometric constraint calculations with no bodies or solver state.</summary>
public static class ConstraintMath2D
{
    private const float MinimumDirectionLengthSquared = 1e-8f;

    public static DistanceConstraintEvaluation2D EvaluateDistance(Vector2f first, Vector2f second, Interval<float> limits, float activationTolerance = 0f, Vector2f fallbackDirection = default)
    {
        Ensure.Finite(first);
        Ensure.Finite(second);
        Ensure.Finite(fallbackDirection);
        ValidateDistanceLimits(limits);

        var delta = second - first;
        var lengthSquared = delta.LengthSquared();
        float distance;
        Vector2f direction;
        if (lengthSquared > MinimumDirectionLengthSquared)
        {
            distance = MathF.Sqrt(lengthSquared);
            direction = delta / distance;
        }
        else
        {
            distance = MathF.Sqrt(lengthSquared);
            direction = fallbackDirection.LengthSquared() > MinimumDirectionLengthSquared
                ? Vector2f.Normalize(fallbackDirection)
                : Vector2f.UnitX;
        }

        return new DistanceConstraintEvaluation2D(direction, distance, ConstraintMath1D.EvaluateLimit(limits, distance, activationTolerance));
    }

    public static Vector2f  ProjectPointToDistance(Vector2f anchor, Vector2f requestedPoint, Interval<float> limits, Vector2f fallbackDirection = default)
    {
        var evaluation = EvaluateDistance(anchor, requestedPoint, limits, fallbackDirection: fallbackDirection);
        return anchor + evaluation.Direction * evaluation.Limit.Target;
    }

    public static AxisConstraintEvaluation2D EvaluateAxis(Vector2f anchor, Vector2f requestedPoint, Vector2f axis, Interval<float> translationLimits, float activationTolerance = 0f)
    {
        Ensure.Finite(anchor);
        Ensure.Finite(requestedPoint);
        Ensure.NonZero(axis);
        axis = Vector2f.Normalize(axis);
        var translation = Vector2f.Dot(requestedPoint - anchor, axis);
        var limit = ConstraintMath1D.EvaluateLimit(translationLimits, translation, activationTolerance);
        return new AxisConstraintEvaluation2D(axis, translation, limit, anchor + axis * limit.Target);
    }

    public static Vector2f  ProjectPointToAxis(Vector2f anchor, Vector2f requestedPoint, Vector2f axis, Interval<float> translationLimits) => EvaluateAxis(anchor, requestedPoint, axis, translationLimits).ProjectedPoint;

    /// <summary>
    /// Projects a point onto a fixed-radius arc. Angles are unwrapped radians
    /// relative to +X; wrapping intervals across -PI/PI are intentionally not inferred.
    /// </summary>
    public static Vector2f  ProjectPointToPolarArc(Vector2f anchor, Vector2f requestedPoint, float radius, Interval<float> angleLimits, float fallbackAngle = 0f)
    {
        Ensure.Finite(anchor);
        Ensure.Finite(requestedPoint);
        Ensure.Positive(radius);
        Ensure.Finite(fallbackAngle);
        var delta = requestedPoint - anchor;
        var angle = delta.LengthSquared() > MinimumDirectionLengthSquared ? delta.AngleRadians : fallbackAngle;
        angle = angleLimits.Clamp(angle);
        return anchor + Vector2f.FromPolar(radius, angle);
    }

    private static void ValidateDistanceLimits(Interval<float> limits)
    {
        if (limits.IsBoundedBelow && limits.Min < 0f)
        {
            Ensure.ThrowOutOfRange(limits, "A distance minimum cannot be negative.");
        }

        if (limits.IsBoundedAbove && limits.Max < 0f)
        {
            Ensure.ThrowOutOfRange(limits, "A distance maximum cannot be negative.");
        }
    }
}

