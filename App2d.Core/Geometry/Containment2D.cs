using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Point containment for raw primitives, boundaries included. Inputs are finite, radii are nonnegative,
/// ellipse radii and box half-extents are positive, and rectangle bounds are ordered (infinite bounds allowed).
/// </summary>
public static class Containment2D
{
    /// <summary>Tests whether a point lies in or on a circle.</summary>
    /// <param name="point">The point to test.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <returns>True when the point is within the radius of the center.</returns>
    public static bool Circle(Vector2 point, Vector2 center, float radius) => Vector2.DistanceSquared(point, center) <= radius * radius;

    /// <summary>Tests whether a point lies in or on an axis-aligned ellipse.</summary>
    /// <param name="point">The point to test.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>True when the normalized radius is at most one.</returns>
    public static bool Ellipse(Vector2 point, Vector2 center, Vector2 radii) => ((point - center) / radii).LengthSquared() <= 1f;

    /// <summary>Tests whether a point lies in or on a capsule.</summary>
    /// <param name="point">The point to test.</param>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <returns>True when the point is within the radius of the spine segment.</returns>
    public static bool Capsule(Vector2 point, Vector2 start, Vector2 end, float radius) => Vector2.DistanceSquared(point, ClosestPoint2D.OnSegment(point, start, end)) <= radius * radius;

    /// <summary>Tests whether a point lies in or on an ordered axis-aligned rectangle.</summary>
    /// <param name="point">The point to test.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>True when the point is inside, including all four edges.</returns>
    public static bool Rectangle(Vector2 point, Vector2 min, Vector2 max) => point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;

    /// <summary>Tests whether one ordered rectangle lies entirely inside another, shared edges included.</summary>
    /// <param name="min">The outer lower-left corner.</param>
    /// <param name="max">The outer upper-right corner.</param>
    /// <param name="innerMin">The inner lower-left corner.</param>
    /// <param name="innerMax">The inner upper-right corner.</param>
    /// <returns>True when both inner corners lie inside the outer rectangle.</returns>
    public static bool RectangleInRectangle(Vector2 min, Vector2 max, Vector2 innerMin, Vector2 innerMax) => Rectangle(innerMin, min, max) && Rectangle(innerMax, min, max);

    /// <summary>Tests whether a point lies on the solid side of a half-space.</summary>
    /// <param name="point">The point to test.</param>
    /// <param name="unitNormal">The unit normal pointing toward free space.</param>
    /// <param name="offset">The signed boundary offset along the normal.</param>
    /// <returns>True when the projection of the point onto the normal is at most the offset.</returns>
    public static bool HalfSpace(Vector2 point, Vector2 unitNormal, float offset) => Vector2.Dot(point, unitNormal) <= offset;

    /// <summary>Tests whether a point lies in or on a convex polygon given in perimeter order, in either winding.</summary>
    /// <param name="point">The point to test.</param>
    /// <param name="vertices">The convex perimeter vertices.</param>
    /// <param name="collinearEpsilon">Cross-product magnitude below which the point counts as on an edge; zero is exact.</param>
    /// <returns>True when the point is on the same side of every edge, or within the tolerance of one.</returns>
    public static bool ConvexPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices, float collinearEpsilon = 0.0001f)
    {
        var winding = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var cross = CrossProduct2D.Orientation(vertices[i], vertices[(i + 1) % vertices.Length], point);
            if (Math.Abs(cross) <= collinearEpsilon) continue;
            var turn = Math.Sign(cross);
            if (winding == 0) winding = turn;
            else if (turn != winding) return false;
        }
        return true;
    }

    /// <summary>A dimensionless radial score for an ellipse: 0 at the center, 1 on the boundary. Not a Euclidean distance.</summary>
    /// <param name="point">The point to score.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>The length of the point in normalized ellipse coordinates.</returns>
    public static float NormalizedEllipseRadius(Vector2 point, Vector2 center, Vector2 radii) => ((point - center) / radii).Length();

    /// <summary>A dimensionless radial score for a box: 0 at the center, 1 on the boundary. Not a Euclidean distance.</summary>
    /// <param name="point">The point to score.</param>
    /// <param name="center">The box center.</param>
    /// <param name="halfExtents">The strictly positive half-extents.</param>
    /// <returns>The larger of the normalized X and Y offsets.</returns>
    public static float NormalizedRectangleRadius(Vector2 point, Vector2 center, Vector2 halfExtents)
    {
        var normalized = Vector2.Abs((point - center) / halfExtents);
        return Math.Max(normalized.X, normalized.Y);
    }
}
