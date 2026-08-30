using SkiaSharp;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace App2d.Engine.Rendering.Textures;

public sealed class Texture2D : IDisposable
{
    private SKBitmap? _bitmap;
    private Dictionary<ImageShaderKey, SKShader>? _imageShaders;

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

    internal ReadOnlySpan<SKColor> PixelSpan =>
        MemoryMarshal.Cast<byte, SKColor>(Bitmap.GetPixelSpan());

    internal Span<SKColor> WritablePixelSpan =>
        MemoryMarshal.Cast<byte, SKColor>(Bitmap.GetPixelSpan());

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SKColor GetPixelUnchecked(int index) => PixelSpan[index];

    internal SKShader GetImageShader(
        SKShaderTileMode tileModeX,
        SKShaderTileMode tileModeY,
        SKFilterMode filterMode,
        float scaleX,
        float scaleY,
        float translateX = 0f,
        float translateY = 0f)
    {
        var key = new ImageShaderKey(
            tileModeX,
            tileModeY,
            filterMode,
            scaleX,
            scaleY,
            translateX,
            translateY);
        if (_imageShaders is not null &&
            _imageShaders.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var shader = Bitmap.ToShader(
            tileModeX,
            tileModeY,
            new SKSamplingOptions(filterMode, SKMipmapMode.None),
            SKMatrix.CreateScaleTranslation(
                scaleX,
                scaleY,
                translateX,
                translateY));
        (_imageShaders ??= []).Add(key, shader);
        return shader;
    }

    public SKColor[] CopyPixels() => [.. PixelSpan];

    public static Texture2D Load(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Texture file was not found.", fullPath);

        using var codec = SKCodec.Create(fullPath);
        if (codec is null || codec.Info.Width <= 0 || codec.Info.Height <= 0)
        {
            throw new InvalidDataException($"Texture could not be decoded: {fullPath}");
        }

        var bitmap = new SKBitmap(new SKImageInfo(
            codec.Info.Width,
            codec.Info.Height,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul));
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result is not SKCodecResult.Success)
        {
            bitmap.Dispose();
            throw new InvalidDataException(
                $"Texture could not be decoded ({result}): {fullPath}");
        }

        return new Texture2D(fullPath, bitmap);
    }

    internal static Texture2D CreateGenerated(
        string sourceName,
        int width,
        int height,
        SKColor[] pixels)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(sourceName);
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNull(pixels);
        if (pixels.Length != width * height)
        {
            ArgGuard.ThrowInvalid(
                "Pixel count must match the texture dimensions.",
                nameof(pixels));
        }

        var bitmap = new SKBitmap(new SKImageInfo(
            width,
            height,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul));
        try
        {
            pixels.AsSpan().CopyTo(
                MemoryMarshal.Cast<byte, SKColor>(bitmap.GetPixelSpan()));
            return new Texture2D(sourceName, bitmap);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    internal static Texture2D CreateUninitializedGenerated(
        string sourceName,
        int width,
        int height)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(sourceName);
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        return new Texture2D(
            sourceName,
            new SKBitmap(new SKImageInfo(
                width,
                height,
                SKColorType.Bgra8888,
                SKAlphaType.Unpremul)));
    }

    public void Dispose()
    {
        if (_imageShaders is not null)
        {
            foreach (var shader in _imageShaders.Values)
                shader.Dispose();
            _imageShaders.Clear();
            _imageShaders = null;
        }
        _bitmap?.Dispose();
        _bitmap = null;
        GC.SuppressFinalize(this);
    }

    private readonly record struct ImageShaderKey(
        SKShaderTileMode TileModeX,
        SKShaderTileMode TileModeY,
        SKFilterMode FilterMode,
        float ScaleX,
        float ScaleY,
        float TranslateX,
        float TranslateY);
}
