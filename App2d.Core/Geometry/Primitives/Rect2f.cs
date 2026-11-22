using App2d.Core.Validation;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// An axis-aligned rectangle holding only Min and Max, independent of the shape hierarchy.
/// Zero-width/height rectangles (including default) are valid and edges are inclusive.
/// Infinite bounds are allowed (see <see cref="Unbounded"/>); centers and edge midpoints are then undefined.
/// Dimensions, anchors, containment, distance and transform queries use the shared rectangle extension members.
/// </summary>
public readonly record struct Rect2f : IRect2f
{
    /// <summary>The rectangle covering the whole plane, used for half-spaces and other unbounded geometry.</summary>
    public static Rect2f Unbounded { get; } = Builder.CreateUnbounded(new Vector2f(float.NegativeInfinity), new Vector2f(float.PositiveInfinity));

    /// <summary>Stores corners for deserialization. Use Builder.Create for validated finite bounds.</summary>
    [JsonConstructor]
    public Rect2f(Vector2f min, Vector2f max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>The lower-left corner.</summary>
    public Vector2f Min { get; }

    /// <summary>The upper-right corner.</summary>
    public Vector2f Max { get; }

    public void Validate(bool allowUnbounded = false) => Rect2<Vector2f, float>.Validate(Min, Max, allowUnbounded);

    public static class Builder
    {
        public static Rect2f Create(Vector2f min, Vector2f max)
        {
            Ensure.LessThanOrEqual(min, max);
            return new(min, max);
        }

        public static Rect2f CreateUnbounded(Vector2f min, Vector2f max)
        {
            Rect2<Vector2f, float>.Validate(min, max, allowUnbounded: true);
            return new(min, max);
        }
    }

    /// <summary>Creates a rectangle of a given size around a center.</summary>
    /// <param name="size">Finite, nonnegative width and height.</param>
    /// <param name="center">The finite center point.</param>
    /// <returns>The rectangle spanning half the size on each side of the center.</returns>
    public static Rect2f FromSize(Vector2f size, Vector2f center = default)
    {
        Ensure.NonNegative(size);
        Ensure.Finite(center);
        var halfSize = size / 2f;
        return Builder.Create(center - halfSize, center + halfSize);
    }

    /// <summary>Creates a rectangle of positive size around a center.</summary>
    public static Rect2f FromSize(Size2D size, Vector2f center = default)
    {
        Ensure.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
        return FromSize(size.ToVector2(), center);
    }

    /// <summary>Creates a rectangle from its minimum corner and positive size.</summary>
    public static Rect2f FromMinAndSize(Vector2f min, Size2D size)
    {
        Ensure.Finite(min);
        Ensure.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
        var max = min + size.ToVector2();
        Ensure.Finite(max);
        return Builder.Create(min, max);
    }

    /// <summary>Orders any two finite points into opposite corners.</summary>
    /// <param name="first">One corner.</param>
    /// <param name="second">The opposite corner, in any order relative to <paramref name="first"/>.</param>
    /// <returns>The smallest rectangle containing both points.</returns>
    public static Rect2f FromPoints(Vector2f first, Vector2f second)
    {
        Ensure.Finite(first);
        Ensure.Finite(second);
        return Builder.Create(Vector2f.Min(first, second), Vector2f.Max(first, second));
    }

    /// <summary>The smallest rectangle containing every point.</summary>
    /// <param name="points">At least one finite point.</param>
    /// <returns>The bounding box of the points.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The span is empty.</exception>
    public static Rect2f FromPoints(ReadOnlySpan<Vector2f> points)
    {
        Ensure.ThrowIfTooShort(points, 1);
        var min = points[0];
        var max = points[0];
        foreach (var point in points[1..])
        {
            min = Vector2f.Min(min, point);
            max = Vector2f.Max(max, point);
        }
        return Builder.Create(min, max);
    }

    /// <summary>The bounding box of a nonempty point sequence, read once without copying it.</summary>
    public static Rect2f FromPoints(IEnumerable<Vector2f> points)
    {
        Ensure.ThrowIfNull(points);
        using var enumerator = points.GetEnumerator();
        Ensure.ThrowIf(!enumerator.MoveNext(), "At least one point is required.", nameof(points));
        var min = enumerator.Current;
        var max = min;
        while (enumerator.MoveNext())
        {
            min = Vector2f.Min(min, enumerator.Current);
            max = Vector2f.Max(max, enumerator.Current);
        }
        return Builder.Create(min, max);
    }

    /// <summary>Bounds of projected items. A static selector and explicit state avoid iterator and closure allocations.</summary>
    public static Rect2f FromPoints<T, TState>(IReadOnlyList<T> items, TState state, Func<T, TState, Vector2f> pointOf)
    {
        Ensure.ThrowIfNull(items);
        Ensure.ThrowIfNull(pointOf);
        Ensure.ThrowIf(items.Count == 0, "At least one point is required.", nameof(items));
        var min = pointOf(items[0], state);
        var max = min;
        for (var i = 1; i < items.Count; i++)
        {
            var point = pointOf(items[i], state);
            min = Vector2f.Min(min, point);
            max = Vector2f.Max(max, point);
        }
        return Builder.Create(min, max);
    }

    /// <summary>The bounding box of a circle.</summary>
    /// <param name="center">The finite circle center.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <returns>The square of side 2 * radius around the center.</returns>
    public static Rect2f FromCircle(Vector2f center, float radius)
    {
        Ensure.Finite(center);
        Ensure.NonNegative(radius);
        var extent = new Vector2f(radius);
        return Builder.Create(center - extent, center + extent);
    }

    /// <summary>The bounding box of a capsule, in either endpoint order.</summary>
    /// <param name="start">One finite spine endpoint.</param>
    /// <param name="end">The other finite spine endpoint.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <returns>The box around the spine expanded by the radius on every side.</returns>
    public static Rect2f FromCapsule(Vector2f start, Vector2f end, float radius)
    {
        Ensure.Finite(start);
        Ensure.Finite(end);
        Ensure.NonNegative(radius);
        var extent = new Vector2f(radius);
        return Builder.Create(Vector2f.Min(start, end) - extent, Vector2f.Max(start, end) + extent);
    }
}
