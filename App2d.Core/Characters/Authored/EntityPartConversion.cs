using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>Explicit offline migration. Loading a Part never silently upgrades an entity file.</summary>
public static class EntityPartConversion
{
    public static Part Convert(string json)
    {
        var source = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Expected an entity object.");
        if (source["format"]?.GetValue<string>() != "app2d-entity" || source["version"]?.GetValue<int>() != 5)
            throw new InvalidDataException("Entity conversion expects app2d-entity version 5.");
        string[] known = ["format", "version", "id", "name", "model", "motionSet", "guard", "roles", "controller", "behaviors", "movement", "colliders", "equipment", "actions"];
        var unknown = source.Select(p => p.Key).Except(known, StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0) throw new InvalidDataException("Unknown entity fields: " + string.Join(", ", unknown));
        var result = new JsonObject { ["format"] = Part.FormatId, ["version"] = Part.CurrentVersion, ["usage"] = "character" };
        foreach (var key in new[] { "id", "name", "model", "equipment" })
            if (source[key] is { } value) result[key] = value.DeepClone();
        var behavior = source["behaviors"]?.DeepClone()?.AsObject() ?? new JsonObject();
        foreach (var key in new[] { "motionSet", "roles", "controller", "actions" })
            if (source[key] is { } value) behavior[key] = value.DeepClone();
        // Legacy entities always received damage, including when their fields were omitted.
        behavior["damage"] ??= new JsonObject();
        if (behavior["damage"] is JsonObject damage && damage.Remove("mass", out var mass)) damage["knockbackResistance"] = mass;
        if (source["guard"] is JsonObject guard)
        {
            var settings = behavior["guard"] as JsonObject ?? new JsonObject();
            settings["collider"] = guard["collider"]?.DeepClone();
            behavior["guard"] = settings;
        }
        else behavior.Remove("guard");
        var geometry = new JsonArray();
        var roles = new JsonObject();
        var colliders = source["colliders"]?.AsArray() ?? [];
        var movementId = "movement";
        while (colliders.Any(c => c?["id"]?.GetValue<string>() == movementId)) movementId += "-body";
        var movement = source["movement"]?.AsObject();
        if (movement?.Any(p => p.Key != "shape") == true) throw new InvalidDataException("Unknown movement fields.");
        geometry.Add(new JsonObject
        {
            ["id"] = movementId,
            ["a"] = PartLocation.Origin,
            ["geometry"] = movement?["shape"]?.DeepClone() ?? System.Text.Json.JsonSerializer.SerializeToNode(EntityAuthoring.MovementBox(.55f, 1.9f), AuthoredJson.Options),
            ["collision"] = new JsonObject()
        });
        foreach (var item in colliders)
        {
            var entry = item?.DeepClone()?.AsObject() ?? throw new InvalidDataException("Null collider.");
            var id = entry["id"]?.GetValue<string>() ?? throw new InvalidDataException("Collider needs an id.");
            roles.Add(id, entry["use"]?.DeepClone() ?? JsonValue.Create(ColliderUses.Hurt));
            entry.Remove("use");
            entry.Remove("shape", out var shape);
            entry["geometry"] = shape ?? System.Text.Json.JsonSerializer.SerializeToNode(App2d.Core.Shapes.Rectangle2D.Builder.FromSize(.3f, .3f), AuthoredJson.Options);
            entry.Remove("control", out var control); entry["a"] = control ?? JsonValue.Create(PartLocation.Origin);
            entry.Remove("toward", out var toward); if (toward is not null) entry["b"] = toward;
            if (control is not null && entry["frame"] is null) entry["frame"] = control.DeepClone();
            if (control is not null) entry.Remove("along"); // Only the old actor/socket/prop path applied Along.
            if (entry["prop"] is not null && entry["point"] is null) entry["point"] = EquipmentPoints.Tip;
            // The retired socket/prop path never applied these fields. Preserve its evaluated placement,
            // while new geometry uses the ordinary local offset consistently for drawing and collision.
            if (entry["socket"] is not null || entry["prop"] is not null)
            {
                entry.Remove("offsetX");
                entry.Remove("offsetY");
            }
            var collision = new JsonObject();
            foreach (var key in new[] { "enabled", "mask" })
                if (entry.Remove(key, out var value)) collision[key] = value;
            entry["collision"] = collision;
            geometry.Add(entry);
        }
        behavior["movement"] = movementId;
        behavior["colliders"] = roles;
        result["behavior"] = behavior;
        result["geometry"] = geometry;
        return Part.FromJson(result.ToJsonString());
    }
}
