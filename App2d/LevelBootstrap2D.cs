using App2d.Core.Assets;
using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Json;
using App2d.Contracts.Player;
using App2d.Contracts.World;
using App2d.Gameplay.Characters;
using App2d.Gameplay.Simulation;
using App2d.Levels;
using App2d.Things;
using System.Text.Json;

namespace App2d;

/// <summary>
/// Resolves and loads the durable authored level file.
/// </summary>
internal static class LevelBootstrap2D
{
    private const string LevelId = "cavern";
    public static IReadOnlyList<string> TerrainTilesetIds { get; } =
    [
        "ink-medieval-ground",
        "ink-medieval-wall",
        "ink-prehistoric-ground",
        "ink-prehistoric-wall",
        "ink-wasteland-ground",
        "ink-wasteland-wall"
    ];

    /// <summary>
    /// Levels are durable authored content, so they live under <c>Assets/Static</c> and are
    /// read from there directly in Debug. <c>Assets/Runtime</c> is generated and disposable
    /// and must never be the only home for a hand-edited file.
    /// </summary>
    public static string CavernLevelPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "level.db");
    public static string WorldManifestPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "world.json");
    public static string CampaignManifestPath { get; } = Path.Combine(AssetPaths.Current.Levels, "ember-crossing", "world.json");
    public static string GiantTreesPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "giant-trees.json");

    public static WorldAsset2D Load() => LoadManifest(WorldManifestPath);

    public static WorldAsset2D LoadCampaign() => LoadManifest(CampaignManifestPath);

    public static SideScrollerSessionDefinition2D CreateSessionDefinition(WorldAsset2D world, SavedProgress2D? progress)
    {
        var movement = ShapeBounds2D.Calculate(GameCharacters.Hero.Movement);
        var height = MathF.Round(GameWorldUnits2D.AuthoredToWorld(movement.Height) / 4f) * 4f;
        var traversal = TraversalMetrics2D.FromGeometry(new(128), .9f,
            new(GameWorldUnits2D.AuthoredToWorld(movement.Width), height), GameWorldUnits2D.AuthoredToWorld(movement.Center.X));
        var things = world.PositionThings
            .Where(thing => ThingTypeRegistry2D.Require(thing.TypeKey).WorldKind is not null)
            .Select(ThingTypeRegistry2D.ToRuntime)
            .ToArray();
        return new(traversal, world.TileMap, things)
        {
            PlayerMaximumHealth = GameCharacters.HeroHealth,
            Spells = JsonSerializer.Deserialize<SpellTuning2D>(
                File.ReadAllText(Path.Combine(AssetPaths.Current.Runtime, "gameplay", "player-spells.json")),
                JsonProfiles.CaseInsensitive) ?? throw new InvalidDataException("Player spell tuning is missing."),
            Characters = true,
            Objects = world.Objects,
            Zones = world.Zones,
            GiantTrees = world.GiantTrees,
            SavedProgress = progress,
            GoalGuardianThingId = world.GoalGuardianThingId,
        };
    }

    public static WorldAsset2D LoadWorld(string manifest)
    {
        if (!File.Exists(manifest))
        {
            throw new FileNotFoundException("World manifest is missing.", manifest);
        }

        return LoadManifest(manifest);
    }

    private static WorldAsset2D LoadManifest(string manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        using var world = WorldFile2D.Load(manifest);
        var regions = WorldZoneFile2D.Load(Path.Combine(directory, "zones.json"));
        var regionIds = world.Zones.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
        return new(world.Maps, [.. world.Zones, .. regions.Where(z => !regionIds.Contains(z.Id))])
        {
            Root = world.Root,
            StartAtArrival = world.StartAtArrival,
            GoalGuardianThingId = world.GoalGuardianThingId,
        };
    }

    public static LevelDatabase2D OpenForEditing() => LevelDatabase2D.Open(RequireLevelPath());

    private static string RequireLevelPath() =>
        File.Exists(CavernLevelPath)
            ? CavernLevelPath
            : throw new FileNotFoundException(
                "The authored cavern level is missing. Restore Assets/Static/levels/cavern/level.db.",
                CavernLevelPath);
}
