using App2d.Core;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Textures;

/// <summary>Maps one full texture onto finite local bounds in the Y-up world.</summary>
public sealed class SpriteShader2D(Texture2D texture, TextureFilter filterMode = TextureFilter.Linear) : IShader2D
{
    public Texture2D Texture
    {
        get;
        set => field = ArgGuard.RequireNotNull(value);
    } = ArgGuard.RequireNotNull(texture);

    public TextureFilter FilterMode { get; } = filterMode;
    public bool FlipX { get; set; }
    public bool FlipY { get; set; }
    public XnaColor BaseColor => XnaColor.White;
}
