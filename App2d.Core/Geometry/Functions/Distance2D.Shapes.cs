using App2d.Core.Geometry;
using App2d.Core.Validation;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry.Functions;

public static partial class Distance2D
{
    private static readonly Similarity2D IdentityPose = CreateIdentityPose();

    /// <summary>Point and shape share the same coordinate space. Composite signed distance is not supported.</summary>
    public static float SignedDistance(Vector2 point, IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => SignedDistanceToCircle(point, circle.Center, circle.Radius),
            Capsule2D capsule => SignedDistanceToCapsule(point, capsule.Start, capsule.End, capsule.Radius),
            IRect2D rectangle => SignedDistanceToRectangle(point, rectangle.Min, rectangle.Max),
            Triangle2D triangle => SignedDistanceToTriangle(point, triangle),
            ConvexPolygon2D polygon => SignedDistanceToConvexPolygon(point, polygon.Vertices),
            HalfSpace2D halfSpace => SignedDistanceToHalfSpace(point, halfSpace.Normal, halfSpace.Offset),
            _ => throw UnsupportedDistance(shape)
        };
    }

    public static float SignedDistance(IShape2D shape, Vector2 point) => SignedDistance(point, shape);

    /// <summary>Distance to the filled shape; points inside any composite part have distance zero.</summary>
    public static float Distance(Vector2 point, IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        if (shape is not CompositeShape2D composite) return Math.Max(0f, SignedDistance(point, shape));
        var distance = float.PositiveInfinity;
        foreach (var part in composite.Parts) distance = Math.Min(distance, Distance(point, part));
        return distance;
    }

    public static float Distance(IShape2D shape, Vector2 point) => Distance(point, shape);

    /// <summary>Both shapes are expressed in the same coordinate space; no objects or bounds caches are needed.</summary>
    public static float SignedDistance(IShape2D first, IShape2D second) => SignedDistance(first, IdentityPose, second, IdentityPose);

    public static float Distance(IShape2D first, IShape2D second) => Distance(first, IdentityPose, second, IdentityPose);

    /// <summary>
    /// World-unit distance for circles, capsules, rectangles and convex polygons in any pairing,
    /// or a convex shape against a half-space. Poses must be valid rotation/uniform-scale/mirror/translation transforms.
    /// Composite signed distance and half-space/half-space pairs are deliberately unsupported.
    /// </summary>
    public static float SignedDistance(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        if (first is HalfSpace2D firstPlane && second is IConvexShape2D secondConvex)
            return SignedDistanceToPlane(secondConvex, secondPose, firstPlane, firstPose);
        if (second is HalfSpace2D secondPlane && first is IConvexShape2D firstConvex)
            return SignedDistanceToPlane(firstConvex, firstPose, secondPlane, secondPose);

        var firstCount = CoreVertexCount(first);
        var secondCount = CoreVertexCount(second);
        var firstVertices = firstCount <= 64 ? stackalloc Vector2[firstCount] : new Vector2[firstCount];
        var secondVertices = secondCount <= 64 ? stackalloc Vector2[secondCount] : new Vector2[secondCount];
        var firstRadius = WriteCore(first, firstPose, firstVertices);
        var secondRadius = WriteCore(second, secondPose, secondVertices);
        return SignedDistanceBetweenConvexPolygons(firstVertices, secondVertices, firstRadius, secondRadius);
    }

    /// <summary>Also supports composite unions by taking the minimum distance across their convex parts.</summary>
    public static float Distance(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        if (first is CompositeShape2D composite)
        {
            var distance = float.PositiveInfinity;
            foreach (var part in composite.Parts) distance = Math.Min(distance, Distance(part, firstPose, second, secondPose));
            return distance;
        }
        if (second is CompositeShape2D) return Distance(second, secondPose, first, firstPose);
        return Math.Max(0f, SignedDistance(first, firstPose, second, secondPose));
    }

    /// <summary>Uses each spatial object's pose; results are in world units.</summary>
    public static float SignedDistance(SpatialObject2D first, SpatialObject2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return SignedDistance(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
    }

    public static float Distance(SpatialObject2D first, SpatialObject2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return Distance(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
    }

    public static float SignedDistance(Vector2 worldPoint, SpatialObject2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        var pose = shape.CollisionPose;
        return SignedDistance(pose.InverseTransformPoint(worldPoint), shape.Shape) * pose.Scale;
    }

    public static float Distance(Vector2 worldPoint, SpatialObject2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        var pose = shape.CollisionPose;
        return Distance(pose.InverseTransformPoint(worldPoint), shape.Shape) * pose.Scale;
    }

    public static float SignedDistance(SpatialObject2D shape, Vector2 worldPoint) => SignedDistance(worldPoint, shape);
    public static float Distance(SpatialObject2D shape, Vector2 worldPoint) => Distance(worldPoint, shape);

    private static float SignedDistanceToPlane(IConvexShape2D shape, Similarity2D pose, HalfSpace2D plane, Similarity2D planePose)
    {
        var normal = Vector2.Normalize(planePose.TransformDirection(plane.Normal));
        var offset = Vector2.Dot(planePose.TransformPoint(plane.Normal * plane.Offset), normal);
        var deepest = pose.TransformPoint(shape.GetSupportPoint(-pose.TransposeTransformDirection(normal)));
        return SignedDistanceToHalfSpace(deepest, normal, offset);
    }

    private static int CoreVertexCount(IShape2D shape) => shape switch
    {
        Circle2D => 1,
        Capsule2D => 2,
        Rectangle2D => 4,
        Triangle2D => 3,
        ConvexPolygon2D polygon => polygon.Vertices.Length,
        _ => throw UnsupportedDistance(shape)
    };

    private static float WriteCore(IShape2D shape, Similarity2D pose, Span<Vector2> vertices)
    {
        var radius = 0f;
        switch (shape)
        {
            case Circle2D circle:
                vertices[0] = circle.Center;
                radius = circle.Radius;
                break;
            case Capsule2D capsule:
                vertices[0] = capsule.Start;
                vertices[1] = capsule.End;
                radius = capsule.Radius;
                break;
            case Rectangle2D rectangle:
                rectangle.WriteCorners(vertices);
                break;
            case Triangle2D triangle:
                triangle.WriteVertices(vertices);
                break;
            case ConvexPolygon2D polygon:
                polygon.Vertices.CopyTo(vertices);
                break;
            default:
                throw UnsupportedDistance(shape);
        }
        for (var i = 0; i < vertices.Length; i++) vertices[i] = pose.TransformPoint(vertices[i]);
        return radius * pose.Scale;
    }

    private static NotSupportedException UnsupportedDistance(IShape2D shape) => new(
        $"Signed distance does not support {shape.GetType().Name} in this query. Composite unions support unsigned Distance only.");

    private static float SignedDistanceToTriangle(Vector2 point, Triangle2D triangle)
    {
        Span<Vector2> vertices = stackalloc Vector2[3];
        triangle.WriteVertices(vertices);
        return SignedDistanceToConvexPolygon(point, vertices);
    }

    private static Similarity2D CreateIdentityPose()
    {
        Similarity2D.TryFromMatrix(Matrix3x2.Identity, out var pose);
        return pose;
    }
}
