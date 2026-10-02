using App2d.Contracts.Enemies;
using App2d.Gameplay.Combat;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public interface IEnemyActor2D
{
    ICombatant2D Combatant { get; }
    EnemyState2D CaptureState();
    ImmutableArray<EnemyEvent2D> DrainEvents() => [];

    void SetSimulationEnabled(bool isEnabled);

    void Update(float deltaSeconds, Vector2 targetPosition);

    void SyncAfterPhysics();
}
