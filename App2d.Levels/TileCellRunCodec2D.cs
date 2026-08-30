using App2d.Core;
using App2d.Tiles;

namespace App2d.Levels;

/// <summary>
/// Run-length encoding for packed tile cells. The payload stays [cell:u8][count:u8],
/// so kind-only level blobs from before tilesets were authored decode unchanged.
/// </summary>
internal static class TileCellRunCodec2D
{
    public static byte[] Encode(ReadOnlySpan<TileCell2D> tiles)
    {
        ArgGuard.ThrowIfNotPositive(tiles.Length);
        var encoded = new List<byte>(16);
        var index = 0;
        while (index < tiles.Length)
        {
            var tile = tiles[index];
            var count = 1;
            while (index + count < tiles.Length && tiles[index + count] == tile && count < byte.MaxValue)
                count++;
            encoded.Add(tile.Packed);
            encoded.Add((byte)count);
            index += count;
        }
        return [.. encoded];
    }

    public static void Decode(ReadOnlySpan<byte> encoded, Span<TileCell2D> tiles)
    {
        if (encoded.Length % 2 != 0)
            ArgGuard.ThrowInvalid("Encoded tile runs must be whole [cell][count] pairs.", nameof(encoded));

        var written = 0;
        for (var pair = 0; pair < encoded.Length; pair += 2)
        {
            var count = encoded[pair + 1];
            if (count == 0)
                ArgGuard.ThrowInvalid("Encoded tile runs must have a positive count.", nameof(encoded));
            if (written + count > tiles.Length)
                ArgGuard.ThrowOutOfRange(count, "Encoded tile runs overflow the destination.");
            tiles.Slice(written, count).Fill(new TileCell2D(encoded[pair]));
            written += count;
        }

        if (written != tiles.Length)
            ArgGuard.ThrowOutOfRange(written, "Encoded tile runs do not fill the destination.");
    }
}
