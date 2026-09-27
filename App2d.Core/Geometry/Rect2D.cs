using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// A rectangle value containing only Min and Max, independent of the shape hierarchy.
/// Zero-width/height rectangles (including default) are valid. Edges are inclusive.
/// Infinite bounds are allowed; centers and edge midpoints may then be undefined.
/// </summary>
public readonly record struct Rect2D : IRect2D
{
    public Rect2D(Vector2 min, Vector2 max)
    {
        ArgGuard.ThrowIf(!NumericValidation.IsComponentWiseLessThanOrEqual(min, max),
            "Rectangle bounds must be ordered and cannot contain NaN.", nameof(max));
        Min = min;
        Max = max;
    }

    public Vector2 Min { get; }
    public Vector2 Max { get; }

    public static Rect2D FromSize(Vector2 size, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(size);
        ArgGuard.ThrowIfNotFinite(center);
        var halfSize = size / 2f;
        return new(center - halfSize, center + halfSize);
    }

    /// <summary>Orders any two finite points into opposite corners.</summary>
    public static Rect2D FromPoints(Vector2 first, Vector2 second)
    {
        ArgGuard.ThrowIfNotFinite(first);
        ArgGuard.ThrowIfNotFinite(second);
        return new(Vector2.Min(first, second), Vector2.Max(first, second));
    }
}
