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
    ShaderLease2D AcquireShader(in ShaderContext context);
}

/// <summary>
/// Describes either a temporary shader owned by the caller or a cached shader
/// borrowed from another resource, such as a texture.
/// </summary>
public readonly record struct ShaderLease2D : IDisposable
{
    private readonly bool _ownsShader;

    private ShaderLease2D(SKShader? shader, bool ownsShader)
    {
        Shader = shader;
        _ownsShader = ownsShader;
    }

    public SKShader? Shader { get; }

    public static ShaderLease2D Owned(SKShader? shader) => new(shader, ownsShader: true);
    public static ShaderLease2D Borrowed(SKShader shader) => new(shader, ownsShader: false);

    public void Dispose()
    {
        if (_ownsShader)
            Shader?.Dispose();
    }
}

public sealed class SolidColorShader(SKColor color) : IShader2D
{
    public SKColor BaseColor { get; } = color;
    public ShaderLease2D AcquireShader(in ShaderContext context) =>
        ShaderLease2D.Owned(null);
}

public sealed class LinearGradientShader(SKColor startColor, SKColor endColor) : IShader2D
{
    private readonly SKColor[] _colors = [startColor, endColor];
    private readonly float[] _positions = [0f, 1f];

    public SKColor BaseColor => SKColors.White;

    public ShaderLease2D AcquireShader(in ShaderContext context)
    {
        var localStart = new Vector2(context.LocalBounds.Center.X, context.LocalBounds.Max.Y);
        var localEnd = new Vector2(context.LocalBounds.Center.X, context.LocalBounds.Min.Y);
        var shader = SKShader.CreateLinearGradient(new SKPoint(localStart.X, localStart.Y), new SKPoint(localEnd.X, localEnd.Y), _colors, _positions, SKShaderTileMode.Clamp);
        return ShaderLease2D.Owned(shader);
    }
}
