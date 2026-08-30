using App2d.Core;
using App2d.Tiles;

namespace App2d.Levels;

/// <summary>
/// Run-length codec for one chunk's tiles, stored row-major as
/// <c>[kind:u8][count:u8]</c> pairs. Runs longer than 255 split across pairs.
/// </summary>
public static class TileRunCodec2D
{
    private const int MaximumRunLength = byte.MaxValue;

    public static byte[] Encode(ReadOnlySpan<TileKind2D> tiles)
    {
        ArgGuard.ThrowIfNotPositive(tiles.Length);

        var encoded = new List<byte>(16);
        var index = 0;
        while (index < tiles.Length)
        {
            var kind = tiles[index];
            var runLength = 1;
            while (index + runLength < tiles.Length &&
                   tiles[index + runLength] == kind &&
                   runLength < MaximumRunLength)
            {
                runLength++;
            }

            encoded.Add((byte)kind);
            encoded.Add((byte)runLength);
            index += runLength;
        }

        return [.. encoded];
    }

    public static void Decode(ReadOnlySpan<byte> encoded, Span<TileKind2D> tiles)
    {
        if (encoded.Length % 2 != 0)
            ArgGuard.ThrowInvalid("Encoded tile runs must be whole [kind][count] pairs.", nameof(encoded));

        var written = 0;
        for (var pair = 0; pair < encoded.Length; pair += 2)
        {
            var kind = (TileKind2D)encoded[pair];
            int count = encoded[pair + 1];
            if (count == 0)
                ArgGuard.ThrowInvalid("Encoded tile runs must have a positive count.", nameof(encoded));
            if (written + count > tiles.Length)
                ArgGuard.ThrowOutOfRange(count, "Encoded tile runs overflow the destination.");

            tiles.Slice(written, count).Fill(kind);
            written += count;
        }

        if (written != tiles.Length)
            ArgGuard.ThrowOutOfRange(written, "Encoded tile runs do not fill the destination.");
    }
}
