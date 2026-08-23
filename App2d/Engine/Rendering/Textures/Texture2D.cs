using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

public sealed class Texture2D : IDisposable
{
    private SKBitmap? _bitmap;

    private Texture2D(string sourcePath, SKBitmap bitmap)
    {
        SourcePath = sourcePath;
        _bitmap = bitmap;
    }

    public string SourcePath { get; }
    public int Width => Bitmap.Width;
    public int Height => Bitmap.Height;
    public bool IsDisposed => _bitmap is null;

    internal SKBitmap Bitmap =>
        _bitmap ?? throw new ObjectDisposedException(nameof(Texture2D));

    public static Texture2D Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Texture file was not found.", fullPath);

        var bitmap = SKBitmap.Decode(fullPath);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            bitmap?.Dispose();
            throw new InvalidDataException($"Texture could not be decoded: {fullPath}");
        }

        return new Texture2D(fullPath, bitmap);
    }

    public void Dispose()
    {
        _bitmap?.Dispose();
        _bitmap = null;
        GC.SuppressFinalize(this);
    }
}
