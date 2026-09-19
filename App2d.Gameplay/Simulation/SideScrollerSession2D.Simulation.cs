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
        var expectedIds = CaptureEnemies().Select(e => e.Id).Concat(PlayerIds).OrderBy(id => id.Value);
        StateGuard.ThrowIf(!ids.SequenceEqual(expectedIds), "Every combatant must belong to the captured players/world.");
        return new(_checkpointOwner, Tick, _eventSequence, IsPaused,
            _players.Select(p => p.CaptureSimulation()).ToImmutableArray(),
            _world.CaptureSimulation(), _physics.CaptureSimulation(),
            combat.DefeatedEnemies, ids, _events.ToImmutableArray());
    }

    public void ValidateCheckpoint(SessionCheckpoint2D checkpoint)
    {
        RequireCheckpointBoundary();
        ArgGuard.ThrowIfNull(checkpoint);
        StateGuard.ThrowIf(checkpoint.Owner != _checkpointOwner, "The checkpoint belongs to another session.");
        StateGuard.ThrowIf(checkpoint.Players.Length != _players.Count, "Participant membership changed since capture.");
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
        for (var i = 0; i < _players.Count; i++) _players[i].RestoreSimulation(checkpoint.Players[i]);
        _combat!.RestoreSimulation(checkpoint.DefeatedEnemies);
        Tick = checkpoint.Tick;
        _eventSequence = checkpoint.EventSequence;
        IsPaused = checkpoint.IsPaused;
        _events.Clear();
        _events.AddRange(checkpoint.Events);
    }

    private void RequireCheckpointBoundary()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(_advancing, "Capture and restore require a completed simulation tick.");
    }
}
