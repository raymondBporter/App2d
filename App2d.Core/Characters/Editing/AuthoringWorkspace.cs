using System.Text.Json;

namespace App2d.Core.Characters.Editing;

/// <summary>What a save wrote, which other drafts it changed and what still needs repair.</summary>
public sealed record SaveReport(string Path, IReadOnlyList<AssetDocument> Updated, IReadOnlyList<string> Problems);

/// <summary>One asset reference: the other asset's ID and how it is used, such as "base" or "motion set Heavy: walk".</summary>
public sealed record AssetReference(string Id, string Relation);

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
    private readonly Dictionary<string, (int Revision, ResolvedEntity? Entity, string? Error)> _entities = new(StringComparer.Ordinal);
    private (int Revision, ILookup<string, AssetReference> UsedBy)? _usedBy;

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
    public IEnumerable<AssetDocument<PropAsset>> Props => _documents.Values.OfType<AssetDocument<PropAsset>>();
    public IEnumerable<AssetDocument<EntityAsset>> Entities => _documents.Values.OfType<AssetDocument<EntityAsset>>();

    public static AuthoringWorkspace Open(string root)
    {
        var workspace = new AuthoringWorkspace(root);
        var catalog = AuthoredCatalog.Load(root);
        // Only unreadable files are load errors; reference problems stay visible on their documents.
        var loaded = catalog.Models.Keys.Concat(catalog.Variants.Keys).Concat(catalog.Animations.Keys).Concat(catalog.Props.Keys).Concat(catalog.EntityAssets.Keys).Select(catalog.PathOf).ToList();
        workspace.LoadErrors = [.. catalog.Errors.Where(e => !loaded.Any(path => e.StartsWith(path + ":", StringComparison.Ordinal)))];
        foreach (var model in catalog.Models.Values) workspace.Add(AssetDocuments.Of(model, catalog.PathOf(model.Id)));
        foreach (var variant in catalog.Variants.Values) workspace.Add(AssetDocuments.Of(variant, catalog.PathOf(variant.Id)));
        foreach (var clip in catalog.Animations.Values) workspace.Add(AssetDocuments.Of(clip, catalog.PathOf(clip.Id)));
        foreach (var prop in catalog.Props.Values) workspace.Add(AssetDocuments.Of(prop, catalog.PathOf(prop.Id)));
        foreach (var entity in catalog.EntityAssets.Values) workspace.Add(AssetDocuments.Of(entity, catalog.PathOf(entity.Id)));
        return workspace;
    }

    public AssetDocument? Find(string? id) => id is not null && _documents.TryGetValue(id, out var document) ? document : null;
    public AssetDocument<CharacterModel>? Model(string? id) => Find(id) as AssetDocument<CharacterModel>;
    public AssetDocument<ModelVariant>? Variant(string? id) => Find(id) as AssetDocument<ModelVariant>;
    public AssetDocument<MotionClip>? Clip(string? id) => Find(id) as AssetDocument<MotionClip>;
    public AssetDocument<PropAsset>? Prop(string? id) => Find(id) as AssetDocument<PropAsset>;
    public AssetDocument<EntityAsset>? Entity(string? id) => Find(id) as AssetDocument<EntityAsset>;
    public bool Exists(string id) => _documents.ContainsKey(id);

    /// <summary>A free ID near <paramref name="basis"/>. IDs are unique across every asset kind.</summary>
    public string SuggestId(string basis) => ModelAuthoring.UniqueId(basis, _documents.Keys);

    /// <summary>Adds a new, unsaved asset. Its file is created on first save.</summary>
    public AssetDocument<T> Create<T>(T asset) where T : class
    {
        AssetDocument document = asset switch
        {
            CharacterModel model => AssetDocuments.Of(model, null), ModelVariant variant => AssetDocuments.Of(variant, null),
            MotionClip clip => AssetDocuments.Of(clip, null), PropAsset prop => AssetDocuments.Of(prop, null), EntityAsset entity => AssetDocuments.Of(entity, null),
            _ => throw new ArgumentException($"Not an authored asset: {typeof(T).Name}."),
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

    /// <summary>
    /// An entity compiled against the current drafts, for previews and problem reports. Cached until any document changes.
    /// Null with the compile error when it does not compile. A playtest uses <see cref="SnapshotEntities"/> instead.
    /// </summary>
    public ResolvedEntity? CompileEntity(string? id, out string? error)
    {
        error = null;
        var document = Entity(id);
        if (document is null) { error = $"No entity '{id}'."; return null; }
        var revision = Revision;
        if (_entities.TryGetValue(document.Id, out var cached) && cached.Revision == revision) { error = cached.Error; return cached.Entity; }
        ResolvedEntity? entity = null;
        try
        {
            entity = ResolvedEntity.Compile(document.Asset,
                model => Find(model) is null ? throw new KeyNotFoundException(model) : Resolve(model, out var problem) ?? throw new InvalidDataException(problem),
                clip => Clip(clip)?.Asset, prop => Prop(prop)?.Asset);
        }
        catch (Exception ex) when (IsAssetError(ex)) { error = ex.Message; }
        _entities[document.Id] = (revision, entity, error);
        return entity;
    }
    public ResolvedEntity? CompileEntity(string? id) => CompileEntity(id, out _);

    /// <summary>
    /// An explicit snapshot for a playtest: entity and prop drafts compiled against copies of the current model, variant and
    /// clip drafts. Later edits never reach a running test; restarting takes a new snapshot. Entities that do not compile
    /// are left out and reported, never patched up.
    /// </summary>
    public (IReadOnlyList<ResolvedEntity> Entities, IReadOnlyList<string> Problems) SnapshotEntities()
    {
        var problems = new List<string>(LoadErrors);
        T? Copy<T>(AssetDocument<T>? document, Func<string, T> parse) where T : class
        {
            if (document is null) return null;
            try { return parse(JsonSerializer.Serialize(document.Asset, AuthoredJson.Options)); }
            catch (Exception ex) when (IsAssetError(ex)) { problems.Add($"{document.Id}: {ex.Message}"); return null; }
        }
        var clips = Clips.Select(c => Copy(c, MotionClip.FromJson)).OfType<MotionClip>().ToDictionary(c => c.Id, StringComparer.Ordinal);
        var props = Props.Select(p => Copy(p, PropAsset.FromJson)).OfType<PropAsset>().ToDictionary(p => p.Id, StringComparer.Ordinal);
        var resolved = new Dictionary<string, ResolvedModel>(StringComparer.Ordinal);
        ResolvedModel ResolveCopy(string id)
        {
            if (resolved.TryGetValue(id, out var cached)) return cached;
            var variant = Copy(Variant(id), ModelVariant.FromJson);
            var model = Copy(Model(variant?.Base ?? id), CharacterModel.FromJson) ?? throw new KeyNotFoundException($"No model or variant '{id}'.");
            return resolved[id] = ResolvedModel.From(model, variant);
        }
        var entities = new List<ResolvedEntity>();
        foreach (var document in Entities.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var asset = Copy(document, EntityAsset.FromJson);
            if (asset is null) continue;
            try { entities.Add(ResolvedEntity.Compile(asset, ResolveCopy, clips.GetValueOrDefault, props.GetValueOrDefault)); }
            catch (Exception ex) when (IsAssetError(ex)) { problems.Add(ex.Message); }
        }
        return (entities, problems);
    }

    /// <summary>Clips that name this subject's base model, playable or not; compatibility is checked separately, never inferred from names.</summary>
    public IEnumerable<AssetDocument<MotionClip>> ClipsFor(string subjectId) { var basis = BaseOf(subjectId); return Clips.Where(c => c.Asset.Model == basis); }

    /// <summary>
    /// What an asset references directly: a variant its base, a clip its model, a model the clips its motion sets assign, and
    /// an entity its model or variant, the clips it plays and its props.
    /// </summary>
    public IReadOnlyList<AssetReference> Uses(string id)
    {
        var uses = new List<AssetReference>();
        switch (Find(id))
        {
            case AssetDocument<ModelVariant> variant: uses.Add(new(variant.Asset.Base, "base")); break;
            case AssetDocument<MotionClip> clip: uses.Add(new(clip.Asset.Model, "model")); break;
            case AssetDocument<CharacterModel> model:
                foreach (var set in model.Asset.MotionSets) foreach (var (role, clip) in set.Roles.OrderBy(r => r.Key, StringComparer.Ordinal)) uses.Add(new(clip, $"motion set {set.Name}: {role}"));
                break;
            case AssetDocument<EntityAsset> entity:
                var asset = entity.Asset;
                uses.Add(new(asset.Model, "model"));
                foreach (var (role, clip) in asset.Roles.OrderBy(r => r.Key, StringComparer.Ordinal)) uses.Add(new(clip, $"role {role} (override)"));
                foreach (var action in asset.Actions) if (action.Clip is not null) uses.Add(new(action.Clip, $"action {action.Id}"));
                foreach (var binding in asset.Equipment) uses.Add(new(binding.Prop, $"equipment on {binding.Socket}"));
                break;
        }
        return uses;
    }

    /// <summary>Every asset that references <paramref name="id"/> directly, with how. Rebuilt only when a document changes.</summary>
    public IReadOnlyList<AssetReference> UsedBy(string id)
    {
        var revision = Revision;
        if (_usedBy is not { } cached || cached.Revision != revision)
        {
            cached = (revision, _documents.Keys.SelectMany(user => Uses(user).Select(r => (Target: r.Id, Reference: new AssetReference(user, r.Relation))))
                .ToLookup(p => p.Target, p => p.Reference, StringComparer.Ordinal));
            _usedBy = cached;
        }
        return [.. cached.UsedBy[id]];
    }

    /// <summary>Entities standing on a base model, directly or through one of its variants.</summary>
    public IReadOnlyList<AssetDocument<EntityAsset>> EntitiesOn(string modelId) => [.. Entities.Where(e => BaseOf(e.Asset.Model) == modelId)];

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
            case AssetDocument<PropAsset> prop: Try(prop.Asset.Validate); break;
            case AssetDocument<EntityAsset> entity: if (CompileEntity(entity.Id, out var compile) is null) problems.Add(compile!); break;
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
            case AssetDocument<PropAsset> prop: prop.Asset.Validate(); break;
            case AssetDocument<EntityAsset> entity: entity.Asset.Validate(); break;
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
