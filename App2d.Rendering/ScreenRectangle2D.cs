namespace App2d.Rendering;

/// <summary>A floating-point rectangle in device pixels, with Y increasing downward.</summary>
public readonly record struct ScreenRectangle2D(float Left, float Top, float Right, float Bottom)
{
    public float Width => Right - Left;
    public float Height => Bottom - Top;
    public float MidX => (Left + Right) * 0.5f;
    public float MidY => (Top + Bottom) * 0.5f;
    public bool Contains(float x, float y) => x >= Left && x < Right && y >= Top && y < Bottom;
    public static ScreenRectangle2D Inflate(ScreenRectangle2D bounds, float x, float y) =>
        new(bounds.Left - x, bounds.Top - y, bounds.Right + x, bounds.Bottom + y);
}
