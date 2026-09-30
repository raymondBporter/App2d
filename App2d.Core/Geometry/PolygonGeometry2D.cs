using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Structure of polygon perimeters: hulls, winding and edge normals. Area, containment, support points,
/// closest points and overlap live in their operation classes.
/// </summary>
public static class PolygonGeometry2D
{
    /// <summary>
    /// The minimal counter-clockwise convex perimeter around finite points. Duplicate and collinear
    /// interior points are omitted; the first point is not repeated at the end.
    /// </summary>
    /// <param name="source">At least three distinct, non-collinear finite points in any order.</param>
    /// <returns>The hull vertices in counter-clockwise order.</returns>
    public static Vector2[] ConvexHull(IEnumerable<Vector2> source)
    {
        ArgGuard.ThrowIfNull(source);
        var input = source.ToArray();
        ArgGuard.ThrowIf(input.Any(point => !NumericValidation.IsFinite(point)), "Hull points must be finite.", nameof(source));

        var points = input.Distinct().OrderBy(point => point.X).ThenBy(point => point.Y).ToArray();
        ArgGuard.ThrowIf(points.Length < 3, "A convex hull requires at least three distinct points.", nameof(source));

        var hull = new List<Vector2>(points.Length * 2);
        foreach (var point in points)
        {
            while (hull.Count >= 2 && CrossProduct2D.Orientation(hull[^2], hull[^1], point) <= 0d) hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }

        var lowerCount = hull.Count;
        for (var index = points.Length - 2; index >= 0; index--)
        {
            var point = points[index];
            while (hull.Count > lowerCount && CrossProduct2D.Orientation(hull[^2], hull[^1], point) <= 0d) hull.RemoveAt(hull.Count - 1);
            hull.Add(point);
        }

        hull.RemoveAt(hull.Count - 1);
        ArgGuard.ThrowIf(hull.Count < 3, "A convex hull requires three non-collinear points.", nameof(source));
        return [.. hull];
    }

    /// <summary>Twice the signed area of a polygon in perimeter order, in double precision: positive for counter-clockwise.</summary>
    /// <param name="vertices">The perimeter vertices without a repeated closing vertex.</param>
    /// <returns>Twice the signed area.</returns>
    public static double SignedAreaTwiceDouble(ReadOnlySpan<Vector2> vertices)
    {
        var signedAreaTwice = 0d;
        for (var i = 0; i < vertices.Length; i++) signedAreaTwice += CrossProduct2D.Of(vertices[i], vertices[(i + 1) % vertices.Length]);
        return signedAreaTwice;
    }

    /// <summary>Twice the signed area of a polygon in perimeter order: positive for counter-clockwise.</summary>
    /// <param name="vertices">The perimeter vertices without a repeated closing vertex.</param>
    /// <returns>Twice the signed area as a float.</returns>
    public static float SignedAreaTwice(ReadOnlySpan<Vector2> vertices) => (float)SignedAreaTwiceDouble(vertices);

    /// <summary>The unit outward normal of one edge of a polygon in either winding.</summary>
    /// <param name="vertices">The perimeter vertices.</param>
    /// <param name="edgeIndex">The edge from vertex i to vertex i + 1 (wrapping).</param>
    /// <returns>The outward unit normal, or +Y for a zero-length edge.</returns>
    public static Vector2 GetOutwardEdgeNormal(ReadOnlySpan<Vector2> vertices, int edgeIndex)
    {
        var edge = vertices[(edgeIndex + 1) % vertices.Length] - vertices[edgeIndex];
        var outward = SignedAreaTwiceDouble(vertices) >= 0 ? edge.PerpCw : edge.PerpCcw;
        return outward.LengthSquared() > float.Epsilon ? Vector2.Normalize(outward) : Vector2.UnitY;
    }
}
