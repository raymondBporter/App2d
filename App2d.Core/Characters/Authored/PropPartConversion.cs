using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>Explicit, offline conversion of retired prop files. Normal part loading never invokes this.</summary>
public static class PropPartConversion
{
    public static Part Convert(string json)
    {
        var source = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Expected a prop object.");
        if (source["format"]?.GetValue<string>() != "app2d-prop" || source["version"]?.GetValue<int>() != 3)
            throw new InvalidDataException("Prop conversion expects app2d-prop version 3.");
        string[] known = ["format", "version", "id", "name", "usage", "attachment", "backView", "ink", "lineWidth", "scale",
            "grip", "tip", "secondGrip", "muzzle", "shapes", "solids"];
        var unknown = source.Select(p => p.Key).Except(known, StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0) throw new InvalidDataException("Unknown prop fields: " + string.Join(", ", unknown));
        var result = new JsonObject { ["format"] = Part.FormatId, ["version"] = Part.CurrentVersion };
        foreach (var field in new[] { "id", "name", "usage", "attachment", "backView", "ink", "lineWidth", "scale" })
            if (source[field] is { } value) result[field] = value.DeepClone();
        result["usage"] ??= "prop";
        result["lineWidth"] ??= .035f;
        result["pivot"] = EquipmentPoints.Grip;
        var locations = new JsonArray();
        foreach (var (field, id) in new[] { ("grip", EquipmentPoints.Grip), ("tip", EquipmentPoints.Tip), ("secondGrip", EquipmentPoints.SecondGrip), ("muzzle", EquipmentPoints.Muzzle) })
        {
            var point = source[field]?.DeepClone()?.AsObject();
            if (point is null && field is not ("grip" or "tip")) continue;
            point ??= field == "tip" ? new JsonObject { ["x"] = 1 } : new JsonObject();
            point["id"] = id;
            locations.Add(point);
        }
        result["locations"] = locations;
        var geometry = new JsonArray();
        foreach (var solid in source["solids"]?.AsArray() ?? [])
        {
            var mesh = solid?.DeepClone()?.AsObject() ?? throw new InvalidDataException("Null prop mesh.");
            var material = mesh["material"]?.DeepClone();
            mesh.Remove("material");
            geometry.Add(new JsonObject { ["id"] = $"mesh-{geometry.Count + 1}", ["a"] = PartLocation.Origin,
                ["frame"] = PartLocation.Origin, ["mesh"] = mesh, ["material"] = material });
        }
        var shapeIndex = 0;
        foreach (var shape in source["shapes"]?.AsArray() ?? [])
        {
            var entry = shape?.DeepClone()?.AsObject() ?? throw new InvalidDataException("Null prop geometry.");
            entry["id"] = $"shape-{++shapeIndex}";
            entry["a"] = PartLocation.Origin;
            entry["frame"] = PartLocation.Origin;
            geometry.Add(entry);
        }
        result["geometry"] = geometry;
        return Part.FromJson(result.ToJsonString());
    }
}
