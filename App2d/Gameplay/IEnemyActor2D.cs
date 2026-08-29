using System.Numerics;

namespace App2d.Gameplay;

public interface IEnemyActor2D
{
    IEnemyCombatant2D Combatant { get; }

    void SetSimulationEnabled(bool isEnabled);

    void Update(float deltaSeconds, Vector2 targetPosition);

    void SyncAfterPhysics();
}
