using App2d.Core.Validation;
using App2d.Core;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Textures;

/// <param name="imageOrigin">
/// When set, the local point where the image's top-left corner sits, with the image upright
/// (image rows run down as local Y falls). Without it, tiles repeat from the local origin
/// using the original positive-Y convention.
/// </param>
public sealed class TextureShader2D(Texture2D texture, Vector2 tileSize,
    TextureAddressMode tileModeX = TextureAddressMode.Wrap,
    TextureAddressMode tileModeY = TextureAddressMode.Wrap,
    TextureFilter filterMode = TextureFilter.Linear,
    Vector2? imageOrigin = null) : IShader2D
{
    public Vector2? ImageOrigin { get; } = imageOrigin;
    public Texture2D Texture { get; } = ArgGuard.RequireNotNull(texture);
    public Vector2 TileSize { get; } = ArgGuard.RequireFinitePositive(tileSize);
    public TextureAddressMode TileModeX { get; } = tileModeX;
    public TextureAddressMode TileModeY { get; } = tileModeY;
    public TextureFilter FilterMode { get; } = filterMode;
    public XnaColor BaseColor => XnaColor.White;
}
