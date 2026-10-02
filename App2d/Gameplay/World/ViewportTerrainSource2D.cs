using App2d.Contracts.World;
using App2d.Core.Geometry;
using App2d.Core.Grids;
using App2d.Core.Validation;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>
/// Local content delivery for the camera, independent of simulation streaming.
/// Only immutable chunk observations cross into presentation.
/// </summary>
internal sealed class ViewportTerrainSource2D : IDisposable
{
    private readonly IChunkedTileMap2D _map;
    private readonly Dictionary<TileChunk2D, TerrainChunkState2D> _chunks = [];
    private ImmutableArray<TerrainChunkState2D> _terrain;
    private long _revision;
    private GridCellRange2D _range;

    public ViewportTerrainSource2D(IChunkedTileMap2D map)
    {
        _map = ArgGuard.RequireNotNull(map);
        if (map is EditableTileMap2D editable) editable.ChunkChanged += Invalidate;
    }

    public ImmutableArray<TerrainChunkState2D> Capture(Rect2D visibleBounds)
    {
        if (!visibleBounds.IsFinite || visibleBounds.Size.X < 0f || visibleBounds.Size.Y < 0f)
            ArgGuard.ThrowOutOfRange(visibleBounds, "Viewport bounds must be finite and ordered.");

        // Include a tile beyond each edge for terrain artwork that overhangs its cell.
        var padding = new Vector2(_map.TileSize);
        var bounds = new Rect2D(visibleBounds.Min - padding, visibleBounds.Max + padding);
        if (!bounds.TryIntersect(_map.WorldBounds, out var clipped))
        {
            _chunks.Clear();
            _range = default;
            return _terrain = [];
        }

        var range = _map.ChunkGridGeometry.GetCellRange(clipped, _map.ChunkGridSize);
        if (_range == range && !_terrain.IsDefault) return _terrain;
        _range = range;

        foreach (var chunk in _chunks.Keys.ToArray())
        {
            if (!range.Contains(new(chunk.X, chunk.Y)))
            {
                _chunks.Remove(chunk);
                _terrain = default;
            }
        }
        foreach (var cell in range)
        {
            var chunk = new TileChunk2D(cell.X, cell.Y);
            if (_chunks.ContainsKey(chunk)) continue;
            _chunks.Add(chunk, TerrainChunkState2D.Capture(_map, chunk, ++_revision));
            _terrain = default;
        }

        if (_terrain.IsDefault)
            _terrain = [.. _chunks.Values.OrderBy(c => c.Chunk.Y).ThenBy(c => c.Chunk.X)];
        return _terrain;
    }

    private void Invalidate(TileChunk2D chunk)
    {
        // Removing now coalesces repeated paint events until the next capture.
        if (_chunks.Remove(chunk)) _terrain = default;
    }

    public void Dispose()
    {
        if (_map is EditableTileMap2D editable) editable.ChunkChanged -= Invalidate;
        _chunks.Clear();
        _terrain = default;
        _range = default;
    }
}
