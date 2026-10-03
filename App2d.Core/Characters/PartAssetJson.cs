using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters;

/// <summary>Writes the typed drawing data for authored parts.</summary>
internal static class PartAssetJson
{
    private static readonly HashSet<string> OldPartFields = new(StringComparer.OrdinalIgnoreCase)
        { "kind", "editorKind", "points", "roundness", "topWidthScale", "fill", "outlineWidth" };

    public static void RequireTypedJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var parts = document.RootElement.EnumerateObject().FirstOrDefault(property =>
            property.Name.Equals("parts", StringComparison.OrdinalIgnoreCase)).Value;
        if (parts.ValueKind != JsonValueKind.Array) return;
        foreach (var part in parts.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Object) continue;
            foreach (var field in part.EnumerateObject())
                if (OldPartFields.Contains(field.Name))
                    throw new JsonException($"Part field '{field.Name}' is from the old drawing format; use geometry and material.");
        }
    }

    public static string Write<T>(T asset, IReadOnlyList<PuppetPart> parts, JsonSerializerOptions options)
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(asset, options))!.AsObject();
        var entries = root["parts"]!.AsArray();
        for (var i = 0; i < parts.Count; i++)
        {
            var entry = entries[i]!.AsObject();
            entry["geometry"] = JsonNode.Parse(PartGeometry.Definition(parts[i]).ToGeometryJson());
            entry["material"] = JsonSerializer.SerializeToNode(parts[i].RenderMaterial, options);
        }
        return root.ToJsonString(options);
    }

    public static void Restore(IEnumerable<PuppetPart> parts)
    {
        foreach (var part in parts)
            if (part.Geometry is not null) PartGeometry.RestoreEditorFields(part);
    }
}
