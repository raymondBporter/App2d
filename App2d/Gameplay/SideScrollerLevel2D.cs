using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Physics;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using App2d.Engine.Tiles;
using App2d.Gameplay.Audio;
using SkiaSharp;

namespace App2d.Gameplay;

public sealed class SideScrollerLevel2D
{
    // World length remains tile-count driven while the scale contract is settling.
    // Encounter pacing can choose a final duration independently of cell granularity.
    private const int WorldWidthTiles = 10_000;
    private const int WorldHeightTiles = 96;
    private const int ChunkSizeTiles = 32;
    private const int HorizontalChunkRadius = 2;
    private const int VerticalChunkRadius = 1;
    private const float SurfaceThickness = 8f;
    private const float OuterCornerSize = 12f;
    private const float InnerCornerSize = 10f;
    private const ulong WorldSeed = 0xA2D_2026_0823UL;
    private const ulong MechanicsEnemySeed = WorldSeed ^ 0xE11E_5EEDUL;

    private readonly float _tileSize;
    private readonly JumpableWorldGenerator2D _generator;
    private readonly Dictionary<TileChunk2D, LoadedChunk> _loadedChunks = [];
    private Scene2D? _scene;
    private PhysicsWorld2D? _physics;
    private IShader2D? _tileFillShader;
    private IShader2D? _topSurfaceShader;
    private IShader2D? _sideSurfaceShader;
    private IShader2D? _bottomSurfaceShader;
    private IShader2D? _outerCornerShader;
    private IShader2D? _innerCornerShader;
    private uint _worldLayer;
    private uint _playerLayer;
    private uint _enemyLayer;
    private bool _mechanicsEnemiesCreated;

    public SideScrollerLevel2D(TraversalMetrics2D traversal)
    {
        ArgGuard.ThrowIfNull(traversal);
        var tileSize = traversal.TileSize;
        ArgGuard.ThrowIfNotPositive(tileSize);

        _tileSize = tileSize;
        _generator = new JumpableWorldGenerator2D(
            WorldSeed,
            WorldWidthTiles,
            WorldHeightTiles,
            traversal);
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
            TileMap.Origin.Y + _generator.TerrainHeight(spawnTileX) * tileSize +
            traversal.PlayerColliderSize.Y / 2f + traversal.GroundProbeDistance);

        var goalTileX = WorldWidthTiles - 5;
        GoalX = TileCenterX(goalTileX);
        GoalGroundY = TileMap.Origin.Y + _generator.TerrainHeight(goalTileX) * tileSize;
    }

    public ProceduralTileMap2D TileMap { get; }
    public Vector2 SpawnPoint { get; }
    public float GoalX { get; }
    public float GoalGroundY { get; }
    public List<WorldObject2D> Platforms { get; } = [];
    public EnemySystem2D EnemySystem { get; } = new();
    public int ActiveChunkCount => _loadedChunks.Count;
    public int LoadedColliderCount => Platforms.Count;
    public static int MaximumActiveChunkCount =>
        (HorizontalChunkRadius * 2 + 1) * (VerticalChunkRadius * 2 + 1);

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
        PhysicsWorld2D physics,
        TextureCache2D textures,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(scene);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(textures);
        StateGuard.ThrowIf(
            _scene is not null,
            "The level environment has already been created.");

        _scene = scene;
        _physics = physics;
        _tileFillShader = new TextureShader2D(
            textures.Load("Terrain/RustCyberpunk/fill.png"),
            new Vector2(_tileSize));
        _topSurfaceShader = CreateTerrainShader(
            textures,
            "surface-top.png",
            new Vector2(_tileSize, SurfaceThickness));
        _sideSurfaceShader = CreateTerrainShader(
            textures,
            "surface-side.png",
            new Vector2(SurfaceThickness, _tileSize));
        _bottomSurfaceShader = CreateTerrainShader(
            textures,
            "surface-bottom.png",
            new Vector2(_tileSize, SurfaceThickness));
        _outerCornerShader = CreateTerrainShader(
            textures,
            "corner-outer.png",
            new Vector2(OuterCornerSize));
        _innerCornerShader = CreateTerrainShader(
            textures,
            "corner-inner.png",
            new Vector2(InnerCornerSize));
        _worldLayer = worldLayer;
        _playerLayer = playerLayer;
        _enemyLayer = enemyLayer;

        UpdateStreaming(SpawnPoint);
        CreateGoal(scene);
    }

    public void UpdateStreaming(Vector2 focus)
    {
        ArgGuard.ThrowIfNotFinite(focus);
        StateGuard.ThrowIf(
            _scene is null || _physics is null ||
            _tileFillShader is null || _topSurfaceShader is null ||
            _sideSurfaceShader is null || _bottomSurfaceShader is null ||
            _outerCornerShader is null || _innerCornerShader is null,
            "Create the level environment before streaming it.");

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

        EnemySystem.UpdateStreaming(_loadedChunks.ContainsKey);
    }

    public void CreateMechanicsPlaygroundEnemies(
        TextureCache2D textures,
        ISoundEffectSink2D sounds)
    {
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(sounds);
        var scene = StateGuard.RequireNotNull(
            _scene,
            "Create the level environment before its enemies.");
        var physics = StateGuard.RequireNotNull(
            _physics,
            "Create the level environment before its enemies.");
        StateGuard.ThrowIf(
            _mechanicsEnemiesCreated,
            "The mechanics enemies have already been created.");

        _mechanicsEnemiesCreated = true;
        var random = new SpatialRandom2D(MechanicsEnemySeed);
        var transparentShader = new SolidColorShader(SKColors.Transparent);

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

            var patrolMinX = TileCenterX(placement.PatrolMinTileX);
            var patrolMaxX = TileCenterX(placement.PatrolMaxTileX);
            if (index % 4 == 0)
            {
                var brute = new BoilerBrute2D(
                    scene,
                    physics,
                    textures,
                    new Vector2(
                        TileCenterX(placement.TileX),
                        TileMap.Origin.Y +
                        (placement.SurfaceTileY + 1) * _tileSize + 50f),
                    patrolMinX,
                    patrolMaxX,
                    _worldLayer,
                    _enemyLayer,
                    sounds);
                RegisterEnemy(brute);
                continue;
            }

            var worldObject = new WorldObject2D(
                new Capsule2D(new Vector2(-19f, 0f), new Vector2(19f, 0f), 22f),
                transparentShader);
            worldObject.Transform.Position = new Vector2(
                TileCenterX(placement.TileX),
                TileMap.Origin.Y + (placement.SurfaceTileY + 1) * _tileSize + 24f);
            scene.Add(worldObject);

            var body = physics.AddBody(worldObject, BodyMotionType2D.Dynamic);
            body.Restitution = 0f;
            body.Mass = 1.25f;
            body.CollisionLayer = _enemyLayer;
            body.CollisionMask = _worldLayer;

            var enemy = new PatrolEnemy2D(
                worldObject,
                body,
                patrolMinX,
                patrolMaxX,
                random.Range(index, 0, 95, 141, channel: 1),
                health: 3,
                transparentShader,
                transparentShader);
            var shieldback = new Shieldback2D(scene, textures, enemy);
            RegisterEnemy(shieldback);
        }

        EnemySystem.UpdateStreaming(_loadedChunks.ContainsKey);
    }

    private void LoadChunk(TileChunk2D chunk)
    {
        var colliders = new List<ChunkCollider>();
        foreach (var bounds in TileMap.BuildCollisionRectangles(chunk))
        {
            var platform = new WorldObject2D(
                AxisAlignedRectangle2D.FromSize(bounds.Size),
                _tileFillShader!);
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

            colliders.Add(new ChunkCollider(platform, body));
        }

        var surfaceVisuals = CreateSurfaceVisuals(chunk);
        _loadedChunks.Add(chunk, new LoadedChunk(colliders, surfaceVisuals));
    }

    private List<WorldObject2D> CreateSurfaceVisuals(TileChunk2D chunk)
    {
        var visuals = new List<WorldObject2D>();
        var startX = chunk.X * TileMap.ChunkSize;
        var startY = chunk.Y * TileMap.ChunkSize;
        var endX = Math.Min(startX + TileMap.ChunkSize, TileMap.Width);
        var endY = Math.Min(startY + TileMap.ChunkSize, TileMap.Height);

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                var surfaces = TileMap.GetExposedSurfaces(x, y);
                if (surfaces == TileSurface2D.None)
                    continue;

                var min = TileMap.Origin + new Vector2(x, y) * _tileSize;
                var max = min + new Vector2(_tileSize);
                if (surfaces.HasFlag(TileSurface2D.Top))
                {
                    AddSurfaceVisual(
                        visuals,
                        new Vector2(_tileSize, SurfaceThickness),
                        new Vector2(min.X + _tileSize / 2f, max.Y - SurfaceThickness / 2f),
                        _topSurfaceShader!);
                }
                if (surfaces.HasFlag(TileSurface2D.Right))
                {
                    AddSurfaceVisual(
                        visuals,
                        new Vector2(SurfaceThickness, _tileSize),
                        new Vector2(max.X - SurfaceThickness / 2f, min.Y + _tileSize / 2f),
                        _sideSurfaceShader!);
                }
                if (surfaces.HasFlag(TileSurface2D.Bottom))
                {
                    AddSurfaceVisual(
                        visuals,
                        new Vector2(_tileSize, SurfaceThickness),
                        new Vector2(min.X + _tileSize / 2f, min.Y + SurfaceThickness / 2f),
                        _bottomSurfaceShader!);
                }
                if (surfaces.HasFlag(TileSurface2D.Left))
                {
                    AddSurfaceVisual(
                        visuals,
                        new Vector2(SurfaceThickness, _tileSize),
                        new Vector2(min.X + SurfaceThickness / 2f, min.Y + _tileSize / 2f),
                        _sideSurfaceShader!);
                }

            }
        }

        // Corners are a separate pass because a diagonal inner corner can be
        // owned by a fully cardinally-surrounded tile. Drawing them last also
        // keeps the diagnostic join visible above both adjoining edge strips.
        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                var corners = TileMap.GetCorners(x, y);
                if (corners == TileCorner2D.None)
                    continue;

                var min = TileMap.Origin + new Vector2(x, y) * _tileSize;
                var max = min + new Vector2(_tileSize);
                AddCornerVisuals(visuals, min, max, corners);
            }
        }

        return visuals;
    }

    private void AddCornerVisuals(
        List<WorldObject2D> visuals,
        Vector2 min,
        Vector2 max,
        TileCorner2D corners)
    {
        var halfOuter = new Vector2(OuterCornerSize / 2f);

        if (corners.HasFlag(TileCorner2D.OuterTopRight))
            AddCornerVisual(visuals, max - halfOuter, OuterCornerSize, _outerCornerShader!);
        if (corners.HasFlag(TileCorner2D.OuterBottomRight))
            AddCornerVisual(
                visuals,
                new Vector2(max.X - OuterCornerSize / 2f, min.Y + OuterCornerSize / 2f),
                OuterCornerSize,
                _outerCornerShader!);
        if (corners.HasFlag(TileCorner2D.OuterBottomLeft))
            AddCornerVisual(visuals, min + halfOuter, OuterCornerSize, _outerCornerShader!);
        if (corners.HasFlag(TileCorner2D.OuterTopLeft))
            AddCornerVisual(
                visuals,
                new Vector2(min.X + OuterCornerSize / 2f, max.Y - OuterCornerSize / 2f),
                OuterCornerSize,
                _outerCornerShader!);

        if (corners.HasFlag(TileCorner2D.InnerTopRight))
            AddCornerVisual(visuals, max, InnerCornerSize, _innerCornerShader!);
        if (corners.HasFlag(TileCorner2D.InnerBottomRight))
            AddCornerVisual(
                visuals,
                new Vector2(max.X, min.Y),
                InnerCornerSize,
                _innerCornerShader!);
        if (corners.HasFlag(TileCorner2D.InnerBottomLeft))
            AddCornerVisual(visuals, min, InnerCornerSize, _innerCornerShader!);
        if (corners.HasFlag(TileCorner2D.InnerTopLeft))
            AddCornerVisual(
                visuals,
                new Vector2(min.X, max.Y),
                InnerCornerSize,
                _innerCornerShader!);
    }

    private static TextureShader2D CreateTerrainShader(
        TextureCache2D textures,
        string fileName,
        Vector2 logicalSize) =>
        new(
            textures.Load($"Terrain/RustCyberpunk/{fileName}"),
            logicalSize,
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp);

    private void AddCornerVisual(
        List<WorldObject2D> visuals,
        Vector2 position,
        float size,
        IShader2D shader) =>
        AddSurfaceVisual(visuals, new Vector2(size), position, shader);

    private void AddSurfaceVisual(
        List<WorldObject2D> visuals,
        Vector2 size,
        Vector2 position,
        IShader2D shader)
    {
        var visual = new WorldObject2D(
            AxisAlignedRectangle2D.FromSize(size),
            shader);
        visual.Transform.Position = position;
        _scene!.Add(visual);
        visuals.Add(visual);
    }

    private void UnloadChunk(TileChunk2D chunk)
    {
        var loaded = _loadedChunks[chunk];
        foreach (var item in loaded.Colliders)
        {
            _physics!.RemoveBody(item.Body);
            _scene!.Remove(item.Platform);
            Platforms.Remove(item.Platform);
        }
        foreach (var visual in loaded.SurfaceVisuals)
            _scene!.Remove(visual);

        _loadedChunks.Remove(chunk);
    }

    private void RegisterEnemy(IEnemyActor2D enemy)
    {
        var homeChunk = TileMap.WorldToChunk(
            enemy.Enemy.WorldObject.Transform.Position);
        EnemySystem.Register(enemy, homeChunk);
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

    private sealed record LoadedChunk(
        List<ChunkCollider> Colliders,
        List<WorldObject2D> SurfaceVisuals);

    private readonly record struct ChunkCollider(
        WorldObject2D Platform,
        PhysicsBody2D Body);

    private readonly record struct EnemyPlacement(
        int TileX,
        int SurfaceTileY,
        int PatrolMinTileX,
        int PatrolMaxTileX);
}
