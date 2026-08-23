using System.Numerics;

namespace App2d.Engine.Geometry;

public sealed class Circle2D : IConvexShape2D
{
    public Circle2D(float radius, Vector2 center = default)
    {
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive and finite.");
        if (!float.IsFinite(center.X) || !float.IsFinite(center.Y))
            throw new ArgumentOutOfRangeException(nameof(center), "Center must be finite.");

        Radius = radius;
        Center = center;
        var extent = new Vector2(radius);
        LocalBounds = new Bounds2D(center - extent, center + extent);
    }

    public float Radius { get; }
    public Vector2 Center { get; }
    public Bounds2D LocalBounds { get; }

    public bool ContainsPoint(Vector2 localPoint) => Vector2.DistanceSquared(localPoint, Center) <= Radius * Radius;

    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        if (localDirection.LengthSquared() <= float.Epsilon)
            return Center;

        return Center + Vector2.Normalize(localDirection) * Radius;
    }
}
