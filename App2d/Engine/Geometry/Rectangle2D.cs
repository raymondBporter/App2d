using System.Numerics;

namespace App2d.Engine.Geometry;

// Axis-aligned in local space; its WorldObject transform may orient it in world space.
public class Rectangle2D : IConvexShape2D
{
    public Rectangle2D(Vector2 min, Vector2 max)
    {
        if (!IsFinite(min) || !IsFinite(max) || min.X >= max.X || min.Y >= max.Y)
            throw new ArgumentException("Rectangle min must be finite and strictly below max.");

        Min = min;
        Max = max;
        LocalBounds = new Bounds2D(min, max);
    }

    public Vector2 Min { get; }
    public Vector2 Max { get; }
    public Bounds2D LocalBounds { get; }

    public bool ContainsPoint(Vector2 localPoint) =>
        localPoint.X >= Min.X && localPoint.X <= Max.X &&
        localPoint.Y >= Min.Y && localPoint.Y <= Max.Y;

    public Vector2 GetSupportPoint(Vector2 localDirection) => new(
        localDirection.X >= 0f ? Max.X : Min.X,
        localDirection.Y >= 0f ? Max.Y : Min.Y);

    public static Rectangle2D FromSize(Vector2 size, Vector2 center = default)
    {
        if (!IsFinite(size) || size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive and finite.");

        var halfSize = size / 2f;
        return new Rectangle2D(center - halfSize, center + halfSize);
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}
