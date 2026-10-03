using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters;

/// <summary>Writes authored parts with typed geometry while accepting the older part fields on read.</summary>
internal static class PartAssetJson
{
    public static string Write<T>(T asset, IReadOnlyList<PuppetPart> parts, JsonSerializerOptions options)
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(asset, options))!.AsObject();
        var entries = root["parts"]!.AsArray();
        for (var i = 0; i < parts.Count; i++)
        {
            var entry = entries[i]!.AsObject();
            entry.Remove("geometry");
            entry.Remove("editorKind");
            entry.Remove("kind");
            entry.Remove("points");
            entry.Remove("fill");
            entry.Remove("outlineWidth");
            entry.Remove("material");
            entry["geometry"] = JsonNode.Parse(PartGeometry.Definition(parts[i]).ToGeometryJson());
            entry["material"] = JsonSerializer.SerializeToNode(parts[i].RenderMaterial, options);
            var editorKind = parts[i].Kind;
            if (parts[i].Geometry is not null && parts[i].EditorKind is null)
            {
                var inferred = parts[i] with { };
                PartGeometry.RestoreEditorFields(inferred);
                editorKind = inferred.Kind;
            }
            entry["editorKind"] = editorKind;
        }
        return root.ToJsonString(options);
    }

    public static void Restore(IEnumerable<PuppetPart> parts)
    {
        foreach (var part in parts)
            if (part.Geometry is not null) PartGeometry.RestoreEditorFields(part);
    }
}
