using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Imaging;

public static class PaletteQuantization2D
{
    /// <summary>
    /// Reduces finite three-channel pixels to at most colorCount colors using k-means.
    /// Returns palette colors in the input channel space and an image of zero-based palette indices.
    /// Initialization is deterministic farthest-point seeding, followed by at most maxIterations
    /// Lloyd updates over all pixels. Unused centers are removed. An empty image has an empty palette.
    /// </summary>
    /// <remarks>
    /// Distance is Euclidean in the supplied channel space; convert sRGB to Oklab first for a
    /// perceptual distance. Alpha/masking, denoising, spatial region cleanup, and dithering are
    /// caller choices. The source is unchanged. Work is O(pixels * colors * iterations).
    /// </remarks>
    public static (Vector3[] Palette, Image2D<int> Indices) QuantizePalette(this Image2D<Vector3> source, int colorCount, int maxIterations = 32)
    {
        ArgGuard.ThrowIfNull(source);
        ArgGuard.Positive(colorCount);
        ArgGuard.Positive(maxIterations);
        var pixels = source.AsSpan();
        foreach (var pixel in pixels)
        {
            ArgGuard.Finite(pixel, nameof(source));
        }

        var indices = new Image2D<int>(source.Width, source.Height);
        if (pixels.IsEmpty)
        {
            return ([], indices);
        }

        var palette = Seed(pixels, Math.Min(colorCount, pixels.Length));
        var labels = indices.AsSpan();
        labels.Fill(-1);
        var sums = new (double X, double Y, double Z)[palette.Length];
        var counts = new int[palette.Length];
        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            Array.Clear(sums);
            Array.Clear(counts);
            var changed = false;
            for (var i = 0; i < pixels.Length; i++)
            {
                var index = Nearest(pixels[i], palette);
                changed |= labels[i] != index;
                labels[i] = index;
                sums[index].X += pixels[i].X;
                sums[index].Y += pixels[i].Y;
                sums[index].Z += pixels[i].Z;
                counts[index]++;
            }

            for (var i = 0; i < palette.Length; i++)
            {
                if (counts[i] > 0)
                {
                    palette[i] = new((float)(sums[i].X / counts[i]),
                        (float)(sums[i].Y / counts[i]), (float)(sums[i].Z / counts[i]));
                }
            }
            if (!changed)
            {
                break;
            }
        }

        // The last update moved the centers: assign against the returned colors, even at the iteration limit.
        Array.Clear(counts);
        for (var i = 0; i < pixels.Length; i++)
        {
            labels[i] = Nearest(pixels[i], palette);
            counts[labels[i]]++;
        }
        return (Compact(palette, counts, labels), indices);
    }

    private static Vector3[] Seed(ReadOnlySpan<Vector3> pixels, int count)
    {
        var palette = new List<Vector3>(count) { pixels[0] };
        var distances = new double[pixels.Length];
        Array.Fill(distances, double.PositiveInfinity);
        while (palette.Count < count)
        {
            var farthest = 0;
            for (var i = 0; i < pixels.Length; i++)
            {
                distances[i] = Math.Min(distances[i], DistanceSquared(pixels[i], palette[^1]));
                if (distances[i] > distances[farthest])
                {
                    farthest = i;
                }
            }
            if (distances[farthest] == 0)
            {
                break; // Fewer distinct colors than requested.
            }
            palette.Add(pixels[farthest]);
        }
        return palette.ToArray();
    }

    private static int Nearest(Vector3 pixel, ReadOnlySpan<Vector3> palette)
    {
        var nearest = 0;
        var distance = DistanceSquared(pixel, palette[0]);
        for (var i = 1; i < palette.Length; i++)
        {
            var candidate = DistanceSquared(pixel, palette[i]);
            if (candidate < distance)
            {
                distance = candidate;
                nearest = i;
            }
        }
        return nearest;
    }

    private static double DistanceSquared(Vector3 a, Vector3 b)
    {
        // Double intermediates keep distances and means finite even for large finite float channels.
        var x = (double)a.X - b.X;
        var y = (double)a.Y - b.Y;
        var z = (double)a.Z - b.Z;
        return x * x + y * y + z * z;
    }

    private static Vector3[] Compact(Vector3[] palette, ReadOnlySpan<int> counts, Span<int> labels)
    {
        var remap = new int[palette.Length];
        var used = 0;
        for (var i = 0; i < palette.Length; i++)
        {
            if (counts[i] > 0)
            {
                remap[i] = used;
                palette[used++] = palette[i];
            }
        }
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = remap[labels[i]];
        }
        return palette.AsSpan(0, used).ToArray();
    }
}
