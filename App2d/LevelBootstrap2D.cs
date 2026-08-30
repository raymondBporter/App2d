using App2d.Levels;
using App2d.Tiles;

namespace App2d;

/// <summary>
/// Resolves and loads the durable authored level file.
/// </summary>
internal static class LevelBootstrap2D
{
    private const string LevelId = "cavern";
    public static IReadOnlyList<string> TerrainTilesetIds { get; } =
        ["dark-cave", "mossy-cavern"];

    /// <summary>
    /// Levels are durable authored content, so they live under <c>Assets/Static</c> and are
    /// read from there directly in Debug. <c>Assets/Runtime</c> is generated and disposable
    /// and must never be the only home for a hand-edited file.
    /// </summary>
    public static string CavernLevelPath { get; } = ResolveLevelPath();

    public static LoadedLevel2D Load()
    {
        using var database = LevelDatabase2D.OpenRead(RequireLevelPath());
        return new LoadedLevel2D(
            database.Load(TerrainTilesetIds),
            database.LoadMovingPlatforms(),
            database.LoadPositionThings());
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

    private static string ResolveLevelPath()
    {
#if DEBUG
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var staticRoot = Path.Combine(directory.FullName, "Assets", "Static");
            if (Directory.Exists(staticRoot))
                return Path.Combine(staticRoot, "levels", LevelId, "level.db");
        }
#endif

        return Path.Combine(AssetPaths.Root, "levels", LevelId, "level.db");
    }
}

internal sealed record LoadedLevel2D(
    EditableTileMap2D TileMap,
    IReadOnlyList<MovingPlatformThingRecord2D> MovingPlatforms,
    IReadOnlyList<PositionThingRecord2D> PositionThings);
