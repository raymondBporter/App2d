using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Serialization;

/// <summary>
/// Serializes a <see cref="Vector2f"/> as X/Y coordinates, using the serializer's property naming and number policies.
/// Register this converter in <see cref="JsonSerializerOptions.Converters"/> to use vectors in authored JSON directly.
/// </summary>
public sealed class Vector2JsonConverter : JsonConverter<Vector2f>
{
    public override Vector2f Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected an X/Y coordinate object.");
        }

        var xName = options.PropertyNamingPolicy?.ConvertName(nameof(Vector2f.X)) ?? nameof(Vector2f.X);
        var yName = options.PropertyNamingPolicy?.ConvertName(nameof(Vector2f.Y)) ?? nameof(Vector2f.Y);
        var comparison = options.PropertyNameCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var x = 0f;
        var y = 0f;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new(x, y);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected a coordinate name.");
            }

            var name = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException("Missing coordinate value.");
            }

            if (string.Equals(name, xName, comparison))
            {
                x = JsonSerializer.Deserialize<float>(ref reader, options);
            }
            else if (string.Equals(name, yName, comparison))
            {
                y = JsonSerializer.Deserialize<float>(ref reader, options);
            }
            else if (options.UnmappedMemberHandling == JsonUnmappedMemberHandling.Disallow)
            {
                throw new JsonException($"Unknown coordinate '{name}'.");
            }
            else
            {
                reader.Skip();
            }
        }
        throw new JsonException("Incomplete coordinate object.");
    }

    public override void Write(Utf8JsonWriter writer, Vector2f value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(Vector2f.X)) ?? nameof(Vector2f.X));
        JsonSerializer.Serialize(writer, value.X, options);
        writer.WritePropertyName(options.PropertyNamingPolicy?.ConvertName(nameof(Vector2f.Y)) ?? nameof(Vector2f.Y));
        JsonSerializer.Serialize(writer, value.Y, options);
        writer.WriteEndObject();
    }
}
