using App2d.Core.Assets;
using App2d.Core.Tiles;
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

    public static LoadedLevel2D Load()
    {
        using var database = LevelDatabase2D.OpenRead(RequireLevelPath());
        return new LoadedLevel2D(
            database.Load(TerrainTilesetIds),
            database.LoadMovingPlatforms(),
            database.LoadPositionThings(),
            WorldZoneFile2D.Load(Path.Combine(Path.GetDirectoryName(CavernLevelPath)!, "zones.json")));
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

internal sealed record LoadedLevel2D(
    EditableTileMap2D TileMap,
    IReadOnlyList<MovingPlatformThingRecord2D> MovingPlatforms,
    IReadOnlyList<PositionThingRecord2D> PositionThings,
    System.Collections.Immutable.ImmutableArray<Contracts.World.WorldZone2D> Zones);
