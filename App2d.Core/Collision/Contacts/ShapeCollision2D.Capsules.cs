using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CapsuleVsCapsule(Capsule2D first, Similarity2D firstPose, Capsule2D second, Similarity2D secondPose)
    {
        var (firstStart, firstEnd, firstRadius) = WorldShape2D.Capsule(first, firstPose);
        var (secondStart, secondEnd, secondRadius) = WorldShape2D.Capsule(second, secondPose);

        var closest = ClosestPoint2D.BetweenSegments(firstStart, firstEnd, secondStart, secondEnd);
        var combinedRadius = firstRadius + secondRadius;
        if (Vector2.DistanceSquared(closest.First, closest.Second) >= combinedRadius * combinedRadius) return CollisionResult.None;

        // The closest-point delta is the exact axis while the spines are apart.
        // Extra feature axes make crossing, coincident and parallel spines stable too.
        Span<Vector2> axes = stackalloc Vector2[8];
        var axisCount = 0;
        AddAxis(axes, ref axisCount, closest.First - closest.Second);
        var firstDirection = firstEnd - firstStart;
        var secondDirection = secondEnd - secondStart;
        AddAxis(axes, ref axisCount, firstDirection.PerpCcw);
        AddAxis(axes, ref axisCount, secondDirection.PerpCcw);
        AddAxis(axes, ref axisCount, firstStart - ClosestPoint2D.OnSegment(firstStart, secondStart, secondEnd));
        AddAxis(axes, ref axisCount, firstEnd - ClosestPoint2D.OnSegment(firstEnd, secondStart, secondEnd));
        AddAxis(axes, ref axisCount, ClosestPoint2D.OnSegment(secondStart, firstStart, firstEnd) - secondStart);
        AddAxis(axes, ref axisCount, ClosestPoint2D.OnSegment(secondEnd, firstStart, firstEnd) - secondEnd);
        AddAxis(axes, ref axisCount, (firstStart + firstEnd) / 2f - (secondStart + secondEnd) / 2f);
        if (axisCount == 0) axes[axisCount++] = Vector2.UnitX;

        var bestDepth = float.PositiveInfinity;
        var bestNormal = Vector2.UnitX;
        foreach (var rawAxis in axes[..axisCount])
        {
            var axis = Vector2.Normalize(rawAxis);
            var firstInterval = Projection2D.Capsule(firstStart, firstEnd, firstRadius, axis);
            var secondInterval = Projection2D.Capsule(secondStart, secondEnd, secondRadius, axis);
            if (!TryUpdateMtv(axis, firstInterval, secondInterval, ref bestNormal, ref bestDepth)) return CollisionResult.None;
        }

        var contactPoint = closest.Second + bestNormal * secondRadius;
        return CollisionResult.From(new CollisionContact2D(contactPoint, bestNormal, bestDepth));
    }

    private static CollisionResult PolygonVsCapsule(ReadOnlySpan<Vector2> localVertices, Similarity2D polygonPose, Capsule2D capsule, Similarity2D capsulePose)
    {
        var (capsuleStart, capsuleEnd, capsuleRadius) = WorldShape2D.Capsule(capsule, capsulePose);
        Span<Vector2> polygonVertices = localVertices.Length <= StackVertexLimit ? stackalloc Vector2[localVertices.Length] : new Vector2[localVertices.Length];
        for (var i = 0; i < polygonVertices.Length; i++) polygonVertices[i] = polygonPose.TransformPoint(localVertices[i]);

        var maximumAxes = 2 * polygonVertices.Length + 3;
        Span<Vector2> axes = maximumAxes <= 2 * StackVertexLimit ? stackalloc Vector2[maximumAxes] : new Vector2[maximumAxes];
        var axisCount = 0;
        AddPolygonEdgeAxes(axes, ref axisCount, polygonVertices);
        AddAxis(axes, ref axisCount, (capsuleEnd - capsuleStart).PerpCcw);
        foreach (var vertex in polygonVertices) AddAxis(axes, ref axisCount, vertex - ClosestPoint2D.OnSegment(vertex, capsuleStart, capsuleEnd));
        AddAxis(axes, ref axisCount, capsuleStart - ClosestPoint2D.OnPolygonPerimeter(capsuleStart, polygonVertices));
        AddAxis(axes, ref axisCount, capsuleEnd - ClosestPoint2D.OnPolygonPerimeter(capsuleEnd, polygonVertices));

        if (!TryGetPolygonCapsuleMtv(polygonVertices, capsuleStart, capsuleEnd, capsuleRadius, axes[..axisCount], out var normal, out var depth)) return CollisionResult.None;

        var polygonSurface = SupportPoint2D.Polygon(-normal, polygonVertices);
        var capsuleSurface = SupportPoint2D.Capsule(normal, capsuleStart, capsuleEnd, capsuleRadius);
        return CollisionResult.From(new CollisionContact2D((polygonSurface + capsuleSurface) / 2f, normal, depth));
    }
}
