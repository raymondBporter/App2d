using App2d.Levels;
using App2d.Core.Tiles;
using Microsoft.Data.Sqlite;
using System.Numerics;
using System.Text.Json;
using Xunit;

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

    private void Write(params WorldFile2D.MapDocument[] maps) => File.WriteAllText(Manifest,
        JsonSerializer.Serialize(new WorldFile2D.WorldDocument(1, [new("zone", "Zone", 0, maps)]),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

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
            db.SaveChunk(world.Maps[1].TileMap, new(0, 0));
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
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.EnumerateFiles(_root, "*.db"))
            foreach (var mode in new[] { SqliteOpenMode.ReadOnly, SqliteOpenMode.ReadWriteCreate })
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = file, Mode = mode }.ToString());
                SqliteConnection.ClearPool(connection);
            }
        Directory.Delete(_root, true);
    }
}
