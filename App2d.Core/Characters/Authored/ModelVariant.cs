using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// Explicit overrides of one base model (plan section 3.7): one live base reference, values for its declared parameters, and
/// typed overrides of its exposed properties addressed by stable IDs. Never structural: no controls, parents or chains. One
/// inheritance level: a variant's base is always a model, never another variant.
/// </summary>
public sealed class ModelVariant
{
    public const string FormatId = "app2d-variant";
    /// <summary>
    /// Version 6 (P6a): one typed <see cref="Overrides"/> list replaces the per-field <c>rest</c>, <c>parts</c> and
    /// <c>attachments</c> dictionaries of versions 4 and 5; an older file is refused by version (P11c).
    /// </summary>
    public const int CurrentVersion = 6;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Base { get; set; } = "";
    /// <summary>
    /// Values for the base's declared parameters by ID, applied before the explicit overrides; a missing value keeps the
    /// declared default. The file member is still <c>build</c>: its meaning (values of the base's exposed proportion
    /// parameters) has not changed since the code build rule became declared bindings in P4a.
    /// </summary>
    [JsonPropertyName("build")]
    public Dictionary<string, float> Parameters { get; set; } = [];
    /// <summary>
    /// Explicit overrides of the base's exposed properties (<see cref="RigProperties"/>): bone lengths and setups, part
    /// fitting, materials, faces and visibility, attachment selection and placement, animation-role clips. One entry per
    /// property; absent inherits.
    /// </summary>
    public List<PropertyOverride> Overrides { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static ModelVariant FromJson(string json)
    {
        var variant = AuthoredAsset.Parse<ModelVariant>(json, "variant");
        variant.Validate();
        return variant;
    }

    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }

    // ---- Override access by stable target ------------------------------------------------------------------------------

    public PropertyOverride? Override(PropertyRef target) => Overrides.FirstOrDefault(o => o.Target.Key == target.Key);
    public bool HasOverride(PropertyRef target) => Override(target) is not null;
    /// <summary>Every override on one owner (a control, part, attachment or role), in file order.</summary>
    public IEnumerable<PropertyOverride> OverridesOf(string ownerKind, string owner) => Overrides.Where(o => o.Target.OwnerKind == ownerKind && o.Target.Owner == owner);

    /// <summary>Sets (or replaces) the override of one property. The entry's structure is validated; its meaning is checked against the base when the variant resolves.</summary>
    public void Set(PropertyOverride change)
    {
        change.Validate($"Variant '{Id}' overrides");
        var index = Overrides.FindIndex(o => o.Target.Key == change.Target.Key);
        if (index < 0)
        {
            Overrides.Add(change);
        }
        else
        {
            Overrides[index] = change;
        }
    }

    /// <summary>Removes the override of one property so it inherits again. Nothing is written in its place.</summary>
    public bool Reset(PropertyRef target) => Overrides.RemoveAll(o => o.Target.Key == target.Key) > 0;

    public void Validate()
    {
        var owner = $"Variant '{Id}'";
        if (Format != FormatId || Version != CurrentVersion)
        {
            throw new InvalidDataException($"{owner}: unsupported format/version.");
        }

        AuthoredAsset.RequireId(Id, "variant id");
        AuthoredAsset.RequireId(Base, $"{owner} base");
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidDataException($"{owner}: a name is required.");
        }

        if (Parameters is null || Overrides is null)
        {
            throw new InvalidDataException($"{owner}: collections cannot be null.");
        }

        foreach (var (id, value) in Parameters)
        {
            new Limit(.01f, 100).Check(value, $"{owner} build.{id}");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Overrides.Count; i++)
        {
            var change = Overrides[i] ?? throw new InvalidDataException($"{owner} overrides: null entry {i + 1}.");
            change.Validate($"{owner} overrides");
            if (!keys.Add(change.Key))
            {
                throw new InvalidDataException($"{owner} overrides: {change.Target} is overridden twice; a property has one override.");
            }
        }
    }
}
