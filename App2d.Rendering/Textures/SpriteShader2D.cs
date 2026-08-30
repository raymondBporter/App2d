using App2d.Core;
using SkiaSharp;

namespace App2d.Rendering.Textures;

/// <summary>
/// Draws one complete texture across an object's local bounds without tiling it.
/// Replacing <see cref="Texture"/> makes it suitable for frame animation.
/// </summary>
public sealed class SpriteShader2D : IShader2D
{
    private readonly SKFilterMode _filterMode;
    public SpriteShader2D(Texture2D texture, SKFilterMode filterMode = SKFilterMode.Linear)
    {
        Texture = texture;
        _filterMode = filterMode;
    }

    public Texture2D Texture
    {
        get;
        set => field = ArgGuard.RequireNotNull(value);
    }

    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public SKColor BaseColor => SKColors.White;

    public ShaderLease2D AcquireShader(in ShaderContext context)
    {
        StateGuard.ThrowIf(
            !context.LocalBounds.IsFinite,
            "Sprites require finite local bounds.");

        var bounds = context.LocalBounds;
        var scaleX = bounds.Size.X / Texture.Width;
        var scaleY = bounds.Size.Y / Texture.Height;
        return ShaderLease2D.Borrowed(Texture.GetImageShader(
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            _filterMode,
            FlipX ? -scaleX : scaleX,
            FlipY ? scaleY : -scaleY,
            FlipX ? bounds.Max.X : bounds.Min.X,
            FlipY ? bounds.Min.Y : bounds.Max.Y));
    }
}
