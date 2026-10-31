using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Serialization;

/// <summary>Tagged authoring form of spatial Béziers; evaluation uses the ordinary generic curve types.</summary>
public sealed class SpatialCurveJsonConverter : JsonConverter<ICurve<Vector3f, float>>
{
    public override ICurve<Vector3f, float> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var fields = root.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);
        Vector3f Point(string name) => fields.TryGetValue(name, out var field) ? field.Deserialize<Vector3f>(options)
            : throw new JsonException($"Missing spatial curve {name}.");
        var kind = fields.TryGetValue("kind", out var discriminator) ? discriminator.GetString() : null;
        string[] expected = kind == "quadratic-bezier" ? ["kind", "start", "control", "end"] : ["kind", "start", "control1", "control2", "end"];
        if (fields.Keys.Any(name => !expected.Contains(name, StringComparer.OrdinalIgnoreCase)))
            throw new JsonException("Unknown spatial curve member.");
        return kind switch
        {
            "quadratic-bezier" => new QuadraticBezier<Vector3f, float>(Point("start"), Point("control"), Point("end")),
            "cubic-bezier" => new CubicBezier<Vector3f, float>(Point("start"), Point("control1"), Point("control2"), Point("end")),
            _ => throw new JsonException("Unknown spatial curve kind.")
        };
    }

    public override void Write(Utf8JsonWriter writer, ICurve<Vector3f, float> value, JsonSerializerOptions options)
    {
        var kind = value switch
        {
            QuadraticBezier<Vector3f, float> => "quadratic-bezier",
            CubicBezier<Vector3f, float> => "cubic-bezier",
            _ => throw new JsonException("This spatial curve cannot be authored.")
        };
        var properties = JsonSerializer.SerializeToElement(value, value.GetType(), options);
        writer.WriteStartObject();
        writer.WriteString("kind", kind);
        foreach (var property in properties.EnumerateObject()) property.WriteTo(writer);
        writer.WriteEndObject();
    }
}
