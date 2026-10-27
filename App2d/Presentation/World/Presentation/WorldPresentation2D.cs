using App2d.Core.Rendering.Characters;
using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Core.Vegetation;
using App2d.Rendering;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Presentation.World.Presentation;

/// <summary>
/// Draws level content and per-tick world observations without reading simulation objects or
/// the editable map. Content-derived visuals rebuild when their content changes;
/// camera-selected terrain can be supplied independently of simulation streaming.
/// </summary>
public sealed class WorldPresentation2D(Scene2D scene, TextureCache2D textures) : IDisposable
{
    private readonly Dictionary<EntityId2D, (ObjectDefinition2D Definition, WorldObject2D Visual)> _objects = [];
    private readonly Dictionary<long, CheckpointView> _checkpoints = [];
    private readonly Dictionary<TileChunk2D, ChunkView> _chunks = [];
    private readonly Dictionary<(string Id, float Size), SideScrollerTerrainTileset2D> _tilesets = [];
    private readonly List<WorldObject2D> _goal = [];
    private Vector2? _goalPosition;
    private LevelContent2D _content = LevelContent2D.Empty;
    private WorldState2D _state = WorldState2D.Empty;
    private long? _appliedContentRevision;
    private ImmutableArray<TerrainChunkState2D>? _visibleTerrain;
    private readonly VegetationPresentation2D _vegetation = new();

    public void DrawTrees(Renderer2D renderer, Rect2D visible) => _vegetation.DrawTrees(renderer, visible);
    public void DrawGrass(Renderer2D renderer, Rect2D visible, VegetationLayer2D layer) =>
        _vegetation.DrawGrass(renderer, visible, layer);

    /// <summary>Use camera-selected terrain instead of the simulation's active terrain set.</summary>
    public void SetVisibleTerrain(ImmutableArray<TerrainChunkState2D> terrain)
    {
        ArgGuard.ThrowIf(terrain.IsDefault, "Terrain must be initialized.", nameof(terrain));
        if (_visibleTerrain == terrain)
        {
            return;
        }
        // Separate sources can assign the same revision to different chunk observations.
        if (_visibleTerrain is null)
        {
            foreach (var chunk in _chunks.Keys.ToArray())
            {
                RemoveChunk(chunk);
            }
        }

        _visibleTerrain = terrain;
        ApplyTerrain(terrain);
    }

    public void ApplyState(LevelContent2D content, WorldState2D state) => Update(content, state, 0f);
    public void Advance(float deltaSeconds) => Update(_content, _state, deltaSeconds);

    public void Update(LevelContent2D content, WorldState2D state, float dt)
    {
        ArgGuard.NonNegative(dt);
        ArgGuard.ThrowIfNull(content);
        ArgGuard.ThrowIfNull(state);
        _state = state;
        _vegetation.Advance(dt);
        if (!ReferenceEquals(content, _content) || _appliedContentRevision != content.Revision)
        {
            _content = content;
            _appliedContentRevision = content.Revision;
            ApplyContent(content);
        }
        _vegetation.ApplyCuts(state.CutGrass);
        foreach (var item in state.Objects)
        {
            if (!_objects.TryGetValue(item.Id, out var view)) continue;
            view.Visual.Transform.Position = item.Position;
            view.Visual.Transform.Rotation = item.Rotation;
            view.Visual.IsVisible = item.IsEnabled;
            ((PartShader2D)view.Visual.Shader).SetTime(item.Time);
        }

        foreach (var checkpoint in _checkpoints)
        {
            var isActive = false;
            foreach (var observed in state.Checkpoints)
            {
                if (observed.ThingId == checkpoint.Key)
                {
                    isActive = observed.IsActive;
                    break;
                }
            }

            checkpoint.Value.Visual.Update(dt, isActive);
        }
    }

    private void ApplyContent(LevelContent2D content)
    {
        var objectIds = content.Objects.Select(o => o.Id).ToHashSet();
        foreach (var id in _objects.Keys.Where(id => !objectIds.Contains(id)).ToArray())
        {
            scene.Remove(_objects[id].Visual);
            _objects.Remove(id);
        }
        foreach (var item in content.Objects)
        {
            if (_objects.TryGetValue(item.Id, out var existing))
            {
                if (existing.Definition == item) continue;
                scene.Remove(existing.Visual);
                _objects.Remove(item.Id);
            }
            if (item.Part.Geometry.Count == 0) continue;
            var shader = new PartShader2D(item.Part) { TextureRoot = item.TextureRoot };
            var visual = new WorldObject2D(shader.BoundsShape(), shader) { ZIndex = 1 };
            scene.Add(visual);
            _objects.Add(item.Id, (item, visual));
        }
        _vegetation.SetGiantTrees(content.GiantTrees);
        var checkpoints = content.Checkpoints.Select(c => c.ThingId).ToHashSet();
        foreach (var id in _checkpoints.Keys.Where(id => !checkpoints.Contains(id)).ToArray())
        {
            _checkpoints[id].Visual.Dispose();
            _checkpoints.Remove(id);
        }
        foreach (var checkpoint in content.Checkpoints)
        {
            if (_checkpoints.TryGetValue(checkpoint.ThingId, out var view) && view.BasePosition == checkpoint.BasePosition)
            {
                continue;
            }

            view?.Visual.Dispose();
            _checkpoints[checkpoint.ThingId] = new CheckpointView(
                new SavePointPresentation2D(scene, checkpoint), checkpoint.BasePosition);
        }

        ApplyTerrain(_visibleTerrain ?? content.Terrain);

        if (_goalPosition != content.GoalPosition)
        {
            foreach (var visual in _goal)
            {
                scene.Remove(visual);
            }

            _goal.Clear();
            _goalPosition = content.GoalPosition;
            if (content.GoalPosition is { } position)
            {
                CreateGoal(position);
            }
        }
    }

    private void ApplyTerrain(ImmutableArray<TerrainChunkState2D> terrain)
    {
        _vegetation.SetTerrain(terrain);
        var chunks = terrain.Select(c => c.Chunk).ToHashSet();
        foreach (var chunk in _chunks.Keys.Where(c => !chunks.Contains(c)).ToArray())
        {
            RemoveChunk(chunk);
        }

        foreach (var chunk in terrain)
        {
            if (_chunks.TryGetValue(chunk.Chunk, out var loaded) && loaded.Revision == chunk.Revision)
            {
                continue;
            }

            RemoveChunk(chunk.Chunk);
            var catalog = chunk.TilesetIds.Select(id => GetTileset(id, chunk.TileSize)).ToArray();
            var resolver = new SideScrollerTerrainTilesetResolver2D((x, y) => catalog[chunk.GetTilesetIndex(x, y)]);
            var factory = new SideScrollerTerrainVisualFactory2D(scene, chunk, resolver);
            var visuals = new List<WorldObject2D>();
            foreach (var collision in chunk.Collisions)
            {
                if (collision.Kind.IsSolid())
                {
                    visuals.AddRange(factory.CreateSolidFill(collision.Bounds));
                }
            }

            visuals.AddRange(factory.CreateSurfaceVisuals(chunk.Chunk));
            _chunks.Add(chunk.Chunk, new ChunkView(chunk.Revision, visuals));
        }
    }

    private SideScrollerTerrainTileset2D GetTileset(string id, float size)
    {
        if (!_tilesets.TryGetValue((id, size), out var tileset))
        {
            tileset = SideScrollerTerrainTileset2D.Load(textures, id, size);
            _tilesets.Add((id, size), tileset);
        }
        return tileset;
    }

    private void RemoveChunk(TileChunk2D chunk)
    {
        if (!_chunks.Remove(chunk, out var view))
        {
            return;
        }

        foreach (var visual in view.Visuals)
        {
            scene.Remove(visual);
        }
    }

    private void CreateGoal(Vector2 position)
    {
        var pole = new WorldObject2D(Capsule2D.Builder.Create(Vector2.Zero, new Vector2(0f, 190f), 5f),
            new SolidColorShader(new Color4b(238, 242, 232)));
        pole.Transform.Position = position;
        var flag = new WorldObject2D(ConvexPolygon2D.Builder.Create([
            Vector2.Zero, new Vector2(92f, -30f), new Vector2(0f, -60f)]),
            new SolidColorShader(new Color4b(255, 79, 120)));
        flag.Transform.Position = position + new Vector2(0f, 185f);
        _goal.Add(pole);
        _goal.Add(flag);
        scene.Add(pole);
        scene.Add(flag);
    }

    public void Dispose()
    {
        foreach (var (Definition, Visual) in _objects.Values) scene.Remove(Visual);
        _objects.Clear();
        foreach (var checkpoint in _checkpoints.Values)
        {
            checkpoint.Visual.Dispose();
        }

        foreach (var chunk in _chunks.Keys.ToArray())
        {
            RemoveChunk(chunk);
        }

        foreach (var visual in _goal)
        {
            scene.Remove(visual);
        }

        _checkpoints.Clear();
        _goal.Clear();
        _goalPosition = null;
        _appliedContentRevision = null;
        _visibleTerrain = null;
    }

    private sealed record CheckpointView(SavePointPresentation2D Visual, Vector2 BasePosition);
    private sealed record ChunkView(long Revision, List<WorldObject2D> Visuals);
}
