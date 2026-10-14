using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled rectangle, axis-aligned in local space; the owning pose may rotate it in world space.</summary>
public class Rectangle2D : IConvexShape2D, IRect2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Rectangle;

    /// <summary>Creates a rectangle from ordered corners with positive extents.</summary>
    /// <param name="min">The finite lower-left corner.</param>
    /// <param name="max">The finite upper-right corner, component-wise greater than <paramref name="min"/>.</param>
    public Rectangle2D(Vector2 min, Vector2 max)
    {
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThan(min, max);
        Min = min;
        Max = max;
    }

    /// <inheritdoc/>
    public Vector2 Min { get; }

    /// <inheritdoc/>
    public Vector2 Max { get; }

    /// <inheritdoc/>
    public float Area => Area2D.Rectangle(Min, Max);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Rectangle(localPoint, Min, Max);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Rectangle(localDirection, Min, Max);

    /// <summary>Writes the four local-space corners counter-clockwise from Min.</summary>
    /// <param name="corners">A buffer of at least four entries.</param>
    public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);

    /// <summary>Creates a rectangle of a given size around a center.</summary>
    /// <param name="size">The finite, positive width and height.</param>
    /// <param name="center">The finite local center.</param>
    /// <returns>The rectangle spanning half the size on each side of the center.</returns>
    public static Rectangle2D FromSize(Vector2 size, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(size);
        ArgGuard.ThrowIfNotFinite(center);
        var halfSize = size / 2f;
        return new Rectangle2D(center - halfSize, center + halfSize);
    }

    /// <summary>Creates a rectangle of positive size around a center.</summary>
    public static Rectangle2D FromSize(Size2D size, Vector2 center = default)
    {
        ArgGuard.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
        return FromSize(size.ToVector2(), center);
    }

    /// <summary>Creates a rectangle from its minimum corner and positive size.</summary>
    public static Rectangle2D FromMinAndSize(Vector2 min, Size2D size)
    {
        ArgGuard.ThrowIfNotFinite(min);
        ArgGuard.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
        return new Rectangle2D(min, min + size.ToVector2());
    }
}
