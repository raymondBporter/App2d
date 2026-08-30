using App2d.Core.Geometry;

namespace App2d.Tiles;

[Flags]
public enum TileKind2D : byte
{
    Empty = 0,
    Solid = 1 << 0,
    OneWay = 1 << 1,
    Grippable = 1 << 2
}

public static class TileKind2DExtensions
{
    public static bool IsSolid(this TileKind2D kind) => (kind & TileKind2D.Solid) != 0;

    public static bool IsOneWay(this TileKind2D kind) => (kind & TileKind2D.OneWay) != 0;

    public static bool IsGrippable(this TileKind2D kind) => (kind & TileKind2D.Grippable) != 0;

    public static bool IsCollidable(this TileKind2D kind) => kind.IsSolid() || kind.IsOneWay();
}

public readonly record struct TileCollisionRectangle2D(Bounds2D Bounds, TileKind2D Kind);
