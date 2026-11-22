using App2d.Core.Collision.Queries;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Spatial;
using App2d.Core.Validation;

namespace App2d.Core.Physics.Queries;

public static class PhysicsRaycastQueries2D
{
    extension(PhysicsWorld2D world)
    {
        public bool Raycast(Ray2D ray, float maxDistance, out RaycastHit2D<RigidBody2D> hit, uint layerMask = uint.MaxValue, bool includeSensors = true,
            RayQueryFilter2D<RigidBody2D>? filter = null)
        {
            Ensure.ThrowIfNull(world);
            ray.Validate();
            Ensure.NonNegative(maxDistance, allowInfinity: true);

            var found = false;
            var nearestDistance = maxDistance;
            hit = default;
            foreach (var body in world.Bodies)
            {
                if (!ShouldQuery(body, layerMask, includeSensors, filter) ||
                    !SpatialRaycast2D.TryIntersect(
                        ray,
                        body.WorldObject,
                        nearestDistance,
                        out var geometry))
                {
                    continue;
                }

                found = true;
                nearestDistance = geometry.Distance;
                hit = new(body, geometry);
            }

            return found;
        }

        public int RaycastAll(Ray2D ray, float maxDistance, Span<RaycastHit2D<RigidBody2D>> hits, uint layerMask = uint.MaxValue, bool includeSensors = true,
            RayQueryFilter2D<RigidBody2D>? filter = null)
        {
            Ensure.ThrowIfNull(world);
            ray.Validate();
            Ensure.NonNegative(maxDistance, allowInfinity: true);
            if (hits.IsEmpty)
            {
                return 0;
            }

            var hitCount = 0;
            foreach (var body in world.Bodies)
            {
                if (!ShouldQuery(body, layerMask, includeSensors, filter) ||
                    !SpatialRaycast2D.TryIntersect(ray, body.WorldObject, maxDistance, out var geometry))
                {
                    continue;
                }

                RaycastQueries2D.InsertSorted(hits, ref hitCount, new(body, geometry));
            }

            return hitCount;
        }
    }

    private static bool ShouldQuery(RigidBody2D body, uint layerMask, bool includeSensors, RayQueryFilter2D<RigidBody2D>? filter)
    {
        return body.IsCollider && (body.CollisionLayer & layerMask) != 0u && (includeSensors || !body.IsSensor) && (filter is null || filter(body));
    }
}
