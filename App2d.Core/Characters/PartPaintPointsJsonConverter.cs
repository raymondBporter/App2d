using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

/// <summary>Reads older paint coordinates with Z=0; writes only the two artwork coordinates.</summary>
internal sealed class PartPaintPointsJsonConverter : JsonConverter<List<Vector2>>
{
    public PartPaintPointsJsonConverter() { }

    public override List<Vector2> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var stored = JsonSerializer.Deserialize<List<PuppetPoint>>(ref reader, options)
            ?? throw new JsonException("Paint needs an array of X/Y points.");
        var points = new List<Vector2>(stored.Count);
        foreach (var point in stored)
        {
            if (point.Z != 0) throw new JsonException("Paint coordinates are XY only.");
            points.Add(point.XY);
        }
        return points;
    }

    public override void Write(Utf8JsonWriter writer, List<Vector2> value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}
