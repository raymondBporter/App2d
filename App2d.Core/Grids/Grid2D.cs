using App2d.Core.Validation;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Grids;

/// <summary>Dense row-major storage with independent, immutable grid geometry. Cells have no per-cell object overhead.</summary>
public sealed class Grid2D<T>
{
    private readonly T[] _values;

    public Grid2D(int width, int height, float cellSize = 1f, Vector2 origin = default)
        : this(new(width, height), new GridGeometry2D(cellSize, origin)) { }

    public Grid2D(int width, int height, Vector2 cellSize, Vector2 origin = default)
        : this(new(width, height), new GridGeometry2D(cellSize, origin)) { }

    public Grid2D(int width, int height, Size2D cellSize, Vector2 origin = default)
        : this(new(width, height), new GridGeometry2D(cellSize, origin)) { }

    public Grid2D(GridSize2D size, GridGeometry2D geometry)
    {
        ArgGuard.ThrowIf(!geometry.IsValid, "Grid geometry must be initialized.", nameof(geometry));
        Size = size;
        Geometry = geometry;
        WorldBounds = geometry.GetBounds(size);
        _values = new T[size.CellCount];
    }

    public GridSize2D Size { get; }
    public GridGeometry2D Geometry { get; }
    public int Width => Size.Width;
    public int Height => Size.Height;
    public int Count => Size.CellCount;
    public Size2D CellSize => Geometry.CellSize;
    public Vector2 Origin => Geometry.Origin;
    public Rect2D WorldBounds { get; }

    public ref T this[int x, int y] => ref _values[Size.GetIndex(x, y)];
    public ref T this[GridCell2D cell] => ref _values[Size.GetIndex(cell)];
    public ref T this[int index] => ref _values[index];

    public Span<T> AsSpan() => _values;
    public Span<T> GetRowSpan(int y)
    {
        ArgGuard.ThrowIfNotInClosedRange(y, 0, Height - 1);
        return _values.AsSpan(y * Width, Width);
    }

    public void Fill(T value) => _values.AsSpan().Fill(value);
    public void Clear() => _values.AsSpan().Clear();
    public Rect2D GetCellBounds(GridCell2D cell)
    {
        if (!Size.Contains(cell)) ArgGuard.ThrowOutOfRange(cell, "Cell must be inside the grid.");
        return Geometry.GetCellBounds(cell);
    }
    public GridCellRange2D GetCellRange<TRect>(TRect bounds) where TRect : IRect2D => Geometry.GetCellRange(bounds, Size);

    public bool TryGetCell(Vector2 position, out GridCell2D cell) => Geometry.TryWorldToCell(position, out cell) && Size.Contains(cell);
}
