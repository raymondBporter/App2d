using App2d.Levels;
using App2d.Tiles;
using Xunit;

namespace App2d.Gameplay.Tests.World;

public sealed class CheckpointBacktracking2DTests
{
    [Fact]
    public void BrokenBridgeCheckpointHasBidirectionalSteppingStone()
    {
        var committedPath = Path.Combine(
            TestAssetPath.StaticRoot, "levels", "cavern", "level.db");

        using var database = LevelDatabase2D.OpenRead(committedPath);
        var tileMap = database.Load();

        // The center platform turns the nine-tile pit before the checkpoint into
        // two comfortable three-tile jumps while preserving the broken bridge.
        Assert.True(tileMap.GetTileKind(120, 11).IsSolid());
        Assert.All(new[] { 121, 122, 123 }, x =>
            Assert.Equal(TileKind2D.Empty, tileMap.GetTileKind(x, 11)));
        Assert.All(new[] { 124, 125, 126 }, x =>
            Assert.True(tileMap.GetTileKind(x, 11).IsOneWay()));
        Assert.All(new[] { 127, 128, 129 }, x =>
            Assert.Equal(TileKind2D.Empty, tileMap.GetTileKind(x, 11)));
        Assert.True(tileMap.GetTileKind(130, 11).IsSolid());
    }
}
