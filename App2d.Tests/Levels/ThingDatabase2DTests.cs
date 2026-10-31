using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Physics;
using App2d.Core.Tiles;
using App2d.Levels;
using Microsoft.Data.Sqlite;
using System.Globalization;
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
    public void VersionTwoFileWithoutPlatformsGainsObjectReferencesOnOpen()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
        {
        }

        Execute(path, "DROP TABLE object_references; PRAGMA user_version = 2;");
        using var reopened = LevelDatabase2D.Open(path);
        Assert.Equal(3, reopened.FormatVersion);
        Assert.Empty(reopened.LoadThingDefinitions());
        _ = reopened.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "objects/lift.json");
        Assert.Equal("objects/lift.json", Assert.Single(reopened.LoadThingDefinitions()).Asset);
    }

    [Fact]
    public void VersionOneMigrationPreservesTileChunksAndCreatesEmptyThingTables()
    {
        var path = NewPath();
        var map = new EditableTileMap2D(8, 4, 32f, 4);
        map.SetTileKind(3, 2, TileKind2D.Spikes);
        using (var database = LevelDatabase2D.Open(path))
        {
            database.Save(map, 17UL);
        }

        Execute(path, """
            DROP TABLE object_references;
            DROP TABLE transforms;
            DROP TABLE things;
            DROP TABLE thing_definitions;
            PRAGMA user_version = 1;
            """);

        using var migrated = LevelDatabase2D.Open(path);
        Assert.Equal(LevelDatabase2D.CurrentFormatVersion, migrated.FormatVersion);
        Assert.Equal(TileKind2D.Spikes, migrated.Load().GetTileKind(3, 2));
        Assert.Empty(migrated.LoadPositionThings());
        Assert.DoesNotContain("rectangle_shapes", TableNames(path));
    }

    [Fact]
    public void VersionTwoPlatformRowsBecomeNamedObjectAssetsAndObjectThings()
    {
        var path = WriteVersionTwoPlatforms(("Bridge ferry", 10f, 20f, 288f, 0f, 105f), ("Summit lift", 30f, 40f, 0f, 256f, 110f));
        using var database = LevelDatabase2D.Open(path, worldRoot: _directory);
        Assert.Equal(3, database.FormatVersion);
        var ferry = Part.Load(Path.Combine(_directory, "objects", "bridge-ferry.json"));
        Assert.Equal(new Vector2(-48f, -8f), ShapeBounds2D.Calculate((Core.Geometry.Shapes.IShape2D)ferry.Geometry[0].Geometry!).Min);
        Assert.Equal("#25d2be", ferry.Geometry[0].Material!.Fill);
        Assert.Equal(BodyMotionType2D.Kinematic, ferry.Physics!.MotionType);
        Assert.Equal(0f, ferry.Physics.Restitution);
        Assert.True(ferry.Physics.TransfersContactMotion);
        Assert.Equal(Vector2.UnitY, ferry.Surface!.OneWayNormal);
        var line = Assert.IsType<LineSegmentCurve2D>(ferry.Motion!.Curve);
        Assert.Equal(new Vector2(288f, 0f), line.End);
        Assert.Equal(2 * 288 / 105d, ferry.Motion.PeriodSeconds, 6);
        Assert.Equal([ProgressMapKind.PingPong, ProgressMapKind.Smooth], ferry.Motion.Progress);
        Assert.True(File.Exists(Path.Combine(_directory, "objects", "summit-lift.json")));
        var definitions = database.LoadThingDefinitions();
        Assert.Equal(["Bridge ferry", "Summit lift"], definitions.Select(d => d.Name));
        Assert.All(definitions, d => Assert.Equal(LevelDatabase2D.ObjectTypeKey, d.TypeKey));
        Assert.Equal(["objects/bridge-ferry.json", "objects/summit-lift.json"], definitions.Select(d => d.Asset));
        var things = database.LoadPositionThings();
        Assert.Equal([(10f, 20f), (30f, 40f)], things.Select(t => (t.X, t.Y)));
        Assert.DoesNotContain("rectangle_shapes", TableNames(path));
        Assert.DoesNotContain("ping_pong_motors", TableNames(path));
    }

    [Fact]
    public void IdenticalPlatformsShareOneMigratedAssetAndDefinition()
    {
        var path = WriteVersionTwoPlatforms(("A", 0f, 0f, 100f, 0f, 50f), ("B", 5f, 5f, 100f, 0f, 50f), ("C", 9f, 9f, 0f, 100f, 50f));
        using var database = LevelDatabase2D.Open(path, worldRoot: _directory);
        // A and B share one group, so that group keeps the definition's name; C is alone and takes its own.
        Assert.Equal(["C", "Lift"], database.LoadThingDefinitions().Select(d => d.Name));
        Assert.Equal(["objects/c.json", "objects/lift.json"], database.LoadThingDefinitions().Select(d => d.Asset));
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_directory, "objects")).Length);
        Assert.Equal(3, database.LoadPositionThings().Count);
        Assert.Equal(2, database.LoadPositionThings().Count(t => t.Asset == "objects/lift.json"));
    }

    [Fact]
    public void MigrationRefusesToOverwriteADifferentExistingAsset()
    {
        var path = WriteVersionTwoPlatforms(("Bridge ferry", 0f, 0f, 288f, 0f, 105f));
        Directory.CreateDirectory(Path.Combine(_directory, "objects"));
        File.WriteAllText(Path.Combine(_directory, "objects", "bridge-ferry.json"), "{ \"format\": \"app2d-object\" }");
        var error = Assert.Throws<InvalidDataException>(() => LevelDatabase2D.Open(path, worldRoot: _directory));
        Assert.Contains("bridge-ferry.json", error.Message);
        Assert.Contains("rectangle_shapes", TableNames(path));
        Assert.Equal(2, UserVersion(path));
    }

    [Theory]
    [InlineData("DELETE FROM solid_color_art;", "Lift")]
    [InlineData("DELETE FROM ping_pong_motors WHERE thing_id = 1;", "A")]
    public void MigrationRefusesAPlatformMissingOneOfItsPieces(string damage, string named)
    {
        var path = WriteVersionTwoPlatforms(("A", 0f, 0f, 100f, 0f, 50f));
        Execute(path, damage);
        var error = Assert.Throws<InvalidDataException>(() => LevelDatabase2D.Open(path, worldRoot: _directory));
        Assert.Contains(named, error.Message);
        Assert.Contains("rectangle_shapes", TableNames(path));
        Assert.Equal(2, UserVersion(path));
    }

    [Fact]
    public void ReadOnlyOpenOfAnOldFileNamesTheMigrationCommand()
    {
        var path = WriteVersionTwoPlatforms();
        var error = Assert.ThrowsAny<Exception>(() => LevelDatabase2D.OpenRead(path));
        Assert.Contains("--migrate-level", error.Message);
    }

    [Fact]
    public void FreshDatabaseHasNoPlatformTablesAndMigrationIsIdempotent()
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

    /// <summary>A format-2 file with one "Lift" definition (96 by 16, teal) and the given platform things, as the August editor wrote them.</summary>
    private string WriteVersionTwoPlatforms(params (string Name, float X, float Y, float TravelX, float TravelY, float Speed)[] things)
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
        {
            database.Save(new EditableTileMap2D(8, 4, 32f, 4), 0);
        }

        var sql = new System.Text.StringBuilder("""
            CREATE TABLE IF NOT EXISTS rectangle_shapes(definition_id INTEGER PRIMARY KEY REFERENCES thing_definitions(definition_id) ON DELETE CASCADE, width REAL NOT NULL, height REAL NOT NULL);
            CREATE TABLE IF NOT EXISTS solid_color_art(definition_id INTEGER PRIMARY KEY REFERENCES thing_definitions(definition_id) ON DELETE CASCADE, color_argb INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS ping_pong_motors(thing_id INTEGER PRIMARY KEY REFERENCES things(thing_id) ON DELETE CASCADE, travel_x REAL NOT NULL, travel_y REAL NOT NULL, speed REAL NOT NULL);
            INSERT INTO thing_definitions(definition_id, type_key, name) VALUES(1, 'moving-platform', 'Lift');
            INSERT INTO rectangle_shapes(definition_id, width, height) VALUES(1, 96, 16);
            """);
        sql.Append(CultureInfo.InvariantCulture, $"INSERT INTO solid_color_art(definition_id, color_argb) VALUES(1, {unchecked((int)0xFF25D2BEu)});");
        for (var i = 0; i < things.Length; i++)
        {
            var (name, x, y, travelX, travelY, speed) = things[i];
            sql.Append(CultureInfo.InvariantCulture, $"INSERT INTO things(thing_id, definition_id, name, enabled) VALUES({i + 1}, 1, '{name}', 1);");
            sql.Append(CultureInfo.InvariantCulture, $"INSERT INTO transforms(thing_id, x, y, rotation) VALUES({i + 1}, {x}, {y}, 0);");
            sql.Append(CultureInfo.InvariantCulture, $"INSERT INTO ping_pong_motors(thing_id, travel_x, travel_y, speed) VALUES({i + 1}, {travelX}, {travelY}, {speed});");
        }

        sql.Append("DROP TABLE IF EXISTS object_references; PRAGMA user_version = 2;");
        Execute(path, sql.ToString());
        return path;
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

    private static int UserVersion(string path)
    {
        using var connection = OpenRaw(path);
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
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
