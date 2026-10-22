using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Transforms;

/// <summary>Transforms local shape parameters and vertex buffers, or creates uniformly scaled shape copies.</summary>
public static class ShapeTransforms2D
{
    /// <summary>The world center and radius of a posed circle.</summary>
    /// <param name="circle">The local circle.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <returns>The transformed center and the uniformly scaled radius.</returns>
    public static (Vector2 Center, float Radius) Circle(Circle2D circle, Similarity2D pose) => (pose.TransformPoint(circle.Center), circle.Radius * pose.Scale);

    /// <summary>The world spine and radius of a posed capsule.</summary>
    /// <param name="capsule">The local capsule.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <returns>The transformed endpoints and the uniformly scaled radius.</returns>
    public static (Vector2 Start, Vector2 End, float Radius) Capsule(Capsule2D capsule, Similarity2D pose) => (pose.TransformPoint(capsule.Start), pose.TransformPoint(capsule.End), capsule.Radius * pose.Scale);

    /// <summary>The world unit normal and offset of a posed half-space.</summary>
    /// <param name="halfSpace">The local half-space.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <returns>The normalized transformed normal and the offset of the transformed boundary point along it.</returns>
    public static (Vector2 UnitNormal, float Offset) HalfSpace(HalfSpace2D halfSpace, Similarity2D pose)
    {
        // Normals transform by (A⁻¹)ᵀ = A / Scale² for this family, so the direct direction transform is exact once normalized.
        var normal = Vector2.Normalize(pose.TransformDirection(halfSpace.Normal));
        var boundary = pose.TransformPoint(halfSpace.Normal * halfSpace.Offset);
        return (normal, Vector2.Dot(boundary, normal));
    }

    /// <summary>Writes a drawing outline transformed into world space; returns zero for shapes without a single outline.</summary>
    /// <param name="shape">The local shape.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <param name="vertices">A caller-owned buffer large enough for the local outline.</param>
    /// <param name="roundSegments">Samples around circles and ellipses; capsules use half per cap.</param>
    public static int WriteWorldOutline(IShape2D shape, Similarity2D pose, Span<Vector2> vertices, int roundSegments = Ellipse2D.DefaultOutlineSegments)
    {
        var count = shape.GetOutlineVertCount(roundSegments);
        if (count == 0)
        {
            return 0;
        }

        shape.GetOutlineVerts(vertices, roundSegments);
        for (var i = 0; i < count; i++)
        {
            vertices[i] = pose.TransformPoint(vertices[i]);
        }

        return count;
    }

    /// <summary>
    /// Writes the world-space convex core of a shape: a point plus radius for circles, a segment plus radius for capsules,
    /// and the perimeter with zero radius for polygonal shapes. Ellipses have no polygonal core and return zero. This is the form
    /// <see cref="Distance2D.SignedDistanceBetweenConvexPolygons"/> takes; <see cref="ShapeVertices2D.GetVerts"/> exposes only exact local cores.
    /// </summary>
    /// <param name="shape">Any shape.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <param name="vertices">A buffer of at least <see cref="ShapeVertices2D.GetVertCount"/> entries.</param>
    /// <param name="radius">The world-space round radius of the core.</param>
    /// <returns>The number of vertices written, or 0 for shapes without a convex core.</returns>
    public static int WriteWorldConvexCore(IShape2D shape, Similarity2D pose, Span<Vector2> vertices, out float radius)
    {
        var count = shape.GetVertCount();
        radius = 0f;
        if (count == 0)
        {
            return 0;
        }

        shape.GetVerts(vertices, out radius);

        for (var i = 0; i < count; i++)
        {
            vertices[i] = pose.TransformPoint(vertices[i]);
        }

        radius *= pose.Scale;
        return count;
    }

    /// <summary>A copy of a built-in convex shape uniformly scaled about the local origin.</summary>
    /// <param name="shape">A built-in convex shape.</param>
    /// <param name="scale">The finite, positive scale factor.</param>
    /// <returns>A new shape of the same type with scaled geometry.</returns>
    /// <exception cref="NotSupportedException">The shape is not a built-in convex type.</exception>
    public static IConvexShape2D Scaled(IConvexShape2D shape, float scale)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.Positive(scale);
        return shape switch
        {
            Circle2D circle => Circle2D.Builder.Create(circle.Radius * scale, circle.Center * scale),
            Ellipse2D ellipse => Ellipse2D.Builder.Create(ellipse.Radii * scale, ellipse.Center * scale, ellipse.Axis),
            Capsule2D capsule => Capsule2D.Builder.Create(capsule.Start * scale, capsule.End * scale, capsule.Radius * scale),
            RoundedRectangle2D rounded => RoundedRectangle2D.Builder.Create(rounded.Center * scale, rounded.HalfSize * scale, rounded.Radius * scale, rounded.Axis),
            AxisAlignedRectangle2D rectangle => AxisAlignedRectangle2D.Builder.Create(rectangle.Min * scale, rectangle.Max * scale),
            Rectangle2D rectangle => Rectangle2D.Builder.Create(rectangle.Center * scale, rectangle.HalfSize * scale, rectangle.Axis),
            Triangle2D triangle => Triangle2D.Builder.Create(triangle.A * scale, triangle.B * scale, triangle.C * scale),
            ConvexPolygon2D polygon => ScaledPolygon(polygon, scale),
            _ => throw new NotSupportedException($"Cannot scale {shape.GetType().Name}.")
        };
    }

    private static ConvexPolygon2D ScaledPolygon(ConvexPolygon2D polygon, float scale)
    {
        var vertices = polygon.Vertices.ToArray();
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] *= scale;
        }

        return ConvexPolygon2D.Builder.Create(vertices);
    }

    /// <summary>
    /// A copy of a built-in shape carried through a similarity: translated, rotated, uniformly scaled and optionally
    /// mirrored. Every kind returns its own kind. An <see cref="AxisAlignedRectangle2D"/> accepts only poses whose axes
    /// stay parallel to the world axes, because its contract is to never rotate.
    /// </summary>
    /// <param name="shape">A built-in shape.</param>
    /// <param name="pose">A finite pose with a positive scale.</param>
    /// <returns>A validated shape of the same kind in the pose's target space.</returns>
    /// <exception cref="NotSupportedException">The shape is a height field, an unknown type, or an axis-aligned rectangle under a rotating pose.</exception>
    public static IShape2D Posed(IShape2D shape, Similarity2D pose)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.ThrowIf(!pose.IsFinite || pose.Scale <= 0f, "The pose must be finite with a positive scale.", nameof(pose));
        switch (shape)
        {
            case Circle2D circle:
                {
                    var (center, radius) = Circle(circle, pose);
                    return Circle2D.Builder.Create(radius, center);
                }
            case Capsule2D capsule:
                {
                    var (start, end, radius) = Capsule(capsule, pose);
                    return Capsule2D.Builder.Create(start, end, radius);
                }
            case HalfSpace2D halfSpace:
                {
                    var (normal, offset) = HalfSpace(halfSpace, pose);
                    return HalfSpace2D.Builder.Create(normal, offset);
                }
            case Triangle2D triangle:
                return Triangle2D.Builder.Create(pose.TransformPoint(triangle.A), pose.TransformPoint(triangle.B), pose.TransformPoint(triangle.C));
            case ConvexPolygon2D polygon:
                return ConvexPolygon2D.Builder.Create(PosedVertices(polygon.Vertices, pose));
            case SimplePolygon2D polygon:
                return SimplePolygon2D.Builder.Create(PosedVertices(polygon.Vertices, pose));
            // The oriented builders normalize the transformed axis, so a mirrored or scaled pose needs no extra care.
            case Rectangle2D rectangle:
                return Rectangle2D.Builder.Create(pose.TransformPoint(rectangle.Center), rectangle.HalfSize * pose.Scale, pose.TransformDirection(rectangle.Axis));
            case RoundedRectangle2D rounded:
                return RoundedRectangle2D.Builder.Create(pose.TransformPoint(rounded.Center), rounded.HalfSize * pose.Scale, rounded.Radius * pose.Scale, pose.TransformDirection(rounded.Axis));
            case Ellipse2D ellipse:
                return Ellipse2D.Builder.Create(ellipse.Radii * pose.Scale, pose.TransformPoint(ellipse.Center), pose.TransformDirection(ellipse.Axis));
            case AxisAlignedRectangle2D rectangle:
                return PosedAxisAligned(rectangle, pose);
            case CompositeShape2D composite:
                {
                    var parts = new IConvexShape2D[composite.Parts.Length];
                    for (var i = 0; i < parts.Length; i++)
                    {
                        parts[i] = (IConvexShape2D)Posed(composite.Parts[i], pose);
                    }

                    return CompositeShape2D.Builder.Create(parts, composite.IncludeOverlap, composite.AreaOutlineSegments);
                }
            default:
                throw new NotSupportedException($"Cannot pose {shape.GetType().Name}.");
        }
    }

    private static Vector2[] PosedVertices(ReadOnlySpan<Vector2> vertices, Similarity2D pose)
    {
        var posed = new Vector2[vertices.Length];
        for (var i = 0; i < posed.Length; i++)
        {
            posed[i] = pose.TransformPoint(vertices[i]);
        }

        return posed;
    }

    // Opposite corners stay opposite corners under any pose whose axes are parallel to the world axes: quarter turns and mirrors included.
    private static AxisAlignedRectangle2D PosedAxisAligned(AxisAlignedRectangle2D rectangle, Similarity2D pose)
    {
        var tolerance = OrientedFrame2D.AxisTolerance * pose.Scale;
        var x = pose.XAxis;
        var y = pose.YAxis;
        var parallel = (MathF.Abs(x.Y) <= tolerance && MathF.Abs(y.X) <= tolerance) || (MathF.Abs(x.X) <= tolerance && MathF.Abs(y.Y) <= tolerance);
        if (!parallel)
        {
            throw new NotSupportedException("AxisAlignedRectangle2D cannot be rotated; use Rectangle2D.");
        }

        var first = pose.TransformPoint(rectangle.Min);
        var second = pose.TransformPoint(rectangle.Max);
        return AxisAlignedRectangle2D.Builder.Create(Vector2.Min(first, second), Vector2.Max(first, second));
    }
}
