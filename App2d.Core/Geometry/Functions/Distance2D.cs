using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Euclidean distance and signed distance in input units. Distance is zero for overlapping solids.
/// Signed distance is positive when separated, zero at contact and negative inside/overlapping;
/// for two convex solids its magnitude is the shortest translation needed to reach contact.
/// Raw queries assume finite inputs, nonnegative radii and ordered rectangle bounds.
/// </summary>
public static partial class Distance2D
{
    public static float Distance(Vector2 first, Vector2 second) => Vector2.Distance(first, second);

    public static float Distance(Interval1D first, Interval1D second) => Math.Max(0f, SignedDistance(first, second));

    public static float SignedDistance(Interval1D first, Interval1D second) => SignedDistance(first, second, out _);

    /// <summary>Direction is +1 or -1: the direction to move the first interval out of the second.</summary>
    public static float SignedDistance(Interval1D first, Interval1D second, out float direction)
    {
        var pushPositive = second.Max - first.Min;
        var pushNegative = first.Max - second.Min;
        direction = pushPositive < pushNegative ? 1f : -1f;
        return -Math.Min(pushPositive, pushNegative);
    }

    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end,
        float degenerateLengthSquared = float.Epsilon) =>
        Vector2.Distance(point, (end - start).LengthSquared() < degenerateLengthSquared
            ? start : ClosestPoint2D.OnSegment(point, start, end));

    public static float DistanceBetweenSegments(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        var closest = ClosestPoint2D.BetweenSegments(firstStart, firstEnd, secondStart, secondEnd);
        return Vector2.Distance(closest.First, closest.Second);
    }

    public static float SignedDistanceToCircle(Vector2 point, Vector2 center, float radius) =>
        Vector2.Distance(point, center) - radius;

    public static float DistanceToCircle(Vector2 point, Vector2 center, float radius) =>
        Math.Max(0f, SignedDistanceToCircle(point, center, radius));

    public static float SignedDistanceBetweenCircles(Vector2 firstCenter, float firstRadius, Vector2 secondCenter, float secondRadius) =>
        Vector2.Distance(firstCenter, secondCenter) - (firstRadius + secondRadius);

    public static float DistanceBetweenCircles(Vector2 firstCenter, float firstRadius, Vector2 secondCenter, float secondRadius) =>
        Math.Max(0f, SignedDistanceBetweenCircles(firstCenter, firstRadius, secondCenter, secondRadius));

    public static float SignedDistanceToCapsule(Vector2 point, Vector2 start, Vector2 end, float radius) =>
        DistanceToSegment(point, start, end) - radius;

    public static float DistanceToCapsule(Vector2 point, Vector2 start, Vector2 end, float radius) =>
        Math.Max(0f, SignedDistanceToCapsule(point, start, end, radius));

    public static float SignedDistanceToRectangle(Vector2 point, Vector2 min, Vector2 max)
    {
        var outside = Vector2.Max(Vector2.Max(min - point, point - max), Vector2.Zero);
        if (outside != Vector2.Zero) return outside.Length();
        return -Math.Min(Math.Min(point.X - min.X, max.X - point.X), Math.Min(point.Y - min.Y, max.Y - point.Y));
    }

    public static float DistanceToRectangle(Vector2 point, Vector2 min, Vector2 max) =>
        Math.Max(0f, SignedDistanceToRectangle(point, min, max));

    /// <summary>The half-space is dot(point, unitNormal) &lt;= offset; the normal must be unit length.</summary>
    public static float SignedDistanceToHalfSpace(Vector2 point, Vector2 unitNormal, float offset) =>
        Vector2.Dot(point, unitNormal) - offset;

    public static float DistanceToHalfSpace(Vector2 point, Vector2 unitNormal, float offset) =>
        Math.Max(0f, SignedDistanceToHalfSpace(point, unitNormal, offset));

    public static float SignedDistanceToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices) =>
        SignedDistanceToConvexPolygon(point, vertices, out _, out _);

    public static float DistanceToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices) =>
        Math.Max(0f, SignedDistanceToConvexPolygon(point, vertices));

    /// <summary>
    /// Convex perimeter in either winding, with at least three vertices and nonzero area.
    /// Also returns the nearest boundary point and its edge for contact generation.
    /// The optional cross-product tolerance preserves callers' existing containment policy; zero gives the geometric sign.
    /// </summary>
    public static float SignedDistanceToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices,
        out Vector2 closestPoint, out int edgeIndex, float collinearEpsilon = 0f)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        closestPoint = PolygonGeometry2D.ClosestPointOnPerimeter(point, vertices, out edgeIndex);
        var distance = Vector2.Distance(point, closestPoint);
        return PolygonGeometry2D.ContainsPoint(vertices, point, collinearEpsilon) ? -distance : distance;
    }

    public static float SignedDistance(Vector2 point, Rect2D rectangle) => SignedDistanceToRectangle(point, rectangle.Min, rectangle.Max);
    public static float SignedDistance(Rect2D rectangle, Vector2 point) => SignedDistance(point, rectangle);
    public static float Distance(Vector2 point, Rect2D rectangle) => Math.Max(0f, SignedDistance(point, rectangle));
    public static float Distance(Rect2D rectangle, Vector2 point) => Distance(point, rectangle);

    public static float SignedDistance(Rect2D first, Rect2D second)
    {
        var x = SignedDistance(new Interval1D(first.Min.X, first.Max.X), new Interval1D(second.Min.X, second.Max.X));
        var y = SignedDistance(new Interval1D(first.Min.Y, first.Max.Y), new Interval1D(second.Min.Y, second.Max.Y));
        return x > 0f || y > 0f ? new Vector2(Math.Max(0f, x), Math.Max(0f, y)).Length() : Math.Max(x, y);
    }

    public static float Distance(Rect2D first, Rect2D second) => Math.Max(0f, SignedDistance(first, second));
}
