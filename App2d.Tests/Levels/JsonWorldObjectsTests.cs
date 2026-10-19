using App2d.Core.Geometry.Serialization;
using App2d.Core.Objects;
using App2d.Core.Tiles;
using App2d.Gameplay.Simulation;
using App2d.Gameplay.World;
using App2d.Levels;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Levels;

public sealed class JsonWorldObjectsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-json-objects-" + Guid.NewGuid().ToString("N"));
    private readonly WorldFile2D.WorldDocument _document;

    public JsonWorldObjectsTests()
    {
        Directory.CreateDirectory(_root);
        using (var database = LevelDatabase2D.Open(Path.Combine(_root, "map.db")))
            database.Save(new EditableTileMap2D(32, 32, 32, 32, Vector2.Zero, ["dark-cave"]), 0);
        ObjectAsset2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json"))
            .Save(Path.Combine(_root, "lift.json"));
        _document = new(1, [new("test", "Test", 0, [new("test", "Test", 0, "map.db")])])
        {
            Objects = [new("first", "lift.json", new(200, 300)), new("second", "lift.json", new(500, 600))]
        };
    }

    [Fact]
    public void ManifestReferencesSpawnIndependentInstancesWithoutThingRegistryEntries()
    {
        using var world = Load(_document);
        Assert.Equal(2, world.Objects.Count);
        Assert.Same(world.Objects[0].Asset, world.Objects[1].Asset);
        using var simulation = SideScrollerSimulation2D.Create(new(
            TraversalMetricsLoader2D.Load(TestAssetPath.Root), world.TileMap, [], []) { Objects = world.Objects });
        var first = simulation.Level.Objects[0];
        var second = simulation.Level.Objects[1];
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotSame(first.Body, second.Body);
        var before = first.WorldObject.Transform.Position;
        simulation.Session.Advance();
        var firstDelta = first.WorldObject.Transform.Position - before;
        Assert.True(firstDelta.LengthSquared() > 0);
        Assert.InRange(Vector2.Distance(second.WorldObject.Transform.Position - first.WorldObject.Transform.Position, new(300, 300)), 0, .001f);
        Assert.Equal(2, simulation.Session.CaptureSnapshot().World.Objects.Length);
    }

    [Fact]
    public void DuplicatePlacementIdsAndAssetPathsOutsideTheWorldAreRejected()
    {
        Assert.Throws<InvalidDataException>(() => Load(_document with
        {
            Objects = [new("same", "lift.json", Vector2.Zero), new("same", "lift.json", Vector2.One)]
        }));
        Assert.Throws<InvalidDataException>(() => Load(_document with
        {
            Objects = [new("outside", "../lift.json", Vector2.Zero)]
        }));
    }

    [Fact]
    public void GameBootstrapPreservesTheManifestObjectsWhenAddingRegions()
    {
        using var manifest = Load(_document);
        using var bootstrapped = LevelBootstrap2D.LoadWorld(Path.Combine(_root, "world.json"));
        Assert.Equal(manifest.Objects.Select(o => (o.Id, o.Position)), bootstrapped.Objects.Select(o => (o.Id, o.Position)));
    }

    private WorldAsset2D Load(WorldFile2D.WorldDocument document)
    {
        var path = Path.Combine(_root, "world.json");
        File.WriteAllText(path, JsonSerializer.Serialize(document, GeometryJson2D.JsonOptions));
        return WorldFile2D.Load(path);
    }

    public void Dispose()
    {
        // The directory is created by this fixture; close its SQLite pool before removing it.
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
