using App2d.Contracts.World;
using App2d.Core;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Objects;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Tiles;
using App2d.Core.Validation;
using App2d.Gameplay.World;
using App2d.Rendering;
using App2d.Vegetation;
using System.Collections.Immutable;

namespace App2d.Presentation.World.Presentation;

/// <summary>
/// Draws level content and per-tick world observations. A local terrain source supplies a spatial
/// query and revisioned visuals independently of simulation streaming.
/// </summary>
public sealed class WorldPresentation2D : IDisposable
{
    private readonly Scene2D _scene;
    private readonly TextureCache2D _textures;
    private readonly ViewportTerrainSource2D? _terrainSource;
    private readonly Dictionary<EntityId2D, (ObjectDefinition2D Definition, WorldObject2D Visual)> _objects = [];
    private readonly Dictionary<long, CheckpointView> _checkpoints = [];
    private readonly Dictionary<TileChunk2D, ChunkView> _chunks = [];
    private readonly Dictionary<(string Id, float Size), SideScrollerTerrainTileset2D> _tilesets = [];
    private readonly List<WorldObject2D> _goal = [];
    private Vector2f? _goalPosition;
    private LevelContent2D _content = LevelContent2D.Empty;
    private WorldState2D _state = WorldState2D.Empty;
    private long? _appliedContentRevision;
    private readonly VegetationPresentation2D _vegetation;

    public WorldPresentation2D(Scene2D scene, TextureCache2D textures) : this(scene, textures, null) { }

    internal WorldPresentation2D(Scene2D scene, TextureCache2D textures, ViewportTerrainSource2D? terrainSource)
    {
        _scene = scene;
        _textures = textures;
        _terrainSource = terrainSource;
        _vegetation = new(terrainSource);
        _terrainSource?.ChunkChanged += InvalidateTerrain;
    }

    public void DrawTrees(Renderer2D renderer, Rect2f visible) => _vegetation.DrawTrees(renderer, visible);
    public void DrawGrass(Renderer2D renderer, Rect2f visible, VegetationLayer2D layer) =>
        _vegetation.DrawGrass(renderer, visible, layer);

    public void Draw(Renderer2D renderer)
    {
        var visible = renderer.View.VisibleWorldBounds;
        DrawTrees(renderer, visible);
        renderer.Draw(_scene, int.MinValue, 0);
        if (_terrainSource is { } terrain)
        {
            foreach (var cell in terrain.QueryTerrain(visible))
            {
                var snapshot = terrain.GetChunk(new(cell.X, cell.Y));
                foreach (var visual in GetTerrainVisuals(snapshot))
                    renderer.Draw(visual);
            }
        }
        DrawGrass(renderer, visible, VegetationLayer2D.Back);
        renderer.Draw(_scene, 1, int.MaxValue);
        DrawGrass(renderer, visible, VegetationLayer2D.Front);
    }

    private void InvalidateTerrain(TileChunk2D chunk) => _chunks.Remove(chunk);

    public void ApplyState(LevelContent2D content, WorldState2D state) => Update(content, state, 0f);
    public void Advance(float deltaSeconds) => Update(_content, _state, deltaSeconds);

    public void Update(LevelContent2D content, WorldState2D state, float dt)
    {
        Ensure.NonNegative(dt);
        Ensure.ThrowIfNull(content);
        Ensure.ThrowIfNull(state);
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
            view.Visual.Position = item.Position;
            view.Visual.Rotation = item.Rotation;
            view.Visual.IsVisible = item.IsEnabled;
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
            _scene.Remove(_objects[id].Visual);
            _objects.Remove(id);
        }
        foreach (var item in content.Objects)
        {
            if (_objects.TryGetValue(item.Id, out var existing))
            {
                if (existing.Definition == item) continue;
                _scene.Remove(existing.Visual);
                _objects.Remove(item.Id);
            }
            var visual = new WorldObject2D(item.Asset.Bounds(), new ObjectShader2D(item.Asset)) { ZIndex = 1 };
            _scene.Add(visual);
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
                new SavePointPresentation2D(_scene, checkpoint), checkpoint.BasePosition);
        }

        if (_terrainSource is null)
            ApplyTerrain(content.Terrain);

        if (_goalPosition != content.GoalPosition)
        {
            foreach (var visual in _goal)
            {
                _scene.Remove(visual);
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
            foreach (var visual in GetTerrainVisuals(chunk))
                _scene.Add(visual);
        }
    }

    internal List<WorldObject2D> GetTerrainVisuals(TerrainChunkState2D chunk)
    {
        if (_chunks.TryGetValue(chunk.Chunk, out var loaded) && loaded.Revision == chunk.Revision)
            return loaded.Visuals;

        var catalog = chunk.TilesetIds.Select(id => GetTileset(id, chunk.TileSize)).ToArray();
        var resolver = new SideScrollerTerrainTilesetResolver2D((x, y) => catalog[chunk.GetTilesetIndex(x, y)]);
        var factory = new SideScrollerTerrainVisualFactory2D(chunk, resolver);
        var visuals = new List<WorldObject2D>();
        foreach (var collision in chunk.Collisions)
        {
            if (collision.Kind.IsSolid())
                visuals.AddRange(factory.CreateSolidFill(collision.Bounds));
        }

        visuals.AddRange(factory.CreateSurfaceVisuals(chunk.Chunk));
        _chunks[chunk.Chunk] = new ChunkView(chunk.Revision, visuals);
        return visuals;
    }

    private SideScrollerTerrainTileset2D GetTileset(string id, float size)
    {
        if (!_tilesets.TryGetValue((id, size), out var tileset))
        {
            tileset = SideScrollerTerrainTileset2D.Load(_textures, id, size);
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
            _scene.Remove(visual);
        }
    }

    private void CreateGoal(Vector2f position)
    {
        var pole = new WorldObject2D(Capsule2.Builder.Create(Vector2f.Zero, new Vector2f(0f, 190f), 5f),
            new SolidColorShader(new Color4b(238, 242, 232)))
        {
            Position = position
        };
        var flag = new WorldObject2D(ConvexPolygon2D.Builder.Create([
            Vector2f.Zero, new Vector2f(92f, -30f), new Vector2f(0f, -60f)]),
            new SolidColorShader(new Color4b(255, 79, 120)))
        {
            Position = position + new Vector2f(0f, 185f)
        };
        _goal.Add(pole);
        _goal.Add(flag);
        _scene.Add(pole);
        _scene.Add(flag);
    }

    public void Dispose()
    {
        foreach (var (Definition, Visual) in _objects.Values) _scene.Remove(Visual);
        _objects.Clear();
        foreach (var checkpoint in _checkpoints.Values)
        {
            checkpoint.Visual.Dispose();
        }

        _vegetation.Dispose();
        if (_terrainSource is not null)
        {
            _terrainSource.ChunkChanged -= InvalidateTerrain;
            _chunks.Clear();
        }
        else
        {
            foreach (var chunk in _chunks.Keys.ToArray())
                RemoveChunk(chunk);
        }

        foreach (var visual in _goal)
        {
            _scene.Remove(visual);
        }

        _checkpoints.Clear();
        _goal.Clear();
        _goalPosition = null;
        _appliedContentRevision = null;
    }

    private sealed record CheckpointView(SavePointPresentation2D Visual, Vector2f BasePosition);
    private sealed record ChunkView(long Revision, List<WorldObject2D> Visuals);
}
