using System.Numerics;

namespace App2d.Core.Characters.Editing;

/// <summary>Model edits rest geometry and appearance; Animate edits motion; Entity edits what a character does in the game.</summary>
public enum Workspace { Model, Animate, Entity }

/// <summary>What the author has picked. Session state, never saved into assets.</summary>
public sealed class Selection
{
    public string? Control { get; set; }
    public string? Part { get; set; }
    public string? Chain { get; set; }
    /// <summary>A selected key time on the timeline.</summary>
    public float? Key { get; set; }
    /// <summary>Entity workspace: the action, and the hit window inside it, being edited.</summary>
    public string? Action { get; set; }
    public string? Hit { get; set; }
    public void Clear() { Control = Part = Chain = Action = Hit = null; Key = null; }
}

/// <summary>A model or variant evaluated for display. <see cref="Clip"/> is null when it shows rest. <see cref="Entity"/>, when set, draws its equipment too.</summary>
public sealed record Subject(string Id, ResolvedModel Model, EvaluatedPose Pose, MotionClip? Clip, ResolvedEntity? Entity = null);

/// <summary>
/// An entity previewed in the Entity workspace at the transport time: its final pose and every region derived from it, as the
/// game would build them. <see cref="Action"/> is the previewed action; <see cref="Weight"/> its masked layer's weight.
/// </summary>
public sealed record EntityPreview(ResolvedEntity Entity, ActorPose Pose, float Time, ResolvedAction? Action, float Weight,
    EntityRegion Movement, IReadOnlyList<EntityRegion> Hurt, IReadOnlyList<(ResolvedHit Hit, EntityRegion Region)> Attacks);

/// <summary>
/// One editing session over a workspace: the open subject and clip, the active workspace, the transport, selection and
/// compare pins. Every command the views issue lands here, so the UI stays a thin layer and tests drive the same paths.
/// Commands report failures through <see cref="Message"/> instead of throwing.
/// </summary>
public sealed class EditorSession
{
    private MotionClip? _pending;
    private (string Clip, int Version, float Time) _pendingFor;
    private bool _dragging;

    public EditorSession(AuthoringWorkspace assets)
    {
        Assets = assets;
        var first = assets.Variants.Select(v => v.Id).Concat(assets.Models.Select(m => m.Id)).Order(StringComparer.Ordinal).FirstOrDefault();
        if (first is not null) Open(first);
    }

    public AuthoringWorkspace Assets { get; }
    public Transport Transport { get; } = new();
    public Selection Selection { get; } = new();
    public Workspace Mode { get; private set; }
    /// <summary>The model or variant shown; edited in Model, used as the preview build in Animate.</summary>
    public string? SubjectId { get; private set; }
    /// <summary>The clip animated in Animate and previewed in Model.</summary>
    public string? ClipId { get; private set; }
    /// <summary>The entity edited in Entity.</summary>
    public string? EntityId { get; private set; }
    /// <summary>Entity workspace: the locomotion role previewed, and under a masked action the role it layers over.</summary>
    public string PreviewRole { get; private set; } = EntityControllers.Idle;
    /// <summary>Entity workspace: the action previewed, or null to preview <see cref="PreviewRole"/>.</summary>
    public string? PreviewAction { get; private set; }
    /// <summary>Further models or variants drawn beside the subject at the same world scale and phase.</summary>
    public List<string> Compare { get; } = [];
    public bool AutoKey { get; set; } = true;
    public bool MoveChildren { get; set; } = true;
    /// <summary>Model workspace: control handles and IK setup instead of drawing-part selection. Shows rest.</summary>
    public bool EditRig { get; set; }
    public bool ShowRest { get; set; }
    /// <summary>A gameplay expression override, as the game would supply it.</summary>
    public string? Expression { get; set; }
    public string Message { get; private set; } = "";
    public bool MessageIsError { get; private set; }

    public AssetDocument? ActiveDocument => Mode switch { Workspace.Model => Assets.Find(SubjectId), Workspace.Animate => Assets.Find(ClipId), _ => Assets.Find(EntityId) };
    public AssetDocument<EntityAsset>? EntityDocument => Assets.Entity(EntityId);
    /// <summary>The open entity compiled against the drafts, or null with <see cref="EntityError"/> saying why.</summary>
    public ResolvedEntity? Entity => Assets.CompileEntity(EntityId, out _);
    public string? EntityError { get { Assets.CompileEntity(EntityId, out var error); return error; } }
    public AssetDocument<CharacterModel>? SubjectModel => Assets.Model(SubjectId);
    public AssetDocument<ModelVariant>? SubjectVariant => Assets.Variant(SubjectId);
    public AssetDocument<MotionClip>? ClipDocument => Assets.Clip(ClipId);
    public bool HasPendingPose => PendingValid();

    // ---- Opening -------------------------------------------------------------------------------------------------

    /// <summary>Opens an asset in the workspace that edits it: models and variants in Model, clips in Animate, entities in Entity.</summary>
    public void Open(string id)
    {
        CommitAll();
        switch (Assets.Find(id))
        {
            case AssetDocument<MotionClip> clip:
                Mode = Workspace.Animate; SetClip(clip.Id);
                break;
            case AssetDocument<EntityAsset> entity:
                Mode = Workspace.Entity; SetEntity(entity.Id);
                break;
            case AssetDocument<PropAsset>: Report($"'{id}' is a prop: props are edited as files and shown through equipment.", true); return;
            case AssetDocument document:
                Mode = Workspace.Model; SetSubject(document.Id);
                break;
            default: Report($"No asset '{id}'.", true); return;
        }
    }

    public void SetMode(Workspace mode)
    {
        CommitAll(); Mode = mode;
        if (mode == Workspace.Animate && ClipId is null) SetClip(Assets.ClipsFor(SubjectId ?? "").Select(c => c.Id).Order(StringComparer.Ordinal).FirstOrDefault());
        if (mode == Workspace.Entity)
        {
            // Prefer an entity on the open subject, so switching from Model to Entity lands on something related.
            if (EntityId is null || Assets.Entity(EntityId) is null)
                SetEntity(Assets.Entities.OrderBy(e => Assets.BaseOf(e.Asset.Model) == Assets.BaseOf(SubjectId ?? "") ? 0 : 1).ThenBy(e => e.Id, StringComparer.Ordinal).FirstOrDefault()?.Id);
            else Transport.Seek(TransportClip, Transport.Time);
        }
    }

    /// <summary>Opens an entity: its model becomes the subject, so Model and Animate stay one click away.</summary>
    public void SetEntity(string? id)
    {
        var entity = Assets.Entity(id);
        EntityId = entity?.Id; PreviewAction = null; PreviewRole = EntityControllers.Idle; Selection.Clear();
        if (entity is not null && Assets.BaseOf(entity.Asset.Model) is not null && entity.Asset.Model != SubjectId)
        {
            var sameBase = SubjectId is not null && Assets.BaseOf(SubjectId) == Assets.BaseOf(entity.Asset.Model);
            SubjectId = entity.Asset.Model; if (!sameBase) Compare.Clear();
            if (ClipId is null || Assets.Clip(ClipId)?.Asset.Model != Assets.BaseOf(SubjectId)) ClipId = Assets.ClipsFor(SubjectId).Select(c => c.Id).Order(StringComparer.Ordinal).FirstOrDefault();
        }
        Transport.Pause(); Transport.Seek(TransportClip, 0);
    }

    /// <summary>Previews a locomotion role; an open masked action keeps playing over it.</summary>
    public void PreviewRoleOf(string role) { PreviewRole = role; if (PreviewAction is null) Transport.Seek(TransportClip, 0); }

    /// <summary>Previews an action from its start, or the locomotion role again with null.</summary>
    public void PreviewActionOf(string? action)
    {
        PreviewAction = action; Selection.Action = action; Selection.Hit = null;
        Transport.Pause(); Transport.Seek(TransportClip, 0);
    }

    public void SetSubject(string id)
    {
        if (Assets.BaseOf(id) is null) { Report($"'{id}' is not a model or variant.", true); return; }
        var sameBase = SubjectId is not null && Assets.BaseOf(SubjectId) == Assets.BaseOf(id);
        SubjectId = id; Selection.Clear();
        if (!sameBase) Compare.Clear();
        if (ClipId is null || Assets.Clip(ClipId)?.Asset.Model != Assets.BaseOf(id))
            ClipId = Assets.ClipsFor(id).Select(c => c.Id).Order(StringComparer.Ordinal).FirstOrDefault();
        Transport.Seek(ClipDocument?.Asset, Transport.Time);
    }

    /// <summary>Selects a clip. The subject stays when the clip plays on it, so switching preserves the build, phase and camera.</summary>
    public void SetClip(string? id)
    {
        var clip = Assets.Clip(id);
        ClipId = clip?.Id;
        if (clip is not null && (SubjectId is null || Assets.BaseOf(SubjectId) != clip.Asset.Model)) { SubjectId = clip.Asset.Model; Compare.Clear(); Selection.Clear(); }
        var phase = clip is null ? 0 : Transport.Time / Math.Max(clip.Asset.Duration, 1e-3f);
        Transport.Seek(clip?.Asset, clip is null ? 0 : Math.Clamp(phase, 0, 1) * clip.Asset.Duration);
    }

    public void Pin(string id)
    {
        if (id == SubjectId || Compare.Contains(id)) return;
        if (Assets.BaseOf(id) != Assets.BaseOf(SubjectId ?? "")) { Report($"'{id}' has a different base; compare pins share one structure.", true); return; }
        if (Compare.Count >= 2) Compare.RemoveAt(0);
        Compare.Add(id);
    }

    // ---- Evaluation ----------------------------------------------------------------------------------------------

    /// <summary>The clip a subject plays in the current workspace, or null for rest. Unkeyed pose edits show until the time changes.</summary>
    public MotionClip? ClipFor(string subjectId)
    {
        if (Mode == Workspace.Entity) return null;
        if (Mode == Workspace.Model && (EditRig || ShowRest)) return null;
        if (ClipId is null || !Assets.CanPlay(ClipId, subjectId, out _)) return null;
        return PendingValid() ? _pending : ClipDocument!.Asset;
    }

    public Subject? Evaluate(string subjectId)
    {
        var model = Assets.Resolve(subjectId);
        if (model is null) return null;
        var clip = ClipFor(subjectId); var input = new PoseInput(Expression);
        var pose = clip is null ? PoseEvaluator.Rest(model, input)
            : PoseEvaluator.Sample(model, clip, Transport.Playing ? Transport.Seconds(clip) : Transport.Time, repeat: Transport.Playing, input);
        return new(subjectId, model, pose, clip);
    }

    /// <summary>The subject, then its compare pins, all at the same transport time. In Entity, the previewed entity alone.</summary>
    public IReadOnlyList<Subject> Scene()
    {
        if (Mode == Workspace.Entity)
            return EvaluateEntity() is { } preview ? [new(preview.Entity.Id, preview.Entity.Model, preview.Pose.Local, TransportClip, preview.Entity)] : [];
        return [.. new[] { SubjectId }.Concat(Compare).OfType<string>().Select(Evaluate).OfType<Subject>()];
    }

    /// <summary>The clip the one transport runs: the open clip, or in Entity the previewed action or role.</summary>
    public MotionClip? TransportClip => Mode != Workspace.Entity ? ClipDocument?.Asset
        : Entity is not { } entity ? null
        : PreviewAction is { } action && entity.Actions.TryGetValue(action, out var resolved) ? resolved.Clip : entity.Clip(PreviewRole);

    /// <summary>
    /// The open entity at the transport time, in authored preview: clip travel shown, facing +X, feet at the origin. An action
    /// plays from its start; a masked one plays over <see cref="PreviewRole"/> at the same time. Hit windows follow the
    /// runtime's half-open rule. Scrubbing never dispatches events.
    /// </summary>
    public EntityPreview? EvaluateEntity()
    {
        if (Entity is not { } entity) return null;
        var action = PreviewAction is { } id ? entity.Actions.GetValueOrDefault(id) : null;
        var time = Transport.Time; var weight = 1f;
        EvaluatedPose local;
        if (action is null)
        {
            var clip = entity.Clip(PreviewRole);
            local = PoseEvaluator.Sample(entity.Model, clip, clip is not null && Transport.Playing ? Transport.Seconds(clip) : time, repeat: Transport.Playing, new(Expression));
        }
        else if (action.Mask is { } mask)
        {
            weight = action.Weight(time);
            local = PoseEvaluator.Sample(entity.Model, entity.Clip(PreviewRole), time, repeat: true, new(Expression) { Overlay = new(action.Clip, time, mask, weight) });
        }
        else local = PoseEvaluator.Sample(entity.Model, action.Clip, time, input: new(Expression));
        var pose = new ActorPose(local, Vector2.Zero, 1);
        var attacks = action is null ? [] : action.Hits.Where(h => time >= h.Start && time < h.Finish).Select(h => (h, EntityCollision.Attack(entity, pose, h))).ToList();
        var movement = EntityCollision.Movement(entity, new(local.Locomotion.X, 0), 1);
        return new(entity, pose, time, action, weight, movement, EntityCollision.Hurt(entity, pose), attacks);
    }

    public void Tick(float seconds)
    {
        if (TransportClip is { } clip) Transport.Advance(clip, seconds);
        else Transport.Pause();
    }

    public void TogglePlay() { if (TransportClip is { } clip) { CommitAll(); Transport.Toggle(clip); } }
    public void Seek(float time) => Transport.Seek(TransportClip, time);

    // ---- Gestures ------------------------------------------------------------------------------------------------

    /// <summary>Starts a viewport drag. Everything until <see cref="EndDrag"/> is one undo step.</summary>
    public void BeginDrag() { Transport.Pause(); _dragging = true; }
    public void EndDrag() { _dragging = false; CommitAll(); }

    /// <summary>
    /// Moves a control to a world position. In Model it changes rest geometry: the base's, or the variant's overrides. In
    /// Animate it poses the clip at the current time, keyed when autokey is on and held as an unkeyed pose otherwise.
    /// </summary>
    public void DragControl(string control, Vector3 world)
    {
        if (!_dragging) BeginDrag();
        Attempt(() =>
        {
            if (Mode == Workspace.Model)
            {
                if (SubjectVariant is { } variant)
                {
                    var resolved = Assets.Resolve(variant.Id) ?? throw new InvalidDataException("The variant does not resolve; repair it first.");
                    variant.Change(() => ModelAuthoring.MoveRest(resolved, variant.Asset, control, world, MoveChildren));
                }
                else if (SubjectModel is { } model) model.Change(() => ModelAuthoring.MoveRest(model.Asset, control, world, MoveChildren));
                return;
            }
            var clip = ClipDocument ?? throw new InvalidOperationException("Choose an animation to pose.");
            var subject = Assets.Resolve(SubjectId) ?? throw new InvalidDataException("The preview model does not resolve.");
            if (!Assets.CanPlay(clip.Id, SubjectId!, out var error)) throw new InvalidDataException(error);
            var time = Transport.Time;
            if (AutoKey)
            {
                clip.Change(() =>
                {
                    var pose = PoseEvaluator.Sample(subject, clip.Asset, time);
                    ClipAuthoring.Pose(subject, clip.Asset, pose, time, control, world);
                });
                return;
            }
            if (!PendingValid()) { _pending = MotionClip.FromJson(clip.Serialize()); }
            ClipAuthoring.Pose(subject, _pending!, PoseEvaluator.Sample(subject, _pending!, time), time, control, world);
            _pendingFor = (clip.Id, clip.Version, time);
        });
    }

    /// <summary>Keys every channel at the current time, including an unkeyed pose held from dragging with autokey off.</summary>
    public void KeyPose()
    {
        var clip = ClipDocument;
        if (clip is null) return;
        var pending = PendingValid() ? _pending : null; var time = Transport.Time;
        Attempt(() => clip.Edit(() => { if (pending is not null) clip.Replace(pending); ClipAuthoring.KeyPose(clip.Asset, time); }), "Keyed pose at " + time.ToString("F3") + "s");
        _pending = null;
    }

    public void DiscardPendingPose() => _pending = null;

    private bool PendingValid() =>
        _pending is not null && ClipDocument is { } clip && _pendingFor.Clip == clip.Id && _pendingFor.Version == clip.Version && MathF.Abs(_pendingFor.Time - Transport.Time) < ClipAuthoring.SameTime;

    // ---- Documents -----------------------------------------------------------------------------------------------

    /// <summary>Applies a discrete edit to a document as one undo step, reporting instead of throwing when it is refused.</summary>
    public bool Edit(AssetDocument? document, Action change, string? success = null)
    {
        if (document is null) { Report("Nothing is open to edit.", true); return false; }
        return Attempt(() => document.Edit(change), success);
    }

    /// <summary>Applies one frame of a continuous edit, such as a slider drag. Commit closes it.</summary>
    public void Change(AssetDocument? document, Action change) { if (document is not null) Attempt(() => document.Change(change)); }

    /// <summary>Closes every open transaction; call once no widget or drag is active.</summary>
    public void CommitAll() { if (_dragging) return; foreach (var document in Assets.Documents) document.Commit(); }

    public void Undo() { var document = ActiveDocument; if (document is null) return; _pending = null; document.Undo(); KeepSelection(); }
    public void Redo() { var document = ActiveDocument; if (document is null) return; _pending = null; document.Redo(); KeepSelection(); }

    /// <summary>After undo, drops an entity selection or preview that no longer exists instead of pointing at nothing.</summary>
    private void KeepSelection()
    {
        if (EntityDocument?.Asset is not { } entity) return;
        if (Selection.Action is { } action && entity.Actions.All(a => a.Id != action)) { Selection.Action = Selection.Hit = null; }
        if (PreviewAction is { } previewed && entity.Actions.All(a => a.Id != previewed)) PreviewAction = null;
    }

    public bool Save(AssetDocument? document)
    {
        if (document is null) return false;
        return Attempt(() =>
        {
            var report = Assets.Save(document);
            var text = "Saved " + report.Path;
            if (report.Updated.Count > 0) text += $". Moved {report.Updated.Count} clip(s) to the new structure revision; save them too: {string.Join(", ", report.Updated.Select(d => d.Id))}";
            if (report.Problems.Count > 0) text += ". Needs repair: " + report.Problems[0];
            Report(text, report.Problems.Count > 0);
        });
    }

    /// <summary>Saves each dirty document in turn. Not atomic across files; the message lists what was written.</summary>
    public void SaveAll()
    {
        var saved = new List<string>();
        // Models first: saving one can move its clips to a new structure revision, which makes them dirty in turn.
        while (Assets.DirtyDocuments.OrderBy(d => d.Kind).FirstOrDefault() is { } next)
        {
            if (!Attempt(() => Assets.Save(next))) return;
            saved.Add(next.Id);
        }
        Report(saved.Count == 0 ? "Nothing to save." : "Saved " + string.Join(", ", saved));
    }

    // ---- New assets ----------------------------------------------------------------------------------------------

    /// <summary>A new base model: <c>empty</c> or the <c>person</c> template.</summary>
    public bool NewModel(string id, string name, string template) => Attempt(() =>
    {
        Assets.Create(template == "person" ? ModelAuthoring.FromPerson(id, name) : ModelAuthoring.Empty(id, name)); Open(id);
        if (template != "person") EditRig = true;
    }, $"Created model '{id}'.");

    /// <summary>A new variant of a base, optionally starting from one of its build presets.</summary>
    public bool NewVariant(string id, string name, string baseId, string? preset = null) => Attempt(() =>
    {
        var basis = Assets.Model(baseId) ?? throw new InvalidDataException($"No base model '{baseId}'.");
        var variant = new ModelVariant { Id = id, Name = name, Base = baseId };
        if (preset is not null)
        {
            var rule = BuildRules.For(basis.Asset) ?? throw new InvalidDataException($"'{baseId}' exposes no build presets.");
            var values = (rule.Presets.FirstOrDefault(p => p.Id == preset) ?? throw new InvalidDataException($"No preset '{preset}'.")).Values;
            foreach (var (value, amount) in values) if (amount != (rule.Values.First(v => v.Id == value).Default)) variant.Build[value] = amount;
        }
        Assets.Create(variant); Open(id);
    }, $"Created variant '{id}'.");

    /// <summary>A sibling of a variant, referencing the same base; never a chain of variants.</summary>
    public bool DuplicateVariant(string sourceId, string id, string name) => Attempt(() =>
    {
        var source = Assets.Variant(sourceId) ?? throw new InvalidDataException($"No variant '{sourceId}'.");
        var copy = AuthoredAsset.Parse<ModelVariant>(source.Serialize(), "variant"); copy.Id = id; copy.Name = name;
        Assets.Create(copy); Open(id);
    }, $"Created variant '{id}'.");

    /// <summary>Flattens a variant into a new independent base. Its clips are not carried over: compatibility must be established explicitly.</summary>
    public bool MakeIndependent(string variantId, string id, string name) => Attempt(() =>
    {
        var resolved = Assets.Resolve(variantId, out var error) ?? throw new InvalidDataException(error);
        Assets.Create(ModelAuthoring.Flatten(resolved, id, name)); Open(id);
    }, $"Created independent model '{id}'. Its animations must be copied or converted explicitly.");

    public bool NewClip(string id, string name, float duration = 1, bool loop = true) => Attempt(() =>
    {
        var preview = Assets.Resolve(SubjectId, out var error) ?? throw new InvalidDataException(error ?? "Open a model or variant first.");
        Assets.Create(ClipAuthoring.New(preview, id, name, duration, loop)); Open(id);
    }, $"Created animation '{id}'.");

    /// <summary>A deliberate specialized copy of a shared clip.</summary>
    public bool DuplicateClip(string sourceId, string id, string name) => Attempt(() =>
    {
        var source = Assets.Clip(sourceId) ?? throw new InvalidDataException($"No animation '{sourceId}'.");
        Assets.Create(ClipAuthoring.Duplicate(source.Asset, id, name)); Open(id);
    }, $"Created animation '{id}'.");

    /// <summary>A new entity on a model or variant, starting from a template's gameplay defaults.</summary>
    public bool NewEntity(string id, string name, string modelId, string template) => Attempt(() =>
    {
        var model = Assets.Resolve(modelId, out var error) ?? throw new InvalidDataException(error);
        Assets.Create(EntityAuthoring.New(template, id, name, model)); Open(id);
    }, $"Created entity '{id}' on '{modelId}'.");

    /// <summary>A sibling entity keeping every model, clip and prop reference.</summary>
    public bool DuplicateEntity(string sourceId, string id, string name) => Attempt(() =>
    {
        var source = Assets.Entity(sourceId) ?? throw new InvalidDataException($"No entity '{sourceId}'.");
        Assets.Create(EntityAuthoring.Duplicate(source.Asset, id, name)); Open(id);
    }, $"Created entity '{id}'.");

    /// <summary>Sets an entity's own assignment for a role, or with null removes it so the selected motion set's applies again.</summary>
    public bool SetEntityRole(string role, string? clipId)
    {
        var document = EntityDocument;
        return Edit(document, () =>
        {
            AuthoredAsset.RequireId(role, "role");
            if (clipId is null) { document!.Asset.Roles.Remove(role); return; }
            var clip = Assets.Clip(clipId) ?? throw new InvalidDataException($"No animation '{clipId}'.");
            if (clip.Asset.Model != Assets.BaseOf(document!.Asset.Model)) throw new InvalidDataException($"Animation '{clipId}' is for model '{clip.Asset.Model}', not '{Assets.BaseOf(document.Asset.Model)}'.");
            document.Asset.Roles[role] = clipId;
        }, clipId is null ? $"Role '{role}' follows the motion set again." : $"Role '{role}' overridden with '{clipId}'.");
    }

    /// <summary>Sets the entity's movement box around its model's rest pose. Explicit: animation never changes it.</summary>
    public bool FitMovement()
    {
        var document = EntityDocument;
        return Edit(document, () =>
        {
            var model = Assets.Resolve(document!.Asset.Model, out var error) ?? throw new InvalidDataException(error);
            var fitted = EntityAuthoring.FitMovement(model);
            document.Asset.Movement = fitted with { OffsetX = document.Asset.Movement.OffsetX };
        }, "Fitted the movement box to the rest pose.");
    }

    /// <summary>A new motion set on a base model, empty or copying another set's assignments.</summary>
    public bool NewMotionSet(string baseId, string id, string name, string? copyFrom) =>
        Edit(Assets.Model(baseId), () => EntityAuthoring.AddMotionSet(Assets.Model(baseId)!.Asset, id, name, copyFrom), $"Added motion set '{name}'.");

    /// <summary>Removes a motion set, refusing while any entity selects it.</summary>
    public bool RemoveMotionSet(string baseId, string setId)
    {
        var users = Assets.EntitiesOn(baseId).Where(e => e.Asset.MotionSet == setId).Select(e => e.Id).ToList();
        if (users.Count > 0) { Report($"Motion set '{setId}' is selected by {string.Join(", ", users)}; choose another set there first.", true); return false; }
        return Edit(Assets.Model(baseId), () => Assets.Model(baseId)!.Asset.MotionSets.RemoveAll(s => s.Id == setId), $"Removed motion set '{setId}'.");
    }

    /// <summary>Assigns a clip to a role in a base's motion set, or unassigns it with null.</summary>
    public bool AssignRole(string baseId, string setId, string role, string? clipId)
    {
        var model = Assets.Model(baseId);
        return Edit(model, () =>
        {
            var clip = clipId is null ? null : Assets.Clip(clipId)?.Asset ?? throw new InvalidDataException($"No animation '{clipId}'.");
            EntityAuthoring.Assign(model!.Asset, setId, role, clip);
        });
    }

    /// <summary>Writes one of the base's looks onto the open variant as overrides.</summary>
    public bool ApplyLook(string lookId)
    {
        var variant = SubjectVariant;
        return Edit(variant, () => EntityAuthoring.ApplyLook(Assets.Model(variant!.Asset.Base)?.Asset ?? throw new InvalidDataException($"Variant '{variant.Id}' has no base."), variant.Asset, lookId), $"Applied look '{lookId}'.");
    }

    /// <summary>Saves the open variant's colors, faces and visibility as a look on its base. The base document becomes dirty.</summary>
    public bool SaveLook(string lookId, string name)
    {
        var variant = SubjectVariant; var model = Assets.Model(variant?.Asset.Base);
        if (variant is null || model is null) { Report("Open a variant with a base to save its look.", true); return false; }
        return Edit(model, () => EntityAuthoring.SaveLook(model.Asset, variant.Asset, lookId, name), $"Saved look '{name}' on base '{model.Name}'; save the base to keep it.");
    }

    // ---- Messages ------------------------------------------------------------------------------------------------

    public void Report(string message, bool error = false) { Message = message; MessageIsError = error; }

    private bool Attempt(Action action, string? success = null)
    {
        try { action(); if (success is not null) Report(success); return true; }
        catch (Exception ex) when (AuthoringWorkspace.IsAssetError(ex) || ex is IOException or UnauthorizedAccessException) { Report(ex.Message, true); return false; }
    }
}
