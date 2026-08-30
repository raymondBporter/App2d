using App2d.Collision.Filtering;

namespace App2d.Collision.BroadPhase;

public interface IBroadPhase2D<T>
    where T : class
{
    void CollectPairs(IReadOnlyList<T> items, IPairFilter2D<T> pairFilter, List<BroadPhasePair2D<T>> pairs);
}
