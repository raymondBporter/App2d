using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Spatial;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Bounds;

/// <summary>
/// Calculates local bounds for shapes on demand; shapes carry no bounds cache. Axis-aligned half-spaces retain one finite edge,
/// composites take the union of their parts, and unknown convex shapes fall back to four support points.
/// New non-convex shape types need a row here before a <see cref="SpatialObject2D"/> can hold them.
/// </summary>
public static partial class ShapeBounds2D
{
    /// <summary>The axis-aligned bounds of a shape in its own coordinate space.</summary>
    /// <param name="shape">Any built-in shape or custom convex shape.</param>
    /// <returns>The local bounding box. Unbounded axes use infinite endpoints.</returns>
    /// <exception cref="NotSupportedException">The shape is an unknown non-convex type.</exception>
    public static Rect2f Calculate(IShape2D shape)
    {
        Ensure.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => Rect2f.FromCircle(circle.Center, circle.Radius),
            Ellipse2D ellipse => ellipse.Frame.EllipseBounds(ellipse.Radii),
            Capsule2 capsule => Rect2f.FromCapsule(capsule.Start, capsule.End, capsule.Radius),
            RoundedRectangle2D rounded => rounded.Frame.BoxBounds(rounded.InnerHalfSize, rounded.Radius),
            Rectangle2D rectangle => rectangle.Frame.BoxBounds(rectangle.HalfSize),
            IRect2f rectangle => rectangle.ToRect(),
            Triangle2D triangle => Rect2f.Builder.Create(Vector2f.Min(Vector2f.Min(triangle.A, triangle.B), triangle.C), Vector2f.Max(Vector2f.Max(triangle.A, triangle.B), triangle.C)),
            ConvexPolygon2D polygon => Rect2f.FromPoints(polygon.Vertices),
            SimplePolygon2D polygon => Rect2f.FromPoints(polygon.Vertices),
            CompositeShape2D composite => Composite(composite.Parts),
            HalfSpace2D halfSpace => TransformedHalfSpace(halfSpace, Vector2d.UnitX, Vector2d.UnitY, Vector2d.Zero),
            IConvexShape2D convex => Convex(convex),
            _ => throw new NotSupportedException($"No bounds calculation is registered for {shape.GetType().Name}."),
        };
    }

    /// <summary>
    /// Bounds after a similarity pose. Circles use one scaled radius, ellipses use analytic extents, and half-spaces retain every finite edge.
    /// Other shapes conservatively transform their local bounding rectangle.
    /// </summary>
    public static Rect2f CalculateTransformed(IShape2D shape, Similarity2D pose)
    {
        var xAxis = pose.XAxis.ToVector2d();
        var yAxis = pose.YAxis.ToVector2d();
        var translation = pose.Translation.ToVector2d();
        if (shape is Circle2D circle)
        {
            var center = xAxis * circle.Center.X + yAxis * circle.Center.Y + translation;
            var extent = new Vector2d((double)circle.Radius * pose.Scale);
            return OutwardRoundedBox(center - extent, center + extent);
        }

        return CalculateProjectedBounds(shape, xAxis, yAxis, translation);
    }

    /// <summary>Bounds of an affine-placed spatial object, reusing its cached local box.</summary>
    internal static Rect2f CalculateAffineTransformed(IShape2D shape, Matrix3x2 transform, Rect2f localBounds)
    {
        var (xAxis, yAxis, translation) = transform.GetRows();
        return CalculateProjectedBounds(shape, xAxis.ToVector2d(), yAxis.ToVector2d(), translation.ToVector2d(), localBounds);
    }

    /// <summary>Bounds of a placed shape in the reference pose's local coordinates.</summary>
    internal static Rect2f CalculateInFrame(IShape2D shape, Similarity2D pose, Similarity2D frame)
    {
        var (xAxis, yAxis, translation) = pose.GetAxesRelativeTo(frame);
        return CalculateProjectedBounds(shape, xAxis, yAxis, translation);
    }

    private static Rect2f CalculateProjectedBounds(IShape2D shape, Vector2d xAxis, Vector2d yAxis, Vector2d translation, Rect2f? localBounds = null)
    {
        Ensure.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => BoundsFromRadiusVectors(TransformDirection(circle.Center) + translation,
                xAxis * circle.Radius, yAxis * circle.Radius),
            Ellipse2D ellipse => BoundsFromRadiusVectors(TransformDirection(ellipse.Center) + translation,
                TransformDirection(ellipse.Axis) * ellipse.Radii.X, TransformDirection(ellipse.Frame.Perp) * ellipse.Radii.Y),
            HalfSpace2D halfSpace => TransformedHalfSpace(halfSpace, xAxis, yAxis, translation),
            _ => TransformedBox(localBounds ?? Calculate(shape), xAxis, yAxis, translation),
        };

        Vector2d TransformDirection(Vector2f direction) => xAxis * direction.X + yAxis * direction.Y;
    }

    /// <summary>
    /// A circle enclosing a shape in its own space. Unlike <see cref="Calculate"/> the result does not change when the
    /// shape rotates, so it suits world bounds for spinning objects. It is a cheap enclosing circle, not the minimal one:
    /// polygonal shapes use the center of their box and their farthest vertex.
    /// </summary>
    /// <param name="shape">Any built-in shape or custom convex shape.</param>
    /// <returns>The local center and radius; a half-space reports an infinite radius.</returns>
    public static (Vector2f Center, float Radius) CalculateBoundingCircle(IShape2D shape)
    {
        Ensure.ThrowIfNull(shape);
        switch (shape)
        {
            case Circle2D circle:
                return (circle.Center, circle.Radius);
            case Ellipse2D ellipse:
                return (ellipse.Center, Math.Max(ellipse.Radii.X, ellipse.Radii.Y));
            case Capsule2 capsule:
                return (capsule.Center, float.IsFinite(capsule.Diameter) ? capsule.Diameter / 2
                    : (float)(Vector2d.Distance(capsule.Start.ToVector2d(), capsule.End.ToVector2d()) / 2 + capsule.Radius));
            case RoundedRectangle2D rounded:
                return (rounded.Center, (float)(new Vector2d(rounded.InnerHalfSize).Length() + rounded.Radius));
            case Rectangle2D rectangle:
                return (rectangle.Center, (float)new Vector2d(rectangle.HalfSize).Length());
            case HalfSpace2D:
                return (Vector2f.Zero, float.PositiveInfinity);
            case CompositeShape2D composite:
                {
                    var center = Calculate(composite).Center;
                    var radius = 0f;
                    foreach (var part in composite.Parts)
                    {
                        var (partCenter, partRadius) = CalculateBoundingCircle(part);
                        radius = Math.Max(radius, Vector2f.Distance(center, partCenter) + partRadius);
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

                    Span<Vector2f> vertices = count <= 64 ? stackalloc Vector2f[count] : new Vector2f[count];
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

    private static Rect2f BoundsFromRadiusVectors(Vector2d center, Vector2d xRadius, Vector2d yRadius)
    {
        var extentX = new Vector2d(xRadius.X, yRadius.X).Length();
        var extentY = new Vector2d(xRadius.Y, yRadius.Y).Length();
        var extent = new Vector2d(extentX, extentY);
        return OutwardRoundedBox(center - extent, center + extent);
    }

    private static Rect2f TransformedBox(Rect2f bounds, Vector2d xAxis, Vector2d yAxis, Vector2d translation)
    {
        if (!bounds.IsFinite)
        {
            return Rect2f.Unbounded;
        }

        var fromMinX = xAxis * bounds.Min.X;
        var fromMaxX = xAxis * bounds.Max.X;
        var fromMinY = yAxis * bounds.Min.Y;
        var fromMaxY = yAxis * bounds.Max.Y;
        var min = Vector2d.Min(fromMinX, fromMaxX) + Vector2d.Min(fromMinY, fromMaxY) + translation;
        var max = Vector2d.Max(fromMinX, fromMaxX) + Vector2d.Max(fromMinY, fromMaxY) + translation;
        return OutwardRoundedBox(min, max);
    }

    private static Rect2f OutwardRoundedBox(Vector2d min, Vector2d max) =>
        Rect2f.Builder.CreateUnbounded(new(Lower(min.X), Lower(min.Y)), new(Upper(max.X), Upper(max.Y)));

    private static Rect2f TransformedHalfSpace(HalfSpace2D halfSpace, Vector2d xAxis, Vector2d yAxis, Vector2d translation)
    {
        var (minX, maxX) = HalfSpaceAxis(halfSpace, xAxis.X, yAxis.X, translation.X);
        var (minY, maxY) = HalfSpaceAxis(halfSpace, xAxis.Y, yAxis.Y, translation.Y);
        return Rect2f.Builder.CreateUnbounded(new(minX, minY), new(maxX, maxY));
    }

    private static (float Min, float Max) HalfSpaceAxis(HalfSpace2D halfSpace, double x, double y, double translation)
    {
        if (x == 0 && y == 0)
        {
            return (Lower(translation), Upper(translation));
        }

        var normal = halfSpace.Normal;
        // A linear coordinate has a finite bound on a half-space only when it is parallel to the normal.
        // Test exact parallelism: snapping a nearly aligned line to an axis would incorrectly exclude distant points.
        if (new Vector2d(x, y).Cross(normal.ToVector2d()) != 0)
        {
            return (float.NegativeInfinity, float.PositiveInfinity);
        }

        var scale = Math.Abs(normal.X) >= Math.Abs(normal.Y) ? x / normal.X : y / normal.Y;
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

    private static Rect2f Composite(ReadOnlySpan<IShape2D> parts)
    {
        var bounds = Calculate(parts[0]);
        foreach (var part in parts[1..])
        {
            bounds = bounds.Union(Calculate(part));
        }

        return bounds;
    }

    private static Rect2f Convex(IConvexShape2D shape) => Rect2f.Builder.Create(
        new(shape.GetSupportPoint(-Vector2f.UnitX).X, shape.GetSupportPoint(-Vector2f.UnitY).Y),
        new(shape.GetSupportPoint(Vector2f.UnitX).X, shape.GetSupportPoint(Vector2f.UnitY).Y));
}
