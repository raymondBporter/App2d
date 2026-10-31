using App2d.Core.Tiles;
using App2d.Levels;
using Microsoft.Data.Sqlite;
using System.Numerics;

namespace App2d.Tests.Levels;

public sealed class ThingDatabase2DTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-things-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void FreshDatabaseStartsWithAnEmptyThingLayer()
    {
        using var database = LevelDatabase2D.Open(NewPath());

        Assert.Equal(LevelDatabase2D.CurrentFormatVersion, database.FormatVersion);
        Assert.Empty(database.LoadThingDefinitions());
        Assert.Empty(database.LoadPositionThings());
    }

    [Fact]
    public void PositionOnlyDefinitionAndThingRoundTripWithoutExtraPieces()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);
        var definition = database.CreateThingDefinition("shieldback", "Basic shieldback");
        var created = database.CreatePositionThing(new NewPositionThing2D(
            definition.DefinitionId,
            "Guard one",
            true,
            320f,
            -96f));

        var loaded = Assert.Single(database.LoadPositionThings());
        Assert.Equal(created, loaded);
        Assert.Equal(1, Count(path, "transforms"));
        Assert.Equal(0, Count(path, "object_references"));
    }

    [Fact]
    public void PositionOnlyThingCanBeEditedDeletedAndRestoredForUndo()
    {
        using var database = LevelDatabase2D.Open(NewPath());
        var definition = database.CreateThingDefinition("goal", "Exit");
        var created = database.CreatePositionThing(new NewPositionThing2D(
            definition.DefinitionId,
            null,
            true,
            100f,
            200f));

        var updated = database.UpdatePositionThing(created with
        {
            Name = "Upper exit",
            Enabled = false,
            X = 150f,
            Y = 250f
        });
        Assert.Equal(updated, Assert.Single(database.LoadPositionThings()));

        var deleted = database.DeletePositionThing(created.ThingId);
        Assert.Empty(database.LoadPositionThings());
        database.RestorePositionThing(deleted);
        Assert.Equal(updated, Assert.Single(database.LoadPositionThings()));
    }

    [Fact]
    public void ObjectDefinitionRoundTripsItsAssetPathAndPlacedThingsCarryIt()
    {
        using var database = LevelDatabase2D.Open(NewPath());
        var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Bridge ferry", "objects/bridge-ferry.json");
        Assert.Equal("objects/bridge-ferry.json", Assert.Single(database.LoadThingDefinitions()).Asset);
        var thing = database.CreatePositionThing(new(definition.DefinitionId, null, true, 10f, 20f));
        Assert.Equal("objects/bridge-ferry.json", Assert.Single(database.LoadPositionThings()).Asset);
        Assert.Equal(thing.ThingId, Assert.Single(database.LoadPositionThings()).ThingId);
        _ = database.UpdateThingDefinition(definition with { Name = "Ferry", Asset = "objects/ferry.json" });
        Assert.Equal("objects/ferry.json", Assert.Single(database.LoadThingDefinitions()).Asset);
        Assert.Equal("Ferry", Assert.Single(database.LoadPositionThings()).DefinitionName);
    }

    [Fact]
    public void ObjectDefinitionNeedsAnAssetAndOtherTypesMustNotHaveOne()
    {
        using var database = LevelDatabase2D.Open(NewPath());
        Assert.Throws<ArgumentException>(() => database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift"));
        Assert.Throws<ArgumentException>(() => database.CreateThingDefinition("save-point", "Save", "objects/lift.json"));
        Assert.Empty(database.LoadThingDefinitions());
    }

    [Fact]
    public void DeletingAnObjectDefinitionCascadesItsReferenceAndIsRefusedWhilePlaced()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);
        var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "objects/lift.json");
        var thing = database.CreatePositionThing(new(definition.DefinitionId, null, true, 0f, 0f));
        Assert.Throws<SqliteException>(() => database.DeleteThingDefinition(definition.DefinitionId));
        database.DeletePositionThing(thing.ThingId);
        database.DeleteThingDefinition(definition.DefinitionId);
        Assert.Empty(database.LoadThingDefinitions());
        Assert.Equal(0, Count(path, "object_references"));
    }

    [Fact]
    public void MissingTilesetCatalogIsRejectedInsteadOfUsingAnImplicitDefault()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path)) database.Save(new EditableTileMap2D(4, 4, 32, 4), 0);
        Execute(path, "DELETE FROM meta WHERE key LIKE 'tileset_%';");
        using var reader = LevelDatabase2D.OpenRead(path);
        Assert.Contains("tileset_count", Assert.Throws<InvalidOperationException>(() => reader.Load()).Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void NoncurrentDatabasesAreRejectedWithoutChangingTheirContents(int version)
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path)) database.Save(new EditableTileMap2D(4, 4, 32, 4), 0);
        Execute(path, $"PRAGMA user_version = {version};");
        var before = ReadBytes(path);
        Assert.Contains($"version {version}", Assert.Throws<InvalidOperationException>(() => LevelDatabase2D.Open(path)).Message);
        Assert.Contains($"version {version}", Assert.Throws<InvalidOperationException>(() => LevelDatabase2D.OpenRead(path)).Message);
        Assert.Equal(before, ReadBytes(path));
    }

    [Fact]
    public void OpeningACurrentDatabaseForEditingDoesNotModifyIt()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
        {
            Assert.Equal(3, database.FormatVersion);
        }

        Assert.DoesNotContain("rectangle_shapes", TableNames(path));
        var bytes = ReadBytes(path);
        using (var again = LevelDatabase2D.Open(path))
        {
            Assert.Equal(3, again.FormatVersion);
            Assert.Empty(again.LoadThingDefinitions());
        }

        Assert.Equal(bytes, ReadBytes(path)); // Opening a current file for editing rewrites nothing.
    }

    /// <summary>Closes pooled connections first: SQLite keeps the file open, and the bytes must be read after every write has landed.</summary>
    private static byte[] ReadBytes(string path)
    {
        SqliteConnection.ClearAllPools();
        return File.ReadAllBytes(path);
    }

    private static List<string> TableNames(string path)
    {
        using var connection = OpenRaw(path);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private string NewPath()
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".db");
    }

    private static int Count(string path, string table)
    {
        using var connection = OpenRaw(path);
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {table};";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void Execute(string path, string sql)
    {
        using var connection = OpenRaw(path);
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static SqliteConnection OpenRaw(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (!Directory.Exists(_directory))
        {
            return;
        }

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 10)
            {
                Thread.Sleep(20);
            }
        }
    }
}
