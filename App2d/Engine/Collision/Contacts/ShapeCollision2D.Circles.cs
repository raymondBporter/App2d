using System.Numerics;
using App2d.Engine.Geometry;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CircleVsRectangle(Circle2D circle, Transform2D circleTransform, Rectangle2D rectangle, Transform2D rectangleTransform)
    {
        Span<Vector2> vertices = stackalloc Vector2[4];
        WriteLocalRectangleVertices(rectangle, vertices);
        return CircleVsPolygon(circle, circleTransform, vertices, rectangleTransform);
    }

    private static CollisionResult CircleVsCircle(Circle2D first, Transform2D firstTransform, Circle2D second, Transform2D secondTransform)
    {
        if (!CollisionMath2D.TryGetWorldCircle(first, firstTransform, out var firstCenter, out var firstRadius))
            return CollisionResult.None;
        if (!CollisionMath2D.TryGetWorldCircle(second, secondTransform, out var secondCenter, out var secondRadius))
        {
            // A non-uniformly scaled circle is an ellipse. Keep exact circle math fast,
            // and use a convex boundary approximation only for this transformed case.
            Span<Vector2> ellipseBoundary = stackalloc Vector2[40];
            WriteCircleBoundary(second, ellipseBoundary);
            return CircleVsPolygon(first, firstTransform, ellipseBoundary, secondTransform);
        }

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

    private static CollisionResult CircleVsCapsule(Circle2D circle, Transform2D circleTransform, Capsule2D capsule, Transform2D capsuleTransform)
    {
        if (!CollisionMath2D.TryGetWorldCircle(circle, circleTransform, out var center, out var circleRadius) ||
            !CollisionMath2D.TryGetWorldCapsule(capsule, capsuleTransform, out var start, out var end, out var capsuleRadius))
        {
            return CollisionResult.None;
        }

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

    private static CollisionResult CircleVsHalfSpace(Circle2D circle, Transform2D circleTransform, HalfSpace2D halfSpace, Transform2D halfSpaceTransform)
    {
        if (!CollisionMath2D.TryGetWorldCircle(circle, circleTransform, out var center, out var radius))
            return CollisionResult.None;

        var (normal, offset) = CollisionMath2D.GetWorldPlane(halfSpace, halfSpaceTransform);
        var circleMinimum = Vector2.Dot(center, normal) - radius;
        var penetration = offset - circleMinimum;
        if (penetration <= 0f)
            return CollisionResult.None;

        var point = center - normal * (radius - penetration);
        return CollisionResult.From(new CollisionContact2D(point, normal, penetration));
    }

    private static CollisionResult CircleVsPolygon(Circle2D circle, Transform2D circleTransform, ReadOnlySpan<Vector2> localVertices, Transform2D polygonTransform)
    {
        if (!CollisionMath2D.TryGetWorldCircle(circle, circleTransform, out var center, out var radius))
            return CollisionResult.None;

        var polygonToWorld = polygonTransform.LocalToWorldMatrix;
        var vertices = localVertices.Length <= 64
            ? stackalloc Vector2[localVertices.Length]
            : new Vector2[localVertices.Length];
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = Vector2.Transform(localVertices[i], polygonToWorld);

        var closest = ClosestPointOnPolygon(center, vertices, out var edgeIndex);
        var centerFromBoundary = center - closest;
        var distanceSquared = centerFromBoundary.LengthSquared();
        var centerInside = ContainsPoint(vertices, center);
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
            normal = GetOutwardEdgeNormal(vertices, edgeIndex);
            penetration = radius;
        }

        return penetration > 0f
            ? CollisionResult.From(new CollisionContact2D(closest, normal, penetration))
            : CollisionResult.None;
    }
}
