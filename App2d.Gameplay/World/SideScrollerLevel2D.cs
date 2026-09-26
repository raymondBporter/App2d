using App2d.Collision;
using App2d.Core;
using App2d.Core.Geometry;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.Player;
using App2d.Physics;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.World;

public sealed partial class SideScrollerLevel2D : IDisposable
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
    private readonly List<SavePoint2D> _savePoints = [];
    private readonly DirtyChunkTracker2D _dirtyChunks = new();
    private LevelEnvironment? _environment;
    private LevelContent2D? _content;
    private (long Streamer, long Definition) _contentKey;
    private long _contentRevision;
    private bool _authoredWorldThingsCreated;
    private CombatantRegistry2D? _combatants;
    private bool _disposed;

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
            editable.ChunkChanged += OnMapChanged;

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
    public IReadOnlyList<WorldThingSpec2D> SavePointThings => _worldThingSpecs
        .Where(thing => thing.Enabled && thing.Kind == WorldThingKind2D.SavePoint)
        .ToArray();
    public IReadOnlyList<SpatialObject2D> Platforms => RequireEnvironment().Streamer.Platforms;
    public IReadOnlyList<MovingPlatform2D> MovingPlatforms => _movingPlatforms;
    public EnemySystem2D EnemySystem { get; } = new();
    public int ActiveChunkCount => _environment?.Streamer.ActiveChunkCount ?? 0;
    public int LoadedColliderCount => _environment?.Streamer.LoadedColliderCount ?? 0;
    public static int MaximumActiveChunkCount =>
        SideScrollerChunkStreamer2D.MaximumActiveChunkCount;

    public WorldThingSpec2D? FindSavePoint(long thingId) =>
        _worldThingSpecs.FirstOrDefault(
            thing => thing.Enabled &&
                thing.Kind == WorldThingKind2D.SavePoint &&
                thing.ThingId == thingId);

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

    public void CreateSimulation(
        CollisionSystem2D collision, PhysicsWorld2D physics, EntityIdAllocator2D ids,
        uint worldLayer, uint playerLayer, uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(ids);
        StateGuard.ThrowIf(_environment is not null, "The level simulation has already been created.");
        var streamer = new SideScrollerChunkStreamer2D(physics, TileMap, worldLayer, playerLayer | enemyLayer);
        _environment = new LevelEnvironment(collision, physics, ids, streamer, worldLayer, playerLayer, enemyLayer);
        UpdateStreaming(SpawnPoint);
        CreateMovingPlatformsFromSpecs();
        CreateSavePoints();
    }

    /// <summary>Per-tick dynamic observation.</summary>
    public WorldState2D CaptureWorld() => new(
        _movingPlatforms.Select(p => p.CaptureState()).ToImmutableArray(),
        _savePoints.Select(p => p.CaptureState()).ToImmutableArray());

    /// <summary>Shared until streaming or authoring changes; successive ticks return the same instance.</summary>
    public LevelContent2D CaptureContent()
    {
        var streamer = RequireEnvironment().Streamer;
        var key = (streamer.Version, _definitionRevision);
        if (_content is null || _contentKey != key)
        {
            _contentKey = key;
            _content = new LevelContent2D(++_contentRevision, streamer.CaptureState(),
                _movingPlatforms.Select(p => p.CaptureDefinition()).ToImmutableArray(),
                _savePoints.Select(p => p.CapturePlacement()).ToImmutableArray(),
                GoalThing?.Position);
        }
        return _content;
    }

    public WorldThingSpec2D? UpdateSavePoints(float deltaSeconds, Bounds2D playerBounds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        foreach (var savePoint in _savePoints)
        {
            if (savePoint.Update(deltaSeconds, playerBounds))
                return savePoint.Spec;
        }

        return null;
    }

    public void SetActiveSavePoint(long? thingId)
    {
        foreach (var savePoint in _savePoints)
            savePoint.SetActive(savePoint.Spec.ThingId == thingId);
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
        _definitionRevision++;
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
        CombatSystem2D combat, App2d.Core.Characters.EntityCatalog? characters = null, App2d.Core.Characters.AuthoredCatalog? authored = null)
    {
        StateGuard.ThrowIf(
            _authoredWorldThingsCreated,
            "The authored world things have already been created.");
        var environment = RequireEnvironment();
        new SideScrollerThingSpawner2D(
            environment.Collision,
            environment.Physics,
            environment.Ids,
            TileMap,
            EnemySystem,
            environment.Streamer,
            _traversal,
            _tileSize,
            environment.WorldLayer,
            environment.PlayerLayer,
            environment.EnemyLayer)
            .Create(_worldThingSpecs, combat, characters, authored);
        _combatants = combat.Combatants;
        _authoredWorldThingsCreated = true;
    }

    private LevelEnvironment RequireEnvironment() =>
        StateGuard.RequireNotNull(
            _environment,
            "Create the level simulation before using it.");

    private void CreateMovingPlatformsFromSpecs()
    {
        var environment = RequireEnvironment();
        foreach (var spec in _movingPlatformSpecs)
        {
            if (!spec.Enabled)
                continue;
            _movingPlatforms.Add(new MovingPlatform2D(
                environment.Ids.Allocate(),
                environment.Physics,
                spec.Position,
                spec.Travel,
                spec.Size,
                spec.Speed,
                environment.WorldLayer,
                environment.PlayerLayer | environment.EnemyLayer,
                spec.ThingId, spec.ColorArgb));
        }
    }

    private void CreateSavePoints()
    {
        foreach (var spec in _worldThingSpecs)
        {
            if (spec.Enabled && spec.Kind == WorldThingKind2D.SavePoint)
            {
                _savePoints.Add(new SavePoint2D(
                    spec,
                    _traversal.PlayerColliderSize.Y / 2f + _traversal.GroundProbeDistance));
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (TileMap is EditableTileMap2D editable) editable.ChunkChanged -= OnMapChanged;
        foreach (var platform in _movingPlatforms) platform.Dispose();
        _movingPlatforms.Clear();
        _environment?.Streamer.Dispose();
        foreach (var enemy in EnemySystem.Combatants)
        {
            _environment?.Physics.RemoveBody(enemy.Body);
            _combatants?.Unregister(enemy.Id);
        }
    }

    private float TileCenterX(int x) =>
        TileMap.Origin.X + (x + 0.5f) * _tileSize;

    private sealed record LevelEnvironment(
        CollisionSystem2D Collision,
        PhysicsWorld2D Physics,
        EntityIdAllocator2D Ids,
        SideScrollerChunkStreamer2D Streamer,
        uint WorldLayer,
        uint PlayerLayer,
        uint EnemyLayer);
}
