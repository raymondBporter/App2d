using System.Numerics;
using App2d.Core.Geometry;

namespace App2d.Tiles;

public interface ISolidTileMap2D
{
    int Width { get; }
    int Height { get; }
    float TileSize { get; }
    Vector2 Origin { get; }

    bool IsSolid(int x, int y);
}

/// <summary>
/// The synchronous data view consumed by chunk streaming. Implementations may
/// generate chunks, read them from storage, or front either source with a cache.
/// </summary>
public interface IChunkedTileMap2D : ISolidTileMap2D
{
    int ChunkSize { get; }
    int ChunkColumns { get; }
    int ChunkRows { get; }
    Bounds2D WorldBounds { get; }

    TileKind2D GetTileKind(int x, int y);
    TileChunk2D WorldToChunk(Vector2 worldPosition);
    IReadOnlyList<TileCollisionRectangle2D> BuildCollisionRectangles(TileChunk2D chunk);
}
