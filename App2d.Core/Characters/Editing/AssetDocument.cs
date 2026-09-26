namespace App2d.Core.Characters.Editing;

/// <summary>Ordered so saving everything writes what others depend on first.</summary>
public enum AssetKind { Model, Variant, Animation, Prop, Entity }

public static class AssetKinds
{
    public static string Folder(AssetKind kind) => kind switch
    {
        AssetKind.Model => "models", AssetKind.Variant => "variants", AssetKind.Animation => "animations", AssetKind.Prop => "props", _ => "entities",
    };
    public static string Label(AssetKind kind) => kind switch
    {
        AssetKind.Model => "Model", AssetKind.Variant => "Variant", AssetKind.Animation => "Animation", AssetKind.Prop => "Prop", _ => "Entity",
    };
}

/// <summary>
/// One asset's draft: its current state, snapshot undo per document, and saved state. A change opens a transaction that
/// stays open until <see cref="Commit"/>, so a whole drag is one undo step, and the asset is serialized only at those
/// boundaries rather than every frame.
/// </summary>
public abstract class AssetDocument
{
    private readonly Stack<string> _undo = [], _redo = [];
    private string _saved;
    private string? _before;

    protected AssetDocument(AssetKind kind, string id, string? path, string saved) { Kind = kind; Id = id; Path = path; _saved = saved; }

    public AssetKind Kind { get; }
    /// <summary>Stable for the document's life. Renaming an ID is a new asset.</summary>
    public string Id { get; }
    public string? Path { get; private set; }
    public abstract string Name { get; }
    public bool IsNew => Path is null;
    public bool Dirty { get; private set; }
    /// <summary>Increments on every change, so caches keyed on it rebuild only when the asset changed.</summary>
    public int Version { get; private set; }
    public bool CanUndo => _undo.Count > 0 || _before is not null;
    public bool CanRedo => _redo.Count > 0;

    public abstract string Serialize();
    protected abstract void Restore(string json);

    /// <summary>Applies a continuous change, such as one frame of a drag. It joins the open transaction.</summary>
    public void Change(Action change)
    {
        _before ??= Serialize(); change(); Touch();
    }

    /// <summary>Applies a discrete change as its own undo step. If it throws, the document returns to its prior state and the exception propagates.</summary>
    public void Edit(Action change)
    {
        Commit(); var before = Serialize();
        try { change(); }
        catch { Restore(before); Version++; throw; }
        _before = before; Touch(); Commit();
    }

    /// <summary>Closes the open transaction. An edit that changed nothing leaves no undo step.</summary>
    public void Commit()
    {
        if (_before is null) return;
        var now = Serialize();
        if (now != _before) { _undo.Push(_before); _redo.Clear(); }
        _before = null; Dirty = now != _saved;
    }

    public void Undo()
    {
        Commit();
        if (!_undo.TryPop(out var previous)) return;
        _redo.Push(Serialize()); Restore(previous); Touch(); Dirty = previous != _saved;
    }

    public void Redo()
    {
        Commit();
        if (!_redo.TryPop(out var next)) return;
        _undo.Push(Serialize()); Restore(next); Touch(); Dirty = next != _saved;
    }

    /// <summary>Records that the current state was written to <paramref name="path"/>.</summary>
    public void MarkSaved(string path) { Commit(); Path = path; _saved = Serialize(); Dirty = false; }

    private void Touch() { Version++; Dirty = true; }
}

public sealed class AssetDocument<T> : AssetDocument where T : class
{
    private readonly Func<T, string> _serialize;
    private readonly Func<T, string> _name;

    internal AssetDocument(AssetKind kind, T asset, string id, string? path, Func<T, string> serialize, Func<T, string> name)
        : base(kind, id, path, path is null ? "" : serialize(asset)) { Asset = asset; _serialize = serialize; _name = name; }

    /// <summary>The current draft. Undo replaces the instance, so views read it each frame rather than keeping it.</summary>
    public T Asset { get; private set; }
    public override string Name => _name(Asset);
    public override string Serialize() => _serialize(Asset);
    // Drafts may be semantically invalid while being repaired, so restoring parses without validating.
    protected override void Restore(string json) => Asset = AuthoredAsset.Parse<T>(json, AssetKinds.Label(Kind));

    /// <summary>Replaces the draft wholesale; call inside <see cref="AssetDocument.Change"/> or <see cref="AssetDocument.Edit"/>.</summary>
    public void Replace(T asset) => Asset = asset;
}

internal static class AssetDocuments
{
    public static AssetDocument<CharacterModel> Of(CharacterModel model, string? path) => new(AssetKind.Model, model, model.Id, path, m => m.ToJson(), m => m.Name);
    public static AssetDocument<ModelVariant> Of(ModelVariant variant, string? path) => new(AssetKind.Variant, variant, variant.Id, path, v => v.ToJson(), v => v.Name);
    public static AssetDocument<MotionClip> Of(MotionClip clip, string? path) => new(AssetKind.Animation, clip, clip.Id, path, c => c.ToJson(), c => c.Name);
    public static AssetDocument<PropAsset> Of(PropAsset prop, string? path) => new(AssetKind.Prop, prop, prop.Id, path, p => p.ToJson(), p => p.Name);
    public static AssetDocument<EntityAsset> Of(EntityAsset entity, string? path) => new(AssetKind.Entity, entity, entity.Id, path, e => e.ToJson(), e => e.Name);
}
