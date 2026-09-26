using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Persons.Actions;

internal sealed partial class MeleeAttack2D
{
    internal sealed record SimulationState(
        float ElapsedSeconds,
        float InputBufferSeconds,
        int AttackId,
        bool IsInProgress,
        bool IsDamageActive,
        TransformState2D Pose,
        MeleeAttackProfile2D Profile,
        MeleeAttackProfile2D? NextProfile) : SimulationState2D;

    internal SimulationState CaptureSimulation() => new SimulationState(
        _elapsedSeconds, _inputBufferSeconds, AttackId, IsInProgress, IsDamageActive, TransformState2D.Capture(WorldObject.Transform), Profile, NextProfile);

    internal void RestoreSimulation(SimulationState snapshot)
    {
        var state = snapshot;
        _elapsedSeconds = state.ElapsedSeconds;
        _inputBufferSeconds = state.InputBufferSeconds;
        AttackId = state.AttackId;
        IsInProgress = state.IsInProgress;
        IsDamageActive = state.IsDamageActive;
        state.Pose.Apply(WorldObject.Transform);
        Profile = state.Profile; NextProfile = state.NextProfile;
    }
}
