using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters;

/// <summary>Writes the typed drawing data for authored parts.</summary>
internal static class PartAssetJson
{
    public static string Write<T>(T asset, IReadOnlyList<PuppetPart> parts, JsonSerializerOptions options)
    {
        var root = JsonNode.Parse(JsonSerializer.Serialize(asset, options))!.AsObject();
        var entries = root["parts"]!.AsArray();
        for (var i = 0; i < parts.Count; i++)
        {
            var entry = entries[i]!.AsObject();
            entry["geometry"] = JsonNode.Parse((parts[i].Geometry ?? throw new InvalidDataException($"Part '{parts[i].Id}' needs geometry.")).ToGeometryJson());
            entry["material"] = JsonSerializer.SerializeToNode(parts[i].RenderMaterial, options);
        }
        return root.ToJsonString(options);
    }
}
