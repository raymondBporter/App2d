using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons;

public sealed partial class Person2D
{
    internal sealed record SimulationState(
        float FootstepSeconds,
        bool SimulationEnabled,
        float Facing,
        float InvulnerabilitySeconds,
        float LandingSpeedThisFrame,
        bool DownAttackBouncedThisFrame,
        PersonCommand2D PreviousCommand,
        PersonLocomotion2D.SimulationState Motor,
        int HitPoints,
        ImmutableDictionary<EntityId2D, int> HitHistory) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _footstepSeconds, _simulationEnabled, Facing, InvulnerabilitySeconds, LandingSpeedThisFrame, DownAttackBouncedThisFrame, _previousCommand, _motor.CaptureSimulation(), Health.Current, _lastAttackIds.ToImmutableDictionary());

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _footstepSeconds = state.FootstepSeconds;
        _simulationEnabled = state.SimulationEnabled;
        Facing = state.Facing;
        InvulnerabilitySeconds = state.InvulnerabilitySeconds;
        LandingSpeedThisFrame = state.LandingSpeedThisFrame;
        DownAttackBouncedThisFrame = state.DownAttackBouncedThisFrame;
        _previousCommand = state.PreviousCommand;
        _motor.RestoreSimulation(state.Motor);
        Health.RestoreSimulation(state.HitPoints);
        _lastAttackIds.Clear();
        foreach (var pair in state.HitHistory) _lastAttackIds.Add(pair.Key, pair.Value);
    }
}
