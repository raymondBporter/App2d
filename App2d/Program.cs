using App2d;
using App2d.Core.Hosting;

if (args is ["--generate-zone", var zoneId])
{
    var map = App2d.Levels.ZoneLevelGenerator2D.Regenerate(LevelBootstrap2D.WorldManifestPath, zoneId, LevelBootstrap2D.TerrainTilesetIds);
    Console.WriteLine($"Assigned generated map: {map}");
    return;
}

if (args is ["--generate-level", var generatedDirectory, var settingsPath])
{
    var settings = App2d.Levels.CombatLevelGenerator2D.LoadSettings(settingsPath);
    var manifest = App2d.Levels.CombatLevelGenerator2D.Create(generatedDirectory, settings, LevelBootstrap2D.TerrainTilesetIds);
    Console.WriteLine($"Generated world: {manifest}");
    return;
}

if (args is ["--create-playground", var playgroundDirectory])
{
    App2d.Levels.PlaygroundMaps2D.Create(playgroundDirectory, LevelBootstrap2D.TerrainTilesetIds);
    return;
}

// Brings every map of a world to the current level format and moves a manifest's objects into its maps.
if (args is ["--migrate-level"] or ["--migrate-level", _])
{
    var worldManifest = args is ["--migrate-level", var worldFile] ? Path.GetFullPath(worldFile) : LevelBootstrap2D.WorldManifestPath;
    foreach (var migrated in LevelBootstrap2D.MigrateWorld(worldManifest))
    {
        Console.WriteLine($"Migrated map: {migrated}");
    }

    return;
}

if (args is ["--create-map", var mapPath, var width, var height, var originX, var originY])
{
    var culture = System.Globalization.CultureInfo.InvariantCulture;
    var map = new App2d.Core.Tiles.EditableTileMap2D(int.Parse(width, culture), int.Parse(height, culture), 32, 32,
        new(float.Parse(originX, culture), float.Parse(originY, culture)), LevelBootstrap2D.TerrainTilesetIds);
    // CreateNew prevents accidentally replacing authored terrain.
    using (File.Open(mapPath, FileMode.CreateNew, FileAccess.Write))
    {
    }
    using var database = App2d.Levels.LevelDatabase2D.Open(mapPath);
    database.Save(map, 0);
    Console.WriteLine($"Created map: {Path.GetFullPath(mapPath)}");
    return;
}

ApplicationConfiguration.Initialize();

// Rendering smokes and studies run from App2d.CharacterSeeds, which references the game; the game does not reference it.
var selectedWorld = args is ["--world", var worldPath] ? Path.GetFullPath(worldPath) : null;
using var host = new GameHost(new SideScrollerGame(selectedWorld));
host.Run();
