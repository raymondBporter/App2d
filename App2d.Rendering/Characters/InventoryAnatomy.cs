using App2d.Core.Characters;
using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

internal sealed class InventoryAnatomy : ICharacterAnatomy
{
    private sealed record Node((int Index, float Weight)[] Weights, float Width, string Part);
    private sealed record Edge(string Name, int A, int B);
    private readonly Node[] _trunk;
    private readonly Node[][] _limbs;
    private readonly Edge[] _edges, _wings;
    private readonly Edge? _head;
    private readonly Vector3[] _points, _neutral;
    public InventoryAnatomy(PointLibrary library)
    {
        var drawing = library.Drawing; var binding = drawing.GetProperty("binding");
        _points = new Vector3[library.PointNames.Count]; _neutral = drawing.GetProperty("neutral").EnumerateArray().Select(p => p.Vector()).ToArray();
        static Edge EdgeFrom(JsonElement e) => new(e.Text("name"), (int)e.Number("a"), (int)e.Number("b"));
        static Node NodeFrom(JsonElement n)
        {
            var width = n.GetProperty("width"); var factor = 1f; var part = "";
            if (width.ValueKind == JsonValueKind.Number) factor = width.GetSingle();
            else if (width.ValueKind == JsonValueKind.String) part = width.GetString()!;
            else { part = width.Text("part"); factor = width.Number("factor"); }
            return new(n.GetProperty("weights").EnumerateArray().Select(w => (w[0].GetInt32(), w[1].GetSingle())).ToArray(), factor, part);
        }
        _edges = drawing.GetProperty("edges").EnumerateArray().Select(EdgeFrom).ToArray();
        _trunk = binding.GetProperty("trunk").EnumerateArray().Select(NodeFrom).ToArray();
        _limbs = binding.GetProperty("limbs").EnumerateArray().Select(l => l.EnumerateArray().Select(NodeFrom).ToArray()).ToArray();
        _head = binding.TryGetProperty("head", out var head) && head.ValueKind == JsonValueKind.Object ? EdgeFrom(head) : null;
        _wings = binding.GetProperty("wings").EnumerateArray().Select(EdgeFrom).ToArray();
    }
    private Vector3 Position(Node node) { var p = Vector3.Zero; foreach (var w in node.Weights) p += _points[w.Index] * w.Weight; return p; }
    private static List<Vector4> Round(List<Vector4> path, float amount)
    {
        if (amount == 0 || path.Count < 3) return path;
        var result = new List<Vector4> { path[0] }; var fraction = MathF.Min(.4f, MathF.Max(0, amount) * .4f);
        static float Distance(Vector4 a, Vector4 b) => new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z).Length();
        for (var i = 1; i < path.Count - 1; i++)
        {
            var a = path[i - 1]; var p = path[i]; var b = path[i + 1]; var la = Distance(a, p); var lb = Distance(p, b); var r = MathF.Min(la, lb) * fraction;
            if (r < 1e-7f) { result.Add(p); continue; }
            var entry = Vector4.Lerp(p, a, r / la); var exit = Vector4.Lerp(p, b, r / lb); result.Add(entry);
            for (var k = 1; k <= 4; k++) { var t = k / 4f; result.Add(entry * ((1 - t) * (1 - t)) + p * (2 * t * (1 - t)) + exit * t * t); }
        }
        result.Add(path[^1]); return result;
    }
    private static Vector3 XYZ(Vector4 v) => new(v.X, v.Y, v.Z);
    public void Build(Vector3[] raw, PointClip clip, double time, CharacterAppearance look, CharacterDrawOptions options, CharacterMesh mesh, CharacterMesh face, CharacterMesh backdrop)
    {
        var cos = MathF.Cos(look.Yaw * MathF.PI / 180); var sin = MathF.Sin(look.Yaw * MathF.PI / 180);
        for (var i = 0; i < _points.Length; i++) { var p = options.Rest ? _neutral[i] : raw[i]; _points[i] = new Vector3((p.X * cos + p.Z * sin) * (look.Flip ? -1 : 1), p.Y, -p.X * sin + p.Z * cos) * look.Size; }
        var ink = CharacterJson.Color(look.Ink); var fill = CharacterJson.Color(look.Fill); var thickness = look.LineWidth;
        if (options.Mode == "skeleton") foreach (var e in _edges)
            mesh.Line(_points[e.A], _points[e.B], thickness, e.Name.Contains(".L") ? new Color(.2f, .6f, .5f) : e.Name.Contains(".R") ? new Color(.3f, .45f, .75f) : new Color(.75f, .43f, .22f));
        else
        {
            var trunk = Round(_trunk.Select(n => new Vector4(Position(n), MathF.Max(.006f, look.Body * n.Width * (n.Part == "neck" ? look.Neck : n.Part == "tail" ? look.Tail : 1)) * look.Size)).ToList(), look.Softness);
            foreach (var nodes in _limbs) { var p = Round(nodes.Select(n => new Vector4(Position(n), 0)).ToList(), look.Softness); for (var i = 1; i < p.Count; i++) mesh.Line(XYZ(p[i - 1]), XYZ(p[i]), thickness, ink); }
            if (options.Mode == "filled" && trunk.Count > 0) Ribbon(mesh, trunk, ink, fill, MathF.Min(thickness * .55f, look.Body * .12f));
            else for (var i = 1; i < trunk.Count; i++) mesh.Line(XYZ(trunk[i - 1]), XYZ(trunk[i]), thickness, ink);
            if (options.Mode == "filled" && _head is { } head)
            {
                var radius = look.Head * look.Size; var center = (_points[head.A] + _points[head.B]) / 2 - new Vector3(0, 0, radius * .8f);
                mesh.Disk(center, radius, ink); mesh.Disk(center - new Vector3(0, 0, .002f), MathF.Max(.001f, radius - thickness * .6f), fill);
                mesh.Disk(center + new Vector3(-radius * .3f, radius * .2f, -.004f), MathF.Max(.008f, radius * .075f), ink);
            }
            if (options.Mode == "filled") foreach (var e in _wings)
            {
                var a = _points[e.A]; var b = _points[e.B]; var d = b - a; var length = MathF.Max(new Vector2(d.X, d.Y).Length(), 1e-8f);
                var wing = Enumerable.Range(0, 20).Select(i => { var t = i / 20f * MathF.Tau; var u = (MathF.Cos(t) + 1) / 2; var v = MathF.Sin(t) * MathF.Min(.11f, length * .25f); return a + d * u + new Vector3(-d.Y / length * v, d.X / length * v, -.015f); }).ToArray();
                mesh.Polygon(wing, new Color(.94f, .89f, .73f), ink, thickness);
            }
        }
        if (options.Joints) foreach (var p in _points) mesh.Disk(new(p.X, p.Y, -7), .011f, new Color(.8f, .25f, .2f));
    }
    private static void Ribbon(CharacterMesh mesh, List<Vector4> path, Color ink, Color fill, float stroke)
    {
        var sides = path.Select((p, i) =>
        {
            var a = path[Math.Max(0, i - 1)]; var b = path[Math.Min(path.Count - 1, i + 1)]; var dx = b.X - a.X; var dy = b.Y - a.Y; var length = MathF.Max(MathF.Sqrt(dx * dx + dy * dy), 1e-8f);
            var r = p.W / 2; var inner = MathF.Max(0, r - stroke);
            Vector3 At(float s, float w) => new(p.X - dy / length * w * s, p.Y + dx / length * w * s, p.Z - r * .7f);
            return (Left: At(1, r), Right: At(-1, r), IL: At(1, inner), IR: At(-1, inner), Center: new Vector3(p.X, p.Y, p.Z - r * .7f));
        }).ToArray();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) { mesh.Triangle(a, b, c, color); mesh.Triangle(b, d, c, color); }
        for (var i = 1; i < sides.Length; i++) { var a = sides[i - 1]; var b = sides[i]; Quad(a.Left, a.IL, b.Left, b.IL, ink); Quad(a.IR, a.Right, b.IR, b.Right, ink); Quad(a.IL, a.Center, b.IL, b.Center, fill); Quad(a.Center, a.IR, b.Center, b.IR, fill); }
        foreach (var i in new[] { 0, path.Count - 1 }) { var p = path[i]; mesh.Disk(XYZ(p), p.W / 2, ink); mesh.Disk(XYZ(p) - new Vector3(0, 0, .001f), MathF.Max(0, p.W / 2 - stroke), fill); }
    }
}
