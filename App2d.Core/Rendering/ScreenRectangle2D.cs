using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>A floating-point rectangle in device pixels, with Y increasing downward.</summary>
public readonly record struct ScreenRectangle2D(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
    public bool TryGetPositiveSize(out Size2D size)
    {
        if (!float.IsFinite(Width) || Width <= 0f || !float.IsFinite(Height) || Height <= 0f)
        {
            size = default;
            return false;
        }
        size = new Size2D(Width, Height);
        return true;
    }

    /// <summary>Creates a screen rectangle from its top-left pixel position and positive size.</summary>
    public static ScreenRectangle2D FromTopLeftAndSize(Vector2 topLeft, Size2D size)
    {
        ArgGuard.ThrowIfNotFinite(topLeft);
        ArgGuard.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
        var right = topLeft.X + size.Width;
        var bottom = topLeft.Y + size.Height;
        ArgGuard.ThrowIfNotFinite(right);
        ArgGuard.ThrowIfNotFinite(bottom);
        return new(topLeft.X, topLeft.Y, right, bottom);
    }
    public float MidX => (Left + Right) * 0.5f;
    public float MidY => (Top + Bottom) * 0.5f;
    public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;

    /// <summary>Expands each horizontal and vertical edge by the supplied amounts.</summary>
    public ScreenRectangle2D InflatedBy(float x, float y) =>
        new(Left - x, Top - y, Right + x, Bottom + y);

    /// <summary>Moves each horizontal and vertical edge inward by the supplied amounts.</summary>
    public ScreenRectangle2D InsetBy(float x, float y) =>
        new(Left + x, Top + y, Right - x, Bottom - y);
}
