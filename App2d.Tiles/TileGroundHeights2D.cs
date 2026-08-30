using App2d.Core;

namespace App2d.Tiles;

/// <summary>
/// Per-column ground rows derived from tile data, replacing the world generator's
/// <c>TerrainHeight</c>.
/// </summary>
/// <remarks>
/// Deliberately crude. Ground height feeds camera floor clamping, spawn and goal rows, and
/// enemy placement — getting it wrong degrades feel, not correctness. A single row per column
/// cannot describe a second-storey floor, so this is replaced by authored, multi-valued data
/// rather than refined in place. The clamp to 1 is the entire mitigation for pit columns:
/// callers use <c>height - 1</c>, which must never index below the world.
/// </remarks>
public static class TileGroundHeights2D
{
    public static int[] Derive(ISolidTileMap2D map)
    {
        ArgGuard.ThrowIfNull(map);

        var heights = new int[map.Width];
        for (var x = 0; x < map.Width; x++)
        {
            var y = 0;
            while (y < map.Height && map.IsSolid(x, y))
                y++;

            heights[x] = Math.Max(1, y);
        }

        return heights;
    }
}
