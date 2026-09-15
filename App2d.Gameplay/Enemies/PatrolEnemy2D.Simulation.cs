using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed partial class PatrolEnemy2D
{
    internal sealed record SimulationState(
        float Direction,
        float StunSeconds,
        int HitPoints,
        ImmutableDictionary<EntityId2D, int> HitHistory) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _direction, _stunSeconds, Health.Current, _lastAttackIds.ToImmutableDictionary());

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _direction = state.Direction;
        _stunSeconds = state.StunSeconds;
        Health.RestoreSimulation(state.HitPoints);
        _lastAttackIds.Clear();
        foreach (var pair in state.HitHistory) _lastAttackIds.Add(pair.Key, pair.Value);
    }
}
