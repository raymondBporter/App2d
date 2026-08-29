using App2d.Engine.Geometry;
using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Composes the two sparse animation layers directly in the Skia shading
/// pipeline. On a GPU-backed canvas the atlas samples, depth comparison, and
/// alpha composition execute in a single fragment pass.
/// </summary>
public sealed class SparseDepthCompositeShader2D : IShader2D, IDisposable
{
    private const string ShaderSource = """
        uniform shader firstColor;
        uniform shader firstDepth;
        uniform shader secondColor;
        uniform shader secondDepth;

        uniform float4 localMapping;
        uniform float2 sourceRoot;
        uniform float4 firstAtlasRect;
        uniform float2 firstOrigin;
        uniform float4 secondAtlasRect;
        uniform float2 secondOrigin;
        uniform int secondWinsDepthTie;

        half4 main(float2 position) {
            float2 source = float2(
                (position.x - localMapping.x) * localMapping.z,
                (localMapping.y - position.y) * localMapping.w);
            float2 rootRelative = source - sourceRoot;

            float2 firstLocal = rootRelative - firstOrigin;
            bool firstActive =
                firstLocal.x >= 0.0 && firstLocal.y >= 0.0 &&
                firstLocal.x < firstAtlasRect.z && firstLocal.y < firstAtlasRect.w;
            half4 first = half4(0.0);
            float firstZ = 0.0;
            if (firstActive) {
                float2 atlasPosition = firstAtlasRect.xy + firstLocal;
                first = firstColor.eval(atlasPosition);
                firstActive = first.a > 0.0;
                if (firstActive) {
                    firstZ = firstDepth.eval(atlasPosition).r;
                }
            }

            float2 secondLocal = rootRelative - secondOrigin;
            bool secondActive =
                secondLocal.x >= 0.0 && secondLocal.y >= 0.0 &&
                secondLocal.x < secondAtlasRect.z && secondLocal.y < secondAtlasRect.w;
            half4 second = half4(0.0);
            float secondZ = 0.0;
            if (secondActive) {
                float2 atlasPosition = secondAtlasRect.xy + secondLocal;
                second = secondColor.eval(atlasPosition);
                secondActive = second.a > 0.0;
                if (secondActive) {
                    secondZ = secondDepth.eval(atlasPosition).r;
                }
            }

            if (!firstActive) {
                return secondActive ? second : half4(0.0);
            }
            if (!secondActive) {
                return first;
            }

            bool secondInFront = secondZ > firstZ ||
                (secondZ == firstZ && secondWinsDepthTie != 0);
            half4 front = secondInFront ? second : first;
            half4 back = secondInFront ? first : second;
            return front + back * (1.0 - front.a);
        }
        """;

    private static readonly Lazy<SKRuntimeEffect> RuntimeEffect = new(CreateRuntimeEffect);
    private readonly SKFilterMode _colorFilterMode;
    private SparseLayeredAnimationFrame2D _frame;
    private SKSizeI _sourceCanvasSize;
    private SKPointI _sourceRoot;
    private SKShader? _cachedShader;
    private Bounds2D _cachedBounds;
    private bool _hasCachedBounds;
    private bool _disposed;

    public SparseDepthCompositeShader2D(
        SparseLayeredAnimationFrame2D frame,
        SKSizeI sourceCanvasSize,
        SKPointI sourceRoot,
        SKFilterMode colorFilterMode = SKFilterMode.Linear)
    {
        _frame = ArgGuard.RequireNotNull(frame);
        ValidateSource(sourceCanvasSize, sourceRoot);
        _sourceCanvasSize = sourceCanvasSize;
        _sourceRoot = sourceRoot;
        _colorFilterMode = colorFilterMode;
    }

    public SKColor BaseColor => SKColors.White;

    public void SetFrame(
        SparseLayeredAnimationFrame2D frame,
        SKSizeI sourceCanvasSize,
        SKPointI sourceRoot)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgGuard.ThrowIfNull(frame);
        ValidateSource(sourceCanvasSize, sourceRoot);
        if (ReferenceEquals(_frame, frame) &&
            _sourceCanvasSize == sourceCanvasSize &&
            _sourceRoot == sourceRoot)
        {
            return;
        }

        _frame = frame;
        _sourceCanvasSize = sourceCanvasSize;
        _sourceRoot = sourceRoot;
        InvalidateShader();
    }

    public ShaderLease2D AcquireShader(in ShaderContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        StateGuard.ThrowIf(
            !context.LocalBounds.IsFinite,
            "Sparse depth-composite sprites require finite local bounds.");
        StateGuard.ThrowIf(
            context.LocalBounds.Size.X <= 0f || context.LocalBounds.Size.Y <= 0f,
            "Sparse depth-composite sprite bounds must have positive size.");

        if (_cachedShader is null ||
            !_hasCachedBounds ||
            _cachedBounds != context.LocalBounds)
        {
            InvalidateShader();
            _cachedShader = BuildShader(context.LocalBounds);
            _cachedBounds = context.LocalBounds;
            _hasCachedBounds = true;
        }
        return ShaderLease2D.Borrowed(_cachedShader);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        InvalidateShader();
        GC.SuppressFinalize(this);
    }

    private SKShader BuildShader(Bounds2D localBounds)
    {
        var first = _frame.FirstLayer;
        var second = _frame.SecondLayer;
        var effect = RuntimeEffect.Value;
        using var uniforms = new SKRuntimeEffectUniforms(effect);
        uniforms["localMapping"] = new float[]
        {
            localBounds.Min.X,
            localBounds.Max.Y,
            _sourceCanvasSize.Width / localBounds.Size.X,
            _sourceCanvasSize.Height / localBounds.Size.Y
        };
        uniforms["sourceRoot"] = new SKPoint(_sourceRoot.X, _sourceRoot.Y);
        uniforms["firstAtlasRect"] = ToFloatRectangle(first.AtlasRectangle);
        uniforms["firstOrigin"] = new SKPoint(first.Origin.X, first.Origin.Y);
        uniforms["secondAtlasRect"] = ToFloatRectangle(second.AtlasRectangle);
        uniforms["secondOrigin"] = new SKPoint(second.Origin.X, second.Origin.Y);
        uniforms["secondWinsDepthTie"] = second.SortRank > first.SortRank ? 1 : 0;

        using var children = new SKRuntimeEffectChildren(effect);
        children["firstColor"] = first.ColorAtlas.GetImageShader(
            SKShaderTileMode.Decal,
            SKShaderTileMode.Decal,
            _colorFilterMode,
            1f,
            1f);
        children["firstDepth"] = first.DepthAtlas.GetImageShader();
        children["secondColor"] = second.ColorAtlas.GetImageShader(
            SKShaderTileMode.Decal,
            SKShaderTileMode.Decal,
            _colorFilterMode,
            1f,
            1f);
        children["secondDepth"] = second.DepthAtlas.GetImageShader();

        return StateGuard.RequireNotNull(
            effect.ToShader(uniforms, children),
            "Skia failed to create the sparse depth-composition shader.");
    }

    private static float[] ToFloatRectangle(SKRectI rectangle) =>
    [
        rectangle.Left,
        rectangle.Top,
        rectangle.Width,
        rectangle.Height
    ];

    private void InvalidateShader()
    {
        _cachedShader?.Dispose();
        _cachedShader = null;
        _hasCachedBounds = false;
    }

    private static SKRuntimeEffect CreateRuntimeEffect()
    {
        var effect = SKRuntimeEffect.CreateShader(ShaderSource, out var errors);
        if (effect is null)
        {
            throw new InvalidOperationException(
                $"Sparse depth-composition SkSL failed to compile: {errors}");
        }
        return effect;
    }

    private static void ValidateSource(SKSizeI canvas, SKPointI root)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(canvas));
        if ((uint)root.X > (uint)canvas.Width || (uint)root.Y > (uint)canvas.Height)
            throw new ArgumentOutOfRangeException(nameof(root));
    }
}
