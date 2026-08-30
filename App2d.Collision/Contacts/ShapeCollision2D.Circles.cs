using System.Numerics;
using App2d.Collision;
using App2d.Core.Geometry;
using App2d.Core.Mathematics;

namespace App2d.Collision.Contacts;

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
                ? Vector2.Normalize(segment.PerpCcw())
                : Vector2.UnitY;
        }

        return CollisionResult.From(new CollisionContact2D(segmentPoint + normal * capsuleRadius, normal, combinedRadius - distance));
    }

    private static CollisionResult CircleVsHalfSpace(Circle2D circle, Similarity2D circlePose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        var (center, radius) = CollisionMath2D.GetWorldCircle(circle, circlePose);
        var (normal, offset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpacePose);
        var circleMinimum = Vector2.Dot(center, normal) - radius;
        var penetration = offset - circleMinimum;
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

        var closest = PolygonGeometry2D.ClosestPointOnPerimeter(center, vertices, out var edgeIndex);
        var centerFromBoundary = center - closest;
        var distanceSquared = centerFromBoundary.LengthSquared();
        var centerInside = PolygonGeometry2D.ContainsPoint(vertices, center);
        if (!centerInside && distanceSquared >= radius * radius)
            return CollisionResult.None;

        var distance = MathF.Sqrt(distanceSquared);
        Vector2 normal;
        float penetration;
        if (distance > float.Epsilon)
        {
            normal = centerInside ? -centerFromBoundary / distance : centerFromBoundary / distance;
            penetration = centerInside ? radius + distance : radius - distance;
        }
        else
        {
            normal = PolygonGeometry2D.GetOutwardEdgeNormal(vertices, edgeIndex);
            penetration = radius;
        }

        return penetration > 0f
            ? CollisionResult.From(new CollisionContact2D(closest, normal, penetration))
            : CollisionResult.None;
    }
}
