using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>Offline conversion only. Normal Part loading never accepts either retired world-object format.</summary>
public static class WorldPartConversion
{
    public static Part Convert(string json)
    {
        var source = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Expected an object.");
        var physicsProp = source["format"]?.GetValue<string>() == "app2d-physics-prop";
        if ((!physicsProp && source["format"]?.GetValue<string>() != "app2d-object") || source["version"]?.GetValue<int>() != 1)
            throw new InvalidDataException("Expected an app2d-object or app2d-physics-prop version 1.");
        string[] fields = physicsProp
            ? ["format", "version", "id", "name", "shape", "fill", "body", "hitVelocityScale", "hitAngularKick"]
            : ["format", "version", "id", "name", "geometry", "collision", "material", "physics", "surface", "motion"];
        if (source.Any(p => !fields.Contains(p.Key, StringComparer.Ordinal)))
            throw new InvalidDataException("Unknown world-object field; conversion would discard data.");
        var shape = source[physicsProp ? "shape" : "geometry"];
        var material = physicsProp ? new JsonObject { ["fill"] = source["fill"]?.DeepClone() } : source["material"];
        var geometry = new JsonArray();
        if (shape is not null && material is not null)
        {
            var shapes = shape["kind"]?.GetValue<string>() == "composite" ? shape["parts"]!.AsArray() : new JsonArray(shape.DeepClone());
            foreach (var piece in shapes)
                geometry.Add(new JsonObject { ["id"] = $"shape-{geometry.Count + 1}", ["a"] = "origin", ["frame"] = "origin", ["geometry"] = piece?.DeepClone(), ["material"] = material.DeepClone() });
        }
        var result = new JsonObject
        {
            ["format"] = Part.FormatId, ["version"] = Part.CurrentVersion,
            ["id"] = source["id"]?.DeepClone(), ["name"] = source["name"]?.DeepClone(),
            ["usage"] = "world", ["geometry"] = geometry,
            ["physics"] = source[physicsProp ? "body" : "physics"]?.DeepClone()
        };
        foreach (var key in new[] { "collision", "surface", "motion" })
            if (source[key] is { } value) result[key] = value.DeepClone();
        if (geometry.Count == 0 && result["collision"] is null) result["collision"] = shape?.DeepClone();
        return Part.FromJson(result.ToJsonString());
    }
}
