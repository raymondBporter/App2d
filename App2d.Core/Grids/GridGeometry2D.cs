using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Grids;

/// <summary>
/// Uniform axis-aligned grid geometry, independent of storage and dimensions. Origin is the lower-left
/// corner of cell (0,0); Y increases upward. Cells own their minimum edge and exclude their maximum edge.
/// Default is uninitialized: Try methods return false, other geometry operations reject it.
/// </summary>
public readonly record struct GridGeometry2D
{
    public GridGeometry2D(float cellSize, Vector2 origin = default) : this(new Size2D(cellSize, cellSize), origin) { }

    public GridGeometry2D(Vector2 cellSize, Vector2 origin = default) : this(Size2D.FromVector2(cellSize), origin) { }

    public GridGeometry2D(Size2D cellSize, Vector2 origin = default)
    {
        ArgGuard.ThrowIf(!cellSize.IsValid, "Cell size must have positive finite dimensions.", nameof(cellSize));
        ArgGuard.ThrowIfNotFinite(origin);
        CellSize = cellSize;
        Origin = origin;
    }

    public Size2D CellSize { get; }
    public Vector2 Origin { get; }
    public bool IsValid => CellSize.IsValid && NumericValidation.IsFinite(Origin);

    public GridCell2D WorldToCell(Vector2 position)
    {
        StateGuard.ThrowIf(!IsValid, "Grid geometry must be initialized with a positive finite cell size.");
        if (!TryWorldToCell(position, out var cell))
            ArgGuard.ThrowOutOfRange(position, "Position must be finite and map to 32-bit cell coordinates.");
        return cell;
    }

    public bool TryWorldToCell(Vector2 position, out GridCell2D cell)
    {
        cell = default;
        if (!IsValid || !NumericValidation.IsFinite(position)) return false;
        // Double intermediates avoid overflowing a float subtraction or wrapping a float-to-int conversion.
        var x = Math.Floor(((double)position.X - Origin.X) / CellSize.Width);
        var y = Math.Floor(((double)position.Y - Origin.Y) / CellSize.Height);
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) return false;
        cell = new((int)x, (int)y);
        return true;
    }

    /// <summary>Clamps to a nonempty finite grid. Even distant finite world positions safely reach an edge cell.</summary>
    public GridCell2D WorldToCellClamped(Vector2 position, GridSize2D size)
    {
        StateGuard.ThrowIf(!IsValid, "Grid geometry must be initialized with a positive finite cell size.");
        ArgGuard.ThrowIfNotFinite(position);
        ArgGuard.ThrowIf(size.IsEmpty, "Cannot clamp to an empty grid.", nameof(size));
        return new(
            (int)Math.Clamp(Math.Floor(((double)position.X - Origin.X) / CellSize.Width), 0, size.Width - 1),
            (int)Math.Clamp(Math.Floor(((double)position.Y - Origin.Y) / CellSize.Height), 0, size.Height - 1));
    }

    public Rect2D GetCellBounds(GridCell2D cell) => new(
        Corner(cell.X, cell.Y), Corner((double)cell.X + 1, (double)cell.Y + 1));

    public Vector2 GetCellCenter(GridCell2D cell) => Corner((double)cell.X + .5, (double)cell.Y + .5);

    public Rect2D GetBounds(GridSize2D size) => new(Corner(0, 0), Corner(size.Width, size.Height));

    public Rect2D GetBounds(GridCellRange2D cells)
    {
        ArgGuard.ThrowIf(cells.IsEmpty, "An empty cell range has no world bounds.", nameof(cells));
        return new(Corner(cells.Minimum.X, cells.Minimum.Y),
            Corner((double)cells.Maximum.X + 1, (double)cells.Maximum.Y + 1));
    }

    /// <summary>
    /// Maps both corners with floor, including the cell owning the maximum endpoint. This conservative
    /// coverage preserves edge contacts in a broad phase. A zero-area rectangle maps to its point's cell.
    /// </summary>
    public GridCellRange2D GetCellRange<TRect>(TRect bounds) where TRect : IRect2D
    {
        // Value-type rectangles cannot be null; avoid boxing them in unoptimized builds.
        if (!typeof(TRect).IsValueType) ArgGuard.ThrowIfNull(bounds);
        StateGuard.ThrowIf(!IsValid, "Grid geometry must be initialized with a positive finite cell size.");
        if (!TryGetCellRange(bounds, out var cells))
            ArgGuard.ThrowOutOfRange(bounds, "Bounds must be finite, ordered, and fit the grid coordinate and cell-count limits.");
        return cells;
    }

    /// <summary>False means a sparse spatial index must use its overflow/fallback path, not skip the object.</summary>
    public bool TryGetCellRange<TRect>(TRect bounds, out GridCellRange2D cells) where TRect : IRect2D
    {
        cells = default;
        if ((!typeof(TRect).IsValueType && bounds is null) ||
            !NumericValidation.IsComponentWiseLessThanOrEqual(bounds.Min, bounds.Max) ||
            !TryWorldToCell(bounds.Min, out var min) || !TryWorldToCell(bounds.Max, out var max)) return false;
        var width = (long)max.X - min.X + 1;
        var height = (long)max.Y - min.Y + 1;
        if (width > long.MaxValue / height) return false;
        cells = new(min, max);
        return true;
    }

    /// <summary>Clips a finite ordered rectangle to a finite grid. Disjoint or empty grids return an empty range.</summary>
    public GridCellRange2D GetCellRange<TRect>(TRect bounds, GridSize2D size) where TRect : IRect2D
    {
        if (!typeof(TRect).IsValueType) ArgGuard.ThrowIfNull(bounds);
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(bounds.Max, bounds.Min, nameof(bounds), nameof(bounds));
        var world = GetBounds(size);
        if (size.IsEmpty || !world.TryIntersect(bounds, out var clipped)) return default;
        return new(WorldToCellClamped(clipped.Min, size), WorldToCellClamped(clipped.Max, size));
    }

    private Vector2 Corner(double x, double y)
    {
        StateGuard.ThrowIf(!IsValid, "Grid geometry must be initialized with a positive finite cell size.");
        var point = new Vector2((float)(Origin.X + x * CellSize.Width), (float)(Origin.Y + y * CellSize.Height));
        if (!NumericValidation.IsFinite(point))
            ArgGuard.ThrowOutOfRange(point, "Grid bounds must fit finite world coordinates.");
        return point;
    }
}
