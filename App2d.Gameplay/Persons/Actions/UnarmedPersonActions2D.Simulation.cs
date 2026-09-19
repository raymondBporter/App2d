using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class UnarmedPersonActions2D
{
    internal sealed record SimulationState(
        float PunchDirection,
        float KickDirection,
        MeleeAttack2D.SimulationState Punch,
        MeleeAttack2D.SimulationState Kick) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _punch.Direction, _kick.Direction, _punch.Action.CaptureSimulation(), _kick.Action.CaptureSimulation());

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _punch.Direction = state.PunchDirection;
        _kick.Direction = state.KickDirection;
        _punch.Action.RestoreSimulation(state.Punch);
        _kick.Action.RestoreSimulation(state.Kick);
    }
}
