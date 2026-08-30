using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Shared math over convex polygon perimeters given as vertex spans.</summary>
public static class PolygonGeometry2D
{
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
        var outward = SignedAreaTwice(vertices) >= 0f ? edge.PerpCw() : edge.PerpCcw();
        return outward.LengthSquared() > float.Epsilon ? Vector2.Normalize(outward) : Vector2.UnitY;
    }
}
