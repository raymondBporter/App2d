using System.Numerics;

namespace App2d.Engine.Geometry;

public sealed class Capsule2D : IConvexShape2D
{
    public Capsule2D(Vector2 start, Vector2 end, float radius)
    {
        if (!IsFinite(start) || !IsFinite(end))
            throw new ArgumentOutOfRangeException(nameof(start), "Capsule endpoints must be finite.");
        if (!float.IsFinite(radius) || radius <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Radius must be positive and finite.");

        Start = start;
        End = end;
        Radius = radius;
        var extent = new Vector2(radius);
        LocalBounds = new Bounds2D(
            Vector2.Min(start, end) - extent,
            Vector2.Max(start, end) + extent);
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public float Radius { get; }
    public Bounds2D LocalBounds { get; }

    public bool ContainsPoint(Vector2 localPoint)
    {
        var segment = End - Start;
        var lengthSquared = segment.LengthSquared();
        var t = lengthSquared > float.Epsilon
            ? Math.Clamp(Vector2.Dot(localPoint - Start, segment) / lengthSquared, 0f, 1f)
            : 0f;
        var closest = Start + segment * t;
        return Vector2.DistanceSquared(localPoint, closest) <= Radius * Radius;
    }

    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        var endpoint = Vector2.Dot(Start, localDirection) > Vector2.Dot(End, localDirection)
            ? Start
            : End;
        if (localDirection.LengthSquared() <= float.Epsilon)
            return endpoint;

        return endpoint + Vector2.Normalize(localDirection) * Radius;
    }

    private static bool IsFinite(Vector2 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y);
}
