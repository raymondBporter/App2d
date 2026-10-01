using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult PolygonVsPolygon(ReadOnlySpan<Vector2> firstLocal, Similarity2D firstPose, ReadOnlySpan<Vector2> secondLocal, Similarity2D secondPose)
    {
        Span<Vector2> first = firstLocal.Length <= StackVertexLimit ? stackalloc Vector2[firstLocal.Length] : new Vector2[firstLocal.Length];
        Span<Vector2> second = secondLocal.Length <= StackVertexLimit ? stackalloc Vector2[secondLocal.Length] : new Vector2[secondLocal.Length];
        for (var i = 0; i < first.Length; i++) first[i] = firstPose.TransformPoint(firstLocal[i]);
        for (var i = 0; i < second.Length; i++) second[i] = secondPose.TransformPoint(secondLocal[i]);

        var maximumAxes = first.Length + second.Length;
        Span<Vector2> axes = maximumAxes <= 2 * StackVertexLimit ? stackalloc Vector2[maximumAxes] : new Vector2[maximumAxes];
        var axisCount = 0;
        AddPolygonEdgeAxes(axes, ref axisCount, first);
        AddPolygonEdgeAxes(axes, ref axisCount, second);
        if (!TryGetPolygonMtv(first, second, axes[..axisCount], out var normal, out var depth)) return CollisionResult.None;

        var firstSurface = SupportPoint2D.Polygon(-normal, first);
        var secondSurface = SupportPoint2D.Polygon(normal, second);
        return CollisionResult.From(new CollisionContact2D((firstSurface + secondSurface) / 2f, normal, depth));
    }

    private static void AddPolygonEdgeAxes(Span<Vector2> axes, ref int axisCount, ReadOnlySpan<Vector2> vertices)
    {
        for (var i = 0; i < vertices.Length; i++) AddAxis(axes, ref axisCount, (vertices[(i + 1) % vertices.Length] - vertices[i]).PerpCcw);
    }

    private static void AddAxis(Span<Vector2> axes, ref int axisCount, Vector2 candidate)
    {
        if (candidate.LengthSquared() > 0.000001f) axes[axisCount++] = candidate;
    }

    private static bool TryGetPolygonMtv(ReadOnlySpan<Vector2> firstVertices, ReadOnlySpan<Vector2> secondVertices, ReadOnlySpan<Vector2> axes, out Vector2 normal, out float depth)
    {
        normal = Vector2.UnitX;
        depth = float.PositiveInfinity;
        var testedAxis = false;
        foreach (var rawAxis in axes)
        {
            var axis = Vector2.Normalize(rawAxis);
            var first = Projection2D.Polygon(firstVertices, axis);
            var second = Projection2D.Polygon(secondVertices, axis);
            testedAxis = true;
            if (!TryUpdateMtv(axis, first, second, ref normal, ref depth)) return false;
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
            var polygon = Projection2D.Polygon(polygonVertices, axis);
            var capsule = Projection2D.Capsule(capsuleStart, capsuleEnd, capsuleRadius, axis);
            testedAxis = true;
            if (!TryUpdateMtv(axis, polygon, capsule, ref normal, ref depth)) return false;
        }
        return testedAxis;
    }

    private static bool TryUpdateMtv(Vector2 axis, Interval1D first, Interval1D second, ref Vector2 bestNormal, ref float bestDepth)
    {
        var signedDistance = Distance2D.SignedDistance(first, second, out var direction);
        if (signedDistance >= 0f) return false;

        var candidateDepth = -signedDistance;
        if (candidateDepth < bestDepth)
        {
            bestDepth = candidateDepth;
            bestNormal = axis * direction;
        }
        return true;
    }
}
