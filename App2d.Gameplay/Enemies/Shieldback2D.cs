using App2d.Core;
using App2d.Gameplay.Combat;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed partial class Shieldback2D(PatrolEnemy2D enemy) : IEnemyActor2D
{
    private bool _simulationEnabled = true;
    public PatrolEnemy2D Enemy { get; } = ArgGuard.RequireNotNull(enemy);
    public ICombatant2D Combatant => Enemy;
    public EnemyState2D CaptureState() => new(Enemy.Id, EnemyKind2D.Shieldback,
        Enemy.WorldObject.Transform.Position, Enemy.Body.LinearVelocity, 0f, Enemy.Facing,
        _simulationEnabled, Enemy.IsAlive) { MoveSpeed = Enemy.Speed };

    public void SetSimulationEnabled(bool isEnabled)
    {
        _simulationEnabled = isEnabled;
        Enemy.SetSimulationEnabled(isEnabled);
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(targetPosition);
        if (_simulationEnabled) Enemy.Update(deltaSeconds);
    }

    public void SyncAfterPhysics() { }
}
