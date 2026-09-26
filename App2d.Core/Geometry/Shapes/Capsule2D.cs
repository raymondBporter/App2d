using System.Numerics;

namespace App2d.Core.Geometry;

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
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public float Radius { get; }
    public float Area => PrimitiveGeometry2D.CapsuleArea(Start, End, Radius);

    public bool ContainsPoint(Vector2 localPoint) => PrimitiveGeometry2D.CapsuleContainsPoint(localPoint, Start, End, Radius);

    public Vector2 GetSupportPoint(Vector2 localDirection) => PrimitiveGeometry2D.CapsuleSupportPoint(localDirection, Start, End, Radius);
}
