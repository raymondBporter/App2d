using App2d.Gameplay.Combat;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public interface IEnemyActor2D
{
    ICombatant2D Combatant { get; }

    void SetSimulationEnabled(bool isEnabled);

    void Update(float deltaSeconds, Vector2 targetPosition);

    void SyncAfterPhysics();
}
