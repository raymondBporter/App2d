using App2d.Core;
using App2d.Core.Geometry;
using App2d.Collision.Filtering;

namespace App2d.Collision.BroadPhase;

// O(n^2), but with cheap policy and world-AABB rejection before narrow phase.
// This is often faster than maintaining a tree for small, highly dynamic scenes.
public sealed class BruteForceAabbBroadPhase2D<T> : IBroadPhase2D<T>
    where T : class
{
    private readonly Func<T, Bounds2D> _getBounds;
    private readonly List<Bounds2D> _worldBounds = [];

    public BruteForceAabbBroadPhase2D(Func<T, Bounds2D> getBounds)
    {
        ArgGuard.ThrowIfNull(getBounds);
        _getBounds = getBounds;
    }

    public void CollectPairs(IReadOnlyList<T> items, IPairFilter2D<T> pairFilter, List<BroadPhasePair2D<T>> pairs)
    {
        ArgGuard.ThrowIfNull(items);
        ArgGuard.ThrowIfNull(pairFilter);
        ArgGuard.ThrowIfNull(pairs);

        pairs.Clear();
        _worldBounds.Clear();
        _worldBounds.EnsureCapacity(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            _worldBounds.Add(_getBounds(items[i]));
        }

        for (var first = 0; first < items.Count; first++)
        {
            for (var second = first + 1; second < items.Count; second++)
            {
                if (pairFilter.ShouldTest(items[first], items[second]) && _worldBounds[first].Intersects(_worldBounds[second]))
                {
                    pairs.Add(new BroadPhasePair2D<T>(items[first], items[second]));
                }
            }
        }
    }
}
