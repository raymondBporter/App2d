using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Primitives;
using App2d.Core.IO;
using App2d.Core.Rendering;
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
        {
            throw new InvalidDataException($"{field} must be one of {string.Join(", ", allowed)}; found '{value}'.");
        }
    }
}

/// <summary>An authored value's accepted range. The soft range is the editor's suggested slider span; files may hold any value within the hard range.</summary>
public readonly record struct Limit(float Min, float Max, float SoftMin, float SoftMax) : IInterval<float>
{
    public Limit(float min, float max) : this(min, max, min, max) { }
    public Limit Soft(float min, float max) => this with { SoftMin = min, SoftMax = max };
    public float Clamp(float value) => IntervalMath<float>.Clamp(this, value);
    public void Check(float value, string field)
    {
        if (!float.IsFinite(value) || value < Min || value > Max)
        {
            throw new InvalidDataException($"{field} must be between {Min} and {Max}; found {value}.");
        }
    }
    public static void Color(string? value, string field)
    {
        if (!ColorExtensions.TryParseRgb(value.AsSpan(), out _))
        {
            throw new InvalidDataException($"{field} must be a #rrggbb color; found '{value}'.");
        }
    }
}

/// <summary>Serializer settings for authored files: camelCase, tolerant of member case, strict about unknown members, and sparse. Values equal to the type's defaults are omitted so files stay small and diffs stay readable.</summary>
public static class AuthoredJson
{
    public static JsonSerializerOptions Options { get; } = Create(strict: true, compactExtrusions: true);
    // Undo snapshots keep derived meshes because draft restoration intentionally skips validation and rebuilding.
    internal static JsonSerializerOptions SnapshotOptions { get; } = Create(strict: true, compactExtrusions: false);
    /// <summary>For files written by other tools, such as head workshop exports, where extra members are tolerated.</summary>
    public static JsonSerializerOptions Tolerant { get; } = Create(strict: false, compactExtrusions: true);
    // Identity members are always written even when they equal the defaults, so a file states what it is.
    private static readonly HashSet<string> AlwaysWritten = new(StringComparer.Ordinal) { "format", "version", "id", "name", "library", "clip", "structureRevision" };

    private static JsonSerializerOptions Create(bool strict, bool compactExtrusions)
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            KeyframeComponents(info);
            // Runtime-only members are outside the file schema, including when checking unknown input fields.
            for (var i = info.Properties.Count - 1; i >= 0; i--)
            {
                if (info.Properties[i].Get is null && info.Properties[i].Set is null)
                {
                    info.Properties.RemoveAt(i);
                }
            }
            OmitDefaults(info, compactExtrusions);
        });
        return new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowOutOfOrderMetadataProperties = true,
            WriteIndented = true,
            UnmappedMemberHandling = strict ? JsonUnmappedMemberHandling.Disallow : JsonUnmappedMemberHandling.Skip,
            TypeInfoResolver = resolver,
            Converters = { new Vector2JsonConverter() }
        };
    }

    // Keep the authored file vocabulary at the serialization boundary. Working keys store vectors only.
    private static void KeyframeComponents(JsonTypeInfo info)
    {
        Components<ClipKey>(info, nameof(ClipKey.Value), ["x", "y", "z"], static (key, i) => key.Value[i],
            static (key, i, component) => { var value = key.Value; value[i] = component; key.Value = value; });
        Components<SlotColorKey2D>(info, nameof(SlotColorKey2D.Value), ["r", "g", "b", "a"], static (key, i) => key.Value[i],
            static (key, i, component) => { var value = key.Value; value[i] = component; key.Value = value; });
        Components<KeyCurve2D>(info, nameof(KeyCurve2D.Control1), ["x1", "y1"], static (curve, i) => curve.Control1[i],
            static (curve, i, component) => { var value = curve.Control1; value[i] = component; curve.Control1 = value; });
        Components<KeyCurve2D>(info, nameof(KeyCurve2D.Control2), ["x2", "y2"], static (curve, i) => curve.Control2[i],
            static (curve, i, component) => { var value = curve.Control2; value[i] = component; curve.Control2 = value; });
    }

    private static void Components<T>(JsonTypeInfo info, string vectorName, string[] names,
        Func<T, int, float> get, Action<T, int, float> set)
    {
        if (info.Type != typeof(T))
            return;
        var name = info.Options.PropertyNamingPolicy?.ConvertName(vectorName) ?? vectorName;
        var vector = info.Properties.Single(property => property.Name == name);
        var position = info.Properties.IndexOf(vector);
        info.Properties.RemoveAt(position);
        for (var i = 0; i < names.Length; i++)
        {
            var component = i;
            var property = info.CreateJsonPropertyInfo(typeof(float), names[i]);
            property.Get = owner => get((T)owner, component);
            property.Set = (owner, value) => set((T)owner, component, (float)value!);
            info.Properties.Insert(position + i, property);
        }
    }

    private static void OmitDefaults(JsonTypeInfo info, bool compactExtrusions)
    {
        if (info.Kind != JsonTypeInfoKind.Object || info.Type.Namespace?.StartsWith(typeof(AuthoredJson).Namespace!, StringComparison.Ordinal) != true || info.Type.GetConstructor(Type.EmptyTypes) is null)
        {
            return;
        }

        var defaults = Activator.CreateInstance(info.Type)!;
        foreach (var property in info.Properties)
        {
            if (property.Get is null || AlwaysWritten.Contains(property.Name))
            {
                continue;
            }

            var baseline = property.Get(defaults);
            // An extrusion's outline is its editable source. Its mesh is rebuilt when loaded.
            if (compactExtrusions && info.Type == typeof(PropSolid) && property.Name is "vertices" or "triangles")
            {
                property.ShouldSerialize = (owner, value) => ((PropSolid)owner).Outline is null && !Equals(value, baseline);
            }
            else if (info.Type == typeof(Authored.PartAsset) && property.Name is "parameters" or "presets" or "measures" || info.Type == typeof(Authored.PartInstance) && property.Name == "inputs")
            {
                // The module contract members (P10b) are written only when declared, so a part or instance without them keeps its file as it was.
                property.ShouldSerialize = (_, value) => value is System.Collections.ICollection { Count: > 0 };
            }
            else
            {
                property.ShouldSerialize = (_, value) => !Equals(value, baseline);
            }
        }
    }
}
