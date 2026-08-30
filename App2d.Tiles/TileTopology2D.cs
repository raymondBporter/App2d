using App2d.Core;

namespace App2d.Tiles;

[Flags]
public enum TileSurface2D : byte
{
    None = 0,
    Top = 1 << 0,
    Right = 1 << 1,
    Bottom = 1 << 2,
    Left = 1 << 3
}

[Flags]
public enum TileCorner2D : byte
{
    None = 0,
    OuterTopRight = 1 << 0,
    OuterBottomRight = 1 << 1,
    OuterBottomLeft = 1 << 2,
    OuterTopLeft = 1 << 3,
    InnerTopRight = 1 << 4,
    InnerBottomRight = 1 << 5,
    InnerBottomLeft = 1 << 6,
    InnerTopLeft = 1 << 7
}

public static class TileTopology2D
{
    public static TileSurface2D GetExposedSurfaces(
        this ISolidTileMap2D tileMap,
        int x,
        int y)
    {
        ArgGuard.ThrowIfNull(tileMap);

        if (!tileMap.IsSolid(x, y))
            return TileSurface2D.None;

        var surfaces = TileSurface2D.None;
        if (!tileMap.IsSolid(x, y + 1))
            surfaces |= TileSurface2D.Top;
        if (!tileMap.IsSolid(x + 1, y))
            surfaces |= TileSurface2D.Right;
        if (!tileMap.IsSolid(x, y - 1))
            surfaces |= TileSurface2D.Bottom;
        if (!tileMap.IsSolid(x - 1, y))
            surfaces |= TileSurface2D.Left;
        return surfaces;
    }

    public static TileCorner2D GetCorners(this ISolidTileMap2D tileMap, int x, int y)
    {
        ArgGuard.ThrowIfNull(tileMap);

        if (!tileMap.IsSolid(x, y))
            return TileCorner2D.None;

        var top = tileMap.IsSolid(x, y + 1);
        var right = tileMap.IsSolid(x + 1, y);
        var bottom = tileMap.IsSolid(x, y - 1);
        var left = tileMap.IsSolid(x - 1, y);
        var corners = TileCorner2D.None;

        if (!top && !right)
            corners |= TileCorner2D.OuterTopRight;
        if (!bottom && !right)
            corners |= TileCorner2D.OuterBottomRight;
        if (!bottom && !left)
            corners |= TileCorner2D.OuterBottomLeft;
        if (!top && !left)
            corners |= TileCorner2D.OuterTopLeft;

        if (top && right && !tileMap.IsSolid(x + 1, y + 1))
            corners |= TileCorner2D.InnerTopRight;
        if (bottom && right && !tileMap.IsSolid(x + 1, y - 1))
            corners |= TileCorner2D.InnerBottomRight;
        if (bottom && left && !tileMap.IsSolid(x - 1, y - 1))
            corners |= TileCorner2D.InnerBottomLeft;
        if (top && left && !tileMap.IsSolid(x - 1, y + 1))
            corners |= TileCorner2D.InnerTopLeft;

        return corners;
    }
}
