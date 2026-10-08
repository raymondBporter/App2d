using App2d.Levels;

namespace App2d;

/// <summary>
/// Resolves and loads the durable authored level file.
/// </summary>
internal static class LevelBootstrap2D
{
    private const string LevelId = "cavern";
    public static IReadOnlyList<string> TerrainTilesetIds { get; } =
        ["ink-medieval-ground", "ink-medieval-wall", "ink-prehistoric-ground", "ink-prehistoric-wall",
         "ink-wasteland-ground", "ink-wasteland-wall"];

    /// <summary>
    /// Levels are durable authored content, so they live under <c>Assets/Static</c> and are
    /// read from there directly in Debug. <c>Assets/Runtime</c> is generated and disposable
    /// and must never be the only home for a hand-edited file.
    /// </summary>
    public static string CavernLevelPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "level.db");

    public static WorldAsset2D Load()
    {
        var directory = Path.GetDirectoryName(CavernLevelPath)!;
        var manifest = Path.Combine(directory, "world.json");
        if (File.Exists(manifest))
        {
            using var world = WorldFile2D.Load(manifest, TerrainTilesetIds);
            // Existing biome/audio regions remain available inside authored map containers.
            var regionIds = world.Zones.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
            var regions = WorldZoneFile2D.Load(Path.Combine(directory, "zones.json"));
            return new(world.Maps, [.. world.Zones, .. regions.Where(z => !regionIds.Contains(z.Id))]);
        }
        return new WorldAsset2D(
            [WorldFile2D.LoadMap(RequireLevelPath(), "cavern", "Cavern", "cavern", 0, TerrainTilesetIds)],
            WorldZoneFile2D.Load(Path.Combine(directory, "zones.json")));
    }
    /// <summary>
    /// Opens the cavern level read-write for an editing session. The caller owns the
    /// returned database and must dispose it.
    /// </summary>
    public static LevelDatabase2D OpenForEditing() => LevelDatabase2D.Open(RequireLevelPath());

    private static string RequireLevelPath() =>
        File.Exists(CavernLevelPath)
            ? CavernLevelPath
            : throw new FileNotFoundException(
                "The authored cavern level is missing. Restore Assets/Static/levels/cavern/level.db.",
                CavernLevelPath);
}
