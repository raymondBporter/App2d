using App2d.Core.Characters;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

internal sealed class PersonAnatomy : ICharacterAnatomy
{
    private readonly JsonElement _spec;
    private readonly Vector2 _pivot;
    private readonly float _ppu, _radius, _lineWidth, _weaponLength;
    private readonly Vector3[] _points = new Vector3[24];
    private readonly Vector3[] _torso = new Vector3[24];
    private readonly WeaponDrawing _weapons;
    private readonly PersonPose _pose;
    private readonly HeadDrawing _headDrawing = new();
    private readonly Dictionary<string, FaceArt> _faces;
    private readonly Dictionary<string, PersonTrail> _trails;
    private sealed record FaceArt(float[][] Dots, float[][] Ovals, string[][] Paths);

    public PersonAnatomy(PointLibrary library)
    {
        if (library.PointNames.Count != 24) throw new InvalidDataException("Person requires the 24-control depth rig.");
        _pose = new(library);
        _spec = library.Drawing; var pivot = _spec.GetProperty("pivot").Floats(); _pivot = new(pivot[0], pivot[1]);
        _ppu = _spec.Number("pixelsPerUnit");
        _radius = _spec.GetProperty("parts").EnumerateArray().First(p => p.Text("kind") == "circle").Number("radius") / _ppu;
        _lineWidth = _spec.GetProperty("style").Number("lineWidth") / _ppu; _weaponLength = _spec.Number("weaponLength");
        _weapons = new(_spec);
        _faces = _spec.GetProperty("faces").GetProperty("expressions").EnumerateArray().ToDictionary(f => f.Text("id"), f => new FaceArt(
            f.TryGetProperty("dots", out var dots) ? dots.EnumerateArray().Select(d => d.Floats()).ToArray() : [],
            f.TryGetProperty("ovals", out var ovals) ? ovals.EnumerateArray().Select(d => d.Floats()).ToArray() : [],
            f.TryGetProperty("paths", out var paths) ? paths.EnumerateArray().Select(p => Regex.Matches(p.GetString()!, @"[MLQ]|-?\d+(?:\.\d+)?").Select(m => m.Value).ToArray()).ToArray() : []));
        _trails = library.Clips.Values.Where(c => c.Metadata.TryGetProperty("trail", out _)).ToDictionary(c => c.Id, c => new PersonTrail(_spec, c));
    }

    public void Build(Vector3[] raw, PointClip clip, double time, CharacterAppearance look, CharacterDrawOptions options, CharacterMesh mesh, CharacterMesh face, CharacterMesh backdrop)
    {
        _pose.Transform(raw, look); _pose.CopyTo(_points);
        var flip = look.Flip ? -1f : 1f; var radius = _radius * look.Size * look.Head;
        var direction = new Vector2(_points[17].X - _points[0].X, _points[17].Y - _points[0].Y); direction /= MathF.Max(direction.Length(), 1e-8f);
        var ink = CharacterJson.Color(look.Ink); var fill = CharacterJson.Color(look.Fill);
        foreach (var start in new[] { 5, 8, 11, 14 }) for (var i = 0; i < 2; i++) mesh.Line(_points[start + i], _points[start + i + 1], _lineWidth, ink);
        var corners = new[] { _points[2], _points[1], _points[3], _points[4] };
        var shortest = Enumerable.Range(0, 4).Min(i => Vector2.Distance(new(corners[i].X, corners[i].Y), new(corners[(i + 1) % 4].X, corners[(i + 1) % 4].Y)));
        for (var i = 0; i < 4; i++)
        {
            var p = corners[i]; var a = corners[(i + 3) % 4]; var b = corners[(i + 1) % 4];
            var la = MathF.Max(new Vector2(a.X - p.X, a.Y - p.Y).Length(), 1e-8f);
            var lb = MathF.Max(new Vector2(b.X - p.X, b.Y - p.Y).Length(), 1e-8f);
            var r = shortest * .5f * look.CornerRadius; var entry = p + (a - p) * r / la; var exit = p + (b - p) * r / lb;
            for (var k = 0; k <= 5; k++) { var t = k / 5f; _torso[i * 6 + k] = r == 0 ? p : entry * ((1 - t) * (1 - t)) + p * (2 * t * (1 - t)) + exit * (t * t); }
        }
        mesh.Polygon(_torso, fill, ink, _lineWidth);
        if (look.CustomHead is { } customHead)
        {
            var up = new Vector3(direction, 0); var right = new Vector3(up.Y * flip, -up.X * flip, 0);
            _headDrawing.Build(mesh, face, customHead, _points[0] - new Vector3(0, 0, radius), right * (radius / .6f), -up * (radius / .6f), _lineWidth, ink, options.Face);
        }
        else
        {
        mesh.Disk(_points[0], radius, fill, true);
        Vector3 Rim(float angle, float r) => _points[0] + new Vector3(MathF.Cos(angle) * r, MathF.Sin(angle) * r, -MathF.Sqrt(MathF.Max(0, radius * radius - r * r)) - .001f);
        for (var i = 0; i < 64; i++)
        {
            var a = i / 64f * MathF.Tau; var b = (i + 1) / 64f * MathF.Tau;
            var p = Rim(a, radius - _lineWidth / 2); var q = Rim(a, radius + _lineWidth / 2);
            var s = Rim(b, radius - _lineWidth / 2); var t = Rim(b, radius + _lineWidth / 2);
            mesh.Triangle(p, q, s, ink); mesh.Triangle(q, t, s, ink);
        }
        }
        if (look.Weapons && look.Weapon == "pistol")
        {
            var grip = _points[16]; var aim = grip - _points[15]; aim.Z = 0;
            aim = aim.LengthSquared() < 1e-10f ? new Vector3(flip, 0, 0) : Vector3.Normalize(aim);
            _weapons.Draw(mesh, "pistol", grip, aim * look.BladeLength, new Vector3(aim.Y, -aim.X, 0) * look.BladeLength, look.BladeLength, 1);
            mesh.Disk(grip - new Vector3(0, 0, _lineWidth * .5f), _lineWidth * .55f, ink);
        }
        else if (look.Weapons && clip.Metadata.TryGetProperty("hands", out var hands)) foreach (var side in hands.EnumerateArray())
        {
            var right = side.GetString() == "r"; var grip = _points[right ? 18 : 20];
            var axis = (_points[right ? 19 : 21] - grip) * look.BladeLength / _weaponLength;
            var across = (_points[right ? 22 : 23] - grip) * look.BladeLength / _weaponLength;
            _weapons.Draw(mesh, look.Weapon, grip, axis, across, look.BladeLength, look.WeaponHeadSize);
            mesh.Disk(grip - new Vector3(0, 0, _lineWidth * .5f), _lineWidth * .55f, ink);
        }
        var faceId = look.Face == "cycle" ? _faces.Keys.ElementAt((int)(time / .8) % _faces.Count) : look.Face;
        var sourceFace = faceId.StartsWith("source:", StringComparison.Ordinal) || look.Face == "cycle";
        if (faceId.StartsWith("source:", StringComparison.Ordinal)) faceId = faceId[7..];
        if (look.CustomHead is null)
        {
            var pose = options.Face ?? (!sourceFace && FaceExpressions.Contains(faceId) ? FaceExpressions.Get(faceId) : (FacePose?)null);
            if (pose is { } facialPose)
            {
                var up = new Vector2(direction.X, direction.Y);
                var right = new Vector2(up.Y, -up.X) * flip;
                Vector3 At(Vector2 p)
                {
                    var offset = (right * p.X - up * p.Y) * radius * 1.65f;
                    // Keep flat ink features just in front of the dome to avoid intersections.
                    return _points[0] + new Vector3(offset, -radius - .012f);
                }
                FaceDrawing.Build(mesh, facialPose, At, radius * .055f, ink);
            }
            else if (_faces.TryGetValue(faceId, out var expression)) DrawFace(mesh, expression, radius, look.Flip, ink);
        }
        if (options.Joints) foreach (var p in _points) mesh.Disk(new(p.X, p.Y, -7), _lineWidth * .48f, new Color(.91f, .32f, .21f));
        if (options.Trails && look.Weapons && look.Weapon == "sword" && _trails.TryGetValue(clip.Id, out var trail)) trail.Draw(backdrop, time, look);
        if (options.Guides) DrawGuides(backdrop, clip, look);
    }

    private void DrawFace(CharacterMesh mesh, FaceArt art, float radius, bool flip, Color ink)
    {
        var size = _spec.GetProperty("faces").Number("textureSize");
        var width = _spec.GetProperty("faces").Number("strokeWidth") * _radius * 2.4f / size;
        var up = _points[17] - _points[0]; var angle = MathF.Atan2(up.Y, up.X) - MathF.PI / 2;
        var cos = MathF.Cos(angle); var sin = MathF.Sin(angle);
        Vector3 At(Vector2 p)
        {
            var a = (p.X - size / 2) * radius * 2.4f / size * (flip ? -1 : 1); var b = -(p.Y - size / 2) * radius * 2.4f / size;
            return _points[0] + new Vector3(a * cos - b * sin, a * sin + b * cos, -MathF.Sqrt(MathF.Max(0, radius * radius - a * a - b * b)) - .006f);
        }
        void Ellipses(float[][] ellipses, bool solid)
        {
            foreach (var e in ellipses)
            {
                var loop = Enumerable.Range(0, 24).Select(i => At(new(e[0] + MathF.Cos(i / 24f * MathF.Tau) * e[2], e[1] + MathF.Sin(i / 24f * MathF.Tau) * e[3]))).ToArray();
                mesh.Polygon(loop, solid ? ink : null, solid ? null : ink, width);
            }
        }
        Ellipses(art.Dots, true); Ellipses(art.Ovals, false);
        foreach (var tokens in art.Paths)
        {
            var i = 0; var previous = Vector2.Zero;
            float Number() => float.Parse(tokens[i++], CultureInfo.InvariantCulture);
            Vector2 Point() => new(Number(), Number());
            while (i < tokens.Length)
            {
                switch (tokens[i++])
                {
                    case "M": previous = Point(); break;
                    case "L": var next = Point(); mesh.Line(At(previous), At(next), width, ink); previous = next; break;
                    case "Q":
                        var control = Point(); var end = Point(); var a = At(previous);
                        for (var k = 1; k <= 10; k++) { var t = k / 10f; var b = At(previous * ((1 - t) * (1 - t)) + control * (2 * t * (1 - t)) + end * t * t); mesh.Line(a, b, width, ink); a = b; }
                        previous = end; break;
                    default: throw new InvalidDataException("Unsupported face path.");
                }
            }
        }
    }

    private void DrawGuides(CharacterMesh mesh, PointClip clip, CharacterAppearance look)
    {
        Vector3 At(float x, float y) => new((x - _pivot.X) / _ppu * look.Width * look.Size * (look.Flip ? -1 : 1), (_pivot.Y - y) / _ppu * look.Height * look.Size, 7);
        var color = new Color(110, 139, 144);
        if (clip.Metadata.TryGetProperty("wallContact", out var wall)) { var p = At(wall.Number("x") * 512, _pivot.Y); mesh.Line(p + new Vector3(0, -3, 0), p + new Vector3(0, 8, 0), .014f, color); }
        if (clip.Metadata.TryGetProperty("platformEdge", out var edge)) { var p = At(edge.Number("x") * 512, edge.Number("y") * 512); mesh.Line(p, p + new Vector3((edge.Text("side") == "left" ? -5 : 5) * (look.Flip ? -1 : 1), 0, 0), .02f, color); mesh.Line(p, p - new Vector3(0, 1, 0), .02f, color); }
        if (clip.Metadata.TryGetProperty("ladderGuide", out var ladder))
        {
            var p = At(ladder.Number("x") * 512, _pivot.Y); var spacing = ladder.Number("spacing") * 512 / _ppu * look.Size * look.Height;
            foreach (var x in new[] { -.14f, .14f }) mesh.Line(p + new Vector3(x, -2, 0), p + new Vector3(x, 8, 0), .012f, color);
            for (var y = -2f; y < 8 && spacing > .001f; y += spacing) mesh.Line(p + new Vector3(-.14f, y, 0), p + new Vector3(.14f, y, 0), .012f, color);
        }
    }
}
