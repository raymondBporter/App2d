using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Shared math over convex polygon perimeters given as vertex spans or lists.</summary>
public static class PolygonGeometry2D
{
    /// <summary>
    /// Separating-axis overlap of two convex perimeters with at least three vertices, in either winding.
    /// Offsets place each local perimeter in the same coordinate space. Touching counts as overlap;
    /// repeated adjacent vertices are allowed and their zero-length edges are ignored.
    /// </summary>
    public static bool OverlapsConvex(IReadOnlyList<Vector2> first, IReadOnlyList<Vector2> second,
        Vector2 firstOffset = default, Vector2 secondOffset = default)
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
                var edge = perimeter[(i + 1) % perimeter.Count] - perimeter[i];
                var axis = edge.PerpCcw;
                if (axis.LengthSquared() < 1e-12f) continue;
                var a = Projection2D.Polygon(first, axis, firstOffset);
                var b = Projection2D.Polygon(second, axis, secondOffset);
                if (a.Max < b.Min || b.Max < a.Min) return true;
            }
            return false;
        }
    }

    public static float SignedAreaTwice(ReadOnlySpan<Vector2> vertices)
    {
        var signedAreaTwice = 0f;
        for (var i = 0; i < vertices.Length; i++)
            signedAreaTwice += vertices[i].Cross(vertices[(i + 1) % vertices.Length]);
        return signedAreaTwice;
    }

    public static float Area(ReadOnlySpan<Vector2> vertices) => MathF.Abs(SignedAreaTwice(vertices)) / 2f;

    public static bool ContainsPoint(ReadOnlySpan<Vector2> vertices, Vector2 point, float collinearEpsilon = 0.0001f)
    {
        var winding = 0f;
        for (var i = 0; i < vertices.Length; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Length];
            var cross = (end - start).Cross(point - start);
            if (MathF.Abs(cross) <= collinearEpsilon)
                continue;

            float turn = MathF.Sign(cross);
            if (winding == 0f)
                winding = turn;
            else if (turn != winding)
                return false;
        }

        return true;
    }

    public static Vector2 GetSupportPoint(ReadOnlySpan<Vector2> vertices, Vector2 direction)
    {
        var support = vertices[0];
        var bestProjection = Vector2.Dot(support, direction);
        foreach (var vertex in vertices[1..])
        {
            var projection = Vector2.Dot(vertex, direction);
            if (projection > bestProjection)
            {
                bestProjection = projection;
                support = vertex;
            }
        }

        return support;
    }

    public static Vector2 ClosestPointOnPerimeter(Vector2 point, ReadOnlySpan<Vector2> vertices, out int edgeIndex)
    {
        var closest = vertices[0];
        var bestDistanceSquared = float.PositiveInfinity;
        edgeIndex = 0;

        for (var i = 0; i < vertices.Length; i++)
        {
            var candidate = ClosestPoint2D.OnSegment(point, vertices[i], vertices[(i + 1) % vertices.Length]);
            var distanceSquared = Vector2.DistanceSquared(point, candidate);
            if (distanceSquared < bestDistanceSquared)
            {
                closest = candidate;
                bestDistanceSquared = distanceSquared;
                edgeIndex = i;
            }
        }

        return closest;
    }

    public static Vector2 GetOutwardEdgeNormal(ReadOnlySpan<Vector2> vertices, int edgeIndex)
    {
        var edge = vertices[(edgeIndex + 1) % vertices.Length] - vertices[edgeIndex];
        var outward = SignedAreaTwice(vertices) >= 0f ? edge.PerpCw : edge.PerpCcw;
        return outward.LengthSquared() > float.Epsilon ? Vector2.Normalize(outward) : Vector2.UnitY;
    }
}
