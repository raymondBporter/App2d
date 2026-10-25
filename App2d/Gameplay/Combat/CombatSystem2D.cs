using App2d.Contracts.Combat;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.Combat;

public sealed class CombatSystem2D(CollisionSystem2D collision, CombatantRegistry2D combatants)
{
    private readonly CollisionSystem2D _collision = ArgGuard.RequireNotNull(collision);
    private readonly List<CollisionOverlap2D> _overlaps = [];
    private readonly HashSet<EntityId2D> _visited = [];

    public CombatantRegistry2D Combatants { get; } = ArgGuard.RequireNotNull(combatants);
    public int DefeatedEnemies { get; private set; }
    public event Action<CombatDamage2D>? DamageResolved;

    public bool ResolveAttack(
        SpatialObject2D hitbox,
        EntityId2D attackSourceId,
        int attackId,
        CombatFaction2D attackerFaction,
        uint targetLayer,
        int damage,
        Func<ICombatant2D, Vector2> knockback,
        bool stopAfterFirstHit = false,
        CombatImpactKind2D impactKind = CombatImpactKind2D.Generic,
        Vector2 impactDirection = default,
        EntityId2D attackerId = default)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIf(!attackSourceId.IsValid, "An attack source ID is required.", nameof(attackSourceId));
        ArgGuard.ThrowIfNull(knockback);

        var hitAny = false;
        _collision.Overlap(hitbox, _overlaps, targetLayer, includeSensors: true);
        foreach (var combatant in Candidates(hitbox, targetLayer))
        {
            if (!combatant.IsAlive ||
                combatant.Faction == attackerFaction)
            {
                continue;
            }

            var force = knockback(combatant);
            var direction = impactDirection == Vector2.Zero ? force : impactDirection;
            if (direction.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(direction);
            }

            var attackerPosition = Combatants.Find(attackerId.IsValid ? attackerId : attackSourceId)?.WorldObject.Transform.Position;
            var blocked = combatant is ICombatGuard2D protectedTarget && protectedTarget.CanBlock(hitbox.WorldBounds, direction, attackerPosition);
            if (combatant is IAuthoredHurt2D authored && !authored.OverlapsHurt(hitbox.WorldBounds) && !blocked)
            {
                continue;
            }

            if (!combatant.TryRegisterHit(attackSourceId, attackId))
            {
                continue;
            }

            var position = combatant is IAuthoredHurt2D hurt ? hurt.HurtContact(hitbox.WorldBounds)
                : Vector2.Clamp(hitbox.WorldBounds.Center, combatant.WorldObject.WorldBounds.Min, combatant.WorldObject.WorldBounds.Max);
            var contact = new CombatContact2D(attackSourceId, attackId,
                position ?? combatant.WorldObject.Transform.Position, direction, impactKind)
            {
                AttackerId = attackerId.IsValid ? attackerId : attackSourceId
            };
            // Physical contact can still bounce a downward attack off an invulnerable target.
            // Only accepted damage emits the contact fact used by audiovisual feedback.
            if (combatant is not ICombatGuard2D guard || !guard.TryBlock(hitbox.WorldBounds, direction, attackerPosition))
            {
                Damage(combatant, damage, force, contact);
            }

            hitAny = true;
            if (stopAfterFirstHit)
            {
                break;
            }
        }

        return hitAny;
    }

    public bool TryDamageFirst(
        SpatialObject2D hitbox,
        CombatFaction2D attackerFaction,
        uint targetLayer,
        int damage,
        Func<ICombatant2D, Vector2> knockback)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIfNull(knockback);

        _collision.Overlap(hitbox, _overlaps, targetLayer, includeSensors: true);
        foreach (var combatant in Candidates(hitbox, targetLayer))
        {
            if (!combatant.IsAlive || combatant.Faction == attackerFaction)
            {
                continue;
            }

            var force = knockback(combatant);
            if (combatant is IAuthoredHurt2D hurt && !hurt.OverlapsHurt(hitbox.WorldBounds) &&
                (combatant is not ICombatGuard2D protection || !protection.CanBlock(hitbox.WorldBounds, force, null)))
            {
                continue;
            }

            if (combatant is not ICombatGuard2D guard || !guard.TryBlock(hitbox.WorldBounds, force, null))
            {
                Damage(combatant, damage, force);
            }

            return true;
        }

        return false;
    }

    private ICombatant2D? GetCombatant(Collider2D collider) => Combatants.Find(collider.EntityId);

    /// <summary>
    /// The combatants an attack shape reaches, each once: plain bodies and registered hurt/guard colliders through the
    /// ordinary broad phase (deduplicated by target entity, never per hurt collider), then the legacy authored-hurt scan for
    /// combatants whose hurt geometry is not registered yet (their heads and long bodies extend past the terrain collider).
    /// </summary>
    private IEnumerable<ICombatant2D> Candidates(SpatialObject2D hitbox, uint targetLayer)
    {
        _visited.Clear();
        foreach (var overlap in _overlaps)
        {
            var attached = overlap.Collider.UserData as AttachedCollider;
            if (attached is not null && attached.Use is not (ColliderUses.Hurt or ColliderUses.Guard))
            {
                continue;
            }

            if (GetCombatant(overlap.Collider) is { } c && (attached is not null || c is not IAuthoredHurt2D) && _visited.Add(c.Id))
            {
                yield return c;
            }
        }
        foreach (var id in Combatants.Ids)
        {
            if (!_visited.Contains(id) && Combatants.Find(id) is ICombatant2D c && (c.Body.CollisionLayer & targetLayer) != 0 &&
                c is IAuthoredHurt2D { HurtRegistered: false } hurt && (hurt.OverlapsHurt(hitbox.WorldBounds) ||
                    c is ICombatGuard2D guard && guard.OverlapsGuard(hitbox.WorldBounds)))
            {
                _visited.Add(id);
                yield return c;
            }
        }
    }

    private void Damage(ICombatant2D combatant, int damage, Vector2 knockback, CombatContact2D? contact = null)
    {
        var wasAlive = combatant.IsAlive;
        if (!combatant.TakeDamage(damage, knockback))
        {
            return;
        }

        var killed = wasAlive && !combatant.IsAlive;
        if (killed && combatant.Faction == CombatFaction2D.Enemy)
        {
            DefeatedEnemies++;
        }

        DamageResolved?.Invoke(new CombatDamage2D(combatant.Id, combatant.Faction,
            combatant.WorldObject.Transform.Position, killed)
        {
            Contact = contact
        });
    }
}
