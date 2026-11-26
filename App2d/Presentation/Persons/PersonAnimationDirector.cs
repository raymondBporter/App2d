using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Simulation;
using App2d.Core.Animation;
using App2d.Core.Characters;
using App2d.Core.Characters.Skeletons;
using App2d.Core.Kinematics;
using App2d.Gameplay.Characters;

namespace App2d.Presentation.Persons;

/// <summary>What the player carries: nothing, the sword on its back, or the sword on its back and a pistol in hand.</summary>
public enum PersonGear
{
    None, Sword, Gun
}

/// <summary>
/// The player's character and its clips by name: one for each role below, and the clip and recovery clip of each of its
/// actions under the clip's own name. A missing clip is an error naming it.
/// </summary>
public sealed class PersonMoves
{
    public const string Idle = "idle", Walk = "walk", Run = "run", Jump = "jump", Fall = "fall", Land = "land", Dash = "dash";
    public const string ClimbOn = "climb-on", Climb = "climb", ClimbOff = "climb-off", WallGrip = "wall-grip";
    public const string BalanceForward = "balance-forward", BalanceBackward = "balance-backward";
    public const string Hit = "hit", Death = "death", Celebrate = "celebrate", Sheathe = "sheathe", DownAttack = "down-attack";
    public const string GunAim = "gun-aim", GunShot = "gun-shot", GunWallShot = "gun-wall-shot", GunCharge = "gun-charge", HealGather = "heal-gather";

    /// <summary>Every role the player's character must have a clip for.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Idle, Walk, Run, Jump, Fall, Land, Dash, ClimbOn, Climb, ClimbOff, WallGrip, BalanceForward, BalanceBackward, Hit, Death, Celebrate, Sheathe,
        DownAttack, GunAim, GunShot, GunWallShot, GunCharge, HealGather,
    ];

    private readonly Dictionary<string, SkeletonClip2D> _clips = [with(StringComparer.Ordinal)];

    public PersonMoves(Combatant hero)
    {
        Hero = hero;
        var missing = All.Where(role => hero.Clip(role) is null).ToList();
        if (missing.Count > 0) throw new InvalidDataException($"'{hero.Name}' cannot be the player; it has no clip for: {string.Join(", ", missing)}.");
        foreach (var role in All) _clips[role] = hero.Clip(role)!;
        foreach (var action in hero.Actions)
        {
            _clips[action.Clip.Name] = action.Clip;
            if (action.Recovery is { } recovery) _clips[recovery.Name] = recovery;
        }
    }

    public Combatant Hero { get; }
    public Skeleton2D Skeleton => Hero.Skeleton;
    /// <summary>The action a sword swing plays: the one named, or the first of the chain.</summary>
    public CharacterAction Swing(string? action) => Hero.Action(action ?? "attack")!;
    public SkeletonClip2D this[string name] => _clips[name];
}

/// <summary>
/// What to draw this frame: a base clip at a time, an optional upper-body overlay and the gear worn. <see cref="Planted"/> is
/// false when the gait could not keep pace with the body (see <see cref="PersonAnimationDirector.MaxCadence"/>); contacts
/// should then follow the clip rather than hold world anchors that would stretch the legs.
/// </summary>
public readonly record struct PersonFrame(string Key, SkeletonClip2D Clip, double Seconds, bool Repeat, PoseOverlay? Overlay, PersonGear Gear, bool Planted = true)
{
    /// <summary>The clip that decides where the sword is and how the character is seen: the overlay's when there is one.</summary>
    public SkeletonClip2D PropClip => Overlay?.Clip ?? Clip;
    public double PropSeconds => Overlay?.Seconds ?? Seconds;
}

/// <summary>
/// Chooses the player's authored animation from observed traversal/controller state, the way an animator would sequence
/// it. Presentation only: the simulation never reads it. Positions and speeds are in model units. Walk and run advance by
/// ground distance over their stride, and the climb by height over its rise. State velocities are in world units per second and
/// are divided by <c>worldUnitsPerModelUnit</c>; so feet and hands keep pace with the body;
/// everything else runs on the clock from the moment it starts. Attacks map the controller's timing onto their clips.
/// </summary>
public sealed class PersonAnimationDirector(PersonMoves moves, float worldUnitsPerModelUnit = 1)
{
    /// <summary>Height climbed per climb cycle: two rungs of the ladder the clip is keyed against.</summary>
    public const float ClimbRise = 1.1f;
    private const float TurnSeconds = .3f;
    private readonly float _walkStride = MathF.Abs(moves[PersonMoves.Walk].Stride(moves.Skeleton).X);
    private readonly float _runStride = MathF.Abs(moves[PersonMoves.Run].Stride(moves.Skeleton).X);
    private PersonState2D _state;
    private Vector2f _position;
    private bool _hasState;
    private double _clock, _sinceState, _keyStart, _reactionStart = double.NegativeInfinity, _celebrateStart = double.NegativeInfinity;
    private double _climbStart = double.NegativeInfinity, _climbEnd = double.NegativeInfinity, _airStart, _landUntil = double.NegativeInfinity;
    private double _dashStart;
    private double _cycle;
    private bool _outpaced;
    private string? _reaction;
    private bool _wasClimbing, _wasDashing, _wasGrounded = true, _meleeActive, _sheathePending, _swingLayered;
    private string _key = "", _swing = "", _recovery = PersonMoves.Sheathe;
    private string? _swingAction;
    private float _swingElapsed;
    private double _recoveryStart;

    public PersonMoves Moves => moves;
    public float HardLandingSpeed { get; init; }
    public EquipmentKind2D Equipment { get; set; }
    public string Key => _key;

    public void PlayHit()
    {
        _reaction = PersonMoves.Hit;
        _reactionStart = _clock;
    }
    public void PlayDeath()
    {
        _reaction = PersonMoves.Death;
        _reactionStart = _clock;
    }
    public void PlayCelebrate() => _celebrateStart = _clock;
    public void Reset()
    {
        _reaction = null;
        _reactionStart = _celebrateStart = _climbStart = _climbEnd = _landUntil = double.NegativeInfinity;
        _sheathePending = _meleeActive = false;
        _key = "";
        _hasState = false;
    }

    /// <summary>A new authoritative state; <paramref name="feet"/> is its feet position in model units.</summary>
    public void ApplyState(PersonState2D state, Vector2f feet, double clock)
    {
        if (_hasState)
        {
            // Walk, run and climb advance by distance covered this tick, never faster than MaxCadence times their own pace.
            var dt = Math.Max(0, clock - _clock);
            var (covered, stride) = _key switch
            {
                PersonMoves.Walk when state.IsGrounded => (MathF.Abs(feet.X - _position.X), _walkStride),
                PersonMoves.Run when state.IsGrounded => (MathF.Abs(feet.X - _position.X), _runStride),
                PersonMoves.Climb when state.IsClimbingLadder => (MathF.Abs(feet.Y - _position.Y), ClimbRise),
                _ => (0f, 0f),
            };
            if (stride > 0)
            {
                var duration = moves[_key].Duration;
                var matched = covered / stride * duration;
                var limit = MaxCadence * dt;
                _outpaced = matched > limit + 1e-9;
                _cycle += Math.Min(matched, limit);
            }
        }
        if (state.LandingSpeedThisFrame > 0)
        {
            _landUntil = state.LandingSpeedThisFrame >= HardLandingSpeed
                ? clock + moves[PersonMoves.Land].Duration : double.NegativeInfinity;
        }

        if (state.IsClimbingLadder && !_wasClimbing)
        {
            _climbStart = clock;
        }

        if (!state.IsClimbingLadder && _wasClimbing)
        {
            _climbEnd = clock;
        }

        if (state.IsDashing && !_wasDashing)
        {
            _dashStart = clock;
        }

        if (!state.IsGrounded && _wasGrounded)
        {
            _airStart = clock;
        }

        _wasClimbing = state.IsClimbingLadder;
        _wasDashing = state.IsDashing;
        _wasGrounded = state.IsGrounded;
        _state = state;
        _position = feet;
        _hasState = true;
        _clock = clock;
        _sinceState = 0;
    }

    public void Advance(float dt)
    {
        _clock += dt;
        _sinceState += dt;
    }

    public PersonFrame Frame()
    {
        var s = _state;
        var gear = Equipment switch
        {
            EquipmentKind2D.Sword => PersonGear.Sword,
            EquipmentKind2D.Gun => PersonGear.Gun,
            _ => PersonGear.None
        };
        PersonFrame Play(string id, double seconds, PoseOverlay? overlay = null) => new(id, moves[id], seconds, moves[id].Loop, overlay, gear);
        double Scaled(string id) => Reald.Clamp01((s.Action.ElapsedSeconds + _sinceState) / s.Action.DurationSeconds) * moves[id].Duration;

        var melee = s.Action.IsActive && s.Action.Kind is PlayerAttackKind2D.Melee or PlayerAttackKind2D.Downward or PlayerAttackKind2D.Punch or PlayerAttackKind2D.Kick;
        if (_meleeActive && !melee)
        {
            _sheathePending = Equipment == EquipmentKind2D.Sword;
            _recoveryStart = _clock;
        }
        if (_sheathePending && (Equipment != EquipmentKind2D.Sword || _clock - _recoveryStart >= moves[_recovery].Duration))
        {
            _sheathePending = false;
        }
        // Gameplay names the hero action each swing plays (the attack, or the combo swing it chained to); a new one starts
        // when the action changes or its time restarts. What follows it is that action's recovery clip.
        if (melee && (!_meleeActive || s.Action.Swing != _swingAction || s.Action.ElapsedSeconds < _swingElapsed))
        {
            var swing = moves.Swing(s.Action.Swing);
            _swingAction = s.Action.Swing;
            _swing = swing.Clip.Name;
            _swingLayered = false;
            _recovery = s.Action.Kind == PlayerAttackKind2D.Downward ? PersonMoves.Sheathe : swing.Recovery?.Name ?? PersonMoves.Sheathe;
        }
        _meleeActive = melee;
        _swingElapsed = melee ? s.Action.ElapsedSeconds : 0;

        PersonFrame frame;
        if (!s.IsAlive)
        {
            frame = Play(PersonMoves.Death, _reaction == PersonMoves.Death ? _clock - _reactionStart : moves[PersonMoves.Death].Duration);
        }
        else if (_reaction == PersonMoves.Hit && _clock - _reactionStart < moves[PersonMoves.Hit].Duration)
        {
            frame = Play(PersonMoves.Hit, _clock - _reactionStart);
        }
        else if (s.Action.IsActive && s.Action.Kind == PlayerAttackKind2D.Shot)
        {
            gear = PersonGear.Gun;
            _sheathePending = false;
            frame = s.IsWallGripping ? Play(PersonMoves.GunWallShot, Scaled(PersonMoves.GunWallShot))
                        : Locomotion(gear) with
                        {
                            Overlay = new(moves[PersonMoves.GunShot], Scaled(PersonMoves.GunShot), PersonSkeleton.UpperBody)
                        };
        }
        else if (s.Spells.IsHealing)
        {
            _sheathePending = false;
            frame = Play(PersonMoves.HealGather, s.Spells.HealProgress * moves[PersonMoves.HealGather].Duration) with
            {
                Gear = PersonGear.Sword
            };
        }
        else if (s.IsChargingPrimary)
        {
            _sheathePending = false;
            var progress = s.Spells.Enabled ? s.Spells.ChargeProgress : .5f;
            // Wall casting keeps the gripping arm planted; the standing overlay uses both arms.
            frame = s.IsWallGripping
                ? Play(PersonMoves.GunWallShot, 0) with
                {
                    Gear = PersonGear.Gun
                }
                : Locomotion(PersonGear.Gun) with
                {
                    Overlay = new(moves[PersonMoves.GunCharge], progress * moves[PersonMoves.GunCharge].Duration, PersonSkeleton.UpperBody)
                };
        }
        else if (s.Action.IsActive && s.Action.Kind == PlayerAttackKind2D.Downward)
        {
            _sheathePending = false;
            frame = Play(PersonMoves.DownAttack, Scaled(PersonMoves.DownAttack));
        }
        else if (melee)
        {
            // Swings plant both feet, which drags the legs out behind a moving body. Once the body moves during a swing,
            // the legs keep their gait and the swing plays over them from the waist up until it ends.
            _sheathePending = false;
            _swingLayered |= !s.IsGrounded || s.IsClimbingLadder || s.IsWallGripping || MathF.Abs(s.LinearVelocity.X) / worldUnitsPerModelUnit > WalkSpeedThreshold;
            frame = _swingLayered ? Locomotion(gear) with
            {
                Overlay = new(moves[_swing], Scaled(_swing), PersonSkeleton.UpperBody)
            } : Play(_swing, Scaled(_swing));
        }
        else if (s.IsDashing)
        {
            frame = Play(PersonMoves.Dash, _clock - _dashStart);
        }
        else if (s.IsGrounded && _clock - _celebrateStart < moves[PersonMoves.Celebrate].Duration)
        {
            frame = Play(PersonMoves.Celebrate, _clock - _celebrateStart);
        }
        else
        {
            frame = Locomotion(gear);
        }

        if (frame.Key != _key)
        {
            _key = frame.Key;
            _keyStart = _clock;
            _cycle = 0;
            _outpaced = false;
        }
        // Reactions/traversal that require both arms cancel recovery rather than replaying it later.
        if (_sheathePending && frame.PropClip.Name != _recovery)
        {
            _sheathePending = false;
        }

        return frame;
    }

    private PersonFrame Locomotion(PersonGear gear)
    {
        var s = _state;
        var gun = Equipment == EquipmentKind2D.Gun;
        PoseOverlay? aim = gun ? new(moves[PersonMoves.GunAim], _clock, PersonSkeleton.UpperBody) : null;
        PersonFrame Play(string id, double seconds, PoseOverlay? overlay = null) => new(id, moves[id], seconds, moves[id].Loop, overlay, gear);
        double Clock(string id) => id == _key ? _clock - _keyStart : 0;
        PersonFrame Gait(string id, PoseOverlay? overlay = null) => Play(id, id == _key ? _cycle : 0, overlay) with
        {
            Planted = id != _key || !_outpaced
        };
        PoseOverlay? recovery = _sheathePending ? new(moves[_recovery], _clock - _recoveryStart, PersonSkeleton.UpperBody) : null;

        var speed = MathF.Abs(s.LinearVelocity.X) / worldUnitsPerModelUnit;
        if (s.IsClimbingLadder)
        {
            _sheathePending = false;
            if (_clock - _climbStart < TurnSeconds)
            {
                return Play(PersonMoves.ClimbOn, _clock - _climbStart);
            }

            return Gait(PersonMoves.Climb);
        }
        if (s.IsGrounded && _clock - _climbEnd < TurnSeconds)
        {
            return Play(PersonMoves.ClimbOff, _clock - _climbEnd);
        }

        if (s.IsWallGripping)
        {
            _sheathePending = false;
            return Play(PersonMoves.WallGrip, Clock(PersonMoves.WallGrip));
        }
        if (!s.IsGrounded)
        {
            return s.LinearVelocity.Y > 0 ? Play(PersonMoves.Jump, _clock - _airStart, recovery ?? aim) : Play(PersonMoves.Fall, Clock(PersonMoves.Fall), recovery ?? aim);
        }
        if (speed > WalkSpeedThreshold)
        {
            var run = speed > RunThreshold;
            return Gait(run ? PersonMoves.Run : PersonMoves.Walk, recovery ?? aim);
        }
        if (_sheathePending)
        {
            return Play(_recovery, _clock - _recoveryStart);
        }

        if (_clock < _landUntil)
        {
            return Play(PersonMoves.Land, _clock - (_landUntil - moves[PersonMoves.Land].Duration), aim);
        }

        if (s.BalanceDirection != 0)
        {
            var edge = s.BalanceDirection == Math.Sign(s.Facing) ? PersonMoves.BalanceForward : PersonMoves.BalanceBackward;
            return Play(edge, Clock(edge));
        }
        if (gun)
        {
            return Play(PersonMoves.GunAim, Clock(PersonMoves.GunAim));
        }

        return Play(PersonMoves.Idle, Clock(PersonMoves.Idle));
    }

    /// <summary>
    /// The fastest a gait cycle may play, as a multiple of its authored pace. The game's movement outruns the run's stride
    /// by far (430 px/s is several times the run's own pace at the player's drawn size), so distance matching stops here.
    /// </summary>
    public double MaxCadence { get; set; } = 2.5;
    /// <summary>Ground speed, in model units per second, above which the body walks rather than stands.</summary>
    public float WalkSpeedThreshold { get; set; } = .12f;
    /// <summary>Above this ground speed the run plays: halfway between the walk's and the run's own paces.</summary>
    public float RunThreshold => (_walkStride / moves[PersonMoves.Walk].Duration + _runStride / moves[PersonMoves.Run].Duration) / 2;
}
