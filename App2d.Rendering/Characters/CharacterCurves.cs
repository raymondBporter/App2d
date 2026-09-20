using System.Numerics;

namespace App2d.Rendering.Characters;

internal static class CharacterCurves
{
    public static List<Vector3> Smooth(IEnumerable<Vector3> input, int steps = 10)
    {
        var points = new List<Vector3>(); foreach (var p in input) if (points.Count == 0 || Vector3.Distance(p, points[^1]) > 1e-7f) points.Add(p);
        if (points.Count < 2) return points;
        var result = new List<Vector3> { points[0] };
        static Vector3 Blend(Vector3 a, Vector3 b, float ta, float tb, float t) => Vector3.Lerp(a, b, (t - ta) / (tb - ta));
        for (var i = 0; i < points.Count - 1; i++)
        {
            var p1 = points[i]; var p2 = points[i + 1]; var p0 = i > 0 ? points[i - 1] : 2 * p1 - p2; var p3 = i + 2 < points.Count ? points[i + 2] : 2 * p2 - p1;
            var t1 = MathF.Sqrt(Vector3.Distance(p0, p1)); var t2 = t1 + MathF.Sqrt(Vector3.Distance(p1, p2)); var t3 = t2 + MathF.Sqrt(Vector3.Distance(p2, p3));
            for (var j = 1; j <= steps; j++)
            {
                var t = t1 + (t2 - t1) * j / steps;
                var a1 = Blend(p0, p1, 0, t1, t); var a2 = Blend(p1, p2, t1, t2, t); var a3 = Blend(p2, p3, t2, t3, t);
                result.Add(Blend(Blend(a1, a2, 0, t2, t), Blend(a2, a3, t1, t3, t), t1, t2, t));
            }
        }
        return result;
    }
    public static bool Inside(Vector3 p, IReadOnlyList<Vector3> outline)
    {
        var inside = false;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        { var a = outline[i]; var b = outline[j]; if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside; }
        return inside;
    }
    public static List<Vector3> Trim(List<Vector3> path, IReadOnlyList<Vector3> outline)
    {
        if (path.Count < 2 || !Inside(path[0], outline)) return path;
        var exit = path.FindIndex(p => !Inside(p, outline)); if (exit < 0) return [];
        var a = path[exit - 1]; var b = path[exit];
        for (var i = 0; i < 18; i++) { var mid = (a + b) / 2; if (Inside(mid, outline)) a = mid; else b = mid; }
        return [b, .. path.Skip(exit)];
    }
    public static List<Vector3> Closed(Vector3[] controls) => Smooth([controls[^1], .. controls, controls[0], controls[1]], 8).Skip(8).Take(65).ToList();
}
