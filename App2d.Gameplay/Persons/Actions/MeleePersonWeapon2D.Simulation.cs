using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal abstract partial class MeleePersonWeapon2D
{
    internal sealed record SimulationState(
        float AttackDirection,
        float BufferedAttackDirection,
        MeleeAttack2D.SimulationState Attack,
        float SinceSwing, bool FollowUp, bool NextFollowUp) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _attackDirection, _bufferedAttackDirection, _attack.CaptureSimulation(), _sinceSwing, _followUp, _nextFollowUp);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _attackDirection = state.AttackDirection;
        _bufferedAttackDirection = state.BufferedAttackDirection;
        _attack.RestoreSimulation(state.Attack);
        _sinceSwing = state.SinceSwing; _followUp = state.FollowUp; _nextFollowUp = state.NextFollowUp;
    }
}
