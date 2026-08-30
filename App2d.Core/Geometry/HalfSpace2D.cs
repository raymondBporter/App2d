using System.Numerics;

namespace App2d.Engine.Geometry;

// The solid side satisfies dot(point, Normal) <= Offset; Normal points toward free space.
public sealed class HalfSpace2D : IShape2D
{
    public HalfSpace2D(Vector2 outwardNormal, float offset)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(outwardNormal);
        ArgGuard.ThrowIfNotFinite(offset);

        var normalLength = outwardNormal.Length();
        Normal = outwardNormal / normalLength;
        Offset = offset / normalLength;
    }

    public Vector2 Normal { get; }
    public float Offset { get; }
    public Bounds2D LocalBounds => Bounds2D.Unbounded;
    public float Area => float.PositiveInfinity;

    public bool ContainsPoint(Vector2 localPoint) => Vector2.Dot(localPoint, Normal) <= Offset;

    public static HalfSpace2D FromPoint(Vector2 pointOnBoundary, Vector2 outwardNormal)
    {
        var normalized = Vector2.Normalize(outwardNormal);
        return new HalfSpace2D(normalized, Vector2.Dot(pointOnBoundary, normalized));
    }
}
