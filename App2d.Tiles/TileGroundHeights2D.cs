using App2d.Core.Validation;
using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// Ground row queries derived from tile data, replacing the world generator's
/// <c>TerrainHeight</c>.
/// </summary>
/// <remarks>
/// Deliberately crude. A single row per column cannot describe a second-storey floor.
/// Runtime only queries the fallback spawn column; authored spawns are used otherwise.
/// The clamp to 1 ensures pit columns never return a row below the world.
/// </remarks>
public static class TileGroundHeights2D
{
    public static int[] Derive(ISolidTileMap2D map)
    {
        ArgGuard.ThrowIfNull(map);

        var heights = new int[map.Width];
        for (var x = 0; x < map.Width; x++)
            heights[x] = AtColumn(map, x);

        return heights;
    }

    public static int AtColumn(ISolidTileMap2D map, int x)
    {
        ArgGuard.ThrowIfNull(map);
        ArgGuard.ThrowIf(x < 0 || x >= map.Width, "Column must lie within the map.", nameof(x));
        var y = 0;
        while (y < map.Height && map.IsSolid(x, y))
            y++;
        return Math.Max(1, y);
    }
}
