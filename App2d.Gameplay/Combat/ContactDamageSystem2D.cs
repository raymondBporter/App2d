using App2d.Collision;
using App2d.Core;
using App2d.Gameplay.Persons;

namespace App2d.Gameplay.Combat;

/// <summary>Resolves opt-in body contact attacks against a person.</summary>
public sealed class ContactDamageSystem2D(
    CollisionSystem2D collision,
    uint sourceLayer)
{
    private readonly CollisionSystem2D _collision =
        ArgGuard.RequireNotNull(collision);
    private readonly List<CollisionOverlap2D> _overlaps = [];

    public bool Resolve(Person2D target)
    {
        ArgGuard.ThrowIfNull(target);
        if (target.IsDashing || !target.IsAlive)
            return false;

        _collision.Overlap(
            target.WorldObject,
            _overlaps,
            sourceLayer,
            includeSensors: true,
            excluded: target.Body.Collider);
        foreach (var overlap in _overlaps)
        {
            if (overlap.Collider.UserData is not Physics.PhysicsBody2D
                {
                    UserData: ICombatant2D { IsAlive: true } combatant and
                        IContactDamageSource2D source
                })
            {
                continue;
            }

            target.WorldObject.Transform.Position +=
                overlap.Contact.MinimumTranslationVector;
            if (target.TryTakeDamageFromX(
                source.ContactDamage,
                combatant.WorldObject.Transform.Position.X))
            {
                return !target.IsAlive;
            }
        }

        return false;
    }
}
