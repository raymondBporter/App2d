using App2d.Core;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Simulation;
using App2d.Tiles;
using System.Collections.Immutable;

namespace App2d.Gameplay.World;

public sealed partial class SideScrollerLevel2D
{
    private readonly Guid _checkpointOwner = Guid.NewGuid();
    private long _definitionRevision;
    internal sealed record PlatformEntry(EntityId2D Id, MovingPlatform2D.SimulationState State);
    internal sealed record CheckpointEntry(long Id, SavePoint2D.SimulationState State);
    internal sealed record SimulationState(Guid Owner, long DefinitionRevision,
        SideScrollerChunkStreamer2D.SimulationState Streamer,
        ImmutableArray<PlatformEntry> Platforms, ImmutableArray<CheckpointEntry> Checkpoints,
        ImmutableArray<EnemySystem2D.EntryState> Enemies) : WorldSimulationState2D
    {
        public override ImmutableArray<int> TerrainColliderIds => Streamer.Chunks.SelectMany(c => c.ColliderIds).ToImmutableArray();
    }

    private void OnMapChanged(TileChunk2D chunk)
    {
        _definitionRevision++;
        _dirtyChunks.Mark(chunk);
    }

    public ImmutableArray<int> TerrainColliderIds => RequireEnvironment().Streamer.ColliderIds;

    public WorldSimulationState2D CaptureSimulation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(!_dirtyChunks.IsEmpty, "Flush editor changes before capturing simulation state.");
        return new SimulationState(_checkpointOwner, _definitionRevision, RequireEnvironment().Streamer.CaptureSimulation(),
            _movingPlatforms.Select(p => new PlatformEntry(p.Id, p.CaptureSimulation())).ToImmutableArray(),
            _savePoints.Select(p => new CheckpointEntry(p.Spec.ThingId, p.CaptureSimulation())).ToImmutableArray(),
            EnemySystem.CaptureSimulation());
    }

    public void ValidateSimulation(WorldSimulationState2D snapshot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(snapshot is not SimulationState state || state.Owner != _checkpointOwner ||
            state.DefinitionRevision != _definitionRevision, "The level definition changed or this checkpoint belongs to another level.");
        var saved = (SimulationState)snapshot;
        StateGuard.ThrowIf(!_movingPlatforms.Select(p => p.Id).SequenceEqual(saved.Platforms.Select(p => p.Id)) ||
            !_savePoints.Select(p => p.Spec.ThingId).SequenceEqual(saved.Checkpoints.Select(p => p.Id)),
            "World membership changed since capture.");
        EnemySystem.ValidateSimulation(saved.Enemies);
    }

    public void RestoreSimulation(WorldSimulationState2D snapshot)
    {
        ValidateSimulation(snapshot);
        var state = (SimulationState)snapshot;
        RequireEnvironment().Streamer.RestoreSimulation(state.Streamer);
        for (var i = 0; i < _movingPlatforms.Count; i++) _movingPlatforms[i].RestoreSimulation(state.Platforms[i].State);
        for (var i = 0; i < _savePoints.Count; i++) _savePoints[i].RestoreSimulation(state.Checkpoints[i].State);
        EnemySystem.RestoreSimulation(state.Enemies);
    }
}
