using System.Numerics;
using App2d.Core;
using App2d.Physics;

namespace App2d.Gameplay;

public interface IEnemyCombatant2D
{
    SpatialObject2D WorldObject { get; }
    PhysicsBody2D Body { get; }
    Health2D Health { get; }
    bool IsAlive { get; }

    bool TryRegisterHit(object attackSource, int attackId);
    bool TakeDamage(int damage, Vector2 knockback);
}
