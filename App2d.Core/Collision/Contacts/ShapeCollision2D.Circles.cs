using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CircleVsCircle(Circle2D first, Similarity2D firstPose, Circle2D second, Similarity2D secondPose)
    {
        var (firstCenter, firstRadius) = WorldShape2D.Circle(first, firstPose);
        var (secondCenter, secondRadius) = WorldShape2D.Circle(second, secondPose);

        var delta = firstCenter - secondCenter;
        var distanceSquared = delta.LengthSquared();
        var combinedRadius = firstRadius + secondRadius;
        if (distanceSquared >= combinedRadius * combinedRadius) return CollisionResult.None;

        var distance = MathF.Sqrt(distanceSquared);
        var normal = distance > float.Epsilon ? delta / distance : Vector2.UnitX;
        var point = secondCenter + normal * secondRadius;
        return CollisionResult.From(new CollisionContact2D(point, normal, combinedRadius - distance));
    }

    private static CollisionResult CircleVsCapsule(Circle2D circle, Similarity2D circlePose, Capsule2D capsule, Similarity2D capsulePose)
    {
        var (center, circleRadius) = WorldShape2D.Circle(circle, circlePose);
        var (start, end, capsuleRadius) = WorldShape2D.Capsule(capsule, capsulePose);

        var segmentPoint = ClosestPoint2D.OnSegment(center, start, end);
        var delta = center - segmentPoint;
        var distanceSquared = delta.LengthSquared();
        var combinedRadius = circleRadius + capsuleRadius;
        if (distanceSquared >= combinedRadius * combinedRadius) return CollisionResult.None;

        var distance = MathF.Sqrt(distanceSquared);
        Vector2 normal;
        if (distance > float.Epsilon)
        {
            normal = delta / distance;
        }
        else
        {
            var segment = end - start;
            normal = segment.LengthSquared() > float.Epsilon ? Vector2.Normalize(segment.PerpCcw) : Vector2.UnitY;
        }

        return CollisionResult.From(new CollisionContact2D(segmentPoint + normal * capsuleRadius, normal, combinedRadius - distance));
    }

    private static CollisionResult CircleVsHalfSpace(Circle2D circle, Similarity2D circlePose, HalfSpace2D halfSpace, Similarity2D halfSpacePose)
    {
        var (center, radius) = WorldShape2D.Circle(circle, circlePose);
        var (normal, offset) = WorldShape2D.HalfSpace(halfSpace, halfSpacePose);
        var penetration = radius - Distance2D.SignedDistanceToHalfSpace(center, normal, offset);
        if (penetration <= 0f) return CollisionResult.None;

        var point = center - normal * (radius - penetration);
        return CollisionResult.From(new CollisionContact2D(point, normal, penetration));
    }

    private static CollisionResult CircleVsPolygon(Circle2D circle, Similarity2D circlePose, ReadOnlySpan<Vector2> localVertices, Similarity2D polygonPose)
    {
        var (center, radius) = WorldShape2D.Circle(circle, circlePose);
        Span<Vector2> vertices = localVertices.Length <= StackVertexLimit ? stackalloc Vector2[localVertices.Length] : new Vector2[localVertices.Length];
        for (var i = 0; i < vertices.Length; i++) vertices[i] = polygonPose.TransformPoint(localVertices[i]);

        var signedDistance = Distance2D.SignedDistanceToConvexPolygon(center, vertices, out var closest, out var edgeIndex, collinearEpsilon: 0.0001f);
        var penetration = radius - signedDistance;
        if (penetration <= 0f) return CollisionResult.None;

        var centerFromBoundary = center - closest;
        var distance = MathF.Abs(signedDistance);
        var normal = distance > float.Epsilon
            ? (signedDistance < 0f ? -centerFromBoundary / distance : centerFromBoundary / distance)
            : PolygonGeometry2D.GetOutwardEdgeNormal(vertices, edgeIndex);
        return CollisionResult.From(new CollisionContact2D(closest, normal, penetration));
    }

    // Exact: the circle center is taken into ellipse space, where the closest perimeter point gives both the
    // escape direction and the depth. Both are mapped back through the similarity, which preserves directions up to scale.
    private static CollisionResult CircleVsEllipse(Circle2D circle, Similarity2D circlePose, Ellipse2D ellipse, Similarity2D ellipsePose)
    {
        var (worldCenter, worldRadius) = WorldShape2D.Circle(circle, circlePose);
        var center = ellipsePose.InverseTransformPoint(worldCenter);
        var radius = worldRadius / ellipsePose.Scale;
        var closest = ClosestPoint2D.OnEllipsePerimeter(center, ellipse.Center, ellipse.Radii);
        var inside = Containment2D.Ellipse(center, ellipse.Center, ellipse.Radii);
        var distance = Vector2.Distance(center, closest);
        var penetration = radius - (inside ? -distance : distance);
        if (penetration <= 0f) return CollisionResult.None;

        var normal = distance > float.Epsilon
            ? (inside ? closest - center : center - closest) / distance
            : Vector2.Normalize((closest - ellipse.Center) / (ellipse.Radii * ellipse.Radii));
        var worldNormal = Vector2.Normalize(ellipsePose.TransformDirection(normal));
        return CollisionResult.From(new CollisionContact2D(ellipsePose.TransformPoint(closest), worldNormal, penetration * ellipsePose.Scale));
    }
}
