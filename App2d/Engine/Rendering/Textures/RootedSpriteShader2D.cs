using System.Numerics;
using App2d.Engine.Geometry;
using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Places a cropped composite around a stable local-space root. Root-relative
/// origins use image coordinates (X right, Y down); local/world Y points up.
/// </summary>
public sealed class RootedSpriteShader2D : IShader2D
{
    private readonly SKSamplingOptions _sampling;
    private SparseAnimationFrame2D _frame;

    public RootedSpriteShader2D(
        SparseAnimationFrame2D frame,
        float pixelsPerUnit = 1f,
        SKFilterMode filterMode = SKFilterMode.Linear)
    {
        ArgGuard.ThrowIfNull(frame);
        ArgGuard.ThrowIfNotPositive(pixelsPerUnit);
        _frame = frame;
        PixelsPerUnit = pixelsPerUnit;
        _sampling = new SKSamplingOptions(filterMode, SKMipmapMode.None);
    }

    public SparseAnimationFrame2D Frame
    {
        get => _frame;
        set
        {
            ArgGuard.ThrowIfNull(value);
            _frame = value;
        }
    }

    public float PixelsPerUnit { get; }
    public SKColor BaseColor => SKColors.White;

    /// <summary>
    /// The exact local bounds occupied by the current frame when the character
    /// root is local position zero.
    /// </summary>
    public Bounds2D LocalFrameBounds
    {
        get
        {
            var inverseScale = 1f / PixelsPerUnit;
            return new Bounds2D(
                new Vector2(
                    Frame.Origin.X * inverseScale,
                    -(Frame.Origin.Y + Frame.Height) * inverseScale),
                new Vector2(
                    (Frame.Origin.X + Frame.Width) * inverseScale,
                    -Frame.Origin.Y * inverseScale));
        }
    }

    public SKShader CreateShader(in ShaderContext context)
    {
        StateGuard.ThrowIf(
            !context.LocalBounds.IsFinite,
            "Rooted sprites require finite local bounds.");

        var inverseScale = 1f / PixelsPerUnit;
        var textureToLocal = SKMatrix.CreateScaleTranslation(
            inverseScale,
            -inverseScale,
            Frame.Origin.X * inverseScale,
            -Frame.Origin.Y * inverseScale);
        return Frame.Texture.Bitmap.ToShader(
            SKShaderTileMode.Decal,
            SKShaderTileMode.Decal,
            _sampling,
            textureToLocal);
    }
}
