using App2d.Tiles;

namespace App2d.Tests.Tiles;

public sealed class TileGroundHeights2DTests
{
    [Fact]
    public void GroundHeightIsTheLowestNonSolidRow()
    {
        var map = new EditableTileMap2D(3, 8, 32f, 4);
        // Column 0 solid to row 2, column 1 solid to row 4, column 2 solid to row 1.
        map.Fill((x, y) => y < x switch { 0 => 3, 1 => 5, _ => 2 }
            ? TileKind2D.Solid
            : TileKind2D.Empty);

        var heights = TileGroundHeights2D.Derive(map);

        Assert.Equal([3, 5, 2], heights);
    }

    [Fact]
    public void EmptyColumnClampsToOneSoCallersNeverIndexBelowTheWorld()
    {
        var map = new EditableTileMap2D(2, 8, 32f, 4);
        map.Fill((x, y) => x == 0 && y < 4 ? TileKind2D.Solid : TileKind2D.Empty);

        var heights = TileGroundHeights2D.Derive(map);

        Assert.Equal(4, heights[0]);
        // A pit column has no solid floor at all; the clamp keeps `height - 1` at 0.
        Assert.Equal(1, heights[1]);
    }

    [Fact]
    public void FullySolidColumnReportsTheMapHeight()
    {
        var map = new EditableTileMap2D(1, 5, 32f, 4);
        map.Fill((_, _) => TileKind2D.Solid);

        Assert.Equal([5], TileGroundHeights2D.Derive(map));
    }

    [Fact]
    public void OneWayAndSpikeTilesAreNotGround()
    {
        var map = new EditableTileMap2D(1, 6, 32f, 4);
        map.Fill((_, y) => y switch
        {
            0 => TileKind2D.Solid,
            1 => TileKind2D.Spikes,
            2 => TileKind2D.OneWay,
            _ => TileKind2D.Empty
        });

        Assert.Equal([1], TileGroundHeights2D.Derive(map));
    }

    [Fact]
    public void EveryDerivedHeightIsAtLeastOne()
    {
        var map = new EditableTileMap2D(16, 8, 32f, 4);

        Assert.All(TileGroundHeights2D.Derive(map), height => Assert.True(height >= 1));
    }
}
