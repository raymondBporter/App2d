using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Imaging;

/// <summary>Spatial derivatives in image coordinates: X rightward, Y downward.</summary>
public static class ImageGradients2D
{
    /// <summary>
    /// Returns (dI/dx, dI/dy) using the 3-by-3 Sobel kernels divided by eight.
    /// A unit linear ramp has unit derivative in the interior. Border samples are clamped
    /// to the nearest pixel; a constant image has zero gradient, including its border.
    /// </summary>
    public static Image2D<Vector2> Sobel(this Image2D<float> source)
    {
        ArgGuard.ThrowIfNull(source);
        var result = new Image2D<Vector2>(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            var top = source.GetRowSpan(Math.Max(y - 1, 0));
            var middle = source.GetRowSpan(y);
            var bottom = source.GetRowSpan(Math.Min(y + 1, source.Height - 1));
            var output = result.GetRowSpan(y);
            for (var x = 0; x < source.Width; x++)
            {
                var left = Math.Max(x - 1, 0);
                var right = Math.Min(x + 1, source.Width - 1);
                var dx = top[right] - top[left]
                    + 2 * (middle[right] - middle[left])
                    + bottom[right] - bottom[left];
                var dy = bottom[left] - top[left]
                    + 2 * (bottom[x] - top[x])
                    + bottom[right] - top[right];
                output[x] = new Vector2(dx, dy) / 8;
            }
        }
        return result;
    }

    /// <summary>
    /// Returns sqrt(|dC/dx|^2 + |dC/dy|^2), combining all three channel derivatives.
    /// Uses the same normalized Sobel kernels and clamped borders as the scalar overload.
    /// The caller chooses the channel space, for example RGB or Oklab; no grayscale
    /// conversion, smoothing, thresholding, or edge thinning is implicit.
    /// </summary>
    public static Image2D<float> SobelMagnitude(this Image2D<Vector3> source)
    {
        ArgGuard.ThrowIfNull(source);
        var result = new Image2D<float>(source.Width, source.Height);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var left = Math.Max(x - 1, 0);
                var right = Math.Min(x + 1, source.Width - 1);
                var top = Math.Max(y - 1, 0);
                var bottom = Math.Min(y + 1, source.Height - 1);
                var dx = (source[right, top] - source[left, top]
                    + 2 * (source[right, y] - source[left, y])
                    + source[right, bottom] - source[left, bottom]) / 8;
                var dy = (source[left, bottom] - source[left, top]
                    + 2 * (source[x, bottom] - source[x, top])
                    + source[right, bottom] - source[right, top]) / 8;
                result[x, y] = MathF.Sqrt(dx.LengthSquared() + dy.LengthSquared());
            }
        }
        return result;
    }
}
