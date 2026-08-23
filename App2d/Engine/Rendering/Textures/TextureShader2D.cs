using System.Numerics;
using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

public sealed class TextureShader2D : IShader2D
{
    private readonly SKSamplingOptions _sampling;

    public TextureShader2D(Texture2D texture, Vector2 tileSize, SKShaderTileMode tileModeX = SKShaderTileMode.Repeat, SKShaderTileMode tileModeY = SKShaderTileMode.Repeat, SKFilterMode filterMode = SKFilterMode.Linear)
    {
        ArgGuard.ThrowIfNull(texture);
        ArgGuard.ThrowIfNotPositive(tileSize);

        Texture = texture;
        TileSize = tileSize;
        TileModeX = tileModeX;
        TileModeY = tileModeY;
        _sampling = new SKSamplingOptions(filterMode, SKMipmapMode.None);
    }

    public Texture2D Texture { get; }
    public Vector2 TileSize { get; }
    public SKShaderTileMode TileModeX { get; }
    public SKShaderTileMode TileModeY { get; }
    public SKColor BaseColor => SKColors.White;

    public SKShader CreateShader(in ShaderContext context)
    {
        var textureToLocal = SKMatrix.CreateScale(
            TileSize.X / Texture.Width,
            TileSize.Y / Texture.Height);
        return Texture.Bitmap.ToShader(
            TileModeX,
            TileModeY,
            _sampling,
            textureToLocal);
    }
}
