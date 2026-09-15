using App2d.Core;
using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
using App2d.Tiles;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Gameplay.World.Presentation;

/// <summary>Draws complete world observations without reading simulation objects or the editable map.</summary>
public sealed class WorldPresentation2D(Scene2D scene, TextureCache2D textures) : IDisposable
{
    private readonly Dictionary<EntityId2D, PlatformView> _platforms = [];
    private readonly Dictionary<long, CheckpointView> _checkpoints = [];
    private readonly Dictionary<TileChunk2D, ChunkView> _chunks = [];
    private readonly Dictionary<(string Id, float Size), SideScrollerTerrainTileset2D> _tilesets = [];
    private readonly List<WorldObject2D> _goal = [];
    private Vector2? _goalPosition;
    private WorldState2D _state = WorldState2D.Empty;

    public void ApplyState(WorldState2D state) => Update(state, 0f);
    public void Advance(float deltaSeconds) => Update(_state, deltaSeconds);

    public void Update(WorldState2D state, float dt)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(dt);
        _state = state;
        var platforms = state.MovingPlatforms.Select(p => p.Id).ToHashSet();
        foreach (var id in _platforms.Keys.Where(id => !platforms.Contains(id)).ToArray())
        {
            scene.Remove(_platforms[id].Visual);
            _platforms.Remove(id);
        }
        foreach (var platform in state.MovingPlatforms)
        {
            if (!_platforms.TryGetValue(platform.Id, out var view) ||
                view.Size != platform.Size || view.ColorArgb != platform.ColorArgb)
            {
                if (view is not null) scene.Remove(view.Visual);
                var color = platform.ColorArgb;
                var visual = new WorldObject2D(AxisAlignedRectangle2D.FromSize(platform.Size),
                    new SolidColorShader(new XnaColor((byte)(color >> 16), (byte)(color >> 8), (byte)color, (byte)(color >> 24))));
                scene.Add(visual);
                view = new PlatformView(visual, platform.Size, color);
                _platforms[platform.Id] = view;
            }
            view.Visual.Transform.Position = platform.Position;
        }

        var checkpoints = state.Checkpoints.Select(c => c.ThingId).ToHashSet();
        foreach (var id in _checkpoints.Keys.Where(id => !checkpoints.Contains(id)).ToArray())
        {
            _checkpoints[id].Visual.Dispose();
            _checkpoints.Remove(id);
        }
        foreach (var checkpoint in state.Checkpoints)
        {
            if (!_checkpoints.TryGetValue(checkpoint.ThingId, out var view) || view.BasePosition != checkpoint.BasePosition)
            {
                view?.Visual.Dispose();
                view = new CheckpointView(new SavePointPresentation2D(scene, checkpoint), checkpoint.BasePosition);
                _checkpoints[checkpoint.ThingId] = view;
            }
            view.Visual.Update(dt, checkpoint.IsActive);
        }

        var chunks = state.Terrain.Select(c => c.Chunk).ToHashSet();
        foreach (var chunk in _chunks.Keys.Where(c => !chunks.Contains(c)).ToArray()) RemoveChunk(chunk);
        foreach (var chunk in state.Terrain)
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

        if (_goalPosition != state.GoalPosition)
        {
            foreach (var visual in _goal) scene.Remove(visual);
            _goal.Clear();
            _goalPosition = state.GoalPosition;
            if (state.GoalPosition is { } position) CreateGoal(position);
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
    }

    private sealed record PlatformView(WorldObject2D Visual, Vector2 Size, uint ColorArgb);
    private sealed record CheckpointView(SavePointPresentation2D Visual, Vector2 BasePosition);
    private sealed record ChunkView(long Revision, List<WorldObject2D> Visuals);
}
