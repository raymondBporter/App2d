using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Where a swept circle first touches a rectangle.</summary>
/// <param name="Time">The fraction of the sweep at first contact, in [0, 1]; zero when already touching at the start.</param>
/// <param name="Center">The circle center at first contact.</param>
/// <param name="SurfacePoint">The point on the rectangle surface that is touched.</param>
/// <param name="Normal">The unit outward normal of the touched rectangle face.</param>
public readonly record struct SweptCircleHit2D(float Time, Vector2 Center, Vector2 SurfacePoint, Vector2 Normal);

/// <summary>
/// Intersection and overlap tests between raw primitives in one coordinate space. Touching counts as intersecting.
/// Linear tests use double intermediates and exact parallel checks; no angular tolerance discards distant crossings.
/// </summary>
public static class Intersection2D
{
    private const float SweepParallelEpsilon = 1e-7f;

    /// <summary>Clips an infinite line to a finite axis-aligned rectangle, including edge and corner touches.</summary>
    /// <param name="line">A valid line.</param>
    /// <param name="rectangle">A finite, ordered rectangle.</param>
    /// <param name="start">The first clipped endpoint along the line direction.</param>
    /// <param name="end">The second clipped endpoint.</param>
    /// <returns>True when the line touches the rectangle.</returns>
    public static bool TryClipToRectangle<TRect>(this Line2D line, TRect rectangle, out Vector2 start, out Vector2 end) where TRect : IRect2D => TryClipLinearToRectangle(line.Origin, line.Direction, line.IsValid, false, rectangle, out start, out end);

    /// <summary>Clips a forward ray to a finite axis-aligned rectangle, keeping the origin when it starts inside.</summary>
    /// <param name="ray">A valid ray.</param>
    /// <param name="rectangle">A finite, ordered rectangle.</param>
    /// <param name="start">The first clipped point along the ray.</param>
    /// <param name="end">The point where the ray leaves the rectangle.</param>
    /// <returns>True when the ray touches the rectangle.</returns>
    public static bool TryClipToRectangle<TRect>(this Ray2D ray, TRect rectangle, out Vector2 start, out Vector2 end) where TRect : IRect2D => TryClipLinearToRectangle(ray.Origin, ray.Direction, ray.IsValid, true, rectangle, out start, out end);

    /// <summary>Tests whether two infinite lines share a point; coincident lines intersect.</summary>
    /// <param name="first">A valid line.</param>
    /// <param name="second">A valid line.</param>
    /// <returns>True unless the lines are parallel and distinct.</returns>
    public static bool Intersects(this Line2D first, Line2D second) => Intersects(first.Origin, first.Direction, false, second.Origin, second.Direction, false);

    /// <summary>Tests whether a line touches a forward ray, including at the ray origin.</summary>
    /// <param name="line">A valid line.</param>
    /// <param name="ray">A valid ray.</param>
    /// <returns>True when the ray reaches the line.</returns>
    public static bool Intersects(this Line2D line, Ray2D ray) => Intersects(line.Origin, line.Direction, false, ray.Origin, ray.Direction, true);

    /// <summary>Tests whether a forward ray touches a line, including at the ray origin.</summary>
    /// <param name="ray">A valid ray.</param>
    /// <param name="line">A valid line.</param>
    /// <returns>True when the ray reaches the line.</returns>
    public static bool Intersects(this Ray2D ray, Line2D line) => Intersects(line, ray);

    /// <summary>Tests whether two forward rays share a point, including collinear rays facing each other.</summary>
    /// <param name="first">A valid ray.</param>
    /// <param name="second">A valid ray.</param>
    /// <returns>True when both forward domains contain a common point.</returns>
    public static bool Intersects(this Ray2D first, Ray2D second) => Intersects(first.Origin, first.Direction, true, second.Origin, second.Direction, true);

    /// <summary>Tests whether two ordered rectangles overlap, including edge and corner contact. Infinite bounds are allowed.</summary>
    /// <param name="firstMin">The first lower-left corner.</param>
    /// <param name="firstMax">The first upper-right corner.</param>
    /// <param name="secondMin">The second lower-left corner.</param>
    /// <param name="secondMax">The second upper-right corner.</param>
    /// <returns>True when the rectangles share at least a point.</returns>
    public static bool RectanglesOverlap(Vector2 firstMin, Vector2 firstMax, Vector2 secondMin, Vector2 secondMax) => firstMin.X <= secondMax.X && firstMax.X >= secondMin.X && firstMin.Y <= secondMax.Y && firstMax.Y >= secondMin.Y;

    /// <summary>
    /// Separating-axis overlap of two convex perimeters with at least three vertices, in either winding.
    /// Offsets place each local perimeter in the same coordinate space without copying. Touching counts as overlap;
    /// repeated adjacent vertices are allowed and their zero-length edges are ignored.
    /// </summary>
    /// <param name="first">The first convex perimeter.</param>
    /// <param name="second">The second convex perimeter.</param>
    /// <param name="firstOffset">A translation applied to the first perimeter.</param>
    /// <param name="secondOffset">A translation applied to the second perimeter.</param>
    /// <returns>True when no edge normal separates the perimeters.</returns>
    public static bool ConvexPolygonsOverlap(IReadOnlyList<Vector2> first, IReadOnlyList<Vector2> second, Vector2 firstOffset = default, Vector2 secondOffset = default)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        ArgGuard.ThrowIf(first.Count < 3, "A convex perimeter requires at least three vertices.", nameof(first));
        ArgGuard.ThrowIf(second.Count < 3, "A convex perimeter requires at least three vertices.", nameof(second));
        return !Separated(first) && !Separated(second);

        bool Separated(IReadOnlyList<Vector2> perimeter)
        {
            for (var i = 0; i < perimeter.Count; i++)
            {
                var axis = (perimeter[(i + 1) % perimeter.Count] - perimeter[i]).PerpCcw;
                if (axis.LengthSquared() < 1e-12f) continue;
                var a = Projection2D.Polygon(first, axis, firstOffset);
                var b = Projection2D.Polygon(second, axis, secondOffset);
                if (a.Max < b.Min || b.Max < a.Min) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Sweeps a circle from one center to another and reports the first contact with a finite rectangle.
    /// A circle already touching at the start reports time zero with the nearest face normal.
    /// </summary>
    /// <param name="start">The circle center at the start of the sweep.</param>
    /// <param name="end">The circle center at the end of the sweep.</param>
    /// <param name="radius">The finite, nonnegative circle radius.</param>
    /// <param name="rectangle">The rectangle to sweep against; non-finite rectangles never hit.</param>
    /// <param name="hit">The first contact.</param>
    /// <returns>True when the swept circle touches the rectangle.</returns>
    public static bool TrySweptCircleRectangle<TRect>(Vector2 start, Vector2 end, float radius, TRect rectangle, out SweptCircleHit2D hit) where TRect : IRect2D
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        if (!rectangle.IsFinite)
        {
            hit = default;
            return false;
        }

        var expandedMin = rectangle.Min - new Vector2(radius);
        var expandedMax = rectangle.Max + new Vector2(radius);
        if (Containment2D.Rectangle(start, expandedMin, expandedMax))
        {
            var normal = NearestBoundaryNormal(start, expandedMin, expandedMax);
            var initialSurfacePoint = Vector2.Clamp(start - normal * radius, rectangle.Min, rectangle.Max);
            hit = new SweptCircleHit2D(0f, start, initialSurfacePoint, normal);
            return true;
        }

        var delta = end - start;
        var entryTime = 0f;
        var exitTime = 1f;
        var entryNormal = Vector2.Zero;
        if (!UpdateSlab(start.X, delta.X, expandedMin.X, expandedMax.X, -Vector2.UnitX, Vector2.UnitX, ref entryTime, ref exitTime, ref entryNormal) ||
            !UpdateSlab(start.Y, delta.Y, expandedMin.Y, expandedMax.Y, -Vector2.UnitY, Vector2.UnitY, ref entryTime, ref exitTime, ref entryNormal) ||
            entryTime < 0f || entryTime > 1f || entryNormal == Vector2.Zero)
        {
            hit = default;
            return false;
        }

        var center = Vector2.Lerp(start, end, entryTime);
        var surfacePoint = Vector2.Clamp(center - entryNormal * radius, rectangle.Min, rectangle.Max);
        hit = new SweptCircleHit2D(entryTime, center, surfacePoint, entryNormal);
        return true;
    }

    private static bool TryClipLinearToRectangle<TRect>(Vector2 origin, Vector2 direction, bool valid, bool forwardOnly, TRect rectangle, out Vector2 start, out Vector2 end) where TRect : IRect2D
    {
        if (!valid) throw new ArgumentException("The line or ray needs a valid direction.", nameof(direction));
        ArgGuard.ThrowIfNull(rectangle);
        ArgGuard.ThrowIf(!rectangle.IsFinite || rectangle.Min.X > rectangle.Max.X || rectangle.Min.Y > rectangle.Max.Y, "Rectangle bounds must be finite and ordered.", nameof(rectangle));

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

    private static bool ClipAxis(double origin, double direction, double low, double high, ref double min, ref double max)
    {
        if (direction == 0d) return origin >= low && origin <= high;
        var first = (low - origin) / direction;
        var second = (high - origin) / direction;
        min = Math.Max(min, Math.Min(first, second));
        max = Math.Min(max, Math.Max(first, second));
        return min <= max;
    }

    private static bool Intersects(Vector2 firstOrigin, Vector2 firstDirection, bool firstIsRay, Vector2 secondOrigin, Vector2 secondDirection, bool secondIsRay)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(firstDirection);
        ArgGuard.ThrowIfNotFiniteOrZero(secondDirection);
        var dx = (double)secondOrigin.X - firstOrigin.X;
        var dy = (double)secondOrigin.Y - firstOrigin.Y;
        var determinant = firstDirection.CrossDouble(secondDirection);
        var firstNumerator = Vector2Extensions.CrossDouble(dx, dy, secondDirection.X, secondDirection.Y);
        var secondNumerator = Vector2Extensions.CrossDouble(dx, dy, firstDirection.X, firstDirection.Y);
        if (determinant != 0)
            return (!firstIsRay || firstNumerator / determinant >= 0) && (!secondIsRay || secondNumerator / determinant >= 0);

        if (secondNumerator != 0) return false; // Parallel, distinct supporting lines.
        if (!firstIsRay || !secondIsRay) return true;
        // Collinear rays overlap if they face the same way or face toward one another.
        var sameDirection = (double)firstDirection.X * secondDirection.X + (double)firstDirection.Y * secondDirection.Y > 0;
        return sameDirection || dx * firstDirection.X + dy * firstDirection.Y >= 0;
    }

    private static bool UpdateSlab(float start, float delta, float minimum, float maximum, Vector2 minimumNormal, Vector2 maximumNormal, ref float entryTime, ref float exitTime, ref Vector2 entryNormal)
    {
        if (MathF.Abs(delta) <= SweepParallelEpsilon) return start >= minimum && start <= maximum;
        var firstTime = (minimum - start) / delta;
        var secondTime = (maximum - start) / delta;
        var firstNormal = minimumNormal;
        if (firstTime > secondTime)
        {
            (firstTime, secondTime) = (secondTime, firstTime);
            firstNormal = maximumNormal;
        }
        if (firstTime >= entryTime)
        {
            entryTime = firstTime;
            entryNormal = firstNormal;
        }
        exitTime = Math.Min(exitTime, secondTime);
        return entryTime <= exitTime;
    }

    private static Vector2 NearestBoundaryNormal(Vector2 point, Vector2 minimum, Vector2 maximum)
    {
        var nearestDistance = point.X - minimum.X;
        var nearestNormal = -Vector2.UnitX;
        if (maximum.X - point.X < nearestDistance)
        {
            nearestDistance = maximum.X - point.X;
            nearestNormal = Vector2.UnitX;
        }
        if (point.Y - minimum.Y < nearestDistance)
        {
            nearestDistance = point.Y - minimum.Y;
            nearestNormal = -Vector2.UnitY;
        }
        if (maximum.Y - point.Y < nearestDistance) nearestNormal = Vector2.UnitY;
        return nearestNormal;
    }
}
