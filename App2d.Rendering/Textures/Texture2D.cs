using App2d.Core;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Textures;

/// <summary>Decoded straight-alpha RGBA pixels, uploaded lazily by the renderer.</summary>
public sealed class Texture2D : IDisposable
{
    private XnaColor[]? _pixels;
    private readonly int _width;
    private readonly int _height;

    private Texture2D(string sourcePath, int width, int height, XnaColor[] pixels)
    {
        SourcePath = sourcePath;
        _width = width;
        _height = height;
        _pixels = pixels;
    }

    public string SourcePath { get; }
    public int Width { get { ThrowIfDisposed(); return _width; } }
    public int Height { get { ThrowIfDisposed(); return _height; } }
    public bool IsDisposed => _pixels is null;
    internal XnaColor[] Pixels => _pixels ?? throw new ObjectDisposedException(nameof(Texture2D));
    internal event Action<Texture2D>? Disposed;
    public XnaColor[] CopyPixels() => [.. Pixels];

    public static Texture2D Load(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Texture file was not found.", fullPath);
        try
        {
            using var source = new Bitmap(fullPath);
            return FromBitmap(fullPath, source);
        }
        catch (Exception error) when (error is ArgumentException or ExternalException)
        {
            throw new InvalidDataException($"Texture could not be decoded: {fullPath}", error);
        }
    }

    internal static Texture2D FromBitmap(string sourceName, Bitmap source)
    {
        using var bitmap = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var pixels = new XnaColor[checked(bitmap.Width * bitmap.Height)];
            var row = new byte[checked(bitmap.Width * 4)];
            for (var y = 0; y < bitmap.Height; y++)
            {
                Marshal.Copy(data.Scan0 + y * data.Stride, row, 0, row.Length);
                for (var x = 0; x < bitmap.Width; x++)
                    pixels[y * bitmap.Width + x] = new XnaColor(row[x * 4 + 2], row[x * 4 + 1], row[x * 4], row[x * 4 + 3]);
            }
            return new Texture2D(sourceName, bitmap.Width, bitmap.Height, pixels);
        }
        finally { bitmap.UnlockBits(data); }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    public void Dispose()
    {
        if (IsDisposed) return;
        _pixels = null;
        Disposed?.Invoke(this);
        Disposed = null;
    }
}
