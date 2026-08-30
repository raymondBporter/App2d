using App2d.Core.Geometry;
using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using Xunit;

namespace App2d.Gameplay.Tests.World;

/// <summary>
/// Characterizes spike terrain in the committed cavern level, not the (soon to be deleted)
/// world generator -- the committed <c>.db</c> is what the game actually loads.
/// </summary>
public sealed class SpikeTerrain2DTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-spike-terrain-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(_directory))
            return;

        // Mirrors App2d.Tests/Levels/LevelDatabase2DTests.cs: Microsoft.Data.Sqlite pools
        // native handles, so clear the pool before deleting the temp copy.
        foreach (var dbFile in Directory.EnumerateFiles(_directory, "*.db"))
        {
            using var probe = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbFile,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());
            SqliteConnection.ClearPool(probe);
        }

        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(20);
            }
        }
    }

    [Fact]
    public void AuthoredSpikesDamageTheirTilesAndNotClearTiles()
    {
        var traversal = (TraversalMetrics2D)Activator.CreateInstance(
            typeof(TraversalMetrics2D),
            nonPublic: true)!;

        // Work on a copy so this test never risks writing back to the checked-in asset.
        var committedPath = Path.Combine(
            TestAssetPath.StaticRoot, "levels", "cavern", "level.db");
        Directory.CreateDirectory(_directory);
        var copyPath = Path.Combine(_directory, "level.db");
        File.Copy(committedPath, copyPath);

        using var database = LevelDatabase2D.Open(copyPath);
        var tileMap = database.Load();

        var groundHeights = TileGroundHeights2D.Derive(tileMap);
        var level = new SideScrollerLevel2D(
            traversal,
            tileMap,
            x => groundHeights[Math.Clamp(x, 0, groundHeights.Length - 1)]);
        var spikes = FindSpikes(level);
        Assert.NotEmpty(spikes);
        Assert.All(spikes, spike =>
        {
            var spikeTileMin = level.TileMap.Origin +
                new System.Numerics.Vector2(spike.X, spike.Y) * level.TileMap.TileSize;
            var insideSpike = new Bounds2D(
                spikeTileMin + new System.Numerics.Vector2(8f, 0f),
                spikeTileMin + new System.Numerics.Vector2(24f, 20f));
            Assert.True(level.TryGetSpikeSource(insideSpike, out var spikeSourceX));
            Assert.Equal(spikeTileMin.X + level.TileMap.TileSize / 2f, spikeSourceX);
        });

        var clear = FindClearTile(level);
        var clearTileMin = level.TileMap.Origin +
            new System.Numerics.Vector2(clear.X, clear.Y) * level.TileMap.TileSize;
        var insideClearTile = new Bounds2D(
            clearTileMin + new System.Numerics.Vector2(8f),
            clearTileMin + new System.Numerics.Vector2(24f));
        Assert.False(level.TryGetSpikeSource(insideClearTile, out _));
    }

    private static List<(int X, int Y)> FindSpikes(SideScrollerLevel2D level)
    {
        var spikes = new List<(int X, int Y)>();
        for (var x = 0; x < level.TileMap.Width; x++)
        {
            for (var y = 0; y < level.TileMap.Height; y++)
            {
                if (level.TileMap.GetTileKind(x, y).IsSpikes())
                    spikes.Add((x, y));
            }
        }

        return spikes;
    }

    private static (int X, int Y) FindClearTile(SideScrollerLevel2D level)
    {
        for (var x = 0; x < level.TileMap.Width; x++)
        {
            for (var y = 0; y < level.TileMap.Height; y++)
            {
                if (!level.TileMap.GetTileKind(x, y).IsSpikes())
                    return (x, y);
            }
        }

        throw new InvalidOperationException("The authored level has no clear tile.");
    }
}
