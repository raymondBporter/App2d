using System.Buffers.Binary;
using System.IO.Compression;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// A lossless unsigned 16-bit depth atlas. PNG decoding is deliberately kept
/// separate from color texture decoding so depth precision is never reduced to 8 bits.
/// </summary>
public sealed class DepthAtlas2D : IDisposable
{
    private static ReadOnlySpan<byte> PngSignature =>
        [137, 80, 78, 71, 13, 10, 26, 10];

    private ushort[]? _pixels;

    private DepthAtlas2D(string sourcePath, int width, int height, ushort[] pixels)
    {
        SourcePath = sourcePath;
        Width = width;
        Height = height;
        _pixels = pixels;
    }

    public string SourcePath { get; }
    public int Width { get; }
    public int Height { get; }
    public bool IsDisposed => _pixels is null;

    internal ReadOnlySpan<ushort> Pixels =>
        _pixels ?? throw new ObjectDisposedException(nameof(DepthAtlas2D));

    public ushort GetDepth(int x, int y)
    {
        if ((uint)x >= (uint)Width)
            throw new ArgumentOutOfRangeException(nameof(x));
        if ((uint)y >= (uint)Height)
            throw new ArgumentOutOfRangeException(nameof(y));
        return Pixels[y * Width + x];
    }

    public static DepthAtlas2D LoadR16Unorm(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Depth atlas was not found.", fullPath);

        using var stream = File.OpenRead(fullPath);
        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(PngSignature))
            throw Unsupported(fullPath, "Only PNG depth atlases are supported");

        var width = 0;
        var height = 0;
        var sawHeader = false;
        var sawEnd = false;
        using var compressed = new MemoryStream();
        Span<byte> chunkHeader = stackalloc byte[8];
        Span<byte> crc = stackalloc byte[4];
        while (!sawEnd && stream.Position < stream.Length)
        {
            stream.ReadExactly(chunkHeader);
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(chunkHeader[..4]));
            var type = chunkHeader[4..8];
            var data = new byte[length];
            stream.ReadExactly(data);
            stream.ReadExactly(crc);

            if (type.SequenceEqual("IHDR"u8))
            {
                if (length != 13)
                    throw new InvalidDataException($"Invalid PNG header: {fullPath}");
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4)));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4)));
                if (width <= 0 || height <= 0)
                    throw new InvalidDataException($"Invalid depth atlas dimensions: {fullPath}");
                if (data[8] != 16 || data[9] != 0 || data[10] != 0 ||
                    data[11] != 0 || data[12] != 0)
                {
                    throw Unsupported(
                        fullPath,
                        "Depth PNG must be non-interlaced 16-bit grayscale (r16-unorm)");
                }
                sawHeader = true;
            }
            else if (type.SequenceEqual("IDAT"u8))
            {
                compressed.Write(data);
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                sawEnd = true;
            }
        }

        if (!sawHeader || !sawEnd || compressed.Length == 0)
            throw new InvalidDataException($"Incomplete depth PNG: {fullPath}");

        var rowBytes = checked(width * sizeof(ushort));
        var decoded = new byte[checked((rowBytes + 1) * height)];
        compressed.Position = 0;
        using (var inflater = new ZLibStream(compressed, CompressionMode.Decompress))
        {
            inflater.ReadExactly(decoded);
            if (inflater.ReadByte() != -1)
                throw new InvalidDataException($"Depth PNG contains excess image data: {fullPath}");
        }

        var pixels = new ushort[checked(width * height)];
        var previous = new byte[rowBytes];
        var current = new byte[rowBytes];
        for (var y = 0; y < height; y++)
        {
            var sourceOffset = y * (rowBytes + 1);
            var filter = decoded[sourceOffset];
            decoded.AsSpan(sourceOffset + 1, rowBytes).CopyTo(current);
            Unfilter(current, previous, filter, sizeof(ushort), fullPath);
            for (var x = 0; x < width; x++)
            {
                pixels[y * width + x] =
                    BinaryPrimitives.ReadUInt16BigEndian(current.AsSpan(x * 2, 2));
            }
            (previous, current) = (current, previous);
        }

        return new DepthAtlas2D(fullPath, width, height, pixels);
    }

    public void Dispose()
    {
        _pixels = null;
        GC.SuppressFinalize(this);
    }

    private static void Unfilter(
        Span<byte> row,
        ReadOnlySpan<byte> previous,
        byte filter,
        int bytesPerPixel,
        string path)
    {
        for (var index = 0; index < row.Length; index++)
        {
            var left = index >= bytesPerPixel ? row[index - bytesPerPixel] : 0;
            var above = previous[index];
            var upperLeft = index >= bytesPerPixel ? previous[index - bytesPerPixel] : 0;
            row[index] = filter switch
            {
                0 => row[index],
                1 => unchecked((byte)(row[index] + left)),
                2 => unchecked((byte)(row[index] + above)),
                3 => unchecked((byte)(row[index] + ((left + above) >> 1))),
                4 => unchecked((byte)(row[index] + Paeth(left, above, upperLeft))),
                _ => throw new InvalidDataException(
                    $"Unsupported PNG row filter {filter}: {path}")
            };
        }
    }

    private static byte Paeth(int left, int above, int upperLeft)
    {
        var estimate = left + above - upperLeft;
        var leftDistance = Math.Abs(estimate - left);
        var aboveDistance = Math.Abs(estimate - above);
        var upperLeftDistance = Math.Abs(estimate - upperLeft);
        return (byte)(leftDistance <= aboveDistance && leftDistance <= upperLeftDistance
            ? left
            : aboveDistance <= upperLeftDistance
                ? above
                : upperLeft);
    }

    private static NotSupportedException Unsupported(string path, string reason) =>
        new($"{reason}: {path}");
}
