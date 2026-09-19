using App2d.Core;
using App2d.Core.Geometry;
using App2d.Physics;
using System.Collections.Immutable;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Gameplay.World;

internal sealed partial class SideScrollerChunkStreamer2D(PhysicsWorld2D physics, IChunkedTileMap2D tileMap, uint worldLayer, uint actorMask) : IDisposable
{
    private const int HorizontalChunkRadius = 2;
    private const int VerticalChunkRadius = 1;
    private const float OneWaySurfaceThickness = 8f;

    private readonly PhysicsWorld2D _physics = ArgGuard.RequireNotNull(physics);
    private readonly IChunkedTileMap2D _tileMap = ArgGuard.RequireNotNull(tileMap);
    private readonly Dictionary<TileChunk2D, LoadedChunk> _loadedChunks = [];
    private readonly List<SpatialObject2D> _platforms = [];
    private readonly List<TileChunk2D> _unloadBuffer = [];

    private long _revision;
    private ImmutableArray<TerrainChunkState2D> _states;
    /// <summary>Changes whenever the active chunk set or any chunk's data changes.</summary>
    public long Version { get; private set; }
    public ImmutableArray<TerrainChunkState2D> CaptureState()
    {
        if (_states.IsDefault) _states = _loadedChunks.Values.OrderBy(c => c.State.Chunk.Y).ThenBy(c => c.State.Chunk.X).Select(c => c.State).ToImmutableArray();
        return _states;
    }
    public void Dispose()
    {
        foreach (var chunk in _loadedChunks.Keys.ToArray()) Unload(chunk);
    }

    public IReadOnlyList<SpatialObject2D> Platforms => _platforms;
    public int ActiveChunkCount => _loadedChunks.Count;
    public int LoadedColliderCount => _platforms.Count;
    public static int MaximumActiveChunkCount => (HorizontalChunkRadius * 2 + 1) * (VerticalChunkRadius * 2 + 1);

    public bool IsChunkActive(TileChunk2D chunk) => _loadedChunks.ContainsKey(chunk);

    /// <summary>
    /// Rebuilds a chunk whose tiles changed. Does nothing when the chunk is not loaded -
    /// loading it later reads the current map anyway.
    /// </summary>
    public void Invalidate(TileChunk2D chunk)
    {
        if (!_loadedChunks.ContainsKey(chunk))
            return;

        Unload(chunk);
        Load(chunk);
    }

    public void Update(Vector2 focus)
    {
        ArgGuard.ThrowIfNotFinite(focus);
        var center = _tileMap.WorldToChunk(focus);
        var minimumX = Math.Max(0, center.X - HorizontalChunkRadius);
        var maximumX = Math.Min(_tileMap.ChunkColumns - 1, center.X + HorizontalChunkRadius);
        var minimumY = Math.Max(0, center.Y - VerticalChunkRadius);
        var maximumY = Math.Min(_tileMap.ChunkRows - 1, center.Y + VerticalChunkRadius);

        _unloadBuffer.Clear();
        foreach (var chunk in _loadedChunks.Keys.OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            if (chunk.X < minimumX || chunk.X > maximumX ||
                chunk.Y < minimumY || chunk.Y > maximumY)
            {
                _unloadBuffer.Add(chunk);
            }
        }

        foreach (var chunk in _unloadBuffer)
            Unload(chunk);

        for (var y = minimumY; y <= maximumY; y++)
        {
            for (var x = minimumX; x <= maximumX; x++)
            {
                var chunk = new TileChunk2D(x, y);
                if (!_loadedChunks.ContainsKey(chunk))
                    Load(chunk);
            }
        }
    }

    private void Load(TileChunk2D chunk)
    {
        Load(TerrainChunkState2D.Capture(_tileMap, chunk, ++_revision), []);
    }

    private void Load(TerrainChunkState2D state, ImmutableArray<int> restoredIds)
    {
        var colliders = new List<ChunkCollider>();
        foreach (var collision in state.Collisions)
        {
            var isOneWay = collision.Kind.IsOneWay();
            var bounds = isOneWay
                ? new Bounds2D(new Vector2(collision.Bounds.Min.X, collision.Bounds.Max.Y - OneWaySurfaceThickness), collision.Bounds.Max)
                : collision.Bounds;
            var platform = new SpatialObject2D(AxisAlignedRectangle2D.FromSize(bounds.Size));
            platform.Transform.Position = bounds.Center;
            _platforms.Add(platform);

            var body = _physics.AddBody(platform, BodyMotionType2D.Static,
                restoredIds.IsEmpty ? null : restoredIds[colliders.Count]);
            body.Restitution = 0f;
            body.CollisionLayer = worldLayer;
            body.CollisionMask = actorMask;
            body.IsOneWayPlatform = isOneWay;
            body.IsWallGrippable = collision.Kind.IsGrippable();
            colliders.Add(new ChunkCollider(platform, body));
        }

        _loadedChunks.Add(state.Chunk, new LoadedChunk(colliders, state));
        _states = default;
        Version++;
    }

    private void Unload(TileChunk2D chunk)
    {
        var loaded = _loadedChunks[chunk];
        foreach (var collider in loaded.Colliders)
        {
            _physics.RemoveBody(collider.Body);
            _platforms.Remove(collider.Platform);
        }
        _loadedChunks.Remove(chunk);
        _states = default;
        Version++;
    }

    private sealed record LoadedChunk(
        List<ChunkCollider> Colliders,
        TerrainChunkState2D State);

    private readonly record struct ChunkCollider(
        SpatialObject2D Platform,
        PhysicsBody2D Body);
}
