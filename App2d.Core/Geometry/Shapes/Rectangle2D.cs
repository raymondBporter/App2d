using System.Numerics;

namespace App2d.Core.Geometry;

// Axis-aligned in local space; its WorldObject transform may orient it in world space.
public class Rectangle2D : IConvexShape2D, IRect2D
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
    public float Area => PrimitiveGeometry2D.RectangleArea(Min, Max);

    public bool ContainsPoint(Vector2 localPoint) =>
        PrimitiveGeometry2D.RectangleContainsPoint(localPoint, Min, Max);

    public Vector2 GetSupportPoint(Vector2 localDirection) => PrimitiveGeometry2D.RectangleSupportPoint(localDirection, Min, Max);

    /// <summary>Writes the four local-space corners counter-clockwise from Min.</summary>
    public void WriteCorners(Span<Vector2> corners) => VertexGenerator2D.WriteRectangle(corners, Min, Max);

    public static Rectangle2D FromSize(Vector2 size, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotPositive(size);
        ArgGuard.ThrowIfNotFinite(center);

        var halfSize = size / 2f;
        return new Rectangle2D(center - halfSize, center + halfSize);
    }
}
