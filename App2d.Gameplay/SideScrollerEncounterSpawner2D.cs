using System.Numerics;
using App2d.Engine;
using App2d.Engine.Collision;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering.Textures;
using App2d.Engine.Tiles;
using App2d.Gameplay.Audio;

namespace App2d.Gameplay;

internal sealed class SideScrollerEncounterSpawner2D(
    Scene2D scene,
    CollisionSystem2D collision,
    PhysicsWorld2D physics,
    IChunkedTileMap2D tileMap,
    JumpableWorldGenerator2D generator,
    EnemySystem2D enemies,
    SideScrollerChunkStreamer2D streamer,
    float tileSize,
    uint worldLayer,
    uint enemyLayer)
{
    private const ulong EnemySeed = 0xA2D_2026_0823UL ^ 0xE11E_5EEDUL;
    // About 1.5 default camera widths beyond the spawn at tile 4.
    private const int FirstEnemyTileX = 76;
    private const int OpeningEnemyCount = 3;
    private const int OpeningEnemySpacingTiles = 12;
    private const int RegularEnemySpacingTiles = 7;

    public void Create(TextureCache2D textures, ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(sounds);
        var random = new SpatialRandom2D(EnemySeed);

        const int enemyCount = 12;
        for (var index = 0; index < enemyCount; index++)
        {
            var openingIndex = Math.Min(index, OpeningEnemyCount - 1);
            var regularIndex = Math.Max(0, index - (OpeningEnemyCount - 1));
            var preferredX = FirstEnemyTileX +
                openingIndex * OpeningEnemySpacingTiles +
                regularIndex * RegularEnemySpacingTiles +
                random.Range(index, 0, -2, 3);
            var wantsElevation = index % 3 == 2;
            var foundPlacement = wantsElevation
                ? TryFindElevatedPlacement(preferredX, 5 + index % 4 * 4, out var placement)
                : TryFindGroundPlacement(preferredX, out placement);
            if (!foundPlacement && wantsElevation)
                foundPlacement = TryFindGroundPlacement(preferredX, out placement);
            if (!foundPlacement)
                continue;

            var patrolMinX = TileCenterX(placement.PatrolMinTileX);
            var patrolMaxX = TileCenterX(placement.PatrolMaxTileX);
            if (index % 4 == 0)
            {
                Register(new BoilerBrute2D(
                    scene,
                    collision,
                    physics,
                    textures,
                    new Vector2(
                        TileCenterX(placement.TileX),
                        tileMap.Origin.Y +
                        (placement.SurfaceTileY + 1) * tileSize + 50f),
                    patrolMinX,
                    patrolMaxX,
                    worldLayer,
                    enemyLayer,
                    sounds));
                continue;
            }

            var spatialObject = new SpatialObject2D(
                new Capsule2D(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f));
            spatialObject.Transform.Position = new Vector2(
                TileCenterX(placement.TileX),
                tileMap.Origin.Y + (placement.SurfaceTileY + 1) * tileSize + 24f);
            var body = physics.AddBody(spatialObject, BodyMotionType2D.Dynamic);
            body.Restitution = 0f;
            body.Mass = 1.25f;
            body.CollisionLayer = enemyLayer;
            body.CollisionMask = worldLayer;

            var enemy = new PatrolEnemy2D(
                spatialObject,
                body,
                patrolMinX,
                patrolMaxX,
                random.Range(index, 0, 95, 141, channel: 1),
                health: 3);
            Register(new Shieldback2D(scene, textures, enemy));
        }

        if (TryFindGroundPlacement(30, out var propPlacement))
        {
            Register(new TumbleProp2D(
                scene,
                physics,
                new Vector2(TileCenterX(propPlacement.TileX), tileMap.Origin.Y + (propPlacement.SurfaceTileY + 1) * tileSize + 40f),
                worldLayer,
                enemyLayer));
        }

        enemies.UpdateStreaming(streamer.IsChunkActive);
    }

    private void Register(IEnemyActor2D enemy)
    {
        var homeChunk = tileMap.WorldToChunk(
            enemy.Combatant.WorldObject.Transform.Position);
        enemies.Register(enemy, homeChunk);
    }

    private bool TryFindGroundPlacement(int preferredX, out EnemyPlacement placement)
    {
        for (var distance = 0; distance <= 18; distance++)
        {
            var direction = distance % 2 == 0 ? 1 : -1;
            var x = preferredX + (distance + 1) / 2 * direction;
            if (x < 3 || x >= tileMap.Width - 3)
                continue;

            var surfaceY = generator.TerrainHeight(x) - 1;
            if (TryGetSurfaceRun(x, surfaceY, out var minimumX, out var maximumX) &&
                maximumX - minimumX >= 4)
            {
                placement = new EnemyPlacement(
                    x,
                    surfaceY,
                    Math.Max(minimumX + 1, x - 2),
                    Math.Min(maximumX - 1, x + 2));
                return placement.PatrolMinTileX < placement.PatrolMaxTileX;
            }
        }

        placement = default;
        return false;
    }

    private bool TryFindElevatedPlacement(
        int preferredX,
        int preferredSurfaceY,
        out EnemyPlacement placement)
    {
        for (var distance = 0; distance <= 30; distance++)
        {
            var direction = distance % 2 == 0 ? 1 : -1;
            var x = preferredX + (distance + 1) / 2 * direction;
            if (x < 2 || x >= tileMap.Width - 2)
                continue;

            for (var yOffset = 0; yOffset <= 4; yOffset += 2)
            {
                var surfaceY = preferredSurfaceY + yOffset;
                if (!TryGetSurfaceRun(x, surfaceY, out var minimumX, out var maximumX) ||
                    maximumX - minimumX < 3)
                {
                    continue;
                }

                placement = new EnemyPlacement(
                    x,
                    surfaceY,
                    Math.Max(minimumX + 1, x - 2),
                    Math.Min(maximumX - 1, x + 2));
                if (placement.PatrolMinTileX < placement.PatrolMaxTileX)
                    return true;
            }
        }

        placement = default;
        return false;
    }

    private bool TryGetSurfaceRun(int x, int surfaceY, out int minimumX, out int maximumX)
    {
        minimumX = x;
        maximumX = x;
        if (!IsStandableSurface(x, surfaceY))
            return false;

        while (minimumX > 1 && IsStandableSurface(minimumX - 1, surfaceY))
            minimumX--;
        while (maximumX < tileMap.Width - 2 && IsStandableSurface(maximumX + 1, surfaceY))
            maximumX++;
        return true;
    }

    private bool IsStandableSurface(int x, int y) =>
        generator.GetTileKind(x, y) != TileKind2D.Empty &&
        generator.GetTileKind(x, y + 1) == TileKind2D.Empty;

    private float TileCenterX(int x) =>
        tileMap.Origin.X + (x + 0.5f) * tileSize;

    private readonly record struct EnemyPlacement(
        int TileX,
        int SurfaceTileY,
        int PatrolMinTileX,
        int PatrolMaxTileX);
}
