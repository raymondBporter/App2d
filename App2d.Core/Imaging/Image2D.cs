using App2d.Core.Grids;
using App2d.Core.Validation;

namespace App2d.Core.Imaging;

/// <summary>
/// A mutable, contiguous row-major pixel array. Coordinates start at the top left;
/// X increases rightward and Y downward. Pixel values have no per-pixel object overhead.
/// </summary>
public sealed class Image2D<T> where T : unmanaged
{
    private readonly GridSize2D _size;
    private readonly T[] _pixels;

    public Image2D(int width, int height)
    {
        _size = new(width, height);
        _pixels = new T[_size.CellCount];
    }

    /// <summary>Copies pixels in row-major order; later edits do not affect the supplied storage.</summary>
    public Image2D(int width, int height, ReadOnlySpan<T> pixels) : this(width, height)
    {
        ArgGuard.ThrowIf(pixels.Length != Count, "Pixel count must equal width times height.", nameof(pixels));
        pixels.CopyTo(_pixels);
    }

    public int Width => _size.Width;
    public int Height => _size.Height;
    public int Count => _pixels.Length;

    public ref T this[int x, int y] => ref _pixels[_size.GetIndex(x, y)];

    public Span<T> AsSpan() => _pixels;

    public Span<T> GetRowSpan(int y)
    {
        ArgGuard.Index(y, Height);
        return _pixels.AsSpan(y * Width, Width);
    }

    public bool Contains(int x, int y) => _size.Contains(x, y);

    /// <summary>Samples a neighbor, using the supplied value outside the image instead of wrapping or clamping.</summary>
    public T GetValueOrDefault(int x, int y, T outside = default) =>
        Contains(x, y) ? _pixels[y * Width + x] : outside;

    public void Fill(T value) => _pixels.AsSpan().Fill(value);
    public void Clear() => _pixels.AsSpan().Clear();
    public Image2D<T> Clone() => new(Width, Height, _pixels);
}
