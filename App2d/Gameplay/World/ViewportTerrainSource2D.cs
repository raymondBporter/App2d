using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Grids;
using App2d.Core.Tiles;
using App2d.Core.Validation;

namespace App2d.Gameplay.World;

/// <summary>
/// Camera-independent chunk observations and a grid query for the local map.
/// </summary>
internal sealed class ViewportTerrainSource2D : IDisposable
{
    private readonly Dictionary<TileChunk2D, TerrainChunkState2D> _chunks = [];
    private long _revision;

    public IChunkedTileMap2D Map { get; }
    public event Action<TileChunk2D>? ChunkChanged;

    public ViewportTerrainSource2D(IChunkedTileMap2D map)
    {
        Map = Ensure.RequireNotNull(map);
        if (map is IChangingTileMap2D editable)
        {
            editable.ChunkChanged += Invalidate;
        }
    }

    public GridCellRange2D QueryChunks(Rect2f bounds)
    {
        Ensure.ThrowIf(!bounds.IsFinite, "Viewport bounds must be finite.", nameof(bounds));
        return bounds.TryIntersect(Map.WorldBounds, out var clipped)
            ? Map.ChunkGridGeometry.GetCellRange(clipped, Map.ChunkGridSize)
            : default;
    }

    public GridCellRange2D QueryTerrain(Rect2f visible)
    {
        // The tileset can draw artwork one tile past the owning cell.
        var padding = new Vector2f(Map.TileSize).ToVector2f();
        return QueryChunks(new(visible.Min - padding, visible.Max + padding));
    }

    public TerrainChunkState2D GetChunk(TileChunk2D chunk)
    {
        if (!_chunks.TryGetValue(chunk, out var snapshot))
        {
            snapshot = TerrainChunkState2D.Capture(Map, chunk, ++_revision);
            _chunks.Add(chunk, snapshot);
        }
        return snapshot;
    }

    private void Invalidate(TileChunk2D chunk)
    {
        _chunks.Remove(chunk);
        ChunkChanged?.Invoke(chunk);
    }

    public void Dispose()
    {
        if (Map is IChangingTileMap2D editable)
        {
            editable.ChunkChanged -= Invalidate;
        }

        _chunks.Clear();
    }
}
