using App2d.Core.Validation;

namespace App2d.Core.Imaging;

/// <summary>Pixel-wise processing; each operation uses the caller's value and channel semantics.</summary>
public static class ImageOperations2D
{
    /// <summary>Maps each pixel into a new image of the same dimensions, possibly with a different pixel type.</summary>
    public static Image2D<TResult> Map<T, TResult>(this Image2D<T> source, Func<T, TResult> map)
        where T : unmanaged where TResult : unmanaged
    {
        ArgGuard.ThrowIfNull(source);
        ArgGuard.ThrowIfNull(map);
        var result = new Image2D<TResult>(source.Width, source.Height);
        var input = source.AsSpan();
        var output = result.AsSpan();
        for (var i = 0; i < input.Length; i++)
        {
            output[i] = map(input[i]);
        }
        return result;
    }

    /// <summary>Replaces each pixel in place. Use for arithmetic, clamping, powers, or other value transforms.</summary>
    public static void Transform<T>(this Image2D<T> image, Func<T, T> transform) where T : unmanaged
    {
        ArgGuard.ThrowIfNull(image);
        ArgGuard.ThrowIfNull(transform);
        var pixels = image.AsSpan();
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = transform(pixels[i]);
        }
    }
}
