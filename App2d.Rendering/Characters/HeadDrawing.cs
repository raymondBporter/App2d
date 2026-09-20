using App2d.Core.Characters;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

/// <summary>One head definition rendered in the workshop or attached to an animated rig.</summary>
public sealed class HeadDrawing
{
    private HeadShape? _shape;
    private Vector2[] _contour = [];
    private int[] _triangles = [];
    public void Build(CharacterMesh body, CharacterMesh face, HeadShape shape, Vector3 origin, Vector3 right, Vector3 down, float lineWidth, Color ink, FacePose? expression = null)
    {
        if (_shape != shape)
        {
            shape.Validate();
            var contour = shape.Contour(); var triangles = Triangulate(contour);
            _contour = contour; _triangles = triangles; _shape = shape;
        }
        Vector3 At(Vector2 p) => origin + right * p.X + down * p.Y;
        var fill = CharacterJson.Color(shape.Color);
        for (var i = 0; i < _triangles.Length; i += 3) body.Triangle(At(_contour[_triangles[i]]), At(_contour[_triangles[i + 1]]), At(_contour[_triangles[i + 2]]), fill);
        for (var i = 0; i < _contour.Length; i++) body.Line(At(_contour[i]) - new Vector3(0, 0, .001f), At(_contour[(i + 1) % _contour.Length]) - new Vector3(0, 0, .001f), lineWidth, ink);
        if (shape.Face == "none" && expression is null) return;
        var angle = shape.FaceAngle * MathF.PI / 180; var cos = MathF.Cos(angle); var sin = MathF.Sin(angle);
        if (expression is not null || FaceExpressions.Contains(shape.Face))
        {
            Vector3 Feature(Vector2 p)
            {
                p *= shape.FaceSize;
                return At(new(shape.FaceX + p.X * cos - p.Y * sin, shape.FaceY + p.X * sin + p.Y * cos)) - new Vector3(0, 0, .012f);
            }
            FaceDrawing.Build(body, expression ?? FaceExpressions.Get(shape.Face), Feature, right.Length() * shape.FaceSize * .033f, ink);
            return;
        }
        foreach (var uv in new[] { new Vector2(0, 0), new(0, 1), new(1, 0), new(1, 0), new(0, 1), new(1, 1) })
        {
            var p = (uv - new Vector2(.5f)) * shape.FaceSize;
            p = new(shape.FaceX + p.X * cos - p.Y * sin, shape.FaceY + p.X * sin + p.Y * cos);
            face.Vertex(At(p) - new Vector3(0, 0, .006f), Color.White, uv);
        }
    }
    // Ear clipping preserves concave muzzle/jaw edits; a triangle fan would fill outside the outline.
    private static int[] Triangulate(Vector2[] points)
    {
        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        var remaining = Enumerable.Range(0, points.Length).ToList(); var result = new List<int>();
        var area = 0f;
        for (var i = 0; i < points.Length; i++) area += points[i].X * points[(i + 1) % points.Length].Y - points[(i + 1) % points.Length].X * points[i].Y;
        var sign = MathF.Sign(area);
        while (remaining.Count > 3)
        {
            var removed = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var a = remaining[(i + remaining.Count - 1) % remaining.Count]; var b = remaining[i]; var c = remaining[(i + 1) % remaining.Count];
                var turn = Cross(points[a], points[b], points[c]) * sign;
                if (turn < -1e-9f) continue;
                if (MathF.Abs(turn) <= 1e-9f) { remaining.RemoveAt(i); removed = true; break; }
                if (remaining.Any(p => p != a && p != b && p != c && Cross(points[a], points[b], points[p]) * sign >= 0 && Cross(points[b], points[c], points[p]) * sign >= 0 && Cross(points[c], points[a], points[p]) * sign >= 0)) continue;
                result.AddRange([a, b, c]); remaining.RemoveAt(i); removed = true; break;
            }
            if (!removed) throw new InvalidDataException("The rounded head outline overlaps itself. Reduce softness or adjust the points.");
        }
        if (remaining.Count == 3) result.AddRange(remaining);
        return result.ToArray();
    }
}
