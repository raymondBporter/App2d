using System.Text.Json;

namespace App2d.Core.Characters.Editing;

/// <summary>What a save wrote, which other drafts it changed and what still needs repair.</summary>
public sealed record SaveReport(string Path, IReadOnlyList<AssetDocument> Updated, IReadOnlyList<string> Problems);

/// <summary>
/// Every authored asset under one root as a document, clean or draft. Resolution reads current drafts, so an unsaved clip or
/// base edit reaches every dependent preview. Resolved models and clip checks are cached on document versions and rebuilt
/// only when an asset changed.
/// </summary>
public sealed class AuthoringWorkspace
{
    private readonly Dictionary<string, AssetDocument> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _savedStructure = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (int ModelVersion, int VariantVersion, ResolvedModel? Resolved, string? Error)> _resolved = new(StringComparer.Ordinal);
    private readonly Dictionary<(string Clip, string Subject), (int ClipVersion, int ModelVersion, int VariantVersion, string? Error)> _playable = [];

    private AuthoringWorkspace(string root) => Root = root;

    public string Root { get; }
    /// <summary>Files that could not be read at all. Semantic problems are reported per document by <see cref="Problems"/>.</summary>
    public IReadOnlyList<string> LoadErrors { get; private set; } = [];
    public IEnumerable<AssetDocument> Documents => _documents.Values;
    /// <summary>Changes whenever any document changes or is added: versions only grow, so their sum never repeats.</summary>
    public int Revision => _documents.Count + _documents.Values.Sum(d => d.Version);
    public IEnumerable<AssetDocument> DirtyDocuments => _documents.Values.Where(d => d.Dirty || d.IsNew);
    public IEnumerable<AssetDocument<CharacterModel>> Models => _documents.Values.OfType<AssetDocument<CharacterModel>>();
    public IEnumerable<AssetDocument<ModelVariant>> Variants => _documents.Values.OfType<AssetDocument<ModelVariant>>();
    public IEnumerable<AssetDocument<MotionClip>> Clips => _documents.Values.OfType<AssetDocument<MotionClip>>();

    public static AuthoringWorkspace Open(string root)
    {
        var workspace = new AuthoringWorkspace(root);
        var catalog = AuthoredCatalog.Load(root);
        // Only unreadable files are load errors; reference problems stay visible on their documents.
        workspace.LoadErrors = [.. catalog.Errors.Where(e => !catalog.Models.Keys.Concat(catalog.Variants.Keys).Concat(catalog.Animations.Keys).Any(id => e.StartsWith(catalog.PathOf(id) + ":", StringComparison.Ordinal)))];
        foreach (var model in catalog.Models.Values) workspace.Add(AssetDocuments.Of(model, catalog.PathOf(model.Id)));
        foreach (var variant in catalog.Variants.Values) workspace.Add(AssetDocuments.Of(variant, catalog.PathOf(variant.Id)));
        foreach (var clip in catalog.Animations.Values) workspace.Add(AssetDocuments.Of(clip, catalog.PathOf(clip.Id)));
        return workspace;
    }

    public AssetDocument? Find(string? id) => id is not null && _documents.TryGetValue(id, out var document) ? document : null;
    public AssetDocument<CharacterModel>? Model(string? id) => Find(id) as AssetDocument<CharacterModel>;
    public AssetDocument<ModelVariant>? Variant(string? id) => Find(id) as AssetDocument<ModelVariant>;
    public AssetDocument<MotionClip>? Clip(string? id) => Find(id) as AssetDocument<MotionClip>;
    public bool Exists(string id) => _documents.ContainsKey(id);

    /// <summary>A free ID near <paramref name="basis"/>. IDs are unique across every asset kind.</summary>
    public string SuggestId(string basis) => ModelAuthoring.UniqueId(basis, _documents.Keys);

    /// <summary>Adds a new, unsaved asset. Its file is created on first save.</summary>
    public AssetDocument<T> Create<T>(T asset) where T : class
    {
        AssetDocument document = asset switch
        {
            CharacterModel model => AssetDocuments.Of(model, null), ModelVariant variant => AssetDocuments.Of(variant, null),
            MotionClip clip => AssetDocuments.Of(clip, null), _ => throw new ArgumentException($"Not an authored asset: {typeof(T).Name}."),
        };
        AuthoredAsset.RequireId(document.Id, AssetKinds.Label(document.Kind) + " id");
        if (Exists(document.Id)) throw new InvalidDataException($"The id '{document.Id}' is already used.");
        Add(document); return (AssetDocument<T>)document;
    }

    /// <summary>The base model a model or variant ID stands on.</summary>
    public string? BaseOf(string id) => Find(id) switch { AssetDocument<CharacterModel> m => m.Id, AssetDocument<ModelVariant> v => v.Asset.Base, AssetDocument<MotionClip> c => c.Asset.Model, _ => null };

    /// <summary>Resolves a model or variant from current drafts. Null with an error message when it cannot resolve.</summary>
    public ResolvedModel? Resolve(string? id, out string? error)
    {
        error = null;
        var variant = Variant(id); var model = Model(variant?.Asset.Base ?? id);
        if (model is null) { error = variant is null ? $"No model or variant '{id}'." : $"Variant '{id}' references missing base model '{variant.Asset.Base}'."; return null; }
        var key = variant?.Id ?? model.Id;
        if (_resolved.TryGetValue(key, out var cached) && cached.ModelVersion == model.Version && cached.VariantVersion == (variant?.Version ?? -1)) { error = cached.Error; return cached.Resolved; }
        ResolvedModel? resolved = null;
        try { resolved = ResolvedModel.From(model.Asset, variant?.Asset); }
        catch (Exception ex) when (IsAssetError(ex)) { error = ex.Message; }
        _resolved[key] = (model.Version, variant?.Version ?? -1, resolved, error);
        return resolved;
    }
    public ResolvedModel? Resolve(string? id) => Resolve(id, out _);

    /// <summary>Whether a clip's draft can play on a model or variant: same base, structurally compatible. Revision drift from an unsaved base edit is tolerated here and settled on save.</summary>
    public bool CanPlay(string clipId, string subjectId, out string? error)
    {
        var clip = Clip(clipId); var model = Resolve(subjectId, out error);
        if (clip is null) { error = $"No animation '{clipId}'."; return false; }
        if (model is null) return false;
        var baseDocument = Model(model.Base.Id)!; var variant = Variant(subjectId);
        if (_playable.TryGetValue((clipId, subjectId), out var cached) && cached.ClipVersion == clip.Version && cached.ModelVersion == baseDocument.Version && cached.VariantVersion == (variant?.Version ?? -1))
        { error = cached.Error; return error is null; }
        error = null;
        try { clip.Asset.Validate(model, exactRevision: false); }
        catch (Exception ex) when (IsAssetError(ex)) { error = ex.Message; }
        _playable[(clipId, subjectId)] = (clip.Version, baseDocument.Version, variant?.Version ?? -1, error);
        return error is null;
    }

    /// <summary>Clips that name this subject's base model, playable or not; compatibility is checked separately, never inferred from names.</summary>
    public IEnumerable<AssetDocument<MotionClip>> ClipsFor(string subjectId) { var basis = BaseOf(subjectId); return Clips.Where(c => c.Asset.Model == basis); }

    /// <summary>Variants and clips that depend on a base model.</summary>
    public (IReadOnlyList<AssetDocument<ModelVariant>> Variants, IReadOnlyList<AssetDocument<MotionClip>> Clips) Dependents(string modelId) =>
        ([.. Variants.Where(v => v.Asset.Base == modelId)], [.. Clips.Where(c => c.Asset.Model == modelId)]);

    /// <summary>Whether a model's draft changed structure since it was last saved, which will raise its structure revision on save.</summary>
    public bool StructureChanged(AssetDocument<CharacterModel> model) =>
        !_savedStructure.TryGetValue(model.Id, out var saved) || saved != ModelAuthoring.StructureSignature(model.Asset);

    /// <summary>Everything that would stop this document compiling for game use, in its current draft state.</summary>
    public IReadOnlyList<string> Problems(AssetDocument document)
    {
        var problems = new List<string>();
        void Try(Action check) { try { check(); } catch (Exception ex) when (IsAssetError(ex)) { problems.Add(ex.Message); } }
        switch (document)
        {
            case AssetDocument<CharacterModel> model: Try(model.Asset.Validate); break;
            case AssetDocument<ModelVariant> variant: if (Resolve(variant.Id, out var error) is null) problems.Add(error!); break;
            case AssetDocument<MotionClip> clip:
                var basis = Resolve(clip.Asset.Model, out var missing);
                if (basis is null) { problems.Add(missing!); break; }
                Try(() => clip.Asset.Validate(basis, exactRevision: false));
                if (problems.Count == 0 && clip.Asset.StructureRevision != basis.Base.StructureRevision && !StructureChanged(Model(basis.Base.Id)!))
                    problems.Add($"Clip '{clip.Id}' was authored against structure revision {clip.Asset.StructureRevision} of '{basis.Base.Id}'; the model is at revision {basis.Base.StructureRevision}.");
                break;
        }
        return problems;
    }

    /// <summary>
    /// Writes one document. The file must be valid on its own; unresolved references are allowed so drafts can be saved for
    /// repair, and are returned as problems. Saving a model whose structure changed raises its revision and moves each
    /// dependent clip that is still structurally compatible onto it, leaving those clips dirty to be saved in turn.
    /// </summary>
    public SaveReport Save(AssetDocument document)
    {
        document.Commit();
        var updated = new List<AssetDocument>();
        switch (document)
        {
            case AssetDocument<CharacterModel> model:
                model.Asset.Validate();
                if (!model.IsNew && StructureChanged(model))
                {
                    var from = model.Asset.StructureRevision; model.Edit(() => model.Asset.StructureRevision++);
                    var resolved = ResolvedModel.From(model.Asset);
                    foreach (var clip in Dependents(model.Id).Clips.Where(c => c.Asset.StructureRevision == from))
                    {
                        try { clip.Asset.Validate(resolved, exactRevision: false); }
                        catch (Exception ex) when (IsAssetError(ex)) { continue; }
                        clip.Edit(() => clip.Asset.StructureRevision = model.Asset.StructureRevision); updated.Add(clip);
                    }
                }
                break;
            case AssetDocument<ModelVariant> variant: variant.Asset.Validate(); break;
            case AssetDocument<MotionClip> clip: clip.Asset.Validate(); break;
        }
        var path = document.Path ?? System.IO.Path.Combine(Root, AssetKinds.Folder(document.Kind), document.Id + ".json");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        AuthoredAsset.Write(path, document.Serialize());
        document.MarkSaved(path);
        if (document is AssetDocument<CharacterModel> saved) _savedStructure[saved.Id] = ModelAuthoring.StructureSignature(saved.Asset);
        return new(path, updated, Problems(document));
    }

    private void Add(AssetDocument document)
    {
        _documents[document.Id] = document;
        if (document is AssetDocument<CharacterModel> { IsNew: false } model) _savedStructure[model.Id] = ModelAuthoring.StructureSignature(model.Asset);
    }

    public static bool IsAssetError(Exception ex) =>
        ex is InvalidDataException or JsonException or KeyNotFoundException or InvalidOperationException or ArgumentException;
}
