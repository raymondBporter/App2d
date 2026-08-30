using System.Numerics;

namespace App2d.Core.Geometry;

// Axis-aligned in local space; its WorldObject transform may orient it in world space.
public class Rectangle2D : IConvexShape2D
{
    public Rectangle2D(Vector2 min, Vector2 max)
    {
        ArgGuard.ThrowIfNotComponentWiseLessThan(min, max);
        Min = min;
        Max = max;
        LocalBounds = new Bounds2D(min, max);
    }

    public Vector2 Min { get; }
    public Vector2 Max { get; }
    public Bounds2D LocalBounds { get; }
    public float Area => (Max.X - Min.X) * (Max.Y - Min.Y);

    public bool ContainsPoint(Vector2 localPoint) =>
        localPoint.X >= Min.X && localPoint.X <= Max.X &&
        localPoint.Y >= Min.Y && localPoint.Y <= Max.Y;

    public Vector2 GetSupportPoint(Vector2 localDirection) => new(
        localDirection.X >= 0f ? Max.X : Min.X,
        localDirection.Y >= 0f ? Max.Y : Min.Y);

    /// <summary>Writes the four local-space corners counter-clockwise from Min.</summary>
    public void WriteCorners(Span<Vector2> corners)
    {
        corners[0] = Min;
        corners[1] = new Vector2(Max.X, Min.Y);
        corners[2] = Max;
        corners[3] = new Vector2(Min.X, Max.Y);
    }

    public static Rectangle2D FromSize(Vector2 size, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotPositive(size);
        ArgGuard.ThrowIfNotFinite(center);

        var halfSize = size / 2f;
        return new Rectangle2D(center - halfSize, center + halfSize);
    }
}
