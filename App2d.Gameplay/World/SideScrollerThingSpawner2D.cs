using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Constructs code-configured actors at authored world-space positions.</summary>
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
    public void Create(
        IReadOnlyList<WorldThingSpec2D> things,
        CombatSystem2D combat, App2d.Core.Characters.EntityCatalog? characters = null, App2d.Core.Characters.AuthoredCatalog? authored = null)
    {
        ArgGuard.ThrowIfNull(things);
        ArgGuard.ThrowIfNull(combat);

        foreach (var thing in things)
        {
            if (!thing.Enabled)
                continue;
            var typeId = thing.Kind switch
            {
                WorldThingKind2D.Shieldback => "needle",
                WorldThingKind2D.BoilerBrute => "maul",
                WorldThingKind2D.Rival => "cinder",
                WorldThingKind2D.GreenDinosaur => "scrap-hound",
                _ => null
            };
            var entityId = thing.Kind switch
            {
                WorldThingKind2D.Shieldback => "spear-guard",
                WorldThingKind2D.GreenDinosaur => "stalker-pest",
                WorldThingKind2D.Rival => "cinder-gunner",
                _ => null
            };
            if (entityId is not null && authored?.Entities.GetValueOrDefault(entityId) is { } entity)
            {
                Register(new AuthoredEntityEnemy2D(ids.Allocate(), entity, physics, thing.Position, worldLayer, enemyLayer));
                continue;
            }
            if (characters is not null && typeId is not null)
            {
                Register(new AuthoredEnemy2D(ids.Allocate(), characters, typeId, collision, physics,
                    thing.Position, worldLayer, enemyLayer));
                continue;
            }
            switch (thing.Kind)
            {
                case WorldThingKind2D.Shieldback:
                    Register(CreateShieldback(thing.Position));
                    break;
                case WorldThingKind2D.BoilerBrute:
                    Register(new BoilerBrute2D(
                        ids.Allocate(),
                        collision,
                        physics,
                        thing.Position,
                        thing.Position.X - tileSize * 2f,
                        thing.Position.X + tileSize * 2f,
                        worldLayer,
                        enemyLayer));
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
                case WorldThingKind2D.GreenDinosaur:
                    Register(CreateGreenDinosaur(thing.Position));
                    break;
                case WorldThingKind2D.TumbleProp:
                    Register(new TumbleProp2D(
                        ids.Allocate(),
                        physics,
                        thing.Position,
                        worldLayer,
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
        var spatialObject = new SpatialObject2D(
            new Capsule2D(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f));
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

    private GreenDinosaur2D CreateGreenDinosaur(
        Vector2 position)
    {
        var spatialObject = new SpatialObject2D(
            new Capsule2D(new Vector2(0f, -14f), new Vector2(0f, 14f), 17f));
        spatialObject.Transform.Position = position;
        var body = physics.AddBody(spatialObject, BodyMotionType2D.Dynamic);
        body.Restitution = 0f;
        body.Mass = 1.4f;
        body.CollisionLayer = enemyLayer;
        body.CollisionMask = worldLayer;
        var enemy = new PatrolEnemy2D(
            ids.Allocate(),
            spatialObject,
            body,
            position.X - tileSize * 2f,
            position.X + tileSize * 2f,
            speed: 82f,
            health: 4);
        return new GreenDinosaur2D(enemy);
    }
}
