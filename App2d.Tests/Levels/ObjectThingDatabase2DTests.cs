using App2d.Levels;
using Microsoft.Data.Sqlite;

namespace App2d.Tests.Levels;

public sealed class ObjectThingDatabase2DTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "app2d-objects-" + Guid.NewGuid().ToString("N"));

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
        using var database = LevelDatabase2D.Open(NewPath());
        var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "objects/lift.json");
        var thing = database.CreatePositionThing(new(definition.DefinitionId, null, true, 0f, 0f));
        Assert.Throws<SqliteException>(() => database.DeleteThingDefinition(definition.DefinitionId));
        database.DeletePositionThing(thing.ThingId);
        database.DeleteThingDefinition(definition.DefinitionId);
        Assert.Empty(database.LoadThingDefinitions());
    }

    [Fact]
    public void VersionTwoFileGainsObjectReferencesOnOpen()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
        {
        }

        Execute(path, "DROP TABLE object_references; PRAGMA user_version = 2;");
        using var reopened = LevelDatabase2D.Open(path);
        Assert.Equal(3, reopened.FormatVersion);
        Assert.Empty(reopened.LoadThingDefinitions());
        var definition = reopened.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "objects/lift.json");
        Assert.Equal("objects/lift.json", Assert.Single(reopened.LoadThingDefinitions()).Asset);
    }

    private string NewPath()
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".db");
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
