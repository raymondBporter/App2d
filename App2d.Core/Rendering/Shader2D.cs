using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>Material data evaluated by the MonoGame renderer in object space.</summary>
public interface IShader2D
{
    Color4b BaseColor { get; }
    Color4b GetVertexColor(Vector2 position, Rect2D bounds) => BaseColor;
}

public sealed class SolidColorShader(Color4b color) : IShader2D
{
    public Color4b BaseColor { get; } = color;
}

public sealed class LinearGradientShader(Color4b startColor, Color4b endColor) : IShader2D
{
    public Color4b BaseColor => Color4b.White;
    public Color4b GetVertexColor(Vector2 position, Rect2D bounds) =>
        Color4b.Lerp(startColor, endColor,
            bounds.Size.Y > 0f ? Math.Clamp((bounds.Max.Y - position.Y) / bounds.Size.Y, 0f, 1f) : 0f);
}
