using App2d.Collision;
using App2d.Core;
using App2d.Gameplay.Audio;
using App2d.Physics;
using System.Numerics;

namespace App2d.Gameplay;

public sealed class CombatSystem2D(
    CollisionSystem2D collision,
    ISoundEffectSink2D sounds)
{
    private readonly CollisionSystem2D _collision =
        ArgGuard.RequireNotNull(collision);
    private readonly ISoundEffectSink2D _sounds =
        ArgGuard.RequireNotNull(sounds);
    private readonly List<CollisionOverlap2D> _overlaps = [];

    public int DefeatedEnemies { get; private set; }

    public bool ResolveAttack(
        SpatialObject2D hitbox,
        object attackSource,
        int attackId,
        CombatFaction2D attackerFaction,
        uint targetLayer,
        int damage,
        Func<ICombatant2D, Vector2> knockback,
        bool stopAfterFirstHit = false)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIfNull(attackSource);
        ArgGuard.ThrowIfNull(knockback);

        var hitAny = false;
        _collision.Overlap(hitbox, _overlaps, targetLayer, includeSensors: true);
        foreach (var overlap in _overlaps)
        {
            if (GetCombatant(overlap.Collider) is not { IsAlive: true } combatant ||
                combatant.Faction == attackerFaction ||
                !combatant.TryRegisterHit(attackSource, attackId))
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
            if (GetCombatant(overlap.Collider) is not { IsAlive: true } combatant ||
                combatant.Faction == attackerFaction)
                continue;

            Damage(combatant, damage, knockback(combatant));
            return true;
        }

        return false;
    }

    private static ICombatant2D? GetCombatant(Collider2D collider) =>
        collider.UserData is PhysicsBody2D { UserData: ICombatant2D combatant }
            ? combatant
            : null;

    private void Damage(ICombatant2D combatant, int damage, Vector2 knockback)
    {
        var wasAlive = combatant.IsAlive;
        if (!combatant.TakeDamage(damage, knockback))
            return;

        if (wasAlive && !combatant.IsAlive && combatant.Faction == CombatFaction2D.Enemy)
        {
            DefeatedEnemies++;
            _sounds.Play(SoundEffect2D.EnemyDeath);
        }
        else if (combatant.IsAlive && combatant.Faction != CombatFaction2D.Player)
        {
            _sounds.Play(SoundEffect2D.EnemyHurt);
        }
    }
}
