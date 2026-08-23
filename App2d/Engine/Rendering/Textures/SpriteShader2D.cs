using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Draws one complete texture across an object's local bounds without tiling it.
/// Replacing <see cref="Texture"/> makes it suitable for frame animation.
/// </summary>
public sealed class SpriteShader2D : IShader2D
{
    private readonly SKSamplingOptions _sampling;
    private Texture2D _texture;

    public SpriteShader2D(Texture2D texture, SKFilterMode filterMode = SKFilterMode.Linear)
    {
        ArgumentNullException.ThrowIfNull(texture);
        _texture = texture;
        _sampling = new SKSamplingOptions(filterMode, SKMipmapMode.None);
    }

    public Texture2D Texture
    {
        get => _texture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _texture = value;
        }
    }

    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public SKColor BaseColor => SKColors.White;

    public SKShader CreateShader(in ShaderContext context)
    {
        if (!context.LocalBounds.IsFinite)
            throw new InvalidOperationException("Sprites require finite local bounds.");

        var bounds = context.LocalBounds;
        var scaleX = bounds.Size.X / Texture.Width;
        var scaleY = bounds.Size.Y / Texture.Height;
        var textureToLocal = SKMatrix.CreateScaleTranslation(
            FlipX ? -scaleX : scaleX,
            FlipY ? scaleY : -scaleY,
            FlipX ? bounds.Max.X : bounds.Min.X,
            FlipY ? bounds.Min.Y : bounds.Max.Y);

        return Texture.Bitmap.ToShader(
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            _sampling,
            textureToLocal);
    }
}
