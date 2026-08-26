using System.Numerics;

namespace App2d.Gameplay;

public interface IEnemyActor2D
{
    PatrolEnemy2D Enemy { get; }

    void SetSimulationEnabled(bool isEnabled);

    void Update(float deltaSeconds, Vector2 targetPosition);

    void SyncAfterPhysics();
}
