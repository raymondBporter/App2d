using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>Union area of finite convex parts in one coordinate space.</summary>
public static class ShapeArea2D
{
    public const int DefaultOutlineSegments = 64;

    /// <summary>
    /// Counts overlap once. Circles, ellipses, rectangles, triangles and convex polygons use analytic boundaries;
    /// other shapes use inscribed polygon outlines. Disconnected regions and holes are supported.
    /// </summary>
    /// <param name="parts">At least one finite convex part in a shared coordinate space.</param>
    /// <param name="outlineSegments">At least three samples for polygonized curves or custom support mappings.</param>
    public static float Union(ReadOnlySpan<IConvexShape2D> parts, int outlineSegments = DefaultOutlineSegments)
    {
        ArgGuard.ThrowIfTooShort(parts, 1);
        ArgGuard.ThrowIfContainsNull(parts);
        ArgGuard.ThrowIf(outlineSegments < 3, "Curved outlines need at least three samples.", nameof(outlineSegments));
        if (parts.Length == 1) return parts[0].Area;
        if (parts.Length == 2 && parts[0] is IRect2D first && parts[1] is IRect2D second)
            return Area2D.RectangleUnion(first.Min, first.Max, second.Min, second.Max);

        var boundaries = new Area2D.UnionPart[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var shape = parts[i];
            if (shape is Circle2D circle)
            {
                boundaries[i] = new(circle.Center, circle.Radius);
                continue;
            }
            if (shape is Ellipse2D ellipse)
            {
                boundaries[i] = new(ellipse.Center, ellipse.Radii);
                continue;
            }
            if (shape is ConvexPolygon2D polygon)
            {
                boundaries[i] = new(polygon.Vertices);
                continue;
            }
            Vector2[] vertices;
            if (shape is RoundedRectangle2D rounded)
            {
                var perCorner = checked(outlineSegments + 3) / 4;
                vertices = new Vector2[checked(4 * (perCorner + 1))];
                VertexGenerator2D.WriteRoundedRectangle(vertices, rounded.Min, rounded.Max, rounded.Radius, perCorner);
            }
            else
            {
                var count = shape.GetOutlineVertCount(outlineSegments);
                vertices = new Vector2[count > 0 ? count : outlineSegments];
                if (count > 0) shape.GetOutlineVerts(vertices, outlineSegments);
                else
                {
                    for (var j = 0; j < vertices.Length; j++)
                    {
                        vertices[j] = shape.GetSupportPoint(Polar2D.Direction(MathF.Tau * j / vertices.Length));
                        ArgGuard.ThrowIfNotFinite(vertices[j], nameof(parts));
                    }
                }
            }
            boundaries[i] = new(vertices);
        }
        return Area2D.Union(boundaries);
    }
}
