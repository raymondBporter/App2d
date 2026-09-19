using App2d.Core;
using App2d.Core.Geometry;
using App2d.Tiles;
using System.Collections.Immutable;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Fixed per platform instance; a replaced platform gets a new runtime ID.</summary>
public readonly record struct MovingPlatformDefinition2D(
    EntityId2D Id, long ThingId, Vector2 Size, uint ColorArgb);
public readonly record struct MovingPlatformState2D(EntityId2D Id, Vector2 Position);
public readonly record struct CheckpointPlacement2D(long ThingId, Vector2 BasePosition);
public readonly record struct CheckpointState2D(long ThingId, bool IsActive);

/// <summary>
/// Level content that changes only when authoring or streaming changes: active terrain,
/// platform definitions, checkpoint placements, and the goal. Successive ticks share one
/// instance; <see cref="Revision"/> lets a receiver skip content it already holds.
/// </summary>
public sealed record LevelContent2D(
    long Revision,
    ImmutableArray<TerrainChunkState2D> Terrain,
    ImmutableArray<MovingPlatformDefinition2D> MovingPlatforms,
    ImmutableArray<CheckpointPlacement2D> Checkpoints,
    Vector2? GoalPosition)
{
    public static LevelContent2D Empty { get; } = new(0, [], [], [], null);
}

/// <summary>Per-tick dynamic world observation, not a physics restore point.</summary>
public sealed record WorldState2D(
    ImmutableArray<MovingPlatformState2D> MovingPlatforms,
    ImmutableArray<CheckpointState2D> Checkpoints)
{
    public static WorldState2D Empty { get; } = new([], []);
}

/// <summary>
/// Immutable chunk data with a one-cell halo for edge/corner sampling. Created
/// only when a chunk loads or changes, and shared safely by successive frames.
/// </summary>
public sealed class TerrainChunkState2D : IChunkedTileMap2D
{
    /// <summary>Row-major packed cells, including a one-cell halo on every side.</summary>
    public ImmutableArray<byte> Cells { get; }
    public TileChunk2D Chunk { get; }
    public long Revision { get; }
    public int Width { get; }
    public int Height { get; }
    public int ChunkSize { get; }
    public float TileSize { get; }
    public Vector2 Origin { get; }
    public int ChunkColumns => (Width + ChunkSize - 1) / ChunkSize;
    public int ChunkRows => (Height + ChunkSize - 1) / ChunkSize;
    public Bounds2D WorldBounds => new(Origin, Origin + new Vector2(Width, Height) * TileSize);
    public ImmutableArray<string> TilesetIds { get; }
    IReadOnlyList<string> IChunkedTileMap2D.TilesetIds => TilesetIds;
    public ImmutableArray<TileCollisionRectangle2D> Collisions { get; }

    public TerrainChunkState2D(TileChunk2D chunk, long revision, int width, int height,
        int chunkSize, float tileSize, Vector2 origin, ImmutableArray<string> tilesetIds,
        ImmutableArray<TileCollisionRectangle2D> collisions, ImmutableArray<byte> cells)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNotPositive(chunkSize);
        ArgGuard.ThrowIfNotPositive(tileSize);
        ArgGuard.ThrowIfNotFinite(origin);
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        if (chunk.X < 0 || chunk.Y < 0 || chunk.X > (width - 1) / chunkSize || chunk.Y > (height - 1) / chunkSize)
            throw new ArgumentOutOfRangeException(nameof(chunk));
        var cellCount = checked((chunkSize + 2) * (chunkSize + 2));
        if (cells.IsDefault || cells.Length != cellCount)
            throw new ArgumentException("Cells must include the complete chunk and its one-cell halo.", nameof(cells));
        if (tilesetIds.IsDefaultOrEmpty || tilesetIds.Length > TileCell2D.MaximumTilesetCount ||
            tilesetIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("A valid tileset catalog is required.", nameof(tilesetIds));
        if (cells.Any(cell => new TileCell2D(cell).TilesetIndex >= tilesetIds.Length))
            throw new ArgumentException("A cell refers to a missing tileset.", nameof(cells));
        if (collisions.IsDefault || collisions.Any(c => !c.Bounds.IsFinite || c.Bounds.Size.X <= 0f || c.Bounds.Size.Y <= 0f))
            throw new ArgumentException("Collision rectangles must be initialized and finite.", nameof(collisions));
        Chunk = chunk;
        Revision = revision;
        Width = width;
        Height = height;
        ChunkSize = chunkSize;
        TileSize = tileSize;
        Origin = origin;
        TilesetIds = tilesetIds;
        Collisions = collisions;
        Cells = cells;
    }

    public static TerrainChunkState2D Capture(IChunkedTileMap2D map, TileChunk2D chunk, long revision)
    {
        ArgGuard.ThrowIfNull(map);
        var cells = ImmutableArray.CreateBuilder<byte>(checked((map.ChunkSize + 2) * (map.ChunkSize + 2)));
        for (var y = -1; y <= map.ChunkSize; y++)
            for (var x = -1; x <= map.ChunkSize; x++)
            {
                var tileX = chunk.X * map.ChunkSize + x;
                var tileY = chunk.Y * map.ChunkSize + y;
                cells.Add(new TileCell2D(map.GetTileKind(tileX, tileY), map.GetTilesetIndex(tileX, tileY)).Packed);
            }
        return new(chunk, revision, map.Width, map.Height, map.ChunkSize, map.TileSize, map.Origin,
            map.TilesetIds.ToImmutableArray(), map.BuildCollisionRectangles(chunk).ToImmutableArray(), cells.MoveToImmutable());
    }

    private TileCell2D GetCell(int x, int y)
    {
        var localX = x - Chunk.X * ChunkSize + 1;
        var localY = y - Chunk.Y * ChunkSize + 1;
        return localX >= 0 && localX < ChunkSize + 2 && localY >= 0 && localY < ChunkSize + 2
            ? new TileCell2D(Cells[localY * (ChunkSize + 2) + localX]) : default;
    }
    public TileKind2D GetTileKind(int x, int y) => GetCell(x, y).Kind;
    public byte GetTilesetIndex(int x, int y) => GetCell(x, y).TilesetIndex;
    public bool IsSolid(int x, int y) => GetTileKind(x, y).IsSolid();
    public TileChunk2D WorldToChunk(Vector2 position) => new(
        (int)MathF.Floor((position.X - Origin.X) / (TileSize * ChunkSize)),
        (int)MathF.Floor((position.Y - Origin.Y) / (TileSize * ChunkSize)));
    public IReadOnlyList<TileCollisionRectangle2D> BuildCollisionRectangles(TileChunk2D chunk) =>
        chunk == Chunk ? Collisions : throw new ArgumentException("This snapshot contains one chunk.", nameof(chunk));
}
