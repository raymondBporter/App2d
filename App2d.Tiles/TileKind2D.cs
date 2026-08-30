using App2d.Core.Geometry;

namespace App2d.Tiles;

[Flags]
public enum TileKind2D : byte
{
    Empty = 0,
    Solid = 1 << 0,
    OneWay = 1 << 1,
    Grippable = 1 << 2,
    Spikes = 1 << 3
}

public static class TileKind2DExtensions
{
    public static bool IsSolid(this TileKind2D kind) => (kind & TileKind2D.Solid) != 0;

    public static bool IsOneWay(this TileKind2D kind) => (kind & TileKind2D.OneWay) != 0;

    public static bool IsGrippable(this TileKind2D kind) => (kind & TileKind2D.Grippable) != 0;

    public static bool IsSpikes(this TileKind2D kind) =>
        (kind & TileKind2D.Spikes) != 0;

    public static bool IsCollidable(this TileKind2D kind) => kind.IsSolid() || kind.IsOneWay();
}

public readonly record struct TileCollisionRectangle2D(Bounds2D Bounds, TileKind2D Kind);

/// <summary>
/// One compact authored map cell: the low nibble is the tile type and the high
/// nibble is its tileset. Old kind-only bytes are therefore valid tileset-zero cells.
/// </summary>
public readonly record struct TileCell2D
{
    public const int MaximumTilesetCount = 16;
    private const byte KindMask = 0x0f;

    public TileCell2D(TileKind2D kind, byte tilesetIndex)
    {
        if (((byte)kind & ~KindMask) != 0)
            throw new ArgumentOutOfRangeException(nameof(kind), "Tile kind must fit in four bits.");
        if (tilesetIndex >= MaximumTilesetCount)
            throw new ArgumentOutOfRangeException(nameof(tilesetIndex), "Tileset index must fit in four bits.");
        Packed = (byte)((tilesetIndex << 4) | (byte)kind);
    }

    public TileCell2D(byte packed) => Packed = packed;

    public byte Packed { get; }
    public TileKind2D Kind => (TileKind2D)(Packed & KindMask);
    public byte TilesetIndex => (byte)(Packed >> 4);
}
