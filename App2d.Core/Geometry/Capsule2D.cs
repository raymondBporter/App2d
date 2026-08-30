using System.Numerics;

namespace App2d.Engine.Geometry;

public sealed class Capsule2D : IConvexShape2D
{
    public Capsule2D(Vector2 start, Vector2 end, float radius)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotPositive(radius);

        Start = start;
        End = end;
        Radius = radius;
        var extent = new Vector2(radius);
        LocalBounds = new Bounds2D(Vector2.Min(start, end) - extent, Vector2.Max(start, end) + extent);
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public float Radius { get; }
    public Bounds2D LocalBounds { get; }

    public bool ContainsPoint(Vector2 localPoint) =>
        Vector2.DistanceSquared(localPoint, ClosestPoint2D.OnSegment(localPoint, Start, End)) <= Radius * Radius;

    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        var endpoint = Vector2.Dot(Start, localDirection) > Vector2.Dot(End, localDirection)
            ? Start
            : End;
        if (localDirection.LengthSquared() <= float.Epsilon)
            return endpoint;

        return endpoint + Vector2.Normalize(localDirection) * Radius;
    }
}
