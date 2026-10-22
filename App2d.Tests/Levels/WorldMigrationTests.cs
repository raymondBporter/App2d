using App2d.Core.Tiles;
using App2d.Levels;
using App2d.Tests.Gameplay.World;
using Microsoft.Data.Sqlite;
using System.Numerics;

namespace App2d.Tests.Levels;

/// <summary>A world written before format 3: one map at version 2 and a manifest that still lists an object placement.</summary>
public sealed class WorldMigrationTests : IDisposable
{
    private readonly string _root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "app2d-world-migration-" + Guid.NewGuid().ToString("N")));

    public WorldMigrationTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "objects"));
        var mapPath = Path.Combine(_root, "map.db");
        using (var database = LevelDatabase2D.Open(mapPath))
        {
            database.Save(new EditableTileMap2D(32, 32, 32, 32, Vector2.Zero, ["dark-cave"]), 0);
        }

        Execute(mapPath, "DROP TABLE object_references; PRAGMA user_version = 2;");
        ObjectPlatforms.Asset("lift", new(0f, 96f), 48f).Save(Path.Combine(_root, "objects", "lift.json"));
        File.WriteAllText(ManifestPath, """
            {
              "version": 1,
              "objects": [ { "id": "arrival", "asset": "objects/lift.json", "position": { "x": 10, "y": 20 } } ],
              "zones": [ { "id": "test", "name": "Test", "priority": 0, "maps": [ { "id": "test", "name": "Test", "identity": 0, "file": "map.db" } ] } ],
              "startMapId": "test"
            }
            """);
    }

    private string ManifestPath => Path.Combine(_root, "world.json");

    [Fact]
    public void MigrateWorldOpensEveryMapImportsManifestObjectsAndStripsThem()
    {
        var migrated = LevelBootstrap2D.MigrateWorld(ManifestPath);
        Assert.Equal([Path.Combine(_root, "map.db")], migrated);
        using var database = LevelDatabase2D.OpenRead(Path.Combine(_root, "map.db"));
        Assert.Equal(3, database.FormatVersion);
        var thing = Assert.Single(database.LoadPositionThings());
        Assert.Equal(("arrival", 10f, 20f, "objects/lift.json"), (thing.Name, thing.X, thing.Y, thing.Asset));
        Assert.DoesNotContain("\"objects\"", File.ReadAllText(ManifestPath));
        Assert.Equal(migrated, LevelBootstrap2D.MigrateWorld(ManifestPath));
        using var world = WorldFile2D.Load(ManifestPath);
        Assert.Equal(new Vector2(10f, 20f), Assert.Single(world.Objects).Position);
    }

    [Fact]
    public void ManifestObjectsArrayIsRejectedAfterMigration()
    {
        var error = Assert.Throws<InvalidDataException>(() => WorldFile2D.Load(ManifestPath));
        Assert.Contains("--migrate-level", error.Message);
    }

    private static void Execute(string path, string sql)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWrite }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(20);
            }
        }
    }
}
