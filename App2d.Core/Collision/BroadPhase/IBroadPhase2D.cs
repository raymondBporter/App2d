using App2d.Core.Collision.Filtering;

namespace App2d.Core.Collision.BroadPhase;

public interface IBroadPhase2D<T>
    where T : class
{
    void CollectPairs(IReadOnlyList<T> items, IPairFilter2D<T> pairFilter, List<BroadPhasePair2D<T>> pairs);
}
