using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Tiles;

/// <summary>A sparse world view over independently editable, non-overlapping maps.</summary>
public sealed class WorldTileMap2D : IChangingTileMap2D, IDisposable
{
    private readonly Entry[] _entries;
    private readonly string[] _tilesets;
    private sealed record Entry(EditableTileMap2D Map, int X, int Y, byte[] Catalog, Action<TileChunk2D> Changed);

    public WorldTileMap2D(IEnumerable<EditableTileMap2D> maps)
    {
        var sources = maps.ToArray();
        if (sources.Length == 0) throw new ArgumentException("A world needs at least one map.", nameof(maps));
        TileSize = sources[0].TileSize;
        ChunkSize = sources[0].ChunkSize;
        Origin = sources.Select(m => m.Origin).Aggregate(Vector2.Min);
        var max = sources.Select(m => m.WorldBounds.Max).Aggregate(Vector2.Max);
        _tilesets = sources.SelectMany(m => m.TilesetIds).Distinct(StringComparer.Ordinal).ToArray();
        if (_tilesets.Length > TileCell2D.MaximumTilesetCount)
            throw new ArgumentException("The world supports at most 16 distinct tilesets.", nameof(maps));
        _entries = new Entry[sources.Length];
        for (var i = 0; i < sources.Length; i++)
        {
            var map = sources[i];
            var offset = (map.Origin - Origin) / TileSize;
            if (map.TileSize != TileSize || map.ChunkSize != ChunkSize ||
                !float.IsFinite(offset.X) || !float.IsFinite(offset.Y) ||
                offset.X != MathF.Floor(offset.X) || offset.Y != MathF.Floor(offset.Y))
                throw new ArgumentException("World maps must share tile/chunk sizes and align to the same tile grid.", nameof(maps));
            for (var j = 0; j < i; j++)
                if (map.WorldBounds.Min.X < sources[j].WorldBounds.Max.X && map.WorldBounds.Max.X > sources[j].WorldBounds.Min.X &&
                    map.WorldBounds.Min.Y < sources[j].WorldBounds.Max.Y && map.WorldBounds.Max.Y > sources[j].WorldBounds.Min.Y)
                    throw new ArgumentException("World maps cannot overlap.", nameof(maps));
            var x = checked((int)offset.X);
            var y = checked((int)offset.Y);
            _entries[i] = new(map, x, y,
                map.TilesetIds.Select(id => checked((byte)Array.IndexOf(_tilesets, id))).ToArray(),
                chunk => Invalidate(map, x, y, chunk));
        }
        Width = checked((int)((max.X - Origin.X) / TileSize));
        Height = checked((int)((max.Y - Origin.Y) / TileSize));
        foreach (var entry in _entries) entry.Map.ChunkChanged += entry.Changed;
    }

    public int Width { get; }
    public int Height { get; }
    public int ChunkSize { get; }
    public int ChunkColumns => (Width - 1) / ChunkSize + 1;
    public int ChunkRows => (Height - 1) / ChunkSize + 1;
    public float TileSize { get; }
    public Vector2 Origin { get; }
    public Rect2D WorldBounds => new(Origin, Origin + new Vector2(Width, Height) * TileSize);
    public IReadOnlyList<string> TilesetIds => _tilesets;
    public event Action<TileChunk2D>? ChunkChanged;

    private Entry? Find(int x, int y)
    {
        foreach (var entry in _entries)
            if (x >= entry.X && y >= entry.Y && x - entry.X < entry.Map.Width && y - entry.Y < entry.Map.Height)
                return entry;
        return null;
    }
    public TileKind2D GetTileKind(int x, int y) => Find(x, y) is { } e ? e.Map.GetTileKind(x - e.X, y - e.Y) : TileKind2D.Empty;
    public byte GetTilesetIndex(int x, int y) => Find(x, y) is { } e ? e.Catalog[e.Map.GetTilesetIndex(x - e.X, y - e.Y)] : (byte)0;
    public bool IsSolid(int x, int y) => GetTileKind(x, y).IsSolid();
    public TileChunk2D WorldToChunk(Vector2 position) => new(
        Math.Clamp((int)MathF.Floor((position.X - Origin.X) / (TileSize * ChunkSize)), 0, ChunkColumns - 1),
        Math.Clamp((int)MathF.Floor((position.Y - Origin.Y) / (TileSize * ChunkSize)), 0, ChunkRows - 1));

    public IReadOnlyList<TileCollisionRectangle2D> BuildCollisionRectangles(TileChunk2D chunk)
    {
        if (chunk.X < 0 || chunk.Y < 0 || chunk.X >= ChunkColumns || chunk.Y >= ChunkRows)
            throw new ArgumentOutOfRangeException(nameof(chunk));
        var x = chunk.X * ChunkSize;
        var y = chunk.Y * ChunkSize;
        var cells = new List<TileCellRectangle2D>();
        TileRectangleMesher2D.Mesh(Math.Min(ChunkSize, Width - x), Math.Min(ChunkSize, Height - y),
            (cx, cy) => GetTileKind(x + cx, y + cy), cells);
        return cells.Select(c => new TileCollisionRectangle2D(new(
            Origin + new Vector2(x + c.X, y + c.Y) * TileSize,
            Origin + new Vector2(x + c.X + c.Width, y + c.Y + c.Height) * TileSize), c.Kind)).ToArray();
    }

    private void Invalidate(EditableTileMap2D map, int x, int y, TileChunk2D chunk)
    {
        // Include neighbouring world chunks: their snapshots sample a one-cell halo across map seams.
        var minX = Math.Max(0, x + chunk.X * ChunkSize - 1) / ChunkSize;
        var minY = Math.Max(0, y + chunk.Y * ChunkSize - 1) / ChunkSize;
        var maxX = Math.Min(Width - 1, x + Math.Min(map.Width, (chunk.X + 1) * ChunkSize)) / ChunkSize;
        var maxY = Math.Min(Height - 1, y + Math.Min(map.Height, (chunk.Y + 1) * ChunkSize)) / ChunkSize;
        for (var cy = minY; cy <= maxY; cy++)
            for (var cx = minX; cx <= maxX; cx++) ChunkChanged?.Invoke(new(cx, cy));
    }

    public void Dispose()
    {
        foreach (var entry in _entries) entry.Map.ChunkChanged -= entry.Changed;
    }
}
