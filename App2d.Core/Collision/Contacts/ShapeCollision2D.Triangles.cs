using App2d.Core.Geometry;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Collision.Contacts;

public static partial class ShapeCollision2D
{
    private static CollisionResult CircleVsTriangle(Circle2D circle, Similarity2D circlePose,
        Triangle2D triangle, Similarity2D trianglePose)
    {
        Span<Vector2> vertices = stackalloc Vector2[3];
        triangle.WriteVertices(vertices);
        return CircleVsPolygon(circle, circlePose, vertices, trianglePose);
    }

    private static CollisionResult TriangleAgainst(Triangle2D triangle, Similarity2D trianglePose,
        IShape2D other, Similarity2D otherPose)
    {
        Span<Vector2> vertices = stackalloc Vector2[3];
        triangle.WriteVertices(vertices);
        switch (other)
        {
            case Circle2D circle:
                return CircleVsPolygon(circle, otherPose, vertices, trianglePose).Flipped();
            case Rectangle2D rectangle:
                Span<Vector2> rectangleVertices = stackalloc Vector2[4];
                rectangle.WriteCorners(rectangleVertices);
                return PolygonVsPolygon(vertices, trianglePose, rectangleVertices, otherPose);
            case Triangle2D otherTriangle:
                Span<Vector2> otherVertices = stackalloc Vector2[3];
                otherTriangle.WriteVertices(otherVertices);
                return PolygonVsPolygon(vertices, trianglePose, otherVertices, otherPose);
            case ConvexPolygon2D polygon:
                return PolygonVsPolygon(vertices, trianglePose, polygon.Vertices, otherPose);
            case Capsule2D capsule:
                return PolygonVsCapsule(vertices, trianglePose, capsule, otherPose);
            case HalfSpace2D halfSpace:
                return ConvexVsHalfSpace(triangle, trianglePose, halfSpace, otherPose);
            default:
                return CollisionResult.None;
        }
    }

    private static CollisionResult PolygonVsPolygon(ReadOnlySpan<Vector2> firstLocal, Similarity2D firstPose,
        ReadOnlySpan<Vector2> secondLocal, Similarity2D secondPose)
    {
        Span<Vector2> first = firstLocal.Length <= 64 ? stackalloc Vector2[firstLocal.Length] : new Vector2[firstLocal.Length];
        Span<Vector2> second = secondLocal.Length <= 64 ? stackalloc Vector2[secondLocal.Length] : new Vector2[secondLocal.Length];
        for (var i = 0; i < first.Length; i++) first[i] = firstPose.TransformPoint(firstLocal[i]);
        for (var i = 0; i < second.Length; i++) second[i] = secondPose.TransformPoint(secondLocal[i]);

        var maximumAxes = first.Length + second.Length;
        Span<Vector2> axes = maximumAxes <= 128 ? stackalloc Vector2[maximumAxes] : new Vector2[maximumAxes];
        var axisCount = 0;
        AddPolygonEdgeAxes(axes, ref axisCount, first);
        AddPolygonEdgeAxes(axes, ref axisCount, second);
        if (!TryGetPolygonMtv(first, second, axes[..axisCount], out var normal, out var depth))
            return CollisionResult.None;

        var firstSurface = PolygonGeometry2D.GetSupportPoint(first, -normal);
        var secondSurface = PolygonGeometry2D.GetSupportPoint(second, normal);
        return CollisionResult.From(new CollisionContact2D((firstSurface + secondSurface) / 2f, normal, depth));
    }
}
