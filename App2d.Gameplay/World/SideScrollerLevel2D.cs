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
using SkiaSharp;
using System.Numerics;

namespace App2d.Gameplay.World;

public sealed class SideScrollerLevel2D
{
    public const int WorldWidthTiles = 640;
    public const int WorldHeightTiles = 96;
    public const int ChunkSizeTiles = 32;
    public static Vector2 WorldOrigin { get; } = new(-512f, -640f);

    private readonly float _tileSize;
    private readonly TraversalMetrics2D _traversal;
    private readonly Func<int, int> _groundY;
    private IReadOnlyList<MovingPlatformSpec2D> _movingPlatformSpecs;
    private readonly IReadOnlyList<WorldThingSpec2D> _worldThingSpecs;
    private readonly List<MovingPlatform2D> _movingPlatforms = [];
    private readonly DirtyChunkTracker2D _dirtyChunks = new();
    private LevelEnvironment? _environment;
    private bool _authoredWorldThingsCreated;

    public SideScrollerLevel2D(
        TraversalMetrics2D traversal,
        IChunkedTileMap2D tileMap,
        Func<int, int> groundY,
        IReadOnlyList<MovingPlatformSpec2D>? movingPlatforms = null,
        IReadOnlyList<WorldThingSpec2D>? worldThings = null)
    {
        ArgGuard.ThrowIfNull(traversal);
        ArgGuard.ThrowIfNull(tileMap);
        ArgGuard.ThrowIfNull(groundY);
        _traversal = traversal;
        _tileSize = traversal.TileSize;
        ArgGuard.ThrowIfNotPositive(_tileSize);
        _groundY = groundY;
        _movingPlatformSpecs = movingPlatforms ?? [];
        _worldThingSpecs = worldThings ?? [];
        TileMap = tileMap;

        // Only an editable map can change under us. A read-only map never raises the event.
        if (tileMap is EditableTileMap2D editable)
            editable.ChunkChanged += _dirtyChunks.Mark;

        StateGuard.ThrowIf(
            tileMap.TileSize != _tileSize,
            $"The loaded map's tile size ({tileMap.TileSize}) does not match " +
            $"the traversal metrics' tile size ({_tileSize}).");
        StateGuard.ThrowIf(
            tileMap.Width != WorldWidthTiles,
            $"The loaded map's width ({tileMap.Width}) does not match " +
            $"the expected world width ({WorldWidthTiles}).");
        StateGuard.ThrowIf(
            tileMap.Height != WorldHeightTiles,
            $"The loaded map's height ({tileMap.Height}) does not match " +
            $"the expected world height ({WorldHeightTiles}).");
        StateGuard.ThrowIf(
            tileMap.Origin != WorldOrigin,
            $"The loaded map's origin ({tileMap.Origin}) does not match " +
            $"the expected world origin ({WorldOrigin}).");

        var authoredSpawn = _worldThingSpecs.FirstOrDefault(
            thing => thing.Enabled && thing.Kind == WorldThingKind2D.PlayerSpawn);
        if (authoredSpawn is not null)
        {
            SpawnPoint = authoredSpawn.Position;
        }
        else
        {
            // An empty authored layer must still boot into the editor. This is a safety
            // fallback, not persisted example content; placing a player-spawn replaces it.
            const int fallbackSpawnTileX = 4;
            SpawnPoint = new Vector2(
                TileCenterX(fallbackSpawnTileX),
                TileMap.Origin.Y + _groundY(fallbackSpawnTileX) * _tileSize +
                traversal.PlayerColliderSize.Y / 2f + traversal.GroundProbeDistance);
        }

        GoalThing = _worldThingSpecs.FirstOrDefault(
            thing => thing.Enabled && thing.Kind == WorldThingKind2D.Goal);
        GoalX = GoalThing?.Position.X ?? float.PositiveInfinity;
        GoalGroundY = GoalThing?.Position.Y ?? TileMap.Origin.Y;
    }

    public IChunkedTileMap2D TileMap { get; }
    public Vector2 SpawnPoint { get; }
    public float GoalX { get; }
    public float GoalGroundY { get; }
    public WorldThingSpec2D? GoalThing { get; }
    public IReadOnlyList<SpatialObject2D> Platforms => RequireEnvironment().Streamer.Platforms;
    public IReadOnlyList<MovingPlatform2D> MovingPlatforms => _movingPlatforms;
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
        return TileMap.Origin.Y + _groundY(tileX) * _tileSize;
    }

    public bool TryGetSpikeSource(Bounds2D actorBounds, out float sourceX)
    {
        if (!actorBounds.IsFinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actorBounds),
                actorBounds,
                "Bounds must be finite.");
        }

        var startX = Math.Clamp(
            (int)MathF.Floor((actorBounds.Min.X - TileMap.Origin.X) / _tileSize),
            0,
            TileMap.Width - 1);
        var endX = Math.Clamp(
            (int)MathF.Floor((actorBounds.Max.X - TileMap.Origin.X) / _tileSize),
            0,
            TileMap.Width - 1);
        var startY = Math.Clamp(
            (int)MathF.Floor((actorBounds.Min.Y - TileMap.Origin.Y) / _tileSize),
            0,
            TileMap.Height - 1);
        var endY = Math.Clamp(
            (int)MathF.Floor((actorBounds.Max.Y - TileMap.Origin.Y) / _tileSize),
            0,
            TileMap.Height - 1);

        var horizontalInset = _tileSize * 0.1f;
        for (var y = startY; y <= endY; y++)
        {
            for (var x = startX; x <= endX; x++)
            {
                if (!TileMap.GetTileKind(x, y).IsSpikes())
                    continue;

                var tileMin = TileMap.Origin + new Vector2(x, y) * _tileSize;
                var spikeBounds = new Bounds2D(
                    tileMin + new Vector2(horizontalInset, 0f),
                    tileMin + new Vector2(_tileSize - horizontalInset, _tileSize * 0.9f));
                if (!actorBounds.Intersects(spikeBounds))
                    continue;

                sourceX = tileMin.X + _tileSize / 2f;
                return true;
            }
        }

        sourceX = 0f;
        return false;
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

        var tilesets = TileMap.TilesetIds
            .Select(id => SideScrollerTerrainTileset2D.Load(textures, id, _tileSize))
            .ToArray();
        var tilesetResolver = new SideScrollerTerrainTilesetResolver2D(
            (x, y) => tilesets[TileMap.GetTilesetIndex(x, y)]);
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
            playerLayer,
            enemyLayer);

        UpdateStreaming(SpawnPoint);
        CreateMovingPlatformsFromSpecs();
        if (GoalThing is not null)
            CreateGoal(scene);
    }

    public void UpdateMovingPlatforms(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        foreach (var platform in _movingPlatforms)
            platform.Update(deltaSeconds);
    }

    public void ReloadMovingPlatforms(IReadOnlyList<MovingPlatformSpec2D> specs)
    {
        ArgGuard.ThrowIfNull(specs);
        foreach (var platform in _movingPlatforms)
            platform.Dispose();
        _movingPlatforms.Clear();
        _movingPlatformSpecs = specs;
        CreateMovingPlatformsFromSpecs();
    }

    public void UpdateStreaming(Vector2 focus)
    {
        ArgGuard.ThrowIfNotFinite(focus);
        var environment = RequireEnvironment();
        environment.Streamer.Update(focus);
        EnemySystem.UpdateStreaming(environment.Streamer.IsChunkActive);
    }

    /// <summary>
    /// Rebuilds every chunk marked dirty since the last call. Cheap when nothing changed.
    /// </summary>
    public void FlushDirtyChunks()
    {
        if (_dirtyChunks.IsEmpty)
            return;

        var environment = RequireEnvironment();
        _dirtyChunks.Flush(environment.Streamer.Invalidate);
    }

    public void CreateAuthoredWorldThings(
        TextureCache2D textures,
        CombatSystem2D combat,
        ISoundEffectSink2D sounds)
    {
        StateGuard.ThrowIf(
            _authoredWorldThingsCreated,
            "The authored world things have already been created.");
        var environment = RequireEnvironment();
        new SideScrollerThingSpawner2D(
            environment.Scene,
            environment.Collision,
            environment.Physics,
            TileMap,
            EnemySystem,
            environment.Streamer,
            _traversal,
            _tileSize,
            environment.WorldLayer,
            environment.PlayerLayer,
            environment.EnemyLayer)
            .Create(_worldThingSpecs, textures, combat, sounds);
        _authoredWorldThingsCreated = true;
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
        pole.Transform.Position = GoalThing!.Position;
        scene.Add(pole);

        var flag = new WorldObject2D(
            new ConvexPolygon2D(
            [
                Vector2.Zero,
                new Vector2(92f, -30f),
                new Vector2(0f, -60f)
            ]),
            new SolidColorShader(new SKColor(255, 79, 120)));
        flag.Transform.Position = GoalThing.Position + new Vector2(0f, 185f);
        scene.Add(flag);
    }

    private void CreateMovingPlatformsFromSpecs()
    {
        var environment = RequireEnvironment();
        foreach (var spec in _movingPlatformSpecs)
        {
            if (!spec.Enabled)
                continue;
            _movingPlatforms.Add(new MovingPlatform2D(
                environment.Scene,
                environment.Physics,
                spec.Position,
                spec.Travel,
                spec.Size,
                spec.Speed,
                environment.WorldLayer,
                environment.PlayerLayer | environment.EnemyLayer,
                spec.Color));
        }
    }

    private float TileCenterX(int x) =>
        TileMap.Origin.X + (x + 0.5f) * _tileSize;

    private sealed record LevelEnvironment(
        Scene2D Scene,
        CollisionSystem2D Collision,
        PhysicsWorld2D Physics,
        SideScrollerChunkStreamer2D Streamer,
        uint WorldLayer,
        uint PlayerLayer,
        uint EnemyLayer);
}
