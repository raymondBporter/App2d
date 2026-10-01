using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>
/// An axis-aligned rectangle holding only Min and Max, independent of the shape hierarchy.
/// Zero-width/height rectangles (including default) are valid and edges are inclusive.
/// Infinite bounds are allowed (see <see cref="Unbounded"/>); centers and edge midpoints are then undefined.
/// Dimensions, anchors, containment, distance and transform queries are the <see cref="IRect2D"/> extension members.
/// </summary>
public readonly record struct Rect2D : IRect2D
{
    /// <summary>The rectangle covering the whole plane, used for half-spaces and other unbounded geometry.</summary>
    public static Rect2D Unbounded { get; } = new(new Vector2(float.NegativeInfinity), new Vector2(float.PositiveInfinity));

    /// <summary>Creates a rectangle from ordered corners.</summary>
    /// <param name="min">The lower-left corner (smallest X and Y).</param>
    /// <param name="max">The upper-right corner; each component must be at least the matching component of <paramref name="min"/>.</param>
    /// <exception cref="ArgumentException">The corners are not ordered or contain NaN.</exception>
    [JsonConstructor]
    public Rect2D(Vector2 min, Vector2 max)
    {
        ArgGuard.ThrowIf(!NumericValidation.IsComponentWiseLessThanOrEqual(min, max),
            "Rectangle bounds must be ordered and cannot contain NaN.", nameof(max));
        Min = min;
        Max = max;
    }

    /// <summary>The lower-left corner.</summary>
    public Vector2 Min { get; }

    /// <summary>The upper-right corner.</summary>
    public Vector2 Max { get; }

    /// <summary>Creates a rectangle of a given size around a center.</summary>
    /// <param name="size">Finite, nonnegative width and height.</param>
    /// <param name="center">The finite center point.</param>
    /// <returns>The rectangle spanning half the size on each side of the center.</returns>
    public static Rect2D FromSize(Vector2 size, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(size);
        ArgGuard.ThrowIfNotFinite(center);
        var halfSize = size / 2f;
        return new(center - halfSize, center + halfSize);
    }

    /// <summary>Orders any two finite points into opposite corners.</summary>
    /// <param name="first">One corner.</param>
    /// <param name="second">The opposite corner, in any order relative to <paramref name="first"/>.</param>
    /// <returns>The smallest rectangle containing both points.</returns>
    public static Rect2D FromPoints(Vector2 first, Vector2 second)
    {
        ArgGuard.ThrowIfNotFinite(first);
        ArgGuard.ThrowIfNotFinite(second);
        return new(Vector2.Min(first, second), Vector2.Max(first, second));
    }

    /// <summary>The smallest rectangle containing every point.</summary>
    /// <param name="points">At least one point; NaN components are rejected by the constructor.</param>
    /// <returns>The bounding box of the points.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The span is empty.</exception>
    public static Rect2D FromPoints(ReadOnlySpan<Vector2> points)
    {
        ArgGuard.ThrowIfTooShort(points, 1);
        var min = points[0];
        var max = points[0];
        foreach (var point in points[1..])
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return new(min, max);
    }

    /// <summary>The bounding box of a circle.</summary>
    /// <param name="center">The finite circle center.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <returns>The square of side 2 * radius around the center.</returns>
    public static Rect2D FromCircle(Vector2 center, float radius)
    {
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        var extent = new Vector2(radius);
        return new(center - extent, center + extent);
    }

    /// <summary>The bounding box of a capsule, in either endpoint order.</summary>
    /// <param name="start">One finite spine endpoint.</param>
    /// <param name="end">The other finite spine endpoint.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <returns>The box around the spine expanded by the radius on every side.</returns>
    public static Rect2D FromCapsule(Vector2 start, Vector2 end, float radius)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        var extent = new Vector2(radius);
        return new(Vector2.Min(start, end) - extent, Vector2.Max(start, end) + extent);
    }
}
