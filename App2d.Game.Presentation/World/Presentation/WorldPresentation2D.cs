using App2d.Core;
using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Gameplay.World.Presentation;

/// <summary>
/// Draws level content and per-tick world observations without reading simulation objects or
/// the editable map. Content-derived visuals rebuild when their content changes;
/// camera-selected terrain can be supplied independently of simulation streaming.
/// </summary>
public sealed class WorldPresentation2D(Scene2D scene, TextureCache2D textures) : IDisposable
{
    private readonly Dictionary<EntityId2D, PlatformView> _platforms = [];
    private readonly Dictionary<long, CheckpointView> _checkpoints = [];
    private readonly Dictionary<TileChunk2D, ChunkView> _chunks = [];
    private readonly Dictionary<(string Id, float Size), SideScrollerTerrainTileset2D> _tilesets = [];
    private readonly List<WorldObject2D> _goal = [];
    private Vector2? _goalPosition;
    private LevelContent2D _content = LevelContent2D.Empty;
    private WorldState2D _state = WorldState2D.Empty;
    private long? _appliedContentRevision;
    private ImmutableArray<TerrainChunkState2D>? _visibleTerrain;

    /// <summary>Use camera-selected terrain instead of the simulation's active terrain set.</summary>
    public void SetVisibleTerrain(ImmutableArray<TerrainChunkState2D> terrain)
    {
        if (terrain.IsDefault) throw new ArgumentException("Terrain must be initialized.", nameof(terrain));
        if (_visibleTerrain == terrain) return;
        // Separate sources can assign the same revision to different chunk observations.
        if (_visibleTerrain is null)
            foreach (var chunk in _chunks.Keys.ToArray()) RemoveChunk(chunk);
        _visibleTerrain = terrain;
        ApplyTerrain(terrain);
    }

    public void ApplyState(LevelContent2D content, WorldState2D state) => Update(content, state, 0f);
    public void Advance(float deltaSeconds) => Update(_content, _state, deltaSeconds);

    public void Update(LevelContent2D content, WorldState2D state, float dt)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(dt);
        ArgGuard.ThrowIfNull(content);
        ArgGuard.ThrowIfNull(state);
        _state = state;
        if (!ReferenceEquals(content, _content) || _appliedContentRevision != content.Revision)
        {
            _content = content;
            _appliedContentRevision = content.Revision;
            ApplyContent(content);
        }

        foreach (var platform in state.MovingPlatforms)
            if (_platforms.TryGetValue(platform.Id, out var view))
                view.Visual.Transform.Position = platform.Position;

        foreach (var checkpoint in _checkpoints)
        {
            var isActive = false;
            foreach (var observed in state.Checkpoints)
                if (observed.ThingId == checkpoint.Key) { isActive = observed.IsActive; break; }
            checkpoint.Value.Visual.Update(dt, isActive);
        }
    }

    private void ApplyContent(LevelContent2D content)
    {
        var platforms = content.MovingPlatforms.Select(p => p.Id).ToHashSet();
        foreach (var id in _platforms.Keys.Where(id => !platforms.Contains(id)).ToArray())
        {
            scene.Remove(_platforms[id].Visual);
            _platforms.Remove(id);
        }
        foreach (var platform in content.MovingPlatforms)
        {
            if (_platforms.TryGetValue(platform.Id, out var view) &&
                view.Size == platform.Size && view.ColorArgb == platform.ColorArgb)
                continue;
            if (view is not null) scene.Remove(view.Visual);
            var color = platform.ColorArgb;
            var visual = new WorldObject2D(AxisAlignedRectangle2D.FromSize(platform.Size),
                new SolidColorShader(new XnaColor((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24))));
            scene.Add(visual);
            _platforms[platform.Id] = new PlatformView(visual, platform.Size, color);
        }

        var checkpoints = content.Checkpoints.Select(c => c.ThingId).ToHashSet();
        foreach (var id in _checkpoints.Keys.Where(id => !checkpoints.Contains(id)).ToArray())
        {
            _checkpoints[id].Visual.Dispose();
            _checkpoints.Remove(id);
        }
        foreach (var checkpoint in content.Checkpoints)
        {
            if (_checkpoints.TryGetValue(checkpoint.ThingId, out var view) && view.BasePosition == checkpoint.BasePosition)
                continue;
            view?.Visual.Dispose();
            _checkpoints[checkpoint.ThingId] = new CheckpointView(
                new SavePointPresentation2D(scene, checkpoint), checkpoint.BasePosition);
        }

        ApplyTerrain(_visibleTerrain ?? content.Terrain);

        if (_goalPosition != content.GoalPosition)
        {
            foreach (var visual in _goal) scene.Remove(visual);
            _goal.Clear();
            _goalPosition = content.GoalPosition;
            if (content.GoalPosition is { } position) CreateGoal(position);
        }
    }

    private void ApplyTerrain(ImmutableArray<TerrainChunkState2D> terrain)
    {
        var chunks = terrain.Select(c => c.Chunk).ToHashSet();
        foreach (var chunk in _chunks.Keys.Where(c => !chunks.Contains(c)).ToArray()) RemoveChunk(chunk);
        foreach (var chunk in terrain)
        {
            if (_chunks.TryGetValue(chunk.Chunk, out var loaded) && loaded.Revision == chunk.Revision) continue;
            RemoveChunk(chunk.Chunk);
            var catalog = chunk.TilesetIds.Select(id => GetTileset(id, chunk.TileSize)).ToArray();
            var resolver = new SideScrollerTerrainTilesetResolver2D((x, y) => catalog[chunk.GetTilesetIndex(x, y)]);
            var factory = new SideScrollerTerrainVisualFactory2D(scene, chunk, resolver);
            var visuals = new List<WorldObject2D>();
            foreach (var collision in chunk.Collisions)
                if (collision.Kind.IsSolid() && !collision.Kind.IsGrippable())
                    visuals.AddRange(factory.CreateSolidFill(collision.Bounds));
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
        if (!_chunks.Remove(chunk, out var view)) return;
        foreach (var visual in view.Visuals) scene.Remove(visual);
    }

    private void CreateGoal(Vector2 position)
    {
        var pole = new WorldObject2D(new Capsule2D(Vector2.Zero, new Vector2(0f, 190f), 5f),
            new SolidColorShader(new XnaColor(238, 242, 232)));
        pole.Transform.Position = position;
        var flag = new WorldObject2D(new ConvexPolygon2D([
            Vector2.Zero, new Vector2(92f, -30f), new Vector2(0f, -60f)]),
            new SolidColorShader(new XnaColor(255, 79, 120)));
        flag.Transform.Position = position + new Vector2(0f, 185f);
        _goal.Add(pole);
        _goal.Add(flag);
        scene.Add(pole);
        scene.Add(flag);
    }

    public void Dispose()
    {
        foreach (var platform in _platforms.Values) scene.Remove(platform.Visual);
        foreach (var checkpoint in _checkpoints.Values) checkpoint.Visual.Dispose();
        foreach (var chunk in _chunks.Keys.ToArray()) RemoveChunk(chunk);
        foreach (var visual in _goal) scene.Remove(visual);
        _platforms.Clear();
        _checkpoints.Clear();
        _goal.Clear();
        _goalPosition = null;
        _appliedContentRevision = null;
        _visibleTerrain = null;
    }

    private sealed record PlatformView(WorldObject2D Visual, Vector2 Size, uint ColorArgb);
    private sealed record CheckpointView(SavePointPresentation2D Visual, Vector2 BasePosition);
    private sealed record ChunkView(long Revision, List<WorldObject2D> Visuals);
}
