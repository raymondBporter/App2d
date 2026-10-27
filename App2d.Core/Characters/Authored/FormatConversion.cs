using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// P11c: rewrites an authored tree's model, part and entity files onto the formats that carry no retired member (model 12,
/// part 3, entity 5), on a scratch tree with a report (plan rule 4). A file at the previous version is read with the retired
/// members skipped (<see cref="AuthoredJson.Tolerant"/>), every retired member it carried is listed by path, the asset is
/// validated at the new version and written under <paramref name="output"/>; nothing under <paramref name="root"/> is written.
/// A rerun reports every output unchanged; an output edited after an earlier run is kept. Verification: every converted model
/// resolves to the rest the source resolved to, and every converted entity compiles against the converted tree.
/// </summary>
public static class FormatConversion
{
    public sealed record FileResult(string Kind, string Id, string Status, IReadOnlyList<string> Dropped);

    public sealed record Result(IReadOnlyList<FileResult> Files, IReadOnlyList<string> Differences, string ReportPath)
    {
        public bool Converted => Differences.Count == 0 && Files.All(f => !f.Status.StartsWith("failed", StringComparison.Ordinal));
    }

    /// <summary>The members each kind carried before this conversion and carries no more, by JSON name.</summary>
    private static readonly Dictionary<string, HashSet<string>> Retired = new(StringComparer.Ordinal)
    {
        ["model"] = new(StringComparer.OrdinalIgnoreCase) { "hurtLayouts", "transform", "solver" },
        ["part"] = new(StringComparer.OrdinalIgnoreCase) { "transform", "solver" },
        ["entity"] = new(StringComparer.OrdinalIgnoreCase) { "health", "mass", "hurt", "socket", "prop", "point", "along", "offsetX", "offsetY", "shape", "damage", "sound" },
    };

    public static Result Convert(string root, string output)
    {
        var files = new List<FileResult>();
        var differences = new List<string>();
        var sourceRests = new Dictionary<string, IReadOnlyDictionary<string, System.Numerics.Vector3>>(StringComparer.Ordinal);
        if (Path.GetFullPath(output).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Equals(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The output must be a scratch tree, not the source tree.");
        }

        Directory.CreateDirectory(output);
        // The output is the source tree with the converted files replaced: everything else is copied once so the whole tree verifies.
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path);
            var folder = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            var target = Path.Combine(output, relative);
            if (folder is not ("models" or "parts" or "entities") && !File.Exists(target))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(path, target);
            }
        }
        foreach (var (folder, kind, previous, current) in new[] { ("models", "model", 11, CharacterModel.CurrentVersion), ("parts", "part", 1, Part.CurrentVersion), ("entities", "entity", 4, EntityAsset.CurrentVersion) })
        {
            var directory = Path.Combine(root, folder);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
            {
                var id = Path.GetFileNameWithoutExtension(path);
                var dropped = new List<string>();
                try
                {
                    var text = File.ReadAllText(path);
                    var node = JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException("not a JSON object");
                    var version = node["version"]?.GetValue<int>() ?? 0;
                    if (version != previous && version != current && !(kind == "part" && version == 2))
                    {
                        throw new InvalidDataException($"version {version} is not an input of this conversion (expects {previous} or {current})");
                    }

                    Scan(node, kind, "", dropped);
                    string fresh;
                    switch (kind)
                    {
                        case "model":
                        {
                            var model = JsonSerializer.Deserialize<CharacterModel>(text, AuthoredJson.Tolerant) ?? throw new InvalidDataException("empty file");
                            model.Version = CharacterModel.CurrentVersion;
                            model.Validate();
                            fresh = model.ToJson();
                            sourceRests[id] = ResolvedModel.Rig(model).Rest;
                            break;
                        }
                        case "part":
                        {
                            if (version < Part.CurrentVersion) PartGeometryConversion.Upgrade(node);
                            var part = JsonSerializer.Deserialize<Part>(node.ToJsonString(), AuthoredJson.Tolerant) ?? throw new InvalidDataException("empty file");
                            part.Version = Part.CurrentVersion;
                            part.Validate();
                            fresh = part.ToJson();
                            break;
                        }
                        default:
                        {
                            var entity = JsonSerializer.Deserialize<EntityAsset>(text, AuthoredJson.Tolerant) ?? throw new InvalidDataException("empty file");
                            entity.Version = EntityAsset.CurrentVersion;
                            entity.Validate();
                            fresh = entity.ToJson();
                            break;
                        }
                    }

                    files.Add(new(kind, id, Write(Path.Combine(output, folder, id + ".json"), fresh, current, dropped), dropped));
                }
                catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException)
                {
                    files.Add(new(kind, id, "failed: " + ex.Message, dropped));
                }
            }
        }

        // Verification over the converted tree as a whole: every model resolves to the source's rest, every entity compiles.
        try
        {
            var catalog = AuthoredCatalog.Load(output);
            foreach (var (id, rest) in sourceRests)
            {
                if (!catalog.Models.TryGetValue(id, out var model))
                {
                    differences.Add($"model '{id}': missing from the converted tree");
                    continue;
                }

                var converted = ResolvedModel.Rig(model).Rest;
                foreach (var (control, position) in rest)
                {
                    if (!converted.TryGetValue(control, out var now) || System.Numerics.Vector3.Distance(now, position) > 1e-6f)
                    {
                        differences.Add($"model '{id}' control '{control}': rest {position} -> {(converted.TryGetValue(control, out var v) ? v.ToString() : "missing")}");
                    }
                }
            }

            foreach (var file in files.Where(f => f.Kind == "entity" && !f.Status.StartsWith("failed", StringComparison.Ordinal)))
            {
                if (!catalog.Entities.ContainsKey(file.Id))
                {
                    differences.Add($"entity '{file.Id}': does not compile in the converted tree ({string.Join("; ", catalog.Errors.Where(e => e.Contains(file.Id)))})");
                }
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException or IOException)
        {
            differences.Add("the converted tree does not load: " + ex.Message);
        }

        var reportPath = Path.Combine(output, "format-conversion-report.md");
        var report = new StringBuilder();
        report.AppendLine("# Format conversion report (P11c: model 12, part 3, entity 5)");
        report.AppendLine();
        report.AppendLine($"Source: `{root}` (unchanged). Output: `{output}`. Date: {DateTime.Now:yyyy-MM-dd HH:mm}.");
        report.AppendLine();
        report.AppendLine("Part v3 uses one geometry array. Existing drawing-entry IDs, rig targets, layers and units are unchanged; old solids become mesh entries with stable mesh-N IDs (uniquified on collision), their original material and physical Z, attached to the root frame. Normal open/save does not convert legacy files.");
        report.AppendLine();
        report.AppendLine("| Kind | Id | Status | Retired members dropped |");
        report.AppendLine("| --- | --- | --- | --- |");
        foreach (var file in files)
        {
            report.AppendLine($"| {file.Kind} | {file.Id} | {file.Status} | {(file.Dropped.Count == 0 ? "none" : string.Join(", ", file.Dropped))} |");
        }

        report.AppendLine();
        report.AppendLine(differences.Count == 0 ? "Verified: every converted model resolves to the source's rest; every converted entity compiles." : "Differences:");
        foreach (var difference in differences)
        {
            report.AppendLine("- DIFFERENCE: " + difference);
        }

        File.WriteAllText(reportPath, report.ToString());
        return new(files, differences, reportPath);
    }

    private static void Scan(JsonNode? node, string kind, string path, List<string> dropped)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (name, value) in obj)
                {
                    var here = path.Length == 0 ? name : path + "." + name;
                    if (Retired[kind].Contains(name) && IsRetired(kind, path, name))
                    {
                        dropped.Add(here);
                        continue;
                    }

                    Scan(value, kind, here, dropped);
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Scan(array[i], kind, $"{path}[{i}]", dropped);
                }
                break;
        }
    }

    /// <summary>Whether a member of a retired name is the retired one at this path (the same names live on elsewhere: a collider's prop, shape and offsets, a part's shape).</summary>
    private static bool IsRetired(string kind, string path, string name) => kind switch
    {
        "model" => name.Equals("hurtLayouts", StringComparison.OrdinalIgnoreCase) ? path.Length == 0
            : name.Equals("transform", StringComparison.OrdinalIgnoreCase) ? path.StartsWith("controls[", StringComparison.Ordinal)
            : path.StartsWith("constraints[", StringComparison.Ordinal),
        "part" => name.Equals("transform", StringComparison.OrdinalIgnoreCase) ? path.StartsWith("controls[", StringComparison.Ordinal) : path.StartsWith("constraints[", StringComparison.Ordinal),
        _ => name.Equals("health", StringComparison.OrdinalIgnoreCase) || name.Equals("mass", StringComparison.OrdinalIgnoreCase) || name.Equals("hurt", StringComparison.OrdinalIgnoreCase) ? path.Length == 0
            : name.Equals("damage", StringComparison.OrdinalIgnoreCase) ? path.Contains(".hits[", StringComparison.Ordinal) || path.EndsWith(".projectile", StringComparison.Ordinal)
            : name.Equals("sound", StringComparison.OrdinalIgnoreCase) ? path.Contains(".hits[", StringComparison.Ordinal)
            : path.Contains(".hits[", StringComparison.Ordinal) || path == "guard" && name is "prop" or "shape",
    };

    private static string Write(string target, string fresh, int current, List<string> report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
        {
            var existing = File.ReadAllText(target);
            if (existing == fresh)
            {
                return "unchanged: the output already holds this conversion";
            }

            try
            {
                if ((JsonNode.Parse(existing) as JsonObject)?["version"]?.GetValue<int>() == current)
                {
                    return "kept: the output was edited after an earlier run (delete it to reconvert)";
                }

                report.Add("replacing an earlier-format output");
            }
            catch (JsonException ex)
            {
                report.Add($"replacing an unreadable earlier output: {ex.Message}");
            }
        }

        AuthoredAsset.Write(target, fresh);
        return "written";
    }
}
