namespace App2d.Rendering;

/// <summary>A floating-point rectangle in device pixels, with Y increasing downward.</summary>
public readonly record struct ScreenRectangle2D(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
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
