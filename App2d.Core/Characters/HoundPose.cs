
using System.Numerics;


namespace App2d.Core.Characters;

public sealed class HoundPose
{
    private static readonly string[][] Chains = [["FrontUpperLeg", "FrontLowerLeg", "IKFrontLeg", "FF"], ["BackLeg", "BackUpperLeg", "BackLowerLeg", "IKBackLeg", "FFB"]];
    private readonly Dictionary<string, int> _indices;
    private readonly Vector3[] _source, _drawing;
    public HoundPose(PointLibrary library)
    {
        _indices = library.PointNames.Select((name, i) => (name, i)).ToDictionary(p => p.name, p => p.i);
        _source = new Vector3[_indices.Count]; _drawing = new Vector3[_indices.Count];
    }
    private Vector3 At(string name, string end = "head") => _drawing[_indices[name + ":" + end]];
    private void Deform(CharacterAppearance look)
    {
        _source.CopyTo(_drawing, 0);
        void Scale(int index, Vector3 anchor, float scale)
        { var p = _drawing[index]; _drawing[index] = new(anchor.X + (p.X - anchor.X) * scale, anchor.Y + (p.Y - anchor.Y) * scale, p.Z); }
        if (look.LegLength != 1)
        {
            foreach (var side in new[] { "L", "R" }) foreach (var chain in Chains)
            {
                var anchor = _source[_indices[chain[0] + "." + side + ":head"]];
                foreach (var bone in chain) foreach (var end in new[] { "head", "tail" }) Scale(_indices[bone + "." + side + ":" + end], anchor, look.LegLength);
            }
            var feet = _indices.Where(p => p.Key.StartsWith("FF.") || p.Key.StartsWith("FFB.")).Select(p => p.Value).ToArray();
            var drop = feet.Min(i => _drawing[i].Y) - feet.Min(i => _source[i].Y);
            for (var i = 0; i < _drawing.Length; i++) _drawing[i].Y -= drop;
            foreach (var side in new[] { "L", "R" }) foreach (var chain in Chains)
            {
                var names = chain.Select(n => n + "." + side).ToArray(); var lengths = new float[names.Length];
                for (var i = 1; i < names.Length; i++) { var a = _source[_indices[names[i - 1] + ":head"]]; var b = _source[_indices[names[i] + ":head"]]; lengths[i] = lengths[i - 1] + new Vector2(b.X - a.X, b.Y - a.Y).Length(); }
                var total = lengths[^1]; var foot = names[^1]; var correction = _source[_indices[foot + ":head"]].Y - At(foot).Y;
                for (var i = 0; i < names.Length - 1; i++) foreach (var end in new[] { "head", "tail" })
                {
                    var index = _indices[names[i] + ":" + end]; var p = _source[index]; var a = _source[_indices[names[i] + ":head"]];
                    var along = lengths[i] + (end == "tail" ? new Vector2(p.X - a.X, p.Y - a.Y).Length() : 0);
                    _drawing[index].Y += correction * (total > 1e-8f ? MathF.Min(1, along / total) : i / (float)(names.Length - 1));
                }
                foreach (var end in new[] { "head", "tail" }) { var index = _indices[foot + ":" + end]; _drawing[index].Y = _source[index].Y; }
            }
        }
        var tailRoot = At("Back") - new Vector3(0, .23f, 0); var neckRoot = At("Torso3") - new Vector3(0, .20f, 0);
        var oldHead = At("Head"); var newHead = Vector3.Lerp(neckRoot, oldHead, look.NeckLength); var shift = newHead - oldHead; shift.Z = 0;
        foreach (var (name, index) in _indices)
        {
            if (name.StartsWith("Tail")) Scale(index, tailRoot, look.TailLength);
            if (name.StartsWith("Neck")) Scale(index, neckRoot, look.NeckLength);
            if (name.StartsWith("Head:") || name.StartsWith("Ear")) _drawing[index] += shift;
        }
    }
    public void Transform(Vector3[] raw, CharacterAppearance look)
    {
        var yaw = look.Yaw * MathF.PI / 180; var cos = MathF.Cos(yaw); var sin = MathF.Sin(yaw);
        for (var i = 0; i < raw.Length; i++) { var p = raw[i]; _source[i] = new(-p.Y * cos + p.X * sin, p.Z, -p.X * cos - p.Y * sin); }
        Deform(look);
        // Global appearance changes happen after the authored projected contact deformation.
        for (var i = 0; i < raw.Length; i++) { _drawing[i] *= look.Size; if (look.Flip) _drawing[i].X *= -1; }
    }
    public void CopyTo(Vector3[] source, Vector3[] drawing) { _source.CopyTo(source, 0); _drawing.CopyTo(drawing, 0); }
    public Vector3 Point(string name) => _drawing[_indices[name]];
}
