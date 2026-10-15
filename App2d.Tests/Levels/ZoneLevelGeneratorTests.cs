using App2d.Core.Tiles;
using App2d.Levels;
using System.Text.Json;

namespace App2d.Tests.Levels;

public sealed class ZoneLevelGeneratorTests
{
    [Fact]
    public void RegenerationAssignsNewRevisionPreservesEditsAndRejectsOverlappingLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), "app2d-zone-generator-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "world.json");
        string[] tilesets = ["ground", "wall", "prehistoric"];
        try
        {
            using (var db = LevelDatabase2D.Open(Path.Combine(root, "original.db")))
                db.Save(new EditableTileMap2D(32, 64, 32, 32, new(0, -384), tilesets), 0);
            using (var db = LevelDatabase2D.Open(Path.Combine(root, "other.db")))
                db.Save(new EditableTileMap2D(32, 64, 32, 32, new(16384, -384), tilesets), 0);
            var generatedMap = new WorldFile2D.MapDocument("combat", "Combat", 2, "original.db")
            {
                Generation = new(1024, -384, new() { Beats = 6 })
            };
            var document = new WorldFile2D.WorldDocument(1,
                [new("other", "Other", 0, [new("other", "Other", 3, "other.db")]),
                 new("combat-zone", "Combat", 0, [generatedMap])])
            { StartMapId = "combat", StartAtArrival = true };
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            void Write(WorldFile2D.WorldDocument value) => File.WriteAllText(path, JsonSerializer.Serialize(value, options));
            Write(document);
            var first = ZoneLevelGenerator2D.Regenerate(path, "combat-zone", tilesets);
            using (var world = WorldFile2D.Load(path))
            {
                Assert.Equal("combat", world.Maps[0].Id);
                Assert.True(world.StartAtArrival);
                Assert.Equal(2, world.Maps[0].Identity);
                Assert.Equal(new System.Numerics.Vector2(1024, -384), world.Maps[0].TileMap.Origin);
                Assert.Equal(1200, world.PositionThings.First(t => t.TypeKey == "player-spawn").X);
            }
            using (var db = LevelDatabase2D.Open(first))
            {
                var map = db.Load();
                map.SetTileKind(4, 40, TileKind2D.Solid);
                db.Save(map, 12345);
            }
            var second = ZoneLevelGenerator2D.Regenerate(path, "combat-zone", tilesets);
            Assert.NotEqual(first, second);
            using (var previous = LevelDatabase2D.OpenRead(first)) Assert.True(previous.Load().IsSolid(4, 40));
            using (var next = LevelDatabase2D.OpenRead(second)) Assert.False(next.Load().IsSolid(4, 40));

            var current = JsonSerializer.Deserialize<WorldFile2D.WorldDocument>(File.ReadAllText(path), options)!;
            var combat = current.Zones.Single(z => z.Id == "combat-zone");
            var badMap = combat.Maps[0] with { Generation = generatedMap.Generation! with { OriginX = 16384 } };
            Write(current with { Zones = [.. current.Zones.Select(z => z.Id == combat.Id ? z with { Maps = [badMap] } : z)] });
            var before = File.ReadAllText(path);
            Assert.Throws<InvalidDataException>(() => ZoneLevelGenerator2D.Regenerate(path, "combat-zone", tilesets));
            Assert.Equal(before, File.ReadAllText(path));
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
