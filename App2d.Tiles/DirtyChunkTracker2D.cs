using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// Accumulates chunks whose tiles or appearance changed, so a burst of edits rebuilds
/// each affected chunk at most once.
/// </summary>
/// <remarks>
/// Painting raises <see cref="EditableTileMap2D.ChunkChanged"/> up to four times per tile
/// (terrain visuals sample across chunk borders) and continuously while dragging. Rebuilding
/// per event would destroy and recreate a chunk's physics bodies and visuals many times in
/// one frame, so edits are coalesced and flushed once per frame instead.
/// </remarks>
public sealed class DirtyChunkTracker2D
{
    private readonly HashSet<TileChunk2D> _dirty = [];
    private readonly List<TileChunk2D> _flushBuffer = [];

    public int Count => _dirty.Count;
    public bool IsEmpty => _dirty.Count == 0;

    public void Mark(TileChunk2D chunk) => _dirty.Add(chunk);

    public void Flush(Action<TileChunk2D> rebuild)
    {
        ArgGuard.ThrowIfNull(rebuild);
        if (_dirty.Count == 0)
            return;

        // Drain before rebuilding: a rebuild may mark further chunks, and those must
        // survive for the next flush rather than corrupt this iteration.
        _flushBuffer.Clear();
        _flushBuffer.AddRange(_dirty);
        _dirty.Clear();

        foreach (var chunk in _flushBuffer)
            rebuild(chunk);
    }
}
