using App2d.Core.Geometry.Distance;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CircleVsSimplePolygon(Circle2D circle, Similarity2D circlePose, SimplePolygon2D polygon, Similarity2D polygonPose)
    {
        var center = polygonPose.InverseTransformPoint(circlePose.TransformPoint(circle.Center));
        var radius = circle.Radius * circlePose.Scale / polygonPose.Scale;
        var closest = ClosestPoint2D.OnPolygonPerimeter(center, polygon.Vertices, out var edge);
        var distance = Vector2f.Distance(center, closest);
        var inside = polygon.ContainsPoint(center);
        var signedDistance = inside ? -distance : distance;
        if (signedDistance >= radius) return CollisionResult.None;

        // Outside: point from the boundary toward the circle. Inside: point toward the nearest exit.
        // At a vertex, the radial normal naturally connects the neighboring faces without a seam wall.
        var localNormal = distance > 1e-7f
            ? (inside ? closest - center : center - closest) / distance
            : PolygonGeometry2D.GetOutwardEdgeNormal(polygon.Vertices, edge);
        var normal = Vector2f.Normalize(polygonPose.TransformDirection(localNormal));
        return CollisionResult.From(new CollisionContact2D(polygonPose.TransformPoint(closest), normal,
            (radius - signedDistance) * polygonPose.Scale));
    }
}
