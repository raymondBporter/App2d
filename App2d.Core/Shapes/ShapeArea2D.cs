using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Core.Shapes;

/// <summary>Union area of finite convex parts in one coordinate space.</summary>
public static class ShapeArea2D
{
    public const int DefaultOutlineSegments = 64;

    /// <summary>
    /// Counts overlap once. Circles, ellipses, capsules, rectangles, triangles and convex polygons use analytic boundaries;
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

        var boundaries = new List<Area2D.UnionPart>(parts.Length);
        foreach (var shape in parts)
        {
            if (shape is Circle2D circle)
            {
                boundaries.Add(new(circle.Center, circle.Radius));
                continue;
            }
            if (shape is Ellipse2D ellipse)
            {
                boundaries.Add(new(ellipse.Center, ellipse.Radii));
                continue;
            }
            if (shape is Capsule2D capsule)
            {
                AddCapsule(boundaries, capsule);
                continue;
            }
            if (shape is ConvexPolygon2D polygon)
            {
                boundaries.Add(new(polygon.Vertices));
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
            boundaries.Add(new(vertices));
        }
        return Area2D.Union(CollectionsMarshal.AsSpan(boundaries));
    }

    private static void AddCapsule(List<Area2D.UnionPart> boundaries, Capsule2D capsule)
    {
        var start = capsule.Start;
        var end = capsule.End;
        // Reversed spines describe the same capsule; canonical order makes their rectangle boundaries identical.
        if (start.X > end.X || start.X == end.X && start.Y > end.Y) (start, end) = (end, start);
        boundaries.Add(new(start, capsule.Radius));
        if (start == end) return;

        var origin = new Area2D.Point(start);
        var spine = new Area2D.Point(end) - origin;
        var normal = new Area2D.Point(-spine.Y, spine.X) * (capsule.Radius / Math.Sqrt(spine.Dot(spine)));
        // A capsule is exactly the union of this rectangle and the two endpoint disks.
        // Store the rectangle relative to its start; translation happens inside the shared integral.
        boundaries.Add(new([normal * -1d, spine - normal, spine + normal, normal], origin));
        boundaries.Add(new(end, capsule.Radius));
    }
}
