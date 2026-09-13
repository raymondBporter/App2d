using App2d.Core.Geometry;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering;

/// <summary>Material data evaluated by the MonoGame renderer in object space.</summary>
public interface IShader2D
{
    XnaColor BaseColor { get; }
    XnaColor GetVertexColor(Vector2 position, Bounds2D bounds) => BaseColor;
}

public sealed class SolidColorShader(XnaColor color) : IShader2D
{
    public XnaColor BaseColor { get; } = color;
}

public sealed class LinearGradientShader(XnaColor startColor, XnaColor endColor) : IShader2D
{
    public XnaColor BaseColor => XnaColor.White;
    public XnaColor GetVertexColor(Vector2 position, Bounds2D bounds) =>
        XnaColor.Lerp(startColor, endColor,
            bounds.Size.Y > 0f ? Math.Clamp((bounds.Max.Y - position.Y) / bounds.Size.Y, 0f, 1f) : 0f);
}
