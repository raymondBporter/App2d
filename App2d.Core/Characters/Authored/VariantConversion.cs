using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Explicit conversion of legacy variant files (versions 4 and 5) to the typed recipe overrides of version 6 (character-system
/// migration, P6b; plan rules 3, 5 and 6). It runs on a scratch output tree, never on the source: each converted variant is
/// written to <c>&lt;output&gt;/variants/&lt;id&gt;.json</c> with a report in <c>&lt;output&gt;/variant-conversion-report.md</c>, the
/// conversion is verified against the base (every legacy rest pin reproduced, every other control, part and attachment
/// unchanged), and a rerun skips a source already at the current version and keeps an output that was edited after a
/// previous run. Moving the outputs into the authored tree is a separate, deliberate step.
/// </summary>
public static class VariantConversion
{
    /// <summary>One variant's conversion outcome.</summary>
    public sealed record Result(string Id, string Source, string? Output, string Status, IReadOnlyList<string> Report, IReadOnlyList<string> Differences)
    {
        public bool Converted => Output is not null && Differences.Count == 0;
    }

    /// <summary>
    /// Converts legacy absolute rest pins into parent-local setup overrides: parent first, each pin re-expressed beneath
    /// its parent's final frame with the earlier pins applied, so a pinned control lands where the legacy pin put it and its
    /// unpinned descendants follow it through the hierarchy exactly as the legacy evaluator carried them.
    /// </summary>
    public static void PinRest(CharacterModel model, ModelVariant variant, IReadOnlyDictionary<string, PuppetPoint> rest, Func<string, PartAsset?>? partOf = null)
    {
        if (rest.Count == 0)
        {
            return;
        }

        var resolved = ResolvedModel.From(model, variant, partOf);
        foreach (var control in resolved.Order)
        {
            if (!rest.TryGetValue(control.Id, out var pin))
            {
                continue;
            }

            try
            {
                ModelAuthoring.MoveRest(resolved, variant, control.Id, pin.XYZ, children: true);
            }
            catch (InvalidOperationException ex)
            {
                throw new InvalidDataException($"Variant '{variant.Id}' rest.{control.Id}: {ex.Message}", ex);
            }

            resolved = ResolvedModel.From(model, variant, partOf);
        }
        var unknown = rest.Keys.Where(id => model.Controls.All(c => c.Id != id)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidDataException($"Variant '{variant.Id}' rest: the base has no control(s) {string.Join(", ", unknown)}.");
        }
    }

    /// <summary>
    /// Converts the named variants of one base from the authored tree at <paramref name="root"/> into <paramref name="output"/>.
    /// Nothing under <paramref name="root"/> is written.
    /// </summary>
    public static IReadOnlyList<Result> ConvertFamily(string root, string baseId, IReadOnlyList<string> variantIds, string output, float tolerance = 1e-4f)
    {
        var catalog = AuthoredCatalog.Load(root);
        var model = catalog.Models.GetValueOrDefault(baseId) ?? throw new InvalidDataException($"No base model '{baseId}' under '{root}'.");
        Func<string, PartAsset?> partOf = catalog.Parts.GetValueOrDefault;
        Directory.CreateDirectory(Path.Combine(output, "variants"));
        var results = new List<Result>();
        foreach (var id in variantIds)
        {
            var source = Path.Combine(root, "variants", id + ".json");
            if (!File.Exists(source))
            {
                results.Add(new(id, source, null, "missing: no such variant file", [], [$"'{source}' does not exist"]));
                continue;
            }

            var json = File.ReadAllText(source);
            var target = Path.Combine(output, "variants", id + ".json");
            try
            {
                results.Add(Convert(id, source, json, target, model, partOf, tolerance));
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException or InvalidOperationException)
            {
                results.Add(new(id, source, null, "failed", [], [ex.Message]));
            }
        }
        File.WriteAllText(Path.Combine(output, "variant-conversion-report.md"), Report(root, baseId, results));
        return results;
    }

    private static Result Convert(string id, string source, string json, string target, CharacterModel model, Func<string, PartAsset?> partOf, float tolerance)
    {
        if (LegacyVariantReader.LegacyVersion(json) is not { } version)
        {
            var current = ModelVariant.FromJson(json);
            return new(id, source, null, $"already at version {current.Version}; nothing to convert", [], []);
        }

        var legacyRoot = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException($"'{source}': empty file.");
        var restKey = legacyRoot.FirstOrDefault(p => string.Equals(p.Key, "rest", StringComparison.OrdinalIgnoreCase)).Key;
        var rest = restKey is null ? new Dictionary<string, PuppetPoint>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize<Dictionary<string, PuppetPoint>>(legacyRoot[restKey]!.ToJsonString(), AuthoredJson.Options) ?? [];
        var (converted, report) = LegacyVariantReader.Convert(json, rest);
        var variant = ModelVariant.FromJson(converted);
        if (variant.Base != model.Id)
        {
            throw new InvalidDataException($"Variant '{id}' is based on '{variant.Base}', not '{model.Id}'.");
        }

        // What the legacy file meant: the field-wise overrides without its rest pins, then each pin landing where it said.
        var unpinned = ResolvedModel.From(model, variant, partOf);
        PinRest(model, variant, rest, partOf);
        var resolved = ResolvedModel.From(model, variant, partOf);
        var differences = new List<string>();
        var pinnedSubtree = new HashSet<string>(StringComparer.Ordinal);
        foreach (var control in resolved.Order)
        {
            if (rest.ContainsKey(control.Id) || control.Parent is { } parent && pinnedSubtree.Contains(parent))
            {
                pinnedSubtree.Add(control.Id);
            }
        }
        foreach (var control in resolved.Order)
        {
            if (rest.TryGetValue(control.Id, out var pin))
            {
                Compare(differences, $"control '{control.Id}' (pinned)", pin.XYZ, resolved.Rest[control.Id], tolerance);
            }
            else if (!pinnedSubtree.Contains(control.Id))
            {
                Compare(differences, $"control '{control.Id}'", unpinned.Rest[control.Id], resolved.Rest[control.Id], tolerance);
            }
        }
        for (var i = 0; i < resolved.Parts.Count; i++)
        {
            if (resolved.Parts[i] != unpinned.Parts[i])
            {
                differences.Add($"part '{resolved.Parts[i].Id}' differs from the field-wise conversion");
            }
        }
        for (var i = 0; i < resolved.Attachments.Count; i++)
        {
            if (resolved.Attachments[i].Instance != unpinned.Attachments[i].Instance)
            {
                differences.Add($"attachment '{resolved.Attachments[i].Id}' differs from the field-wise conversion");
            }
        }
        var lines = new List<string>(report) { $"{variant.Overrides.Count} typed override(s)" };
        if (differences.Count > 0)
        {
            return new(id, source, null, "not written: verification failed", lines, differences);
        }

        var fresh = variant.ToJson();
        if (File.Exists(target))
        {
            var existing = File.ReadAllText(target);
            if (existing == fresh)
            {
                return new(id, source, target, "unchanged: the output already holds this conversion", lines, []);
            }

            try
            {
                ModelVariant.FromJson(existing);
                return new(id, source, target, "kept: the output was edited after an earlier run (delete it to reconvert)", lines, []);
            }
            catch (Exception ex) when (ex is InvalidDataException or JsonException)
            {
                lines.Add($"replacing an unreadable earlier output: {ex.Message}");
            }
        }
        variant.Save(target);
        return new(id, source, target, $"converted: version {version} -> {ModelVariant.CurrentVersion}", lines, []);
    }

    private static void Compare(List<string> differences, string what, System.Numerics.Vector3 expected, System.Numerics.Vector3 actual, float tolerance)
    {
        var distance = System.Numerics.Vector3.Distance(expected, actual);
        if (distance > tolerance)
        {
            differences.Add($"{what}: expected ({expected.X:0.####}, {expected.Y:0.####}, {expected.Z:0.####}), converted ({actual.X:0.####}, {actual.Y:0.####}, {actual.Z:0.####}), {distance:0.######} apart");
        }
    }

    private static string Report(string root, string baseId, IReadOnlyList<Result> results)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Variant conversion report: family of '{baseId}'");
        text.AppendLine();
        text.AppendLine($"Source: `{root}` (unchanged). Target version: {ModelVariant.CurrentVersion}. Date: {DateTime.Now:yyyy-MM-dd HH:mm}.");
        text.AppendLine();
        text.AppendLine("| Variant | Status | Output |");
        text.AppendLine("| --- | --- | --- |");
        foreach (var result in results)
        {
            text.AppendLine($"| {result.Id} | {result.Status} | {(result.Output is null ? "-" : "`" + result.Output + "`")} |");
        }
        foreach (var result in results)
        {
            text.AppendLine();
            text.AppendLine($"## {result.Id}");
            text.AppendLine();
            foreach (var line in result.Report)
            {
                text.AppendLine("- " + line);
            }

            foreach (var difference in result.Differences)
            {
                text.AppendLine("- DIFFERENCE: " + difference);
            }
        }
        return text.ToString();
    }
}
