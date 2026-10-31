using App2d.Contracts.Persons;
using App2d.Contracts.Persons.Actions;
using App2d.Contracts.Simulation;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Presentation.Persons;

/// <summary>
/// The authored Person and the player move set, resolved once from the player profile (P9c): each semantic role
/// (<see cref="PersonRoles"/>) bound to the clip the profile names, the hero entity's action clips by id, the gear props by
/// role. Missing clips or props are an error naming them. The role constants here are the roles, not clip ids.
/// </summary>
public sealed class PersonMoves
{
    public const string GunCharge = PersonRoles.GunCharge;
    public const string HealGather = PersonRoles.HealGather;
    public const string Idle = PersonRoles.Idle;
    public const string Walk = PersonRoles.Walk;
    public const string Run = PersonRoles.Run;
    public const string Jump = PersonRoles.Jump;
    public const string Fall = PersonRoles.Fall;
    public const string Land = PersonRoles.Land;
    public const string Dash = PersonRoles.Dash;
    public const string ClimbOn = PersonRoles.ClimbOn;
    public const string Climb = PersonRoles.Climb;
    public const string ClimbOff = PersonRoles.ClimbOff;
    public const string WallGrip = PersonRoles.WallGrip;
    public const string BalanceForward = PersonRoles.BalanceForward;
    public const string BalanceBackward = PersonRoles.BalanceBackward;
    public const string Hit = PersonRoles.Hit;
    public const string Death = PersonRoles.Death;
    public const string Celebrate = PersonRoles.Celebrate;
    public const string Sheathe = PersonRoles.Sheathe;
    public const string DownAttack = PersonRoles.DownAttack;
    public const string GunAim = PersonRoles.GunAim;
    public const string GunShot = PersonRoles.GunShot;
    public const string GunWallShot = PersonRoles.GunWallShot;
    public static IReadOnlyList<string> All => PersonRoles.All;

    private PersonMoves(GameProfile profile, ResolvedModel model, ResolvedEntity hero, Dictionary<string, MotionClip> clips, Dictionary<string, Part> props)
    {
        Profile = profile;
        Model = model;
        Hero = hero;
        Clips = clips;
        Props = props;
    }

    /// <summary>The profile the bindings came from.</summary>
    public GameProfile Profile { get; }
    /// <summary>The gear props by role.</summary>
    public PersonGearIds Gear => Profile.Gear;
    public ResolvedModel Model { get; }
    /// <summary>The player's authored entity: its sword swings, and the recovery clip after each, are its actions' clips.</summary>
    public ResolvedEntity Hero { get; }
    /// <summary>The action a sword swing plays: the one gameplay named, or the attack.</summary>
    public ResolvedAction Swing(string? action) => Hero.Actions[action ?? EntityControllers.Attack];
    /// <summary>Clips by role, and the hero's action and recovery clips by id.</summary>
    public IReadOnlyDictionary<string, MotionClip> Clips { get; }
    public IReadOnlyDictionary<string, Part> Props { get; }
    public MotionClip this[string id] => Clips[id];

    /// <summary>The move set the catalog's player profile binds. Every role, the hero entity and every gear prop must resolve, by name.</summary>
    public static PersonMoves From(AuthoredCatalog catalog, string profileId = GameProfile.GameId)
    {
        var profile = catalog.Profiles.GetValueOrDefault(profileId)
            ?? throw new InvalidDataException($"The player move set needs the authored profile '{profileId}' (profiles/{profileId}.json), which is missing or does not check.");
        if (!profile.HasPlayer)
        {
            throw new InvalidDataException($"The player move set needs profile '{profileId}' to bind a player entity and model.");
        }

        var model = profile.Model;
        var resolved = catalog.Resolve(model);
        var missing = All
            .Where(role => !profile.Roles.TryGetValue(role, out var clip) || !catalog.Animations.ContainsKey(clip))
            .Select(role => $"role {role}")
            .Concat(new[] { profile.Gear.Sword, profile.Gear.Pistol, profile.Gear.Hair, profile.Gear.HairBack }.Where(id => !catalog.Parts.ContainsKey(id)).Select(id => $"prop {id}"))
            .ToList();

        if (missing.Count > 0)
        {
            throw new InvalidDataException($"The player move set is incomplete; missing: {string.Join(", ", missing)}.");
        }

        var hero = catalog.Entities.GetValueOrDefault(profile.Entity)
            ?? throw new InvalidDataException($"The player move set needs the authored '{profile.Entity}' entity, which is missing or does not compile.");

        var clips = All.ToDictionary(role => role, role =>
        {
            var clip = catalog.Animations[profile.Roles[role]];
            clip.Validate(resolved);
            return clip;
        }, StringComparer.Ordinal);

        foreach (var action in hero.Actions.Values)
        {
            clips[action.Clip.Id] = action.Clip;
            if (action.Recovery != null)
            {
                clips[action.Recovery.Id] = action.Recovery;
            }
        }

        foreach (var socket in new[] { Humanoid.BackSocket, Humanoid.BackViewSocket, Humanoid.SwordSocket, Humanoid.GunSocket, Humanoid.HeadSocket })
        {
            if (resolved.Base.Sockets.All(s => s.Id != socket))
            {
                throw new InvalidDataException($"Model '{model}' has no '{socket}' socket for the player's props.");
            }
        }

        return new(profile, resolved, hero, clips, catalog.Parts.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
    }
}

/// <summary>What to draw this frame: a base clip at a time, an optional upper-body overlay and the gear worn.</summary>
/// <summary>
/// What to draw this frame: a base clip at a time, an optional upper-body overlay and the gear worn. <see cref="Planted"/> is
/// false when the gait could not keep pace with the body (see <see cref="PersonAnimationDirector.MaxCadence"/>); contacts
/// should then follow the clip rather than hold world anchors that would stretch the legs.
/// </summary>
public readonly record struct PersonFrame(string Key, MotionClip Clip, double Seconds, bool Repeat, PoseLayer? Overlay, PersonGear Gear, bool Planted = true)
{
    public MotionClip PropClip => Overlay?.Clip ?? Clip;
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
    private readonly float _walkStride = MathF.Abs(PoseEvaluator.CycleTravel(moves.Model, moves[PersonMoves.Walk]).X);
    private readonly float _runStride = MathF.Abs(PoseEvaluator.CycleTravel(moves.Model, moves[PersonMoves.Run]).X);
    private PersonState2D _state;
    private Vector2 _position;
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
    public void ApplyState(PersonState2D state, Vector2 feet, double clock)
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
        PersonFrame Play(string id, double seconds, PoseLayer? overlay = null) => new(id, moves[id], seconds, moves[id].Loop, overlay, gear);
        double Scaled(string id) => Math.Clamp((s.Action.ElapsedSeconds + _sinceState) / s.Action.DurationSeconds, 0, 1) * moves[id].Duration;

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
            _swing = swing.Clip.Id;
            _swingLayered = false;
            _recovery = s.Action.Kind == PlayerAttackKind2D.Downward ? PersonMoves.Sheathe : swing.Recovery?.Id ?? PersonMoves.Sheathe;
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
                            Overlay = new(moves[PersonMoves.GunShot], Scaled(PersonMoves.GunShot), PersonLoadout.UpperBody)
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
                    Overlay = new(moves[PersonMoves.GunCharge], progress * moves[PersonMoves.GunCharge].Duration, PersonLoadout.UpperBody)
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
                Overlay = new(moves[_swing], Scaled(_swing), PersonLoadout.SwordUpperBody)
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
        if (_sheathePending && frame.PropClip.Id != _recovery)
        {
            _sheathePending = false;
        }

        return frame;
    }

    private PersonFrame Locomotion(PersonGear gear)
    {
        var s = _state;
        var gun = Equipment == EquipmentKind2D.Gun;
        var aim = gun ? new PoseLayer(moves[PersonMoves.GunAim], _clock, PersonLoadout.UpperBody) : null;
        PersonFrame Play(string id, double seconds, PoseLayer? overlay = null) => new(id, moves[id], seconds, moves[id].Loop, overlay, gear);
        double Clock(string id) => id == _key ? _clock - _keyStart : 0;
        PersonFrame Gait(string id, PoseLayer? overlay = null) => Play(id, id == _key ? _cycle : 0, overlay) with
        {
            Planted = id != _key || !_outpaced
        };
        var recovery = _sheathePending ? new PoseLayer(moves[_recovery], _clock - _recoveryStart, PersonLoadout.SwordUpperBody) : null;

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
