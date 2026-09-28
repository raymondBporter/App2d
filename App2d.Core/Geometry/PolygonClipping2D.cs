using App2d.Core.Validation;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Sutherland-Hodgman clipping against a convex polygon in either winding.</summary>
public static class PolygonClipping2D
{
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
