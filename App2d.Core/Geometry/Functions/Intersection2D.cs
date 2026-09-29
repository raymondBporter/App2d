using App2d.Core.Validation;
using App2d.Core.Mathematics;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry.Functions;

/// <summary>
/// Intersections in one coordinate space. Includes touching origins and collinear overlap.
/// Uses double intermediates and exact parallel tests; no angular tolerance discards distant crossings.
/// </summary>
public static class Intersection2D
{
    /// <summary>Clips an infinite line to a finite axis-aligned rectangle, including edge and corner touches.</summary>
    public static bool TryClipToRectangle<TRect>(this Line2D line, TRect rectangle,
        out Vector2 start, out Vector2 end) where TRect : IRect2D =>
        TryClipLinearToRectangle(line.Origin, line.Direction, line.IsValid, false, rectangle, out start, out end);

    /// <summary>Clips a forward ray to a finite axis-aligned rectangle, including its origin.</summary>
    public static bool TryClipToRectangle<TRect>(this Ray2D ray, TRect rectangle,
        out Vector2 start, out Vector2 end) where TRect : IRect2D =>
        TryClipLinearToRectangle(ray.Origin, ray.Direction, ray.IsValid, true, rectangle, out start, out end);

    private static bool TryClipLinearToRectangle<TRect>(Vector2 origin, Vector2 direction, bool valid,
        bool forwardOnly, TRect rectangle, out Vector2 start, out Vector2 end) where TRect : IRect2D
    {
        if (!valid) throw new ArgumentException("The line or ray needs a valid direction.", nameof(direction));
        ArgGuard.ThrowIfNull(rectangle);
        ArgGuard.ThrowIf(!float.IsFinite(rectangle.Min.X) || !float.IsFinite(rectangle.Min.Y) ||
            !float.IsFinite(rectangle.Max.X) || !float.IsFinite(rectangle.Max.Y) ||
            rectangle.Min.X > rectangle.Max.X || rectangle.Min.Y > rectangle.Max.Y,
            "Rectangle bounds must be finite and ordered.", nameof(rectangle));

        var min = forwardOnly ? 0d : double.NegativeInfinity;
        var max = double.PositiveInfinity;
        if (!ClipAxis(origin.X, direction.X, rectangle.Min.X, rectangle.Max.X, ref min, ref max) ||
            !ClipAxis(origin.Y, direction.Y, rectangle.Min.Y, rectangle.Max.Y, ref min, ref max))
        {
            start = end = default;
            return false;
        }
        start = new((float)(origin.X + direction.X * min), (float)(origin.Y + direction.Y * min));
        end = new((float)(origin.X + direction.X * max), (float)(origin.Y + direction.Y * max));
        return true;
    }

    private static bool ClipAxis(double origin, double direction, double low, double high,
        ref double min, ref double max)
    {
        if (direction == 0d) return origin >= low && origin <= high;
        var first = (low - origin) / direction;
        var second = (high - origin) / direction;
        min = Math.Max(min, Math.Min(first, second));
        max = Math.Min(max, Math.Max(first, second));
        return min <= max;
    }

    public static bool Intersects(this Line2D first, Line2D second) =>
        Intersects(first.Origin, first.Direction, false, second.Origin, second.Direction, false);

    public static bool Intersects(this Line2D line, Ray2D ray) =>
        Intersects(line.Origin, line.Direction, false, ray.Origin, ray.Direction, true);

    public static bool Intersects(this Ray2D ray, Line2D line) => Intersects(line, ray);

    public static bool Intersects(this Ray2D first, Ray2D second) =>
        Intersects(first.Origin, first.Direction, true, second.Origin, second.Direction, true);

    private static bool Intersects(Vector2 firstOrigin, Vector2 firstDirection, bool firstIsRay,
        Vector2 secondOrigin, Vector2 secondDirection, bool secondIsRay)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(firstDirection);
        ArgGuard.ThrowIfNotFiniteOrZero(secondDirection);
        var dx = (double)secondOrigin.X - firstOrigin.X;
        var dy = (double)secondOrigin.Y - firstOrigin.Y;
        var determinant = CrossProduct2D.Of(firstDirection.X, firstDirection.Y, secondDirection.X, secondDirection.Y);
        var firstNumerator = CrossProduct2D.Of(dx, dy, secondDirection.X, secondDirection.Y);
        var secondNumerator = CrossProduct2D.Of(dx, dy, firstDirection.X, firstDirection.Y);
        if (determinant != 0)
            return (!firstIsRay || firstNumerator / determinant >= 0) &&
                   (!secondIsRay || secondNumerator / determinant >= 0);

        if (secondNumerator != 0) return false; // Parallel, distinct supporting lines.
        if (!firstIsRay || !secondIsRay) return true;
        // Collinear rays overlap if they face the same way or face toward one another.
        var sameDirection = (double)firstDirection.X * secondDirection.X + (double)firstDirection.Y * secondDirection.Y > 0;
        return sameDirection || dx * firstDirection.X + dy * firstDirection.Y >= 0;
    }

}
