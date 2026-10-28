using App2d.Core.Json;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Levels;

public sealed record CombatLevelSettings2D
{
    public int Seed { get; init; } = 12345;
    public int Beats { get; init; } = 12;
    public float Difficulty { get; init; } = .3f;
    public float Exploration { get; init; } = .2f;
    public int JumpRiseTiles { get; init; } = 3;
    public byte TilesetIndex { get; init; } = 2;
    public IReadOnlyList<string> MeleeEnemies { get; init; } = ["club-caveman", "shield-defender", "baby-triceratops"];
    public IReadOnlyList<string> RangedEnemies { get; init; } = ["rock-thrower"];
}

public sealed record GeneratedPlacement2D(string TypeKey, string Name, float X, float Y);
public sealed record GeneratedBeat2D(int Index, string Recipe, int StartTile, int WidthTiles, bool HasOverlook);
public sealed record CombatLevelDraft2D(EditableTileMap2D Map,
    IReadOnlyList<GeneratedPlacement2D> Placements, IReadOnlyList<GeneratedBeat2D> Beats);

/// <summary>Offline beat grammar producing ordinary editable terrain and existing thing types.</summary>
public static class CombatLevelGenerator2D
{
    private const int BeatWidth = 40;
    private const int Floor = 12;
    private const float TileSize = 32;
    private static readonly HashSet<string> MeleeTypes =
        ["club-caveman", "shield-defender", "baby-triceratops", "shieldback", "boiler-brute", "green-dinosaur"];
    private static readonly HashSet<string> RangedTypes = ["rock-thrower", "rival"];

    public static CombatLevelSettings2D LoadSettings(string path) =>
        JsonSerializer.Deserialize<CombatLevelSettings2D>(File.ReadAllText(path), JsonProfiles.StrictCamelCase)
        ?? throw new InvalidDataException("Generator settings are null.");

    public static CombatLevelDraft2D Generate(CombatLevelSettings2D settings, IReadOnlyList<string> tilesets,
        System.Numerics.Vector2? origin = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tilesets);
        ArgGuard.InRange(settings.Beats, 6, 60);
        ArgGuard.InRange(settings.JumpRiseTiles, 1, 4);
        ArgGuard.InRange(settings.Difficulty, 0f, 1f);
        ArgGuard.InRange(settings.Exploration, 0f, 1f);
        ArgGuard.Index(settings.TilesetIndex, tilesets.Count);

        ValidateRoster(settings.MeleeEnemies, MeleeTypes);
        ValidateRoster(settings.RangedEnemies, RangedTypes);

        var random = new Random(settings.Seed);
        var explorationRandom = new Random(settings.Seed ^ 0x51A7);
        var map = new EditableTileMap2D(settings.Beats * BeatWidth, 64, TileSize, 32,
            origin ?? new(0, -Floor * TileSize), tilesets);
        var placements = new List<GeneratedPlacement2D>();
        var beats = new List<GeneratedBeat2D>();
        Fill(0, map.Width, 0, Floor);
        Place("player-spawn", "Arrival", 5, Floor, 80);
        Place("save-point", "Arrival checkpoint", 8, Floor, 64);
        var fights = 0;
        var sinceRelease = 0;
        for (var i = 0; i < settings.Beats; i++)
        {
            var start = i * BeatWidth;
            var recipe = i == 0 ? "arrival" : i == settings.Beats - 1 ? "exit" :
                i == settings.Beats - 2 ? "climax" :
                sinceRelease >= 2 ? "release" : i % 4 == 2 ? "traversal" :
                fights == 0 ? "introduce-melee" : fights == 1 ? "introduce-ranged" :
                settings.Difficulty >= .45f && random.NextDouble() < .5 ? "melee-fight" :
                settings.Difficulty >= .25f ? "combined-fight" : "practice-melee";
            var isFight = recipe is "climax" or "introduce-melee" or "introduce-ranged" or "combined-fight" or "melee-fight" or "practice-melee";
            var overlook = i > 0 && i < settings.Beats - 2 && explorationRandom.NextDouble() < settings.Exploration;

            if (recipe == "traversal")
            {
                // Connected steps with no pits: each rise stays inside the configured jump allowance.
                var rise = random.Next(1, settings.JumpRiseTiles + 1);
                Fill(start + 12, 5, Floor, rise);
                Fill(start + 17, 6, Floor, rise * 2);
                Fill(start + 23, 5, Floor, rise);
            }
            if (isFight)
            {
                var count = recipe.StartsWith("introduce-", StringComparison.Ordinal) ? 1 :
                    1 + (int)MathF.Floor(settings.Difficulty * 3);
                if (recipe == "combined-fight")
                {
                    count = Math.Max(2, count);
                }

                if (recipe == "climax" && settings.Difficulty >= .3f)
                {
                    count = Math.Max(2, count);
                }

                for (var enemy = 0; enemy < count; enemy++)
                {
                    var ranged = recipe == "introduce-ranged" || recipe is "combined-fight" or "climax" && enemy == count - 1 && count > 1;
                    var roster = ranged ? settings.RangedEnemies : settings.MeleeEnemies;
                    var type = recipe == "introduce-melee" ? roster[0] : roster[random.Next(roster.Count)];
                    var column = start + 15 + enemy * 6;
                    var surface = Floor;
                    if (ranged && recipe != "introduce-ranged")
                    {
                        Fill(column - 2, 5, Floor, settings.JumpRiseTiles);
                        surface += settings.JumpRiseTiles;
                    }
                    Place(type, $"{i + 1}: {recipe}", column, surface, 96);
                }
                fights++;
                sinceRelease++;
            }
            if (recipe == "release")
            {
                Place("save-point", $"Checkpoint {i + 1}", start + 20, Floor, 64);
                sinceRelease = 0;
            }
            if (overlook)
            {
                // An elevated optional detour above the main route; no progression item is required.
                for (var step = 1; step <= 3; step++)
                {
                    Platform(start + step * 5, 4, Floor + step * settings.JumpRiseTiles);
                }

                var top = Floor + 3 * settings.JumpRiseTiles;
                Platform(start + 20, 12, top);
                Place(settings.MeleeEnemies[0], $"Overlook guard {i + 1}", start + 25, top, 96);
                Place("save-point", $"Optional overlook {i + 1}", start + 30, top, 64);
            }
            beats.Add(new(i, recipe, start, BeatWidth, overlook));
        }
        Place("goal", "Level exit", map.Width - 6, Floor, 64);
        // End walls contain combat patrols while leaving the main route open.
        Fill(0, 2, Floor, 16);
        Fill(map.Width - 2, 2, Floor, 16);
        return new(map, placements.AsReadOnly(), beats.AsReadOnly());

        void Fill(int x, int width, int bottom, int height)
        {
            for (var column = x; column < x + width; column++)
            {
                for (var row = bottom; row < bottom + height; row++)
                {
                    map.SetTile(column, row, new(TileKind2D.Solid | TileKind2D.Grippable, settings.TilesetIndex));
                }
            }
        }
        void Platform(int x, int width, int row)
        {
            for (var column = x; column < x + width; column++)
            {
                map.SetTile(column, row, new(TileKind2D.OneWay, settings.TilesetIndex));
            }
        }
        void Place(string type, string name, int column, int surface, float clearance) =>
            placements.Add(new(type, name, map.Origin.X + (column + .5f) * TileSize, map.Origin.Y + surface * TileSize + clearance));
    }

    /// <summary>Writes to a new directory. Rerolls use another directory so editor changes survive.</summary>
    public static string Create(string directory, CombatLevelSettings2D settings, IReadOnlyList<string> tilesets,
        System.Numerics.Vector2? origin = null)
    {
        var draft = Generate(settings, tilesets, origin);
        var root = Path.GetFullPath(directory);
        if (Directory.Exists(root) || File.Exists(root))
        {
            throw new IOException($"Output already exists: {root}. Choose a fresh directory for the next draft.");
        }

        Directory.CreateDirectory(root);
        using (var db = LevelDatabase2D.Open(Path.Combine(root, "level.db")))
        {
            db.Save(draft.Map, unchecked((ulong)settings.Seed));
            var definitions = new Dictionary<string, long>();
            foreach (var placement in draft.Placements)
            {
                if (!definitions.TryGetValue(placement.TypeKey, out var definition))
                {
                    definition = db.CreateThingDefinition(placement.TypeKey, placement.TypeKey).DefinitionId;
                    definitions.Add(placement.TypeKey, definition);
                }
                db.CreatePositionThing(new(definition, placement.Name, true, placement.X, placement.Y));
            }
        }
        var manifest = Path.Combine(root, "world.json");
        Write("world.json", new WorldFile2D.WorldDocument(1,
            [new("generated", $"Generated {settings.Seed}", -1,
                [new("generated", $"Combat draft {settings.Seed}", 1, "level.db")])]));
        Write("zones.json", new
        {
            version = 1,
            zones = draft.Beats.Select(b => new
            {
                id = $"beat-{b.Index + 1:D2}",
                name = $"{b.Index + 1}: {b.Recipe}",
                minX = draft.Map.Origin.X + b.StartTile * TileSize,
                minY = draft.Map.WorldBounds.Min.Y,
                maxX = draft.Map.Origin.X + (b.StartTile + b.WidthTiles) * TileSize,
                maxY = draft.Map.WorldBounds.Max.Y,
                priority = 0
            })
        });
        Write("generation.json", new
        {
            settings,
            beats = draft.Beats,
            placements = draft.Placements
        });
        return manifest;

        void Write(string name, object value) => File.WriteAllText(Path.Combine(root, name),
            JsonSerializer.Serialize(value, value is WorldFile2D.WorldDocument ? JsonProfiles.World : JsonProfiles.StrictCamelCase));
    }

    private static void ValidateRoster(IReadOnlyList<string> roster, HashSet<string> allowed)
    {
        if (roster is null || roster.Count == 0 || roster.Any(t => !allowed.Contains(t)))
        {
            throw new ArgumentException($"Enemy roster must contain supported types: {string.Join(", ", allowed.Order())}.");
        }
    }
}
