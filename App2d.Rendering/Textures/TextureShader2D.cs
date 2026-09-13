using App2d.Core;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Textures;

public sealed class TextureShader2D(Texture2D texture, Vector2 tileSize,
    TextureAddressMode tileModeX = TextureAddressMode.Wrap,
    TextureAddressMode tileModeY = TextureAddressMode.Wrap,
    TextureFilter filterMode = TextureFilter.Linear) : IShader2D
{
    public Texture2D Texture { get; } = ArgGuard.RequireNotNull(texture);
    public Vector2 TileSize { get; } = ArgGuard.RequireFinitePositive(tileSize);
    public TextureAddressMode TileModeX { get; } = tileModeX;
    public TextureAddressMode TileModeY { get; } = tileModeY;
    public TextureFilter FilterMode { get; } = filterMode;
    public XnaColor BaseColor => XnaColor.White;
}
