using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

internal sealed class WeaponDrawing
{
    private sealed record Shape(string Kind, Vector2[] Points, Vector2 Center, Vector2? Attachment, float Radius, float HalfLength, float ReferenceLength, Color? Fill, Color? Stroke, float Width, bool Closed);
    private readonly Dictionary<string, Shape[]> _art = [];
    public WeaponDrawing(JsonElement spec)
    {
        var length = spec.Number("weaponLength");
        Shape[] Read(JsonElement art) => art.EnumerateArray().Select(s => new Shape(s.Text("kind"),
            s.TryGetProperty("points", out var points) ? points.EnumerateArray().Select(p => new Vector2(p[0].GetSingle(), p[1].GetSingle())).ToArray() : [],
            s.TryGetProperty("center", out var center) ? new(center[0].GetSingle(), center[1].GetSingle()) : default,
            s.TryGetProperty("attachment", out var attachment) ? new(attachment[0].GetSingle(), attachment[1].GetSingle()) : null,
            s.Number("radius"), s.Number("halfLength"), s.Number("referenceLength"), s.GetProperty("fill").Rgba(), s.GetProperty("stroke").Rgba(), s.Number("width") * length, s.Flag("closed"))).ToArray();
        _art.Add("sword", Read(spec.GetProperty("weaponArt")));
        _art.Add("pistol", [
            new("", [new(-.055f,.14f),new(.07f,.14f),new(.08f,-.12f),new(-.07f,-.12f)], default, null, 0, 0, 0, Color.White, Color.Black, .025f, true),
            new("", [new(-.09f,-.1f),new(.09f,-.1f),new(.09f,-.48f),new(-.09f,-.48f)], default, null, 0, 0, 0, Color.White, Color.Black, .025f, true)]);
        if (spec.TryGetProperty("weapons", out var weapons)) foreach (var weapon in weapons.EnumerateObject())
            if (weapon.Value.TryGetProperty("art", out var art)) _art.Add(weapon.Name, Read(art));
    }
    public void Draw(CharacterMesh mesh, string weapon, Vector3 grip, Vector3 axis, Vector3 across, float length, float headSize)
    {
        if (!_art.TryGetValue(weapon, out var shapes)) throw new InvalidDataException("Weapon artwork is missing: " + weapon);
        Vector3 At(Vector2 p) => grip + across * p.X - axis * p.Y;
        foreach (var s in shapes)
        {
            var headLength = (s.ReferenceLength == 0 ? length : s.ReferenceLength) * headSize;
            if (s.Kind == "ball")
            {
                var center = At(s.Center); var radius = s.Radius * headLength; center.Z -= radius;
                var outer = Enumerable.Range(0, 32).Select(i => center + new Vector3(MathF.Cos(i / 32f * MathF.Tau), MathF.Sin(i / 32f * MathF.Tau), 0) * (radius + s.Width / 2)).ToArray();
                var inner = Enumerable.Range(0, 32).Select(i => center + new Vector3(MathF.Cos(i / 32f * MathF.Tau) * MathF.Max(0, radius - s.Width / 2), MathF.Sin(i / 32f * MathF.Tau) * MathF.Max(0, radius - s.Width / 2), -.0005f)).ToArray();
                mesh.Polygon(outer, s.Stroke, null, 0); mesh.Polygon(inner, s.Fill, null, 0);
            }
            else if (s.Kind == "cylinder")
            {
                var headAxis = axis * (headLength / length); var headAcross = across * (headLength / length);
                var center = s.Attachment is { } attachment ? At(attachment) + headAxis * s.Radius : At(s.Center);
                var cross = Vector3.Cross(across, axis); var normal = cross / MathF.Max(cross.Length(), 1e-8f) * headLength;
                var centers = new[] { center - headAcross * s.HalfLength, center + headAcross * s.HalfLength };
                Vector3 Ring(Vector3 c, float t) => c + s.Radius * (-headAxis * MathF.Cos(t) + normal * MathF.Sin(t));
                var rings = centers.Select(c => Enumerable.Range(0, 32).Select(i => Ring(c, i / 32f * MathF.Tau)).ToArray()).ToArray();
                for (var i = 0; i < 32; i++)
                {
                    var j = (i + 1) % 32;
                    mesh.Triangle(rings[0][i], rings[1][i], rings[1][j], s.Fill!.Value); mesh.Triangle(rings[0][i], rings[1][j], rings[0][j], s.Fill.Value);
                    for (var end = 0; end < 2; end++) mesh.Triangle(centers[end], rings[end][i], rings[end][j], s.Fill.Value);
                }
                var front = new Vector3(0, 0, s.Width * .6f);
                foreach (var ring in rings) for (var i = 0; i < 32; i++) mesh.Line(ring[i] - front, ring[(i + 1) % 32] - front, s.Width, s.Stroke!.Value);
                var tangent = MathF.Atan2(normal.Z, -headAxis.Z) + MathF.PI / 2;
                foreach (var t in new[] { tangent, tangent + MathF.PI }) mesh.Line(Ring(centers[0], t) - front, Ring(centers[1], t) - front, s.Width, s.Stroke!.Value);
            }
            else
            {
                var vertices = s.Points.Select(At).ToArray();
                if (s.Closed) mesh.Polygon(vertices, s.Fill, s.Stroke, s.Width);
                else if (s.Stroke is { } stroke) for (var i = 1; i < vertices.Length; i++) mesh.Line(vertices[i - 1], vertices[i], s.Width, stroke);
            }
        }
    }
}
