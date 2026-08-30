using App2d.Engine.Collision.BroadPhase;
using App2d.Engine.Collision.Filtering;
using App2d.Engine.Geometry;

namespace App2d.Engine.Collision;

public sealed class CollisionSystem2D
{
    private const int MaximumCellsPerCollider = 4_096;
    private const int MaximumRetainedCells = 1_024;

    private readonly List<Collider2D> _colliders = [];
    private readonly List<BroadPhasePair2D<Collider2D>> _candidatePairs = [];
    private readonly List<Collider2D> _queryCandidates = [];
    private readonly Dictionary<GridCell, List<Collider2D>> _staticCells = [];
    private readonly Dictionary<GridCell, List<Collider2D>> _dynamicCells = [];
    private readonly List<Collider2D> _staticOverflow = [];
    private readonly List<Collider2D> _dynamicOverflow = [];
    private readonly List<GridCell> _staleCells = [];
    private readonly CombinedPairFilter _combinedFilter = new();
    private int _nextColliderId = 1;
    private int _queryStamp;
    private float _cellSize = 256f;
    private bool _staticIndexDirty = true;
    private bool _dynamicIndexDirty = true;

    public IReadOnlyList<Collider2D> Colliders => _colliders;
    public IPairFilter2D<Collider2D> PairFilter { get; set; } =
        new DefaultColliderPairFilter2D();
    public ICollisionContactProvider2D ContactProvider { get; set; } =
        new ShapeCollisionContactProvider2D();
    public int LastCandidatePairCount { get; private set; }
    public int LastNarrowPhaseTestCount { get; private set; }
    public int LastBoundsQueryCandidateCount { get; private set; }

    public float CellSize
    {
        get => _cellSize;
        set
        {
            ArgGuard.ThrowIfNotPositive(value);
            if (_cellSize == value)
                return;
            _cellSize = value;
            _staticIndexDirty = true;
            _dynamicIndexDirty = true;
        }
    }

    public Collider2D AddCollider(
        SpatialObject2D worldObject,
        ColliderMobility2D mobility = ColliderMobility2D.Static)
    {
        ArgGuard.ThrowIfNull(worldObject);
        var collider = new Collider2D(
            this,
            _nextColliderId++,
            worldObject,
            mobility);
        _colliders.Add(collider);
        MarkIndexDirty(mobility);
        return collider;
    }

    public bool RemoveCollider(Collider2D collider)
    {
        RequireOwned(collider);
        if (!_colliders.Remove(collider))
            return false;
        collider.Detach();
        MarkIndexDirty(collider.Mobility);
        return true;
    }

    public void RefreshCollider(Collider2D collider)
    {
        RequireOwned(collider);
        MarkIndexDirty(collider.Mobility);
    }

    public void CollectContacts(
        List<CollisionPair2D> contacts,
        IPairFilter2D<Collider2D>? additionalFilter = null)
    {
        ArgGuard.ThrowIfNull(contacts);
        contacts.Clear();
        _candidatePairs.Clear();
        SynchronizeIndexes();
        _combinedFilter.Primary = PairFilter;
        _combinedFilter.Additional = additionalFilter;

        foreach (var first in _colliders)
        {
            if (!first.IsEnabled || first.Mobility != ColliderMobility2D.Dynamic)
                continue;

            QueryBoundsCore(first.WorldObject.WorldBounds, _queryCandidates);
            foreach (var second in _queryCandidates)
            {
                if (ReferenceEquals(first, second) ||
                    (second.Mobility == ColliderMobility2D.Dynamic && first.Id >= second.Id) ||
                    !_combinedFilter.ShouldTest(first, second))
                {
                    continue;
                }

                _candidatePairs.Add(new BroadPhasePair2D<Collider2D>(first, second));
            }
        }

        LastCandidatePairCount = _candidatePairs.Count;
        LastNarrowPhaseTestCount = 0;
        foreach (var pair in _candidatePairs)
        {
            LastNarrowPhaseTestCount++;
            if (ContactProvider.TryGetContact(
                pair.First.WorldObject,
                pair.Second.WorldObject,
                out var contact))
            {
                contacts.Add(new CollisionPair2D(pair.First, pair.Second, contact));
            }
        }
    }

    public bool TryGetContact(
        Collider2D first,
        Collider2D second,
        out Contacts.CollisionContact2D contact)
    {
        RequireOwned(first);
        RequireOwned(second);
        if (!PairFilter.ShouldTest(first, second) ||
            !first.WorldObject.WorldBounds.Intersects(second.WorldObject.WorldBounds))
        {
            contact = default;
            return false;
        }

        return ContactProvider.TryGetContact(
            first.WorldObject,
            second.WorldObject,
            out contact);
    }

    public bool TryGetContact(
        SpatialObject2D query,
        Collider2D collider,
        out Contacts.CollisionContact2D contact)
    {
        ArgGuard.ThrowIfNull(query);
        RequireOwned(collider);
        if (!collider.IsEnabled ||
            !query.WorldBounds.Intersects(collider.WorldObject.WorldBounds))
        {
            contact = default;
            return false;
        }

        return ContactProvider.TryGetContact(
            query,
            collider.WorldObject,
            out contact);
    }

    public int QueryBounds(Bounds2D bounds, List<Collider2D> results, uint layerMask = uint.MaxValue, bool includeSensors = true, Collider2D? excluded = null)
    {
        ArgGuard.ThrowIfNull(results);
        if (excluded is not null)
            RequireOwned(excluded);
        SynchronizeIndexes();
        QueryBoundsCore(bounds, results);
        LastBoundsQueryCandidateCount = results.Count;

        for (var index = results.Count - 1; index >= 0; index--)
        {
            var collider = results[index];
            if (!collider.IsEnabled || ReferenceEquals(collider, excluded) ||
                (collider.CollisionLayer & layerMask) == 0u ||
                (!includeSensors && collider.IsSensor))
            {
                results.RemoveAt(index);
            }
        }

        return results.Count;
    }

    public int Overlap(SpatialObject2D query, List<CollisionOverlap2D> overlaps, uint layerMask = uint.MaxValue, bool includeSensors = true, Collider2D? excluded = null)
    {
        ArgGuard.ThrowIfNull(query);
        ArgGuard.ThrowIfNull(overlaps);
        overlaps.Clear();
        QueryBounds(query.WorldBounds, _queryCandidates, layerMask, includeSensors, excluded);

        foreach (var collider in _queryCandidates)
        {
            if (ContactProvider.TryGetContact(query, collider.WorldObject, out var contact))
            {
                overlaps.Add(new CollisionOverlap2D(collider, contact));
            }
        }

        return overlaps.Count;
    }

    internal void OnMobilityChanged(ColliderMobility2D previous, ColliderMobility2D current)
    {
        MarkIndexDirty(previous);
        MarkIndexDirty(current);
    }

    internal void OnTransformChanged(Collider2D collider) => MarkIndexDirty(collider.Mobility);

    private void SynchronizeIndexes()
    {
        if (_staticIndexDirty)
        {
            RebuildIndex(
                ColliderMobility2D.Static,
                _staticCells,
                _staticOverflow);
            _staticIndexDirty = false;
        }

        if (_dynamicIndexDirty)
        {
            RebuildIndex(
                ColliderMobility2D.Dynamic,
                _dynamicCells,
                _dynamicOverflow);
            _dynamicIndexDirty = false;
        }
    }

    private void RebuildIndex(ColliderMobility2D mobility, Dictionary<GridCell, List<Collider2D>> cells, List<Collider2D> overflow)
    {
        foreach (var bucket in cells.Values)
            bucket.Clear();
        overflow.Clear();
        foreach (var collider in _colliders)
        {
            if (collider.Mobility != mobility)
                continue;
            AddToIndex(collider, cells, overflow);
        }

        // Buckets are kept for reuse, but drop empties once the index grows
        // large so a wandering world can't retain cells forever.
        if (cells.Count <= MaximumRetainedCells)
            return;
        _staleCells.Clear();
        foreach (var (cell, bucket) in cells)
        {
            if (bucket.Count == 0)
                _staleCells.Add(cell);
        }
        foreach (var cell in _staleCells)
            cells.Remove(cell);
    }

    private void AddToIndex(Collider2D collider, Dictionary<GridCell, List<Collider2D>> cells, List<Collider2D> overflow)
    {
        var bounds = collider.WorldObject.WorldBounds;
        if (!TryGetCellRange(bounds, out var range) ||
            range.CellCount > MaximumCellsPerCollider)
        {
            overflow.Add(collider);
            return;
        }

        for (var y = range.MinimumY; y <= range.MaximumY; y++)
        {
            for (var x = range.MinimumX; x <= range.MaximumX; x++)
            {
                var key = new GridCell(x, y);
                if (!cells.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    cells.Add(key, bucket);
                }
                bucket.Add(collider);
            }
        }
    }

    private void QueryBoundsCore(Bounds2D bounds, List<Collider2D> results)
    {
        results.Clear();
        var stamp = NextQueryStamp();
        if (!TryGetCellRange(bounds, out var range) ||
            range.CellCount > MaximumCellsPerCollider)
        {
            foreach (var collider in _colliders)
                TryAddQueryCandidate(collider, bounds, stamp, results);
            return;
        }

        QueryCells(_staticCells, range, bounds, stamp, results);
        QueryCells(_dynamicCells, range, bounds, stamp, results);
        foreach (var collider in _staticOverflow)
            TryAddQueryCandidate(collider, bounds, stamp, results);
        foreach (var collider in _dynamicOverflow)
            TryAddQueryCandidate(collider, bounds, stamp, results);
    }

    private static void QueryCells(Dictionary<GridCell, List<Collider2D>> cells, CellRange range, Bounds2D bounds, int stamp, List<Collider2D> results)
    {
        for (var y = range.MinimumY; y <= range.MaximumY; y++)
        {
            for (var x = range.MinimumX; x <= range.MaximumX; x++)
            {
                if (cells.TryGetValue(new GridCell(x, y), out var bucket))
                {
                    foreach (var collider in bucket)
                    {
                        TryAddQueryCandidate(collider, bounds, stamp, results);
                    }
                }
            }
        }
    }

    private static void TryAddQueryCandidate(Collider2D collider, Bounds2D bounds, int stamp, List<Collider2D> results)
    {
        if (collider.QueryStamp == stamp ||
            !bounds.Intersects(collider.WorldObject.WorldBounds))
        {
            return;
        }
        collider.QueryStamp = stamp;
        results.Add(collider);
    }

    private bool TryGetCellRange(Bounds2D bounds, out CellRange range)
    {
        if (!bounds.IsFinite)
        {
            range = default;
            return false;
        }

        var minimumX = (int)MathF.Floor(bounds.Left / CellSize);
        var maximumX = (int)MathF.Floor(bounds.Right / CellSize);
        var minimumY = (int)MathF.Floor(bounds.Bottom / CellSize);
        var maximumY = (int)MathF.Floor(bounds.Top / CellSize);
        range = new CellRange(minimumX, maximumX, minimumY, maximumY);
        return true;
    }

    private int NextQueryStamp()
    {
        if (_queryStamp != int.MaxValue)
            return ++_queryStamp;

        foreach (var collider in _colliders)
            collider.QueryStamp = 0;
        _queryStamp = 1;
        return _queryStamp;
    }

    private void MarkIndexDirty(ColliderMobility2D mobility)
    {
        if (mobility == ColliderMobility2D.Static)
            _staticIndexDirty = true;
        else
            _dynamicIndexDirty = true;
    }

    private void RequireOwned(Collider2D collider)
    {
        ArgGuard.ThrowIfNull(collider);
        StateGuard.ThrowIf(!ReferenceEquals(collider.System, this), "The collider belongs to a different collision system.");
    }

    private sealed class CombinedPairFilter : IPairFilter2D<Collider2D>
    {
        public IPairFilter2D<Collider2D> Primary { get; set; } = new DefaultColliderPairFilter2D();
        public IPairFilter2D<Collider2D>? Additional { get; set; }

        public bool ShouldTest(Collider2D first, Collider2D second) => Primary.ShouldTest(first, second) && (Additional?.ShouldTest(first, second) != false);
    }

    private readonly record struct GridCell(int X, int Y);

    private readonly record struct CellRange(int MinimumX, int MaximumX, int MinimumY, int MaximumY)
    {
        public long CellCount => (long)(MaximumX - MinimumX + 1) * (MaximumY - MinimumY + 1);
    }
}
