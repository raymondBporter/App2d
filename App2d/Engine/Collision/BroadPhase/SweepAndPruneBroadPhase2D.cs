using App2d.Engine.Collision.Filtering;
using App2d.Engine.Geometry;

namespace App2d.Engine.Collision.BroadPhase;

// Sort by the beginning of one AABB interval, then only test later intervals whose
// beginning is before the current interval's end. The other axis is the prune step.
public sealed class SweepAndPruneBroadPhase2D<T> : IBroadPhase2D<T>
    where T : class
{
    private readonly Func<T, Bounds2D> _getBounds;
    private readonly List<Proxy> _proxies = [];

    public SweepAndPruneBroadPhase2D(
        Func<T, Bounds2D> getBounds,
        SweepAxis2D axis = SweepAxis2D.X)
    {
        ArgGuard.ThrowIfNull(getBounds);
        _getBounds = getBounds;
        Axis = axis;
    }

    public SweepAxis2D Axis { get; set; }

    public void CollectPairs(
        IReadOnlyList<T> items,
        IPairFilter2D<T> pairFilter,
        List<BroadPhasePair2D<T>> pairs)
    {
        ArgGuard.ThrowIfNull(items);
        ArgGuard.ThrowIfNull(pairFilter);
        ArgGuard.ThrowIfNull(pairs);

        pairs.Clear();
        WriteProxies(items);
        _proxies.Sort(CompareProxies);

        for (var currentIndex = 0; currentIndex < _proxies.Count; currentIndex++)
        {
            var current = _proxies[currentIndex];
            for (var candidateIndex = currentIndex + 1; candidateIndex < _proxies.Count; candidateIndex++)
            {
                var candidate = _proxies[candidateIndex];
                if (candidate.PrimaryMinimum > current.PrimaryMaximum)
                    break;
                if (!OverlapsSecondaryAxis(current, candidate))
                    continue;

                var first = current.Ordinal < candidate.Ordinal ? current.Item : candidate.Item;
                var second = current.Ordinal < candidate.Ordinal ? candidate.Item : current.Item;
                if (pairFilter.ShouldTest(first, second))
                    pairs.Add(new BroadPhasePair2D<T>(first, second));
            }
        }
    }

    private void WriteProxies(IReadOnlyList<T> items)
    {
        _proxies.Clear();
        _proxies.EnsureCapacity(items.Count);

        for (var itemIndex = 0; itemIndex < items.Count; itemIndex++)
        {
            var bounds = _getBounds(items[itemIndex]);
            _proxies.Add(Axis switch
            {
                SweepAxis2D.X => new Proxy(items[itemIndex], itemIndex, bounds.Left, bounds.Right, bounds.Bottom, bounds.Top),
                SweepAxis2D.Y => new Proxy(items[itemIndex], itemIndex, bounds.Bottom, bounds.Top, bounds.Left, bounds.Right),
                _ => throw StateGuard.Create($"Unknown sweep axis: {Axis}.")
            });
        }
    }

    private static bool OverlapsSecondaryAxis(Proxy first, Proxy second) =>
        first.SecondaryMinimum <= second.SecondaryMaximum &&
        first.SecondaryMaximum >= second.SecondaryMinimum;

    private static int CompareProxies(Proxy first, Proxy second)
    {
        var comparison = first.PrimaryMinimum.CompareTo(second.PrimaryMinimum);
        if (comparison != 0)
            return comparison;

        comparison = first.PrimaryMaximum.CompareTo(second.PrimaryMaximum);
        return comparison != 0
            ? comparison
            : first.Ordinal.CompareTo(second.Ordinal);
    }

    private readonly record struct Proxy(
        T Item,
        int Ordinal,
        float PrimaryMinimum,
        float PrimaryMaximum,
        float SecondaryMinimum,
        float SecondaryMaximum);
}
