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

if (args.SequenceEqual(["--migrate-level"]))
{
    using var database = LevelBootstrap2D.OpenForEditing();
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

if (args is ["--object-study", var objectDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(objectDirectory, objectsOnly: true);
    return;
}

if (args is ["--smoke-editor-ui", var editorDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(editorDirectory, editorOnly: true);
    return;
}

if (args is ["--giant-tree-study", var giantTreeDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(giantTreeDirectory, giantTreesOnly: true);
    return;
}

if (args is ["--spell-study", var spellDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(spellDirectory, spellsOnly: true);
    return;
}

if (args is ["--hitstop-study", var timingDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(timingDirectory, timingOnly: true);
    return;
}

if (args is ["--contact-study", var contactDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(contactDirectory, contactOnly: true);
    return;
}

if (args is ["--face-smoke", var faceDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(faceDirectory, facesOnly: true);
    return;
}

if (args is ["--platform-study", var platformDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(platformDirectory, platformOnly: true);
    return;
}

if (args is ["--render-smoke", var outputDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(outputDirectory);
    return;
}

if (args is ["--tileset-smoke", var tilesetDirectory, .. var tilesetPairs] &&
    (tilesetPairs is ["level"] || tilesetPairs.Length is > 0 and var count && count % 2 == 0))
{
    App2d.Diagnostics.RenderingSmoke2D.Run(tilesetDirectory, tilesetPairs: tilesetPairs);
    return;
}

if (args is ["--vegetation-smoke", var vegetationDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(vegetationDirectory, vegetationOnly: true);
    return;
}

var selectedWorld = args is ["--world", var worldPath] ? Path.GetFullPath(worldPath) : null;
using var host = new GameHost(new SideScrollerGame(selectedWorld));
host.Run();
