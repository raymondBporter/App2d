using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Spatial;

/// <summary>World-unit distances for placed objects, using their validated collision poses.</summary>
public static class SpatialDistance2D
{
    /// <summary>Signed world-unit distance between two placed objects using their collision poses.</summary>
    /// <param name="first">The first object.</param>
    /// <param name="second">The second object.</param>
    /// <returns>The gap when separated, or the negated minimum escape translation when overlapping.</returns>
    public static float SignedDistance(SpatialObject2D first, SpatialObject2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return ShapeDistance2D.SignedDistance(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
    }

    /// <summary>World-unit distance between two placed filled objects using their collision poses.</summary>
    /// <param name="first">The first object.</param>
    /// <param name="second">The second object.</param>
    /// <returns>The nonnegative gap.</returns>
    public static float Distance(SpatialObject2D first, SpatialObject2D second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return ShapeDistance2D.Distance(first.Shape, first.CollisionPose, second.Shape, second.CollisionPose);
    }

    /// <summary>Signed world-unit distance from a world point to a placed object.</summary>
    /// <param name="worldPoint">The query point in world space.</param>
    /// <param name="shape">The placed object.</param>
    /// <returns>Negative inside, zero on the boundary, positive outside.</returns>
    public static float SignedDistance(Vector2 worldPoint, SpatialObject2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        var pose = shape.CollisionPose;
        return ShapeDistance2D.SignedDistance(pose.InverseTransformPoint(worldPoint), shape.Shape) * pose.Scale;
    }

    /// <summary>World-unit distance from a world point to a placed filled object.</summary>
    /// <param name="worldPoint">The query point in world space.</param>
    /// <param name="shape">The placed object.</param>
    /// <returns>The nonnegative distance.</returns>
    public static float Distance(Vector2 worldPoint, SpatialObject2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        var pose = shape.CollisionPose;
        return ShapeDistance2D.Distance(pose.InverseTransformPoint(worldPoint), shape.Shape) * pose.Scale;
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

}
