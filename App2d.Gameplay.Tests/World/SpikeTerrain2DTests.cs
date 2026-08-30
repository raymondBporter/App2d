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
    public void GeneratedSpikesSitAboveSolidGroundAndDamageOnlyTheirTile()
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
        Assert.Contains(spikes, spike => spike.X < level.TileMap.Width / 2);
        Assert.Contains(spikes, spike => spike.X >= level.TileMap.Width / 2);
        var (X, Y) = spikes[0];

        Assert.True(level.TileMap.GetTileKind(X, Y - 1).IsSolid());

        var tileMin = level.TileMap.Origin +
            new System.Numerics.Vector2(X, Y) * level.TileMap.TileSize;
        var inside = new Bounds2D(
            tileMin + new System.Numerics.Vector2(8f, 0f),
            tileMin + new System.Numerics.Vector2(24f, 20f));
        Assert.True(level.TryGetSpikeSource(inside, out var sourceX));
        Assert.Equal(tileMin.X + level.TileMap.TileSize / 2f, sourceX);

        var clearTile = new Bounds2D(
            tileMin + new System.Numerics.Vector2(0f, level.TileMap.TileSize),
            tileMin + new System.Numerics.Vector2(16f, level.TileMap.TileSize + 16f));
        Assert.False(level.TryGetSpikeSource(clearTile, out _));
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
}
