using App2d.Contracts.Player;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Characters.Authored;
using App2d.Core.Collision;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Spatial;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Gameplay.Combat;
using App2d.Gameplay.Enemies;
using App2d.Gameplay.World.Vegetation;
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
    private readonly IReadOnlyList<WorldThingSpec2D> _worldThingSpecs;
    private IReadOnlyList<ObjectPlacement2D> _objectPlacements;
    private readonly List<AuthoredObject2D> _objects = [];
    private readonly List<SavePoint2D> _savePoints = [];
    private readonly DirtyChunkTracker2D _dirtyChunks = new();
    private LevelEnvironment? _environment;
    private LevelContent2D? _content;
    private (long Streamer, long Definition) _contentKey;
    private long _contentRevision;
    private bool _authoredWorldThingsCreated;
    private CombatantRegistry2D? _combatants;
    private bool _disposed;

    public SideScrollerLevel2D(TraversalMetrics2D traversal, IChunkedTileMap2D tileMap, IReadOnlyList<WorldThingSpec2D>? worldThings = null, IEnumerable<WorldZone2D>? zones = null, IEnumerable<GiantTree2D>? giantTrees = null, IReadOnlyList<ObjectPlacement2D>? objects = null)
    {
        ArgGuard.ThrowIfNull(traversal);
        ArgGuard.ThrowIfNull(tileMap);
        _traversal = traversal;
        _tileSize = traversal.TileSize;
        ArgGuard.Positive(_tileSize);
        _worldThingSpecs = worldThings ?? [];
        _objectPlacements = ValidatePlacements(objects ?? []);
        TileMap = tileMap;
        Zones = zones is null ? [] : [.. zones];
        GiantTrees = giantTrees is null ? [] : ValidateTrees(giantTrees);

        // Only an editable map can change under us. A read-only map never raises the event.
        if (tileMap is IChangingTileMap2D editable)
        {
            editable.ChunkChanged += OnMapChanged;
        }

        StateGuard.ThrowIf(
            tileMap.TileSize != _tileSize,
            $"The loaded map's tile size ({tileMap.TileSize}) does not match " +
            $"the traversal metrics' tile size ({_tileSize}).");
        var authoredSpawn = _worldThingSpecs
            .FirstOrDefault(thing => thing.Enabled && thing.Kind == WorldThingKind2D.PlayerSpawn);
        if (authoredSpawn is not null)
        {
            SpawnPoint = authoredSpawn.Position;
        }
        else
        {
            // An empty authored layer must still boot into the editor. This is a safety
            // fallback, not persisted example content; placing a player-spawn replaces it.
            var fallbackSpawnTileX = Math.Min(4, TileMap.Width - 1);
            SpawnPoint = new Vector2(
                TileCenterX(fallbackSpawnTileX),
                TileMap.Origin.Y + TileGroundHeights2D.AtColumn(TileMap, fallbackSpawnTileX) * _tileSize +
                traversal.PlayerColliderSize.Y / 2f + traversal.GroundProbeDistance);
        }

        GoalThing = _worldThingSpecs
            .FirstOrDefault(thing => thing.Enabled && thing.Kind == WorldThingKind2D.Goal);
        GoalX = GoalThing?.Position.X ?? float.PositiveInfinity;
        GoalGroundY = GoalThing?.Position.Y ?? TileMap.Origin.Y;
    }

    public IChunkedTileMap2D TileMap { get; }
    public System.Collections.Immutable.ImmutableArray<WorldZone2D> Zones { get; }
    public Vector2 SpawnPoint { get; }
    public float GoalX { get; }
    public float GoalGroundY { get; }
    public WorldThingSpec2D? GoalThing { get; }
    public IReadOnlyList<WorldThingSpec2D> SavePointThings => [.. _worldThingSpecs.Where(thing => thing.Enabled && thing.Kind == WorldThingKind2D.SavePoint)];
    public IReadOnlyList<SpatialObject2D> Platforms => RequireEnvironment().Streamer.Platforms;
    public IReadOnlyList<AuthoredObject2D> Objects => _objects;
    public EnemySystem2D EnemySystem { get; } = new();
    public int ActiveChunkCount => _environment?.Streamer.ActiveChunkCount ?? 0;
    public int LoadedColliderCount => _environment?.Streamer.LoadedColliderCount ?? 0;
    public static int MaximumActiveChunkCount => SideScrollerChunkStreamer2D.MaximumActiveChunkCount;

    public WorldThingSpec2D? FindSavePoint(long thingId) =>
        _worldThingSpecs.FirstOrDefault(
            thing => thing.Enabled &&
                thing.Kind == WorldThingKind2D.SavePoint &&
                thing.ThingId == thingId);

    public bool TryGetSpikeSource(Rect2D actorBounds, out float sourceX)
    {
        if (!actorBounds.IsFinite)
        {
            ArgGuard.ThrowOutOfRange(
                actorBounds,
                "Bounds must be finite.");
        }

        var cells = TileMap.GridGeometry.GetCellRange(actorBounds, TileMap.GridSize);

        var horizontalInset = _tileSize * 0.1f;
        foreach (var cell in cells)
        {
            if (!TileMap.GetTileKind(cell.X, cell.Y).IsSpikes())
            {
                continue;
            }

            var tileMin = TileMap.GridGeometry.GetCellBounds(cell).Min;
            var spikeBounds = new Rect2D(
                tileMin + new Vector2(horizontalInset, 0f),
                tileMin + new Vector2(_tileSize - horizontalInset, _tileSize * 0.9f));
            if (!actorBounds.Intersects(spikeBounds))
            {
                continue;
            }

            sourceX = tileMin.X + _tileSize / 2f;
            return true;
        }

        sourceX = 0f;
        return false;
    }

    public void CreateSimulation(
        CollisionSystem2D collision,
        PhysicsWorld2D physics,
        EntityIdAllocator2D ids,
        uint worldLayer,
        uint playerLayer,
        uint enemyLayer)
    {
        ArgGuard.ThrowIfNull(collision);
        ArgGuard.ThrowIfNull(physics);
        ArgGuard.ThrowIfNull(ids);
        StateGuard.ThrowIf(_environment is not null, "The level simulation has already been created.");
        var streamer = new SideScrollerChunkStreamer2D(physics, TileMap, worldLayer, worldLayer | playerLayer | enemyLayer);
        _environment = new LevelEnvironment(collision, physics, ids, streamer, worldLayer, playerLayer, enemyLayer);
        UpdateStreaming(SpawnPoint);
        CreateObjects();
        CreateGiantTreeBranches();
        CreateSavePoints();
    }

    /// <summary>Per-tick dynamic observation.</summary>
    public WorldState2D CaptureWorld() => new([.. _savePoints.Select(p => p.CaptureState())])
    {
        CutGrass = _cutGrass,
        Objects = [.. _objects.Select(o => o.CaptureState())]
    };

    /// <summary>Shared until streaming or authoring changes; successive ticks return the same instance.</summary>
    public LevelContent2D CaptureContent()
    {
        var streamer = RequireEnvironment().Streamer;
        var key = (streamer.Version, _definitionRevision);
        if (_content is null || _contentKey != key)
        {
            _contentKey = key;
            _content = new LevelContent2D(++_contentRevision, streamer.CaptureState(), [.. _savePoints.Select(p => p.CapturePlacement())], GoalThing?.Position)
            {
                Objects = [.. _objects.Select(o => o.CaptureDefinition())],
                Zones = Zones,
                GiantTrees = GiantTrees
            };
        }
        return _content;
    }

    public WorldThingSpec2D? UpdateSavePoints(float deltaSeconds, Rect2D playerBounds)
    {
        ArgGuard.NonNegative(deltaSeconds);
        foreach (var savePoint in _savePoints)
        {
            if (savePoint.Update(deltaSeconds, playerBounds))
            {
                return savePoint.Spec;
            }
        }

        return null;
    }

    public void SetActiveSavePoint(long? thingId)
    {
        foreach (var savePoint in _savePoints)
        {
            savePoint.SetActive(savePoint.Spec.ThingId == thingId);
        }
    }

    public void UpdateWorldMotion(float deltaSeconds)
    {
        ArgGuard.NonNegative(deltaSeconds);
        foreach (var item in _objects) item.Update(deltaSeconds);
    }

    /// <summary>Replaces every placed object; existing objects are disposed and content gets a new revision.</summary>
    public void ReloadObjects(IReadOnlyList<ObjectPlacement2D> placements)
    {
        ArgGuard.ThrowIfNull(placements);
        ValidatePlacements(placements);
        _definitionRevision++;
        foreach (var item in _objects) item.Dispose();
        _objects.Clear();
        _objectPlacements = placements;
        CreateObjects();
    }

    private static IReadOnlyList<ObjectPlacement2D> ValidatePlacements(IReadOnlyList<ObjectPlacement2D> placements)
    {
        foreach (var placement in placements) placement.Validate();
        ArgGuard.ThrowIf(placements.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != placements.Count, "Object placements need unique IDs.", nameof(placements));
        return placements;
    }

    private void CreateObjects()
    {
        var environment = RequireEnvironment();
        foreach (var placement in _objectPlacements)
            _objects.Add(new(environment.Ids.Allocate(), placement, environment.Physics, environment.WorldLayer, environment.WorldLayer | environment.PlayerLayer | environment.EnemyLayer));
        UpdateObjectStreaming();
    }

    private void UpdateObjectStreaming()
    {
        foreach (var item in _objects)
            item.SetSimulationEnabled(RequireEnvironment().Streamer.IsChunkActive(TileMap.WorldToChunk(item.WorldObject.Transform.Position)));
    }

    public void UpdateStreaming(Vector2 focus)
    {
        ArgGuard.Finite(focus);
        var environment = RequireEnvironment();
        var previousVersion = environment.Streamer.Version;
        environment.Streamer.Update(focus);
        if (environment.Streamer.Version != previousVersion)
        {
            ForgetUnloadedGrass(environment.Streamer);
        }

        EnemySystem.UpdateStreaming(environment.Streamer.IsChunkActive);
        UpdateObjectStreaming();
    }

    /// <summary>
    /// Rebuilds every chunk marked dirty since the last call. Cheap when nothing changed.
    /// </summary>
    public void FlushDirtyChunks()
    {
        if (_dirtyChunks.IsEmpty)
        {
            return;
        }

        var environment = RequireEnvironment();
        _dirtyChunks.Flush(environment.Streamer.Invalidate);
    }

    public void CreateAuthoredWorldThings(
        CombatSystem2D combat, AuthoredCatalog? authored = null)
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
            .Create(_worldThingSpecs, combat, authored);
        UpdateObjectStreaming();
        _definitionRevision++;
        _combatants = combat.Combatants;
        _authoredWorldThingsCreated = true;
    }

    private LevelEnvironment RequireEnvironment() =>
        StateGuard.RequireNotNull(_environment, "Create the level simulation before using it.");

    private void CreateSavePoints()
    {
        foreach (var spec in _worldThingSpecs)
        {
            if (spec.Enabled && spec.Kind == WorldThingKind2D.SavePoint)
            {
                _savePoints.Add(new SavePoint2D(spec, _traversal.PlayerColliderSize.Y / 2f + _traversal.GroundProbeDistance));
            }
        }
    }

    private long _definitionRevision;

    private void OnMapChanged(TileChunk2D chunk)
    {
        _definitionRevision++;
        _dirtyChunks.Mark(chunk);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (TileMap is IChangingTileMap2D editable)
        {
            editable.ChunkChanged -= OnMapChanged;
        }

        RemoveGiantTreeBranches();
        foreach (var item in _objects) item.Dispose();
        _objects.Clear();
        _environment?.Streamer.Dispose();
        foreach (var enemy in EnemySystem.Combatants)
        {
            _environment?.Physics.RemoveBody(enemy.Body);
            _combatants?.Unregister(enemy.Id);
        }
    }

    private float TileCenterX(int x) => TileMap.Origin.X + (x + 0.5f) * _tileSize;

    private sealed record LevelEnvironment(
        CollisionSystem2D Collision,
        PhysicsWorld2D Physics,
        EntityIdAllocator2D Ids,
        SideScrollerChunkStreamer2D Streamer,
        uint WorldLayer,
        uint PlayerLayer,
        uint EnemyLayer);
}
