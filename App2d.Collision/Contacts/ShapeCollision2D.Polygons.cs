using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult RectangleVsRectangle(Rectangle2D first, Similarity2D firstPose, Rectangle2D second, Similarity2D secondPose)
    {
        Span<Vector2> firstVertices = stackalloc Vector2[4];
        Span<Vector2> secondVertices = stackalloc Vector2[4];
        WriteWorldRectangleVertices(first, firstPose, firstVertices);
        WriteWorldRectangleVertices(second, secondPose, secondVertices);
        Span<Vector2> axes = stackalloc Vector2[8];
        var axisCount = 0;
        AddPolygonEdgeAxes(axes, ref axisCount, firstVertices);
        AddPolygonEdgeAxes(axes, ref axisCount, secondVertices);

        if (!TryGetPolygonMtv(firstVertices, secondVertices, axes[..axisCount], out var normal, out var depth))
            return CollisionResult.None;

        var firstSurface = PolygonGeometry2D.GetSupportPoint(firstVertices, -normal);
        var secondSurface = PolygonGeometry2D.GetSupportPoint(secondVertices, normal);
        return CollisionResult.From(new CollisionContact2D(
            (firstSurface + secondSurface) / 2f,
            normal,
            depth));
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
            var first = Interval1D.ProjectPolygon(firstVertices, axis);
            var second = Interval1D.ProjectPolygon(secondVertices, axis);
            testedAxis = true;
            if (!TryUpdateMtv(axis, first, second, ref normal, ref depth))
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
            var polygon = Interval1D.ProjectPolygon(polygonVertices, axis);
            var capsule = Interval1D.ProjectCapsule(capsuleStart, capsuleEnd, capsuleRadius, axis);
            testedAxis = true;
            if (!TryUpdateMtv(axis, polygon, capsule, ref normal, ref depth))
                return false;
        }

        return testedAxis;
    }

    private static bool TryUpdateMtv(Vector2 axis, Interval1D first, Interval1D second, ref Vector2 bestNormal, ref float bestDepth)
    {
        var pushPositive = second.Max - first.Min;
        var pushNegative = first.Max - second.Min;
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

    private static Vector2 GetCapsuleSupportPoint(Vector2 start, Vector2 end, float radius, Vector2 direction)
    {
        var endpoint = Vector2.Dot(start, direction) > Vector2.Dot(end, direction)
            ? start
            : end;
        return endpoint + Vector2.Normalize(direction) * radius;
    }
}
