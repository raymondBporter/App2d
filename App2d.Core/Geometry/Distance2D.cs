using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Euclidean distance and signed distance between raw primitives in one coordinate space.
/// Distance is zero for touching or overlapping solids. Signed distance is positive when separated,
/// zero at contact and negative inside or overlapping; for two convex solids its magnitude is the
/// shortest translation needed to reach contact. Squared variants exist only where they avoid a square root:
/// radius-subtracting and signed queries have no squared form. Inputs are finite, radii nonnegative and
/// rectangle bounds ordered. Shape and spatial-object overloads live in <c>App2d.Core.Shapes.ShapeDistance2D</c>.
/// </summary>
public static partial class Distance2D
{
    /// <summary>The distance between two points.</summary>
    /// <param name="first">The first point.</param>
    /// <param name="second">The second point.</param>
    /// <returns>The Euclidean distance.</returns>
    public static float Distance(Vector2 first, Vector2 second) => Vector2.Distance(first, second);

    /// <summary>The squared distance between two points.</summary>
    /// <param name="first">The first point.</param>
    /// <param name="second">The second point.</param>
    /// <returns>The squared Euclidean distance.</returns>
    public static float DistanceSquared(Vector2 first, Vector2 second) => Vector2.DistanceSquared(first, second);

    /// <summary>The gap between two intervals; zero when they overlap or touch.</summary>
    /// <param name="first">The first interval.</param>
    /// <param name="second">The second interval.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float Distance(Interval1D first, Interval1D second) => Math.Max(0f, SignedDistance(first, second));

    /// <summary>The signed gap between two intervals: positive when apart, negative by the overlap depth.</summary>
    /// <param name="first">The first interval.</param>
    /// <param name="second">The second interval.</param>
    /// <returns>The gap, or the negated minimum overlap.</returns>
    public static float SignedDistance(Interval1D first, Interval1D second) => SignedDistance(first, second, out _);

    /// <summary>The signed gap between two intervals and the direction that separates them fastest.</summary>
    /// <param name="first">The interval to move.</param>
    /// <param name="second">The interval to move out of.</param>
    /// <param name="direction">+1 or -1: the direction to move the first interval out of the second.</param>
    /// <returns>The gap, or the negated minimum overlap.</returns>
    public static float SignedDistance(Interval1D first, Interval1D second, out float direction)
    {
        var pushPositive = second.Max - first.Min;
        var pushNegative = first.Max - second.Min;
        direction = pushPositive < pushNegative ? 1f : -1f;
        return -Math.Min(pushPositive, pushNegative);
    }

    /// <summary>The distance from a point to a segment, including its endpoints.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="start">The segment start.</param>
    /// <param name="end">The segment end.</param>
    /// <param name="degenerateLengthSquared">Squared length below which the segment is treated as its start point.</param>
    /// <returns>The distance to the nearest point of the segment.</returns>
    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end, float degenerateLengthSquared = float.Epsilon) => MathF.Sqrt(DistanceSquaredToSegment(point, start, end, degenerateLengthSquared));

    /// <summary>The squared distance from a point to a segment, including its endpoints.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="start">The segment start.</param>
    /// <param name="end">The segment end.</param>
    /// <param name="degenerateLengthSquared">Squared length below which the segment is treated as its start point.</param>
    /// <returns>The squared distance to the nearest point of the segment.</returns>
    public static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end, float degenerateLengthSquared = float.Epsilon) => Vector2.DistanceSquared(point, (end - start).LengthSquared() < degenerateLengthSquared ? start : ClosestPoint2D.OnSegment(point, start, end));

    /// <summary>The distance between two segments; zero when they cross.</summary>
    /// <param name="firstStart">The first segment start.</param>
    /// <param name="firstEnd">The first segment end.</param>
    /// <param name="secondStart">The second segment start.</param>
    /// <param name="secondEnd">The second segment end.</param>
    /// <returns>The distance between their closest points.</returns>
    public static float DistanceBetweenSegments(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd) => MathF.Sqrt(DistanceSquaredBetweenSegments(firstStart, firstEnd, secondStart, secondEnd));

    /// <summary>The squared distance between two segments; zero when they cross.</summary>
    /// <param name="firstStart">The first segment start.</param>
    /// <param name="firstEnd">The first segment end.</param>
    /// <param name="secondStart">The second segment start.</param>
    /// <param name="secondEnd">The second segment end.</param>
    /// <returns>The squared distance between their closest points.</returns>
    public static float DistanceSquaredBetweenSegments(Vector2 firstStart, Vector2 firstEnd, Vector2 secondStart, Vector2 secondEnd)
    {
        var closest = ClosestPoint2D.BetweenSegments(firstStart, firstEnd, secondStart, secondEnd);
        return Vector2.DistanceSquared(closest.First, closest.Second);
    }

    /// <summary>The signed distance from a point to a circle.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistanceToCircle(Vector2 point, Vector2 center, float radius) => Vector2.Distance(point, center) - radius;

    /// <summary>The distance from a point to a filled circle; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float DistanceToCircle(Vector2 point, Vector2 center, float radius) => Math.Max(0f, SignedDistanceToCircle(point, center, radius));

    /// <summary>The signed distance between two circles.</summary>
    /// <param name="firstCenter">The first center.</param>
    /// <param name="firstRadius">The first radius.</param>
    /// <param name="secondCenter">The second center.</param>
    /// <param name="secondRadius">The second radius.</param>
    /// <returns>The gap when separated, or the negated overlap depth.</returns>
    public static float SignedDistanceBetweenCircles(Vector2 firstCenter, float firstRadius, Vector2 secondCenter, float secondRadius) => Vector2.Distance(firstCenter, secondCenter) - (firstRadius + secondRadius);

    /// <summary>The distance between two filled circles; zero when they touch or overlap.</summary>
    /// <param name="firstCenter">The first center.</param>
    /// <param name="firstRadius">The first radius.</param>
    /// <param name="secondCenter">The second center.</param>
    /// <param name="secondRadius">The second radius.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float DistanceBetweenCircles(Vector2 firstCenter, float firstRadius, Vector2 secondCenter, float secondRadius) => Math.Max(0f, SignedDistanceBetweenCircles(firstCenter, firstRadius, secondCenter, secondRadius));

    /// <summary>The signed distance from a point to an axis-aligned ellipse, exact to float precision.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistanceToEllipse(Vector2 point, Vector2 center, Vector2 radii)
    {
        var distance = Vector2.Distance(point, ClosestPoint2D.OnEllipsePerimeter(point, center, radii));
        return Containment2D.Ellipse(point, center, radii) ? -distance : distance;
    }

    /// <summary>The distance from a point to a filled axis-aligned ellipse; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float DistanceToEllipse(Vector2 point, Vector2 center, Vector2 radii) => Math.Max(0f, SignedDistanceToEllipse(point, center, radii));

    /// <summary>The signed distance from a point to a capsule.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistanceToCapsule(Vector2 point, Vector2 start, Vector2 end, float radius) => DistanceToSegment(point, start, end) - radius;

    /// <summary>The distance from a point to a filled capsule; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float DistanceToCapsule(Vector2 point, Vector2 start, Vector2 end, float radius) => Math.Max(0f, SignedDistanceToCapsule(point, start, end, radius));

    /// <summary>The signed distance from a point to an ordered axis-aligned rectangle. Outside distance is Euclidean, including at corners.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>Negative inside (distance to the nearest edge), zero on the boundary, positive outside.</returns>
    public static float SignedDistanceToRectangle(Vector2 point, Vector2 min, Vector2 max)
    {
        var outside = Vector2.Max(Vector2.Max(min - point, point - max), Vector2.Zero);
        if (outside != Vector2.Zero) return outside.Length();
        return -Math.Min(Math.Min(point.X - min.X, max.X - point.X), Math.Min(point.Y - min.Y, max.Y - point.Y));
    }

    /// <summary>The distance from a point to a filled rectangle; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float DistanceToRectangle(Vector2 point, Vector2 min, Vector2 max) => Math.Max(0f, SignedDistanceToRectangle(point, min, max));

    /// <summary>The squared distance from a point to a filled rectangle; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>The nonnegative squared distance.</returns>
    public static float DistanceSquaredToRectangle(Vector2 point, Vector2 min, Vector2 max) => Vector2.Max(Vector2.Max(min - point, point - max), Vector2.Zero).LengthSquared();

    /// <summary>The signed distance from a point to a half-space whose solid side is dot(point, unitNormal) &lt;= offset.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="unitNormal">The unit normal pointing toward free space.</param>
    /// <param name="offset">The signed boundary offset along the normal.</param>
    /// <returns>Negative inside the solid, zero on the boundary, positive in free space.</returns>
    public static float SignedDistanceToHalfSpace(Vector2 point, Vector2 unitNormal, float offset) => Vector2.Dot(point, unitNormal) - offset;

    /// <summary>The distance from a point to the solid side of a half-space; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="unitNormal">The unit normal pointing toward free space.</param>
    /// <param name="offset">The signed boundary offset along the normal.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float DistanceToHalfSpace(Vector2 point, Vector2 unitNormal, float offset) => Math.Max(0f, SignedDistanceToHalfSpace(point, unitNormal, offset));

    /// <summary>The signed distance from a point to a convex polygon in either winding.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least three vertices in perimeter order with nonzero area.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistanceToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices) => SignedDistanceToConvexPolygon(point, vertices, out _, out _);

    /// <summary>The distance from a point to a filled convex polygon; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least three vertices in perimeter order with nonzero area.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float DistanceToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices) => Math.Max(0f, SignedDistanceToConvexPolygon(point, vertices));

    /// <summary>The squared distance from a point to a filled convex polygon; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least three vertices in perimeter order with nonzero area.</param>
    /// <returns>The nonnegative squared distance.</returns>
    public static float DistanceSquaredToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        return Containment2D.ConvexPolygon(point, vertices, 0f) ? 0f : Vector2.DistanceSquared(point, ClosestPoint2D.OnPolygonPerimeter(point, vertices));
    }

    /// <summary>The squared distance from a point to the perimeter of a polygon given in perimeter order, ignoring the interior.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least one vertex.</param>
    /// <returns>The squared distance to the nearest edge.</returns>
    public static float DistanceSquaredToPolygonPerimeter(Vector2 point, ReadOnlySpan<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 1);
        return Vector2.DistanceSquared(point, ClosestPoint2D.OnPolygonPerimeter(point, vertices));
    }

    /// <summary>
    /// The signed distance from a point to a convex polygon in either winding, plus the nearest boundary point and its edge for contact generation.
    /// </summary>
    /// <param name="point">The query point.</param>
    /// <param name="vertices">At least three vertices in perimeter order with nonzero area.</param>
    /// <param name="closestPoint">The nearest point on the perimeter.</param>
    /// <param name="edgeIndex">The index of the edge holding <paramref name="closestPoint"/>.</param>
    /// <param name="collinearEpsilon">Cross-product tolerance for the containment test; zero gives the geometric sign.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistanceToConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices, out Vector2 closestPoint, out int edgeIndex, float collinearEpsilon = 0f)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        closestPoint = ClosestPoint2D.OnPolygonPerimeter(point, vertices, out edgeIndex);
        var distance = Vector2.Distance(point, closestPoint);
        return Containment2D.ConvexPolygon(point, vertices, collinearEpsilon) ? -distance : distance;
    }

    /// <summary>The signed distance from a point to a rectangle value.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistance(Vector2 point, Rect2D rectangle) => SignedDistanceToRectangle(point, rectangle.Min, rectangle.Max);

    /// <summary>The signed distance from a rectangle value to a point; argument order does not matter.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="point">The query point.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistance(Rect2D rectangle, Vector2 point) => SignedDistance(point, rectangle);

    /// <summary>The distance from a point to a filled rectangle value; zero inside.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float Distance(Vector2 point, Rect2D rectangle) => Math.Max(0f, SignedDistance(point, rectangle));

    /// <summary>The distance from a filled rectangle value to a point; argument order does not matter.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="point">The query point.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float Distance(Rect2D rectangle, Vector2 point) => Distance(point, rectangle);

    /// <summary>The signed distance between two rectangle values. Separated rectangles measure the Euclidean gap, including across corners.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    public static float SignedDistance(Rect2D first, Rect2D second)
    {
        var x = SignedDistance(new Interval1D(first.Min.X, first.Max.X), new Interval1D(second.Min.X, second.Max.X));
        var y = SignedDistance(new Interval1D(first.Min.Y, first.Max.Y), new Interval1D(second.Min.Y, second.Max.Y));
        return x > 0f || y > 0f ? new Vector2(Math.Max(0f, x), Math.Max(0f, y)).Length() : Math.Max(x, y);
    }

    /// <summary>The distance between two filled rectangle values; zero when touching or overlapping.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float Distance(Rect2D first, Rect2D second) => Math.Max(0f, SignedDistance(first, second));
}
