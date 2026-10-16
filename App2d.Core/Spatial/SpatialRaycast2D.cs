using App2d.Core.Geometry.Operations.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Spatial;

/// <summary>
/// Ray casts against placed objects. The ray is taken into the object's local space, dispatched to the raw
/// <see cref="Raycast2D"/> primitive for its shape, and the hit is mapped back to world units.
/// </summary>
public static class SpatialRaycast2D
{
    private const float Epsilon = 0.000001f;

    /// <summary>Finds where a world ray first hits a placed object.</summary>
    /// <param name="ray">A valid world-space ray.</param>
    /// <param name="worldObject">The object to test.</param>
    /// <param name="maxDistance">The largest world distance accepted.</param>
    /// <param name="hit">The world-space hit point, unit normal and distance.</param>
    /// <returns>True when the ray reaches the object within the distance.</returns>
    public static bool TryIntersect(Ray2D ray, SpatialObject2D worldObject, float maxDistance, out RayHit2D hit)
    {
        ArgGuard.ThrowIfNull(worldObject);
        ray.Validate();
        ArgGuard.ThrowIfNegativeOrNaN(maxDistance);

        var worldBounds = worldObject.WorldBounds;
        if (worldBounds.IsFinite && !IntersectsBounds(ray, worldBounds, maxDistance))
        {
            hit = default;
            return false;
        }

        // A direction of length 1 / Scale makes the local ray parameter equal the world distance.
        var pose = worldObject.CollisionPose;
        var localOrigin = pose.InverseTransformPoint(ray.Origin);
        var localDirection = pose.InverseTransformDirection(ray.Direction);
        if (!ShapeRaycast2D.TryIntersect(localOrigin, localDirection, worldObject.Shape, maxDistance, out var localHit))
        {
            hit = default;
            return false;
        }

        // Normals transform by (A⁻¹)ᵀ = A / Scale² for this family; normalize after.
        var worldNormal = pose.TransformDirection(localHit.Normal);
        if (worldNormal.LengthSquared() <= Epsilon)
        {
            hit = default;
            return false;
        }

        hit = new RayHit2D(ray.PointAt(localHit.Distance), Vector2.Normalize(worldNormal), localHit.Distance);
        return true;
    }

    /// <summary>Tests whether a world ray passes through a bounding box within a distance. Unbounded boxes always pass.</summary>
    /// <param name="ray">A valid world-space ray.</param>
    /// <param name="bounds">The box to test.</param>
    /// <param name="maxDistance">The largest world distance considered.</param>
    /// <returns>True when the ray touches the box.</returns>
    public static bool IntersectsBounds(Ray2D ray, Rect2D bounds, float maxDistance)
    {
        ray.Validate();
        ArgGuard.ThrowIfNegativeOrNaN(maxDistance);
        return Raycast2D.IntersectsRectangle(ray.Origin, ray.Direction, bounds.Min, bounds.Max, maxDistance);
    }

}
