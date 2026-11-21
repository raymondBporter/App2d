using App2d.Core.Animation;
using App2d.Core.Kinematics;
using System.Globalization;
using System.Text;

namespace App2d.Tests.Characters;

/// <summary>
/// Writes converted clips out as C# source, so the authored JSON clips can be retired. Values are rounded to six significant
/// digits; times are kept exactly. Dies with <see cref="LegacyClips"/>.
/// </summary>
internal static class LegacyClipEmitter
{
    private const int Width = 150;

    private enum Ease { Straight, Smooth, SmoothStraightX }

    private static readonly Interpolator<Vector2f, float> Straight = Interpolation.Default<Vector2f, float>();
    private static readonly Interpolator<Vector2f, float> Smooth = Straight.WithProgress(Easingf.Smooth);
    private static readonly Interpolator<Vector2f, float> SmoothStraightX = Smooth.With(Vector2f.Channels.X, Interpolation.Linear<float>());
    private static readonly Interpolator<float, float> StraightTurn = Interpolation.Linear<float>();
    private static readonly Interpolator<float, float> SmoothTurn = StraightTurn.WithProgress(Easingf.Smooth);

    private static readonly Interpolator<Vector3f, float> StraightOrient = Interpolation.Default<Vector3f, float>();
    private static readonly Interpolator<Vector3f, float> SmoothOrient = StraightOrient.WithProgress(Easingf.Smooth);

    private static readonly float[] Phases = [.1f, .3f, .7f, .9f];

    /// <summary>Names an interpolator by what it does, so an ease and a recipe that behave alike are written alike.</summary>
    private static Ease Classify(Interpolator<Vector2f, float> rule)
    {
        Vector2f from = new(1, -2), to = new(4, 3);
        foreach (var (ease, candidate) in new[] { (Ease.Straight, Straight), (Ease.Smooth, Smooth), (Ease.SmoothStraightX, SmoothStraightX) })
        {
            if (Phases.All(p => Vector2f.Distance(rule.Evaluate(from, to, p), candidate.Evaluate(from, to, p)) < 1e-5f)) return ease;
        }
        throw new InvalidOperationException("A move uses an interpolation the emitter does not know how to write.");
    }

    private static Ease Classify(Interpolator<Vector3f, float> rule)
    {
        Vector3f from = new(1, -2, 3), to = new(4, 3, -1);
        foreach (var (ease, candidate) in new[] { (Ease.Straight, StraightOrient), (Ease.Smooth, SmoothOrient) })
        {
            if (Phases.All(p => (rule.Evaluate(from, to, p) - candidate.Evaluate(from, to, p)).Length() < 1e-5f)) return ease;
        }
        throw new InvalidOperationException("A mount turn uses an interpolation the emitter does not know how to write.");
    }

    private static Ease Classify(Interpolator<float, float> rule)
    {
        foreach (var (ease, candidate) in new[] { (Ease.Straight, StraightTurn), (Ease.Smooth, SmoothTurn) })
        {
            if (Phases.All(p => MathF.Abs(rule.Evaluate(1, 4, p) - candidate.Evaluate(1, 4, p)) < 1e-5f)) return ease;
        }
        throw new InvalidOperationException("A turn uses an interpolation the emitter does not know how to write.");
    }

    internal static string Time(float value) => value == 0 ? "0f" : value.ToString("R", CultureInfo.InvariantCulture) + "f";
    // Six significant digits, and nothing smaller than a millionth: that is rounding noise from the rig's own arithmetic.
    internal static string Number(float value) => MathF.Abs(value) < 1e-6f ? "0f" : Time(float.Parse(value.ToString("G6", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture));
    internal static string Vector(Vector2f value) => $"new({Number(value.X)}, {Number(value.Y)})";
    internal static string Pascal(string id) => string.Concat(id.Split('-', '_', ' ').Where(p => p.Length > 0).Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    /// <summary>"label" + "head" + keys + "close" + "tail". Keys that do not fit on the line wrap underneath, indented once more.</summary>
    internal static string Wrap(string label, string head, IReadOnlyList<string> keys, int indent, string tail, string close = ")")
    {
        var pad = new string(' ', indent);
        var single = pad + label + head + string.Join(", ", keys) + close + tail;
        if (single.Length <= Width) return single;
        var text = new StringBuilder((pad + label + head).TrimEnd());
        var line = new StringBuilder();
        for (var i = 0; i < keys.Count; i++)
        {
            if (line.Length > 0 && indent + 4 + line.Length + keys[i].Length + 2 > Width)
            {
                text.Append('\n').Append(pad).Append("    ").Append(line.ToString().TrimEnd());
                line.Clear();
            }
            line.Append(keys[i]).Append(i == keys.Count - 1 ? "" : ", ");
        }
        return text.Append('\n').Append(pad).Append("    ").Append(line).Append(close).Append(tail).ToString();
    }

    public static string Emit(string className, string skeletonClass, string summary, string regenerateWith, Skeleton2D skeleton, IReadOnlyList<string> limbs, IReadOnlyList<SkeletonClip2D> clips)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        string Joint(int joint) => Pascal(skeleton.Joints[joint].Name);
        string Use(string name)
        {
            used.Add(name);
            return name;
        }

        string Moves(string label, Track<Vector2f> track, int indent, string tail)
        {
            if (track.Keys.Length == 1) return new string(' ', indent) + label + $"Track.Constant(new Vector2f({Number(track.Keys[0].Value.X)}, {Number(track.Keys[0].Value.Y)}))" + tail;
            var eases = track.Keys.Take(track.Keys.Length - 1).Select(k => Classify(k.Next ?? track.Rule)).ToArray();
            var rule = eases.GroupBy(e => e).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            var keys = track.Keys.Select((k, i) => $"new({Time(k.Time)}, {Vector(k.Value)}{(i < eases.Length && eases[i] != rule ? ", " + Use(eases[i].ToString()) : "")})").ToList();
            return Wrap(label, rule == Ease.Straight ? "Track.Of<Vector2f>(" : $"new({Use(rule.ToString())}, ", keys, indent, tail);
        }

        string Turns(string label, Track<float> track, int indent, string tail)
        {
            if (track.Keys.Length == 1) return new string(' ', indent) + label + $"Track.Constant({Number(track.Keys[0].Value)})" + tail;
            var eases = track.Keys.Take(track.Keys.Length - 1).Select(k => Classify(k.Next ?? track.Rule)).ToArray();
            var rule = eases.GroupBy(e => e).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            var keys = track.Keys.Select((k, i) => $"new({Time(k.Time)}, {Number(k.Value)}{(i < eases.Length && eases[i] != rule ? ", " + Use(eases[i] + "Turn") : "")})").ToList();
            return Wrap(label, rule == Ease.Straight ? "Track.Linear(" : $"new({Use(rule + "Turn")}, ", keys, indent, tail);
        }

        static string Orient(Vector3f value) => $"new({Number(value.X)}, {Number(value.Y)}, {Number(value.Z)})";

        string Orients(string label, Track<Vector3f> track, int indent, string tail)
        {
            if (track.Keys.Length == 1) return new string(' ', indent) + label + $"Track.Constant(new Vector3f({Number(track.Keys[0].Value.X)}, {Number(track.Keys[0].Value.Y)}, {Number(track.Keys[0].Value.Z)}))" + tail;
            var eases = track.Keys.Take(track.Keys.Length - 1).Select(k => Classify(k.Next ?? track.Rule)).ToArray();
            var rule = eases.GroupBy(e => e).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            var keys = track.Keys.Select((k, i) => $"new({Time(k.Time)}, {Orient(k.Value)}{(i < eases.Length && eases[i] != rule ? ", " + Use(eases[i] + "Orient") : "")})").ToList();
            return Wrap(label, rule == Ease.Straight ? "Track.Of<Vector3f>(" : $"new({Use(rule + "Orient")}, ", keys, indent, tail);
        }

        string Bends(string label, Track<int> track, int indent, string tail) => track.Keys.Length == 1
            ? new string(' ', indent) + label + $"Track.Constant({track.Keys[0].Value})" + tail
            : Wrap(label, "Track.Held<int>(", [.. track.Keys.Select(k => $"new({Time(k.Time)}, {k.Value})")], indent, tail);

        // A track whose every key is zero changes nothing, so it is not written.
        bool Still(Track<Vector2f> track) => track.Keys.All(k => Vector(k.Value) == "new(0f, 0f)");

        // One entry of Joints or Limbs: its tracks inline when there is one short one, otherwise one per line.
        void Entry(StringBuilder body, string owner, List<Func<int, string, string>> tracks)
        {
            if (tracks.Count == 0) return;
            if (tracks.Count == 1 && tracks[0](0, "") is var only && !only.Contains('\n') && 12 + owner.Length + only.Length + 8 <= Width)
            {
                body.Append($"            new({owner}, {only}),\n");
                return;
            }
            body.Append($"            new({owner},\n");
            for (var i = 0; i < tracks.Count; i++) body.Append(tracks[i](16, i == tracks.Count - 1 ? ")," : ",")).Append('\n');
        }

        var members = new StringBuilder();
        var names = new List<string>();
        foreach (var clip in clips)
        {
            var name = Pascal(clip.Name);
            names.Add(name);
            members.Append($"\n    public static SkeletonClip2D {name} {{ get; }} = new(\"{clip.Name}\", {Time(clip.Duration)}{(clip.Loop ? ", loop: true" : "")})\n    {{\n");
            var joints = new StringBuilder();
            foreach (var track in clip.Joints.OrderBy(t => t.Joint))
            {
                var tracks = new List<Func<int, string, string>>();
                if (track.Move is { } move && !Still(move)) tracks.Add((indent, tail) => Moves("Move: ", move, indent, tail));
                if (track.Turn is { } turn && !turn.Keys.All(k => Number(k.Value) == "0f")) tracks.Add((indent, tail) => Turns("Turn: ", turn, indent, tail));
                if (track.Layer is { } layer && !layer.Keys.All(k => Number(k.Value) == "0f")) tracks.Add((indent, tail) => Turns("Layer: ", layer, indent, tail));
                Entry(joints, Joint(track.Joint), tracks);
            }
            if (joints.Length > 0) members.Append("        Joints =\n        [\n").Append(joints).Append("        ],\n");
            var limbTracks = new StringBuilder();
            foreach (var track in clip.Limbs.OrderBy(t => t.Limb))
            {
                var tracks = new List<Func<int, string, string>>();
                if (track.Target is { } target && !Still(target)) tracks.Add((indent, tail) => Moves("Target: ", target, indent, tail));
                if (track.Bend is { } bend) tracks.Add((indent, tail) => Bends("Bend: ", bend, indent, tail));
                Entry(limbTracks, limbs[track.Limb], tracks);
            }
            if (limbTracks.Length > 0) members.Append("        Limbs =\n        [\n").Append(limbTracks).Append("        ],\n");
            var mountTracks = new StringBuilder();
            foreach (var track in clip.Mounts.OrderBy(t => t.Mount))
            {
                // A mount that is never turned needs no track.
                if (track.Turn.Keys.All(k => Orient(k.Value) == "new(0f, 0f, 0f)")) continue;
                mountTracks.Append(Orients($"new({Pascal(skeleton.Mounts[track.Mount].Name)}, ", track.Turn, 12, "),")).Append('\n');
            }
            if (mountTracks.Length > 0) members.Append("        Mounts =\n        [\n").Append(mountTracks).Append("        ],\n");
            if (clip.Contacts.Length > 0)
            {
                members.Append(Wrap("Contacts = ", "[", [.. clip.Contacts.Select(c => $"new({limbs[c.Limb]}, {Time(c.Start)}, {Time(c.Finish)}, {Vector(c.Target)})")], 8, ",", "]")).Append('\n');
            }
            if (clip.Markers.Length > 0)
            {
                members.Append(Wrap("Markers = ", "[", [.. clip.Markers.Select(m => $"new(\"{m.Name}\", {Time(m.Time)})")], 8, ",", "]")).Append('\n');
            }
            if (clip.Travel is { } travel && !Still(travel)) members.Append(Moves("Travel = ", travel, 8, ",")).Append('\n');
            members.Append("    };\n");
        }

        var source = new StringBuilder();
        source.Append("// Generated from the authored JSON clips by LegacyClipEmitter in App2d.Tests. While those clips exist, change them and\n");
        source.Append($"// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named {regenerateWith}) instead of editing this file.\n");
        source.Append("using App2d.Core.Animation;\nusing System.Collections.Immutable;\n");
        source.Append($"using static App2d.Core.Characters.{skeletonClass};\n\nnamespace App2d.Core.Characters;\n\n");
        source.Append($"/// <summary>{summary}</summary>\npublic static class {className}\n{{\n");
        (string Name, string Declaration)[] rules =
        [
            ("Straight", "Interpolator<Vector2f, float> Straight = Interpolation.Default<Vector2f, float>()"),
            ("Smooth", "Interpolator<Vector2f, float> Smooth = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth)"),
            ("SmoothStraightX", "Interpolator<Vector2f, float> SmoothStraightX = Interpolation.Default<Vector2f, float>().WithProgress(Easingf.Smooth).With(Vector2f.Channels.X, Interpolation.Linear<float>())"),
            ("StraightTurn", "Interpolator<float, float> StraightTurn = Interpolation.Linear<float>()"),
            ("SmoothTurn", "Interpolator<float, float> SmoothTurn = Interpolation.Linear<float>().WithProgress(Easingf.Smooth)"),
            ("StraightOrient", "Interpolator<Vector3f, float> StraightOrient = Interpolation.Default<Vector3f, float>()"),
            ("SmoothOrient", "Interpolator<Vector3f, float> SmoothOrient = Interpolation.Default<Vector3f, float>().WithProgress(Easingf.Smooth)"),
        ];
        foreach (var (name, declaration) in rules)
        {
            if (used.Contains(name)) source.Append($"    private static readonly {declaration};\n");
        }
        source.Append(members);
        source.Append('\n').Append(Wrap("public static ImmutableArray<SkeletonClip2D> All { get; } = ", "[", names, 4, ";", "]")).Append("\n}\n");
        return source.ToString();
    }
}
