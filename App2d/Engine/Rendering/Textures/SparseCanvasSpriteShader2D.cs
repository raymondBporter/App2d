using SkiaSharp;

namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Draws a root-relative sparse frame inside the same logical source canvas used
/// by legacy full-canvas sprites. This permits incremental content migration
/// without changing object placement or collision geometry.
/// </summary>
public sealed class SparseCanvasSpriteShader2D : IShader2D
{
    private readonly SKFilterMode _filterMode;
    private SparseAnimationFrame2D _frame;

    public SparseCanvasSpriteShader2D(
        SparseAnimationFrame2D frame,
        SKSizeI sourceCanvasSize,
        SKPointI sourceRoot,
        SKFilterMode filterMode = SKFilterMode.Linear)
    {
        ArgGuard.ThrowIfNull(frame);
        ValidateSource(sourceCanvasSize, sourceRoot);
        _frame = frame;
        SourceCanvasSize = sourceCanvasSize;
        SourceRoot = sourceRoot;
        _filterMode = filterMode;
    }

    public SparseAnimationFrame2D Frame
    {
        get => _frame;
        set
        {
            ArgGuard.ThrowIfNull(value);
            _frame = value;
        }
    }

    public SKSizeI SourceCanvasSize { get; private set; }
    public SKPointI SourceRoot { get; private set; }
    public SKColor BaseColor => SKColors.White;

    public void SetFrame(
        SparseAnimationFrame2D frame,
        SKSizeI sourceCanvasSize,
        SKPointI sourceRoot)
    {
        ArgGuard.ThrowIfNull(frame);
        ValidateSource(sourceCanvasSize, sourceRoot);
        _frame = frame;
        SourceCanvasSize = sourceCanvasSize;
        SourceRoot = sourceRoot;
    }

    public ShaderLease2D AcquireShader(in ShaderContext context)
    {
        StateGuard.ThrowIf(
            !context.LocalBounds.IsFinite,
            "Sparse canvas sprites require finite local bounds.");
        var bounds = context.LocalBounds;
        var scaleX = bounds.Size.X / SourceCanvasSize.Width;
        var scaleY = bounds.Size.Y / SourceCanvasSize.Height;
        return ShaderLease2D.Borrowed(Frame.Texture.GetImageShader(
            SKShaderTileMode.Decal,
            SKShaderTileMode.Decal,
            _filterMode,
            scaleX,
            -scaleY,
            bounds.Min.X + (SourceRoot.X + Frame.Origin.X) * scaleX,
            bounds.Max.Y - (SourceRoot.Y + Frame.Origin.Y) * scaleY));
    }

    private static void ValidateSource(SKSizeI canvas, SKPointI root)
    {
        if (canvas.Width <= 0 || canvas.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(canvas));
        if ((uint)root.X > (uint)canvas.Width || (uint)root.Y > (uint)canvas.Height)
            throw new ArgumentOutOfRangeException(nameof(root));
    }
}
