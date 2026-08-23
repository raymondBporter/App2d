using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Engine.Tiles;

public sealed class ProceduralTileMap2D
{
    private readonly Func<int, int, bool> _isSolid;

    public ProceduralTileMap2D(
        int width,
        int height,
        float tileSize,
        int chunkSize,
        Func<int, int, bool> isSolid,
        Vector2 origin = default)
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
        _isSolid = ArgGuard.RequireNotNull(isSolid);
    }

    public int Width { get; }
    public int Height { get; }
    public float TileSize { get; }
    public int ChunkSize { get; }
    public Vector2 Origin { get; }
    public int ChunkColumns => DivideRoundUp(Width, ChunkSize);
    public int ChunkRows => DivideRoundUp(Height, ChunkSize);
    public Bounds2D WorldBounds => new(
        Origin,
        Origin + new Vector2(Width * TileSize, Height * TileSize));

    public bool IsSolid(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height && _isSolid(x, y);

    public TileChunk2D WorldToChunk(Vector2 worldPosition)
    {
        var tile = (worldPosition - Origin) / TileSize;
        return new TileChunk2D(
            Math.Clamp((int)MathF.Floor(tile.X / ChunkSize), 0, ChunkColumns - 1),
            Math.Clamp((int)MathF.Floor(tile.Y / ChunkSize), 0, ChunkRows - 1));
    }

    public IReadOnlyList<Bounds2D> BuildCollisionRectangles(TileChunk2D chunk)
    {
        ValidateChunk(chunk);

        var startX = chunk.X * ChunkSize;
        var startY = chunk.Y * ChunkSize;
        var width = Math.Min(ChunkSize, Width - startX);
        var height = Math.Min(ChunkSize, Height - startY);
        var solid = new bool[width * height];
        var consumed = new bool[solid.Length];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
                solid[y * width + x] = _isSolid(startX + x, startY + y);
        }

        var rectangles = new List<Bounds2D>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if (!solid[index] || consumed[index])
                    continue;

                var rectangleWidth = 1;
                while (x + rectangleWidth < width &&
                       solid[index + rectangleWidth] &&
                       !consumed[index + rectangleWidth])
                {
                    rectangleWidth++;
                }

                var rectangleHeight = 1;
                while (y + rectangleHeight < height &&
                       IsSolidRun(solid, consumed, width, x, y + rectangleHeight, rectangleWidth))
                {
                    rectangleHeight++;
                }

                for (var row = y; row < y + rectangleHeight; row++)
                    consumed.AsSpan(row * width + x, rectangleWidth).Fill(true);

                var min = Origin + new Vector2(startX + x, startY + y) * TileSize;
                var max = min + new Vector2(rectangleWidth, rectangleHeight) * TileSize;
                rectangles.Add(new Bounds2D(min, max));
            }
        }

        return rectangles;
    }

    public int WorldToTileY(float worldY) =>
        (int)MathF.Floor((worldY - Origin.Y) / TileSize);

    private static bool IsSolidRun(
        ReadOnlySpan<bool> solid,
        ReadOnlySpan<bool> consumed,
        int rowWidth,
        int x,
        int y,
        int width)
    {
        var start = y * rowWidth + x;
        for (var offset = 0; offset < width; offset++)
        {
            if (!solid[start + offset] || consumed[start + offset])
                return false;
        }

        return true;
    }

    private void ValidateChunk(TileChunk2D chunk)
    {
        if (chunk.X < 0 || chunk.X >= ChunkColumns ||
            chunk.Y < 0 || chunk.Y >= ChunkRows)
        {
            ArgGuard.ThrowOutOfRange(chunk, "Chunk coordinates must be inside the map.");
        }
    }

    private static int DivideRoundUp(int value, int divisor) =>
        (value + divisor - 1) / divisor;
}

public readonly record struct TileChunk2D(int X, int Y);
