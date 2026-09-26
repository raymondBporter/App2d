using System.Numerics;
using App2d.Core.Kinematics;

namespace App2d.Core.Characters;

public sealed record ChainResult(string Chain, float Residual, bool Reached);
public sealed record ContactResult(string Chain, Vector3 Target, float Residual);

/// <summary>
/// Gameplay inputs to evaluation. An expression here wins over the clip's face channel and the model default.
/// <see cref="InPlace"/> drops clip travel: the locomotion frame stays at the origin because the controller owns actor
/// movement. <see cref="Contact"/> may replace an active contact's target (chain, authored target in the locomotion
/// frame) with a held one, such as a world anchor captured at touchdown.
/// </summary>
public readonly record struct PoseInput(string? Expression = null)
{
    public bool InPlace { get; init; }
    public Func<string, Vector3, Vector3>? Contact { get; init; }
}

/// <summary>The final pose: world positions for every control and the residuals that produced them. Drawing, sockets and collision all read this.</summary>
public sealed class EvaluatedPose
{
    public Dictionary<string, Vector3> Points { get; } = new(StringComparer.Ordinal);
    /// <summary>Accumulated XY rotation of each control's frame, inherited by its children.</summary>
    public Dictionary<string, float> Angles { get; } = new(StringComparer.Ordinal);
    /// <summary>The expression each visible face-bearing part shows: gameplay input, then the clip's face channel, then the model default.</summary>
    public Dictionary<string, string> Expressions { get; } = new(StringComparer.Ordinal);
    /// <summary>The locomotion frame's origin at this sample.</summary>
    public Vector3 Locomotion { get; set; }
    public List<ChainResult> Chains { get; } = [];
    public List<ContactResult> Contacts { get; } = [];
    public Vector3 World(string id) => Points[id];
}

/// <summary>Samples channels, builds the hierarchy, then solves IK and contacts. No graphics.</summary>
public static class PoseEvaluator
{
    private static readonly MotionClip Still = new() { Id = "rest", Name = "Rest", Model = "rest" };

    /// <summary>The rest pose: no channels, no travel, no contacts.</summary>
    public static EvaluatedPose Rest(ResolvedModel model, PoseInput input = default) => Sample(model, null, 0, false, input);

    /// <summary>The clip must already have passed <see cref="MotionClip.Validate(ResolvedModel, bool)"/> for this model. A null clip samples rest.</summary>
    public static EvaluatedPose Sample(ResolvedModel model, MotionClip? clip, double seconds, bool repeat = false, PoseInput input = default)
    {
        clip ??= Still;
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        var cycles = repeat && clip.Loop ? Math.Floor(seconds / clip.Duration) : 0;
        var time = (float)(cycles > 0 ? seconds % clip.Duration : Math.Min(seconds, clip.Duration));
        float Ratio(string scale) => scale == CharacterModel.Unit ? 1 : model.Measure(scale) / clip.Reference[scale];

        var travelRatio = Ratio(clip.Travel.Scale);
        Vector2 Travel(float t) { var (value, _) = Interpolate(clip.Travel.Keys, t); return new Vector2(value.X, value.Y) * travelRatio; }
        var cycleTravel = (Travel(clip.Duration) - Travel(0)) * (float)cycles;
        var cycleOrigin = new Vector3(Travel(0) + cycleTravel, 0);
        var pose = new EvaluatedPose { Locomotion = new(Travel(time) + cycleTravel, 0) };
        if (input.InPlace) { cycleOrigin -= pose.Locomotion; pose.Locomotion = Vector3.Zero; }

        var tracks = clip.Tracks.ToDictionary(t => (t.Kind, t.Target));
        Vector3 Delta(string kind, string target, string defaultScale)
        {
            if (!tracks.TryGetValue((kind, target), out var track)) return default;
            var (value, _) = Interpolate(track.Keys, time); var ratio = Ratio(track.Scale ?? defaultScale);
            return new(value.X * ratio, value.Y * ratio, value.Z);
        }
        int Bend(ModelChain chain) => tracks.TryGetValue((MotionClip.TargetKind, chain.Id), out var track)
            && track.Keys.LastOrDefault(k => k.Bend is not null && k.Time <= time) is { Bend: { } bend } ? bend : chain.Bend;
        float Angle(string target) => tracks.TryGetValue((MotionClip.RotateKind, target), out var track) ? Interpolate(track.Keys, time).Angle : 0;

        var angles = pose.Angles;
        foreach (var control in model.Order)
        {
            var parentPoint = control.Parent is null ? pose.Locomotion : pose.Points[control.Parent];
            var parentAngle = control.Parent is null ? 0 : angles[control.Parent];
            var parentRest = control.Parent is null ? Vector3.Zero : model.Rest[control.Parent];
            var offset = model.Rest[control.Id] - parentRest + Delta(MotionClip.TranslateKind, control.Id, control.Scale);
            pose.Points[control.Id] = parentPoint + RotateXY(offset, parentAngle);
            angles[control.Id] = parentAngle + Angle(control.Id);
        }
        foreach (var chain in model.Base.Chains)
        {
            var locomotion = chain.Frame == CharacterModel.Locomotion;
            var framePoint = locomotion ? pose.Locomotion : pose.Points[chain.Frame];
            var frameAngle = locomotion ? 0 : angles[chain.Frame];
            var frameRest = locomotion ? Vector3.Zero : model.Rest[chain.Frame];
            var target = framePoint + RotateXY(model.Rest[chain.End] - frameRest + Delta(MotionClip.TargetKind, chain.Id, chain.Scale), frameAngle);
            pose.Chains.Add(Solve(model, pose, chain, target, Bend(chain)));
        }
        foreach (var contact in clip.Contacts)
        {
            if (!(time >= contact.Start && (time < contact.Finish || time == clip.Duration && contact.Finish == clip.Duration))) continue;
            var chain = model.Chains[contact.Chain]; var ratio = Ratio(chain.Scale);
            var target = cycleOrigin + model.Rest[chain.End] + new Vector3(contact.Target.X * ratio, contact.Target.Y * ratio, contact.Target.Z);
            if (input.Contact is { } hold) target = hold(chain.Id, target);
            var result = Solve(model, pose, chain, target, Bend(chain));
            pose.Chains[pose.Chains.FindIndex(c => c.Chain == chain.Id)] = result;
            pose.Contacts.Add(new(chain.Id, target, result.Residual));
        }
        foreach (var part in model.Parts)
        {
            if (part.Hidden || part.Face == "none") continue;
            pose.Expressions[part.Id] = input.Expression ?? FaceAt(clip, part.Id, time) ?? part.Face;
        }
        return pose;
    }

    /// <summary>The locomotion frame's travel over one cycle on this model: the stride gameplay matches against ground distance.</summary>
    public static Vector2 CycleTravel(ResolvedModel model, MotionClip clip)
    {
        var keys = clip.Travel.Keys; if (keys.Count == 0) return Vector2.Zero;
        var ratio = clip.Travel.Scale == CharacterModel.Unit ? 1 : model.Measure(clip.Travel.Scale) / clip.Reference[clip.Travel.Scale];
        var (end, _) = Interpolate(keys, clip.Duration); var (start, _) = Interpolate(keys, 0);
        return new Vector2(end.X - start.X, end.Y - start.Y) * ratio;
    }

    private static ChainResult Solve(ResolvedModel model, EvaluatedPose pose, ModelChain chain, Vector3 target, int bend)
    {
        var root = pose.Points[chain.Root];
        var solved = TwoBoneIk2D.Solve(new(root.X, root.Y), new(target.X, target.Y), model.Length(chain.Root, chain.Joint), model.Length(chain.Joint, chain.End), bend);
        var joint = new Vector3(solved.Joint, pose.Points[chain.Joint].Z);
        var end = new Vector3(solved.End, target.Z);
        MoveDescendants(model, pose, chain.Joint, joint - pose.Points[chain.Joint], chain.End);
        MoveDescendants(model, pose, chain.End, end - pose.Points[chain.End], null);
        pose.Points[chain.Joint] = joint; pose.Points[chain.End] = end;
        return new(chain.Id, Vector2.Distance(solved.End, new(target.X, target.Y)), solved.ReachesTarget);
    }

    private static void MoveDescendants(ResolvedModel model, EvaluatedPose pose, string id, Vector3 delta, string? except)
    {
        foreach (var child in model.Children[id])
        {
            if (child == except) continue;
            pose.Points[child] += delta; MoveDescendants(model, pose, child, delta, null);
        }
    }

    /// <summary>The clip's expression for a part at a time: the last key at or before it, else the first key. Null without a track.</summary>
    public static string? FaceAt(MotionClip clip, string part, float time)
    {
        var track = clip.Faces.FirstOrDefault(f => f.Part == part);
        if (track is null || track.Keys.Count == 0) return null;
        return (track.Keys.LastOrDefault(k => k.Time <= time) ?? track.Keys[0]).Expression;
    }

    /// <summary>Eased interpolation, holding the first and last keys outside their range. No keys means a zero delta.</summary>
    public static (Vector3 Value, float Angle) Interpolate(List<ClipKey> keys, float time)
    {
        if (keys.Count == 0) return default;
        static (Vector3, float) Of(ClipKey k) => (new(k.X, k.Y, k.Z), k.Angle);
        if (time <= keys[0].Time) return Of(keys[0]);
        for (var i = 1; i < keys.Count; i++)
        {
            if (time > keys[i].Time) continue;
            if (time == keys[i].Time) return Of(keys[i]);
            var a = keys[i - 1]; var b = keys[i]; var u = ClipEase.Apply(a.Ease, (time - a.Time) / (b.Time - a.Time));
            return (Vector3.Lerp(new(a.X, a.Y, a.Z), new(b.X, b.Y, b.Z), u), a.Angle + (b.Angle - a.Angle) * u);
        }
        return Of(keys[^1]);
    }

    public static Vector3 RotateXY(Vector3 v, float angle)
    {
        if (angle == 0) return v;
        var (sin, cos) = MathF.SinCos(angle);
        return new(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos, v.Z);
    }
}
