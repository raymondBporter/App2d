using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.IO;

/// <summary>
/// Serializes a <see cref="Vector2"/> as X/Y coordinates, using the serializer's property naming and number policies.
/// Register this converter in <see cref="JsonSerializerOptions.Converters"/> to use vectors in authored JSON directly.
/// </summary>
public sealed class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("Expected an X/Y coordinate object.");
        var xName = options.PropertyNamingPolicy?.ConvertName(nameof(Vector2.X)) ?? nameof(Vector2.X);
        var yName = options.PropertyNamingPolicy?.ConvertName(nameof(Vector2.Y)) ?? nameof(Vector2.Y);
        var comparison = options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var vector = Vector2.Zero;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) return vector;
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Expected a coordinate name.");
            var name = reader.GetString();
            if (!reader.Read()) throw new JsonException("Missing coordinate value.");
            if (string.Equals(name, xName, comparison)) vector.X = JsonSerializer.Deserialize<float>(ref reader, options);
            else if (string.Equals(name, yName, comparison)) vector.Y = JsonSerializer.Deserialize<float>(ref reader, options);
            else if (options.UnmappedMemberHandling == JsonUnmappedMemberHandling.Disallow)
                throw new JsonException($"Unknown coordinate '{name}'.");
            else reader.Skip();
        }
        throw new JsonException("Incomplete coordinate object.");
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(Vector2.X)) ?? nameof(Vector2.X));
        JsonSerializer.Serialize(writer, value.X, options);
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(Vector2.Y)) ?? nameof(Vector2.Y));
        JsonSerializer.Serialize(writer, value.Y, options);
        writer.WriteEndObject();
    }
}
