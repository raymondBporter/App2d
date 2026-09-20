using System.Numerics;
using System.Text.Json;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using XVector3 = Microsoft.Xna.Framework.Vector3;
using XVector2 = Microsoft.Xna.Framework.Vector2;

namespace App2d.Rendering.Characters;

/// <summary>Reusable triangle storage in character units, Y up and depth positive away.</summary>
public sealed class CharacterMesh
{
    private VertexPositionColorTexture[] _vertices;
    public CharacterMesh(int initialCapacity = 32768) => _vertices = new VertexPositionColorTexture[Math.Max(3, initialCapacity)];
    public ReadOnlySpan<VertexPositionColorTexture> Vertices => _vertices.AsSpan(0, Count);
    internal VertexPositionColorTexture[] Buffer => _vertices;
    public int Count { get; private set; }
    public int TriangleCount => Count / 3;
    public Vector3 Min { get; private set; }
    public Vector3 Max { get; private set; }
    public void Clear() { Count = 0; Min = new(float.PositiveInfinity); Max = new(float.NegativeInfinity); }
    public void Vertex(Vector3 p, Color color, Vector2 uv = default)
    {
        if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) throw new InvalidDataException("Nonfinite character geometry.");
        if (Count == _vertices.Length)
        {
            if (Count >= 1_048_576) throw new InvalidDataException("Character geometry capacity exceeded.");
            Array.Resize(ref _vertices, _vertices.Length * 2);
        }
        _vertices[Count++] = new(new XVector3(p.X, p.Y, p.Z), color, new XVector2(uv.X, uv.Y));
        Min = Vector3.Min(Min, p); Max = Vector3.Max(Max, p);
    }
    public void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color) { Vertex(a, color); Vertex(b, color); Vertex(c, color); }
    public void Disk(Vector3 p, float radius, Color color, bool dome = false)
    {
        var n = dome ? 48 : 12; var rings = dome ? 8 : 1;
        Vector3 At(int ring, int i)
        {
            var angle = i / (float)n * MathF.Tau; var r = radius * ring / rings;
            return p + new Vector3(MathF.Cos(angle) * r, MathF.Sin(angle) * r, dome ? -MathF.Sqrt(MathF.Max(0, radius * radius - r * r)) : 0);
        }
        for (var ring = 1; ring <= rings; ring++) for (var i = 0; i < n; i++)
        {
            Triangle(At(ring - 1, i), At(ring, i), At(ring, i + 1), color);
            if (ring > 1) Triangle(At(ring - 1, i), At(ring, i + 1), At(ring - 1, i + 1), color);
        }
    }
    public void Line(Vector3 a, Vector3 b, float width, Color color)
    {
        var d = new Vector2(b.X - a.X, b.Y - a.Y); var length = d.Length(); var radius = width / 2;
        if (length > 1e-8f)
        {
            var offset = new Vector3(-d.Y / length * radius, d.X / length * radius, 0);
            Triangle(a + offset, a - offset, b + offset, color); Triangle(a - offset, b - offset, b + offset, color);
        }
        Disk(a, radius, color); Disk(b, radius, color);
    }
    public void Polygon(IReadOnlyList<Vector3> points, Color? fill, Color? ink, float width)
    {
        if (fill is { } f) for (var i = 1; i < points.Count - 1; i++) Triangle(points[0], points[i], points[i + 1], f);
        if (ink is { } c) for (var i = 0; i < points.Count; i++) Line(points[i] - new Vector3(0, 0, .0005f), points[(i + 1) % points.Count] - new Vector3(0, 0, .0005f), width, c);
    }
    public void Path(IReadOnlyList<Vector3> path, Color color, float startWidth, float? endWidth = null)
    {
        var total = 0f; for (var i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
        var distance = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var step = Vector3.Distance(path[i - 1], path[i]);
            var width = startWidth + ((endWidth ?? startWidth) - startWidth) * (distance + step * .5f) / MathF.Max(total, 1e-8f);
            Line(path[i - 1], path[i], width, color); distance += step;
        }
    }
    public void Shell(Vector3 center, IReadOnlyList<Vector3> contour, float depth, Color color)
    {
        Vector3 At(float r, int i) => Vector3.Lerp(center, contour[i], r) - new Vector3(0, 0, depth * MathF.Sqrt(MathF.Max(0, 1 - r * r)));
        for (var r = 1; r <= 5; r++) for (var i = 0; i < contour.Count - 1; i++)
        {
            Triangle(At((r - 1) / 5f, i), At(r / 5f, i), At(r / 5f, i + 1), color);
            if (r > 1) Triangle(At((r - 1) / 5f, i), At(r / 5f, i + 1), At((r - 1) / 5f, i + 1), color);
        }
    }
}

internal static class CharacterJson
{
    public static float Number(this JsonElement j, string key, float fallback = 0) => j.TryGetProperty(key, out var value) ? value.GetSingle() : fallback;
    public static string Text(this JsonElement j, string key, string fallback = "") => j.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback;
    public static bool Flag(this JsonElement j, string key) => j.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.True;
    public static float[] Floats(this JsonElement j) => j.EnumerateArray().Select(v => v.GetSingle()).ToArray();
    public static int[] Ints(this JsonElement j) => j.EnumerateArray().Select(v => v.GetInt32()).ToArray();
    public static Vector3 Vector(this JsonElement j) { var a = j.Floats(); return new(a[0], a[1], a[2]); }
    public static Color Color(string hex) => new((byte)Convert.ToInt32(hex.Substring(1, 2), 16), (byte)Convert.ToInt32(hex.Substring(3, 2), 16), (byte)Convert.ToInt32(hex.Substring(5, 2), 16));
    public static Color? Rgba(this JsonElement j) => j.ValueKind == JsonValueKind.Null ? null : new Color(j[0].GetSingle(), j[1].GetSingle(), j[2].GetSingle(), j[3].GetSingle());
}
