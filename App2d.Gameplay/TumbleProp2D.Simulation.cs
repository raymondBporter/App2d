using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay;

internal sealed partial class TumbleProp2D
{
    internal sealed record SimulationState(
        bool SimulationEnabled,
        int HitPoints,
        ImmutableDictionary<EntityId2D, int> HitHistory) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new SimulationState(
        _simulationEnabled, Health.Current, _lastAttackIds.ToImmutableDictionary());

    public void RestoreSimulation(SimulationState2D snapshot)
    {
        var state = (SimulationState)snapshot;
        _simulationEnabled = state.SimulationEnabled;
        Health.RestoreSimulation(state.HitPoints);
        _lastAttackIds.Clear();
        foreach (var pair in state.HitHistory) _lastAttackIds.Add(pair.Key, pair.Value);
    }
}
