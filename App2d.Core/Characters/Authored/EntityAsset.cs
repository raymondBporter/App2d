using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>A moment in an action: a clip marker when named, otherwise a normalized time over the clip's duration.</summary>
public sealed record ActionTime
{
    public string? Marker { get; set; }
    public float At { get; set; }
}

/// <summary>
/// An attack region active over [Start, Finish). It is centred on a socket or prop point and pushed Along the anchor's
/// axis. Without an anchor it is fixed to the actor at OffsetX/OffsetY from the feet. Boxes stay world-aligned;
/// capsules follow the anchor's projected XY axis. Circles use Width as their diameter.
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
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float Width { get; set; } = .3f;
    public float Height { get; set; } = .3f;
    public string Shape { get; set; } = "box";
    public int Damage { get; set; } = 1;
    /// <summary>Played where the hit lands; null plays the game's default impact.</summary>
    public string? Sound { get; set; }
}

/// <summary>
/// A shot an action fires on its "fire" event: a box leaving the equipped prop's muzzle along the prop's axis. Speed is model
/// units per second; it flies until it hits, meets terrain or its lifetime ends.
/// </summary>
public sealed record ProjectileDef
{
    public float Speed { get; set; } = 8;
    public float Width { get; set; } = .16f;
    public float Height { get; set; } = .08f;
    public int Damage { get; set; } = 1;
    public float Lifetime { get; set; } = 3;
    /// <summary>Downward acceleration in model units/s²; zero preserves straight shots.</summary>
    public float Gravity { get; set; }
    /// <summary>When positive, solve an arc to the target sampled when the attack starts.</summary>
    public float FlightSeconds { get; set; }
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
    /// <summary>What a "fire" event launches. Required with one, meaningless without.</summary>
    public ProjectileDef? Projectile { get; set; }
    public List<HitWindow> Hits { get; set; } = [];
    public List<ActionEvent> Events { get; set; } = [];
    /// <summary>
    /// A combo: the action another press plays while this one runs (buffered to its end) or its <see cref="Recovery"/> still
    /// holds the weapon out. An action reached this way needs no support from the controller of its own.
    /// </summary>
    public string? Next { get; set; }
    /// <summary>The clip played when the action ends and nothing else takes over, such as putting the weapon away.</summary>
    public string? Recovery { get; set; }
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
    public bool RespectTerrain { get; set; }
    public float VerticalRange { get; set; } = 2;
    public float RetreatRange { get; set; }
    public float RetreatSeconds { get; set; } = .35f;
    public float RetreatPause { get; set; } = .65f;
    public float ChargeSpeed { get; set; }
    public float ChargeStartSeconds { get; set; }
    public float ChargeEndSeconds { get; set; }
    public float BrakeSeconds { get; set; } = .3f;
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

/// <summary>Pose-derived front protection; an action's guard-open event exposes it through recovery.</summary>
public sealed record GuardDef
{
    public string Prop { get; set; } = "";
    public float Width { get; set; } = .9f;
    public float Height { get; set; } = 1.4f;
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
    public GuardDef? Guard { get; set; }
    /// <summary>Role assignments that win over the selected motion set.</summary>
    public Dictionary<string, string> Roles { get; set; } = [];
    public ControllerConfig Controller { get; set; } = new();
    public int Health { get; set; } = 3;
    /// <summary>Divides knockback: a heavy entity barely moves when hit.</summary>
    public float Mass { get; set; } = 1;
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
        Require(Roles is not null && Controller is not null && Movement is not null && Hurt?.Regions is not null && Equipment is not null && Actions is not null,
            $"{owner}: collections cannot be null.");
        foreach (var (role, clip) in Roles) { AuthoredAsset.RequireId(role, $"{owner} roles"); AuthoredAsset.RequireId(clip, $"{owner} roles.{role}"); }
        var spec = EntityControllers.Get(Controller.Kind, $"{owner} controller.kind");
        new Limit(0, 100).Check(Controller.WalkSpeed, $"{owner} controller.walkSpeed"); new Limit(0, 100).Check(Controller.RunSpeed, $"{owner} controller.runSpeed");
        new Limit(0, 100).Check(Controller.JumpSpeed, $"{owner} controller.jumpSpeed"); new Limit(0, 100).Check(Controller.Range, $"{owner} controller.range");
        new Limit(0, 60).Check(Controller.Cooldown, $"{owner} controller.cooldown");
        new Limit(0, 100).Check(Controller.VerticalRange, $"{owner} controller.verticalRange");
        new Limit(0, 100).Check(Controller.RetreatRange, $"{owner} controller.retreatRange");
        new Limit(0, 10).Check(Controller.RetreatSeconds, $"{owner} controller.retreatSeconds");
        new Limit(0, 10).Check(Controller.RetreatPause, $"{owner} controller.retreatPause");
        new Limit(0, 100).Check(Controller.ChargeSpeed, $"{owner} controller.chargeSpeed");
        new Limit(0, 30).Check(Controller.ChargeStartSeconds, $"{owner} controller.chargeStartSeconds");
        new Limit(0, 30).Check(Controller.ChargeEndSeconds, $"{owner} controller.chargeEndSeconds");
        new Limit(.01f, 10).Check(Controller.BrakeSeconds, $"{owner} controller.brakeSeconds");
        Require(Controller.ChargeSpeed == 0 || Controller.ChargeEndSeconds > Controller.ChargeStartSeconds, $"{owner}: charge end must follow charge start.");
        new Limit(1, 10000).Check(Health, $"{owner} health"); new Limit(.1f, 100).Check(Mass, $"{owner} mass");
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
        if (Guard is { } guard)
        {
            Require(props.Contains(guard.Prop), $"{owner}: guard prop '{guard.Prop}' must be equipped.");
            new Limit(.01f, 10).Check(guard.Width, $"{owner} guard.width");
            new Limit(.01f, 10).Check(guard.Height, $"{owner} guard.height");
        }
        var chained = Actions.Where(a => a?.Next is not null).Select(a => a.Next!).ToHashSet(StringComparer.Ordinal);
        foreach (var action in Actions)
        {
            Require(action?.Hits is not null && action.Events is not null, $"{owner}: incomplete action.");
            AuthoredAsset.RequireId(action.Id, $"{owner} action id");
            var field = $"{owner} action '{action.Id}'";
            Require(actions.Add(action.Id), $"{owner}: duplicate action '{action.Id}'.");
            Require(spec.Actions.Contains(action.Id) || chained.Contains(action.Id), $"{field}: the '{spec.Id}' controller does not support it (supports: {string.Join(", ", spec.Actions)}, and any action another names as next).");
            if (action.Next is not null) { AuthoredAsset.RequireId(action.Next, field + " next"); Require(Actions.Any(a => a?.Id == action.Next), $"{field} next: no action '{action.Next}'."); }
            if (action.Recovery is not null) AuthoredAsset.RequireId(action.Recovery, field + " recovery");
            Require((action.Role is null) != (action.Clip is null), $"{field}: name exactly one of role or clip.");
            if (action.Role is not null) AuthoredAsset.RequireId(action.Role, field + " role");
            if (action.Clip is not null) AuthoredAsset.RequireId(action.Clip, field + " clip");
            if (action.Mask is not null) AuthoredAsset.RequireId(action.Mask, field + " mask");
            new Limit(0, 5).Check(action.BlendIn, field + " blendIn"); new Limit(0, 5).Check(action.BlendOut, field + " blendOut");
            Require(action.Mask is not null || action.BlendIn == 0 && action.BlendOut == 0, $"{field}: blending needs a mask; a whole-body action replaces locomotion at once.");
            var fires = action.Events.Any(e => e?.Id == EntityControllers.Fire);
            Require(fires == (action.Projectile is not null), fires ? $"{field}: a '{EntityControllers.Fire}' event needs a projectile." : $"{field}: a projectile needs a '{EntityControllers.Fire}' event to launch it.");
            if (action.Projectile is { } shot)
            {
                new Limit(.1f, 200).Check(shot.Speed, field + " projectile.speed"); new Limit(.01f, 10).Check(shot.Width, field + " projectile.width");
                new Limit(.01f, 10).Check(shot.Height, field + " projectile.height"); new Limit(0, 10000).Check(shot.Damage, field + " projectile.damage");
                new Limit(.05f, 30).Check(shot.Lifetime, field + " projectile.lifetime");
                new Limit(0, 200).Check(shot.Gravity, field + " projectile.gravity");
                new Limit(0, 10).Check(shot.FlightSeconds, field + " projectile.flightSeconds");
            }
            var hits = new HashSet<string>(StringComparer.Ordinal);
            foreach (var hit in action.Hits)
            {
                Require(hit?.Start is not null && hit.Finish is not null, $"{field}: incomplete hit window.");
                AuthoredAsset.RequireId(hit.Id, field + " hit id");
                Require(hits.Add(hit.Id), $"{field}: duplicate hit window '{hit.Id}'.");
                Require(hit.Socket is null || hit.Prop is null, $"{field} hit '{hit.Id}': anchor to a socket or a prop, not both (neither fixes it to the actor).");
                if (hit.Prop is not null) { Require(props.Contains(hit.Prop), $"{field} hit '{hit.Id}': prop '{hit.Prop}' is not equipped."); EntityVocabulary.Require(hit.Point, PropAsset.PointNames, $"{field} hit '{hit.Id}' point"); }
                CheckTime(hit.Start, $"{field} hit '{hit.Id}' start"); CheckTime(hit.Finish, $"{field} hit '{hit.Id}' finish");
                new Limit(-100, 100).Check(hit.Along, $"{field} hit '{hit.Id}' along");
                new Limit(-100, 100).Check(hit.OffsetX, $"{field} hit '{hit.Id}' offsetX"); new Limit(-100, 100).Check(hit.OffsetY, $"{field} hit '{hit.Id}' offsetY");
                new Limit(.01f, 100).Check(hit.Width, $"{field} hit '{hit.Id}' width"); new Limit(.01f, 100).Check(hit.Height, $"{field} hit '{hit.Id}' height");
                Require(hit.Shape is "box" or "circle" or "capsule", $"{field} hit '{hit.Id}': shape must be box, circle or capsule.");
                Require(hit.Shape != "capsule" || hit.Width >= hit.Height, $"{field} hit '{hit.Id}': capsule width must be at least its height (diameter).");
                new Limit(0, 10000).Check(hit.Damage, $"{field} hit '{hit.Id}' damage");
                if (hit.Sound is not null) AuthoredAsset.RequireId(hit.Sound, $"{field} hit '{hit.Id}' sound");
            }
            foreach (var cue in action.Events)
            {
                Require(cue?.At is not null, $"{field}: incomplete event.");
                AuthoredAsset.RequireId(cue.Id, field + " event id");
                CheckTime(cue.At, $"{field} event '{cue.Id}'");
                if (cue.Sound is not null) AuthoredAsset.RequireId(cue.Sound, $"{field} event '{cue.Id}' sound");
            }
        }
    }

    private static void CheckTime(ActionTime time, string field)
    {
        if (time.Marker is not null)
        {
            AuthoredAsset.RequireId(time.Marker, field + " marker");
            Require(time.At == 0, $"{field}: use a marker or a normalized time, not both.");
        }
        else
        {
            new Limit(0, 1).Check(time.At, field + " at");
        }
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
    public const string Walker = "walker", Platformer = "platformer", Stationary = "stationary", Traversal = "traversal";
    public const string Attack = "attack", Jump = "jump", Hit = "hit", Death = "death", Shoot = "shoot", WallShot = "wall-shot";
    /// <summary>A second swing played while the weapon is still out from the first.</summary>
    public const string FollowUp = "follow-up";
    public const string Idle = "idle", Walk = "walk", Run = "run", Fall = "fall";
    /// <summary>The action event at which a jump leaves the ground.</summary>
    public const string Launch = "launch";
    /// <summary>The action event at which a shot leaves an equipped prop's muzzle.</summary>
    public const string Fire = "fire";

    private static readonly Dictionary<string, ControllerSpec> Specs = new(StringComparer.Ordinal)
    {
        [Walker] = new(Walker, [Idle, Walk], [Attack], Moves: true, Jumps: false),
        [Platformer] = new(Platformer, [Idle, Walk], [Attack, Jump], Moves: true, Jumps: true),
        [Stationary] = new(Stationary, [Idle], [Attack], Moves: false, Jumps: false),
        // The game's traversal player: Person2D owns movement and jumping; the entity supplies the actions' timing and geometry.
        [Traversal] = new(Traversal, [Idle, Walk], [Attack, FollowUp, Shoot, WallShot], Moves: true, Jumps: false),
    };

    public static IReadOnlyCollection<ControllerSpec> All => Specs.Values;
    public static ControllerSpec Get(string? kind, string field) =>
        kind is not null && Specs.TryGetValue(kind, out var spec) ? spec : throw new InvalidDataException($"{field}: unknown controller '{kind}' (known: {string.Join(", ", Specs.Keys)}).");
}
