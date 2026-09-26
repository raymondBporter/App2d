using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>A moment in an action: a clip marker when named, otherwise a normalized time over the clip's duration.</summary>
public sealed record ActionTime
{
    public string? Marker { get; set; }
    public float At { get; set; }
}

/// <summary>
/// An attack region active over [Start, Finish). A box centred on a socket, or on a named point of an equipped prop,
/// pushed Along the anchor's axis. It reads the same resolved transform the prop is drawn with.
/// </summary>
public sealed record HitWindow
{
    public string Id { get; set; } = "hit";
    public ActionTime Start { get; set; } = new();
    public ActionTime Finish { get; set; } = new() { At = 1 };
    public string? Socket { get; set; }
    public string? Prop { get; set; }
    public string Point { get; set; } = PropAsset.TipPoint;
    public float Along { get; set; }
    public float Width { get; set; } = .3f;
    public float Height { get; set; } = .3f;
    public int Damage { get; set; } = 1;
}

/// <summary>A gameplay event bound to an action moment. The controller interprets Id (for example "launch"); Sound, when set, plays.</summary>
public sealed record ActionEvent
{
    public string Id { get; set; } = "";
    public ActionTime At { get; set; } = new();
    public string? Sound { get; set; }
}

/// <summary>
/// An explicitly enabled action. It plays its animation role, or a directly named clip such as a spear thrust. With a
/// <see cref="Mask"/> (a control group of the model) it plays over locomotion instead of replacing it: the action owns the
/// group's channels, locomotion keeps the rest, contacts included, and the controller keeps moving the actor. The masked
/// layer fades in over <see cref="BlendIn"/> and out over the last <see cref="BlendOut"/> seconds of the clip.
/// </summary>
public sealed record EntityActionDef
{
    public string Id { get; set; } = "";
    public string? Role { get; set; }
    public string? Clip { get; set; }
    public string? Mask { get; set; }
    public float BlendIn { get; set; }
    public float BlendOut { get; set; }
    public List<HitWindow> Hits { get; set; } = [];
    public List<ActionEvent> Events { get; set; } = [];
}

/// <summary>The controller and its supported configuration. Speeds are model units per second.</summary>
public sealed record ControllerConfig
{
    public string Kind { get; set; } = EntityControllers.Walker;
    public float WalkSpeed { get; set; } = 1.5f;
    /// <summary>Speed while the run input is held; the run role plays when assigned. Zero never runs.</summary>
    public float RunSpeed { get; set; }
    public float JumpSpeed { get; set; }
    /// <summary>AI: attack within this horizontal distance.</summary>
    public float Range { get; set; } = 1.5f;
    public float Cooldown { get; set; } = 1;
}

/// <summary>The stable body-local movement box. Animation never changes it.</summary>
public sealed record MovementBox
{
    public float Width { get; set; } = .55f;
    public float Height { get; set; } = 1.9f;
    public float OffsetX { get; set; }
}

public sealed record HurtOverride
{
    public bool Disabled { get; set; }
    public float? Pad { get; set; }
}

/// <summary>The model's hurt layout this entity uses, with explicit per-region overrides.</summary>
public sealed record HurtSelection
{
    public string? Layout { get; set; }
    public Dictionary<string, HurtOverride> Regions { get; set; } = [];
}

public sealed record EquipmentBinding
{
    public string Prop { get; set; } = "";
    public string Socket { get; set; } = "";
}

/// <summary>
/// What a character does in the game. References a model or variant and one of its base's motion sets; owns the
/// controller, explicit actions, movement and hurt geometry, equipment and events. No entity inheritance.
/// </summary>
public sealed class EntityAsset
{
    public const string FormatId = "app2d-entity";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string MotionSet { get; set; } = "";
    /// <summary>Role assignments that win over the selected motion set.</summary>
    public Dictionary<string, string> Roles { get; set; } = [];
    public ControllerConfig Controller { get; set; } = new();
    public int Health { get; set; } = 3;
    public MovementBox Movement { get; set; } = new();
    public HurtSelection Hurt { get; set; } = new();
    public List<EquipmentBinding> Equipment { get; set; } = [];
    public List<EntityActionDef> Actions { get; set; } = [];

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static EntityAsset FromJson(string json) { var entity = AuthoredAsset.Parse<EntityAsset>(json, "entity"); entity.Validate(); return entity; }
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    /// <summary>Checks the file on its own. References to models, clips and props are checked when compiling.</summary>
    public void Validate()
    {
        var owner = $"Entity '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "entity id"); AuthoredAsset.RequireId(Model, $"{owner} model"); AuthoredAsset.RequireId(MotionSet, $"{owner} motionSet");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(Roles is not null && Controller is not null && Movement is not null && Hurt is not null && Hurt.Regions is not null && Equipment is not null && Actions is not null,
            $"{owner}: collections cannot be null.");
        foreach (var (role, clip) in Roles) { AuthoredAsset.RequireId(role, $"{owner} roles"); AuthoredAsset.RequireId(clip, $"{owner} roles.{role}"); }
        var spec = EntityControllers.Get(Controller.Kind, $"{owner} controller.kind");
        new Limit(0, 100).Check(Controller.WalkSpeed, $"{owner} controller.walkSpeed"); new Limit(0, 100).Check(Controller.RunSpeed, $"{owner} controller.runSpeed");
        new Limit(0, 100).Check(Controller.JumpSpeed, $"{owner} controller.jumpSpeed"); new Limit(0, 100).Check(Controller.Range, $"{owner} controller.range");
        new Limit(0, 60).Check(Controller.Cooldown, $"{owner} controller.cooldown");
        new Limit(1, 10000).Check(Health, $"{owner} health");
        new Limit(.01f, 100).Check(Movement.Width, $"{owner} movement.width"); new Limit(.01f, 100).Check(Movement.Height, $"{owner} movement.height");
        new Limit(-100, 100).Check(Movement.OffsetX, $"{owner} movement.offsetX");
        if (Hurt.Layout is not null) AuthoredAsset.RequireId(Hurt.Layout, $"{owner} hurt.layout");
        foreach (var (region, change) in Hurt.Regions)
        {
            Require(change is not null, $"{owner} hurt.regions.{region}: null override.");
            if (change.Pad is { } pad) new Limit(0, 10).Check(pad, $"{owner} hurt.regions.{region}.pad");
        }
        var props = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in Equipment)
        {
            Require(binding is not null, $"{owner}: null equipment.");
            AuthoredAsset.RequireId(binding.Prop, $"{owner} equipment prop"); AuthoredAsset.RequireId(binding.Socket, $"{owner} equipment '{binding.Prop}' socket");
            Require(props.Add(binding.Prop), $"{owner}: prop '{binding.Prop}' is equipped twice.");
        }
        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            Require(action is not null && action.Hits is not null && action.Events is not null, $"{owner}: incomplete action.");
            AuthoredAsset.RequireId(action.Id, $"{owner} action id");
            var field = $"{owner} action '{action.Id}'";
            Require(actions.Add(action.Id), $"{owner}: duplicate action '{action.Id}'.");
            Require(spec.Actions.Contains(action.Id), $"{field}: the '{spec.Id}' controller does not support it (supports: {string.Join(", ", spec.Actions)}).");
            Require((action.Role is null) != (action.Clip is null), $"{field}: name exactly one of role or clip.");
            if (action.Role is not null) AuthoredAsset.RequireId(action.Role, field + " role");
            if (action.Clip is not null) AuthoredAsset.RequireId(action.Clip, field + " clip");
            if (action.Mask is not null) AuthoredAsset.RequireId(action.Mask, field + " mask");
            new Limit(0, 5).Check(action.BlendIn, field + " blendIn"); new Limit(0, 5).Check(action.BlendOut, field + " blendOut");
            Require(action.Mask is not null || action.BlendIn == 0 && action.BlendOut == 0, $"{field}: blending needs a mask; a whole-body action replaces locomotion at once.");
            var hits = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hit in action.Hits)
            {
                Require(hit is not null && hit.Start is not null && hit.Finish is not null, $"{field}: incomplete hit window.");
                AuthoredAsset.RequireId(hit.Id, field + " hit id");
                Require(hits.Add(hit.Id), $"{field}: duplicate hit window '{hit.Id}'.");
                Require((hit.Socket is null) != (hit.Prop is null), $"{field} hit '{hit.Id}': anchor to exactly one of socket or prop.");
                if (hit.Prop is not null) { Require(props.Contains(hit.Prop), $"{field} hit '{hit.Id}': prop '{hit.Prop}' is not equipped."); EntityVocabulary.Require(hit.Point, PropAsset.PointNames, $"{field} hit '{hit.Id}' point"); }
                CheckTime(hit.Start, $"{field} hit '{hit.Id}' start"); CheckTime(hit.Finish, $"{field} hit '{hit.Id}' finish");
                new Limit(-100, 100).Check(hit.Along, $"{field} hit '{hit.Id}' along");
                new Limit(.01f, 100).Check(hit.Width, $"{field} hit '{hit.Id}' width"); new Limit(.01f, 100).Check(hit.Height, $"{field} hit '{hit.Id}' height");
                new Limit(0, 10000).Check(hit.Damage, $"{field} hit '{hit.Id}' damage");
            }
            foreach (var cue in action.Events)
            {
                Require(cue is not null && cue.At is not null, $"{field}: incomplete event.");
                AuthoredAsset.RequireId(cue.Id, field + " event id");
                CheckTime(cue.At, $"{field} event '{cue.Id}'");
                if (cue.Sound is not null) AuthoredAsset.RequireId(cue.Sound, $"{field} event '{cue.Id}' sound");
            }
        }
    }

    private static void CheckTime(ActionTime time, string field)
    {
        if (time.Marker is not null) { AuthoredAsset.RequireId(time.Marker, field + " marker"); Require(time.At == 0, $"{field}: use a marker or a normalized time, not both."); }
        else new Limit(0, 1).Check(time.At, field + " at");
    }
}

/// <summary>What a controller kind can do. Its requirements replace a universal required-action list.</summary>
public sealed record ControllerSpec(string Id, IReadOnlyList<string> RequiredRoles, IReadOnlyList<string> Actions, bool Moves, bool Jumps);

/// <summary>
/// The controller kinds and their capabilities. A shared library containing a jump never enables jumping: an entity's
/// jump must be listed in its actions, and only a controller that supports it may list it. Reaction roles "hit" and
/// "death" are optional for every kind; without them the controller holds its current animation.
/// </summary>
public static class EntityControllers
{
    public const string Walker = "walker", Platformer = "platformer", Stationary = "stationary";
    public const string Attack = "attack", Jump = "jump", Hit = "hit", Death = "death";
    public const string Idle = "idle", Walk = "walk", Run = "run", Fall = "fall";
    /// <summary>The action event at which a jump leaves the ground.</summary>
    public const string Launch = "launch";

    private static readonly Dictionary<string, ControllerSpec> Specs = new(StringComparer.Ordinal)
    {
        [Walker] = new(Walker, [Idle, Walk], [Attack], Moves: true, Jumps: false),
        [Platformer] = new(Platformer, [Idle, Walk], [Attack, Jump], Moves: true, Jumps: true),
        [Stationary] = new(Stationary, [Idle], [Attack], Moves: false, Jumps: false),
    };

    public static IReadOnlyCollection<ControllerSpec> All => Specs.Values;
    public static ControllerSpec Get(string? kind, string field) =>
        kind is not null && Specs.TryGetValue(kind, out var spec) ? spec : throw new InvalidDataException($"{field}: unknown controller '{kind}' (known: {string.Join(", ", Specs.Keys)}).");
}
