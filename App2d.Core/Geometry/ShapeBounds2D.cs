using App2d.Core.Validation;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Calculates local bounds on demand. Shape geometry carries no bounds cache.</summary>
public static class ShapeBounds2D
{
    /// <summary>
    /// Calculates bounds for built-in shapes and other finite convex shapes. Half-spaces remain unbounded.
    /// New non-convex shape types need a calculation here before they can be placed in a SpatialObject2D.
    /// </summary>
    public static Rect2D Calculate(IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => Rect2D.FromCircle(circle.Center, circle.Radius),
            Ellipse2D ellipse => new(ellipse.Center - ellipse.Radii, ellipse.Center + ellipse.Radii),
            Capsule2D capsule => Rect2D.FromCapsule(capsule.Start, capsule.End, capsule.Radius),
            IRect2D rectangle => rectangle.ToRect(),
            Triangle2D triangle => new(
                Vector2.Min(Vector2.Min(triangle.A, triangle.B), triangle.C),
                Vector2.Max(Vector2.Max(triangle.A, triangle.B), triangle.C)),
            ConvexPolygon2D polygon => Rect2D.FromPoints(polygon.Vertices),
            CompositeShape2D composite => Composite(composite.Parts),
            HalfSpace2D => Rect2D.Unbounded,
            IConvexShape2D convex => Convex(convex),
            _ => throw new NotSupportedException($"No bounds calculation is registered for {shape.GetType().Name}."),
        };
    }

    private static Rect2D Composite(ReadOnlySpan<IConvexShape2D> parts)
    {
        var bounds = Calculate(parts[0]);
        foreach (var part in parts[1..]) bounds = bounds.Union(Calculate(part));
        return bounds;
    }

    private static Rect2D Convex(IConvexShape2D shape) => new(
        new(shape.GetSupportPoint(-Vector2.UnitX).X, shape.GetSupportPoint(-Vector2.UnitY).Y),
        new(shape.GetSupportPoint(Vector2.UnitX).X, shape.GetSupportPoint(Vector2.UnitY).Y));
}
