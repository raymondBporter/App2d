using App2d.Core.Characters;
using System.Numerics;

namespace App2d.CharacterStudio.PlayerMoves;

/// <summary>
/// Builds a Person clip from key poses written the way an animator thinks: feet and hands where they land in actor space
/// (facing +X, feet at y = 0), body turns in radians (positive leans back), and a blade angle for the held prop. Hands are
/// converted to arm-chain targets against the shoulder the body keys actually produce, so a lean never drags a planted
/// hand off its mark. Art tooling only: the output is a plain clip file.
/// </summary>
internal sealed class MoveBuilder(ResolvedModel model, string id, string name, float duration, bool loop)
{
    public const string BackSocket = "back", BackViewSocket = "back-view", SwordSocket = "sword-hand", GunSocket = "gun-hand";
    /// <summary>
    /// View markers: from "view-back" the figure is seen from behind and the sheath and sword use
    /// <see cref="BackViewSocket"/>; from "view-profile" they return to <see cref="BackSocket"/>. The latest marker at or
    /// before the sample time wins; a clip with neither is in profile.
    /// </summary>
    public const string BackViewMarker = "view-back", ProfileViewMarker = "view-profile";

    private readonly Dictionary<(string Kind, string Target), SortedDictionary<float, ClipKey>> _tracks = [];
    private readonly List<(string Chain, float Time, Vector2 Offset, bool Absolute, string Ease)> _hands = [];
    private readonly List<(float Time, float Angle, string Ease)> _blade = [];
    private readonly List<ClipContact> _contacts = [];
    private readonly List<ClipMarker> _markers = [];
    private readonly List<ClipFaceKey> _faces = [];
    private readonly List<ClipKey> _travel = [];
    private readonly List<(string Chain, float Time, int Bend)> _bends = [];

    public ResolvedModel Model => model;
    public float Duration => duration;

    /// <summary>Keys every channel mentioned in <paramref name="pose"/> at one time. Unmentioned channels interpolate through.</summary>
    public MoveBuilder Key(float time, Action<PoseKey> pose, string ease = ClipEase.Smooth)
    {
        if (time < 0 || time > duration + 1e-4f) throw new ArgumentOutOfRangeException(nameof(time), $"{id}: key at {time} is outside 0..{duration}.");
        pose(new PoseKey(this, MathF.Min(time, duration), ease)); return this;
    }

    /// <summary>A foot held on the ground at actor-space X over [start, finish).</summary>
    public MoveBuilder Plant(string leg, float start, float finish, float x)
    {
        var rest = model.Rest[model.Chains[leg].End];
        _contacts.Add(new() { Chain = leg, Start = start, Finish = MathF.Min(finish, duration), Target = new(x - rest.X) }); return this;
    }

    public MoveBuilder Marker(string marker, float time) { _markers.Add(new() { Id = marker, Time = time }); return this; }
    public MoveBuilder Face(float time, string expression) { _faces.Add(new() { Time = time, Expression = expression }); return this; }
    /// <summary>
    /// Which side a limb's joint bends to from <paramref name="time"/> on (a back view mirrors an elbow and knee). The limb
    /// must have a key at that time; place it where the limb is straight so the flip never shows.
    /// </summary>
    public MoveBuilder Bend(string chain, float time, int bend) { _bends.Add((chain, time, bend)); return this; }
    public MoveBuilder Travel(float time, float x, float y = 0) { _travel.Add(new() { Time = time, X = x, Y = y, Ease = ClipEase.Linear }); return this; }

    internal void Set(string kind, string target, float time, ClipKey key)
    {
        if (!_tracks.TryGetValue((kind, target), out var keys)) _tracks[(kind, target)] = keys = [];
        keys[time] = key with { Time = time };
    }
    internal void Hand(string chain, float time, Vector2 offset, bool absolute, string ease) => _hands.Add((chain, time, offset, absolute, ease));
    internal void Blade(float time, float angle, string ease) => _blade.Add((time, angle, ease));

    public MotionClip Build()
    {
        var clip = new MotionClip
        {
            Id = id, Name = name, Model = model.Base.Id, StructureRevision = model.Base.StructureRevision, Duration = duration, Loop = loop,
            Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), Travel = new() { Scale = "leg" },
            Contacts = [.. _contacts], Markers = [.. _markers.OrderBy(m => m.Time)],
        };
        if (_travel.Count > 0) clip.Travel.Keys = [.. _travel.OrderBy(k => k.Time)];
        if (_faces.Count > 0) clip.Faces = [new() { Part = "head", Keys = [.. _faces.OrderBy(f => f.Time)] }];
        void Emit() => clip.Tracks = [.. _tracks.Select(t => new ClipTrack { Kind = t.Key.Kind, Target = t.Key.Target, Keys = [.. t.Value.Values] })];

        // Pass 1: the body alone fixes each shoulder's accumulated frame. The blade angle is world-relative, so the
        // right shoulder's own turn is whatever is left after the chest's.
        Emit();
        foreach (var (time, angle, ease) in _blade)
        {
            var body = PoseEvaluator.Sample(model, clip, time);
            var inherited = body.Angles["right-shoulder"] - Angle(clip, "right-shoulder", time);
            Set(MotionClip.RotateKind, "right-shoulder", time, new() { Angle = angle - inherited, Ease = ease });
        }
        // Pass 2: hands against the shoulders that pass 1 produced.
        Emit();
        foreach (var (chainId, time, offset, absolute, ease) in _hands)
        {
            var chain = model.Chains[chainId]; var pose = PoseEvaluator.Sample(model, clip, time);
            var shoulder = pose.Points[chain.Frame]; var frameAngle = pose.Angles[chain.Frame];
            var goal = absolute ? new Vector3(offset, shoulder.Z) : shoulder + new Vector3(offset, 0);
            var local = PoseEvaluator.RotateXY(goal - shoulder, -frameAngle) - (model.Rest[chain.End] - model.Rest[chain.Frame]);
            Set(MotionClip.TargetKind, chainId, time, new() { X = local.X, Y = local.Y, Ease = ease });
        }
        foreach (var (chain, time, bend) in _bends)
        {
            if (!_tracks.TryGetValue((MotionClip.TargetKind, chain), out var keys) || !keys.TryGetValue(time, out var key))
                throw new InvalidOperationException($"{id}: bend on '{chain}' at {time} needs a key for that limb at that time.");
            keys[time] = key with { Bend = bend };
        }
        Emit();
        clip.Validate(model);
        return clip;
    }

    private static float Angle(MotionClip clip, string target, float time)
    {
        var track = clip.Tracks.FirstOrDefault(t => t.Kind == MotionClip.RotateKind && t.Target == target);
        return track is null ? 0 : PoseEvaluator.Interpolate(track.Keys, time).Angle;
    }
}

/// <summary>One key pose. Positions are actor-space deltas or absolutes as named; angles are radians, positive counter-clockwise.</summary>
internal sealed class PoseKey(MoveBuilder owner, float time, string ease)
{
    private ClipKey K(float x = 0, float y = 0, float z = 0) => new() { X = x, Y = y, Z = z, Ease = ease };
    private ClipKey R(float angle) => new() { Angle = angle, Ease = ease };

    /// <summary>Pelvis offset from rest (hips rest at y = 1) and its turn, which the whole body inherits.</summary>
    public PoseKey Hips(float dx, float dy, float turn = 0)
    {
        owner.Set(MotionClip.TranslateKind, "hips", time, K(dx, dy)); owner.Set(MotionClip.RotateKind, "hips", time, R(turn)); return this;
    }
    /// <summary>
    /// Torso lean about the pelvis. Negative leans forward. The torso box is drawn hips to chest, so the chest point swings
    /// round the pelvis and the chest also turns, carrying the head and shoulders with it.
    /// </summary>
    public PoseKey Chest(float turn, float dx = 0, float dy = 0, float dz = 0)
    {
        var swing = Swing("hips", "chest", turn);
        owner.Set(MotionClip.RotateKind, "chest", time, R(turn)); owner.Set(MotionClip.TranslateKind, "chest", time, K(swing.X + dx, swing.Y + dy, dz)); return this;
    }
    /// <summary>A plain translate of one control from its rest offset (parent-local; Z is depth, + away from the viewer).</summary>
    public PoseKey Move(string control, float dx, float dy, float dz = 0) { owner.Set(MotionClip.TranslateKind, control, time, K(dx, dy, dz)); return this; }
    /// <summary>Neck tilt: the head swings round the chest point (the head is drawn upright, so only its position shows).</summary>
    public PoseKey Head(float turn, float dx = 0, float dy = 0)
    {
        var swing = Swing("chest", "head", turn);
        owner.Set(MotionClip.TranslateKind, "head", time, K(swing.X + dx, swing.Y + dy)); return this;
    }
    private Vector2 Swing(string parent, string child, float turn)
    {
        var offset = owner.Model.Rest[child] - owner.Model.Rest[parent]; var turned = PoseEvaluator.RotateXY(offset, turn);
        return new(turned.X - offset.X, turned.Y - offset.Y);
    }
    /// <summary>A foot at an actor-space position in the locomotion frame (ground is y = 0.025 for a flat sole).</summary>
    public PoseKey LeftFoot(float x, float y) => Foot("left-leg", x, y);
    public PoseKey RightFoot(float x, float y) => Foot("right-leg", x, y);
    private PoseKey Foot(string chain, float x, float y)
    {
        var rest = owner.Model.Rest[owner.Model.Chains[chain].End];
        owner.Set(MotionClip.TargetKind, chain, time, K(x - rest.X, y - rest.Y)); return this;
    }
    /// <summary>A hand relative to its shoulder, in actor axes (unrotated). A relaxed hanging hand is about (0.02, -0.64).</summary>
    public PoseKey LeftHand(float dx, float dy) { owner.Hand("left-arm", time, new(dx, dy), false, ease); return this; }
    public PoseKey RightHand(float dx, float dy) { owner.Hand("right-arm", time, new(dx, dy), false, ease); return this; }
    /// <summary>A hand at an absolute actor-space position.</summary>
    public PoseKey LeftHandAt(float x, float y) { owner.Hand("left-arm", time, new(x, y), true, ease); return this; }
    public PoseKey RightHandAt(float x, float y) { owner.Hand("right-arm", time, new(x, y), true, ease); return this; }
    /// <summary>The direction the right hand's held prop points, in world radians (0 = forward, pi/2 = up).</summary>
    public PoseKey Blade(float angle) { owner.Blade(time, angle, ease); return this; }
}
