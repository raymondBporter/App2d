using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.World;

internal sealed partial class SavePoint2D
{
    internal sealed record SimulationState(
        bool PlayerWasInside,
        bool IsActive) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new(
        _playerWasInside, IsActive);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _playerWasInside = state.PlayerWasInside;
        IsActive = state.IsActive;
    }
}
