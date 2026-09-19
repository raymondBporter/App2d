using App2d.Gameplay.Simulation;
using App2d.Gameplay.Combat;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public interface IEnemyActor2D
{
    SimulationState2D CaptureSimulation() => throw new NotSupportedException("This participant does not support rollback.");
    void RestoreSimulation(SimulationState2D state) => throw new NotSupportedException("This participant does not support rollback.");
    ICombatant2D Combatant { get; }
    EnemyState2D CaptureState();
    ImmutableArray<EnemyEvent2D> DrainEvents() => [];

    void SetSimulationEnabled(bool isEnabled);

    void Update(float deltaSeconds, Vector2 targetPosition);

    void SyncAfterPhysics();
}
