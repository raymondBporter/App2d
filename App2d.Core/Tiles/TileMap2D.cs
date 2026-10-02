using App2d.Core.Validation;
using App2d.Core.Geometry;
using App2d.Core.Grids;
using System.Numerics;

namespace App2d.Tiles;

public sealed class TileMap2D : ISolidTileMap2D
{
    private readonly Grid2D<bool> _solidTiles;
    private readonly List<Rect2D> _collisionRectangles = [];
    private readonly List<TileCellRectangle2D> _meshBuffer = [];
    private readonly TileRectangleMesher2D.KindAt _kindAt;
    private bool _collisionRectanglesDirty = true;

    public TileMap2D(int width, int height, float tileSize, Vector2 origin = default)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(tileSize);
        ArgGuard.ThrowIfNotFinite(origin);

        _solidTiles = new(width, height, tileSize, origin);
        _kindAt = (x, y) => _solidTiles[x, y] ? TileKind2D.Solid : TileKind2D.Empty;
    }

    public int Width => _solidTiles.Width;
    public int Height => _solidTiles.Height;
    public float TileSize => _solidTiles.CellSize.Width;
    public Vector2 Origin => _solidTiles.Origin;
    public Rect2D WorldBounds => _solidTiles.WorldBounds;
    public GridSize2D GridSize => _solidTiles.Size;
    public GridGeometry2D GridGeometry => _solidTiles.Geometry;

    public IReadOnlyList<Rect2D> CollisionRectangles
    {
        get
        {
            if (_collisionRectanglesDirty)
                RebuildCollisionRectangles();
            return _collisionRectangles;
        }
    }

    public bool IsSolid(int x, int y) => GridSize.Contains(x, y) && _solidTiles[x, y];

    public void SetSolid(int x, int y, bool isSolid = true)
    {
        _solidTiles[x, y] = isSolid;
        _collisionRectanglesDirty = true;
    }

    public void Fill(int x, int y, int width, int height, bool isSolid = true)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);

        if (!GridSize.Contains(x, y) || width > Width - x || height > Height - y)
            ArgGuard.ThrowOutOfRange(width, "Fill rectangle must stay inside the tilemap.");

        for (var row = y; row < y + height; row++)
        {
            _solidTiles.GetRowSpan(row).Slice(x, width).Fill(isSolid);
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
            _collisionRectangles.Add(GridGeometry.GetBounds(new GridCellRange2D(
                new(cell.X, cell.Y), new(cell.X + cell.Width - 1, cell.Y + cell.Height - 1))));
        }

        _collisionRectanglesDirty = false;
    }

}
