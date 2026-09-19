using App2d.Core;
using App2d.Gameplay.World;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Physics;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// Authoritative session. Owns stepping and progression for its participants, but has
/// no input devices, renderer, audio playback or save-file access. The same type runs
/// on a server and inside a predicting client.
/// </summary>
public sealed partial class SideScrollerSession2D : IDisposable
{
    public const float FixedDeltaSeconds = 1f / 120f;
    private const float DeathRestartDelaySeconds = 1.1f;
    private readonly PhysicsWorld2D _physics;
    private readonly CombatSystem2D? _combat;
    private readonly ISideScrollerSessionWorld2D _world;
    private readonly List<Participant> _players = [];
    private readonly List<SessionEvent2D> _events = [];
    private bool _disposed;
    private long _eventSequence;

    public SideScrollerSession2D(PhysicsWorld2D physics, Person2D player,
        ISessionPlayerActions2D actions, ISideScrollerSessionWorld2D world,
        RespawnState2D respawn, CombatSystem2D? combat = null)
    {
        _physics = ArgGuard.RequireNotNull(physics);
        _combat = combat;
        _world = ArgGuard.RequireNotNull(world);
        ArgGuard.ThrowIfNull(player);
        ArgGuard.ThrowIfNull(actions);
        ArgGuard.ThrowIfNotFinite(respawn.Position);
        if (respawn.HitPoints <= 0 || respawn.HitPoints > player.Health.Maximum)
            throw new ArgumentOutOfRangeException(nameof(respawn));
        StateGuard.ThrowIf(!physics.Bodies.Contains(player.Body), "The player must belong to the session's physics world.");
        StateGuard.ThrowIf(!ReferenceEquals(player.Actions, actions), "The session actions must be attached to the player.");

        var participant = new Participant(player, actions, respawn);
        _players.Add(participant);
        player.Health.Reset(respawn.HitPoints);
        _world.SetActiveSavePoint(respawn.CheckpointId);
        _world.UpdateStreaming(player.Position);
        _ = _world.UpdateSavePoints(0f, player.WorldObject.WorldBounds);

        if (_combat is not null) _combat.DamageResolved += OnCombatDamage;
        participant.Subscribe(this);
    }

    public long Tick { get; private set; }
    public bool IsPaused { get; private set; }
    public ImmutableArray<EntityId2D> PlayerIds => _players.Select(p => p.Person.Id).ToImmutableArray();

    public LevelContent2D CaptureContent() => _world.CaptureContent();
    public WorldState2D CaptureWorld() => _world.CaptureWorld();
    public ImmutableArray<EnemyState2D> CaptureEnemies() => _world.CaptureEnemies();
    public ImmutableArray<PlayerState2D> CapturePlayers() => _players.Select(p => p.CaptureState()).ToImmutableArray();

    public SessionSnapshot2D CaptureSnapshot()
    {
        RequireCheckpointBoundary();
        return new(Tick, CapturePlayers(), CaptureContent(), CaptureWorld(), CaptureEnemies());
    }

    public void SetPaused(bool paused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(_advancing, "Pause changes require a completed simulation tick.");
        if (paused == IsPaused) return;
        TimelineRevision++;
        if (paused)
            foreach (var player in _players) player.Actions.InterruptPrimary();
        IsPaused = paused;
    }

    /// <summary>
    /// Checks one input against the current tick without changing state. A transport
    /// should call this and drop rejected inputs; <see cref="Advance"/> throws on them.
    /// </summary>
    public bool TryValidateInput(PlayerInput2D input, out InputRejection2D rejection)
    {
        var participant = FindParticipant(input.EntityId);
        rejection = participant is null ? InputRejection2D.UnknownPlayer
            : input.Tick != Tick + 1 ? InputRejection2D.WrongTick
            : input.Sequence <= participant.LastInputSequence ? InputRejection2D.StaleSequence
            : !IsValidMovement(input.Command) ? InputRejection2D.InvalidMovement
            : InputRejection2D.None;
        return rejection == InputRejection2D.None;
    }

    /// <summary>
    /// Advances exactly one fixed tick. Each input applies to its player for this tick;
    /// a player without an input this tick repeats its previous command, so a missing
    /// packet degrades to "hold what you were doing" rather than stalling the session.
    /// </summary>
    public SessionFrame2D Advance(params ReadOnlySpan<PlayerInput2D> inputs)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(IsPaused, "A paused session cannot advance.");
        StateGuard.ThrowIf(_advancing, "Session stepping cannot be reentered.");
        // Reject the entire batch before changing simulation state.
        for (var i = 0; i < inputs.Length; i++)
        {
            if (!TryValidateInput(inputs[i], out var rejection))
                throw new ArgumentException($"Input for player {inputs[i].EntityId.Value} was rejected: {rejection}.", nameof(inputs));
            for (var j = 0; j < i; j++)
                if (inputs[j].EntityId == inputs[i].EntityId)
                    throw new ArgumentException($"Input for player {inputs[i].EntityId.Value} was rejected: {InputRejection2D.DuplicatePlayer}.", nameof(inputs));
        }

        _advancing = true;
        TimelineRevision++;
        try
        {
            Tick++;
            foreach (var input in inputs)
            {
                var participant = FindParticipant(input.EntityId)!;
                participant.LastInputSequence = input.Sequence;
                participant.LastCommand = input.Command;
            }
            _events.Clear();
            Step();
            foreach (var occurrence in _world.DrainEnemyEvents())
                _events.Add(new EnemyOccurred2D(new SessionEventStamp2D(Tick, ++_eventSequence, occurrence.EntityId), occurrence));
            return new SessionFrame2D(Tick, CapturePlayers(), _events.ToImmutableArray())
                { Content = CaptureContent(), Enemies = CaptureEnemies(), World = CaptureWorld() };
        }
        finally { _advancing = false; }
    }

    private void Step()
    {
        const float dt = FixedDeltaSeconds;
        _world.UpdateStreaming(StreamingFocus);
        _world.UpdateMovingPlatforms(dt);
        foreach (var player in _players) player.Person.BeginFrame(dt);
        _world.UpdateEnemies(dt, EnemyTarget);

        foreach (var player in _players)
        {
            player.WasRestarting = player.RestartSeconds > 0f;
            if (player.WasRestarting)
            {
                player.MoveX = 0f;
                player.RestartSeconds = Math.Max(0f, player.RestartSeconds - dt);
            }
            else
            {
                player.MoveX = player.LastCommand.MoveX;
            }
            // A dead person only records the command for edge derivation.
            player.Person.ApplyCommand(player.LastCommand, dt);
        }

        StepPhysics();

        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (player.WasRestarting)
            {
                if (player.RestartSeconds <= 0f)
                    Respawn(player);
                continue;
            }

            _world.ResolveDamage(player.Person);
            if (!player.Person.IsAlive)
            {
                player.MoveX = 0f;
                player.RestartSeconds = DeathRestartDelaySeconds;
                continue;
            }

            // Checkpoint timers advance once per tick; further participants only test entry.
            if (_world.UpdateSavePoints(i == 0 ? dt : 0f, player.Person.WorldObject.WorldBounds) is { } checkpoint)
            {
                player.Person.Health.Reset();
                player.Respawn = new RespawnState2D(checkpoint.Position, player.Person.Health.Current, checkpoint.ThingId);
                _world.SetActiveSavePoint(checkpoint.ThingId);
                _events.Add(new CheckpointActivated2D(Stamp(player), checkpoint.ThingId,
                    player.Person.Health.Current, player.Person.Position));
            }

            if (player.Person.Position.Y < _world.Bounds.Min.Y - 260f)
                Respawn(player);
            if (!player.ReachedGoal && player.Person.Position.X >= _world.GoalX)
            {
                player.ReachedGoal = true;
                _events.Add(new GoalReached2D(Stamp(player)));
            }
        }
    }

    private void StepPhysics()
    {
        _physics.Step(FixedDeltaSeconds);
        foreach (var player in _players) player.Person.UpdateAfterPhysics(FixedDeltaSeconds);
        _world.SyncEnemiesAfterPhysics();
    }

    private void Respawn(Participant player)
    {
        player.RestartSeconds = 0f;
        player.Person.Reset(player.Respawn.Position, player.Respawn.HitPoints);
        _world.UpdateStreaming(player.Respawn.Position);
        _ = _world.UpdateSavePoints(0f, player.Person.WorldObject.WorldBounds);
        player.ReachedGoal = false;
        _events.Add(new Respawned2D(Stamp(player), player.Respawn.Position));
    }

    /// <summary>Policy: terrain streams around the first participant.</summary>
    private Vector2 StreamingFocus => _players[0].Person.Position;

    /// <summary>Policy: enemies pursue the first living participant, or the first one if none live.</summary>
    private Vector2 EnemyTarget
    {
        get
        {
            foreach (var player in _players)
                if (player.Person.IsAlive) return player.Person.Position;
            return _players[0].Person.Position;
        }
    }

    private static bool IsValidMovement(PersonCommand2D command) =>
        float.IsFinite(command.MoveX) && command.MoveX is >= -1f and <= 1f &&
        float.IsFinite(command.ClimbY) && command.ClimbY is >= -1f and <= 1f;

    private Participant? FindParticipant(EntityId2D id)
    {
        foreach (var player in _players)
            if (player.Person.Id == id) return player;
        return null;
    }

    private SessionEventStamp2D Stamp(Participant player) => new(Tick, ++_eventSequence, player.Person.Id);
    private void OnCombatDamage(CombatDamage2D damage) => _events.Add(new CombatDamageOccurred2D(
        new SessionEventStamp2D(Tick, ++_eventSequence, damage.TargetId), damage));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_combat is not null) _combat.DamageResolved -= OnCombatDamage;
        foreach (var player in _players) player.Unsubscribe();
        _events.Clear();
    }

    /// <summary>One controlled person and the session-level progression that belongs to it.</summary>
    private sealed class Participant(Person2D person, ISessionPlayerActions2D actions, RespawnState2D respawn)
    {
        private readonly List<Action> _unsubscribe = [];
        public Person2D Person { get; } = person;
        public ISessionPlayerActions2D Actions { get; } = actions;
        public RespawnState2D Respawn { get; set; } = respawn;
        public float RestartSeconds { get; set; }
        public float MoveX { get; set; }
        public bool ReachedGoal { get; set; }
        public long LastInputSequence { get; set; }
        public PersonCommand2D LastCommand { get; set; }
        /// <summary>Within-tick scratch: whether this tick began inside the death restart delay.</summary>
        public bool WasRestarting { get; set; }

        public PlayerState2D CaptureState() => new(Person.CaptureState(), LastInputSequence, MoveX,
            Actions.Equipment, Actions.IsMeleeAttackActive,
            RestartSeconds, Respawn.CheckpointId, ReachedGoal, Actions.CaptureWeaponState());

        public ParticipantState CaptureSimulation() => new(Respawn, RestartSeconds, MoveX, ReachedGoal,
            LastInputSequence, LastCommand, Person.CaptureSimulation(), Actions.CaptureSimulation());

        public void RestoreSimulation(ParticipantState state)
        {
            Respawn = state.Respawn;
            RestartSeconds = state.RestartSeconds;
            MoveX = state.MoveX;
            ReachedGoal = state.ReachedGoal;
            LastInputSequence = state.LastInputSequence;
            LastCommand = state.LastCommand;
            Person.RestoreSimulation(state.Person);
            Actions.RestoreSimulation(state.Actions);
        }

        public void Subscribe(SideScrollerSession2D session)
        {
            Action onJump = () => session._events.Add(new JumpStarted2D(session.Stamp(this)));
            Action<float> onLanded = speed => session._events.Add(new Landed2D(session.Stamp(this), speed));
            Action onFootstep = () => session._events.Add(new Footstep2D(session.Stamp(this)));
            Action onDamaged = () => session._events.Add(new Damaged2D(session.Stamp(this)));
            Action onDied = () => session._events.Add(new Died2D(session.Stamp(this)));
            Action<WeaponEvent2D> onWeapon = occurrence => session._events.Add(new WeaponOccurred2D(session.Stamp(this), occurrence));
            Action<EquipmentKind2D> onEquipment = kind => session._events.Add(new EquipmentChanged2D(session.Stamp(this), kind));
            Action<float> onMelee = duration => Attack(PlayerAttackKind2D.Melee, duration);
            Action<float> onDownward = duration => Attack(PlayerAttackKind2D.Downward, duration);
            Action onShot = () => Attack(PlayerAttackKind2D.Shot, 0f);
            Action<UnarmedAttackKind2D, float> onUnarmed = (kind, duration) =>
                Attack(kind == UnarmedAttackKind2D.Punch ? PlayerAttackKind2D.Punch : PlayerAttackKind2D.Kick, duration);

            Person.JumpStarted += onJump; _unsubscribe.Add(() => Person.JumpStarted -= onJump);
            Person.Landed += onLanded; _unsubscribe.Add(() => Person.Landed -= onLanded);
            Person.Footstep += onFootstep; _unsubscribe.Add(() => Person.Footstep -= onFootstep);
            Person.Damaged += onDamaged; _unsubscribe.Add(() => Person.Damaged -= onDamaged);
            Person.Died += onDied; _unsubscribe.Add(() => Person.Died -= onDied);
            Actions.WeaponOccurred += onWeapon; _unsubscribe.Add(() => Actions.WeaponOccurred -= onWeapon);
            Actions.EquipmentChanged += onEquipment; _unsubscribe.Add(() => Actions.EquipmentChanged -= onEquipment);
            Actions.MeleeAttackStarted += onMelee; _unsubscribe.Add(() => Actions.MeleeAttackStarted -= onMelee);
            Actions.DownAttackStarted += onDownward; _unsubscribe.Add(() => Actions.DownAttackStarted -= onDownward);
            Actions.ShotStarted += onShot; _unsubscribe.Add(() => Actions.ShotStarted -= onShot);
            Actions.UnarmedAttackStarted += onUnarmed; _unsubscribe.Add(() => Actions.UnarmedAttackStarted -= onUnarmed);

            void Attack(PlayerAttackKind2D kind, float duration) =>
                session._events.Add(new AttackStarted2D(session.Stamp(this), kind, duration, Person.IsWallGripping));
        }

        public void Unsubscribe()
        {
            foreach (var detach in _unsubscribe) detach();
            _unsubscribe.Clear();
        }
    }

    internal sealed record ParticipantState(RespawnState2D Respawn, float RestartSeconds, float MoveX, bool ReachedGoal,
        long LastInputSequence, PersonCommand2D LastCommand, Person2D.SimulationState Person, SimulationState2D Actions);
}
