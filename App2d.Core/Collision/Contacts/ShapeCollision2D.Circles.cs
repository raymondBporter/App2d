using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CircleVsRectangle(Circle2D circle, Similarity2D circlePose, Rectangle2D rectangle, Similarity2D rectanglePose)
    {
        Span<Vector2> vertices = stackalloc Vector2[4];
        rectangle.WriteCorners(vertices);
        return CircleVsPolygon(circle, circlePose, vertices, rectanglePose);
    }

    private static CollisionResult CircleVsCircle(Circle2D first, Similarity2D firstPose, Circle2D second, Similarity2D secondPose)
    {
        var (firstCenter, firstRadius) = CollisionMath2D.GetWorldCircle(first, firstPose);
        var (secondCenter, secondRadius) = CollisionMath2D.GetWorldCircle(second, secondPose);

        var delta = firstCenter - secondCenter;
        var distanceSquared = delta.LengthSquared();
        var combinedRadius = firstRadius + secondRadius;
        if (distanceSquared >= combinedRadius * combinedRadius)
            return CollisionResult.None;

        var distance = MathF.Sqrt(distanceSquared);
        var normal = distance > float.Epsilon ? delta / distance : Vector2.UnitX;
        var point = secondCenter + normal * secondRadius;
        return CollisionResult.From(new CollisionContact2D(point, normal, combinedRadius - distance));
    }

    private static CollisionResult CircleVsCapsule(Circle2D circle, Similarity2D circlePose, Capsule2D capsule, Similarity2D capsulePose)
    {
        var (center, circleRadius) = CollisionMath2D.GetWorldCircle(circle, circlePose);
        var (start, end, capsuleRadius) = CollisionMath2D.GetWorldCapsule(capsule, capsulePose);

        var segmentPoint = ClosestPoint2D.OnSegment(center, start, end);
        var delta = center - segmentPoint;
        var distanceSquared = delta.LengthSquared();
        var combinedRadius = circleRadius + capsuleRadius;
        if (distanceSquared >= combinedRadius * combinedRadius)
            return CollisionResult.None;

        var distance = MathF.Sqrt(distanceSquared);
        Vector2 normal;
        if (distance > float.Epsilon)
        {
            normal = delta / distance;
        }
        else
        {
            var segment = end - start;
            normal = segment.LengthSquared() > float.Epsilon
                ? Vector2.Normalize(segment.PerpCcw)
                : Vector2.UnitY;
        }

        return CollisionResult.From(new CollisionContact2D(segmentPoint + normal * capsuleRadius, normal, combinedRadius - distance));
    }

    private static CollisionResult CircleVsHalfSpace(Circle2D circle, Similarity2D circlePose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        var (center, radius) = CollisionMath2D.GetWorldCircle(circle, circlePose);
        var (normal, offset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpacePose);
        var penetration = radius - Distance2D.SignedDistanceToHalfSpace(center, normal, offset);
        if (penetration <= 0f)
            return CollisionResult.None;

        var point = center - normal * (radius - penetration);
        return CollisionResult.From(new CollisionContact2D(point, normal, penetration));
    }

    private static CollisionResult CircleVsPolygon(Circle2D circle, Similarity2D circlePose, ReadOnlySpan<Vector2> localVertices, Similarity2D polygonPose)
    {
        var (center, radius) = CollisionMath2D.GetWorldCircle(circle, circlePose);

        var vertices = localVertices.Length <= 64
            ? stackalloc Vector2[localVertices.Length]
            : new Vector2[localVertices.Length];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = polygonPose.TransformPoint(localVertices[i]);

        var signedDistance = Distance2D.SignedDistanceToConvexPolygon(center, vertices, out var closest, out var edgeIndex,
            collinearEpsilon: 0.0001f);
        var penetration = radius - signedDistance;
        if (penetration <= 0f)
            return CollisionResult.None;

        var centerFromBoundary = center - closest;
        var distance = MathF.Abs(signedDistance);
        Vector2 normal;
        if (distance > float.Epsilon)
        {
            normal = signedDistance < 0f ? -centerFromBoundary / distance : centerFromBoundary / distance;
        }
        else
        {
            normal = PolygonGeometry2D.GetOutwardEdgeNormal(vertices, edgeIndex);
        }

        return penetration > 0f
            ? CollisionResult.From(new CollisionContact2D(closest, normal, penetration))
            : CollisionResult.None;
    }
}
