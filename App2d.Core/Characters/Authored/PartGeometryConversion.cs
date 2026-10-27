using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>Explicit, temporary v1/v2-to-v3 conversion. Normal loading and saving never upgrade a file.</summary>
public static class PartGeometryConversion
{
    public static void Upgrade(JsonObject part)
    {
        if (part["format"]?.GetValue<string>() != Part.FormatId || part["version"]?.GetValue<int>() is not (1 or 2))
            throw new InvalidDataException("Expected an app2d-part version 1 or 2.");
        if (part.ContainsKey("geometry")) throw new InvalidDataException("Legacy part already contains geometry.");
        var geometry = (part["parts"] as JsonArray)?.DeepClone().AsArray() ?? new();
        var solids = part["solids"] as JsonArray ?? new();
        var ids = geometry.Select(e => e?["id"]?.GetValue<string>() ?? "").ToHashSet(StringComparer.Ordinal);
        var origin = PartLocation.Origin;
        if (solids.Count > 0 && part["payload"]?.GetValue<string>() == PartPayloads.Animated)
        {
            var controls = part["controls"]?.AsArray() ?? throw new InvalidDataException("Animated part needs controls.");
            origin = ModelAuthoring.UniqueId("mesh-origin", controls.Select(c => c!["id"]!.GetValue<string>()));
            controls.Add(new JsonObject { ["id"] = origin });
        }
        for (var i = 0; i < solids.Count; i++)
        {
            var mesh = solids[i]?.DeepClone().AsObject() ?? throw new InvalidDataException("Null solid.");
            var material = mesh["material"]?.DeepClone();
            mesh.Remove("material");
            var id = ModelAuthoring.UniqueId($"mesh-{i + 1}", ids);
            ids.Add(id);
            geometry.Add(new JsonObject { ["id"] = id, ["a"] = origin, ["frame"] = origin, ["material"] = material, ["mesh"] = mesh });
        }
        part.Remove("parts");
        part.Remove("solids");
        part["geometry"] = geometry;
        part["version"] = Part.CurrentVersion;
    }
}
