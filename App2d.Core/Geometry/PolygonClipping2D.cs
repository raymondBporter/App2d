using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Sutherland-Hodgman clipping against half-spaces and convex polygons in either winding.</summary>
public static class PolygonClipping2D
{
    /// <summary>
    /// Writes the part of a finite rectangle on the solid side of a half-space, dot(point, unitNormal) &lt;= offset.
    /// Returns 0 to 5 perimeter vertices; the output must have room for five. The boundary is included.
    /// </summary>
    /// <param name="rectangle">A finite, ordered rectangle.</param>
    /// <param name="unitNormal">The finite, nonzero half-space normal pointing toward free space.</param>
    /// <param name="offset">The signed boundary offset along the normal.</param>
    /// <param name="output">A buffer of at least five vertices.</param>
    /// <returns>The number of vertices written.</returns>
    public static int ClipRectangleToHalfSpace<TRect>(TRect rectangle, Vector2 unitNormal, float offset, Span<Vector2> output) where TRect : IRect2D
    {
        ArgGuard.ThrowIfNull(rectangle);
        ArgGuard.ThrowIf(!rectangle.IsFinite || rectangle.Min.X > rectangle.Max.X || rectangle.Min.Y > rectangle.Max.Y, "Rectangle bounds must be finite and ordered.", nameof(rectangle));
        ArgGuard.ThrowIf(output.Length < 5, "Output needs room for five vertices.", nameof(output));
        Span<Vector2> corners = stackalloc Vector2[4];
        VertexGenerator2D.WriteRectangle(corners, rectangle.Min, rectangle.Max);
        return ClipConvexToHalfSpace(corners, unitNormal, offset, output);
    }

    /// <summary>
    /// Clips a convex polygon against dot(point, unitNormal) &lt;= offset without allocating.
    /// The output needs at least subject.Length + 1 slots and must not overlap the subject.
    /// </summary>
    /// <param name="subject">The convex perimeter to clip; empty input yields empty output.</param>
    /// <param name="unitNormal">The finite, nonzero half-space normal pointing toward free space.</param>
    /// <param name="offset">The signed boundary offset along the normal.</param>
    /// <param name="output">A buffer of at least subject.Length + 1 vertices.</param>
    /// <returns>The number of vertices written.</returns>
    public static int ClipConvexToHalfSpace(ReadOnlySpan<Vector2> subject, Vector2 unitNormal, float offset, Span<Vector2> output)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(unitNormal);
        ArgGuard.ThrowIfNotFinite(offset);
        if (subject.IsEmpty) return 0;
        ArgGuard.ThrowIf(output.Length < subject.Length + 1, "Output needs room for one more vertex than the subject.", nameof(output));
        ArgGuard.ThrowIf(subject.Overlaps(output), "Input and output must not overlap.", nameof(output));
        foreach (var point in subject) ArgGuard.ThrowIfNotFinite(point, nameof(subject));

        var count = 0;
        var previous = subject[^1];
        var before = SignedSide(previous, unitNormal, offset);
        foreach (var current in subject)
        {
            var after = SignedSide(current, unitNormal, offset);
            if ((before < 0 && after > 0) || (before > 0 && after < 0)) output[count++] = Vector2.Lerp(previous, current, (float)(before / (before - after)));
            if (after <= 0) output[count++] = current;
            previous = current;
            before = after;
        }
        return count;
    }

    /// <summary>Clips a subject polygon against a convex clipping polygon, in either winding.</summary>
    /// <param name="subject">The polygon to clip.</param>
    /// <param name="clip">A convex polygon with at least three vertices and nonzero area.</param>
    /// <returns>The clipped perimeter, possibly empty.</returns>
    public static List<Vector2> ClipConvex(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip) => Clip(subject, clip, static point => point, static (a, b, t) => Vector2.Lerp(a, b, t));

    /// <summary>Clips in XY while interpolating Z as a carried vertex attribute.</summary>
    /// <param name="subject">The polygon to clip, with Z carried through.</param>
    /// <param name="clip">A convex polygon with at least three vertices and nonzero XY area; its Z is ignored.</param>
    /// <returns>The clipped perimeter with interpolated Z, possibly empty.</returns>
    public static List<Vector3> ClipConvexXY(IReadOnlyList<Vector3> subject, IReadOnlyList<Vector3> clip) => Clip(subject, clip, static point => new Vector2(point.X, point.Y), static (a, b, t) => Vector3.Lerp(a, b, t));

    private static double SignedSide(Vector2 point, Vector2 normal, float offset) => (double)point.X * normal.X + (double)point.Y * normal.Y - offset;

    private static List<T> Clip<T>(IReadOnlyList<T> subject, IReadOnlyList<T> clip, Func<T, Vector2> xy, Func<T, T, float, T> lerp)
    {
        ArgGuard.ThrowIfNull(subject);
        ArgGuard.ThrowIfNull(clip);
        ArgGuard.ThrowIf(clip.Count < 3, "The clipping polygon needs at least three vertices.", nameof(clip));
        var twiceArea = 0d;
        for (var i = 0; i < clip.Count; i++) twiceArea += xy(clip[i]).CrossDouble(xy(clip[(i + 1) % clip.Count]));
        ArgGuard.ThrowIf(twiceArea == 0, "The clipping polygon needs nonzero area.", nameof(clip));
        var winding = Math.Sign(twiceArea);
        var points = subject.ToList();
        for (var edge = 0; edge < clip.Count && points.Count > 0; edge++)
        {
            var a = xy(clip[edge]);
            var b = xy(clip[(edge + 1) % clip.Count]);
            if (Vector2.DistanceSquared(a, b) < 1e-12f) continue;
            var clipped = new List<T>();
            var previous = points[^1];
            var before = winding * a.Orientation(b, xy(previous));
            foreach (var current in points)
            {
                var after = winding * a.Orientation(b, xy(current));
                if ((before >= 0) != (after >= 0)) clipped.Add(lerp(previous, current, (float)(before / (before - after))));
                if (after >= 0) clipped.Add(current);
                previous = current;
                before = after;
            }
            points = clipped;
        }
        return points;
    }
}
