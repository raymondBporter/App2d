using App2d.Core;
using System.Collections.Immutable;

namespace App2d.Collision;

public sealed record CollisionOrderState2D(ImmutableArray<int> ColliderIds, int NextColliderId, float CellSize);

public sealed partial class CollisionSystem2D
{
    public CollisionOrderState2D CaptureOrder() => new(_colliders.Select(c => c.Id).ToImmutableArray(), _nextColliderId, CellSize);

    public void RestoreOrder(CollisionOrderState2D state)
    {
        ArgGuard.ThrowIfNull(state);
        ArgGuard.ThrowIfNotPositive(state.CellSize);
        StateGuard.ThrowIf(state.ColliderIds.IsDefault || state.NextColliderId <= 0 ||
            state.ColliderIds.Any(id => id <= 0 || id >= state.NextColliderId), "Invalid collider allocation state.");
        var byId = _colliders.ToDictionary(c => c.Id);
        StateGuard.ThrowIf(state.ColliderIds.Length != byId.Count ||
            state.ColliderIds.Distinct().Count() != byId.Count ||
            state.ColliderIds.Any(id => !byId.ContainsKey(id)), "Collider membership differs from the checkpoint.");
        _colliders.Clear();
        foreach (var id in state.ColliderIds)
        {
            var collider = byId[id];
            collider.QueryStamp = 0;
            _colliders.Add(collider);
        }
        _nextColliderId = state.NextColliderId;
        _queryStamp = 0;
        CellSize = state.CellSize;
        _staticIndexDirty = _dynamicIndexDirty = true;
        // Scratch caches are rebuilt from the restored ordering and poses.
        _staticCells.Clear();
        _dynamicCells.Clear();
        _staticOverflow.Clear();
        _dynamicOverflow.Clear();
        _candidatePairs.Clear();
        _queryCandidates.Clear();
    }
}
