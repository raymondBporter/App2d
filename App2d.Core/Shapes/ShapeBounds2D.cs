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

    /// <summary>
    /// A circle enclosing a shape in its own space. Unlike <see cref="Calculate"/> the result does not change when the
    /// shape rotates, so it suits world bounds for spinning objects. It is a cheap enclosing circle, not the minimal one:
    /// polygonal shapes use the center of their box and their farthest vertex.
    /// </summary>
    /// <param name="shape">Any built-in shape or custom convex shape.</param>
    /// <returns>The local center and radius; a half-space reports an infinite radius.</returns>
    public static (Vector2 Center, float Radius) CalculateBoundingCircle(IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        switch (shape)
        {
            case Circle2D circle: return (circle.Center, circle.Radius);
            case Ellipse2D ellipse: return (ellipse.Center, Math.Max(ellipse.Radii.X, ellipse.Radii.Y));
            case Capsule2D capsule: return ((capsule.Start + capsule.End) / 2f, Vector2.Distance(capsule.Start, capsule.End) / 2f + capsule.Radius);
            case HalfSpace2D: return (Vector2.Zero, float.PositiveInfinity);
            case CompositeShape2D composite:
                {
                    var center = Calculate(composite).Center;
                    var radius = 0f;
                    foreach (var part in composite.Parts)
                    {
                        var (partCenter, partRadius) = CalculateBoundingCircle(part);
                        radius = Math.Max(radius, Vector2.Distance(center, partCenter) + partRadius);
                    }
                    return (center, radius);
                }
            default:
                {
                    var bounds = Calculate(shape);
                    var count = WorldShape2D.PerimeterVertexCount(shape);
                    if (count == 0) return (bounds.Center, bounds.HalfSize.Length());
                    Span<Vector2> vertices = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
                    WorldShape2D.WritePerimeter(shape, vertices);
                    var radiusSquared = 0f;
                    foreach (var vertex in vertices) radiusSquared = Math.Max(radiusSquared, Vector2.DistanceSquared(bounds.Center, vertex));
                    return (bounds.Center, MathF.Sqrt(radiusSquared));
                }
        }
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
