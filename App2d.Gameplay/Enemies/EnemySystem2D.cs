using App2d.Core;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Persons;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.Enemies;

public sealed class EnemySystem2D
{
    private readonly List<RegisteredEnemy> _registeredEnemies = [];
    private readonly List<ICombatant2D> _combatants = [];

    public IReadOnlyList<ICombatant2D> Combatants => _combatants;
    public int Count => _combatants.Count(
        combatant => combatant.Faction == CombatFaction2D.Enemy);

    public void Register(IEnemyActor2D actor, TileChunk2D homeChunk)
    {
        ArgGuard.ThrowIfNull(actor);
        StateGuard.ThrowIf(
            _registeredEnemies.Any(item => ReferenceEquals(item.Actor, actor)),
            "The enemy actor is already registered.");
        StateGuard.ThrowIf(
            _combatants.Any(item => ReferenceEquals(item, actor.Combatant)),
            "The enemy combatant is already registered.");

        actor.SetSimulationEnabled(false);
        _registeredEnemies.Add(
            new RegisteredEnemy(
                actor,
                actor as IEnemyAttackSource2D,
                homeChunk));
        _combatants.Add(actor.Combatant);
    }

    public void UpdateStreaming(Func<TileChunk2D, bool> isChunkActive)
    {
        ArgGuard.ThrowIfNull(isChunkActive);

        foreach (var registered in _registeredEnemies)
        {
            var isEnabled = isChunkActive(registered.HomeChunk);
            if (registered.IsEnabled == isEnabled)
                continue;

            registered.IsEnabled = isEnabled;
            registered.Actor.SetSimulationEnabled(isEnabled);
        }
    }

    public void Update(float deltaSeconds, Vector2 targetPosition)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        ArgGuard.ThrowIfNotFinite(targetPosition);

        foreach (var registered in _registeredEnemies)
        {
            if (registered.IsEnabled)
                registered.Actor.Update(deltaSeconds, targetPosition);
        }
    }

    public void SyncAfterPhysics()
    {
        foreach (var registered in _registeredEnemies)
        {
            if (registered.IsEnabled)
                registered.Actor.SyncAfterPhysics();
        }
    }

    public bool TryResolvePlayerHits(Person2D player)
    {
        ArgGuard.ThrowIfNull(player);

        var playerDefeated = false;
        foreach (var registered in _registeredEnemies)
        {
            if (registered.IsEnabled && registered.AttackSource is not null)
            {
                playerDefeated |=
                    registered.AttackSource.TryResolvePlayerHit(player);
            }
        }

        return playerDefeated;
    }

    public IEnumerable<SpatialObject2D> GetActiveAttackHitboxes()
    {
        foreach (var registered in _registeredEnemies)
        {
            if (!registered.IsEnabled || registered.AttackSource is null)
                continue;

            foreach (var hitbox in registered.AttackSource.GetActiveAttackHitboxes())
                yield return hitbox;
        }
    }

    private sealed class RegisteredEnemy(
        IEnemyActor2D actor,
        IEnemyAttackSource2D? attackSource,
        TileChunk2D homeChunk)
    {
        public IEnemyActor2D Actor { get; } = actor;
        public IEnemyAttackSource2D? AttackSource { get; } = attackSource;
        public TileChunk2D HomeChunk { get; } = homeChunk;
        public bool IsEnabled { get; set; }
    }
}
