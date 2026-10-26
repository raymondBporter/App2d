using App2d.CharacterSeeds.Diagnostics;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>Runs the rendering smokes and studies against the game's real assets and simulation. This project references the game; the game does not reference it.</summary>
internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterSeeds [--render-smoke output-directory | --face-smoke output-directory | --tileset-smoke output-directory (level | ground-id wall-id ...) | --vegetation-smoke output-directory | --object-study output-directory | --smoke-editor-ui output-directory | --giant-tree-study output-directory | --spell-study output-directory | --hitstop-study output-directory | --contact-study output-directory | --platform-study output-directory | --convert-variants authored-root base-id output-directory variant-id... | --convert-face authored-root model-id control-id output-directory part-id | --bake-face authored-root | --convert-face-payload authored-root model-id part-id instance-id asset-id socket-id output-directory | --convert-colliders authored-root output-directory entity-id... | --convert-behaviors authored-root output-directory entity-id... | --convert-models authored-root output-directory model-id...]";

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
            case ["--convert-face", var authoredRoot, var modelId, var control, var output, var partId]:
                // P7b: the face beneath one control of a model becomes a reusable animated face part, on a scratch tree with a report.
                var face = App2d.Core.Characters.Authored.FaceConversion.Convert(authoredRoot, modelId, control, FaceConversionPlans.For(modelId, partId), output);
                Console.WriteLine($"{face.PartId}: {face.Status}");
                foreach (var difference in face.Differences)
                {
                    Console.WriteLine("  DIFFERENCE: " + difference);
                }
                Console.WriteLine($"Report: {Path.Combine(output, "face-conversion-report.md")}");
                return face.Converted || face.Status.StartsWith("unchanged", StringComparison.Ordinal) || face.Status.StartsWith("kept", StringComparison.Ordinal) ? 0 : 2;
            case ["--bake-face", var authoredRoot]:
                // P7c: the retained procedural generator writes the Person face part; regenerate when the vocabulary changes.
                var baked = ProceduralFace.Bake();
                var bakedPath = Path.Combine(authoredRoot, "parts", baked.Id + ".json");
                baked.Save(bakedPath);
                Console.WriteLine($"{baked.Id}: {baked.Controls.Count} controls, {baked.Parts.Count} pieces, {baked.Clips.Count} clips, {baked.Expressions.Count} expressions -> {bakedPath}");
                return 0;
            case ["--convert-clips", var authoredRoot]:
                // P7c: every clip file below the current version whose legacy form carries nothing (no face tracks) rewrites at the
                // current version through the legacy reader; a file with face tracks is refused and reported, not rewritten.
                var rewritten = 0;
                foreach (var path in Directory.EnumerateFiles(Path.Combine(authoredRoot, "animations"), "*.json").Order(StringComparer.Ordinal))
                {
                    var text = File.ReadAllText(path);
                    if (!LegacySourceFields.Remaining(authoredRoot).Any(f => f.Kind == "clip" && Path.GetFullPath(Path.Combine(authoredRoot, f.Path)) == Path.GetFullPath(path)))
                    {
                        continue;
                    }

                    try
                    {
                        var clip = MotionClip.FromJson(text);
                        clip.Version = MotionClip.CurrentVersion;
                        clip.Save(path);
                        rewritten++;
                        Console.WriteLine($"clip {clip.Id}: converted to version {MotionClip.CurrentVersion}");
                    }
                    catch (InvalidDataException ex)
                    {
                        Console.WriteLine($"clip {Path.GetFileNameWithoutExtension(path)}: refused ({ex.Message})");
                    }
                }
                Console.WriteLine($"{rewritten} clip file(s) rewritten; {LegacySourceFields.Remaining(authoredRoot).Count(f => f.Kind == "clip")} clip file(s) still below version {MotionClip.CurrentVersion}.");
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
            case ["--convert-colliders", var authoredRoot, var output, .. var entityIds] when entityIds.Length > 0:
                // P8c: entities' legacy hit-window geometry, guard and hurt layout to explicit attached colliders at entity version 3, on a scratch tree with a report each.
                var exit = 0;
                foreach (var entityId in entityIds)
                {
                    var conversion = App2d.Core.Characters.Authored.ColliderConversion.ConvertFile(authoredRoot, entityId, output);
                    Console.WriteLine($"{conversion.EntityId}: {conversion.Status}");
                    foreach (var line in conversion.Report)
                    {
                        Console.WriteLine("  " + line);
                    }
                    foreach (var difference in conversion.Differences)
                    {
                        Console.WriteLine("  DIFFERENCE: " + difference);
                    }
                    exit = conversion.Converted || conversion.Status.StartsWith("unchanged", StringComparison.Ordinal) ? exit : 2;
                }
                return exit;
            case ["--convert-behaviors", var authoredRoot, var output, .. var entityIds] when entityIds.Length > 0:
                // P9a: entities' legacy health, mass, window damage/sound and projectile damage into typed behaviors at entity version 4, on a scratch tree with a report each.
                var behaviorsExit = 0;
                foreach (var entityId in entityIds)
                {
                    var behaviors = App2d.Core.Characters.Authored.BehaviorConversion.ConvertFile(authoredRoot, entityId, output);
                    Console.WriteLine($"{behaviors.EntityId}: {behaviors.Status}");
                    foreach (var line in behaviors.Report)
                    {
                        Console.WriteLine("  " + line);
                    }
                    behaviorsExit = behaviors.Converted ? behaviorsExit : 2;
                }
                return behaviorsExit;
            case ["--convert-models", var authoredRoot, var output, .. var modelIds] when modelIds.Length > 0:
                // P11a: a model bundle onto the canonical rig (point constraints to the bone solver, translation-only affine
                // nodes to point controls with fit tracks, the current version), on a scratch tree with a report each.
                var modelsExit = 0;
                foreach (var modelId in modelIds)
                {
                    var conversion = ModelConversion.Convert(authoredRoot, modelId, output, ModelConversionPlans.Naming(modelId));
                    Console.WriteLine($"{conversion.ModelId}: {conversion.Status}");
                    foreach (var line in conversion.Report)
                    {
                        Console.WriteLine("  " + line);
                    }
                    foreach (var file in conversion.Files)
                    {
                        Console.WriteLine($"  {file.Kind} {file.Id}: {file.Status}");
                    }
                    foreach (var difference in conversion.Differences)
                    {
                        Console.WriteLine("  DIFFERENCE: " + difference);
                    }
                    modelsExit = conversion.Converted || conversion.Status.StartsWith("unchanged", StringComparison.Ordinal) || conversion.Status.StartsWith("kept", StringComparison.Ordinal) ? modelsExit : 2;
                }
                return modelsExit;
            case ["--convert-face-payload", var authoredRoot, var modelId, var partId, var instanceId, var assetId, var socketId, var output]:
                // P7c: a model's procedural face payload, its clips' face tracks, its variants' face overrides and its looks' face words onto a face instance.
                var payload = App2d.Core.Characters.Authored.FaceConversion.ConvertPayload(authoredRoot, modelId, partId, instanceId, assetId, socketId, output);
                foreach (var file in payload.Files)
                {
                    Console.WriteLine($"{file.Kind} {file.Id}: {file.Status}");
                }
                foreach (var difference in payload.Differences)
                {
                    Console.WriteLine("  DIFFERENCE: " + difference);
                }
                Console.WriteLine($"Report: {payload.ReportPath}");
                return payload.Converted ? 0 : 2;
            default:
                Console.Error.WriteLine(Usage);
                return 1;
        }
    }
}
