using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>One animated value: a control's translate or rotate channel, or a chain's end-target channel.</summary>
public readonly record struct Channel(string Kind, string Target)
{
    public override string ToString() => $"{Target} {Kind}";
}

/// <summary>
/// Graphics-free clip edits. Pose edits are the inverse of <see cref="PoseEvaluator"/>: a desired world position becomes a
/// delta in the channel's frame and reference units, so the same key reads correctly on every build.
/// </summary>
public static class ClipAuthoring
{
    /// <summary>Keys closer than this are the same key.</summary>
    public const float SameTime = 1e-4f;

    /// <summary>
    /// An empty clip authored against <paramref name="preview"/>'s measures. Travel scales like the model's planted limbs, when
    /// its locomotion-frame chains agree on a scale, so contacts can be added without changing it.
    /// </summary>
    public static MotionClip New(ResolvedModel preview, string id, string name, float duration = 1, bool loop = true)
    {
        var scales = preview.Chains.Values.Where(c => c.Frame == CharacterModel.Locomotion).Select(c => c.Scale).Distinct().ToArray();
        return new()
        {
            Id = id, Name = name, Model = preview.Base.Id, StructureRevision = preview.Base.StructureRevision, Duration = duration, Loop = loop,
            Reference = preview.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal),
            Travel = new() { Scale = scales.Length == 1 ? scales[0] : CharacterModel.Unit },
        };
    }

    public static MotionClip Duplicate(MotionClip clip, string id, string name)
    {
        var copy = MotionClip.FromJson(clip.ToJson()); copy.Id = id; copy.Name = name; return copy;
    }

    /// <summary>The channel a control is posed through: its chain's target for an IK end, none for an IK joint, else its translation.</summary>
    public static Channel? ChannelFor(ResolvedModel model, string control)
    {
        foreach (var chain in model.Chains.Values)
        {
            if (chain.End == control) return new(MotionClip.TargetKind, chain.Id);
            if (chain.Joint == control) return null;
        }
        return new(MotionClip.TranslateKind, control);
    }

    public static ClipTrack? Track(MotionClip clip, Channel channel) => clip.Tracks.FirstOrDefault(t => t.Kind == channel.Kind && t.Target == channel.Target);

    public static (Vector3 Value, float Angle) Value(MotionClip clip, Channel channel, float time) =>
        Track(clip, channel) is { } track ? PoseEvaluator.Interpolate(track.Keys, time) : default;

    /// <summary>Writes a key at <paramref name="time"/>, replacing a key already there and keeping its easing.</summary>
    public static void SetKey(MotionClip clip, Channel channel, float time, Vector3 value, float angle = 0)
    {
        var track = Track(clip, channel);
        if (track is null) clip.Tracks.Add(track = new() { Kind = channel.Kind, Target = channel.Target });
        var rotate = channel.Kind == MotionClip.RotateKind;
        var key = new ClipKey { Time = time, X = rotate ? 0 : value.X, Y = rotate ? 0 : value.Y, Z = rotate ? 0 : value.Z, Angle = rotate ? angle : 0 };
        Put(track.Keys, key, k => k.Time);
    }

    /// <summary>
    /// Poses <paramref name="control"/> at a world position at <paramref name="time"/>. <paramref name="pose"/> is the clip
    /// sampled at that time without repetition. An active contact moves its planted target instead of a key.
    /// Returns the channel keyed, or null when a contact target moved.
    /// </summary>
    public static Channel? Pose(ResolvedModel model, MotionClip clip, EvaluatedPose pose, float time, string control, Vector3 world)
    {
        var channel = ChannelFor(model, control) ?? throw new InvalidOperationException(
            $"'{control}' is solved by IK; drag the chain's end or flip its bend.");
        if (channel.Kind == MotionClip.TargetKind)
        {
            var chain = model.Chains[channel.Target];
            if (ActiveContact(clip, chain.Id, time) is { } contact)
            {
                var origin = TravelAt(model, clip, 0); var ratio = Ratio(model, clip, chain.Scale);
                var local = world - origin - model.Rest[chain.End];
                contact.Target = new(local.X / ratio, local.Y / ratio, local.Z); return null;
            }
            var (framePoint, frameAngle, frameRest) = Frame(model, pose, chain.Frame);
            SetKey(clip, channel, time, Delta(world, framePoint, frameAngle, model.Rest[chain.End] - frameRest, Ratio(model, clip, Track(clip, channel)?.Scale ?? chain.Scale)));
            return channel;
        }
        var spec = model.Controls[control];
        var (parentPoint, parentAngle, parentRest) = Frame(model, pose, spec.Parent);
        SetKey(clip, channel, time, Delta(world, parentPoint, parentAngle, model.Rest[control] - parentRest, Ratio(model, clip, Track(clip, channel)?.Scale ?? spec.Scale)));
        return channel;
    }

    /// <summary>Adds <paramref name="radians"/> to a control's rotation at a time.</summary>
    public static void Rotate(MotionClip clip, string control, float time, float radians)
    {
        var channel = new Channel(MotionClip.RotateKind, control);
        SetKey(clip, channel, time, default, Value(clip, channel, time).Angle + radians);
    }

    /// <summary>The contact holding a chain at a time under the runtime's half-open rule.</summary>
    public static ClipContact? ActiveContact(MotionClip clip, string chain, float time) =>
        clip.Contacts.FirstOrDefault(c => c.Chain == chain && time >= c.Start && (time < c.Finish || time == clip.Duration && c.Finish == clip.Duration));

    /// <summary>Plants a chain's end where it is now, from <paramref name="time"/> for up to <paramref name="length"/> seconds, stopping short of the next contact.</summary>
    public static ClipContact Plant(ResolvedModel model, MotionClip clip, EvaluatedPose pose, string chain, float time, float length = .3f)
    {
        var spec = model.Chains[chain];
        if (ActiveContact(clip, chain, time) is not null) throw new InvalidOperationException($"'{chain}' is already planted at {time:F3}s.");
        if (spec.Frame != CharacterModel.Locomotion) throw new InvalidOperationException($"'{chain}' is keyed in '{spec.Frame}'; only chains in the locomotion frame can plant.");
        if (spec.Scale != clip.Travel.Scale) throw new InvalidOperationException($"'{chain}' scales with '{spec.Scale}' but travel scales with '{clip.Travel.Scale}'; contacts need them to match.");
        var next = clip.Contacts.Where(c => c.Chain == chain && c.Start > time).Select(c => c.Start).DefaultIfEmpty(clip.Duration).Min();
        var finish = MathF.Min(next, MathF.Min(clip.Duration, time + length));
        if (finish - time < SameTime) throw new InvalidOperationException($"No room to plant '{chain}' at {time:F3}s.");
        var ratio = Ratio(model, clip, spec.Scale); var local = pose.World(spec.End) - TravelAt(model, clip, 0) - model.Rest[spec.End];
        var contact = new ClipContact { Chain = chain, Start = time, Finish = finish, Target = new(local.X / ratio, local.Y / ratio, local.Z) };
        clip.Contacts.Add(contact); clip.Contacts.Sort((a, b) => (a.Chain, a.Start).CompareTo((b.Chain, b.Start))); return contact;
    }

    /// <summary>Every time at which any channel, or travel, has a key.</summary>
    public static IReadOnlyList<float> KeyTimes(MotionClip clip, IReadOnlyCollection<Channel>? channels = null)
    {
        var times = Tracks(clip, channels).SelectMany(t => t.Keys).Select(k => k.Time);
        if (channels is null) times = times.Concat(clip.Travel.Keys.Select(k => k.Time));
        var sorted = times.Order().ToList(); var distinct = new List<float>();
        foreach (var time in sorted) if (distinct.Count == 0 || time - distinct[^1] > SameTime) distinct.Add(time);
        return distinct;
    }

    /// <summary>Pins the current pose at <paramref name="time"/>: every existing channel, and travel, gets a key holding its present value.</summary>
    public static void KeyPose(MotionClip clip, float time)
    {
        foreach (var track in clip.Tracks)
        {
            var (value, angle) = PoseEvaluator.Interpolate(track.Keys, time);
            SetKey(clip, new(track.Kind, track.Target), time, value, angle);
        }
        if (clip.Travel.Keys.Count > 0)
        {
            var (value, _) = PoseEvaluator.Interpolate(clip.Travel.Keys, time);
            Put(clip.Travel.Keys, new ClipKey { Time = time, X = value.X, Y = value.Y }, k => k.Time);
        }
    }

    /// <summary>Moves (or with <paramref name="copy"/>, copies) the keys at one time to another. Null channels means every channel and travel.</summary>
    public static void MoveKeys(MotionClip clip, float from, float to, IReadOnlyCollection<Channel>? channels = null, bool copy = false)
    {
        to = Math.Clamp(to, 0, clip.Duration);
        if (MathF.Abs(from - to) < SameTime) return;
        void Move(List<ClipKey> keys)
        {
            var key = keys.FirstOrDefault(k => MathF.Abs(k.Time - from) < SameTime);
            if (key is null) return;
            if (!copy) keys.Remove(key);
            Put(keys, key with { Time = to }, k => k.Time);
        }
        foreach (var track in Tracks(clip, channels)) Move(track.Keys);
        if (channels is null) Move(clip.Travel.Keys);
    }

    public static void DeleteKeys(MotionClip clip, float time, IReadOnlyCollection<Channel>? channels = null)
    {
        foreach (var track in Tracks(clip, channels)) track.Keys.RemoveAll(k => MathF.Abs(k.Time - time) < SameTime);
        if (channels is null) clip.Travel.Keys.RemoveAll(k => MathF.Abs(k.Time - time) < SameTime);
        clip.Tracks.RemoveAll(t => t.Keys.Count == 0);
    }

    public static void SetEase(MotionClip clip, float time, string ease, IReadOnlyCollection<Channel>? channels = null)
    {
        EntityVocabulary.Require(ease, ClipEase.All, "ease");
        foreach (var key in Tracks(clip, channels).SelectMany(t => t.Keys).Where(k => MathF.Abs(k.Time - time) < SameTime)) key.Ease = ease;
    }

    /// <summary>Changes the duration, scaling every key, contact, marker and face time with it.</summary>
    public static void Retime(MotionClip clip, float duration)
    {
        new Limit(.05f, 60).Check(duration, "duration");
        var ratio = duration / clip.Duration;
        float Scale(float time) => MathF.Min(duration, time * ratio);
        foreach (var key in clip.Tracks.SelectMany(t => t.Keys).Concat(clip.Travel.Keys)) key.Time = Scale(key.Time);
        foreach (var contact in clip.Contacts) { contact.Start = Scale(contact.Start); contact.Finish = Scale(contact.Finish); }
        foreach (var marker in clip.Markers) marker.Time = Scale(marker.Time);
        foreach (var key in clip.Faces.SelectMany(f => f.Keys)) key.Time = Scale(key.Time);
        clip.Duration = duration;
    }

    public static void SetMarker(MotionClip clip, string id, float time)
    {
        AuthoredAsset.RequireId(id, "marker id");
        var marker = clip.Markers.FirstOrDefault(m => m.Id == id);
        if (marker is null) clip.Markers.Add(marker = new() { Id = id });
        marker.Time = Math.Clamp(time, 0, clip.Duration); clip.Markers.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>Holds <paramref name="expression"/> on a part from <paramref name="time"/>; null removes the key there.</summary>
    public static void SetFace(MotionClip clip, string part, float time, string? expression)
    {
        var track = clip.Faces.FirstOrDefault(f => f.Part == part);
        if (expression is null)
        {
            track?.Keys.RemoveAll(k => MathF.Abs(k.Time - time) < SameTime);
            clip.Faces.RemoveAll(f => f.Keys.Count == 0); return;
        }
        if (!FaceExpressions.Contains(expression)) throw new InvalidDataException($"Unknown expression '{expression}'.");
        if (track is null) clip.Faces.Add(track = new() { Part = part });
        Put(track.Keys, new ClipFaceKey { Time = time, Expression = expression }, k => k.Time);
    }

    private static IEnumerable<ClipTrack> Tracks(MotionClip clip, IReadOnlyCollection<Channel>? channels) =>
        channels is null ? clip.Tracks : clip.Tracks.Where(t => channels.Contains(new(t.Kind, t.Target)));

    private static void Put<T>(List<T> keys, T key, Func<T, float> time)
    {
        var index = keys.FindIndex(k => MathF.Abs(time(k) - time(key)) < SameTime);
        if (index >= 0) { keys[index] = key; return; }
        index = keys.FindIndex(k => time(k) > time(key));
        keys.Insert(index < 0 ? keys.Count : index, key);
    }

    private static (Vector3 Point, float Angle, Vector3 Origin) Frame(ResolvedModel model, EvaluatedPose pose, string? frame) =>
        frame is null || frame == CharacterModel.Locomotion ? (pose.Locomotion, 0, Vector3.Zero) : (pose.Points[frame], pose.Angles[frame], model.Rest[frame]);

    private static Vector3 Delta(Vector3 world, Vector3 framePoint, float frameAngle, Vector3 restOffset, float ratio)
    {
        var local = PoseEvaluator.RotateXY(world - framePoint, -frameAngle) - restOffset;
        return new(local.X / ratio, local.Y / ratio, local.Z);
    }

    /// <summary>The locomotion frame's origin at a clip time, as the evaluator places it for the first cycle.</summary>
    private static Vector3 TravelAt(ResolvedModel model, MotionClip clip, float time)
    {
        var value = PoseEvaluator.Interpolate(clip.Travel.Keys, time).Value * Ratio(model, clip, clip.Travel.Scale);
        return new(value.X, value.Y, 0);
    }

    /// <summary>Variant measure over reference measure. A measure the clip has no reference for is recorded now, against this preview.</summary>
    private static float Ratio(ResolvedModel model, MotionClip clip, string scale)
    {
        if (scale == CharacterModel.Unit) return 1;
        if (!clip.Reference.TryGetValue(scale, out var reference)) clip.Reference[scale] = reference = model.Measure(scale);
        return model.Measure(scale) / reference;
    }
}
