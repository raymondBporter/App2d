using App2d.CharacterSeeds.Diagnostics;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>Runs the rendering smokes and studies against the game's real assets and simulation. This project references the game; the game does not reference it.</summary>
internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterSeeds [--render-smoke output-directory | --face-smoke output-directory | --tileset-smoke output-directory (level | ground-id wall-id ...) | --vegetation-smoke output-directory | --object-study output-directory | --smoke-editor-ui output-directory | --giant-tree-study output-directory | --spell-study output-directory | --hitstop-study output-directory | --contact-study output-directory | --platform-study output-directory | --bake-face authored-root | --publish-templates authored-root output-directory | --write-centaur authored-root [--force] | --extract-modules authored-root output-directory | --convert-formats authored-root output-directory | --rig-measure output-directory]";

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
            case ["--bake-face", var authoredRoot]:
                // P7c: the retained procedural generator writes the Person face part; regenerate when the vocabulary changes.
                var baked = ProceduralFace.Bake();
                var bakedPath = Path.Combine(authoredRoot, "parts", baked.Id + ".json");
                baked.Save(bakedPath);
                Console.WriteLine($"{baked.Id}: {baked.Controls.Count} controls, {baked.Parts.Count} pieces, {baked.Clips.Count} clips, {baked.Expressions.Count} expressions -> {bakedPath}");
                return 0;
            case ["--publish-templates", var authoredRoot, var output]:
                // P10a: the Person, quadruped, triceratops and pterosaur templates as version-2 resources with their models'
                // declared dimensions, on a scratch tree with a report each; the caller moves what it accepts into the tree.
                var published = TemplateResources.Publish(authoredRoot, output);
                foreach (var result in published)
                {
                    Console.WriteLine($"template {result.TemplateId} ({result.ModelId}): {result.Status}");
                    foreach (var line in result.Report)
                    {
                        Console.WriteLine("  " + line);
                    }
                    foreach (var difference in result.Differences)
                    {
                        Console.WriteLine("  DIFFERENCE: " + difference);
                    }
                }
                return published.All(r => r.Published) ? 0 : 2;
            case ["--write-centaur", var authoredRoot, .. var centaurFlags] when centaurFlags is [] or ["--force"]:
                // P10c: the centaur study composed from the shipped modules: its model, clips and template (P11b: editor-owned files are kept unless forced).
                var centaurReport = new SeedReport(centaurFlags.Length == 1);
                CentaurStudy.Write(authoredRoot, centaurReport);
                foreach (var line in centaurReport.Lines)
                {
                    Console.WriteLine(line);
                }
                Console.WriteLine($"centaur study under {authoredRoot}: {centaurReport}");
                return 0;
            case ["--extract-modules", var authoredRoot, var output]:
                // P10b: the Person's upper and lower body and the quadruped's lower body and head as rig modules, on a scratch tree with a report each.
                var extracted = ModulePlans.Extract(authoredRoot, output);
                foreach (var result in extracted)
                {
                    Console.WriteLine($"module {result.PartId}: {result.Status}");
                    foreach (var line in result.Report)
                    {
                        Console.WriteLine("  " + line);
                    }
                    foreach (var difference in result.Differences)
                    {
                        Console.WriteLine("  DIFFERENCE: " + difference);
                    }
                }
                return extracted.All(r => r.Converted) ? 0 : 2;
            case ["--rig-measure", var directory]:
                // P11c: the migration's closing measurements (load and compile time, allocation, registrations, candidates) as a report.
                MigrationMeasurements.Run(Path.GetFullPath(directory));
                return 0;
            case ["--convert-formats", var authoredRoot, var output]:
                // P11c: the authored tree's model, part and entity files onto the formats without retired members (model 12, part 2, entity 5), on a scratch tree with a report.
                var formats = FormatConversion.Convert(authoredRoot, output);
                foreach (var file in formats.Files)
                {
                    Console.WriteLine($"{file.Kind} {file.Id}: {file.Status}{(file.Dropped.Count == 0 ? "" : $" (dropped {string.Join(", ", file.Dropped)})")}");
                }
                foreach (var difference in formats.Differences)
                {
                    Console.WriteLine("  DIFFERENCE: " + difference);
                }
                Console.WriteLine($"Report: {formats.ReportPath}");
                return formats.Converted ? 0 : 2;
            default:
                Console.Error.WriteLine(Usage);
                return 1;
        }
    }
}
