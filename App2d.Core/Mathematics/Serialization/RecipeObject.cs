using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Serialization;

internal sealed class RecipeObject
{
    private readonly Dictionary<string, JsonElement> _fields;
    private readonly HashSet<string> _read;
    private readonly JsonSerializerOptions _options;

    public RecipeObject(JsonElement element, JsonSerializerOptions options)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new JsonException("Expected an interpolation recipe object.");
        _options = options;
        var comparer = options.PropertyNameCaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _fields = new(comparer);
        _read = new(comparer);
        foreach (var field in element.EnumerateObject())
            if (!_fields.TryAdd(field.Name, field.Value)) throw new JsonException($"Duplicate recipe member '{field.Name}'.");
    }

    public bool Has(string name) => _fields.ContainsKey(name);
    public JsonElement Element(string name)
    {
        _read.Add(name);
        return _fields.TryGetValue(name, out var value) ? value : throw new JsonException($"Missing recipe member '{name}'.");
    }

    public T Required<T>(string name) => Element(name).Deserialize<T>(_options)!;
    public T Optional<T>(string name, T fallback) => Has(name) ? Required<T>(name) : fallback;
    public string Text(string name) => Element(name) is var value && value.ValueKind == JsonValueKind.String
        ? value.GetString()! : throw new JsonException($"Recipe member '{name}' must be a string.");

    public void Complete()
    {
        if (_options.UnmappedMemberHandling != JsonUnmappedMemberHandling.Disallow) return;
        foreach (var name in _fields.Keys)
            if (!_read.Contains(name)) throw new JsonException($"Unknown recipe member '{name}'.");
    }
}
