using App2d.Core;
using App2d.Core.Mathematics;
using App2d.Gameplay.Simulation;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed partial class BoilerBrute2D
{
    internal sealed record SimulationState(
        float AttackElapsedSeconds,
        float AttackCooldownSeconds,
        float Facing,
        bool IsAttacking,
        bool HammerConnected,
        bool StrikeReported,
        bool SimulationEnabled,
        PatrolEnemy2D.SimulationState Patrol,
        TransformState2D Hammer,
        ImmutableArray<EnemyEvent2D> Events) : SimulationState2D;

    public SimulationState2D CaptureSimulation() => new SimulationState(
        _attackElapsedSeconds, _attackCooldownSeconds, _facing, _isAttacking, _hammerConnected, _strikeReported, _simulationEnabled, Enemy.CaptureSimulation(), TransformState2D.Capture(_hammerHitbox.Transform), _events.ToImmutableArray());

    public void RestoreSimulation(SimulationState2D snapshot)
    {
        var state = (SimulationState)snapshot;
        _attackElapsedSeconds = state.AttackElapsedSeconds;
        _attackCooldownSeconds = state.AttackCooldownSeconds;
        _facing = state.Facing;
        _isAttacking = state.IsAttacking;
        _hammerConnected = state.HammerConnected;
        _strikeReported = state.StrikeReported;
        _simulationEnabled = state.SimulationEnabled;
        Enemy.RestoreSimulation(state.Patrol);
        state.Hammer.Apply(_hammerHitbox.Transform);
        _events.Clear();
        _events.AddRange(state.Events);
    }
}
