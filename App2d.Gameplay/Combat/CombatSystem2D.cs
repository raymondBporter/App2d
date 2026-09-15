using App2d.Collision;
using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Combat;

public sealed class CombatSystem2D(
    CollisionSystem2D collision,
    CombatantRegistry2D combatants)
{
    private readonly CollisionSystem2D _collision =
        ArgGuard.RequireNotNull(collision);
    private readonly List<CollisionOverlap2D> _overlaps = [];

    public CombatantRegistry2D Combatants { get; } = ArgGuard.RequireNotNull(combatants);
    public int DefeatedEnemies { get; private set; }
    public event Action<CombatDamage2D>? DamageResolved;
    internal void RestoreSimulation(int defeatedEnemies) => DefeatedEnemies = defeatedEnemies;

    public bool ResolveAttack(
        SpatialObject2D hitbox,
        EntityId2D attackSourceId,
        int attackId,
        CombatFaction2D attackerFaction,
        uint targetLayer,
        int damage,
        Func<ICombatant2D, Vector2> knockback,
        bool stopAfterFirstHit = false)
    {
        ArgGuard.ThrowIfNull(hitbox);
        if (!attackSourceId.IsValid)
            throw new ArgumentException("An attack source ID is required.", nameof(attackSourceId));
        ArgGuard.ThrowIfNull(knockback);

        var hitAny = false;
        _collision.Overlap(hitbox, _overlaps, targetLayer, includeSensors: true);
        foreach (var overlap in _overlaps)
        {
            if (GetCombatant(overlap.Collider) is not { IsAlive: true } combatant ||
                combatant.Faction == attackerFaction ||
                !combatant.TryRegisterHit(attackSourceId, attackId))
            {
                continue;
            }

            Damage(combatant, damage, knockback(combatant));
            hitAny = true;
            if (stopAfterFirstHit)
                break;
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
        foreach (var overlap in _overlaps)
        {
            if (GetCombatant(overlap.Collider) is not { IsAlive: true } combatant || combatant.Faction == attackerFaction)
                continue;

            Damage(combatant, damage, knockback(combatant));
            return true;
        }

        return false;
    }

    private ICombatant2D? GetCombatant(Collider2D collider) =>
        Combatants.Find(collider.EntityId);

    private void Damage(ICombatant2D combatant, int damage, Vector2 knockback)
    {
        var wasAlive = combatant.IsAlive;
        if (!combatant.TakeDamage(damage, knockback))
            return;

        var killed = wasAlive && !combatant.IsAlive;
        if (killed && combatant.Faction == CombatFaction2D.Enemy) DefeatedEnemies++;
        DamageResolved?.Invoke(new CombatDamage2D(combatant.Id, combatant.Faction,
            combatant.WorldObject.Transform.Position, killed));
    }
}
