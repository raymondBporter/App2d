using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Brings older entity files up to <see cref="EntityAsset.CurrentVersion"/> in memory before parsing, so files saved by
/// earlier studio builds still open. Version 1 stored hit, movement, projectile and guard geometry as width/height pairs;
/// version 2 stores a <see cref="Shapes.ShapeDefinition2D"/> for each.
/// </summary>
public static class EntityAssetUpgrade
{
    /// <summary>Upgrades an entity file's JSON to the current version. Current files are returned unchanged.</summary>
    /// <param name="json">The file text, any supported version.</param>
    /// <returns>JSON that parses as the current <see cref="EntityAsset"/>.</returns>
    /// <exception cref="InvalidDataException">The text is not a JSON object.</exception>
    public static string ToCurrent(string json)
    {
        JsonObject root;
        try { root = JsonNode.Parse(json) as JsonObject ?? throw new InvalidDataException("Empty entity file."); }
        catch (JsonException error) { throw new InvalidDataException("The entity file is not valid JSON.", error); }
        var version = Int(root, "version") ?? 1;
        if (version >= EntityAsset.CurrentVersion) return json;
        if (version == 1) UpgradeFrom1(root);
        root[Key(root, "version") ?? "version"] = EntityAsset.CurrentVersion;
        return root.ToJsonString();
    }

    private static void UpgradeFrom1(JsonObject root)
    {
        if (Object(root, "movement") is { } movement)
        {
            var width = Float(movement, "width") ?? .55f;
            var height = Float(movement, "height") ?? 1.9f;
            var offsetX = Float(movement, "offsetX") ?? 0f;
            Replace(movement, ["width", "height", "offsetX"], Rectangle(new(offsetX - width / 2f, 0f), new(offsetX + width / 2f, height)));
        }
        if (Object(root, "guard") is { } guard)
            Replace(guard, ["width", "height"], CenteredRectangle(Float(guard, "width") ?? .9f, Float(guard, "height") ?? 1.4f));
        if (Array(root, "actions") is { } actions)
        {
            foreach (var action in actions.OfType<JsonObject>())
            {
                if (Object(action, "projectile") is { } projectile)
                    Replace(projectile, ["width", "height"], CenteredRectangle(Float(projectile, "width") ?? .16f, Float(projectile, "height") ?? .08f));
                if (Array(action, "hits") is not { } hits) continue;
                foreach (var hit in hits.OfType<JsonObject>())
                {
                    var width = Float(hit, "width") ?? .3f;
                    var height = Float(hit, "height") ?? .3f;
                    JsonObject shape = (String(hit, "shape") ?? "box") switch
                    {
                        "circle" => Circle(width / 2f),
                        "capsule" => Capsule(width, height),
                        _ => CenteredRectangle(width, height)
                    };
                    Replace(hit, ["width", "height", "shape"], shape);
                }
            }
        }
    }

    private static JsonObject Rectangle(Vector2 min, Vector2 max) => new() { ["kind"] = "rectangle", ["min"] = Point(min), ["max"] = Point(max) };
    private static JsonObject CenteredRectangle(float width, float height) => Rectangle(new(-width / 2f, -height / 2f), new(width / 2f, height / 2f));
    private static JsonObject Circle(float radius) => new() { ["kind"] = "circle", ["center"] = Point(Vector2.Zero), ["radius"] = MathF.Round(radius, 5) };

    private static JsonObject Capsule(float length, float diameter)
    {
        var reach = MathF.Max(length - diameter, 0f) / 2f;
        return new() { ["kind"] = "capsule", ["start"] = Point(new(-reach, 0f)), ["end"] = Point(new(reach, 0f)), ["radius"] = MathF.Round(diameter / 2f, 5) };
    }

    // Old files held short decimals; halving and offsetting them in float leaves noise such as -0.90000004, so coordinates are rounded.
    private static JsonObject Point(Vector2 point) => new() { ["x"] = MathF.Round(point.X, 5), ["y"] = MathF.Round(point.Y, 5) };

    private static void Replace(JsonObject owner, string[] oldKeys, JsonObject shape)
    {
        foreach (var name in oldKeys)
            if (Key(owner, name) is { } key) owner.Remove(key);
        owner["shape"] = shape;
    }

    /// <summary>Authored files are read case-insensitively, so hand-edited casing still upgrades.</summary>
    private static string? Key(JsonObject owner, string name) => owner.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase));
    private static JsonNode? Node(JsonObject owner, string name) => Key(owner, name) is { } key ? owner[key] : null;
    private static JsonObject? Object(JsonObject owner, string name) => Node(owner, name) as JsonObject;
    private static JsonArray? Array(JsonObject owner, string name) => Node(owner, name) as JsonArray;
    private static float? Float(JsonObject owner, string name) => Node(owner, name) is JsonValue value && value.TryGetValue<float>(out var number) ? number : null;
    private static int? Int(JsonObject owner, string name) => Node(owner, name) is JsonValue value && value.TryGetValue<int>(out var number) ? number : null;
    private static string? String(JsonObject owner, string name) => Node(owner, name) is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
