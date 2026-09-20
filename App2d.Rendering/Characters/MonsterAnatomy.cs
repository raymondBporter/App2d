using App2d.Core.Characters;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

internal sealed class MonsterAnatomy : ICharacterAnatomy
{
    private sealed record Shape(int[] Indices, int[][] Triangles, Color? Fill, float Width, bool Closed, int Center, int Anchor, int[] WingPoints);
    private readonly Shape[] _shapes;
    private readonly Vector3[] _points, _neutral;
    private readonly int[] _faceIndices;
    private readonly int[][] _faceTriangles;
    private readonly Vector2[] _faceUV;
    public MonsterAnatomy(PointLibrary library)
    {
        var drawing = library.Drawing;
        _points = new Vector3[library.PointNames.Count]; _neutral = drawing.GetProperty("neutral").EnumerateArray().Select(p => p.Vector()).ToArray();
        _shapes = drawing.GetProperty("shapes").EnumerateArray().Select(s =>
        {
            var triangles = s.TryGetProperty("triangles", out var t) ? t.EnumerateArray().Select(r => r.Ints()).ToArray() : [];
            return new Shape(s.GetProperty("indices").Ints(), triangles, s.Text("fill") is { Length: > 0 } fill ? CharacterJson.Color(fill) : null,
                s.Number("width"), s.Flag("closed"), (int)s.Number("center"), (int)s.Number("anchor"),
                s.Text("role") == "wing-membrane" ? triangles.SelectMany(i => i).Distinct().ToArray() : []);
        }).ToArray();
        var face = drawing.GetProperty("face"); _faceIndices = face.GetProperty("indices").Ints();
        _faceTriangles = face.GetProperty("triangles").EnumerateArray().Select(t => t.Ints()).ToArray();
        _faceUV = face.GetProperty("uv").EnumerateArray().Select(v => new Vector2(v[0].GetSingle(), v[1].GetSingle())).ToArray();
    }
    public void Build(Vector3[] raw, PointClip clip, double time, CharacterAppearance look, CharacterDrawOptions options, CharacterMesh mesh, CharacterMesh face, CharacterMesh backdrop)
    {
        (options.Rest ? _neutral : raw).CopyTo(_points, 0);
        foreach (var shape in _shapes) foreach (var i in shape.WingPoints) _points[i] = Vector3.Lerp((options.Rest ? _neutral : raw)[shape.Anchor], _points[i], look.WingSize);
        var yaw = look.Yaw * MathF.PI / 180; var cos = MathF.Cos(yaw); var sin = MathF.Sin(yaw);
        for (var i = 0; i < _points.Length; i++) { var p = _points[i]; _points[i] = new Vector3((p.X * cos + p.Z * sin) * (look.Flip ? -1 : 1), p.Y, -p.X * sin + p.Z * cos) * look.Size; }
        var ink = CharacterJson.Color(look.Ink);
        foreach (var shape in _shapes)
        {
            if (shape.Fill is { } fill && options.Mode == "filled")
            {
                if (shape.Triangles.Length > 0) foreach (var t in shape.Triangles) mesh.Triangle(_points[t[0]], _points[t[1]], _points[t[2]], fill);
                else for (var i = 0; i < shape.Indices.Length; i++) mesh.Triangle(_points[shape.Center], _points[shape.Indices[i]], _points[shape.Indices[(i + 1) % shape.Indices.Length]], fill);
            }
            if (shape.Width == 0) { mesh.Polygon(shape.Indices.Select(i => _points[i]).ToArray(), ink, null, 0); continue; }
            for (var i = 1; i < shape.Indices.Length; i++) mesh.Line(_points[shape.Indices[i - 1]] - new Vector3(0, 0, .006f), _points[shape.Indices[i]] - new Vector3(0, 0, .006f), shape.Width, ink);
            if (shape.Closed) mesh.Line(_points[shape.Indices[^1]] - new Vector3(0, 0, .006f), _points[shape.Indices[0]] - new Vector3(0, 0, .006f), shape.Width, ink);
        }
        if (look.Face != "none") foreach (var t in _faceTriangles) foreach (var i in t) face.Vertex(_points[_faceIndices[i]] - new Vector3(0, 0, .12f), Color.White, _faceUV[i]);
        if (options.Joints) foreach (var p in _points) mesh.Disk(new(p.X, p.Y, -6), .014f, new Color(.88f, .28f, .18f));
    }
}
