using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// Distance and signed distance for shapes and placed spatial objects, built on <see cref="Distance2D"/>.
/// Signed distance is positive when separated, zero at contact and negative inside or overlapping;
/// for two convex solids its magnitude is the shortest translation needed to reach contact.
/// Finite convex pairs share the support-based query used by collision contacts. Curves are not polygonized.
/// </summary>
public static class ShapeDistance2D
{
    private static readonly Similarity2D IdentityPose = Similarity2D.FromTranslation(Vector2.Zero);

    /// <summary>Signed distance from a point to a shape in the same coordinate space.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="shape">A circle, ellipse, capsule, rectangle, triangle, convex polygon or half-space.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    /// <exception cref="NotSupportedException">The shape is a composite or an unknown type.</exception>
    public static float SignedDistance(Vector2 point, IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => Distance2D.SignedDistanceToCircle(point, circle.Center, circle.Radius),
            Ellipse2D ellipse => Distance2D.SignedDistanceToEllipse(point, ellipse.Center, ellipse.Radii),
            Capsule2D capsule => Distance2D.SignedDistanceToCapsule(point, capsule.Start, capsule.End, capsule.Radius),
            RoundedRectangle2D rectangle => Distance2D.SignedDistanceToRectangle(point, rectangle.CoreMin, rectangle.CoreMax) - rectangle.Radius,
            IRect2D rectangle => Distance2D.SignedDistanceToRectangle(point, rectangle.Min, rectangle.Max),
            HalfSpace2D halfSpace => Distance2D.SignedDistanceToHalfSpace(point, halfSpace.Normal, halfSpace.Offset),
            Triangle2D or ConvexPolygon2D => SignedDistanceToPerimeter(point, shape),
            SimplePolygon2D polygon => SignedDistanceToSimplePolygon(point, polygon),
            _ => throw Unsupported(shape)
        };
    }

    /// <summary>Signed distance from a point to a shape; argument order does not matter.</summary>
    /// <param name="shape">The shape.</param>
    /// <param name="point">The query point.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistance(IShape2D shape, Vector2 point) => SignedDistance(point, shape);

    /// <summary>Distance from a point to a filled shape; zero inside. Composites take the minimum over their parts.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="shape">Any built-in shape, including composites.</param>
    /// <returns>The nonnegative distance to the filled shape.</returns>
    public static float Distance(Vector2 point, IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        if (shape is not CompositeShape2D composite) return Math.Max(0f, SignedDistance(point, shape));
        var distance = float.PositiveInfinity;
        foreach (var part in composite.Parts) distance = Math.Min(distance, Distance(point, part));
        return distance;
    }

    /// <summary>Distance from a point to a filled shape; argument order does not matter.</summary>
    /// <param name="shape">Any built-in shape, including composites.</param>
    /// <param name="point">The query point.</param>
    /// <returns>The nonnegative distance to the filled shape.</returns>
    public static float Distance(IShape2D shape, Vector2 point) => Distance(point, shape);

    /// <summary>Signed distance between two shapes expressed in the same coordinate space.</summary>
    /// <param name="first">The first shape.</param>
    /// <param name="second">The second shape.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    public static float SignedDistance(IShape2D first, IShape2D second) => SignedDistance(first, IdentityPose, second, IdentityPose);

    /// <summary>Distance between two filled shapes in the same coordinate space; zero when touching or overlapping.</summary>
    /// <param name="first">The first shape.</param>
    /// <param name="second">The second shape.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float Distance(IShape2D first, IShape2D second) => Distance(first, IdentityPose, second, IdentityPose);

    /// <summary>
    /// Signed world-unit distance between two posed shapes. Supports every pairing of circles, ellipses, capsules,
    /// rectangles, triangles and convex polygons, plus any convex shape against a half-space.
    /// </summary>
    /// <param name="first">The first shape.</param>
    /// <param name="firstPose">The first local-to-world similarity.</param>
    /// <param name="second">The second shape.</param>
    /// <param name="secondPose">The second local-to-world similarity.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    /// <exception cref="NotSupportedException">Composite shapes, half-space pairs and unknown shape types.</exception>
    public static float SignedDistance(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        if (first is SimplePolygon2D) throw Unsupported(first);
        if (second is SimplePolygon2D) throw Unsupported(second);
        if (first is HalfSpace2D firstPlane && second is IConvexShape2D secondConvex) return SignedDistanceToPlane(secondConvex, secondPose, firstPlane, firstPose);
        if (second is HalfSpace2D secondPlane && first is IConvexShape2D firstConvex) return SignedDistanceToPlane(firstConvex, firstPose, secondPlane, secondPose);
        if (first is not IConvexShape2D convexFirst) throw Unsupported(first);
        if (second is not IConvexShape2D convexSecond) throw Unsupported(second);
        return ShapeConvexQuery2D.SignedDistance(convexFirst, firstPose, convexSecond, secondPose);
    }

    /// <summary>World-unit distance between two posed filled shapes. Composites take the minimum over their parts.</summary>
    /// <param name="first">The first shape.</param>
    /// <param name="firstPose">The first local-to-world similarity.</param>
    /// <param name="second">The second shape.</param>
    /// <param name="secondPose">The second local-to-world similarity.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float Distance(IShape2D first, Similarity2D firstPose, IShape2D second, Similarity2D secondPose)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        if (first is SimplePolygon2D firstPolygon) return Distance(firstPolygon.ConvexPieces, firstPose, second, secondPose);
        if (second is SimplePolygon2D secondPolygon) return Distance(first, firstPose, secondPolygon.ConvexPieces, secondPose);
        if (first is CompositeShape2D composite)
        {
            var distance = float.PositiveInfinity;
            foreach (var part in composite.Parts) distance = Math.Min(distance, Distance(part, firstPose, second, secondPose));
            return distance;
        }
        if (second is CompositeShape2D) return Distance(second, secondPose, first, firstPose);
        if (first is IConvexShape2D convexFirst && second is IConvexShape2D convexSecond)
            return ShapeConvexQuery2D.Distance(convexFirst, firstPose, convexSecond, secondPose);
        return Math.Max(0f, SignedDistance(first, firstPose, second, secondPose));
    }

    /// <summary>Signed world-unit distance between two placed objects using their collision poses.</summary>
    /// <param name="first">The first object.</param>
    /// <param name="second">The second object.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    public static float SignedDistance(SpatialObject2D first, SpatialObject2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return SignedDistance(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
    }

    /// <summary>World-unit distance between two placed filled objects using their collision poses.</summary>
    /// <param name="first">The first object.</param>
    /// <param name="second">The second object.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float Distance(SpatialObject2D first, SpatialObject2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return Distance(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
    }

    /// <summary>Signed world-unit distance from a world point to a placed object.</summary>
    /// <param name="worldPoint">The query point in world space.</param>
    /// <param name="shape">The placed object.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistance(Vector2 worldPoint, SpatialObject2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        var pose = shape.CollisionPose;
        return SignedDistance(pose.InverseTransformPoint(worldPoint), shape.Shape) * pose.Scale;
    }

    /// <summary>World-unit distance from a world point to a placed filled object.</summary>
    /// <param name="worldPoint">The query point in world space.</param>
    /// <param name="shape">The placed object.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float Distance(Vector2 worldPoint, SpatialObject2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        var pose = shape.CollisionPose;
        return Distance(pose.InverseTransformPoint(worldPoint), shape.Shape) * pose.Scale;
    }

    /// <summary>Signed world-unit distance from a placed object to a world point; argument order does not matter.</summary>
    /// <param name="shape">The placed object.</param>
    /// <param name="worldPoint">The query point in world space.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistance(SpatialObject2D shape, Vector2 worldPoint) => SignedDistance(worldPoint, shape);

    /// <summary>World-unit distance from a placed filled object to a world point; argument order does not matter.</summary>
    /// <param name="shape">The placed object.</param>
    /// <param name="worldPoint">The query point in world space.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float Distance(SpatialObject2D shape, Vector2 worldPoint) => Distance(worldPoint, shape);

    private static float SignedDistanceToPerimeter(Vector2 point, IShape2D shape)
    {
        var count = WorldShape2D.PerimeterVertexCount(shape);
        Span<Vector2> vertices = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
        WorldShape2D.WritePerimeter(shape, vertices);
        return Distance2D.SignedDistanceToConvexPolygon(point, vertices);
    }

    private static float SignedDistanceToSimplePolygon(Vector2 point, SimplePolygon2D polygon)
    {
        var vertices = polygon.Vertices;
        var distance = float.PositiveInfinity;
        for (var i = 0; i < vertices.Length; i++)
            distance = Math.Min(distance, Distance2D.DistanceToSegment(point, vertices[i], vertices[(i + 1) % vertices.Length]));
        return polygon.ContainsPoint(point) ? -distance : distance;
    }

    private static float SignedDistanceToPlane(IConvexShape2D shape, Similarity2D pose, HalfSpace2D plane, Similarity2D planePose)
    {
        var (normal, offset) = WorldShape2D.HalfSpace(plane, planePose);
        var deepest = pose.TransformPoint(shape.GetSupportPoint(-pose.TransposeTransformDirection(normal)));
        return Distance2D.SignedDistanceToHalfSpace(deepest, normal, offset);
    }

    private static NotSupportedException Unsupported(IShape2D shape) => new($"Signed distance does not support {shape.GetType().Name} in this query. Composite unions support unsigned Distance only.");

}
