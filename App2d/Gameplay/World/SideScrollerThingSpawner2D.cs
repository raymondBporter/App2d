using App2d.Contracts.Player;
using App2d.Core;
using App2d.Core.Collision;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Physics;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Gameplay.Characters;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;

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
    public ICombatant2D? Create(IReadOnlyList<WorldThingSpec2D> things, CombatSystem2D combat,
        bool characters, long? goalGuardianThingId)
    {
        Ensure.ThrowIfNull(things);
        Ensure.ThrowIfNull(combat);

        ICombatant2D? guardian = null;
        foreach (var thing in things)
        {
            if (!thing.Enabled)
            {
                continue;
            }

            if (characters && GameCharacters.Enemy(thing.Kind) is { } type)
            {
                Register(new CharacterEnemy2D(ids.Allocate(), type, physics, thing.Position, worldLayer, enemyLayer), thing.ThingId);
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
                case WorldThingKind2D.Centaur:
                case WorldThingKind2D.ShieldDefender:
                    throw new InvalidDataException($"{thing.Kind} exists only as one of the game's characters; this session was made without them.");
                case WorldThingKind2D.Shieldback:
                    Register(CreateShieldback(thing.Position), thing.ThingId);
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
                        enemyLayer), thing.ThingId);
                    break;
            }
        }

        enemies.UpdateStreaming(streamer.IsChunkActive);
        if (goalGuardianThingId is not null && guardian is null)
        {
            throw new InvalidDataException($"Goal guardian {goalGuardianThingId} must name an enabled enemy placement.");
        }

        return guardian;

        void Register(IEnemyActor2D enemy, long thingId)
        {
            var homeChunk = tileMap.WorldToChunk(
                enemy.Combatant.WorldObject.Position);
            combat.Combatants.Register(enemy.Combatant);
            enemies.Register(enemy, homeChunk);
            if (thingId == goalGuardianThingId)
            {
                guardian = enemy.Combatant;
            }
        }
    }

    private Shieldback2D CreateShieldback(Vector2f position)
    {
        var spatialObject = new SpatialObject2D(Capsule2.Builder.Create(new Vector2f(-19f, 0f), new Vector2f(19f, 0f), 22f))
        {
            Position = position
        };
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
