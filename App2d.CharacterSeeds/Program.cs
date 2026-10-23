using App2d.CharacterSeeds.Diagnostics;

namespace App2d.CharacterSeeds;

/// <summary>Runs the rendering smokes and studies against the game's real assets and simulation. This project references the game; the game does not reference it.</summary>
internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterSeeds [--render-smoke output-directory | --face-smoke output-directory | --tileset-smoke output-directory (level | ground-id wall-id ...) | --vegetation-smoke output-directory | --object-study output-directory | --smoke-editor-ui output-directory | --giant-tree-study output-directory | --spell-study output-directory | --hitstop-study output-directory | --contact-study output-directory | --platform-study output-directory | --convert-variants authored-root base-id output-directory variant-id...]";

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
            case ["--convert-variants", var authoredRoot, var baseId, var output, .. var variantIds] when variantIds.Length > 0:
                // P6b: legacy variant files (versions 4/5) of one base's family to typed overrides, on a scratch tree with a report.
                var results = App2d.Core.Characters.Authored.VariantConversion.ConvertFamily(authoredRoot, baseId, variantIds, output);
                foreach (var result in results)
                {
                    Console.WriteLine($"{result.Id}: {result.Status}");
                    foreach (var difference in result.Differences)
                    {
                        Console.WriteLine("  DIFFERENCE: " + difference);
                    }
                }
                Console.WriteLine($"Report: {Path.Combine(output, "variant-conversion-report.md")}");
                return results.All(r => r.Differences.Count == 0) ? 0 : 2;
            default:
                Console.Error.WriteLine(Usage);
                return 1;
        }
    }
}
