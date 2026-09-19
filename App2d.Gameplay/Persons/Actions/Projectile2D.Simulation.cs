using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

public sealed partial class Projectile2D
{
    internal sealed record SimulationState(
        EntityId2D Id,
        Vector2 Origin,
        Vector2 Velocity,
        float RemainingLifetime,
        TransformState2D Pose) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        Id, Origin, Velocity, RemainingLifetime, TransformState2D.Capture(WorldObject.Transform));

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        Id = state.Id;
        Origin = state.Origin;
        Velocity = state.Velocity;
        RemainingLifetime = state.RemainingLifetime;
        state.Pose.Apply(WorldObject.Transform);
    }
}
