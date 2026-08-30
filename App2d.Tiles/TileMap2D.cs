using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Tiles;

public sealed class TileMap2D : ISolidTileMap2D
{
    private readonly bool[] _solidTiles;
    private readonly List<Bounds2D> _collisionRectangles = [];
    private readonly List<TileCellRectangle2D> _meshBuffer = [];
    private readonly TileRectangleMesher2D.KindAt _kindAt;
    private bool _collisionRectanglesDirty = true;

    public TileMap2D(int width, int height, float tileSize, Vector2 origin = default)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNotPositive(tileSize);
        ArgGuard.ThrowIfNotFinite(origin);

        Width = width;
        Height = height;
        TileSize = tileSize;
        Origin = origin;
        _solidTiles = new bool[width * height];
        _kindAt = (x, y) => _solidTiles[y * Width + x] ? TileKind2D.Solid : TileKind2D.Empty;
    }

    public int Width { get; }
    public int Height { get; }
    public float TileSize { get; }
    public Vector2 Origin { get; }
    public Bounds2D WorldBounds => new(Origin, Origin + new Vector2(Width * TileSize, Height * TileSize));

    public IReadOnlyList<Bounds2D> CollisionRectangles
    {
        get
        {
            if (_collisionRectanglesDirty)
                RebuildCollisionRectangles();
            return _collisionRectangles;
        }
    }

    public bool IsSolid(int x, int y) => IsInside(x, y) && _solidTiles[y * Width + x];

    public void SetSolid(int x, int y, bool isSolid = true)
    {
        ValidateCoordinates(x, y);
        _solidTiles[y * Width + x] = isSolid;
        _collisionRectanglesDirty = true;
    }

    public void Fill(int x, int y, int width, int height, bool isSolid = true)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);

        if (!IsInside(x, y) || !IsInside(x + width - 1, y + height - 1))
            ArgGuard.ThrowOutOfRange(width, "Fill rectangle must stay inside the tilemap.");

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
        _meshBuffer.Clear();
        TileRectangleMesher2D.Mesh(Width, Height, _kindAt, _meshBuffer);
        foreach (var cell in _meshBuffer)
        {
            var min = Origin + new Vector2(cell.X, cell.Y) * TileSize;
            _collisionRectangles.Add(new Bounds2D(min, min + new Vector2(cell.Width, cell.Height) * TileSize));
        }

        _collisionRectanglesDirty = false;
    }

    private bool IsInside(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    private void ValidateCoordinates(int x, int y)
    {
        if (!IsInside(x, y))
            ArgGuard.ThrowOutOfRange(x, $"Tile ({x}, {y}) is outside the map.");
    }
}
