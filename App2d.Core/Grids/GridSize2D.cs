using App2d.Core.Validation;
namespace App2d.Core.Grids;

/// <summary>Dimensions and row-major addressing for a finite grid. Default is empty; the cell count fits an int.</summary>
public readonly record struct GridSize2D
{
    public GridSize2D(int width, int height)
    {
        ArgGuard.NonNegative(width);
        ArgGuard.NonNegative(height);
        var count = (long)width * height;
        ArgGuard.AtMost(count, int.MaxValue);
        Width = width;
        Height = height;
        CellCount = (int)count;
    }

    public int Width { get; }
    public int Height { get; }
    public int CellCount { get; }
    public bool IsEmpty => CellCount == 0;
    public GridCellRange2D Cells => IsEmpty ? default : new(default, new(Width - 1, Height - 1));

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;
    public bool Contains(GridCell2D cell) => Contains(cell.X, cell.Y);

    public int GetIndex(int x, int y)
    {
        ArgGuard.Index(x, Width);
        ArgGuard.Index(y, Height);
        return x + y * Width;
    }

    public int GetIndex(GridCell2D cell) => GetIndex(cell.X, cell.Y);

    public bool TryGetIndex(GridCell2D cell, out int index)
    {
        index = Contains(cell) ? cell.X + cell.Y * Width : -1;
        return index >= 0;
    }

    public GridCell2D GetCell(int index)
    {
        ArgGuard.Index(index, CellCount);
        return new(index % Width, index / Width);
    }

    /// <summary>Number of blocks needed to cover this grid, including partial edge blocks.</summary>
    public GridSize2D DivideRoundUp(int blockWidth, int blockHeight)
    {
        ArgGuard.Positive(blockWidth);
        ArgGuard.Positive(blockHeight);
        return new((int)((Width + (long)blockWidth - 1) / blockWidth),
            (int)((Height + (long)blockHeight - 1) / blockHeight));
    }
}
