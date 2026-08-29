using App2d.Engine.Geometry;

namespace App2d.Engine.Tiles;

public enum TileKind2D : byte
{
    Empty,
    Solid,
    OneWay
}

public readonly record struct TileCollisionRectangle2D(
    Bounds2D Bounds,
    TileKind2D Kind);
