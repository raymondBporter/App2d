using App2d.Levels;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using System.Numerics;

namespace App2d.Tests.Levels;

public sealed class MovingPlatformThingDatabase2DTests : IDisposable
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
        Assert.Empty(database.LoadMovingPlatformDefinitions());
        Assert.Empty(database.LoadMovingPlatforms());
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
        Assert.Equal(0, Count(path, "ping_pong_motors"));
        Assert.Equal(0, Count(path, "rectangle_shapes"));
        Assert.Equal(0, Count(path, "solid_color_art"));
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
    public void DefinitionAndPlacedThingRoundTripAsTypedRows()
    {
        var path = NewPath();
        long thingId;
        using (var database = LevelDatabase2D.Open(path))
        {
            var definition = database.CreateMovingPlatformDefinition(
                "Teal lift", 96f, 16f, unchecked((int)0xFF25D2BEu));
            var thing = database.CreateMovingPlatform(new NewMovingPlatformThing2D(
                definition.DefinitionId,
                "Entrance lift",
                true,
                128f,
                -64f,
                0f,
                96f,
                0f,
                48f));
            thingId = thing.ThingId;
        }

        using var reader = LevelDatabase2D.OpenRead(path);
        var loaded = Assert.Single(reader.LoadMovingPlatforms());
        Assert.Equal(thingId, loaded.ThingId);
        Assert.Equal("Teal lift", loaded.DefinitionName);
        Assert.Equal("Entrance lift", loaded.Name);
        Assert.Equal(96f, loaded.Width);
        Assert.Equal(16f, loaded.Height);
        Assert.Equal(new Vector2(128f, -64f), new Vector2(loaded.X, loaded.Y));
        Assert.Equal(new Vector2(96f, 0f), new Vector2(loaded.TravelX, loaded.TravelY));
        Assert.Equal(48f, loaded.Speed);
    }

    [Fact]
    public void DeletingAThingCascadesItsInstancePiecesAndItCanBeRestoredForUndo()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);
        var definition = database.CreateMovingPlatformDefinition("Lift", 64f, 12f, -1);
        var thing = database.CreateMovingPlatform(new NewMovingPlatformThing2D(
            definition.DefinitionId, null, true, 0f, 0f, 0f, 64f, 0f, 32f));

        var deleted = database.DeleteMovingPlatform(thing.ThingId);
        Assert.Empty(database.LoadMovingPlatforms());
        Assert.Equal(0, Count(path, "transforms"));
        Assert.Equal(0, Count(path, "ping_pong_motors"));

        database.RestoreMovingPlatform(deleted);
        Assert.Equal(thing.ThingId, Assert.Single(database.LoadMovingPlatforms()).ThingId);
    }

    [Fact]
    public void ReferencedDefinitionCannotBeDeletedButUnreferencedPiecesCascade()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);
        var definition = database.CreateMovingPlatformDefinition("Lift", 64f, 12f, -1);
        var thing = database.CreateMovingPlatform(new NewMovingPlatformThing2D(
            definition.DefinitionId, null, true, 0f, 0f, 0f, 64f, 0f, 32f));

        Assert.Throws<SqliteException>(() => database.DeleteMovingPlatformDefinition(definition.DefinitionId));

        database.DeleteMovingPlatform(thing.ThingId);
        database.DeleteMovingPlatformDefinition(definition.DefinitionId);
        Assert.Empty(database.LoadMovingPlatformDefinitions());
        Assert.Equal(0, Count(path, "rectangle_shapes"));
        Assert.Equal(0, Count(path, "solid_color_art"));
    }

    [Fact]
    public void MissingRequiredPieceNamesTheDefinitionAndType()
    {
        var path = NewPath();
        long definitionId;
        using (var database = LevelDatabase2D.Open(path))
            definitionId = database.CreateMovingPlatformDefinition("Lift", 64f, 12f, -1).DefinitionId;

        Execute(path, $"DELETE FROM rectangle_shapes WHERE definition_id = {definitionId};");

        using var reader = LevelDatabase2D.OpenRead(path);
        var exception = Assert.Throws<InvalidOperationException>(() => reader.LoadMovingPlatformDefinitions());
        Assert.Contains(definitionId.ToString(), exception.Message);
        Assert.Contains("moving-platform", exception.Message);
        Assert.Contains("rectangle_shapes", exception.Message);
    }

    [Fact]
    public void InvalidPlacementDoesNotLeavePartialThingRows()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);
        var definition = database.CreateMovingPlatformDefinition("Lift", 64f, 12f, -1);

        Assert.Throws<InvalidOperationException>(() =>
            database.CreateMovingPlatform(new NewMovingPlatformThing2D(
                definition.DefinitionId, null, true, 0f, 0f, 0f, 0f, 0f, 32f)));
        Assert.Empty(database.LoadMovingPlatforms());
        Assert.Equal(0, Count(path, "things"));
    }

    [Fact]
    public void VersionOneMigrationPreservesTileChunksAndCreatesEmptyThingTables()
    {
        var path = NewPath();
        var map = new EditableTileMap2D(8, 4, 32f, 4);
        map.SetTileKind(3, 2, TileKind2D.Spikes);
        using (var database = LevelDatabase2D.Open(path))
            database.Save(map, 17UL);

        Execute(path, """
            DROP TABLE ping_pong_motors;
            DROP TABLE transforms;
            DROP TABLE things;
            DROP TABLE solid_color_art;
            DROP TABLE rectangle_shapes;
            DROP TABLE thing_definitions;
            PRAGMA user_version = 1;
            """);

        using var migrated = LevelDatabase2D.Open(path);
        Assert.Equal(LevelDatabase2D.CurrentFormatVersion, migrated.FormatVersion);
        Assert.Equal(TileKind2D.Spikes, migrated.Load().GetTileKind(3, 2));
        Assert.Empty(migrated.LoadMovingPlatforms());
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
            return;
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
