using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult RectangleVsRectangle(Rectangle2D first, Transform2D firstTransform, Rectangle2D second, Transform2D secondTransform)
    {
        Span<Vector2> firstVertices = stackalloc Vector2[4];
        Span<Vector2> secondVertices = stackalloc Vector2[4];
        WriteWorldRectangleVertices(first, firstTransform, firstVertices);
        WriteWorldRectangleVertices(second, secondTransform, secondVertices);
        Span<Vector2> axes = stackalloc Vector2[8];
        var axisCount = 0;
        AddPolygonEdgeAxes(axes, ref axisCount, firstVertices);
        AddPolygonEdgeAxes(axes, ref axisCount, secondVertices);

        if (!TryGetPolygonMtv(firstVertices, secondVertices, axes[..axisCount], out var normal, out var depth))
            return CollisionResult.None;

        var firstSurface = GetPolygonSupportPoint(firstVertices, -normal);
        var secondSurface = GetPolygonSupportPoint(secondVertices, normal);
        return CollisionResult.From(new CollisionContact2D(
            (firstSurface + secondSurface) / 2f,
            normal,
            depth));
    }

    private static Vector2 ClosestPointOnPolygon(Vector2 point, ReadOnlySpan<Vector2> vertices, out int edgeIndex)
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

    private static bool ContainsPoint(ReadOnlySpan<Vector2> vertices, Vector2 point)
    {
        var winding = 0f;
        for (var i = 0; i < vertices.Length; i++)
        {
            var start = vertices[i];
            var end = vertices[(i + 1) % vertices.Length];
            var cross = (end - start).Cross(point - start);
            if (MathF.Abs(cross) <= 0.0001f)
                continue;

            float turn = MathF.Sign(cross);
            if (winding == 0f)
                winding = turn;
            else if (turn != winding)
                return false;
        }

        return true;
    }

    private static Vector2 GetOutwardEdgeNormal(ReadOnlySpan<Vector2> vertices, int edgeIndex)
    {
        var edge = vertices[(edgeIndex + 1) % vertices.Length] - vertices[edgeIndex];
        var signedAreaTwice = 0f;
        for (var i = 0; i < vertices.Length; i++)
            signedAreaTwice += vertices[i].Cross(vertices[(i + 1) % vertices.Length]);

        var outward = signedAreaTwice >= 0f
            ? edge.PerpCw()
            : edge.PerpCcw();
        return outward.LengthSquared() > float.Epsilon
            ? Vector2.Normalize(outward)
            : Vector2.UnitY;
    }

    private static void AddPolygonEdgeAxes(Span<Vector2> axes, ref int axisCount, ReadOnlySpan<Vector2> vertices)
    {
        for (var i = 0; i < vertices.Length; i++)
        {
            var edge = vertices[(i + 1) % vertices.Length] - vertices[i];
            AddAxis(axes, ref axisCount, edge.PerpCcw());
        }
    }

    private static bool TryGetPolygonMtv(ReadOnlySpan<Vector2> firstVertices, ReadOnlySpan<Vector2> secondVertices, ReadOnlySpan<Vector2> axes, out Vector2 normal, out float depth)
    {
        normal = Vector2.UnitX;
        depth = float.PositiveInfinity;
        var testedAxis = false;

        foreach (var rawAxis in axes)
        {
            var axis = Vector2.Normalize(rawAxis);
            ProjectPolygon(firstVertices, axis, out var firstMin, out var firstMax);
            ProjectPolygon(secondVertices, axis, out var secondMin, out var secondMax);
            testedAxis = true;
            if (!TryUpdateMtv(axis, firstMin, firstMax, secondMin, secondMax, ref normal, ref depth))
                return false;
        }

        return testedAxis;
    }

    private static bool TryGetPolygonCapsuleMtv(ReadOnlySpan<Vector2> polygonVertices, Vector2 capsuleStart, Vector2 capsuleEnd, float capsuleRadius, ReadOnlySpan<Vector2> axes, out Vector2 normal, out float depth)
    {
        normal = Vector2.UnitX;
        depth = float.PositiveInfinity;
        var testedAxis = false;

        foreach (var rawAxis in axes)
        {
            var axis = Vector2.Normalize(rawAxis);
            ProjectPolygon(polygonVertices, axis, out var polygonMin, out var polygonMax);
            ProjectCapsule(capsuleStart, capsuleEnd, capsuleRadius, axis, out var capsuleMin, out var capsuleMax);
            testedAxis = true;
            if (!TryUpdateMtv(axis, polygonMin, polygonMax, capsuleMin, capsuleMax, ref normal, ref depth))
                return false;
        }

        return testedAxis;
    }

    private static bool TryUpdateMtv(Vector2 axis, float firstMin, float firstMax, float secondMin, float secondMax, ref Vector2 bestNormal, ref float bestDepth)
    {
        var pushPositive = secondMax - firstMin;
        var pushNegative = firstMax - secondMin;
        if (pushPositive <= 0f || pushNegative <= 0f)
            return false;

        float candidateDepth;
        Vector2 candidateNormal;
        if (pushPositive < pushNegative)
        {
            candidateDepth = pushPositive;
            candidateNormal = axis;
        }
        else
        {
            candidateDepth = pushNegative;
            candidateNormal = -axis;
        }

        if (candidateDepth < bestDepth)
        {
            bestDepth = candidateDepth;
            bestNormal = candidateNormal;
        }

        return true;
    }

    private static void ProjectPolygon(ReadOnlySpan<Vector2> vertices, Vector2 axis, out float min, out float max)
    {
        min = Vector2.Dot(vertices[0], axis);
        max = min;
        foreach (var vertex in vertices[1..])
        {
            var projection = Vector2.Dot(vertex, axis);
            min = Math.Min(min, projection);
            max = Math.Max(max, projection);
        }
    }

    private static Vector2 GetPolygonSupportPoint(ReadOnlySpan<Vector2> vertices, Vector2 direction)
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

    private static Vector2 GetCapsuleSupportPoint(Vector2 start, Vector2 end, float radius, Vector2 direction)
    {
        var endpoint = Vector2.Dot(start, direction) > Vector2.Dot(end, direction)
            ? start
            : end;
        return endpoint + Vector2.Normalize(direction) * radius;
    }
}
