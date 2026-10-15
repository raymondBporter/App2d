using App2d.Core.Tiles;
using App2d.Gameplay.World;
using App2d.Levels;

namespace App2d.Tests.Levels;

public sealed class GiantTreeFile2DTests
{
    [Fact]
    public void CommittedGroveLoadsInTileUnitsWithGroundedRoots()
    {
        var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin);
        for (var x = 0; x < map.Width; x++)
        {
            for (var y = 0; y < 12; y++)
            {
                map.SetTileKind(x, y, TileKind2D.Solid);
            }
        }

        var path = Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "giant-trees.json");
        var trees = GiantTreeFile2D.Load(path, map);
        Assert.Equal(4, trees.Length);
        Assert.Equal(map.Origin.X + 23 * 32, trees[0].Root.X);
        Assert.All(trees, tree => Assert.Equal(map.Origin.Y + 12 * 32, tree.Root.Y));
        Assert.Equal(48 * 32, trees[0].Settings.Height);
        Assert.Equal(4 * 32, trees[0].Settings.BranchLength.Min);
        Assert.Equal(12 * 32, trees[0].Settings.BranchLength.Max);
    }

    [Theory]
    [InlineData("{\"version\":1,\"trees\":[],\"branchGapTiles\":[0,3]}")]
    [InlineData("{\"version\":1,\"trees\":[],\"branchLengthTiles\":[12,4]}")]
    [InlineData("{\"version\":1,\"trees\":[{\"xTiles\":20,\"heightTiles\":48,\"trunkWidthTiles\":6}]}")]
    public void MalformedRangesAndMissingSeedsGiveFileSpecificErrors(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, json);
            var map = new EditableTileMap2D(640, 96, 32, 32, SideScrollerLevel2D.WorldOrigin);
            var error = Assert.Throws<InvalidDataException>(() => GiantTreeFile2D.Load(path, map));
            Assert.Contains(path, error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
