using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Physics;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

public sealed class CombatSystem2D(
    CollisionSystem2D collision,
    uint enemyLayer,
    ISoundEffectSink2D sounds)
{
    private readonly CollisionSystem2D _collision =
        ArgGuard.RequireNotNull(collision);
    private readonly uint _enemyLayer = enemyLayer;
    private readonly ISoundEffectSink2D _sounds =
        ArgGuard.RequireNotNull(sounds);
    private readonly List<CollisionOverlap2D> _overlaps = [];

    public int DefeatedEnemies { get; private set; }

    public bool ResolveAttack(
        SpatialObject2D hitbox,
        object attackSource,
        int attackId,
        int damage,
        Func<IEnemyCombatant2D, Vector2> knockback,
        bool stopAfterFirstHit = false)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIfNull(attackSource);
        ArgGuard.ThrowIfNull(knockback);

        var hitAny = false;
        _collision.Overlap(hitbox, _overlaps, _enemyLayer, includeSensors: true);
        foreach (var overlap in _overlaps)
        {
            if (GetEnemy(overlap.Collider) is not { IsAlive: true } enemy ||
                !enemy.TryRegisterHit(attackSource, attackId))
            {
                continue;
            }

            Damage(enemy, damage, knockback(enemy));
            hitAny = true;
            if (stopAfterFirstHit)
                break;
        }

        return hitAny;
    }

    public bool TryDamageFirst(
        SpatialObject2D hitbox,
        int damage,
        Func<IEnemyCombatant2D, Vector2> knockback)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIfNull(knockback);

        _collision.Overlap(hitbox, _overlaps, _enemyLayer, includeSensors: true);
        foreach (var overlap in _overlaps)
        {
            if (GetEnemy(overlap.Collider) is not { IsAlive: true } enemy)
                continue;

            Damage(enemy, damage, knockback(enemy));
            return true;
        }

        return false;
    }

    private static IEnemyCombatant2D? GetEnemy(Collider2D collider) =>
        collider.UserData is PhysicsBody2D { UserData: IEnemyCombatant2D enemy }
            ? enemy
            : null;

    private void Damage(IEnemyCombatant2D enemy, int damage, Vector2 knockback)
    {
        var wasAlive = enemy.IsAlive;
        enemy.TakeDamage(damage, knockback);
        if (wasAlive && !enemy.IsAlive)
        {
            DefeatedEnemies++;
            _sounds.Play(SoundEffect2D.EnemyDeath);
        }
        else if (enemy.IsAlive)
        {
            _sounds.Play(SoundEffect2D.EnemyHurt);
        }
    }
}
