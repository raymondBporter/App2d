using App2d.Core.Geometry.Serialization;
using App2d.Core.Objects;
using App2d.Core.Tiles;
using App2d.Levels;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Levels;

public sealed class WorldObjectsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-world-objects-" + Guid.NewGuid().ToString("N"));
    private readonly WorldFile2D.WorldDocument _document;

    public WorldObjectsTests()
    {
        Directory.CreateDirectory(_root);
        using (var database = LevelDatabase2D.Open(Path.Combine(_root, "map.db")))
            database.Save(new EditableTileMap2D(32, 32, 32, 32, Vector2.Zero, ["dark-cave"]), 0);
        ObjectAsset2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"))
            .Save(Path.Combine(_root, "lift.json"));
        _document = new(1, [new("test", "Test", 0, [new("test", "Test", 0, "map.db")])]);
    }

    [Fact]
    public void MapDatabaseObjectDefinitionsBecomePlacementsWithWorldThingIds()
    {
        using (var database = LevelDatabase2D.Open(Path.Combine(_root, "map.db")))
        {
            var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "lift.json");
            database.CreatePositionThing(new(definition.DefinitionId, "first", true, 200f, 300f));
            database.CreatePositionThing(new(definition.DefinitionId, "second", true, 500f, 600f));
        }
        using var world = Load(_document);
        Assert.Equal(2, world.Objects.Count);
        Assert.Same(world.Objects[0].Asset, world.Objects[1].Asset);
        Assert.Equal(world.Maps[0].WorldThingId(1).ToString(), world.Objects[0].Id);
        Assert.Equal(new Vector2(500f, 600f), world.Objects[1].Position);
        Assert.Equal(_root, world.Root);
    }

    [Fact]
    public void DisabledObjectThingsAreNotPlaced()
    {
        using (var database = LevelDatabase2D.Open(Path.Combine(_root, "map.db")))
        {
            var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Lift", "lift.json");
            database.CreatePositionThing(new(definition.DefinitionId, "running", true, 200f, 300f));
            database.CreatePositionThing(new(definition.DefinitionId, "parked", false, 500f, 600f));
        }
        using var world = Load(_document);
        Assert.Equal(2, world.PositionThings.Count);
        Assert.Equal(new Vector2(200f, 300f), Assert.Single(world.Objects).Position);
    }

    [Fact]
    public void ObjectDefinitionWithMissingOrEscapingAssetFailsToLoad()
    {
        var assets = new[] { "missing.json", "../lift.json" };
        for (var i = 0; i < assets.Length; i++)
        {
            var path = Path.Combine(_root, $"bad-{i}.db");
            using (var database = LevelDatabase2D.Open(path))
            {
                database.Save(new EditableTileMap2D(32, 32, 32, 32, Vector2.Zero, ["dark-cave"]), 0);
                var definition = database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, "Bad", assets[i]);
                database.CreatePositionThing(new(definition.DefinitionId, null, true, 0f, 0f));
            }
            var error = Assert.Throws<InvalidDataException>(() => WorldFile2D.LoadMap(path, "bad", "Bad", "test", 7, worldRoot: _root));
            Assert.Contains(assets[i], error.Message);
        }
    }

    private WorldAsset2D Load(WorldFile2D.WorldDocument document)
    {
        var path = Path.Combine(_root, "world.json");
        File.WriteAllText(path, JsonSerializer.Serialize(document, GeometryJson2D.JsonOptions));
        return WorldFile2D.Load(path);
    }

    public void Dispose()
    {
        // The directory is created by this fixture; close its SQLite pools before removing it.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var mode in new[] { Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Microsoft.Data.Sqlite.SqliteOpenMode.ReadWriteCreate })
        {
            using var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(_root, "map.db"), Mode = mode
            }.ToString());
            Microsoft.Data.Sqlite.SqliteConnection.ClearPool(connection);
        }
        Directory.Delete(_root, true);
    }
}
