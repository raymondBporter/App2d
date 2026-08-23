using System.Numerics;
using App2d.Engine.Geometry;

namespace App2d.Engine.Tiles;

public sealed class TileMap2D
{
    private readonly bool[] _solidTiles;
    private readonly List<Bounds2D> _collisionRectangles = [];
    private bool _collisionRectanglesDirty = true;

    public TileMap2D(int width, int height, float tileSize, Vector2 origin = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (!float.IsFinite(tileSize) || tileSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(tileSize));

        Width = width;
        Height = height;
        TileSize = tileSize;
        Origin = origin;
        _solidTiles = new bool[width * height];
    }

    public int Width { get; }
    public int Height { get; }
    public float TileSize { get; }
    public Vector2 Origin { get; }
    public Bounds2D WorldBounds => new(
        Origin,
        Origin + new Vector2(Width * TileSize, Height * TileSize));

    public IReadOnlyList<Bounds2D> CollisionRectangles
    {
        get
        {
            if (_collisionRectanglesDirty)
                RebuildCollisionRectangles();
            return _collisionRectangles;
        }
    }

    public bool IsSolid(int x, int y) =>
        IsInside(x, y) && _solidTiles[y * Width + x];

    public void SetSolid(int x, int y, bool isSolid = true)
    {
        ValidateCoordinates(x, y);
        _solidTiles[y * Width + x] = isSolid;
        _collisionRectanglesDirty = true;
    }

    public void Fill(int x, int y, int width, int height, bool isSolid = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (!IsInside(x, y) || !IsInside(x + width - 1, y + height - 1))
            throw new ArgumentOutOfRangeException(nameof(width), "Fill rectangle must stay inside the tilemap.");

        for (var row = y; row < y + height; row++)
        {
            var start = row * Width + x;
            _solidTiles.AsSpan(start, width).Fill(isSolid);
        }

        _collisionRectanglesDirty = true;
    }

    private void RebuildCollisionRectangles()
    {
        _collisionRectangles.Clear();
        var consumed = new bool[_solidTiles.Length];

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var index = y * Width + x;
                if (!_solidTiles[index] || consumed[index])
                    continue;

                var rectangleWidth = 1;
                while (x + rectangleWidth < Width)
                {
                    var next = index + rectangleWidth;
                    if (!_solidTiles[next] || consumed[next])
                        break;
                    rectangleWidth++;
                }

                var rectangleHeight = 1;
                while (y + rectangleHeight < Height && IsUnconsumedSolidRun(x, y + rectangleHeight, rectangleWidth, consumed))
                    rectangleHeight++;

                for (var row = y; row < y + rectangleHeight; row++)
                    consumed.AsSpan(row * Width + x, rectangleWidth).Fill(true);

                var min = Origin + new Vector2(x, y) * TileSize;
                var max = min + new Vector2(rectangleWidth, rectangleHeight) * TileSize;
                _collisionRectangles.Add(new Bounds2D(min, max));
            }
        }

        _collisionRectanglesDirty = false;
    }

    private bool IsUnconsumedSolidRun(int x, int y, int width, ReadOnlySpan<bool> consumed)
    {
        var start = y * Width + x;
        for (var offset = 0; offset < width; offset++)
        {
            if (!_solidTiles[start + offset] || consumed[start + offset])
                return false;
        }
        return true;
    }

    private bool IsInside(int x, int y) =>
        x >= 0 && x < Width && y >= 0 && y < Height;

    private void ValidateCoordinates(int x, int y)
    {
        if (!IsInside(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x}, {y}) is outside the map.");
    }
}
