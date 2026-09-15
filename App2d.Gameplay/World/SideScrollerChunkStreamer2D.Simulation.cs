using System.Collections.Immutable;

namespace App2d.Gameplay.World;

internal sealed partial class SideScrollerChunkStreamer2D
{
    internal sealed record ChunkState(TerrainChunkState2D Terrain, ImmutableArray<int> ColliderIds);
    internal sealed record SimulationState(long Revision, ImmutableArray<ChunkState> Chunks);
    internal ImmutableArray<int> ColliderIds => _loadedChunks.Values.SelectMany(c => c.Colliders)
        .Select(c => c.Body.Collider.Id).ToImmutableArray();

    internal SimulationState CaptureSimulation() => new(_revision,
        _loadedChunks.Values.OrderBy(c => c.State.Chunk.Y).ThenBy(c => c.State.Chunk.X).Select(c =>
            new ChunkState(c.State, c.Colliders.Select(b => b.Body.Collider.Id).ToImmutableArray())).ToImmutableArray());

    internal void RestoreSimulation(SimulationState state)
    {
        foreach (var chunk in _loadedChunks.Keys.ToArray()) Unload(chunk);
        _loadedChunks.Clear();
        foreach (var chunk in state.Chunks) Load(chunk.Terrain, chunk.ColliderIds);
        _revision = state.Revision;
        _unloadBuffer.Clear();
    }
}
