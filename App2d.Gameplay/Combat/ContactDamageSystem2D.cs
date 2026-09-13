using App2d.Collision;
using App2d.Core;
using App2d.Gameplay.Persons;

namespace App2d.Gameplay.Combat;

/// <summary>Resolves opt-in body contact attacks against a person.</summary>
public sealed class ContactDamageSystem2D(
    CollisionSystem2D collision,
    uint sourceLayer,
    CombatantRegistry2D combatants)
{
    private readonly CollisionSystem2D _collision =
        ArgGuard.RequireNotNull(collision);
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly CombatantRegistry2D _combatants = ArgGuard.RequireNotNull(combatants);

    public bool Resolve(Person2D target)
    {
        ArgGuard.ThrowIfNull(target);
        if (target.IsDashing || target.DownAttackBouncedThisFrame || !target.IsAlive)
            return false;

        _collision.Overlap(
            target.WorldObject,
            _overlaps,
            sourceLayer,
            includeSensors: true,
            excluded: target.Body.Collider);
        foreach (var overlap in _overlaps)
        {
            if (_combatants.Find(overlap.Collider.EntityId) is not
                ICombatant2D { IsAlive: true } combatant ||
                combatant is not IContactDamageSource2D source)
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
