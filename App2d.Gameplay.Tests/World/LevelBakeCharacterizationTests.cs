using App2d.Gameplay;
using App2d.Levels;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using Xunit;

namespace App2d.Gameplay.Tests.World;

/// <summary>
/// Proves the level format reproduces the world generator exactly. Delete this file when
/// <see cref="JumpableWorldGenerator2D"/> is deleted — it characterizes scaffolding.
/// </summary>
public sealed class LevelBakeCharacterizationTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-bake-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (!Directory.Exists(_directory))
            return;

        // Microsoft.Data.Sqlite pools native sqlite3 handles by default: disposing a
        // SqliteConnection returns the handle to the pool rather than closing it, which
        // on Windows keeps the file locked for a moment after the last `using` block
        // here has already run. Clear the pool for the database file before deleting,
        // then allow a short bounded retry as a safety margin -- never a blanket catch
        // that would hide a genuine leak. Mirrors App2d.Tests/Levels/LevelDatabase2DTests.cs.
        ClearPoolsForDatabaseFiles();
        DeleteDirectoryWithRetry(_directory);
    }

    private void ClearPoolsForDatabaseFiles()
    {
        foreach (var dbFile in Directory.EnumerateFiles(_directory, "*.db"))
        {
            using var probe = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = dbFile,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());
            SqliteConnection.ClearPool(probe);
        }
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(20);
            }
        }
    }

    [Fact]
    public void EveryBakedTileMatchesTheGenerator()
    {
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);

        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        map.Fill(generator.GetTileKind);

        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "level.db");
        using (var database = LevelDatabase2D.Open(path))
            database.Save(map, SideScrollerLevel2D.WorldSeed);

        using var reopened = LevelDatabase2D.Open(path);
        var loaded = reopened.Load();

        var mismatches = 0;
        for (var y = 0; y < SideScrollerLevel2D.WorldHeightTiles; y++)
        {
            for (var x = 0; x < SideScrollerLevel2D.WorldWidthTiles; x++)
            {
                if (loaded.GetTileKind(x, y) != generator.GetTileKind(x, y))
                    mismatches++;
            }
        }

        Assert.Equal(0, mismatches);
    }

    [Fact]
    public void GroundHeightsNeverIndexBelowTheWorld()
    {
        var traversal = TraversalMetrics2D.FromPlayerAsset(TestAssetPath.Root);
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        map.Fill(generator.GetTileKind);

        // Ground height is provisional and may differ from the generator's TerrainHeight.
        // The only contract is that `height - 1` stays inside the world.
        Assert.All(TileGroundHeights2D.Derive(map), height => Assert.True(height >= 1));
    }
}
