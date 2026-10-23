using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.InteropServices;

namespace App2d.Core.Geometry.Operations.Area;

/// <summary>Union and intersection areas of finite convex parts in one coordinate space.</summary>
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
        if (parts.Length == 1)
        {
            return parts[0].Area;
        }

        if (parts.Length == 2 && parts[0] is IRect2D first && parts[1] is IRect2D second)
        {
            return Area2D.RectangleUnion(first.Min, first.Max, second.Min, second.Max);
        }

        var boundaries = new List<Area2D.BoundaryPart>(parts.Length);
        foreach (var shape in parts)
        {
            AddParts(boundaries, shape, outlineSegments);
        }
        return Area2D.Union(CollectionsMarshal.AsSpan(boundaries));
    }

    /// <summary>
    /// The area two convex shapes share, from the same boundary integral as <see cref="Union"/>: it sums the arcs and
    /// edges of each shape that lie inside the other, so it is exact for every kind the union is exact for and keeps
    /// its precision for thin overlaps. Shapes that only touch give zero.
    /// </summary>
    /// <param name="first">A finite convex shape.</param>
    /// <param name="second">A finite convex shape in the same coordinate space.</param>
    /// <param name="outlineSegments">At least three samples for polygonized curves or custom support mappings.</param>
    public static float Intersection(IConvexShape2D first, IConvexShape2D second, int outlineSegments = DefaultOutlineSegments)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgGuard.ThrowIf(outlineSegments < 3, "Curved outlines need at least three samples.", nameof(outlineSegments));
        if (first is IRect2D firstRect && second is IRect2D secondRect)
        {
            return Area2D.RectangleIntersection(firstRect.Min, firstRect.Max, secondRect.Min, secondRect.Max);
        }

        var a = new List<Area2D.BoundaryPart>(3);
        var b = new List<Area2D.BoundaryPart>(3);
        AddParts(a, first, outlineSegments);
        AddParts(b, second, outlineSegments);
        return Area2D.Intersection(CollectionsMarshal.AsSpan(a), CollectionsMarshal.AsSpan(b));
    }

    private static void AddParts(List<Area2D.BoundaryPart> parts, IConvexShape2D shape, int outlineSegments)
    {
        if (shape is Circle2D circle)
        {
            parts.Add(new(circle.Center, circle.Radius));
            return;
        }
        if (shape is Ellipse2D ellipse)
        {
            parts.Add(new(ellipse.Center, ellipse.Radii, ellipse.Axis));
            return;
        }
        if (shape is Capsule2D capsule)
        {
            AddCapsule(parts, capsule);
            return;
        }
        if (shape is ConvexPolygon2D polygon)
        {
            parts.Add(new(polygon.Vertices));
            return;
        }
        Vector2[] vertices;
        if (shape is RoundedRectangle2D rounded)
        {
            var perCorner = checked(outlineSegments + 3) / 4;
            vertices = new Vector2[checked(4 * (perCorner + 1))];
            VertexGenerator2D.WriteRoundedRectangle(vertices, -rounded.HalfSize, rounded.HalfSize, rounded.Radius, perCorner);
            rounded.Frame.MapFromFrame(vertices);
        }
        else
        {
            var count = shape.GetOutlineVertCount(outlineSegments);
            vertices = new Vector2[count > 0 ? count : outlineSegments];
            if (count > 0)
            {
                shape.GetOutlineVerts(vertices, outlineSegments);
            }
            else
            {
                for (var j = 0; j < vertices.Length; j++)
                {
                    vertices[j] = shape.GetSupportPoint(Polar2D.Direction(MathF.Tau * j / vertices.Length));
                    ArgGuard.Finite(vertices[j], nameof(shape));
                }
            }
        }
        parts.Add(new(vertices));
    }

    private static void AddCapsule(List<Area2D.BoundaryPart> boundaries, Capsule2D capsule)
    {
        var start = capsule.Start;
        var end = capsule.End;
        // Reversed spines describe the same capsule; canonical order makes their rectangle boundaries identical.
        if (start.X > end.X || start.X == end.X && start.Y > end.Y)
        {
            (start, end) = (end, start);
        }

        boundaries.Add(new(start, capsule.Radius));
        if (start == end)
        {
            return;
        }

        var origin = start.ToVector2d();
        var spine = end.ToVector2d() - origin;
        var normal = spine.PerpCcw * (capsule.Radius / spine.Length());
        // A capsule is exactly the union of this rectangle and the two endpoint disks.
        // Store the rectangle relative to its start; translation happens inside the shared integral.
        boundaries.Add(new([normal * -1d, spine - normal, spine + normal, normal], origin));
        boundaries.Add(new(end, capsule.Radius));
    }
}
