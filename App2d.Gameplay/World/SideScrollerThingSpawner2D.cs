using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Constructs code-configured actors at authored world-space positions.</summary>
internal sealed class SideScrollerThingSpawner2D(
    Scene2D scene,
    CollisionSystem2D collision,
    PhysicsWorld2D physics,
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
        TextureCache2D textures,
        CombatSystem2D combat,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(things);
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(combat);
        ArgGuard.ThrowIfNull(sounds);

        foreach (var thing in things)
        {
            if (!thing.Enabled)
                continue;
            switch (thing.Kind)
            {
                case WorldThingKind2D.Shieldback:
                    Register(CreateShieldback(thing.Position, textures));
                    break;
                case WorldThingKind2D.BoilerBrute:
                    Register(new BoilerBrute2D(
                        scene,
                        collision,
                        physics,
                        textures,
                        thing.Position,
                        thing.Position.X - tileSize * 2f,
                        thing.Position.X + tileSize * 2f,
                        worldLayer,
                        enemyLayer,
                        sounds));
                    break;
                case WorldThingKind2D.Rival:
                    Register(new RivalEnemy2D(
                        scene,
                        collision,
                        physics,
                        textures,
                        traversal,
                        combat,
                        thing.Position,
                        thing.Position.X - tileSize * 6f,
                        thing.Position.X + tileSize * 6f,
                        worldLayer,
                        playerLayer,
                        enemyLayer,
                        sounds));
                    break;
                case WorldThingKind2D.TumbleProp:
                    Register(new TumbleProp2D(
                        scene,
                        physics,
                        thing.Position,
                        worldLayer,
                        enemyLayer));
                    break;
            }
        }

        enemies.UpdateStreaming(streamer.IsChunkActive);
    }

    private Shieldback2D CreateShieldback(Vector2 position, TextureCache2D textures)
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
            spatialObject,
            body,
            position.X - tileSize * 2f,
            position.X + tileSize * 2f,
            speed: 118f,
            health: 3);
        return new Shieldback2D(scene, textures, enemy);
    }

    private void Register(IEnemyActor2D enemy)
    {
        var homeChunk = tileMap.WorldToChunk(
            enemy.Combatant.WorldObject.Transform.Position);
        enemies.Register(enemy, homeChunk);
    }
}
