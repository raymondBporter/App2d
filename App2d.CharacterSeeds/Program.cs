using App2d.CharacterSeeds.Diagnostics;

namespace App2d.CharacterSeeds;

/// <summary>Runs the rendering smokes and studies against the game's real assets and simulation. This project references the game; the game does not reference it.</summary>
internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterSeeds [--render-smoke output-directory | --face-smoke output-directory | --tileset-smoke output-directory (level | ground-id wall-id ...) | --vegetation-smoke output-directory | --object-study output-directory | --smoke-editor-ui output-directory | --giant-tree-study output-directory | --spell-study output-directory | --hitstop-study output-directory | --contact-study output-directory | --platform-study output-directory]";

    [STAThread]
    private static int Main(string[] args)
    {
        // Not ApplicationConfiguration.Initialize(): WinForms generates that type for every executable that calls it, and
        // this project can see the game's copy, so calling it here would compile against two definitions of the same name.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        switch (args)
        {
            case ["--render-smoke", var directory]:
                RenderingSmoke2D.Run(directory);
                return 0;
            case ["--face-smoke", var directory]:
                RenderingSmoke2D.Run(directory, facesOnly: true);
                return 0;
            case ["--tileset-smoke", var directory, .. var tilesetPairs] when tilesetPairs is ["level"] || (tilesetPairs.Length > 0 && tilesetPairs.Length % 2 == 0):
                RenderingSmoke2D.Run(directory, tilesetPairs: tilesetPairs);
                return 0;
            case ["--vegetation-smoke", var directory]:
                RenderingSmoke2D.Run(directory, vegetationOnly: true);
                return 0;
            case ["--object-study", var directory]:
                RenderingSmoke2D.Run(directory, objectsOnly: true);
                return 0;
            case ["--smoke-editor-ui", var directory]:
                RenderingSmoke2D.Run(directory, editorOnly: true);
                return 0;
            case ["--giant-tree-study", var directory]:
                RenderingSmoke2D.Run(directory, giantTreesOnly: true);
                return 0;
            case ["--spell-study", var directory]:
                RenderingSmoke2D.Run(directory, spellsOnly: true);
                return 0;
            case ["--hitstop-study", var directory]:
                RenderingSmoke2D.Run(directory, timingOnly: true);
                return 0;
            case ["--contact-study", var directory]:
                RenderingSmoke2D.Run(directory, contactOnly: true);
                return 0;
            case ["--platform-study", var directory]:
                RenderingSmoke2D.Run(directory, platformOnly: true);
                return 0;
            default:
                Console.Error.WriteLine(Usage);
                return 1;
        }
    }
}
