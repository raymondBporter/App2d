using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.World;

public sealed partial class MovingPlatform2D
{
    internal sealed record SimulationState(
        float DistanceAlongPath,
        float TravelDirection) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new(
        _distanceAlongPath, _travelDirection);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _distanceAlongPath = state.DistanceAlongPath;
        _travelDirection = state.TravelDirection;
    }
}
