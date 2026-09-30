using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult EllipseAgainst(Ellipse2D ellipse, Similarity2D ellipsePose, IShape2D other, Similarity2D otherPose)
    {
        if (other is HalfSpace2D halfSpace)
            return ConvexVsHalfSpace(ellipse, ellipsePose, halfSpace, otherPose);

        Span<Vector2> ellipseVertices = stackalloc Vector2[Ellipse2D.CollisionSegments];
        ellipse.WriteVertices(ellipseVertices);
        switch (other)
        {
            case Circle2D circle:
                return CircleVsPolygon(circle, otherPose, ellipseVertices, ellipsePose).Flipped();
            case Capsule2D capsule:
                return PolygonVsCapsule(ellipseVertices, ellipsePose, capsule, otherPose);
            case Rectangle2D rectangle:
                Span<Vector2> corners = stackalloc Vector2[4];
                rectangle.WriteCorners(corners);
                return PolygonVsPolygon(ellipseVertices, ellipsePose, corners, otherPose);
            case Triangle2D triangle:
                Span<Vector2> triangleVertices = stackalloc Vector2[3];
                triangle.WriteVertices(triangleVertices);
                return PolygonVsPolygon(ellipseVertices, ellipsePose, triangleVertices, otherPose);
            case ConvexPolygon2D polygon:
                return PolygonVsPolygon(ellipseVertices, ellipsePose, polygon.Vertices, otherPose);
            case Ellipse2D otherEllipse:
                Span<Vector2> otherVertices = stackalloc Vector2[Ellipse2D.CollisionSegments];
                otherEllipse.WriteVertices(otherVertices);
                return PolygonVsPolygon(ellipseVertices, ellipsePose, otherVertices, otherPose);
            default:
                return CollisionResult.None;
        }
    }
}
