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

var campaign = args.Length == 0;
var selectedWorld = args is ["--world", var worldPath] ? Path.GetFullPath(worldPath) : null;
if (!campaign && selectedWorld is null && args is not ["--playground"])
{
    Console.Error.WriteLine("Usage: App2d [--playground | --world <manifest>]");
    Environment.ExitCode = 2;
    return;
}

ApplicationConfiguration.Initialize();
var replay = false;
do
{
    var game = new SideScrollerGame(selectedWorld, campaign, replay);
    using var host = new GameHost(game);
    replay = false;
    game.ReplayRequested += () =>
    {
        if (!replay && Application.OpenForms[0] is { } window)
        {
            replay = true;
            window.BeginInvoke((Action)window.Close);
        }
    };
    host.Run();
}
while (replay);
