using App2d.Core.Geometry;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Collision.Queries;

/// <summary>
/// Ray casts against placed objects. The ray is taken into the object's local space, dispatched to the raw
/// <see cref="Raycast2D"/> primitive for its shape, and the hit is mapped back to world units.
/// </summary>
public static class RayIntersection2D
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
        if (!TryIntersectLocal(localOrigin, localDirection, worldObject.Shape, maxDistance, out var localHit))
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

        hit = new RayHit2D(ray.GetPoint(localHit.Distance), Vector2.Normalize(worldNormal), localHit.Distance);
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

    private static bool TryIntersectLocal(Vector2 origin, Vector2 direction, IShape2D shape, float maxDistance, out RayHit2D hit)
    {
        switch (shape)
        {
            case Circle2D circle: return Raycast2D.TryCircle(origin, direction, circle.Center, circle.Radius, maxDistance, out hit);
            case Ellipse2D ellipse: return Raycast2D.TryEllipse(origin, direction, ellipse.Center, ellipse.Radii, maxDistance, out hit);
            case Capsule2D capsule: return Raycast2D.TryCapsule(origin, direction, capsule.Start, capsule.End, capsule.Radius, maxDistance, out hit);
            case HalfSpace2D halfSpace: return Raycast2D.TryHalfSpace(origin, direction, halfSpace.Normal, halfSpace.Offset, maxDistance, out hit);
            case SimplePolygon2D polygon: return TrySimplePolygon(origin, direction, polygon, maxDistance, out hit);
            case CompositeShape2D composite:
                hit = default;
                var found = false;
                foreach (var part in composite.Parts)
                {
                    if (TryIntersectLocal(origin, direction, part, maxDistance, out var partHit) && (!found || partHit.Distance < hit.Distance))
                    {
                        hit = partHit;
                        found = true;
                    }
                }
                return found;
            default:
                var count = WorldShape2D.PerimeterVertexCount(shape);
                if (count == 0)
                {
                    hit = default;
                    return false;
                }
                Span<Vector2> vertices = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
                WorldShape2D.WritePerimeter(shape, vertices);
                return Raycast2D.TryConvexPolygon(origin, direction, vertices, maxDistance, out hit);
        }
    }

    private static bool TrySimplePolygon(Vector2 origin, Vector2 direction, SimplePolygon2D polygon, float maxDistance, out RayHit2D hit)
    {
        var vertices = polygon.Vertices;
        var winding = Math.Sign(PolygonGeometry2D.SignedAreaTwiceDouble(vertices));
        hit = default;
        var found = false;
        for (var i = 0; i < vertices.Length; i++)
        {
            var start = vertices[i];
            var edge = vertices[(i + 1) % vertices.Length] - start;
            var denominator = direction.X * edge.Y - direction.Y * edge.X;
            if (MathF.Abs(denominator) < Epsilon) continue;
            var offset = start - origin;
            var distance = (offset.X * edge.Y - offset.Y * edge.X) / denominator;
            var fraction = (offset.X * direction.Y - offset.Y * direction.X) / denominator;
            if (distance < 0 || distance > maxDistance || fraction < 0 || fraction > 1 || found && distance >= hit.Distance) continue;
            var normal = Vector2.Normalize(new Vector2(edge.Y, -edge.X) * winding);
            hit = new RayHit2D(origin + direction * distance, normal, distance);
            found = true;
        }
        return found;
    }
}
