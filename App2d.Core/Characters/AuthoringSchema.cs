using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace App2d.Core.Characters;

/// <summary>Closed-vocabulary checks shared by the authored validators.</summary>
public static class EntityVocabulary
{
    public static void Require(string? value, IReadOnlyList<string> allowed, string field)
    {
        if (value is null || !allowed.Contains(value, StringComparer.Ordinal))
            throw new InvalidDataException($"{field} must be one of {string.Join(", ", allowed)}; found '{value}'.");
    }
}

/// <summary>An authored value's accepted range. The soft range is the editor's suggested slider span; files may hold any value within the hard range.</summary>
public readonly record struct Limit(float Min, float Max, float SoftMin, float SoftMax)
{
    public Limit(float min, float max) : this(min, max, min, max) { }
    public Limit Soft(float min, float max) => this with { SoftMin = min, SoftMax = max };
    public float Clamp(float value) => Math.Clamp(value, Min, Max);
    public void Check(float value, string field)
    {
        if (!float.IsFinite(value) || value < Min || value > Max)
            throw new InvalidDataException($"{field} must be between {Min} and {Max}; found {value}.");
    }
    public static void Color(string? value, string field)
    {
        if (value is null || value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out _))
            throw new InvalidDataException($"{field} must be a #rrggbb color; found '{value}'.");
    }
}

/// <summary>Serializer settings for authored files: camelCase, tolerant of member case, strict about unknown members, and sparse. Values equal to the type's defaults are omitted so files stay small and diffs stay readable.</summary>
public static class AuthoredJson
{
    public static JsonSerializerOptions Options { get; } = Create(strict: true);
    /// <summary>For files written by other tools, such as head workshop exports, where extra members are tolerated.</summary>
    public static JsonSerializerOptions Tolerant { get; } = Create(strict: false);
    // Identity members are always written even when they equal the defaults, so a file states what it is.
    private static readonly HashSet<string> AlwaysWritten = new(StringComparer.Ordinal) { "format", "version", "id", "name", "library", "clip", "structureRevision" };

    private static JsonSerializerOptions Create(bool strict)
    {
        var resolver = new DefaultJsonTypeInfoResolver(); resolver.Modifiers.Add(OmitDefaults);
        return new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true,
            UnmappedMemberHandling = strict ? JsonUnmappedMemberHandling.Disallow : JsonUnmappedMemberHandling.Skip,
            TypeInfoResolver = resolver
        };
    }
    private static void OmitDefaults(JsonTypeInfo info)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.Namespace != typeof(AuthoredJson).Namespace || info.Type.GetConstructor(Type.EmptyTypes) is null) return;
        var defaults = Activator.CreateInstance(info.Type)!;
        foreach (var property in info.Properties)
        {
            if (property.Get is null || AlwaysWritten.Contains(property.Name)) continue;
            var baseline = property.Get(defaults);
            property.ShouldSerialize = (_, value) => !Equals(value, baseline);
        }
    }
}
