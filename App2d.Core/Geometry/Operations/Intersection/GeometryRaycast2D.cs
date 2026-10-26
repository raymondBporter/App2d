using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Intersection;

/// <summary>One entry point for authored shape and curve values, independent of physics and rendering.</summary>
public static class GeometryRaycast2D
{
    public static bool TryIntersect(Ray2D ray, IGeometryValue2D geometry, float maxDistance, out RayHit2D hit)
    {
        ray.Validate();
        return TryIntersect(ray.Origin, ray.Direction, geometry, maxDistance, out hit);
    }

    /// <summary>
    /// Queries a shape or curve through any finite invertible affine pose, including shear and nonuniform scale.
    /// The ray is in world space; Distance remains a world distance and the normal uses the inverse transpose.
    /// </summary>
    public static bool TryIntersect(Ray2D ray, IGeometryValue2D geometry, Matrix3x2 localToWorld,
        float maxDistance, out RayHit2D hit)
    {
        ray.Validate();
        ArgGuard.Finite(localToWorld);
        ArgGuard.ThrowIf(!Matrix3x2.Invert(localToWorld, out var inverse) || !inverse.IsFinite(),
            "The geometry pose must have a finite inverse.", nameof(localToWorld));
        var origin = Vector2.Transform(ray.Origin, inverse);
        // Keep the unnormalized transformed direction: its parameter is still the world ray distance.
        var direction = Vector2.TransformNormal(ray.Direction, inverse);
        if (!TryIntersect(origin, direction, geometry, maxDistance, out var localHit))
        {
            hit = default;
            return false;
        }
        var normal = new Vector2d(
            (double)inverse.M11 * localHit.Normal.X + (double)inverse.M12 * localHit.Normal.Y,
            (double)inverse.M21 * localHit.Normal.X + (double)inverse.M22 * localHit.Normal.Y);
        hit = new(ray.PointAt(localHit.Distance), (normal / normal.Length()).ToVector2(), localHit.Distance);
        return true;
    }

    /// <summary>Distance is the parameter of the supplied, possibly unnormalized direction.</summary>
    public static bool TryIntersect(Vector2 origin, Vector2 direction, IGeometryValue2D geometry, float maxDistance, out RayHit2D hit)
    {
        ArgGuard.ThrowIfNull(geometry);
        ArgGuard.Finite(origin);
        ArgGuard.Finite(direction);
        ArgGuard.ThrowIf(direction == Vector2.Zero, "Ray direction must be nonzero.", nameof(direction));
        ArgGuard.NonNegative(maxDistance, allowInfinity: true);
        return geometry switch
        {
            IShape2D shape => ShapeRaycast2D.TryIntersect(origin, direction, shape, maxDistance, out hit),
            ICurve2D curve => CurveRaycast2D.TryIntersect(origin, direction, curve, maxDistance, out hit),
            _ => throw new NotSupportedException($"No ray query exists for {geometry.GetType().Name}.")
        };
    }
}
