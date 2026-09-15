using App2d.Core;
using System.Collections.Immutable;

namespace App2d.Gameplay.Simulation;

public sealed partial class SideScrollerSession2D
{
    private readonly Guid _checkpointOwner = Guid.NewGuid();
    private bool _advancing;
    internal long TimelineRevision { get; private set; }

    public SessionCheckpoint2D CaptureCheckpoint()
    {
        RequireCheckpointBoundary();
        var combat = _combat ?? throw new InvalidOperationException("Rollback requires the session's combat system.");
        var ids = combat.Combatants.Ids.OrderBy(id => id.Value).ToImmutableArray();
        var expectedIds = CaptureEnemies().Select(e => e.Id).Append(PlayerId).OrderBy(id => id.Value);
        StateGuard.ThrowIf(!ids.SequenceEqual(expectedIds), "Every combatant must belong to the captured player/world.");
        return new(_checkpointOwner, Tick, _lastInputSequence, _eventSequence, IsPaused,
            _respawn, _restartSeconds, _moveX, _reachedGoal, _player.CaptureSimulation(),
            _actions.CaptureSimulation(), _world.CaptureSimulation(), _physics.CaptureSimulation(),
            combat.DefeatedEnemies, ids, _events.ToImmutableArray());
    }

    public void ValidateCheckpoint(SessionCheckpoint2D checkpoint)
    {
        RequireCheckpointBoundary();
        ArgGuard.ThrowIfNull(checkpoint);
        StateGuard.ThrowIf(checkpoint.Owner != _checkpointOwner, "The checkpoint belongs to another session.");
        _world.ValidateSimulation(checkpoint.World);
        _physics.ValidateSimulation(checkpoint.Physics, _world.TerrainColliderIds, checkpoint.World.TerrainColliderIds);
        StateGuard.ThrowIf(_combat is null || !_combat.Combatants.Ids.OrderBy(id => id.Value).SequenceEqual(checkpoint.Combatants),
            "Combatant membership changed since capture.");
    }

    /// <summary>Restores values without advancing physics or publishing gameplay occurrences.</summary>
    public void RestoreCheckpoint(SessionCheckpoint2D checkpoint)
    {
        // Validate definition/ownership/membership before any subsystem is mutated.
        ValidateCheckpoint(checkpoint);
        TimelineRevision++;
        _world.RestoreSimulation(checkpoint.World);
        _physics.RestoreSimulation(checkpoint.Physics);
        _player.RestoreSimulation(checkpoint.Player);
        _actions.RestoreSimulation(checkpoint.Actions);
        _combat!.RestoreSimulation(checkpoint.DefeatedEnemies);
        Tick = checkpoint.Tick;
        _lastInputSequence = checkpoint.LastInputSequence;
        _eventSequence = checkpoint.EventSequence;
        IsPaused = checkpoint.IsPaused;
        _respawn = checkpoint.Respawn;
        _restartSeconds = checkpoint.RestartSeconds;
        _moveX = checkpoint.MoveX;
        _reachedGoal = checkpoint.ReachedGoal;
        _events.Clear();
        _events.AddRange(checkpoint.Events);
    }

    private void RequireCheckpointBoundary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(_advancing, "Capture and restore require a completed simulation tick.");
    }
}
