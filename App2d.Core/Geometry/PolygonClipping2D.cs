using App2d.Core.Validation;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Sutherland-Hodgman clipping against a convex polygon in either winding.</summary>
public static class PolygonClipping2D
{
    /// <summary>
    /// Writes the part of a finite rectangle inside a half-space. Returns 0 to 5 perimeter vertices;
    /// output must have room for five. The half-space boundary is included.
    /// </summary>
    public static int ClipRectangleToHalfSpace<TRect>(TRect rectangle, HalfSpace2D halfSpace, Span<Vector2> output)
        where TRect : IRect2D
    {
        ArgGuard.ThrowIfNull(rectangle);
        ArgGuard.ThrowIfNull(halfSpace);
        ValidateFiniteRectangle(rectangle);
        ArgGuard.ThrowIf(output.Length < 5, "Output needs room for five vertices.", nameof(output));
        Span<Vector2> corners = [rectangle.Min, new(rectangle.Max.X, rectangle.Min.Y),
            rectangle.Max, new(rectangle.Min.X, rectangle.Max.Y)];
        return ClipConvexToHalfSpace(corners, halfSpace, output);
    }

    /// <summary>
    /// Clips a convex polygon against dot(point, Normal) &lt;= Offset without allocating.
    /// Output needs at least subject.Length + 1 slots and must not overlap subject.
    /// </summary>
    public static int ClipConvexToHalfSpace(ReadOnlySpan<Vector2> subject, HalfSpace2D halfSpace,
        Span<Vector2> output)
    {
        ArgGuard.ThrowIfNull(halfSpace);
        if (subject.IsEmpty) return 0;
        ArgGuard.ThrowIf(output.Length < subject.Length + 1,
            "Output needs room for one more vertex than the subject.", nameof(output));
        ArgGuard.ThrowIf(subject.Overlaps(output), "Input and output must not overlap.", nameof(output));
        foreach (var point in subject) ArgGuard.ThrowIfNotFinite(point, nameof(subject));

        var count = 0;
        var previous = subject[^1];
        var before = SignedSide(previous, halfSpace);
        foreach (var current in subject)
        {
            var after = SignedSide(current, halfSpace);
            if ((before < 0 && after > 0) || (before > 0 && after < 0))
                output[count++] = Vector2.Lerp(previous, current, (float)(before / (before - after)));
            if (after <= 0) output[count++] = current;
            previous = current;
            before = after;
        }
        return count;
    }

    private static double SignedSide(Vector2 point, HalfSpace2D halfSpace) =>
        (double)point.X * halfSpace.Normal.X + (double)point.Y * halfSpace.Normal.Y - halfSpace.Offset;

    private static void ValidateFiniteRectangle<TRect>(TRect rectangle) where TRect : IRect2D =>
        ArgGuard.ThrowIf(!float.IsFinite(rectangle.Min.X) || !float.IsFinite(rectangle.Min.Y) ||
            !float.IsFinite(rectangle.Max.X) || !float.IsFinite(rectangle.Max.Y) ||
            rectangle.Min.X > rectangle.Max.X || rectangle.Min.Y > rectangle.Max.Y,
            "Rectangle bounds must be finite and ordered.", nameof(rectangle));

    public static List<Vector2> ClipConvex(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> clip) =>
        Clip(subject, clip, static point => point, static (a, b, t) => Vector2.Lerp(a, b, t));

    /// <summary>Clips in XY while interpolating Z as a carried vertex attribute.</summary>
    public static List<Vector3> ClipConvexXY(IReadOnlyList<Vector3> subject, IReadOnlyList<Vector3> clip) =>
        Clip(subject, clip, static point => new Vector2(point.X, point.Y), static (a, b, t) => Vector3.Lerp(a, b, t));

    private static List<T> Clip<T>(IReadOnlyList<T> subject, IReadOnlyList<T> clip,
        Func<T, Vector2> xy, Func<T, T, float, T> lerp)
    {
        ArgGuard.ThrowIfNull(subject);
        ArgGuard.ThrowIfNull(clip);
        ArgGuard.ThrowIf(clip.Count < 3, "The clipping polygon needs at least three vertices.", nameof(clip));
        var twiceArea = 0d;
        for (var i = 0; i < clip.Count; i++) twiceArea += CrossProduct2D.Of(xy(clip[i]), xy(clip[(i + 1) % clip.Count]));
        ArgGuard.ThrowIf(twiceArea == 0, "The clipping polygon needs nonzero area.", nameof(clip));
        var winding = Math.Sign(twiceArea);
        var points = subject.ToList();
        for (var edge = 0; edge < clip.Count && points.Count > 0; edge++)
        {
            var a = xy(clip[edge]); var b = xy(clip[(edge + 1) % clip.Count]);
            if (Vector2.DistanceSquared(a, b) < 1e-12f) continue;
            var clipped = new List<T>();
            var previous = points[^1]; var before = winding * CrossProduct2D.Orientation(a, b, xy(previous));
            foreach (var current in points)
            {
                var after = winding * CrossProduct2D.Orientation(a, b, xy(current));
                if ((before >= 0) != (after >= 0))
                    clipped.Add(lerp(previous, current, (float)(before / (before - after))));
                if (after >= 0) clipped.Add(current);
                previous = current; before = after;
            }
            points = clipped;
        }
        return points;
    }
}
