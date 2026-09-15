using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons;

internal sealed partial class RivalBrain2D
{
    internal sealed record SimulationState(
        float AttackDelaySeconds,
        float DashDelaySeconds,
        float JumpDelaySeconds,
        float JumpHoldSeconds,
        bool NextAttackIsKick) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _attackDelaySeconds, _dashDelaySeconds, _jumpDelaySeconds, _jumpHoldSeconds, _nextAttackIsKick);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _attackDelaySeconds = state.AttackDelaySeconds;
        _dashDelaySeconds = state.DashDelaySeconds;
        _jumpDelaySeconds = state.JumpDelaySeconds;
        _jumpHoldSeconds = state.JumpHoldSeconds;
        _nextAttackIsKick = state.NextAttackIsKick;
    }
}
