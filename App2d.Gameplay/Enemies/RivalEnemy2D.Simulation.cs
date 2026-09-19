using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed partial class RivalEnemy2D
{
    internal sealed record SimulationState(
        float LastDeltaSeconds,
        float LastMoveX,
        bool SimulationEnabled,
        App2d.Gameplay.Persons.Person2D.SimulationState PersonState,
        App2d.Gameplay.Persons.RivalBrain2D.SimulationState Brain,
        App2d.Gameplay.Persons.Actions.UnarmedPersonActions2D.SimulationState Actions,
        ImmutableArray<EnemyEvent2D> Events) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new SimulationState(
        _lastDeltaSeconds, _lastMoveX, _simulationEnabled, Person.CaptureSimulation(), _brain.CaptureSimulation(), _actions.CaptureSimulation(), _events.ToImmutableArray());

    public void RestoreSimulation(SimulationState2D snapshot)
    {
        var state = (SimulationState)snapshot;
        _lastDeltaSeconds = state.LastDeltaSeconds;
        _lastMoveX = state.LastMoveX;
        _simulationEnabled = state.SimulationEnabled;
        Person.RestoreSimulation(state.PersonState);
        _brain.RestoreSimulation(state.Brain);
        _actions.RestoreSimulation(state.Actions);
        _events.Clear();
        _events.AddRange(state.Events);
    }
}
