using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;

namespace App2d.Core.Characters.Authored;

/// <summary>A moment in an action: a clip marker when named, otherwise a normalized time over the clip's duration.</summary>
public sealed record ActionTime
{
    public string? Marker { get; set; }
    public float At { get; set; }
}

/// <summary>
/// An attack's activation window: the half-open interval [Start, Finish) over which the attack collider it names is enabled
/// (the <see cref="ChannelSemantics.EnabledState"/> channel of the entity's timeline). <see cref="Collider"/> names one of the
/// entity's attack colliders; the window's id is its hit group, whose strike is <c>behaviors.strikes</c>.
/// </summary>
public sealed record HitWindow
{
    public string Id { get; set; } = "hit";
    public ActionTime Start { get; set; } = new();
    public ActionTime Finish { get; set; } = new() { At = 1 };
    /// <summary>The attack collider this window enables.</summary>
    public string Collider { get; set; } = "";
}

/// <summary>
/// A shot an action fires on its "fire" event: a box leaving the equipped prop's muzzle along the prop's axis. Speed is model
/// units per second; it flies until it hits, meets terrain or its lifetime ends.
/// </summary>
public sealed record ProjectileDef
{
    public float Speed { get; set; } = 8;
    /// <summary>The bolt's shape around its position; its bounds size the bolt in flight. Its strike is <c>behaviors.strikes</c> under the action's id.</summary>
    public IShape2D Shape { get; set; } = Rectangle2D.Builder.FromSize(.16f, .08f);
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
public sealed record FlightDef
{
    public float HoverHeight { get; set; } = 3.2f;
    public float Speed { get; set; } = 3;
    public float DiveSpeed { get; set; } = 10;
    public float LowSpeed { get; set; } = .8f;
}

public sealed record ControllerConfig
{
    public string Kind { get; set; } = EntityControllers.Walker;
    public FlightDef? Flight { get; set; }
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

/// <summary>A prop equipped on a socket: gameplay equipment (weapons, shields, wearable art with points) as distinct from the model's part instances.</summary>
public sealed record EquipmentBinding
{
    public string Prop { get; set; } = "";
    public string Socket { get; set; } = "";
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
    public const string Walker = "walker";
    public const string Platformer = "platformer";
    public const string Stationary = "stationary";
    public const string Flying = "flying";
    public const string Traversal = "traversal";
    public const string Attack = "attack";
    public const string Jump = "jump";
    public const string Hit = "hit";
    public const string Death = "death";
    public const string Shoot = "shoot";
    public const string WallShot = "wall-shot";
    /// <summary>A second swing played while the weapon is still out from the first.</summary>
    public const string FollowUp = "follow-up";
    public const string Idle = "idle";
    public const string Walk = "walk";
    public const string Run = "run";
    public const string Fall = "fall";
    /// <summary>The action event at which a jump leaves the ground.</summary>
    public const string Launch = "launch";
    /// <summary>The action event at which a shot leaves an equipped prop's muzzle.</summary>
    public const string Fire = "fire";

    private static readonly Dictionary<string, ControllerSpec> Specs = new(StringComparer.Ordinal)
    {
        [Walker] = new(Walker, [Idle, Walk], [Attack], Moves: true, Jumps: false),
        [Platformer] = new(Platformer, [Idle, Walk], [Attack, Jump], Moves: true, Jumps: true),
        [Flying] = new(Flying, [Idle], [Attack], Moves: true, Jumps: false),
        [Stationary] = new(Stationary, [Idle], [Attack], Moves: false, Jumps: false),
        // The game's traversal player: Person2D owns movement and jumping; the entity supplies the actions' timing and geometry.
        [Traversal] = new(Traversal, [Idle, Walk], [Attack, FollowUp, Shoot, WallShot], Moves: true, Jumps: false),
    };

    public static IReadOnlyCollection<ControllerSpec> All => Specs.Values;
    public static ControllerSpec Get(string? kind, string field) =>
        kind is not null && Specs.TryGetValue(kind, out var spec)
        ? spec
        : throw new InvalidDataException($"{field}: unknown controller '{kind}' (known: {string.Join(", ", Specs.Keys)}).");
}
