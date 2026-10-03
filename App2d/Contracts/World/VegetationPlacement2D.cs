using App2d.Core.Geometry;
using App2d.Core.Tiles;
using System.Numerics;

namespace App2d.Contracts.World;

/// <summary>Stable grass identity, independent of streamed chunk instances.</summary>
public readonly record struct GrassCell2D(int X, int Y);

/// <summary>Shared placement and interaction footprint; rendering never decides what was cut.</summary>
public static class VegetationPlacement2D
{
    public const float MaximumHeightInTiles = 1.1f;
    /// <summary>Height of the remaining stalk above its terrain surface.</summary>
    public const float CutHeightInTiles = 0.12f;

    public static bool HasGrass(IChunkedTileMap2D map, int x, int y) =>
        x >= 0 && x < map.Width && y >= 0 && y < map.Height - 1 &&
        map.GetTileKind(x, y) is var kind && kind.IsSolid() && !kind.IsSpikes() &&
        map.GetTileKind(x, y + 1) == TileKind2D.Empty;

    public static Rect2D GrassBounds(IChunkedTileMap2D map, GrassCell2D cell)
    {
        var root = map.Origin + new Vector2(cell.X, cell.Y + 1) * map.TileSize;
        return new(root + new Vector2(0f, map.TileSize * CutHeightInTiles),
            root + new Vector2(map.TileSize, map.TileSize * MaximumHeightInTiles));
    }

    // Explicit arithmetic keeps layouts stable across runs and chunk load orders.
    public static int Seed(int x, int y)
    {
        unchecked
        {
            var value = (uint)x * 0x9e3779b9u ^ (uint)y * 0x85ebca6bu ^ 107u;
            value = (value ^ (value >> 16)) * 0x7feb352du;
            value = (value ^ (value >> 15)) * 0x846ca68bu;
            return (int)((value ^ (value >> 16)) & 0x7fffffffu);
        }
    }
}
