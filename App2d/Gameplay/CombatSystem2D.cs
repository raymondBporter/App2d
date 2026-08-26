using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision.Contacts;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

public sealed class CombatSystem2D(
    IReadOnlyList<PatrolEnemy2D> enemies,
    ISoundEffectSink2D sounds)
{
    private readonly IReadOnlyList<PatrolEnemy2D> _enemies =
        ArgGuard.RequireNotNull(enemies);
    private readonly ISoundEffectSink2D _sounds =
        ArgGuard.RequireNotNull(sounds);

    public int DefeatedEnemies { get; private set; }

    public bool ResolveAttack(
        WorldObject2D hitbox,
        object attackSource,
        int attackId,
        int damage,
        Func<PatrolEnemy2D, Vector2> knockback,
        bool stopAfterFirstHit = false)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIfNull(attackSource);
        ArgGuard.ThrowIfNull(knockback);

        var hitAny = false;
        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive ||
                !Intersects(hitbox, enemy.WorldObject) ||
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
        WorldObject2D hitbox,
        int damage,
        Func<PatrolEnemy2D, Vector2> knockback)
    {
        ArgGuard.ThrowIfNull(hitbox);
        ArgGuard.ThrowIfNull(knockback);

        foreach (var enemy in _enemies)
        {
            if (!enemy.IsAlive || !Intersects(hitbox, enemy.WorldObject))
                continue;

            Damage(enemy, damage, knockback(enemy));
            return true;
        }

        return false;
    }

    public static bool Intersects(WorldObject2D first, WorldObject2D second) =>
        first.WorldBounds.Intersects(second.WorldBounds) &&
        ShapeCollision2D.TryGetContact(first, second, out _);

    private void Damage(PatrolEnemy2D enemy, int damage, Vector2 knockback)
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
