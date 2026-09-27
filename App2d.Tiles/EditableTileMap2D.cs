using App2d.Core;
using App2d.Core.Geometry;
using App2d.Core.Grids;
using System.Numerics;

namespace App2d.Tiles;

/// <summary>
/// The dense, mutable tile map that authored levels load into. It is the only
/// <see cref="IChunkedTileMap2D"/> implementation: terrain is data, not a generator.
/// </summary>
public sealed class EditableTileMap2D : IChunkedTileMap2D
{
    private readonly Grid2D<TileCell2D> _tiles;
    private readonly string[] _tilesetIds;
    private readonly List<TileCellRectangle2D> _meshBuffer = [];

    public EditableTileMap2D(
        int width,
        int height,
        float tileSize,
        int chunkSize,
        Vector2 origin = default,
        IReadOnlyList<string>? tilesetIds = null)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(tileSize);
        ArgGuard.ThrowIfNotPositive(chunkSize);
        ArgGuard.ThrowIfNotFinite(origin);

        ChunkSize = chunkSize;
        _tiles = new(width, height, tileSize, origin);
        ChunkGridSize = GridSize.DivideRoundUp(chunkSize, chunkSize);
        ChunkGridGeometry = new(tileSize * chunkSize, origin);
        _tilesetIds = ValidateTilesets(tilesetIds ?? ["default"]);
    }

    /// <summary>Raised when a chunk's tiles changed. Phase 2's editor drives streamer reloads from this.</summary>
    public event Action<TileChunk2D>? ChunkChanged;

    public int Width => _tiles.Width;
    public int Height => _tiles.Height;
    public float TileSize => _tiles.CellSize.X;
    public int ChunkSize { get; }
    public Vector2 Origin => _tiles.Origin;
    public int ChunkColumns => ChunkGridSize.Width;
    public int ChunkRows => ChunkGridSize.Height;
    public Bounds2D WorldBounds => _tiles.WorldBounds;
    public GridSize2D GridSize => _tiles.Size;
    public GridGeometry2D GridGeometry => _tiles.Geometry;
    public GridSize2D ChunkGridSize { get; }
    public GridGeometry2D ChunkGridGeometry { get; }
    public IReadOnlyList<string> TilesetIds => _tilesetIds;

    public TileKind2D GetTileKind(int x, int y) => GridSize.Contains(x, y)
        ? _tiles[x, y].Kind
        : TileKind2D.Empty;

    public byte GetTilesetIndex(int x, int y) => GridSize.Contains(x, y)
        ? _tiles[x, y].TilesetIndex
        : (byte)0;

    public TileCell2D GetTile(int x, int y) => GridSize.Contains(x, y)
        ? _tiles[x, y]
        : default;

    public bool IsSolid(int x, int y) => GetTileKind(x, y).IsSolid();

    public void SetTileKind(int x, int y, TileKind2D kind)
    {
        var tilesetIndex = GetTilesetIndex(x, y);
        SetTile(x, y, new TileCell2D(kind, tilesetIndex));
    }

    public void SetTile(int x, int y, TileCell2D tile)
    {
        var index = GridSize.GetIndex(x, y);
        if (tile.TilesetIndex >= _tilesetIds.Length)
            ArgGuard.ThrowOutOfRange(tile.TilesetIndex, "Tileset index must exist in the map catalog.");

        if (_tiles[index] == tile)
            return;

        _tiles[index] = tile;

        if (ChunkChanged is null)
            return;

        // Terrain visuals sample the tile's 3x3 neighbourhood across chunk borders, so a
        // tile on a chunk edge or corner can change up to four chunks' appearance. Chunk
        // assignment is monotonic in x and y independently, so the distinct chunks touched
        // by the (clamped) 3x3 neighbourhood are exactly the outer product of the chunk
        // columns/rows at its min and max corners -- at most four, deduplicated below.
        var minChunkX = TileToChunk(Math.Max(0, x - 1), 0).X;
        var maxChunkX = TileToChunk(Math.Min(Width - 1, x + 1), 0).X;
        var minChunkY = TileToChunk(0, Math.Max(0, y - 1)).Y;
        var maxChunkY = TileToChunk(0, Math.Min(Height - 1, y + 1)).Y;

        // DistinctPair already collapses each axis to its unique values, so the cross
        // product below can never repeat a (chunkX, chunkY) pair.
        foreach (var chunkX in DistinctPair(minChunkX, maxChunkX))
        {
            foreach (var chunkY in DistinctPair(minChunkY, maxChunkY))
                ChunkChanged.Invoke(new TileChunk2D(chunkX, chunkY));
        }
    }

    private static int[] DistinctPair(int first, int second) =>
        first == second ? [first] : [first, second];

    /// <summary>Seeds every tile from <paramref name="source"/> without raising per-tile change events.</summary>
    public void Fill(Func<int, int, TileKind2D> source)
    {
        ArgGuard.ThrowIfNull(source);
        for (var y = 0; y < Height; y++)
        {
            var row = _tiles.GetRowSpan(y);
            for (var x = 0; x < Width; x++)
                row[x] = new TileCell2D(source(x, y), 0);
        }
    }

    public TileChunk2D TileToChunk(int x, int y) => new(x / ChunkSize, y / ChunkSize);

    public int ChunkWidth(int chunkX) => Math.Min(ChunkSize, Width - chunkX * ChunkSize);

    public int ChunkHeight(int chunkY) => Math.Min(ChunkSize, Height - chunkY * ChunkSize);

    /// <summary>Copies a chunk's tiles into <paramref name="destination"/> in row-major order.</summary>
    public ReadOnlySpan<TileKind2D> GetChunkTiles(TileChunk2D chunk, Span<TileKind2D> destination)
    {
        ValidateChunk(chunk);
        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);
        ArgGuard.ThrowIfTooShort(destination, width * height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                destination[y * width + x] = _tiles[startX + x, startY + y].Kind;
        }

        return destination[..(width * height)];
    }

    /// <summary>Copies a chunk's packed one-byte cells in row-major order.</summary>
    public ReadOnlySpan<TileCell2D> GetChunkCells(TileChunk2D chunk, Span<TileCell2D> destination)
    {
        ValidateChunk(chunk);
        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);
        ArgGuard.ThrowIfTooShort(destination, width * height);

        for (var y = 0; y < height; y++)
        {
            _tiles.GetRowSpan(startY + y).Slice(startX, width)
                .CopyTo(destination[(y * width)..]);
        }

        return destination[..(width * height)];
    }

    /// <summary>Writes a chunk's tiles from <paramref name="source"/> in row-major order.</summary>
    public void SetChunkTiles(TileChunk2D chunk, ReadOnlySpan<TileKind2D> source)
    {
        ValidateChunk(chunk);
        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);
        ArgGuard.ThrowIfTooShort(source, width * height);

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var mapIndex = GridSize.GetIndex(startX + x, startY + y);
                _tiles[mapIndex] = new TileCell2D(source[y * width + x], _tiles[mapIndex].TilesetIndex);
            }
        }

        ChunkChanged?.Invoke(chunk);
    }

    /// <summary>Writes a chunk's packed one-byte cells.</summary>
    public void SetChunkCells(TileChunk2D chunk, ReadOnlySpan<TileCell2D> source)
    {
        ValidateChunk(chunk);
        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);
        ArgGuard.ThrowIfTooShort(source, width * height);

        for (var index = 0; index < width * height; index++)
        {
            if (source[index].TilesetIndex >= _tilesetIds.Length)
                ArgGuard.ThrowOutOfRange(source[index].TilesetIndex, "Tileset index must exist in the map catalog.");
        }

        for (var y = 0; y < height; y++)
        {
            source.Slice(y * width, width)
                .CopyTo(_tiles.GetRowSpan(startY + y).Slice(startX, width));
        }

        ChunkChanged?.Invoke(chunk);
    }

    public TileChunk2D WorldToChunk(Vector2 worldPosition)
    {
        var cell = ChunkGridGeometry.WorldToCellClamped(worldPosition, ChunkGridSize);
        return new(cell.X, cell.Y);
    }

    public IReadOnlyList<TileCollisionRectangle2D> BuildCollisionRectangles(TileChunk2D chunk)
    {
        ValidateChunk(chunk);

        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = ChunkWidth(chunk.X);
        var height = ChunkHeight(chunk.Y);

        _meshBuffer.Clear();
        TileRectangleMesher2D.Mesh(
            width,
            height,
            (x, y) => _tiles[startX + x, startY + y].Kind,
            _meshBuffer);

        var rectangles = new List<TileCollisionRectangle2D>(_meshBuffer.Count);
        foreach (var cell in _meshBuffer)
        {
            var cells = new GridCellRange2D(new(startX + cell.X, startY + cell.Y),
                new(startX + cell.X + cell.Width - 1, startY + cell.Y + cell.Height - 1));
            rectangles.Add(new TileCollisionRectangle2D(GridGeometry.GetBounds(cells), cell.Kind));
        }

        return rectangles;
    }

    private void ValidateChunk(TileChunk2D chunk)
    {
        if (!ChunkGridSize.Contains(chunk.X, chunk.Y))
            ArgGuard.ThrowOutOfRange(chunk, "Chunk coordinates must be inside the map.");
    }

    private static string[] ValidateTilesets(IReadOnlyList<string> tilesetIds)
    {
        if (tilesetIds.Count == 0 || tilesetIds.Count > TileCell2D.MaximumTilesetCount)
            ArgGuard.ThrowOutOfRange(tilesetIds.Count, "A map must have between 1 and 16 tilesets.");

        var result = new string[tilesetIds.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < tilesetIds.Count; index++)
        {
            var id = tilesetIds[index];
            ArgGuard.ThrowIfNullOrWhiteSpace(id);
            if (!seen.Add(id))
                ArgGuard.ThrowInvalid($"Tileset ID '{id}' is duplicated.", nameof(tilesetIds));
            result[index] = id;
        }

        return result;
    }
}

public readonly record struct TileChunk2D(int X, int Y);
