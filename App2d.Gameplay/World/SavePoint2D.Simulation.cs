using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.World;

internal sealed partial class SavePoint2D
{
    internal sealed record SimulationState(
        bool PlayerWasInside,
        bool IsActive) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _playerWasInside, IsActive);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _playerWasInside = state.PlayerWasInside;
        IsActive = state.IsActive;
    }
}
