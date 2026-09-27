using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry.Functions;

/// <summary>Calculates local bounds on demand. Shape geometry carries no bounds cache.</summary>
public static class ShapeBounds2D
{
    /// <summary>
    /// Calculates bounds for built-in shapes and other finite convex shapes. Half-spaces remain unbounded.
    /// New non-convex shape types need a calculation here before they can be placed in a SpatialObject2D.
    /// </summary>
    public static Bounds2D Calculate(IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => BoundsGeometry2D.FromCircle(circle.Center, circle.Radius),
            Capsule2D capsule => BoundsGeometry2D.FromCapsule(capsule.Start, capsule.End, capsule.Radius),
            IRect2D rectangle => BoundsGeometry2D.FromRectangle(rectangle),
            ConvexPolygon2D polygon => BoundsGeometry2D.FromPoints(polygon.Vertices),
            CompositeShape2D composite => Composite(composite.Parts),
            HalfSpace2D => Bounds2D.Unbounded,
            IConvexShape2D convex => Convex(convex),
            _ => throw new NotSupportedException($"No bounds calculation is registered for {shape.GetType().Name}."),
        };
    }

    private static Bounds2D Composite(ReadOnlySpan<IConvexShape2D> parts)
    {
        var bounds = Calculate(parts[0]);
        foreach (var part in parts[1..]) bounds = BoundsGeometry2D.Union(bounds, Calculate(part));
        return bounds;
    }

    private static Bounds2D Convex(IConvexShape2D shape) => new(
        new(shape.GetSupportPoint(-Vector2.UnitX).X, shape.GetSupportPoint(-Vector2.UnitY).Y),
        new(shape.GetSupportPoint(Vector2.UnitX).X, shape.GetSupportPoint(Vector2.UnitY).Y));
}
