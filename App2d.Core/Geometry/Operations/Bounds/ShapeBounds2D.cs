using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Bounds;

/// <summary>
/// Calculates local bounds for shapes on demand; shapes carry no bounds cache. Axis-aligned half-spaces retain one finite edge,
/// composites take the union of their parts, and unknown convex shapes fall back to four support points.
/// New non-convex shape types need a row here before a <see cref="SpatialObject2D"/> can hold them.
/// </summary>
public static class ShapeBounds2D
{
    /// <summary>The axis-aligned bounds of a shape in its own coordinate space.</summary>
    /// <param name="shape">Any built-in shape or custom convex shape.</param>
    /// <returns>The local bounding box. Unbounded axes use infinite endpoints.</returns>
    /// <exception cref="NotSupportedException">The shape is an unknown non-convex type.</exception>
    public static Rect2D Calculate(IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => Rect2D.FromCircle(circle.Center, circle.Radius),
            Ellipse2D ellipse => ellipse.Frame.EllipseBounds(ellipse.Radii),
            Capsule2D capsule => Rect2D.FromCapsule(capsule.Start, capsule.End, capsule.Radius),
            RoundedRectangle2D rounded => rounded.Frame.BoxBounds(rounded.InnerHalfSize, rounded.Radius),
            Rectangle2D rectangle => rectangle.Frame.BoxBounds(rectangle.HalfSize),
            IRect2D rectangle => rectangle.ToRect(),
            Triangle2D triangle => new(Vector2.Min(Vector2.Min(triangle.A, triangle.B), triangle.C), Vector2.Max(Vector2.Max(triangle.A, triangle.B), triangle.C)),
            ConvexPolygon2D polygon => Rect2D.FromPoints(polygon.Vertices),
            SimplePolygon2D polygon => Rect2D.FromPoints(polygon.Vertices),
            HeightFieldShape2D field => field.Profile.Bounds(),
            CompositeShape2D composite => Composite(composite.Parts),
            HalfSpace2D halfSpace => TransformedHalfSpace(halfSpace, Matrix3x2.Identity),
            IConvexShape2D convex => Convex(convex),
            _ => throw new NotSupportedException($"No bounds calculation is registered for {shape.GetType().Name}."),
        };
    }

    /// <summary>
    /// Bounds after an affine transform. Circles and ellipses use analytic extents, and half-spaces retain every finite edge.
    /// Other shapes conservatively transform their local bounding rectangle.
    /// </summary>
    public static Rect2D CalculateTransformed(IShape2D shape, Matrix3x2 transform)
    {
        ArgGuard.ThrowIfNull(shape);
        return TryCalculateTransformed(shape, transform, out var bounds) ? bounds : Calculate(shape).TransformedBy(transform);
    }

    internal static bool TryCalculateTransformed(IShape2D shape, Matrix3x2 transform, out Rect2D bounds)
    {
        switch (shape)
        {
            case Circle2D circle:
                bounds = TransformedEllipse(circle.Center, new(circle.Radius), transform);
                return true;
            case Ellipse2D ellipse:
                // The ellipse's own frame composes with the transform, so the analytic extents stay exact when it is rotated.
                bounds = TransformedEllipse(Vector2.Zero, ellipse.Radii, ellipse.Frame.Matrix * transform);
                return true;
            case HalfSpace2D halfSpace:
                bounds = TransformedHalfSpace(halfSpace, transform);
                return true;
            default:
                bounds = default;
                return false;
        }
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
            case Circle2D circle:
                return (circle.Center, circle.Radius);
            case Ellipse2D ellipse:
                return (ellipse.Center, Math.Max(ellipse.Radii.X, ellipse.Radii.Y));
            case Capsule2D capsule:
                return (capsule.Center, float.IsFinite(capsule.Diameter) ? capsule.Diameter / 2
                    : (float)(Vector2d.Distance(capsule.Start.ToVector2d(), capsule.End.ToVector2d()) / 2 + capsule.Radius));
            case RoundedRectangle2D rounded:
                return (rounded.Center, (float)(new Vector2d(rounded.InnerHalfSize).Length() + rounded.Radius));
            case Rectangle2D rectangle:
                return (rectangle.Center, (float)new Vector2d(rectangle.HalfSize).Length());
            case HalfSpace2D:
                return (Vector2.Zero, float.PositiveInfinity);
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
                    var count = shape.GetOutlineVertCount();
                    if (count == 0)
                    {
                        return (bounds.Center, bounds.HalfSize.Length());
                    }

                    Span<Vector2> vertices = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
                    shape.GetOutlineVerts(vertices);
                    var radiusSquared = 0d;
                    foreach (var vertex in vertices)
                    {
                        radiusSquared = Math.Max(radiusSquared, Vector2d.DistanceSquared(bounds.Center.ToVector2d(), vertex.ToVector2d()));
                    }

                    return (bounds.Center, (float)Math.Sqrt(radiusSquared));
                }
        }
    }

    private static Rect2D TransformedEllipse(Vector2 center, Vector2 radii, Matrix3x2 transform)
    {
        var (xColumn, yColumn) = transform.GetColumns();
        var position = Vector2d.Transform(center.ToVector2d(), transform);
        var extentX = new Vector2d((double)radii.X * xColumn.X, (double)radii.Y * xColumn.Y).Length();
        var extentY = new Vector2d((double)radii.X * yColumn.X, (double)radii.Y * yColumn.Y).Length();
        return new(new(Lower(position.X - extentX), Lower(position.Y - extentY)),
            new(Upper(position.X + extentX), Upper(position.Y + extentY)));
    }

    private static Rect2D TransformedHalfSpace(HalfSpace2D halfSpace, Matrix3x2 transform)
    {
        var (minX, maxX) = HalfSpaceAxis(halfSpace, transform.M11, transform.M21, transform.M31);
        var (minY, maxY) = HalfSpaceAxis(halfSpace, transform.M12, transform.M22, transform.M32);
        return new(new(minX, minY), new(maxX, maxY));
    }

    private static (float Min, float Max) HalfSpaceAxis(HalfSpace2D halfSpace, float x, float y, float translation)
    {
        if (x == 0 && y == 0)
        {
            return (translation, translation);
        }

        var normal = halfSpace.Normal;
        // A linear coordinate has a finite bound on a half-space only when it is parallel to the normal.
        // Test exact parallelism: snapping a nearly aligned line to an axis would incorrectly exclude distant points.
        if (new Vector2d(x, y).Cross(normal.ToVector2d()) != 0)
        {
            return (float.NegativeInfinity, float.PositiveInfinity);
        }

        var scale = Math.Abs(normal.X) >= Math.Abs(normal.Y) ? (double)x / normal.X : (double)y / normal.Y;
        var boundary = scale * halfSpace.Offset + translation;
        return scale > 0 ? (float.NegativeInfinity, Upper(boundary)) : (Lower(boundary), float.PositiveInfinity);
    }

    private static float Lower(double value)
    {
        var result = (float)value;
        return result > value ? MathF.BitDecrement(result) : result;
    }

    private static float Upper(double value)
    {
        var result = (float)value;
        return result < value ? MathF.BitIncrement(result) : result;
    }

    private static Rect2D Composite(ReadOnlySpan<IShape2D> parts)
    {
        var bounds = Calculate(parts[0]);
        foreach (var part in parts[1..])
        {
            bounds = bounds.Union(Calculate(part));
        }

        return bounds;
    }

    private static Rect2D Convex(IConvexShape2D shape) => new(
        new(shape.GetSupportPoint(-Vector2.UnitX).X, shape.GetSupportPoint(-Vector2.UnitY).Y),
        new(shape.GetSupportPoint(Vector2.UnitX).X, shape.GetSupportPoint(Vector2.UnitY).Y));
}
