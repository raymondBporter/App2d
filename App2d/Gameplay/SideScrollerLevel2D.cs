using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Tiles;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class SideScrollerLevel2D
{
    // 10,000 tiles is 24.8 minutes at uninterrupted maximum run speed. Normal
    // traversal should put a complete left-to-right trip in the 30-60 minute band.
    private const int WorldWidthTiles = 10_000;
    private const int WorldHeightTiles = 96;
    private const int ChunkSizeTiles = 32;
    private const int HorizontalChunkRadius = 2;
    private const int VerticalChunkRadius = 1;
    private const ulong WorldSeed = 0xA2D_2026_0823UL;
    private const ulong MechanicsEnemySeed = WorldSeed ^ 0xE11E_5EEDUL;

    private readonly float _tileSize;
    private readonly JumpableWorldGenerator2D _generator;
    private readonly Dictionary<TileChunk2D, LoadedChunk> _loadedChunks = [];
    private readonly List<MechanicsEnemy> _mechanicsEnemies = [];
    private Scene2D? _scene;
    private PhysicsWorld2D? _physics;
    private IShader2D? _platformShader;
    private IShader2D? _grassShader;
    private uint _worldLayer;
    private uint _playerLayer;
    private uint _enemyLayer;
    private bool _mechanicsEnemiesCreated;

    public SideScrollerLevel2D(float tileSize)
    {
        if (!float.IsFinite(tileSize) || tileSize <= 0f)
            throw new ArgumentOutOfRangeException(nameof(tileSize));

        _tileSize = tileSize;
        _generator = new JumpableWorldGenerator2D(
            WorldSeed,
            WorldWidthTiles,
            WorldHeightTiles);
        TileMap = new ProceduralTileMap2D(
            WorldWidthTiles,
            WorldHeightTiles,
            tileSize,
            ChunkSizeTiles,
            _generator.IsSolid,
            new Vector2(-512f, -640f));

        const int spawnTileX = 4;
        SpawnPoint = new Vector2(
            TileCenterX(spawnTileX),
            TileMap.Origin.Y + _generator.TerrainHeight(spawnTileX) * tileSize + 38f);

        var goalTileX = WorldWidthTiles - 5;
        GoalX = TileCenterX(goalTileX);
        GoalGroundY = TileMap.Origin.Y + _generator.TerrainHeight(goalTileX) * tileSize;
    }

    public ProceduralTileMap2D TileMap { get; }
    public Vector2 SpawnPoint { get; }
    public float GoalX { get; }
    public float GoalGroundY { get; }
    public List<WorldObject2D> Platforms { get; } = [];
    public List<PatrolEnemy2D> Enemies { get; } = [];
    public int ActiveChunkCount => _loadedChunks.Count;
    public int LoadedColliderCount => Platforms.Count;
    public static int MaximumActiveChunkCount =>
        (HorizontalChunkRadius * 2 + 1) * (VerticalChunkRadius * 2 + 1);

    public void CreateEnvironment(
        Scene2D scene,
        PhysicsWorld2D physics,
        IShader2D platformShader,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(physics);
        ArgumentNullException.ThrowIfNull(platformShader);
        if (_scene is not null)
            throw new InvalidOperationException("The level environment has already been created.");

        _scene = scene;
        _physics = physics;
        _platformShader = platformShader;
        _grassShader = new SolidColorShader(new SKColor(101, 205, 116));
        _worldLayer = worldLayer;
        _playerLayer = playerLayer;
        _enemyLayer = enemyLayer;

        UpdateStreaming(SpawnPoint);
        CreateGoal(scene);
    }

    public void UpdateStreaming(Vector2 focus)
    {
        if (_scene is null || _physics is null ||
            _platformShader is null || _grassShader is null)
        {
            throw new InvalidOperationException("Create the level environment before streaming it.");
        }

        var center = TileMap.WorldToChunk(focus);
        var minimumX = Math.Max(0, center.X - HorizontalChunkRadius);
        var maximumX = Math.Min(TileMap.ChunkColumns - 1, center.X + HorizontalChunkRadius);
        var minimumY = Math.Max(0, center.Y - VerticalChunkRadius);
        var maximumY = Math.Min(TileMap.ChunkRows - 1, center.Y + VerticalChunkRadius);

        foreach (var chunk in _loadedChunks.Keys.ToArray())
        {
            if (chunk.X < minimumX || chunk.X > maximumX ||
                chunk.Y < minimumY || chunk.Y > maximumY)
            {
                UnloadChunk(chunk);
            }
        }

        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var chunk = new TileChunk2D(x, y);
                if (!_loadedChunks.ContainsKey(chunk))
                    LoadChunk(chunk);
            }
        }

        UpdateMechanicsEnemyStreaming();
    }

    public void CreateMechanicsPlaygroundEnemies()
    {
        if (_scene is null || _physics is null)
            throw new InvalidOperationException("Create the level environment before its enemies.");
        if (_mechanicsEnemiesCreated)
            throw new InvalidOperationException("The mechanics enemies have already been created.");

        _mechanicsEnemiesCreated = true;
        var random = new SpatialRandom2D(MechanicsEnemySeed);
        var hitShader = new SolidColorShader(new SKColor(255, 245, 245));
        var coralShader = new LinearGradientShader(
            new SKColor(255, 101, 137),
            new SKColor(179, 48, 102));
        var violetShader = new LinearGradientShader(
            new SKColor(178, 125, 255),
            new SKColor(91, 61, 178));

        const int enemyCount = 12;
        for (var index = 0; index < enemyCount; index++)
        {
            var preferredX = 14 + index * 7 + random.Range(index, 0, -2, 3);
            var wantsElevation = index % 3 == 2;
            var foundPlacement = wantsElevation
                ? TryFindElevatedPlacement(preferredX, 5 + index % 4 * 4, out var placement)
                : TryFindGroundPlacement(preferredX, out placement);
            if (!foundPlacement && wantsElevation)
                foundPlacement = TryFindGroundPlacement(preferredX, out placement);
            if (!foundPlacement)
                continue;

            IShader2D normalShader = index % 2 == 0 ? coralShader : violetShader;
            var worldObject = new WorldObject2D(
                new Capsule2D(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f),
                normalShader);
            worldObject.Transform.Position = new Vector2(
                TileCenterX(placement.TileX),
                TileMap.Origin.Y + (placement.SurfaceTileY + 1) * _tileSize + 24f);
            _scene.Add(worldObject);

            var body = _physics.AddBody(worldObject, BodyMotionType2D.Dynamic);
            body.Restitution = 0f;
            body.Mass = 1.25f;
            body.CollisionLayer = _enemyLayer;
            body.CollisionMask = _worldLayer;

            var enemy = new PatrolEnemy2D(
                worldObject,
                body,
                TileCenterX(placement.PatrolMinTileX),
                TileCenterX(placement.PatrolMaxTileX),
                random.Range(index, 0, 95, 141, channel: 1),
                health: 3,
                normalShader,
                hitShader);
            Enemies.Add(enemy);
            var homeChunk = TileMap.WorldToChunk(worldObject.Transform.Position);
            _mechanicsEnemies.Add(new MechanicsEnemy(enemy, homeChunk));
        }

        UpdateMechanicsEnemyStreaming();
    }

    private void LoadChunk(TileChunk2D chunk)
    {
        var objects = new List<ChunkObject>();
        foreach (var bounds in TileMap.BuildCollisionRectangles(chunk))
        {
            var platform = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(bounds.Size),
                _platformShader!);
            platform.Transform.Position = bounds.Center;
            _scene!.Add(platform);
            Platforms.Add(platform);

            var body = _physics!.AddBody(platform, BodyMotionType2D.Static);
            body.Restitution = 0f;
            body.CollisionLayer = _worldLayer;
            body.CollisionMask = _playerLayer | _enemyLayer;
            body.IsOneWayPlatform =
                bounds.Size.Y <= _tileSize + 0.01f &&
                TileMap.WorldToTileY(bounds.Min.Y) >= 5;

            const float capHeight = 9f;
            var cap = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(new Vector2(bounds.Size.X, capHeight)),
                _grassShader!);
            cap.Transform.Position = new Vector2(
                bounds.Center.X,
                bounds.Max.Y - capHeight / 2f);
            _scene.Add(cap);
            objects.Add(new ChunkObject(platform, cap, body));
        }

        _loadedChunks.Add(chunk, new LoadedChunk(objects));
    }

    private void UnloadChunk(TileChunk2D chunk)
    {
        var loaded = _loadedChunks[chunk];
        foreach (var item in loaded.Objects)
        {
            _physics!.RemoveBody(item.Body);
            _scene!.Remove(item.Platform);
            _scene.Remove(item.Cap);
            Platforms.Remove(item.Platform);
        }

        _loadedChunks.Remove(chunk);
    }

    private void UpdateMechanicsEnemyStreaming()
    {
        foreach (var fixture in _mechanicsEnemies)
            fixture.Enemy.SetSimulationEnabled(_loadedChunks.ContainsKey(fixture.HomeChunk));
    }

    private bool TryFindGroundPlacement(int preferredX, out EnemyPlacement placement)
    {
        for (var distance = 0; distance <= 18; distance++)
        {
            var direction = distance % 2 == 0 ? 1 : -1;
            var x = preferredX + (distance + 1) / 2 * direction;
            if (x < 3 || x >= TileMap.Width - 3)
                continue;

            var surfaceY = _generator.TerrainHeight(x) - 1;
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
            if (x < 2 || x >= TileMap.Width - 2)
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
        if (!_generator.IsSolid(x, surfaceY) || _generator.IsSolid(x, surfaceY + 1))
            return false;

        while (minimumX > 1 &&
               _generator.IsSolid(minimumX - 1, surfaceY) &&
               !_generator.IsSolid(minimumX - 1, surfaceY + 1))
        {
            minimumX--;
        }

        while (maximumX < TileMap.Width - 2 &&
               _generator.IsSolid(maximumX + 1, surfaceY) &&
               !_generator.IsSolid(maximumX + 1, surfaceY + 1))
        {
            maximumX++;
        }

        return true;
    }

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

    private sealed record LoadedChunk(List<ChunkObject> Objects);

    private readonly record struct ChunkObject(
        WorldObject2D Platform,
        WorldObject2D Cap,
        PhysicsBody2D Body);

    private readonly record struct MechanicsEnemy(
        PatrolEnemy2D Enemy,
        TileChunk2D HomeChunk);

    private readonly record struct EnemyPlacement(
        int TileX,
        int SurfaceTileY,
        int PatrolMinTileX,
        int PatrolMaxTileX);
}
