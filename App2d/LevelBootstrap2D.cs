using App2d.Gameplay.Player;
using App2d.Gameplay.World;
using App2d.Levels;
using App2d.Tiles;

namespace App2d;

/// <summary>
/// Resolves the authored level file, baking it from the world generator the first time.
/// </summary>
/// <remarks>
/// The bake path is temporary scaffolding: <see cref="JumpableWorldGenerator2D"/> exists only
/// to produce a level to start editing from. Deleting the generator means deleting
/// <see cref="Bake"/> and the <c>level_rebake</c> command with it — <see cref="LoadOrBake"/>
/// becomes a plain load.
/// </remarks>
internal static class LevelBootstrap2D
{
    private const string LevelId = "cavern";

    /// <summary>
    /// Levels are durable authored content, so they live under <c>Assets/Static</c> and are
    /// read from there directly in Debug. <c>Assets/Runtime</c> is generated and disposable
    /// and must never be the only home for a hand-edited file.
    /// </summary>
    public static string CavernLevelPath { get; } = ResolveLevelPath();

    public static EditableTileMap2D LoadOrBake(TraversalMetrics2D traversal)
    {
        if (!File.Exists(CavernLevelPath))
            Bake(traversal);

        using var database = LevelDatabase2D.OpenRead(CavernLevelPath);
        return database.Load();
    }

    /// <summary>
    /// Opens the cavern level read-write for an editing session. The caller owns the
    /// returned database and must dispose it.
    /// </summary>
    public static LevelDatabase2D OpenForEditing() => LevelDatabase2D.Open(CavernLevelPath);

    public static void Bake(TraversalMetrics2D traversal)
    {
        var generator = new JumpableWorldGenerator2D(
            SideScrollerLevel2D.WorldSeed,
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal);
        var map = new EditableTileMap2D(
            SideScrollerLevel2D.WorldWidthTiles,
            SideScrollerLevel2D.WorldHeightTiles,
            traversal.TileSize,
            SideScrollerLevel2D.ChunkSizeTiles,
            SideScrollerLevel2D.WorldOrigin);
        map.Fill(generator.GetTileKind);

        using var database = LevelDatabase2D.Open(CavernLevelPath);
        database.Save(map, SideScrollerLevel2D.WorldSeed);
    }

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
