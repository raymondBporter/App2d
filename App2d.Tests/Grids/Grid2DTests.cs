using App2d.Core.Geometry.Functions;
using App2d.Core.Geometry;
using App2d.Core.Grids;
using System.Numerics;

namespace App2d.Tests.Grids;

public sealed class Grid2DTests
{
    [Fact]
    public void CoordinatesFlatIndicesAndRowsAddressTheSameStorage()
    {
        var grid = new Grid2D<Vector2>(5, 3, new Vector2(10, 20), new(-30, -40));
        foreach (var cell in grid.Size.Cells)
        {
            var index = grid.Size.GetIndex(cell);
            Assert.Equal(cell, grid.Size.GetCell(index));
            grid[cell] = new(index, -index);
        }
        Assert.Equal(15, grid.Count);
        Assert.Equal(new Vector2(13, -13), grid[3, 2]);
        grid[3, 2].X = 100; // Ref access can update a struct cell without a copy/write-back.
        Assert.Equal(100, grid[13].X);
        grid.GetRowSpan(1).Fill(Vector2.One);
        Assert.All(grid.AsSpan().Slice(5, 5).ToArray(), v => Assert.Equal(Vector2.One, v));
        Assert.Equal(new Bounds2D(new(-30, -40), new(20, 20)), grid.WorldBounds);
        Assert.Equal(new Bounds2D(new(0, 0), new(10, 20)), grid.GetCellBounds(new(3, 2)));
        Assert.Equal(new Vector2(5, 10), grid.Geometry.GetCellCenter(new(3, 2)));
        grid.Fill(new(7, 8));
        Assert.All(grid.AsSpan().ToArray(), v => Assert.Equal(new Vector2(7, 8), v));
        grid.Clear();
        Assert.All(grid.AsSpan().ToArray(), v => Assert.Equal(Vector2.Zero, v));
    }

    [Fact]
    public void InvalidCoordinatesCannotAliasTheNextOrPreviousRow()
    {
        var grid = new Grid2D<int>(3, 2);
        grid[0, 1] = 42;
        Assert.Throws<ArgumentOutOfRangeException>(() => grid[3, 0] = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid[-1, 1] = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid[0, 2] = 99);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Size.GetCell(6));
        Assert.False(grid.Size.TryGetIndex(new(3, 0), out var index));
        Assert.Equal(-1, index);
        Assert.Equal(42, grid[0, 1]);
    }

    [Fact]
    public void EmptyAndOversizedDimensionsDoNotWrapArrayLengths()
    {
        var grid = new Grid2D<float>(0, 3);
        Assert.Equal(0, grid.Count);
        Assert.True(grid.Size.Cells.IsEmpty);
        Assert.True(grid.GetRowSpan(2).IsEmpty);
        Assert.False(grid.TryGetCell(Vector2.Zero, out _));
        Assert.True(grid.GetCellRange(new Rect2D(Vector2.Zero, Vector2.One)).IsEmpty);
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridSize2D(65536, 65536));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridSize2D(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid[0, 0] = 1);
        Assert.Equal(new GridSize2D(3, 2), new GridSize2D(10, 6).DivideRoundUp(4, 4));
        Assert.Equal(new GridSize2D(1, 1), new GridSize2D(int.MaxValue, 1).DivideRoundUp(int.MaxValue, 1));
    }

    [Theory]
    [InlineData(-.01f, -.01f, -1, -1)]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(9.99f, 19.99f, 0, 0)]
    [InlineData(10f, 20f, 1, 1)]
    [InlineData(-10f, -20f, -1, -1)]
    [InlineData(-10.01f, -20.01f, -2, -2)]
    public void WorldCoordinatesUseFloorAndRespectRectangularCells(float x, float y, int cx, int cy)
    {
        var geometry = new GridGeometry2D(new Vector2(10, 20), new(-70, 30));
        Assert.Equal(new GridCell2D(cx, cy), geometry.WorldToCell(new Vector2(x, y) + geometry.Origin));
        Assert.Equal(new GridCell2D(cx, cy), geometry.WorldToCell(geometry.GetCellCenter(new(cx, cy))));
    }

    [Fact]
    public void FiniteGridLookupAndClampingHaveSeparatePolicies()
    {
        var grid = new Grid2D<float>(3, 2, 10f, new(-10, -20));
        Assert.True(grid.TryGetCell(new(-10, -20), out var minimum));
        Assert.Equal(new GridCell2D(0, 0), minimum);
        Assert.False(grid.TryGetCell(grid.WorldBounds.Max, out _));
        Assert.Equal(new GridCell2D(2, 1), grid.Geometry.WorldToCellClamped(grid.WorldBounds.Max, grid.Size));
        Assert.Equal(new GridCell2D(0, 1), grid.Geometry.WorldToCellClamped(new(-float.MaxValue, float.MaxValue), grid.Size));
    }

    [Fact]
    public void RangesKeepMaximumEdgeContactsAndClipWithoutFoldingDistantBoundsOntoTheGrid()
    {
        var geometry = new GridGeometry2D(10f);
        var range = geometry.GetCellRange(new Rect2D(new(-10), new(10)));
        Assert.Equal(new GridCell2D(-1, -1), range.Minimum);
        Assert.Equal(new GridCell2D(1, 1), range.Maximum);
        Assert.Equal(9, range.CellCount);
        Assert.Equal(1, geometry.GetCellRange(new Bounds2D(new(10), new(10))).CellCount);

        var size = new GridSize2D(3, 2);
        Assert.Equal(size.Cells, geometry.GetCellRange(new Rect2D(new(-float.MaxValue), new(float.MaxValue)), size));
        Assert.True(geometry.GetCellRange(new Rect2D(new(31, 0), new(40, 10)), size).IsEmpty);
        Assert.Equal(new GridCellRange2D(new(2, 0), new(2, 1)),
            geometry.GetCellRange(new Rect2D(new(30, 0), new(30, 20)), size));
        Assert.Equal(new GridCellRange2D(new(0, 0), new(1, 1)), range.Intersect(size.Cells));
    }

    [Fact]
    public void RangeIterationIncludesIntegerLimitsWithoutWrapping()
    {
        var range = new GridCellRange2D(new(int.MaxValue - 1, int.MaxValue), new(int.MaxValue, int.MaxValue));
        GridCell2D[] cells = [.. range];
        Assert.Equal([range.Minimum, range.Maximum], cells);
        Assert.Empty((GridCell2D[])[.. default(GridCellRange2D)]);
        Assert.False(default(GridCellRange2D).Contains(default));
        Assert.Equal(4294967296L,
            new GridCellRange2D(new(int.MinValue, 0), new(int.MaxValue, 0)).CellCount);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new GridCellRange2D(new(int.MinValue, int.MinValue), new(int.MaxValue, int.MaxValue)));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void CellDimensionsMustBePositiveAndFinite(float size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridGeometry2D(new Vector2(1, size)));
    }

    [Fact]
    public void UnrepresentableCoordinatesSignalTheSpatialIndexToFallBack()
    {
        var geometry = new GridGeometry2D(1f);
        Assert.False(geometry.TryWorldToCell(new(float.MaxValue, 0), out _));
        Assert.False(geometry.TryWorldToCell(new(float.NaN, 0), out _));
        Assert.False(geometry.TryGetCellRange(Bounds2D.Unbounded, out _));
        Assert.False(geometry.TryGetCellRange(new Bounds2D(Vector2.One, Vector2.Zero), out _));
        Assert.False(default(GridGeometry2D).TryWorldToCell(Vector2.Zero, out _));
        Assert.Throws<InvalidOperationException>(() => default(GridGeometry2D).WorldToCell(Vector2.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => geometry.WorldToCell(new(float.MaxValue)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new GridGeometry2D(float.MaxValue).GetCellBounds(new(2, 0)));
    }

    [Fact]
    public void GeometryQueriesAndRangeIterationDoNotAllocate()
    {
        var geometry = new GridGeometry2D(16f);
        var bounds = new Bounds2D(new(-32), new(32));
        for (var i = 0; i < 100; i++) Visit();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Visit();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        void Visit()
        {
            foreach (var cell in geometry.GetCellRange(bounds))
                geometry.WorldToCell(geometry.GetCellCenter(cell));
        }
    }
}
