using App2d.Core.Characters;
using System.Numerics;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering.Characters;

internal sealed class PersonTrail
{
    private readonly float[][] _samples, _windows;
    private readonly (float Low, float High, Color Color)[] _bands;
    private readonly float _history, _fade, _width, _power, _opacity, _spacing, _start, _end, _ppu, _weaponLength;
    private readonly double _duration;
    private readonly Vector2 _pivot;
    public PersonTrail(JsonElement spec, PointClip clip)
    {
        var trail = clip.Metadata.GetProperty("trail"); var recipe = spec.GetProperty("trailStyle"); var style = recipe.GetProperty("style");
        _samples = trail.GetProperty("samples").EnumerateArray().Select(s => s.Floats()).ToArray();
        _windows = trail.GetProperty("windows").EnumerateArray().Select(s => s.Floats()).ToArray();
        _bands = recipe.GetProperty("bands").EnumerateArray().Select(b => (b[0].GetSingle(), b[1].GetSingle(), CharacterJson.Color(b[2].GetString()!))).ToArray();
        var fps = spec.Number("sourceFps"); _history = style.Number("history_ms") * fps / 1000; _fade = style.Number("fade_ms") * fps / 1000;
        _width = style.Number("blade_width"); _power = style.Number("taper_power"); _opacity = style.Number("opacity"); _spacing = style.Number("spacing_pixels");
        _start = clip.Metadata.Number("start"); _end = clip.Metadata.Number("end"); _duration = clip.Duration;
        _ppu = spec.Number("pixelsPerUnit"); _weaponLength = spec.Number("weaponLength"); var p = spec.GetProperty("pivot"); _pivot = new(p[0].GetSingle(), p[1].GetSingle());
    }
    public void Draw(CharacterMesh mesh, double time, CharacterAppearance look)
    {
        Vector4 Transform(float[] s)
        {
            var grip = new Vector2(_pivot.X + (s[1] - _pivot.X) * look.Width * look.Size * (look.Flip ? -1 : 1), _pivot.Y + (s[2] - _pivot.Y) * look.Height * look.Size);
            var d = new Vector2(s[3] - s[1], s[4] - s[2]); var length = d.Length() * look.BladeLength / _weaponLength;
            d *= new Vector2(look.Width * (look.Flip ? -1 : 1), look.Height); d /= MathF.Max(d.Length(), 1e-8f);
            return new(grip.X, grip.Y, grip.X + d.X * length, grip.Y + d.Y * length);
        }
        Vector4 Sample(float t)
        {
            if (t <= _samples[0][0]) return Transform(_samples[0]); if (t >= _samples[^1][0]) return Transform(_samples[^1]);
            var lo = 0; var hi = _samples.Length - 1;
            while (hi - lo > 1) { var mid = (lo + hi) / 2; if (_samples[mid][0] <= t) lo = mid; else hi = mid; }
            return Vector4.Lerp(Transform(_samples[lo]), Transform(_samples[hi]), (t - _samples[lo][0]) / (_samples[hi][0] - _samples[lo][0]));
        }
        Vector3 At(Vector2 p) => new((p.X - _pivot.X) / _ppu, (_pivot.Y - p.Y) / _ppu, 6);
        var frame = _start + (_end - _start) * (float)(time / _duration);
        foreach (var window in _windows)
        {
            if (frame <= window[0] || frame >= window[1] + _fade) continue;
            var head = MathF.Min(frame, window[1]); var tail = MathF.Max(window[0], head - _history);
            if (tail >= head) continue;
            var points = new List<Vector4> { Sample(tail) };
            foreach (var s in _samples) if (s[0] > tail && s[0] < head) points.Add(Transform(s));
            points.Add(Sample(head));
            var path = new List<(Vector4 P, float Distance)> { (points[0], 0) }; var length = 0f;
            for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1]; var b = points[i]; var distance = Vector2.Distance(new(a.Z, a.W), new(b.Z, b.W));
                var steps = Math.Max(1, (int)MathF.Ceiling(distance / _spacing));
                for (var j = 1; j <= steps; j++) path.Add((Vector4.Lerp(a, b, j / (float)steps), length + distance * j / steps));
                length += distance;
            }
            if (length < .5f) continue;
            var age = MathF.Max(0, (frame - window[1]) / _fade); var alpha = _opacity * (1 - age * age * (3 - 2 * age));
            Vector2 Boundary((Vector4 P, float Distance) row, float fraction)
            {
                var tip = new Vector2(row.P.Z, row.P.W); var grip = new Vector2(row.P.X, row.P.Y);
                var width = _width * MathF.Pow(MathF.Sin(row.Distance / length * MathF.PI / 2), _power);
                return Vector2.Lerp(tip + (grip - tip) * width, tip, fraction);
            }
            foreach (var band in _bands)
            {
                var color = new Color(band.Color.R, band.Color.G, band.Color.B, (byte)(alpha * 255));
                for (var i = 1; i < path.Count; i++)
                {
                    var a = At(Boundary(path[i - 1], band.Low)); var b = At(Boundary(path[i - 1], band.High));
                    var c = At(Boundary(path[i], band.Low)); var d = At(Boundary(path[i], band.High));
                    mesh.Triangle(a, b, c, color); mesh.Triangle(b, d, c, color);
                }
            }
        }
    }
}
