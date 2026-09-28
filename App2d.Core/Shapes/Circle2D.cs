using App2d.Core.Validation;
using App2d.Core.Geometry.Functions;
using System.Numerics;

namespace App2d.Core.Shapes;

public sealed class Circle2D : IConvexShape2D
{
    public Circle2D(float radius, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(radius);
        ArgGuard.ThrowIfNotFinite(center);

        Radius = radius;
        Center = center;
    }

    public float Radius { get; }
    public Vector2 Center { get; }
    public float Area => PrimitiveGeometry2D.CircleArea(Radius);

    public bool ContainsPoint(Vector2 localPoint) => PrimitiveGeometry2D.CircleContainsPoint(localPoint, Center, Radius);

    public Vector2 GetSupportPoint(Vector2 localDirection) => PrimitiveGeometry2D.CircleSupportPoint(localDirection, Center, Radius);
}
