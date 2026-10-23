using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// The named compatibility boundary for variant files below version 6 (plan rule 2; character-system migration, P6a/P6b).
/// A version-4 or version-5 file holds per-field override dictionaries (<c>rest</c> as absolute character-frame positions,
/// <c>parts</c> and <c>attachments</c> with one nullable member per property); this reader turns <c>parts</c> and
/// <c>attachments</c> into the typed <c>overrides</c> list in memory. The file on disk is not touched: a legacy file is
/// written at version 6 only by an explicit conversion (<see cref="VariantConversion"/>) or by an author saving it.
/// A legacy <c>rest</c> pin needs the base model to become a parent-local setup override, so the reader refuses it by name
/// and the conversion handles it with the base.
/// Remaining callers after P6b: <c>variants/club-caveman-build.json</c>, <c>rock-thrower-build.json</c> and
/// <c>shield-defender-build.json</c> (version 4, outside the converted Person family) and the conversion's own input.
/// Deletion condition: no variant file below version 6 remains in the authored tree, seeds or tests.
/// </summary>
public static class LegacyVariantReader
{
    public const int FirstVersion = 4, LastVersion = 5;

    /// <summary>The file's version when it is a legacy variant, else null.</summary>
    public static int? LegacyVersion(string json)
    {
        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }

        return root is not null && Version(root) is { } version && version is >= FirstVersion and <= LastVersion ? version : null;
    }

    private static int? Version(JsonObject root)
    {
        var key = root.FirstOrDefault(p => string.Equals(p.Key, "version", StringComparison.OrdinalIgnoreCase)).Key;
        return key is not null && root[key] is JsonValue value && value.TryGetValue(out int version) ? version : null;
    }

    /// <summary>Reads a legacy file at the current version; other input is returned unchanged.</summary>
    public static string Read(string json) => LegacyVersion(json) is null ? json : Convert(json).Json;

    /// <summary>
    /// Converts legacy JSON to the current version, reporting every member it translated. A non-empty <c>rest</c> is
    /// refused here and converted by <see cref="VariantConversion"/> with the base.
    /// </summary>
    public static (string Json, IReadOnlyList<string> Report) Convert(string json, IReadOnlyDictionary<string, PuppetPoint>? restPins = null)
    {
        var root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Empty variant file.");
        var version = Version(root) ?? throw new InvalidDataException("A variant file needs a version.");
        if (version is < FirstVersion or > LastVersion)
        {
            throw new InvalidDataException($"Variant version {version} is not a legacy version ({FirstVersion}..{LastVersion}).");
        }

        var id = Member(root, "id")?.ToString() ?? "?";
        var report = new List<string> { $"variant '{id}': version {version} -> {ModelVariant.CurrentVersion}" };
        var overrides = new JsonArray();
        var existing = Member(root, "overrides") as JsonArray;
        if (existing is not null)
        {
            foreach (var entry in existing.ToArray())
            {
                existing.Remove(entry);
                overrides.Add(entry);
            }
        }

        if (Member(root, "rest") is JsonObject rest && rest.Count > 0)
        {
            var pinned = rest.Select(p => p.Key).ToArray();
            if (restPins is null)
            {
                throw new InvalidDataException($"Variant '{id}': legacy rest pins ({string.Join(", ", pinned)}) need the base model to become setup overrides; convert the file with VariantConversion.");
            }

            foreach (var control in pinned)
            {
                if (!restPins.ContainsKey(control))
                {
                    throw new InvalidDataException($"Variant '{id}': no converted setup for rest pin '{control}'.");
                }
            }

            report.Add($"rest: {pinned.Length} pin(s) converted to parent-local setup overrides by the conversion ({string.Join(", ", pinned)})");
        }
        if (Member(root, "parts") is JsonObject parts)
        {
            foreach (var (part, node) in parts)
            {
                var change = node as JsonObject ?? throw new InvalidDataException($"Variant '{id}' parts.{part}: null override.");
                Translate(overrides, report, "part", part, change,
                [
                    ("scaleX", RigProperties.ScaleX, PropertyKinds.Number), ("scaleY", RigProperties.ScaleY, PropertyKinds.Number),
                    ("offsetX", RigProperties.OffsetX, PropertyKinds.Number), ("offsetY", RigProperties.OffsetY, PropertyKinds.Number),
                    ("material", RigProperties.Material, PropertyKinds.Material), ("face", RigProperties.Face, PropertyKinds.Selection),
                    ("faceX", RigProperties.FaceX, PropertyKinds.Number), ("hidden", RigProperties.Hidden, PropertyKinds.Flag),
                ]);
            }
        }
        if (Member(root, "attachments") is JsonObject attachments)
        {
            foreach (var (instance, node) in attachments)
            {
                var change = node as JsonObject ?? throw new InvalidDataException($"Variant '{id}' attachments.{instance}: null override.");
                Translate(overrides, report, "attachment", instance, change,
                [
                    ("asset", RigProperties.Asset, PropertyKinds.Selection), ("x", RigProperties.X, PropertyKinds.Number), ("y", RigProperties.Y, PropertyKinds.Number),
                    ("angle", RigProperties.Angle, PropertyKinds.Number), ("scale", RigProperties.Scale, PropertyKinds.Number), ("mirror", RigProperties.Mirror, PropertyKinds.Flag),
                    ("layer", RigProperties.Layer, PropertyKinds.Number), ("fitX", RigProperties.FitX, PropertyKinds.Number), ("fitY", RigProperties.FitY, PropertyKinds.Number),
                    ("material", RigProperties.Material, PropertyKinds.Material), ("hidden", RigProperties.Hidden, PropertyKinds.Flag),
                ]);
            }
        }
        foreach (var member in new[] { "rest", "parts", "attachments" })
        {
            if (Key(root, member) is { } key)
            {
                root.Remove(key);
            }
        }

        root[Key(root, "version")!] = ModelVariant.CurrentVersion;
        root["overrides"] = overrides;
        return (root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), report);
    }

    private static void Translate(JsonArray overrides, List<string> report, string ownerKind, string owner, JsonObject change, (string Member, string Property, string Kind)[] members)
    {
        foreach (var (member, property, kind) in members)
        {
            if (Member(change, member) is not { } value)
            {
                continue;
            }

            var entry = new JsonObject { ["target"] = new JsonObject { [ownerKind] = owner, ["property"] = property } };
            entry[kind switch { PropertyKinds.Number => "value", PropertyKinds.Flag => "flag", PropertyKinds.Selection => "selection", _ => "material" }] = value.DeepClone();
            overrides.Add(entry);
            report.Add($"{ownerKind} '{owner}' {member} -> {property} ({kind})");
        }
        var unknown = change.Select(p => p.Key).Where(k => !members.Any(m => string.Equals(m.Member, k, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidDataException($"{ownerKind} '{owner}': unknown legacy override member(s) {string.Join(", ", unknown)}.");
        }
    }

    private static string? Key(JsonObject node, string name) => node.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Key;
    private static JsonNode? Member(JsonObject node, string name) => Key(node, name) is { } key ? node[key] : null;
}
