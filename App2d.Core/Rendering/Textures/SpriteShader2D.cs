using App2d.Core.Validation;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Core.Rendering.Textures;

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
    public Color4b BaseColor => Color4b.White;
}
