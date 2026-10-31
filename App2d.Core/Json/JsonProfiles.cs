using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Json;

/// <summary>
/// Cached, read-only JSON contracts. All file output is indented; names, number handling and schema validation
/// remain explicit per format. Clone a profile only when defining a different contract, then cache that clone.
/// </summary>
public static class JsonProfiles
{
    /// <summary>Reports and data whose declared property names are the file names.</summary>
    public static JsonSerializerOptions Indented { get; } = Freeze(new() { WriteIndented = true });

    /// <summary>Value observations that include public fields, such as numerics vectors.</summary>
    public static JsonSerializerOptions Fields { get; } = Freeze(new(Indented) { IncludeFields = true });

    public static JsonSerializerOptions CamelCase { get; } = Freeze(new(Indented)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

    public static JsonSerializerOptions SparseCamelCase { get; } = Freeze(new(CamelCase)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    });

    /// <summary>Case-tolerant input without changing declared names or permitting quoted numbers.</summary>
    public static JsonSerializerOptions CaseInsensitive { get; } = Freeze(new(Indented)
    {
        PropertyNameCaseInsensitive = true
    });

    public static JsonSerializerOptions CamelCaseInsensitive { get; } = Freeze(new(CamelCase)
    {
        PropertyNameCaseInsensitive = true
    });

    /// <summary>Content manifests: camelCase, case-tolerant input and quoted numbers, matching Web defaults.</summary>
    public static JsonSerializerOptions Web { get; } = Freeze(new(JsonSerializerDefaults.Web) { WriteIndented = true });

    /// <summary>The external music-stem manifest's snake_case vocabulary.</summary>
    public static JsonSerializerOptions SnakeCase { get; } = Freeze(new(Indented)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    });

    /// <summary>Owned file schemas: exact camelCase names, numeric tokens and no unknown members.</summary>
    public static JsonSerializerOptions StrictCamelCase { get; } = Freeze(new(CamelCase)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    });

    /// <summary>World documents and their generators share coordinate encoding and omit absent optional values.</summary>
    public static JsonSerializerOptions World { get; } = Freeze(new(StrictCamelCase)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new Vector2JsonConverter() }
    });

    public static JsonSerializerOptions Geometry { get; } = Freeze(new(Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new Vector2JsonConverter() }
    });

    /// <summary>The prototype puppet file keeps its existing metadata-order contract.</summary>
    public static JsonSerializerOptions Puppet { get; } = Freeze(new(Geometry)
    {
        AllowOutOfOrderMetadataProperties = false
    });

    /// <summary>Authored Parts, rigs and clips: schema-aware defaults and keyframe components.</summary>
    public static JsonSerializerOptions Authored { get; } = Freeze(AuthoredJson.CreateOptions(compactExtrusions: true));

    // Undo must retain derived meshes rather than omit and rebuild them as file loading does.
    internal static JsonSerializerOptions AuthoredSnapshot { get; } = Freeze(AuthoredJson.CreateOptions(compactExtrusions: false));

    private static JsonSerializerOptions Freeze(JsonSerializerOptions options)
    {
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
