using App2d.Gameplay.Simulation;

namespace App2d.Gameplay.World;

public sealed partial class MovingPlatform2D
{
    internal sealed record SimulationState(double TimeSeconds) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new(_kinematic.TimeSeconds);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        _kinematic.RestoreTime(snapshot.TimeSeconds);
    }
}
