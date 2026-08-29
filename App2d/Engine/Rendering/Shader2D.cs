using System.Numerics;
using App2d.Engine.Geometry;
using SkiaSharp;

namespace App2d.Engine.Rendering;

public readonly record struct ShaderContext(
    Matrix3x2 ObjectToDevice,
    Bounds2D LocalBounds,
    FrameTime Time);

public interface IShader2D
{
    SKColor BaseColor { get; }
    SKShader? CreateShader(in ShaderContext context);
}

public sealed class SolidColorShader(SKColor color) : IShader2D
{
    public SKColor BaseColor { get; } = color;
    public SKShader? CreateShader(in ShaderContext context) => null;
}

public sealed class LinearGradientShader(SKColor startColor, SKColor endColor) : IShader2D
{
    private readonly SKColor[] _colors = [startColor, endColor];
    private readonly float[] _positions = [0f, 1f];

    public SKColor BaseColor => SKColors.White;

    public SKShader CreateShader(in ShaderContext context)
    {
        var localStart = new Vector2(context.LocalBounds.Center.X, context.LocalBounds.Max.Y);
        var localEnd = new Vector2(context.LocalBounds.Center.X, context.LocalBounds.Min.Y);
        return SKShader.CreateLinearGradient(new SKPoint(localStart.X, localStart.Y), new SKPoint(localEnd.X, localEnd.Y), _colors, _positions, SKShaderTileMode.Clamp);
    }
}
