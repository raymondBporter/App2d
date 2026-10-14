using App2d.Core.Tiles;
using System.Numerics;
using System.Text.Json;

namespace App2d.Levels;

/// <summary>Small authored starter maps; never regenerates or replaces existing content.</summary>
public static class PlaygroundMaps2D
{
    private static readonly JsonSerializerOptions JsonOptions = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public static Vector2 Arrival(WorldMapAsset2D map, float playerHeight, float groundProbe)
    {
        var spawn = map.PositionThings.FirstOrDefault(t => t.Enabled && t.TypeKey == "player-spawn");
        if (spawn is not null) return new(spawn.X, spawn.Y);
        var tiles = map.TileMap;
        var x = Math.Min(4, tiles.Width - 1);
        return tiles.Origin + new Vector2((x + .5f) * tiles.TileSize,
            TileGroundHeights2D.AtColumn(tiles, x) * tiles.TileSize + playerHeight / 2 + groundProbe);
    }

    public static void Create(string directory, IReadOnlyList<string> tilesets)
    {
        var files = new[] { "hub.db", "sandbox-a.db", "sandbox-b.db", "world.json" };
        if (files.Any(f => File.Exists(Path.Combine(directory, f))))
            throw new IOException("Playground files already exist; refusing to replace authored maps.");
        if (!File.Exists(Path.Combine(directory, "level.db")))
            throw new FileNotFoundException("The existing level.db must be present.");
        CreateMap("hub.db", -4608, 96, 0);
        CreateMap("sandbox-a.db", 22016, 128, 2);
        CreateMap("sandbox-b.db", 27136, 128, 4);
        var world = new WorldFile2D.WorldDocument(1,
        [
            new("hub", "Central hub", 0, [new("hub", "Central hub", 1, "hub.db")]),
            new("sandbox-a", "Sandbox A", 0, [new("sandbox-a", "Sandbox A", 2, "sandbox-a.db")]),
            new("sandbox-b", "Sandbox B", 0, [new("sandbox-b", "Sandbox B", 3, "sandbox-b.db")]),
            new("cavern-world", "Original playground", -1, [new("main", "Original playground", 0, "level.db")])
        ]);
        File.WriteAllText(Path.Combine(directory, "world.json"), JsonSerializer.Serialize(world, JsonOptions));

        void CreateMap(string file, float x, int width, byte tileset)
        {
            var map = new EditableTileMap2D(width, 96, 32, 32, new(x, -640), tilesets);
            for (var column = 0; column < width; column++)
                for (var row = 0; row < 20; row++)
                    map.SetTile(column, row, new(TileKind2D.Solid | TileKind2D.Grippable, tileset));
            var path = Path.Combine(directory, file);
            using (File.Open(path, FileMode.CreateNew, FileAccess.Write)) { }
            using var db = LevelDatabase2D.Open(path);
            db.Save(map, 0);
            var spawn = db.CreateThingDefinition("player-spawn", "Arrival");
            db.CreatePositionThing(new(spawn.DefinitionId, "Arrival", true, x + 512, 160));
        }
    }
}
