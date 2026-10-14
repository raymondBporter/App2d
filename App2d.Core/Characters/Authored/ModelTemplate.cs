using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// An authoring recipe over existing resources. Animation keys are suffixes for new IDs; values are source clip IDs.
/// Instantiation copies the model and clips, remapping references without changing control IDs or motion data.
/// </summary>
public sealed class ModelTemplate
{
    public const string FormatId = "app2d-model-template";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Model { get; set; } = "";
    public Dictionary<string, string> Animations { get; set; } = [];
    /// <summary>A key in Animations to preview after creation. Null opens the rest pose.</summary>
    public string? PreviewAnimation { get; set; }

    public static ModelTemplate FromJson(string json)
    {
        var template = AuthoredAsset.Parse<ModelTemplate>(json, "model template");
        template.Validate(); return template;
    }

    public void Validate()
    {
        var owner = $"Model template '{Id}'";
        if (Format != FormatId || Version != 1) throw new InvalidDataException($"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "template id"); AuthoredAsset.RequireId(Model, $"{owner} model");
        if (Id == "empty") throw new InvalidDataException($"{owner}: 'empty' is reserved for a blank model.");
        if (string.IsNullOrWhiteSpace(Name) || Description is null) throw new InvalidDataException($"{owner}: a name and non-null description are required.");
        if (Animations is null || Animations.Count > 256) throw new InvalidDataException($"{owner}: invalid animations collection.");
        var sources = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (suffix, source) in Animations)
        {
            AuthoredAsset.RequireId(suffix, $"{owner} animation suffix");
            AuthoredAsset.RequireId(source, $"{owner} animation '{suffix}'");
            if (!sources.Add(source)) throw new InvalidDataException($"{owner}: animation '{source}' is included more than once.");
        }
        if (PreviewAnimation is not null && !Animations.ContainsKey(PreviewAnimation))
            throw new InvalidDataException($"{owner}: previewAnimation '{PreviewAnimation}' is not an animation suffix.");
    }

    /// <summary>Prepares and validates the whole bundle before the workspace adds any documents. Source objects stay untouched.</summary>
    public ModelTemplateInstance Instantiate(string id, string name, Func<string, CharacterModel?> modelOf, Func<string, MotionClip?> clipOf)
    {
        Validate();
        AuthoredAsset.RequireId(id, "new model id");
        var owner = $"Model template '{Id}'";
        var source = modelOf(Model) ?? throw new InvalidDataException($"{owner}: missing source model '{Model}'.");
        var resolved = ResolvedModel.From(source);
        var model = CharacterModel.FromJson(source.ToJson());
        model.Id = id; model.Name = name; model.StructureRevision = 1;
        var remap = Animations.ToDictionary(a => a.Value, a => id + "-" + a.Key, StringComparer.Ordinal);
        var clips = new List<MotionClip>();
        foreach (var sourceId in Animations.Values)
        {
            var original = clipOf(sourceId) ?? throw new InvalidDataException($"{owner}: missing source animation '{sourceId}'.");
            // Match the editor's draft rules; the new bundle gets one consistent revision after compatibility is checked.
            original.Validate(resolved, exactRevision: false);
            var clip = MotionClip.FromJson(original.ToJson());
            clip.Id = remap[sourceId]; clip.Name = name + " / " + original.Name;
            clip.Model = id; clip.StructureRevision = model.StructureRevision;
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
        var instance = ResolvedModel.From(model);
        foreach (var clip in clips) clip.Validate(instance);
        return new(model, clips, PreviewAnimation is null ? null : id + "-" + PreviewAnimation);
    }
}

public sealed record ModelTemplateInstance(CharacterModel Model, IReadOnlyList<MotionClip> Animations, string? PreviewAnimation);

/// <summary>Read-only recipes under an authored root's templates folder. Template IDs have their own namespace.</summary>
public sealed class ModelTemplateCatalog
{
    private readonly Dictionary<string, ModelTemplate> _entries = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, ModelTemplate> Entries => _entries;
    public List<string> Errors { get; } = [];

    public static ModelTemplateCatalog Load(string authoredRoot)
    {
        var catalog = new ModelTemplateCatalog();
        var directory = Path.Combine(authoredRoot, "templates");
        if (!Directory.Exists(directory)) return catalog;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var template = ModelTemplate.FromJson(File.ReadAllText(path));
                if (!catalog._entries.TryAdd(template.Id, template)) throw new InvalidDataException($"Duplicate template id '{template.Id}'.");
            }
            catch (Exception error) when (error is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or ArgumentException)
            {
                catalog.Errors.Add($"{path}: {error.Message}");
            }
        }
        return catalog;
    }
}
