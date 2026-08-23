using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CapsuleVsCapsule(
        Capsule2D first,
        Transform2D firstTransform,
        Capsule2D second,
        Transform2D secondTransform)
    {
        if (!CollisionMath2D.TryGetWorldCapsule(first, firstTransform, out var firstStart, out var firstEnd, out var firstRadius) ||
            !CollisionMath2D.TryGetWorldCapsule(second, secondTransform, out var secondStart, out var secondEnd, out var secondRadius))
        {
            return CollisionResult.None;
        }

        var closest = ClosestPoint2D.BetweenSegments(firstStart, firstEnd, secondStart, secondEnd);
        var combinedRadius = firstRadius + secondRadius;
        if (Vector2.DistanceSquared(closest.First, closest.Second) >= combinedRadius * combinedRadius)
            return CollisionResult.None;

        // The closest-point delta is the exact axis while the spines are apart.
        // Extra feature axes make crossing/coincident/parallel spines stable too.
        Span<Vector2> axes = stackalloc Vector2[8];
        var axisCount = 0;
        AddAxis(axes, ref axisCount, closest.First - closest.Second);
        var firstDirection = firstEnd - firstStart;
        var secondDirection = secondEnd - secondStart;
        AddAxis(axes, ref axisCount, new Vector2(-firstDirection.Y, firstDirection.X));
        AddAxis(axes, ref axisCount, new Vector2(-secondDirection.Y, secondDirection.X));
        AddAxis(axes, ref axisCount, firstStart - ClosestPoint2D.OnSegment(firstStart, secondStart, secondEnd));
        AddAxis(axes, ref axisCount, firstEnd - ClosestPoint2D.OnSegment(firstEnd, secondStart, secondEnd));
        AddAxis(axes, ref axisCount, ClosestPoint2D.OnSegment(secondStart, firstStart, firstEnd) - secondStart);
        AddAxis(axes, ref axisCount, ClosestPoint2D.OnSegment(secondEnd, firstStart, firstEnd) - secondEnd);
        AddAxis(axes, ref axisCount, (firstStart + firstEnd) / 2f - (secondStart + secondEnd) / 2f);

        if (axisCount == 0)
            axes[axisCount++] = Vector2.UnitX;

        var bestDepth = float.PositiveInfinity;
        var bestNormal = Vector2.UnitX;
        foreach (var rawAxis in axes[..axisCount])
        {
            var axis = Vector2.Normalize(rawAxis);
            ProjectCapsule(firstStart, firstEnd, firstRadius, axis, out var firstMin, out var firstMax);
            ProjectCapsule(secondStart, secondEnd, secondRadius, axis, out var secondMin, out var secondMax);

            var pushPositive = secondMax - firstMin;
            var pushNegative = firstMax - secondMin;
            if (pushPositive <= 0f || pushNegative <= 0f)
                return CollisionResult.None;

            float depth;
            Vector2 normal;
            if (pushPositive < pushNegative)
            {
                depth = pushPositive;
                normal = axis;
            }
            else
            {
                depth = pushNegative;
                normal = -axis;
            }

            if (depth < bestDepth)
            {
                bestDepth = depth;
                bestNormal = normal;
            }
        }

        var contactPoint = closest.Second + bestNormal * secondRadius;
        return CollisionResult.From(new CollisionContact2D(
            contactPoint,
            bestNormal,
            bestDepth));
    }

    private static CollisionResult RectangleVsCapsule(
        Rectangle2D rectangle,
        Transform2D rectangleTransform,
        Capsule2D capsule,
        Transform2D capsuleTransform)
    {
        if (!CollisionMath2D.TryGetWorldCapsule(capsule, capsuleTransform, out var capsuleStart, out var capsuleEnd, out var capsuleRadius))
            return CollisionResult.None;

        Span<Vector2> rectangleVertices = stackalloc Vector2[4];
        WriteWorldRectangleVertices(rectangle, rectangleTransform, rectangleVertices);
        Span<Vector2> axes = stackalloc Vector2[12];
        var axisCount = 0;
        AddPolygonEdgeAxes(axes, ref axisCount, rectangleVertices);
        var capsuleDirection = capsuleEnd - capsuleStart;
        AddAxis(axes, ref axisCount, new Vector2(-capsuleDirection.Y, capsuleDirection.X));

        foreach (var vertex in rectangleVertices)
            AddAxis(axes, ref axisCount, vertex - ClosestPoint2D.OnSegment(vertex, capsuleStart, capsuleEnd));

        var closestToStart = ClosestPointOnPolygon(capsuleStart, rectangleVertices, out _);
        var closestToEnd = ClosestPointOnPolygon(capsuleEnd, rectangleVertices, out _);
        AddAxis(axes, ref axisCount, capsuleStart - closestToStart);
        AddAxis(axes, ref axisCount, capsuleEnd - closestToEnd);

        if (!TryGetPolygonCapsuleMtv(
                rectangleVertices,
                capsuleStart,
                capsuleEnd,
                capsuleRadius,
                axes[..axisCount],
                out var normal,
                out var depth))
        {
            return CollisionResult.None;
        }

        var rectangleSurface = GetPolygonSupportPoint(rectangleVertices, -normal);
        var capsuleSurface = GetCapsuleSupportPoint(capsuleStart, capsuleEnd, capsuleRadius, normal);
        return CollisionResult.From(new CollisionContact2D(
            (rectangleSurface + capsuleSurface) / 2f,
            normal,
            depth));
    }
}
