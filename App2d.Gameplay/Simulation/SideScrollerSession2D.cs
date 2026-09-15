using App2d.Core;
using App2d.Gameplay.World;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Gameplay.Persons.Actions;
using App2d.Physics;
using System.Collections.Immutable;

namespace App2d.Gameplay.Simulation;

/// <summary>
/// Authoritative local session for the existing single-player campaign. Owns
/// stepping and progression, but has no input devices, renderer, audio playback
/// or save-file access. Networking and predicted instances are future callers.
/// </summary>
public sealed partial class SideScrollerSession2D : IDisposable
{
    public const float FixedDeltaSeconds = 1f / 120f;
    private const float DeathRestartDelaySeconds = 1.1f;
    private readonly PhysicsWorld2D _physics;
    private readonly CombatSystem2D? _combat;
    private readonly Person2D _player;
    private readonly ISessionPlayerActions2D _actions;
    private readonly ISideScrollerSessionWorld2D _world;
    private readonly List<SessionEvent2D> _events = [];
    private RespawnState2D _respawn;
    private float _restartSeconds;
    private float _moveX;
    private bool _reachedGoal;
    private bool _disposed;
    private long _eventSequence;
    private long _lastInputSequence;

    public SideScrollerSession2D(PhysicsWorld2D physics, Person2D player,
        ISessionPlayerActions2D actions, ISideScrollerSessionWorld2D world,
        RespawnState2D respawn, CombatSystem2D? combat = null)
    {
        _physics = ArgGuard.RequireNotNull(physics);
        _combat = combat;
        _player = ArgGuard.RequireNotNull(player);
        _actions = ArgGuard.RequireNotNull(actions);
        _world = ArgGuard.RequireNotNull(world);
        ArgGuard.ThrowIfNotFinite(respawn.Position);
        if (respawn.HitPoints <= 0 || respawn.HitPoints > player.Health.Maximum)
            throw new ArgumentOutOfRangeException(nameof(respawn));
        StateGuard.ThrowIf(!physics.Bodies.Contains(player.Body), "The player must belong to the session's physics world.");
        StateGuard.ThrowIf(!ReferenceEquals(player.Actions, actions), "The session actions must be attached to the player.");
        _respawn = respawn;
        _player.Health.Reset(respawn.HitPoints);
        _world.SetActiveSavePoint(respawn.CheckpointId);
        _world.UpdateStreaming(player.Position);
        _ = _world.UpdateSavePoints(0f, player.WorldObject.WorldBounds);

        if (_combat is not null) _combat.DamageResolved += OnCombatDamage;
        player.JumpStarted += OnJump;
        player.Landed += OnLanded;
        player.Footstep += OnFootstep;
        player.Damaged += OnDamaged;
        player.Died += OnDied;
        actions.WeaponOccurred += OnWeapon;
        actions.EquipmentChanged += OnEquipmentChanged;
        actions.MeleeAttackStarted += OnMelee;
        actions.DownAttackStarted += OnDownward;
        actions.ShotStarted += OnShot;
        actions.UnarmedAttackStarted += OnUnarmed;
    }

    public long Tick { get; private set; }
    public EntityId2D PlayerId => _player.Id;
    public bool IsPaused { get; private set; }

    public WorldState2D CaptureWorld() => _world.CaptureWorld();
    public ImmutableArray<EnemyState2D> CaptureEnemies() => _world.CaptureEnemies();

    public SessionSnapshot2D CaptureSnapshot()
    {
        RequireCheckpointBoundary();
        return new(Tick, _lastInputSequence, CaptureState(), CaptureWorld(), CaptureEnemies());
    }

    public PlayerState2D CaptureState() => new(_player.CaptureState(), _moveX,
        _actions.EquipmentId, _actions.IsMeleeAttackActive,
        _restartSeconds, _respawn.CheckpointId, _reachedGoal, _actions.CaptureWeaponState());

    public void SetPaused(bool paused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(_advancing, "Pause changes require a completed simulation tick.");
        if (paused == IsPaused) return;
        TimelineRevision++;
        if (paused)
            _actions.InterruptPrimary();
        IsPaused = paused;
    }

    public SessionFrame2D Advance(PlayerInput2D input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(IsPaused, "A paused session cannot advance.");
        // Reject the entire command before changing simulation state.
        if (input.EntityId != PlayerId || input.Tick != Tick + 1 ||
            input.Sequence <= _lastInputSequence)
            throw new ArgumentException("Expected this player's next tick and a new input sequence.", nameof(input));
        Validate(input.Command);

        StateGuard.ThrowIf(_advancing, "Session stepping cannot be reentered.");
        _advancing = true;
        TimelineRevision++;
        try
        {
            Tick++;
            _lastInputSequence = input.Sequence;
            _events.Clear();
            Step(input.Command);
            foreach (var occurrence in _world.DrainEnemyEvents())
                _events.Add(new EnemyOccurred2D(new SessionEventStamp2D(Tick, ++_eventSequence, occurrence.EntityId), occurrence));
            return new SessionFrame2D(Tick, _lastInputSequence, CaptureState(), _events.ToImmutableArray())
                { Enemies = CaptureEnemies(), World = CaptureWorld() };
        }
        finally { _advancing = false; }
    }

    private void Step(PersonCommand2D command)
    {
        const float dt = FixedDeltaSeconds;
        _world.UpdateStreaming(_player.Position);
        _world.UpdateMovingPlatforms(dt);
        _player.BeginFrame(dt);
        _world.UpdateEnemies(dt, _player.Position);

        if (_restartSeconds > 0f)
        {
            _moveX = 0f;
            _restartSeconds = Math.Max(0f, _restartSeconds - dt);
            StepPhysics();
            if (_restartSeconds <= 0f)
                Respawn();
            return;
        }

        _moveX = command.Movement.MoveX;
        _player.ApplyCommand(command, dt);
        StepPhysics();
        _world.ResolveDamage(_player);
        if (!_player.IsAlive)
        {
            _moveX = 0f;
            _restartSeconds = DeathRestartDelaySeconds;
            return;
        }

        if (_world.UpdateSavePoints(dt, _player.WorldObject.WorldBounds) is { } checkpoint)
        {
            _player.Health.Reset();
            _respawn = new RespawnState2D(checkpoint.Position, _player.Health.Current, checkpoint.ThingId);
            _world.SetActiveSavePoint(checkpoint.ThingId);
            _events.Add(new CheckpointActivated2D(Stamp(), checkpoint.ThingId,
                _player.Health.Current, _player.Position));
        }

        if (_player.Position.Y < _world.Bounds.Min.Y - 260f)
            Respawn();
        if (!_reachedGoal && _player.Position.X >= _world.GoalX)
        {
            _reachedGoal = true;
            _events.Add(new GoalReached2D(Stamp()));
        }
    }

    private void StepPhysics()
    {
        _physics.Step(FixedDeltaSeconds);
        _player.UpdateAfterPhysics(FixedDeltaSeconds);
        _world.SyncEnemiesAfterPhysics();
    }

    private void Respawn()
    {
        _restartSeconds = 0f;
        _player.Reset(_respawn.Position, _respawn.HitPoints);
        _world.UpdateStreaming(_respawn.Position);
        _ = _world.UpdateSavePoints(0f, _player.WorldObject.WorldBounds);
        _reachedGoal = false;
        _events.Add(new Respawned2D(Stamp(), _respawn.Position));
    }

    private static void Validate(PersonCommand2D command)
    {
        ArgGuard.ThrowIfNotInClosedRange(command.Movement.MoveX, -1f, 1f);
        ArgGuard.ThrowIfNotInClosedRange(command.Movement.ClimbY, -1f, 1f);
    }

    private SessionEventStamp2D Stamp() => new(Tick, ++_eventSequence, PlayerId);
    private void OnCombatDamage(CombatDamage2D damage) => _events.Add(new CombatDamageOccurred2D(
        new SessionEventStamp2D(Tick, ++_eventSequence, damage.TargetId), damage));
    private void OnWeapon(WeaponEvent2D occurrence) => _events.Add(new WeaponOccurred2D(Stamp(), occurrence));
    private void OnJump() => _events.Add(new JumpStarted2D(Stamp()));
    private void OnLanded(float speed) => _events.Add(new Landed2D(Stamp(), speed));
    private void OnFootstep() => _events.Add(new Footstep2D(Stamp()));
    private void OnDamaged() => _events.Add(new Damaged2D(Stamp()));
    private void OnDied() => _events.Add(new Died2D(Stamp()));
    private void OnEquipmentChanged(string id) => _events.Add(new EquipmentChanged2D(Stamp(), id));
    private void OnMelee(float duration) => Attack(PlayerAttackKind2D.Melee, duration);
    private void OnDownward(float duration) => Attack(PlayerAttackKind2D.Downward, duration);
    private void OnShot() => Attack(PlayerAttackKind2D.Shot, 0f);
    private void OnUnarmed(UnarmedAttackKind2D kind, float duration) =>
        Attack(kind == UnarmedAttackKind2D.Punch ? PlayerAttackKind2D.Punch : PlayerAttackKind2D.Kick, duration);
    private void Attack(PlayerAttackKind2D kind, float duration) =>
        _events.Add(new AttackStarted2D(Stamp(), kind, duration, _player.IsWallGripping));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_combat is not null) _combat.DamageResolved -= OnCombatDamage;
        _player.JumpStarted -= OnJump;
        _player.Landed -= OnLanded;
        _player.Footstep -= OnFootstep;
        _player.Damaged -= OnDamaged;
        _player.Died -= OnDied;
        _actions.WeaponOccurred -= OnWeapon;
        _actions.EquipmentChanged -= OnEquipmentChanged;
        _actions.MeleeAttackStarted -= OnMelee;
        _actions.DownAttackStarted -= OnDownward;
        _actions.ShotStarted -= OnShot;
        _actions.UnarmedAttackStarted -= OnUnarmed;
        _events.Clear();
    }
}
