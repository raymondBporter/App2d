using App2d.Core.Geometry.Clipping;
using App2d.Core.Geometry.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Meshes.Tessellation;

/// <summary>Backend-independent triangle and boundary views over shape geometry and caller-owned sampling storage.</summary>
/// <remarks>Borrowed views must be consumed before reusing scratch. Composite parts are separate surfaces, not a polygon union.</remarks>
public static class ShapeTessellation2D
{
    /// <summary>Reuses concave polygon triangulation; finite convex outlines use a fan. Half-spaces require finite clip bounds.</summary>
    public static IndexedTriangleEnumerable<Vector2f> Triangles(this IShape2D shape, Span<Vector2f> scratch,
        int roundSegments = 32, int cornerSegments = 8, Rect2f? clipBounds = null)
    {
        Ensure.ThrowIfNull(shape);
        if (shape is SimplePolygon2D polygon) return polygon.Mesh.Triangles;
        if (shape is HalfSpace2D halfSpace)
        {
            var count = PolygonClipping2D.ClipRectangleToHalfSpace(RequireClipBounds(clipBounds), halfSpace.Normal, halfSpace.Offset, scratch);
            return MeshTopology.TriangleFan(scratch[..count]);
        }
        return MeshTopology.TriangleFan(OutlineVertices(shape, scratch, roundSegments, cornerSegments));
    }

    /// <summary>Returns true perimeter segments. A half-space contributes only its boundary line, never the clip rectangle's edges.</summary>
    public static SegmentVertexEnumerable<Segment<Vector2f, float>, Vector2f> OutlineSegments(this IShape2D shape, Span<Vector2f> scratch,
        int roundSegments = 32, int cornerSegments = 8, Rect2f? clipBounds = null)
    {
        Ensure.ThrowIfNull(shape);
        if (shape is HalfSpace2D halfSpace)
        {
            Ensure.ThrowIfTooShort(scratch, 2);
            var boundary = new Line2D(halfSpace.Normal * halfSpace.Offset, halfSpace.Normal.PerpCcw);
            var count = 0;
            if (boundary.TryClipToRectangle(RequireClipBounds(clipBounds), out var start, out var end))
            {
                scratch[0] = start;
                scratch[1] = end;
                count = 2;
            }
            return MeshTopology.Segments(scratch[..count], closed: false);
        }
        return MeshTopology.Segments(OutlineVertices(shape, scratch, roundSegments, cornerSegments));
    }

    private static ReadOnlySpan<Vector2f> OutlineVertices(IShape2D shape, Span<Vector2f> scratch, int roundSegments, int cornerSegments) => shape switch
    {
        ConvexPolygon2D polygon => polygon.Vertices,
        SimplePolygon2D polygon => polygon.Vertices,
        _ => shape.GetOutlineVerts(scratch, roundSegments, cornerSegments)
    };

    /// <summary>Sampling quality for a projected curve. Keeps view scale out of the stored geometry.</summary>
    public static int RoundSegmentsFor(this IShape2D shape, Matrix3x2 localToView) => shape switch
    {
        Circle2D circle => CurveSegments(circle.Radius, localToView),
        Ellipse2D ellipse => CurveSegments(MathF.Max(ellipse.Radii.X, ellipse.Radii.Y), localToView),
        Capsule2D capsule => CurveSegments(capsule.Radius, localToView),
        _ => 32
    };

    public static int CurveSegments(float radius, Matrix3x2 localToView)
    {
        Ensure.NonNegative(radius);
        Ensure.Finite(localToView);
        if (radius == 0) return 16;
        var scale = Math.Max(localToView.X.Length(), localToView.Y.Length());
        var segments = MathF.Ceiling(MathF.PI * MathF.Sqrt(Math.Max(1f, radius * scale) * 2f));
        return (int)Math.Clamp(segments, 16f, 128f);
    }

    private static Rect2f RequireClipBounds(Rect2f? clipBounds)
    {
        if (clipBounds is not { IsFinite: true } bounds)
            throw new ArgumentException("An infinite shape needs finite local clip bounds.", nameof(clipBounds));
        return bounds;
    }
}
