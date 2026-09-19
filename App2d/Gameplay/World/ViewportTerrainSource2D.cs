using App2d.Core;
using App2d.Core.Geometry;
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
    private (TileChunk2D Minimum, TileChunk2D Maximum)? _range;

    public ViewportTerrainSource2D(IChunkedTileMap2D map)
    {
        _map = ArgGuard.RequireNotNull(map);
        if (map is EditableTileMap2D editable) editable.ChunkChanged += Invalidate;
    }

    public ImmutableArray<TerrainChunkState2D> Capture(Bounds2D visibleBounds)
    {
        if (!visibleBounds.IsFinite || visibleBounds.Size.X < 0f || visibleBounds.Size.Y < 0f)
            throw new ArgumentOutOfRangeException(nameof(visibleBounds));

        // Include a tile beyond each edge for terrain artwork that overhangs its cell.
        var padding = new Vector2(_map.TileSize);
        var bounds = new Bounds2D(visibleBounds.Min - padding, visibleBounds.Max + padding);
        if (!bounds.Intersects(_map.WorldBounds))
        {
            _chunks.Clear();
            _range = null;
            return _terrain = [];
        }

        var minimum = _map.WorldToChunk(Vector2.Max(bounds.Min, _map.WorldBounds.Min));
        var maximum = _map.WorldToChunk(Vector2.Min(bounds.Max, _map.WorldBounds.Max));
        var maxX = Math.Min(maximum.X, _map.ChunkColumns - 1);
        var maxY = Math.Min(maximum.Y, _map.ChunkRows - 1);
        var range = (minimum, new TileChunk2D(maxX, maxY));
        if (_range == range && !_terrain.IsDefault) return _terrain;
        _range = range;

        foreach (var chunk in _chunks.Keys.ToArray())
        {
            if (chunk.X < minimum.X || chunk.X > maxX || chunk.Y < minimum.Y || chunk.Y > maxY)
            {
                _chunks.Remove(chunk);
                _terrain = default;
            }
        }
        for (var y = minimum.Y; y <= maxY; y++)
        {
            for (var x = minimum.X; x <= maxX; x++)
            {
                var chunk = new TileChunk2D(x, y);
                if (_chunks.ContainsKey(chunk)) continue;
                _chunks.Add(chunk, TerrainChunkState2D.Capture(_map, chunk, ++_revision));
                _terrain = default;
            }
        }

        if (_terrain.IsDefault)
            _terrain = _chunks.Values.OrderBy(c => c.Chunk.Y).ThenBy(c => c.Chunk.X).ToImmutableArray();
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
        _range = null;
    }
}
