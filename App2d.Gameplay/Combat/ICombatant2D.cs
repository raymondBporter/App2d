using App2d.Core;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay.Combat;

public interface ICombatant2D
{
    EntityId2D Id { get; }
    SpatialObject2D WorldObject { get; }
    PhysicsBody2D Body { get; }
    Health2D Health { get; }
    CombatFaction2D Faction { get; }
    bool IsAlive { get; }

    bool TryRegisterHit(EntityId2D attackSourceId, int attackId);
    bool TakeDamage(int damage, Vector2 knockback);
}
