using System.Numerics;
using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Audio;
using App2d.Physics;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class SideScrollerLevel2D
{
    private const int WorldWidthTiles = 640;
    private const int WorldHeightTiles = 96;
    private const int ChunkSizeTiles = 32;
    private const ulong WorldSeed = 0xA2D_2026_0823UL;

    private readonly float _tileSize;
    private readonly JumpableWorldGenerator2D _generator;
    private LevelEnvironment? _environment;
    private bool _mechanicsEnemiesCreated;

    public SideScrollerLevel2D(TraversalMetrics2D traversal)
    {
        ArgGuard.ThrowIfNull(traversal);
        _tileSize = traversal.TileSize;
        ArgGuard.ThrowIfNotPositive(_tileSize);

        _generator = new JumpableWorldGenerator2D(
            WorldSeed,
            WorldWidthTiles,
            WorldHeightTiles,
            traversal);
        TileMap = new ProceduralTileMap2D(
            WorldWidthTiles,
            WorldHeightTiles,
            _tileSize,
            ChunkSizeTiles,
            _generator.GetTileKind,
            new Vector2(-512f, -640f));

        const int spawnTileX = 4;
        SpawnPoint = new Vector2(
            TileCenterX(spawnTileX),
            TileMap.Origin.Y + _generator.TerrainHeight(spawnTileX) * _tileSize +
            traversal.PlayerColliderSize.Y / 2f + traversal.GroundProbeDistance);

        const int goalTileX = WorldWidthTiles - 5;
        GoalX = TileCenterX(goalTileX);
        GoalGroundY = TileMap.Origin.Y + _generator.TerrainHeight(goalTileX) * _tileSize;
    }

    public IChunkedTileMap2D TileMap { get; }
    public Vector2 SpawnPoint { get; }
    public float GoalX { get; }
    public float GoalGroundY { get; }
    public IReadOnlyList<SpatialObject2D> Platforms => RequireEnvironment().Streamer.Platforms;
    public EnemySystem2D EnemySystem { get; } = new();
    public int ActiveChunkCount => _environment?.Streamer.ActiveChunkCount ?? 0;
    public int LoadedColliderCount => _environment?.Streamer.LoadedColliderCount ?? 0;
    public static int MaximumActiveChunkCount =>
        SideScrollerChunkStreamer2D.MaximumActiveChunkCount;

    public float GetCameraFloorY(float worldX)
    {
        if (!float.IsFinite(worldX))
            ArgGuard.ThrowOutOfRange(worldX, "Value must be finite.");

        var tileX = (int)MathF.Floor((worldX - TileMap.Origin.X) / _tileSize);
        tileX = Math.Clamp(tileX, 0, TileMap.Width - 1);
        return TileMap.Origin.Y + _generator.TerrainHeight(tileX) * _tileSize;
    }

    public void CreateEnvironment(
        Scene2D scene,
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        TextureCache2D textures,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(textures);
        StateGuard.ThrowIf(
            _environment is not null,
            "The level environment has already been created.");

        SideScrollerTerrainTileset2D[] tilesets =
        [
            SideScrollerTerrainTileset2D.Load(
                textures,
                "rust-cyberpunk",
                _tileSize),
            SideScrollerTerrainTileset2D.CreateCollisionTest()
        ];
        var tilesetResolver = new SideScrollerTerrainTilesetResolver2D(
            (x, _) => tilesets[GetPreviewTilesetIndex(
                x,
                TileMap.Width,
                tilesets.Length)]);
        var visualFactory = new SideScrollerTerrainVisualFactory2D(
            scene,
            TileMap,
            tilesetResolver);
        var streamer = new SideScrollerChunkStreamer2D(
            scene,
            physics,
            TileMap,
            visualFactory,
            worldLayer,
            playerLayer | enemyLayer);
        _environment = new LevelEnvironment(
            scene,
            collision,
            physics,
            streamer,
            worldLayer,
            enemyLayer);

        UpdateStreaming(SpawnPoint);
        CreateGoal(scene);
    }

    public void UpdateStreaming(Vector2 focus)
    {
        ArgGuard.ThrowIfNotFinite(focus);
        var environment = RequireEnvironment();
        environment.Streamer.Update(focus);
        EnemySystem.UpdateStreaming(environment.Streamer.IsChunkActive);
    }

    public void CreateMechanicsPlaygroundEnemies(
        TextureCache2D textures,
        ISoundEffectSink2D sounds)
    {
        StateGuard.ThrowIf(
            _mechanicsEnemiesCreated,
            "The mechanics enemies have already been created.");
        var environment = RequireEnvironment();
        new SideScrollerEncounterSpawner2D(
            environment.Scene,
            environment.Collision,
            environment.Physics,
            TileMap,
            _generator,
            EnemySystem,
            environment.Streamer,
            _tileSize,
            environment.WorldLayer,
            environment.EnemyLayer)
            .Create(textures, sounds);
        _mechanicsEnemiesCreated = true;
    }

    private LevelEnvironment RequireEnvironment() =>
        StateGuard.RequireNotNull(
            _environment,
            "Create the level environment before using it.");

    private void CreateGoal(Scene2D scene)
    {
        var pole = new WorldObject2D(
            new Capsule2D(Vector2.Zero, new Vector2(0f, 190f), 5f),
            new SolidColorShader(new SKColor(238, 242, 232)));
        pole.Transform.Position = new Vector2(GoalX, GoalGroundY);
        scene.Add(pole);

        var flag = new WorldObject2D(
            new ConvexPolygon2D(
            [
                Vector2.Zero,
                new Vector2(92f, -30f),
                new Vector2(0f, -60f)
            ]),
            new SolidColorShader(new SKColor(255, 79, 120)));
        flag.Transform.Position = new Vector2(GoalX, GoalGroundY + 185f);
        scene.Add(flag);
    }

    private float TileCenterX(int x) =>
        TileMap.Origin.X + (x + 0.5f) * _tileSize;

    // Temporary preview rule. A saved level can replace this resolver with its
    // authored per-tile or per-region tileset IDs without changing the renderer.
    private static int GetPreviewTilesetIndex(
        int tileX,
        int mapWidth,
        int tilesetCount) =>
        Math.Min((int)((long)tileX * tilesetCount / mapWidth), tilesetCount - 1);

    private sealed record LevelEnvironment(
        Scene2D Scene,
        CollisionSystem2D Collision,
        PhysicsWorld2D Physics,
        SideScrollerChunkStreamer2D Streamer,
        uint WorldLayer,
        uint EnemyLayer);
}
