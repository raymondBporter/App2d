namespace App2d.Engine.Collision.Queries;

public static class RaycastQueries2D
{
    public static bool Raycast<T>(
        this IEnumerable<T> worldObjects,
        Ray2D ray,
        float maxDistance,
        out RaycastHit2D<T> hit,
        RayQueryFilter2D<T>? filter = null)
        where T : SpatialObject2D
    {
        ArgGuard.ThrowIfNull(worldObjects);
        RayIntersection2D.ValidateMaxDistance(maxDistance);

        var found = false;
        var nearestDistance = maxDistance;
        hit = default;
        foreach (var worldObject in worldObjects)
        {
            if (filter is not null && !filter(worldObject) ||
                !RayIntersection2D.TryIntersect(
                    ray,
                    worldObject,
                    nearestDistance,
                    out var geometry))
            {
                continue;
            }

            found = true;
            nearestDistance = geometry.Distance;
            hit = CreateHit(worldObject, geometry);
        }

        return found;
    }

    public static int RaycastAll<T>(
        this IEnumerable<T> worldObjects,
        Ray2D ray,
        float maxDistance,
        Span<RaycastHit2D<T>> hits,
        RayQueryFilter2D<T>? filter = null)
        where T : SpatialObject2D
    {
        ArgGuard.ThrowIfNull(worldObjects);
        RayIntersection2D.ValidateMaxDistance(maxDistance);
        if (hits.IsEmpty)
            return 0;

        var hitCount = 0;
        foreach (var worldObject in worldObjects)
        {
            if (filter is not null && !filter(worldObject) ||
                !RayIntersection2D.TryIntersect(
                    ray,
                    worldObject,
                    maxDistance,
                    out var geometry))
            {
                continue;
            }

            InsertSorted(hits, ref hitCount, CreateHit(worldObject, geometry));
        }

        return hitCount;
    }

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
