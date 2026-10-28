using App2d.Core.Json;
using App2d.Core.Tiles;
using App2d.Levels;
using Microsoft.Data.Sqlite;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Gameplay.World;

public sealed class WorldFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-world-" + Guid.NewGuid().ToString("N"));
    private string Manifest => Path.Combine(_root, "world.json");

    private void CreateMap(string name, float x)
    {
        Directory.CreateDirectory(_root);
        using var db = LevelDatabase2D.Open(Path.Combine(_root, name));
        db.Save(new EditableTileMap2D(8, 8, 32, 4, new Vector2(x, 0)), 0);
        var definition = db.CreateThingDefinition("save-point", "Checkpoint");
        db.CreatePositionThing(new(definition.DefinitionId, null, true, x + 32, 32));
    }

    private void Write(params WorldFile2D.MapDocument[] maps)
    {
        var json = JsonSerializer.Serialize(new WorldFile2D.WorldDocument(1, [new("zone", "Zone", 0, maps)]), JsonProfiles.World);
        File.WriteAllText(Manifest, json);
    }

    [Fact]
    public void ShippedPlaygroundStartsInUpstreamMapAndRetainsGeneratedCombatZone()
    {
        using var world = WorldFile2D.Load(Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "world.json"),
            ["ink-medieval-ground", "ink-medieval-wall", "ink-prehistoric-ground", "ink-prehistoric-wall",
             "ink-wasteland-ground", "ink-wasteland-wall"]);
        Assert.Equal("main", world.Maps[0].Id);
        Assert.Equal("level.db", Path.GetFileName(world.Maps[0].DatabasePath));
        Assert.True(world.StartAtArrival);
        Assert.Contains(world.Maps.Single(m => m.Id == "sandbox-a").PositionThings, t => t.TypeKey == "rock-thrower");
        Assert.Contains(world.Maps[0].PositionThings, t => t.Enabled && t.TypeKey == "player-spawn");
        Assert.NotEmpty(world.GiantTrees);
        Assert.Equal(4, world.Maps.Count);
        Assert.Equal(4, world.Maps.Select(m => m.Identity).Distinct().Count());
        Assert.Equal("player-spawn", world.PositionThings.First(t => t.Enabled).TypeKey);
        // The migrated lifts and the arrival lift: every shipped object moves along a path.
        Assert.Equal(8, world.Objects.Count);
        Assert.All(world.Objects, o => Assert.NotNull(o.Asset.Motion));
    }

    [Fact]
    public void PlaygroundCreatesIndependentMapsAndKeepsOriginalIdentity()
    {
        Directory.CreateDirectory(_root);
        using (var db = LevelDatabase2D.Open(Path.Combine(_root, "level.db")))
        {
            var map = new EditableTileMap2D(8, 8, 32, 32);
            map.SetTileKind(2, 3, TileKind2D.Solid);
            db.Save(map, 0);
        }
        PlaygroundMaps2D.Create(_root, ["a", "b", "c", "d", "e"]);
        using var world = WorldFile2D.Load(Manifest);
        Assert.Equal(4, world.Maps.Count);
        Assert.Equal("hub", world.Maps[0].Id);
        Assert.Equal(0, world.Maps.Single(m => m.Id == "main").Identity);
        foreach (var map in world.Maps.Take(3))
        {
            Assert.True(map.TileMap.IsSolid(4, 19));
            Assert.False(map.TileMap.IsSolid(4, 20));
            Assert.Equal(new Vector2(map.TileMap.Origin.X + 512, 160), PlaygroundMaps2D.Arrival(map, 128, 2));
        }
        Assert.True(world.Maps.Single(m => m.Id == "main").TileMap.IsSolid(2, 3));
        Assert.Throws<IOException>(() => PlaygroundMaps2D.Create(_root, ["a"]));
    }

    [Fact]
    public void MapFilesKeepIndependentEditsAndStableWorldThingIds()
    {
        CreateMap("left.db", -256);
        CreateMap("right.db", 0);
        var left = new WorldFile2D.MapDocument("left", "Left", 0, "left.db");
        var right = new WorldFile2D.MapDocument("right", "Right", 7, "right.db");
        Write(left, right);
        using var world = WorldFile2D.Load(Manifest);
        Assert.Equal(2, world.Maps.Count);
        Assert.Equal(new Vector2(-256, 0), Assert.Single(world.Zones).Bounds.Min);
        Assert.Equal(new Vector2(256, 256), world.Zones[0].Bounds.Max);
        Assert.Equal(2, world.PositionThings.Select(t => t.ThingId).Distinct().Count());
        Assert.Equal(1, world.PositionThings[0].ThingId);
        var rightId = world.PositionThings[1].ThingId;
        world.Maps[1].TileMap.SetTileKind(0, 0, TileKind2D.Solid);
        using (var db = LevelDatabase2D.Open(world.Maps[1].DatabasePath))
        {
            db.SaveChunk(world.Maps[1].TileMap, new(0, 0));
        }

        Write(right, left);
        using var reloaded = WorldFile2D.Load(Manifest);
        Assert.Equal(rightId, reloaded.PositionThings[0].ThingId);
        Assert.True(reloaded.Maps[0].TileMap.IsSolid(0, 0));
        Assert.False(reloaded.Maps[1].TileMap.IsSolid(0, 0));
        Assert.True(reloaded.TileMap.IsSolid(8, 0));
    }

    [Theory]
    [InlineData("left", 1, "right.db")]
    [InlineData("right", 0, "right.db")]
    [InlineData("right", 1, "left.db")]
    [InlineData("right", 1, "../outside.db")]
    public void DuplicateIdentityOrFileAndEscapingPathsAreRejected(string id, int identity, string file)
    {
        CreateMap("left.db", 0);
        CreateMap("right.db", 256);
        Write(new("left", "Left", 0, "left.db"), new(id, "Right", identity, file));
        Assert.Throws<InvalidDataException>(() => WorldFile2D.Load(Manifest));
    }

    public void Dispose()
    {
        // Only this test's unique temporary databases are released and removed.
        if (!Directory.Exists(_root))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_root, "*.db"))
        {
            foreach (var mode in new[] { SqliteOpenMode.ReadOnly, SqliteOpenMode.ReadWriteCreate })
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = mode }.ToString());
                SqliteConnection.ClearPool(connection);
            }
        }

        Directory.Delete(_root, true);
    }
}
