using App2d.Core.Characters.Authored;
using App2d.Core.Json;
using System.Text.Json;

namespace App2d.CharacterStudio.Authoring;

/// <summary>
/// A part slot a template publishes (character-system migration, P10a): a socket of the model with the usage category of
/// the library parts it takes, whether the anatomy needs an instance there and the asset the published model wears by default.
/// </summary>
public sealed record TemplateSlot
{
    public string Id { get; set; } = "";
    /// <summary>The <see cref="Part.Usage"/> a part instanced here must declare.</summary>
    public string Usage { get; set; } = "part";
    public bool Required { get; set; }
    /// <summary>The asset the published model instances on this slot, or null for an empty slot.</summary>
    public string? Default { get; set; }

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        AuthoredAsset.RequireId(Usage, field + " usage");
        if (Default is not null)
        {
            AuthoredAsset.RequireId(Default, field + " default");
        }
    }
}

/// <summary>
/// A template's default animation library (P10a): the role assignments its clips fill (role → animation key), the control
/// groups a layered action may own as masks, and which exposed dimension scales each clip measure.
/// </summary>
public sealed record TemplateLibrary
{
    public Dictionary<string, string> Roles { get; set; } = [];
    public List<string> Masks { get; set; } = [];
    /// <summary>measure id → dimension (parameter) id: the dimension a clip channel keyed in that measure scales with.</summary>
    public Dictionary<string, string> Mappings { get; set; } = [];
}

/// <summary>
/// An authoring recipe over existing resources. Animation keys are suffixes for new IDs; values are source clip IDs.
/// Instantiation copies the model and clips, remapping references without changing control IDs or motion data.
/// Publishes the anatomy's contract as data: part slots, exposed dimensions and presets, and the default animation library.
/// </summary>
public sealed class ModelTemplate
{
    public const string FormatId = "app2d-model-template";
    public const int CurrentVersion = 2;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Model { get; set; } = "";
    public Dictionary<string, string> Animations { get; set; } = [];
    /// <summary>A key in Animations to preview after creation. Null opens the rest pose.</summary>
    public string? PreviewAnimation { get; set; }
    /// <summary>The attachment contract: the sockets a character made from this template offers to library parts.</summary>
    public List<TemplateSlot> Slots { get; set; } = [];
    /// <summary>The model parameters the template exposes as its dimensions.</summary>
    public List<string> Dimensions { get; set; } = [];
    /// <summary>The model presets a creation may start from.</summary>
    public List<string> Presets { get; set; } = [];
    public TemplateLibrary Library { get; set; } = new();

    public static ModelTemplate FromJson(string json)
    {
        var template = AuthoredAsset.Parse<ModelTemplate>(json, "model template");
        template.Validate();
        return template;
    }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);

    public void Save(string path)
    {
        Validate();
        File.WriteAllText(path, ToJson());
    }

    public void Validate()
    {
        var owner = $"Model template '{Id}'";
        if (Format != FormatId || Version != CurrentVersion)
        {
            throw new InvalidDataException($"{owner}: unsupported format/version.");
        }

        AuthoredAsset.RequireId(Id, "template id");
        AuthoredAsset.RequireId(Model, $"{owner} model");
        if (Id == "empty")
        {
            throw new InvalidDataException($"{owner}: 'empty' is reserved for a blank model.");
        }

        if (string.IsNullOrWhiteSpace(Name) || Description is null)
        {
            throw new InvalidDataException($"{owner}: a name and non-null description are required.");
        }

        if (Animations is null || Animations.Count > 256)
        {
            throw new InvalidDataException($"{owner}: invalid animations collection.");
        }

        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (suffix, source) in Animations)
        {
            AuthoredAsset.RequireId(suffix, $"{owner} animation suffix");
            AuthoredAsset.RequireId(source, $"{owner} animation '{suffix}'");
            if (!sources.Add(source))
            {
                throw new InvalidDataException($"{owner}: animation '{source}' is included more than once.");
            }
        }
        if (PreviewAnimation is not null && !Animations.ContainsKey(PreviewAnimation))
        {
            throw new InvalidDataException($"{owner}: previewAnimation '{PreviewAnimation}' is not an animation suffix.");
        }

        if (Slots is null || Dimensions is null || Presets is null || Library?.Roles is null || Library.Masks is null || Library.Mappings is null)
        {
            throw new InvalidDataException($"{owner}: collections cannot be null.");
        }

        var slots = new HashSet<string>(StringComparer.Ordinal);
        foreach (var slot in Slots)
        {
            if (slot is null)
            {
                throw new InvalidDataException($"{owner}: null slot.");
            }

            slot.Validate($"{owner} slot");
            if (!slots.Add(slot.Id))
            {
                throw new InvalidDataException($"{owner}: duplicate slot '{slot.Id}'.");
            }
        }
        foreach (var dimension in Dimensions)
        {
            AuthoredAsset.RequireId(dimension, $"{owner} dimension");
        }
        if (Dimensions.Distinct(StringComparer.Ordinal).Count() != Dimensions.Count)
        {
            throw new InvalidDataException($"{owner}: duplicate dimension.");
        }

        foreach (var preset in Presets)
        {
            AuthoredAsset.RequireId(preset, $"{owner} preset");
        }
        if (Presets.Distinct(StringComparer.Ordinal).Count() != Presets.Count)
        {
            throw new InvalidDataException($"{owner}: duplicate preset.");
        }

        foreach (var (role, key) in Library.Roles)
        {
            AuthoredAsset.RequireId(role, $"{owner} library role");
            if (!Animations.ContainsKey(key))
            {
                throw new InvalidDataException($"{owner} library role '{role}': '{key}' is not an animation key of this template.");
            }
        }
        foreach (var mask in Library.Masks)
        {
            AuthoredAsset.RequireId(mask, $"{owner} library mask");
        }
        if (Library.Masks.Distinct(StringComparer.Ordinal).Count() != Library.Masks.Count)
        {
            throw new InvalidDataException($"{owner}: duplicate library mask.");
        }

        foreach (var (measure, dimension) in Library.Mappings)
        {
            AuthoredAsset.RequireId(measure, $"{owner} library mapping measure");
            if (!Dimensions.Contains(dimension, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"{owner} library mapping '{measure}': '{dimension}' is not an exposed dimension.");
            }
        }
    }

    /// <summary>
    /// The published contract checked against the model it publishes (P10a), each problem with its reference path: every
    /// slot is a socket, a slot's default asset exists with the slot's usage and the model wears it there, a required slot
    /// is filled, every dimension is a parameter, every preset exists, every mask is a group and every mapping names a
    /// measure.
    /// </summary>
    public IReadOnlyList<string> ContractProblems(CharacterModel model, Func<string, Part?>? partOf)
    {
        var problems = new List<string>();
        var path = $"templates > {Id}";
        foreach (var slot in Slots)
        {
            var socket = model.Sockets.FirstOrDefault(s => s.Id == slot.Id);
            if (socket is null)
            {
                problems.Add($"{path} > slots > {slot.Id}: model '{model.Id}' has no socket '{slot.Id}'.");
                continue;
            }

            var worn = model.Attachments.Where(a => a.Socket == slot.Id).ToList();
            if (slot.Required && worn.Count == 0)
            {
                problems.Add($"{path} > slots > {slot.Id}: required, but model '{model.Id}' instances nothing on it.");
            }

            if (slot.Default is { } fallback)
            {
                var asset = partOf?.Invoke(fallback);
                if (asset is null)
                {
                    problems.Add($"{path} > slots > {slot.Id} > default: no part asset '{fallback}'.");
                }
                else if (asset.Usage != slot.Usage)
                {
                    problems.Add($"{path} > slots > {slot.Id} > default: part '{fallback}' is a '{asset.Usage}', the slot takes '{slot.Usage}'.");
                }

                if (worn.All(a => a.Asset != fallback))
                {
                    problems.Add($"{path} > slots > {slot.Id} > default: model '{model.Id}' does not wear '{fallback}' there (path: {model.Id} > attachments).");
                }
            }

            foreach (var instance in worn)
            {
                if (partOf?.Invoke(instance.Asset) is { } asset && asset.Usage != slot.Usage)
                {
                    problems.Add($"{path} > slots > {slot.Id}: instance '{instance.Id}' wears '{instance.Asset}' ({asset.Usage}); the slot takes '{slot.Usage}' (path: {model.Id} > attachments > {instance.Id}).");
                }
            }
        }
        foreach (var dimension in Dimensions)
        {
            if (model.Parameters.All(p => p.Id != dimension))
            {
                problems.Add($"{path} > dimensions > {dimension}: model '{model.Id}' declares no parameter '{dimension}'.");
            }
        }
        foreach (var preset in Presets)
        {
            if (model.Presets.All(p => p.Id != preset))
            {
                problems.Add($"{path} > presets > {preset}: model '{model.Id}' has no preset '{preset}'.");
            }
        }
        foreach (var (role, key) in Library.Roles)
        {
            // The source assigns the recipe's clip; a model created from the recipe assigns its own copy (<id>-<key>).
            var clip = Animations[key];
            if (!model.MotionSets.Any(s => s.Roles.TryGetValue(role, out var assigned) && (assigned == clip || assigned == model.Id + "-" + key)))
            {
                problems.Add($"{path} > library > roles > {role}: no motion set of '{model.Id}' assigns '{clip}' to it (path: {model.Id} > motionSets).");
            }
        }
        foreach (var mask in Library.Masks)
        {
            if (model.Groups.All(g => g.Id != mask))
            {
                problems.Add($"{path} > library > masks > {mask}: model '{model.Id}' has no group '{mask}'.");
            }
        }
        foreach (var (measure, _) in Library.Mappings)
        {
            if (model.Measures.All(m => m.Id != measure))
            {
                problems.Add($"{path} > library > mappings > {measure}: model '{model.Id}' declares no measure '{measure}'.");
            }
        }
        return problems;
    }

    /// <summary>
    /// Prepares and validates the whole bundle before the workspace adds any documents. Source objects stay untouched. A
    /// published template's contract is checked against the source first; <paramref name="preset"/> (one the template
    /// publishes) writes its values as the new model's parameter defaults.
    /// </summary>
    public ModelTemplateInstance Instantiate(string id, string name, Func<string, CharacterModel?> modelOf, Func<string, MotionClip?> clipOf, Func<string, Part?>? partOf = null, string? preset = null)
    {
        Validate();
        AuthoredAsset.RequireId(id, "new model id");
        var owner = $"Model template '{Id}'";
        var source = modelOf(Model) ?? throw new InvalidDataException($"{owner}: missing source model '{Model}'.");
        var problems = ContractProblems(source, partOf);
        if (problems.Count > 0)
        {
            throw new InvalidDataException($"{owner}: its published contract does not hold on '{Model}': {string.Join(" ", problems)}");
        }

        if (preset is not null && !Presets.Contains(preset, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{owner}: publishes no preset '{preset}'{(Presets.Count > 0 ? $" (one of {string.Join(", ", Presets)})" : "")}.");
        }

        var resolved = ResolvedModel.From(source, null, partOf);
        var model = CharacterModel.FromJson(source.ToJson());
        model.Id = id;
        model.Name = name;
        model.StructureRevision = 1;
        if (preset is not null)
        {
            // The chosen preset becomes the new model's rest: its values are the parameter defaults, so every build of
            // the new character starts from it and the preset list stays as published.
            var values = model.Presets.First(p => p.Id == preset).Values;
            foreach (var parameter in model.Parameters)
            {
                if (values.TryGetValue(parameter.Id, out var value))
                {
                    parameter.Default = value;
                }
            }
        }

        var remap = Animations.ToDictionary(a => a.Value, a => id + "-" + a.Key, StringComparer.Ordinal);
        var clips = new List<MotionClip>();
        foreach (var sourceId in Animations.Values)
        {
            var original = clipOf(sourceId) ?? throw new InvalidDataException($"{owner}: missing source animation '{sourceId}'.");
            // Match the editor's draft rules; the new bundle gets one consistent revision after compatibility is checked.
            original.Validate(resolved, exactRevision: false);
            var clip = MotionClip.FromJson(original.ToJson());
            clip.Id = remap[sourceId];
            clip.Name = name + " / " + original.Name;
            clip.Model = id;
            clip.StructureRevision = model.StructureRevision;
            clips.Add(clip);
        }
        foreach (var set in model.MotionSets)
        {
            foreach (var role in set.Roles.Keys.ToArray())
            {
                var clip = set.Roles[role];
                set.Roles[role] = remap.TryGetValue(clip, out var replacement) ? replacement
                    : throw new InvalidDataException($"{owner}: motion set '{set.Id}' role '{role}' uses '{clip}'; include it in animations.");
            }
        }
        model.Validate();
        // The new model keeps its attachment instances as references to the same shared library parts.
        var instance = ResolvedModel.From(model, null, partOf);
        foreach (var clip in clips)
        {
            clip.Validate(instance);
        }

        return new(model, clips, PreviewAnimation is null ? null : id + "-" + PreviewAnimation);
    }
}

public sealed record ModelTemplateInstance(CharacterModel Model, IReadOnlyList<MotionClip> Animations, string? PreviewAnimation);
