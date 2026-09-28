using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Functions;

/// <summary>Bounds calculations on raw geometry. These functions do not retain or cache their results.</summary>
public static class BoundsGeometry2D
{
    public static Bounds2D FromCircle(Vector2 center, float radius)
    {
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        var extent = new Vector2(radius);
        return new(center - extent, center + extent);
    }

    public static Bounds2D FromCapsule(Vector2 start, Vector2 end, float radius)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        var extent = new Vector2(radius);
        return new(Vector2.Min(start, end) - extent, Vector2.Max(start, end) + extent);
    }

    public static Bounds2D FromRectangle<T>(T rectangle) where T : IRect2D => new(rectangle.Min, rectangle.Max);

    public static Bounds2D FromPoints(ReadOnlySpan<Vector2> points)
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

    public static Bounds2D Union(Bounds2D first, Bounds2D second) =>
        new(Vector2.Min(first.Min, second.Min), Vector2.Max(first.Max, second.Max));

    public static Bounds2D Translate(Bounds2D bounds, Vector2 translation) =>
        bounds.IsFinite ? new(bounds.Min + translation, bounds.Max + translation) : Bounds2D.Unbounded;

    /// <summary>No rotation or shear. Negative scales mirror the box; zero scales collapse the corresponding axis.</summary>
    public static Bounds2D ScaleAndTranslate(Bounds2D bounds, Vector2 scale, Vector2 translation)
    {
        if (!bounds.IsFinite) return Bounds2D.Unbounded;
        var first = bounds.Min * scale + translation;
        var second = bounds.Max * scale + translation;
        return new(Vector2.Min(first, second), Vector2.Max(first, second));
    }

    /// <summary>
    /// Encloses the transformed box. Uses translation/scaling paths when the matrix has no rotation or shear,
    /// otherwise transforms all four corners. This bounds the box, not the original shape inside it.
    /// Unbounded inputs stay unbounded, avoiding infinity * zero and preserving broad-phase candidates.
    /// </summary>
    public static Bounds2D Transform(Bounds2D bounds, Matrix3x2 transform)
    {
        if (!bounds.IsFinite) return Bounds2D.Unbounded;
        if (transform.M12 == 0f && transform.M21 == 0f)
        {
            return transform.M11 == 1f && transform.M22 == 1f
                ? Translate(bounds, transform.Translation)
                : ScaleAndTranslate(bounds, new(transform.M11, transform.M22), transform.Translation);
        }

        Span<Vector2> corners =
        [
            Vector2.Transform(bounds.Min, transform),
            Vector2.Transform(new Vector2(bounds.Max.X, bounds.Min.Y), transform),
            Vector2.Transform(bounds.Max, transform),
            Vector2.Transform(new Vector2(bounds.Min.X, bounds.Max.Y), transform),
        ];
        return FromPoints(corners);
    }
}
