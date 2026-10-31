using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Physics;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Constructs authored enemies and the remaining code-defined actors at world-space placements.</summary>
internal sealed class SideScrollerThingSpawner2D(
    CollisionSystem2D collision,
    PhysicsWorld2D physics,
    EntityIdAllocator2D ids,
    IChunkedTileMap2D tileMap,
    EnemySystem2D enemies,
    SideScrollerChunkStreamer2D streamer,
    TraversalMetrics2D traversal,
    float tileSize,
    uint worldLayer,
    uint playerLayer,
    uint enemyLayer)
{
    public void Create(IReadOnlyList<WorldThingSpec2D> things, CombatSystem2D combat, AuthoredCatalog? authored = null)
    {
        ArgGuard.ThrowIfNull(things);
        ArgGuard.ThrowIfNull(combat);

        foreach (var thing in things)
        {
            if (!thing.Enabled)
            {
                continue;
            }

            // P9c: the placement kind -> authored entity binding is the player profile's `spawns` data, not a switch.
            var entityId = authored?.Profiles.GetValueOrDefault(GameProfile.GameId)?.Spawns.GetValueOrDefault(thing.Kind.ToString());
            if (entityId is not null && authored?.Entities.GetValueOrDefault(entityId) is { } entity)
            {
                Register(new AuthoredEntityEnemy2D(ids.Allocate(), entity, physics, thing.Position, worldLayer, enemyLayer));
                continue;
            }
            switch (thing.Kind)
            {
                case WorldThingKind2D.ClubCaveman:
                case WorldThingKind2D.BoilerBrute:
                case WorldThingKind2D.GreenDinosaur:
                case WorldThingKind2D.RockThrower:
                case WorldThingKind2D.BabyTriceratops:
                case WorldThingKind2D.Pterosaur:
                case WorldThingKind2D.ShieldDefender:
                    throw new InvalidDataException(entityId is null
                        ? $"{thing.Kind} requires an authored entity bound by the spawns of the 'game' profile (profiles/game.json); none is loaded."
                        : $"{thing.Kind} requires its authored entity '{entityId}', which is missing or does not compile.");
                case WorldThingKind2D.Shieldback:
                    Register(CreateShieldback(thing.Position));
                    break;
                case WorldThingKind2D.Rival:
                    Register(new RivalEnemy2D(
                        ids,
                        collision,
                        physics,
                        traversal,
                        combat,
                        thing.Position,
                        thing.Position.X - tileSize * 6f,
                        thing.Position.X + tileSize * 6f,
                        worldLayer,
                        playerLayer,
                        enemyLayer));
                    break;
            }
        }

        enemies.UpdateStreaming(streamer.IsChunkActive);

        void Register(IEnemyActor2D enemy)
        {
            var homeChunk = tileMap.WorldToChunk(
                enemy.Combatant.WorldObject.Transform.Position);
            combat.Combatants.Register(enemy.Combatant);
            enemies.Register(enemy, homeChunk);
        }
    }

    private Shieldback2D CreateShieldback(Vector2 position)
    {
        var spatialObject = new SpatialObject2D(Capsule2D.Builder.Create(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f));
        spatialObject.Transform.Position = position;
        var body = physics.AddBody(spatialObject, BodyMotionType2D.Dynamic);
        body.Restitution = 0f;
        body.Mass = 1.25f;
        body.CollisionLayer = enemyLayer;
        body.CollisionMask = worldLayer;
        var enemy = new PatrolEnemy2D(
            ids.Allocate(),
            spatialObject,
            body,
            position.X - tileSize * 2f,
            position.X + tileSize * 2f,
            speed: 118f,
            health: 3);
        return new Shieldback2D(enemy);
    }
}
