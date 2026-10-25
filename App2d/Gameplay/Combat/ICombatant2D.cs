using App2d.Contracts.Combat;
using App2d.Core;
using App2d.Core.Physics;
using App2d.Core.Spatial;
using System.Numerics;

namespace App2d.Gameplay.Combat;

public interface ICombatant2D
{
    EntityId2D Id { get; }
    SpatialObject2D WorldObject { get; }
    RigidBody2D Body { get; }
    Health2D Health { get; }
    CombatFaction2D Faction { get; }
    bool IsAlive { get; }

    /// <summary>Whether this attack instance's hit group has not struck this combatant yet: one accepted contact per source, instance and group.</summary>
    bool TryRegisterHit(EntityId2D attackSourceId, int attackId, string? hitGroup = null);
    bool TakeDamage(int damage, Vector2 knockback);
}
