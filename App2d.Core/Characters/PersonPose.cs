using System.Numerics;
namespace App2d.Core.Characters;

/// <summary>Shared posed controls for drawing, attachments and gameplay geometry.</summary>
public sealed class PersonPose
{
    private readonly Vector2 _pivot;
    private readonly float _ppu, _radius, _weaponLength;
    private readonly Vector3[] _points = new Vector3[24];
    private readonly Vector3[] _scratch = new Vector3[24];
    private readonly float _restLegReach;
    public PersonPose(PointLibrary library)
    {
        var reference = library.Clips.TryGetValue("idle", out var idle) ? idle : library.Clips.Values.First();
        reference.Sample(0, _scratch);
        _restLegReach = ((_scratch[7].Y - _scratch[5].Y) + (_scratch[10].Y - _scratch[8].Y)) / 2;
        var spec = library.Drawing; var pivot = spec.GetProperty("pivot"); _pivot = new(pivot[0].GetSingle(), pivot[1].GetSingle());
        _ppu = spec.GetProperty("pixelsPerUnit").GetSingle(); _weaponLength = spec.GetProperty("weaponLength").GetSingle();
        _radius = spec.GetProperty("parts").EnumerateArray().First(p => p.GetProperty("kind").GetString() == "circle").GetProperty("radius").GetSingle() / _ppu;
    }
    public void Transform(Vector3[] raw, CharacterAppearance look)
    {
        raw = Retarget(raw, look);
        var flip = look.Flip ? -1f : 1f;
        for (var i = 0; i < raw.Length; i++) _points[i] = new((raw[i].X - _pivot.X) / _ppu * look.Width * look.Size * flip,
            (_pivot.Y - raw[i].Y) / _ppu * look.Height * look.Size, raw[i].Z / _ppu * look.Size);
        var u = new Vector2(raw[17].X - raw[0].X, raw[17].Y - raw[0].Y);
        var length = u.Length(); u /= MathF.Max(length, 1e-8f);
        var direction = new Vector2(u.X * look.Width * flip, -u.Y * look.Height); direction /= MathF.Max(direction.Length(), 1e-8f);
        var neck = new Vector2((raw[0].X - u.X * _radius * _ppu - _pivot.X) / _ppu * look.Width * look.Size * flip,
            (_pivot.Y - raw[0].Y + u.Y * _radius * _ppu) / _ppu * look.Height * look.Size);
        var radius = _radius * look.Size * look.Head;
        var head = neck + direction * radius;
        _points[0] = new(head, _points[0].Z); _points[17] = new(head + direction * length * look.Size / _ppu, _points[17].Z);
        PrepareSword(raw, look, 18, 19, 22, 16); PrepareSword(raw, look, 20, 21, 23, 13);
    }
    /// <summary>Per-pose proportion edits in export space: hips spread about their midpoints, limbs scale from their roots, feet stay on the pivot.</summary>
    private Vector3[] Retarget(Vector3[] raw, CharacterAppearance look)
    {
        if (look.LegLength == 1 && look.ArmLength == 1 && look.HipWidth == 1) return raw;
        raw.CopyTo(_scratch, 0); var p = _scratch;
        if (look.HipWidth != 1)
        {
            var torso = (p[1] + p[2]) / 2; p[1] = torso + (p[1] - torso) * look.HipWidth; p[2] = torso + (p[2] - torso) * look.HipWidth;
            var hips = (p[5] + p[8]) / 2;
            var left = (p[5] - hips) * (look.HipWidth - 1); var right = (p[8] - hips) * (look.HipWidth - 1);
            for (var i = 5; i <= 7; i++) p[i] += left;
            for (var i = 8; i <= 10; i++) p[i] += right;
        }
        Chain(p, 5, look.LegLength); Chain(p, 8, look.LegLength); Chain(p, 11, look.ArmLength); Chain(p, 14, look.ArmLength);
        var lift = new Vector3(0, (look.LegLength - 1) * _restLegReach, 0);
        for (var i = 0; i < p.Length; i++) p[i] -= lift;
        return p;
    }
    private static void Chain(Vector3[] p, int root, float factor)
    {
        if (factor == 1) return;
        var middle = p[root] + (p[root + 1] - p[root]) * factor;
        p[root + 2] = middle + (p[root + 2] - p[root + 1]) * factor; p[root + 1] = middle;
    }
    public void CopyTo(Vector3[] points) => _points.CopyTo(points, 0);
    public Vector3 Point(int index) => _points[index];
    public float Radius(CharacterAppearance look) => _radius * look.Size * look.Head;
    private void PrepareSword(Vector3[] raw, CharacterAppearance look, int grip, int tip, int width, int hand)
    {
        Vector3 Direction(int index) { var d = raw[index] - raw[grip]; return new(d.X * look.Width * (look.Flip ? -1 : 1), -d.Y * look.Height, d.Z); }
        var blade = Direction(tip); var across = Direction(width); _points[grip] = _points[hand];
        if (blade.Length() < 1e-8f) { _points[tip] = _points[width] = _points[grip]; return; }
        blade = Vector3.Normalize(blade); across -= Vector3.Dot(across, blade) * blade; across /= MathF.Max(across.Length(), 1e-8f);
        _points[tip] = _points[grip] + blade * _weaponLength; _points[width] = _points[grip] + across * _weaponLength;
    }

}
