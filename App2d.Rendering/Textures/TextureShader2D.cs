using System.Numerics;
using App2d.Core;
using SkiaSharp;

namespace App2d.Rendering.Textures;

public sealed class TextureShader2D : IShader2D
{
    private readonly SKFilterMode _filterMode;

    public TextureShader2D(Texture2D texture, Vector2 tileSize, SKShaderTileMode tileModeX = SKShaderTileMode.Repeat, SKShaderTileMode tileModeY = SKShaderTileMode.Repeat, SKFilterMode filterMode = SKFilterMode.Linear)
    {
        ArgGuard.ThrowIfNull(texture);
        ArgGuard.ThrowIfNotPositive(tileSize);

        Texture = texture;
        TileSize = tileSize;
        TileModeX = tileModeX;
        TileModeY = tileModeY;
        _filterMode = filterMode;
    }

    public Texture2D Texture { get; }
    public Vector2 TileSize { get; }
    public SKShaderTileMode TileModeX { get; }
    public SKShaderTileMode TileModeY { get; }
    public SKColor BaseColor => SKColors.White;

    public ShaderLease2D AcquireShader(in ShaderContext context)
    {
        return ShaderLease2D.Borrowed(Texture.GetImageShader(
            TileModeX,
            TileModeY,
            _filterMode,
            TileSize.X / Texture.Width,
            TileSize.Y / Texture.Height));
    }
}
