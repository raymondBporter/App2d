using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed partial class GreenDinosaur2D
{
    internal sealed record SimulationState(
        bool SimulationEnabled,
        PatrolEnemy2D.SimulationState Patrol) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new SimulationState(
        _simulationEnabled, Enemy.CaptureSimulation());

    public void RestoreSimulation(SimulationState2D snapshot)
    {
        var state = (SimulationState)snapshot;
        _simulationEnabled = state.SimulationEnabled;
        Enemy.RestoreSimulation(state.Patrol);
    }
}
