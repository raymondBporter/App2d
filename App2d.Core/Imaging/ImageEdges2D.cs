using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Imaging;

/// <summary>Scalar Canny edges: Gaussian smoothing, ridge suppression, and connected weak/strong thresholding.</summary>
public static class ImageEdges2D
{
    /// <summary>
    /// Returns a new scalar image smoothed by a separable Gaussian truncated at three sigma.
    /// Sigma is in pixels, in [0, 20]; zero copies the source. Samples clamp at the image border.
    /// </summary>
    public static Image2D<float> GaussianBlur(this Image2D<float> source, float sigma)
    {
        ArgGuard.ThrowIfNull(source);
        ArgGuard.InRange(sigma, 0, 20);
        if (sigma == 0 || source.Count == 0)
        {
            return source.Clone();
        }
        var radius = (int)MathF.Ceiling(3 * sigma);
        var kernel = new float[2 * radius + 1];
        var total = 0f;
        for (var offset = -radius; offset <= radius; offset++)
        {
            var weight = MathF.Exp(-.5f * MathF.Pow(offset / sigma, 2));
            kernel[offset + radius] = weight;
            total += weight;
        }
        for (var i = 0; i < kernel.Length; i++)
        {
            kernel[i] /= total;
        }
        var horizontal = new Image2D<float>(source.Width, source.Height);
        var result = new Image2D<float>(source.Width, source.Height);
        var width = source.Width;
        var lanes = Vector<float>.Count;
        var vectorWeights = new Vector<float>[kernel.Length];
        for (var i = 0; i < kernel.Length; i++)
        {
            vectorWeights[i] = new(kernel[i]);
        }
        for (var y = 0; y < source.Height; y++)
        {
            var row = source.GetRowSpan(y);
            var output = horizontal.GetRowSpan(y);
            var x = 0;
            for (; x < Math.Min(radius, width); x++)
            {
                output[x] = BlurBorder(row, kernel, radius, x);
            }
            if (Vector.IsHardwareAccelerated)
            {
                // Each lane is a different pixel. Adjacent taps are contiguous float loads, not integer gathers.
                for (; x <= width - radius - lanes; x += lanes)
                {
                    var sum = Vector<float>.Zero;
                    for (var tap = 0; tap < kernel.Length; tap++)
                    {
                        sum += new Vector<float>(row.Slice(x + tap - radius, lanes)) * vectorWeights[tap];
                    }
                    sum.CopyTo(output.Slice(x, lanes));
                }
            }
            for (; x < width; x++)
            {
                output[x] = BlurBorder(row, kernel, radius, x);
            }
        }
        var intermediate = horizontal.AsSpan();
        var rowOffsets = new int[kernel.Length];
        for (var y = 0; y < source.Height; y++)
        {
            for (var tap = 0; tap < kernel.Length; tap++)
            {
                rowOffsets[tap] = Math.Clamp(y + tap - radius, 0, source.Height - 1) * width;
            }
            var output = result.GetRowSpan(y);
            var x = 0;
            if (Vector.IsHardwareAccelerated)
            {
                for (; x <= width - lanes; x += lanes)
                {
                    var sum = Vector<float>.Zero;
                    for (var tap = 0; tap < kernel.Length; tap++)
                    {
                        sum += new Vector<float>(intermediate.Slice(rowOffsets[tap] + x, lanes)) * vectorWeights[tap];
                    }
                    sum.CopyTo(output.Slice(x, lanes));
                }
            }
            for (; x < width; x++)
            {
                var value = 0f;
                for (var tap = 0; tap < kernel.Length; tap++)
                {
                    value += kernel[tap] * intermediate[rowOffsets[tap] + x];
                }
                output[x] = value;
            }
        }
        return result;
    }

    private static float BlurBorder(ReadOnlySpan<float> row, ReadOnlySpan<float> kernel, int radius, int x)
    {
        var sum = 0f;
        for (var tap = 0; tap < kernel.Length; tap++)
        {
            sum += kernel[tap] * row[Math.Clamp(x + tap - radius, 0, row.Length - 1)];
        }
        return sum;
    }

    /// <summary>
    /// Keeps positive strengths at least highThreshold and every eight-connected continuation
    /// at least lowThreshold. Isolated weak components are rejected. Thresholds are finite and
    /// nonnegative, with high >= low; input strengths must be finite and nonnegative.
    /// </summary>
    public static Image2D<bool> HysteresisThreshold(this Image2D<float> strengths, float lowThreshold, float highThreshold)
    {
        ArgGuard.ThrowIfNull(strengths);
        ArgGuard.NonNegative(lowThreshold);
        ArgGuard.AtLeast(highThreshold, lowThreshold);
        var result = new Image2D<bool>(strengths.Width, strengths.Height);
        var input = strengths.AsSpan();
        var output = result.AsSpan();
        var pending = new Queue<int>();
        var width = strengths.Width;
        var height = strengths.Height;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            if (!float.IsFinite(value) || value < 0)
            {
                ArgGuard.NonNegative(value, nameof(strengths));
            }
            if (value > 0 && value >= highThreshold)
            {
                output[i] = true;
                pending.Enqueue(i);
            }
        }
        while (pending.TryDequeue(out var index))
        {
            var x = index % width;
            var y = index / width;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var nx = x + dx;
                    var ny = y + dy;
                    if ((uint)nx >= (uint)width || (uint)ny >= (uint)height)
                    {
                        continue;
                    }
                    var neighbor = ny * width + nx;
                    if (!output[neighbor] && input[neighbor] > 0 && input[neighbor] >= lowThreshold)
                    {
                        output[neighbor] = true;
                        pending.Enqueue(neighbor);
                    }
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Finds thin raster edges in a finite scalar image without changing it. Thresholds use the
    /// existing Sobel /8 gradient units, not a fraction of the image's maximum gradient.
    /// Bilinear sampling along the gradient suppresses non-maxima; eight-connected hysteresis
    /// follows weak continuations of strong edges. This does not repair gaps or segment objects.
    /// </summary>
    public static Image2D<bool> CannyEdges(this Image2D<float> source,
        float lowThreshold = .01f, float highThreshold = .03f, float sigma = 1.2f)
    {
        ArgGuard.ThrowIfNull(source);
        ArgGuard.NonNegative(lowThreshold);
        ArgGuard.AtLeast(highThreshold, lowThreshold);
        return source.CannyEdgeStrengths(sigma).HysteresisThreshold(lowThreshold, highThreshold);
    }

    /// <summary>
    /// Prepares the threshold-independent part of Canny: Gaussian smoothing, Sobel magnitudes,
    /// and ridge suppression. Reuse the returned nonnegative strengths with HysteresisThreshold
    /// when only the cutoffs change. Rebuild when the source pixels or sigma change.
    /// </summary>
    public static Image2D<float> CannyEdgeStrengths(this Image2D<float> source, float sigma = 1.2f)
    {
        ArgGuard.ThrowIfNull(source);
        foreach (var value in source.AsSpan())
        {
            ArgGuard.Finite(value, nameof(source));
        }
        var gradients = source.GaussianBlur(sigma).Sobel();
        var magnitudes = gradients.Map(static gradient => gradient.Length());
        var ridges = new Image2D<float>(source.Width, source.Height);
        var width = source.Width;
        var height = source.Height;
        var magnitudePixels = magnitudes.AsSpan();
        var gradientPixels = gradients.AsSpan();
        var ridgePixels = ridges.AsSpan();
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var index = y * width + x;
                var strength = magnitudePixels[index];
                if (strength == 0)
                {
                    continue;
                }
                var direction = gradientPixels[index] / strength;
                var forward = Sample(magnitudePixels, width, height, new(x + direction.X, y + direction.Y));
                var backward = Sample(magnitudePixels, width, height, new(x - direction.X, y - direction.Y));
                // An asymmetric tie break keeps a plateau from becoming a two-pixel ridge.
                if (strength >= forward && strength > backward)
                {
                    ridgePixels[index] = strength;
                }
            }
        }
        return ridges;
    }

    private static float Sample(ReadOnlySpan<float> pixels, int width, int height, Vector2 point)
    {
        var x = Math.Clamp(point.X, 0, width - 1);
        var y = Math.Clamp(point.Y, 0, height - 1);
        var left = (int)x;
        var top = (int)y;
        var right = Math.Min(left + 1, width - 1);
        var bottom = Math.Min(top + 1, height - 1);
        var tx = x - left;
        var ty = y - top;
        var a = pixels[top * width + left] * (1 - tx) + pixels[top * width + right] * tx;
        var b = pixels[bottom * width + left] * (1 - tx) + pixels[bottom * width + right] * tx;
        return a * (1 - ty) + b * ty;
    }
}
