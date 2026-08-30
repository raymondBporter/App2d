using App2d.Core;
using SkiaSharp;
using System.Numerics;

namespace App2d.Rendering.Textures;

public sealed class TextureShader2D(Texture2D texture, Vector2 tileSize, SKShaderTileMode tileModeX = SKShaderTileMode.Repeat, SKShaderTileMode tileModeY = SKShaderTileMode.Repeat, SKFilterMode filterMode = SKFilterMode.Linear)
    : IShader2D
{
    public Texture2D Texture { get; } = ArgGuard.RequireNotNull(texture);
    public Vector2 TileSize { get; } = ArgGuard.RequireFinitePositive(tileSize);
    public SKShaderTileMode TileModeX { get; } = tileModeX;
    public SKShaderTileMode TileModeY { get; } = tileModeY;
    public float ScaleX => TileSize.X / Texture.Width;
    public float ScaleY => TileSize.Y / Texture.Height;
    public SKColor BaseColor => SKColors.White;
    public ShaderLease2D AcquireShader(in ShaderContext context)
    {
        return ShaderLease2D.Borrowed(Texture.GetImageShader(TileModeX, TileModeY, filterMode, ScaleX, ScaleY));
    }
}
