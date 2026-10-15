using App2d.Core.Geometry.Operations.Vertices;
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
        ArgGuard.ThrowIfNotFiniteOrNotPositive(scale);
        return shape switch
        {
            Circle2D circle => Circle2D.Builder.Create(circle.Radius * scale, circle.Center * scale),
            Ellipse2D ellipse => Ellipse2D.Builder.Create(ellipse.Radii * scale, ellipse.Center * scale),
            Capsule2D capsule => Capsule2D.Builder.Create(capsule.Start * scale, capsule.End * scale, capsule.Radius * scale),
            RoundedRectangle2D rounded => RoundedRectangle2D.Builder.Create(rounded.OuterRect.Min * scale, rounded.OuterRect.Max * scale, rounded.Radius * scale),
            AxisAlignedRectangle2D rectangle => AxisAlignedRectangle2D.Builder.Create(rectangle.Min * scale, rectangle.Max * scale),
            Rectangle2D rectangle => Rectangle2D.Builder.Create(rectangle.Min * scale, rectangle.Max * scale),
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
}
