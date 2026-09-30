using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// Calculates local bounds for shapes on demand; shapes carry no bounds cache. Half-spaces are unbounded,
/// composites take the union of their parts, and unknown convex shapes fall back to four support points.
/// New non-convex shape types need a row here before a <see cref="SpatialObject2D"/> can hold them.
/// </summary>
public static class ShapeBounds2D
{
    /// <summary>The axis-aligned bounds of a shape in its own coordinate space.</summary>
    /// <param name="shape">Any built-in shape or custom convex shape.</param>
    /// <returns>The local bounding box, or <see cref="Rect2D.Unbounded"/> for a half-space.</returns>
    /// <exception cref="NotSupportedException">The shape is an unknown non-convex type.</exception>
    public static Rect2D Calculate(IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => Rect2D.FromCircle(circle.Center, circle.Radius),
            Ellipse2D ellipse => new(ellipse.Center - ellipse.Radii, ellipse.Center + ellipse.Radii),
            Capsule2D capsule => Rect2D.FromCapsule(capsule.Start, capsule.End, capsule.Radius),
            IRect2D rectangle => rectangle.ToRect(),
            Triangle2D triangle => new(Vector2.Min(Vector2.Min(triangle.A, triangle.B), triangle.C), Vector2.Max(Vector2.Max(triangle.A, triangle.B), triangle.C)),
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
