using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Engine.Tiles;

public sealed class ProceduralTileMap2D : IChunkedTileMap2D
{
    private readonly Func<int, int, TileKind2D> _getTileKind;

    public ProceduralTileMap2D(int width, int height, float tileSize, int chunkSize, Func<int, int, TileKind2D> getTileKind, Vector2 origin = default)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNotPositive(chunkSize);
        ArgGuard.ThrowIfNotPositive(tileSize);

        Width = width;
        Height = height;
        TileSize = tileSize;
        ChunkSize = chunkSize;
        Origin = origin;
        _getTileKind = ArgGuard.RequireNotNull(getTileKind);
    }

    public int Width { get; }
    public int Height { get; }
    public float TileSize { get; }
    public int ChunkSize { get; }
    public Vector2 Origin { get; }
    public int ChunkColumns => DivideRoundUp(Width, ChunkSize);
    public int ChunkRows => DivideRoundUp(Height, ChunkSize);
    public Bounds2D WorldBounds => new(Origin, Origin + new Vector2(Width * TileSize, Height * TileSize));

    public TileKind2D GetTileKind(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height
        ? _getTileKind(x, y)
        : TileKind2D.Empty;

    public bool IsSolid(int x, int y) => GetTileKind(x, y).IsSolid();

    public TileChunk2D WorldToChunk(Vector2 worldPosition)
    {
        var tile = (worldPosition - Origin) / TileSize;
        return new TileChunk2D(Math.Clamp((int)MathF.Floor(tile.X / ChunkSize), 0, ChunkColumns - 1), Math.Clamp((int)MathF.Floor(tile.Y / ChunkSize), 0, ChunkRows - 1));
    }

    public IReadOnlyList<TileCollisionRectangle2D> BuildCollisionRectangles(TileChunk2D chunk)
    {
        ValidateChunk(chunk);

        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = Math.Min(ChunkSize, Width - startX);
        var height = Math.Min(ChunkSize, Height - startY);
        // Prefetch so the mesher's repeated reads never re-run generator code.
        var tiles = new TileKind2D[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                tiles[y * width + x] = _getTileKind(startX + x, startY + y);
            }
        }

        var cells = new List<TileCellRectangle2D>();
        TileRectangleMesher2D.Mesh(width, height, (x, y) => tiles[y * width + x], cells);

        var rectangles = new List<TileCollisionRectangle2D>(cells.Count);
        foreach (var cell in cells)
        {
            var min = Origin + new Vector2(startX + cell.X, startY + cell.Y) * TileSize;
            var max = min + new Vector2(cell.Width, cell.Height) * TileSize;
            rectangles.Add(new TileCollisionRectangle2D(new Bounds2D(min, max), cell.Kind));
        }

        return rectangles;
    }

    private void ValidateChunk(TileChunk2D chunk)
    {
        if (chunk.X < 0 || chunk.X >= ChunkColumns || chunk.Y < 0 || chunk.Y >= ChunkRows)
        {
            ArgGuard.ThrowOutOfRange(chunk, "Chunk coordinates must be inside the map.");
        }
    }

    private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;
}

public readonly record struct TileChunk2D(int X, int Y);
