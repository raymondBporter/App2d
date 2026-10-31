using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Json;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.CharacterStudio.Authoring;

/// <summary>One part's material and visibility in a saved look. Null leaves the variant's value alone.</summary>
public sealed record LookAppearance
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RenderMaterialDefinition2D? Material { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Hidden { get; set; }
    [JsonIgnore]
    public bool IsEmpty => Material is null && Hidden is null;

    public static void Check(LookAppearance? part, string field)
    {
        if (part is null)
        {
            throw new InvalidDataException($"{field}: null look part.");
        }

        part.Material?.Validate(field + ".material");
    }

    /// <summary>The typed overrides this appearance writes on a part.</summary>
    public IEnumerable<PropertyOverride> Overrides(string part)
    {
        if (Material is not null) yield return PropertyOverride.Of(PropertyRef.OfPart(part, RigProperties.Material), Material);
        if (Hidden is { } hidden) yield return PropertyOverride.Of(PropertyRef.OfPart(part, RigProperties.Hidden), hidden);
    }
}

/// <summary>A saved appearance applied to a variant as ordinary overrides; it has no runtime role.</summary>
public sealed record LookPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, LookAppearance> Parts { get; set; } = [];
    public Dictionary<string, string> Expressions { get; set; } = [];

    /// <summary>Reads a copy of the presets stored with a model. Use WriteTo to persist edits.</summary>
    public static List<LookPreset> ReadFrom(CharacterModel model) => model.LookData is { } data
        ? data.Deserialize<List<LookPreset>>(AuthoredJson.Options) ?? throw new InvalidDataException($"Model '{model.Id}': null looks.")
        : [];

    public static void WriteTo(CharacterModel model, List<LookPreset> looks)
    {
        Validate(model, looks);
        model.LookData = JsonSerializer.SerializeToElement(looks, AuthoredJson.Options);
    }

    public static void Validate(CharacterModel model) => Validate(model, ReadFrom(model));

    private static void Validate(CharacterModel model, List<LookPreset> looks)
    {
        var owner = $"Model '{model.Id}'";
        var parts = model.Parts.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var attachments = model.Attachments.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var look in looks)
        {
            Require(look?.Parts is not null && look.Expressions is not null, $"{owner}: incomplete look.");
            AuthoredAsset.RequireId(look.Id, $"{owner} look id");
            Require(ids.Add(look.Id), $"{owner}: duplicate look '{look.Id}'.");
            Require(!string.IsNullOrWhiteSpace(look.Name), $"{owner} look '{look.Id}': a name is required.");
            foreach (var (part, change) in look.Parts)
            {
                Require(parts.Contains(part), $"{owner} look '{look.Id}': no part '{part}'.");
                LookAppearance.Check(change, $"{owner} look '{look.Id}' parts.{part}");
                change.Material?.Resolve(model.Materials);
            }
            foreach (var (instance, expression) in look.Expressions)
            {
                Require(attachments.Contains(instance), $"{owner} look '{look.Id}': no attachment '{instance}'.");
                AuthoredAsset.RequireId(expression, $"{owner} look '{look.Id}' expressions.{instance}");
            }
        }
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
