using App2d.Engine.Physics;

namespace App2d.Engine.Collision.Queries;

public static class RaycastQueries2D
{
    public static bool Raycast(
        this Scene2D scene,
        Ray2D ray,
        float maxDistance,
        out RaycastHit2D<WorldObject2D> hit,
        RayQueryFilter2D<WorldObject2D>? filter = null)
    {
        ArgGuard.ThrowIfNull(scene);
        RayIntersection2D.ValidateMaxDistance(maxDistance);

        var found = false;
        var nearestDistance = maxDistance;
        hit = default;
        foreach (var worldObject in scene)
        {
            if (filter is not null && !filter(worldObject))
                continue;
            if (!RayIntersection2D.TryIntersect(ray, worldObject, nearestDistance, out var geometry))
                continue;

            found = true;
            nearestDistance = geometry.Distance;
            hit = CreateHit(worldObject, geometry);
        }

        return found;
    }

    public static int RaycastAll(
        this Scene2D scene,
        Ray2D ray,
        float maxDistance,
        Span<RaycastHit2D<WorldObject2D>> hits,
        RayQueryFilter2D<WorldObject2D>? filter = null)
    {
        ArgGuard.ThrowIfNull(scene);
        RayIntersection2D.ValidateMaxDistance(maxDistance);
        if (hits.IsEmpty)
            return 0;

        var hitCount = 0;
        foreach (var worldObject in scene)
        {
            if (filter is not null && !filter(worldObject))
                continue;
            if (!RayIntersection2D.TryIntersect(ray, worldObject, maxDistance, out var geometry))
                continue;

            InsertSorted(hits, ref hitCount, CreateHit(worldObject, geometry));
        }

        return hitCount;
    }

    public static bool Raycast(
        this PhysicsWorld2D world,
        Ray2D ray,
        float maxDistance,
        out RaycastHit2D<PhysicsBody2D> hit,
        uint layerMask = uint.MaxValue,
        bool includeSensors = true,
        RayQueryFilter2D<PhysicsBody2D>? filter = null)
    {
        ArgGuard.ThrowIfNull(world);
        RayIntersection2D.ValidateMaxDistance(maxDistance);

        var found = false;
        var nearestDistance = maxDistance;
        hit = default;
        var bodies = world.Bodies;
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            if (!ShouldQuery(body, layerMask, includeSensors, filter))
                continue;
            if (!RayIntersection2D.TryIntersect(ray, body.WorldObject, nearestDistance, out var geometry))
                continue;

            found = true;
            nearestDistance = geometry.Distance;
            hit = CreateHit(body, geometry);
        }

        return found;
    }

    public static int RaycastAll(
        this PhysicsWorld2D world,
        Ray2D ray,
        float maxDistance,
        Span<RaycastHit2D<PhysicsBody2D>> hits,
        uint layerMask = uint.MaxValue,
        bool includeSensors = true,
        RayQueryFilter2D<PhysicsBody2D>? filter = null)
    {
        ArgGuard.ThrowIfNull(world);
        RayIntersection2D.ValidateMaxDistance(maxDistance);
        if (hits.IsEmpty)
            return 0;

        var hitCount = 0;
        var bodies = world.Bodies;
        for (var bodyIndex = 0; bodyIndex < bodies.Count; bodyIndex++)
        {
            var body = bodies[bodyIndex];
            if (!ShouldQuery(body, layerMask, includeSensors, filter))
                continue;
            if (!RayIntersection2D.TryIntersect(ray, body.WorldObject, maxDistance, out var geometry))
                continue;

            InsertSorted(hits, ref hitCount, CreateHit(body, geometry));
        }

        return hitCount;
    }

    private static bool ShouldQuery(
        PhysicsBody2D body,
        uint layerMask,
        bool includeSensors,
        RayQueryFilter2D<PhysicsBody2D>? filter) =>
        body.IsCollider &&
        (body.CollisionLayer & layerMask) != 0u &&
        (includeSensors || !body.IsSensor) &&
        (filter is null || filter(body));

    private static RaycastHit2D<T> CreateHit<T>(T item, RayHit2D geometry)
        where T : class =>
        new(item, geometry.Point, geometry.Normal, geometry.Distance);

    private static void InsertSorted<T>(
        Span<RaycastHit2D<T>> hits,
        ref int hitCount,
        RaycastHit2D<T> candidate)
        where T : class
    {
        if (hitCount == hits.Length && candidate.Distance >= hits[^1].Distance)
            return;

        if (hitCount < hits.Length)
            hitCount++;

        var insertionIndex = hitCount - 1;
        while (insertionIndex > 0 && candidate.Distance < hits[insertionIndex - 1].Distance)
        {
            hits[insertionIndex] = hits[insertionIndex - 1];
            insertionIndex--;
        }

        hits[insertionIndex] = candidate;
    }
}
