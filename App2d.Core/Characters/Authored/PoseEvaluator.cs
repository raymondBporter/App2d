using App2d.Core.Animation;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

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
    /// <summary>A masked override: its clip owns every channel whose target is in the mask. The base clip keeps the rest.</summary>
    public PoseLayer? Overlay { get; init; }
    /// <summary>Reverse authored horizontal offsets and rotation, without turning the actor's rest pose.</summary>
    public bool ReverseHorizontalMotion { get; init; }
    public string? Skin { get; init; }
}

/// <summary>
/// One override clip over the base, such as a gun shot's arms on any legs. <see cref="Targets"/> names the controls and chains
/// the overlay owns, whole: a masked channel without an overlay track rests rather than falling back to the base. Its travel
/// and contacts are ignored; the base owns locomotion. <see cref="Weight"/> fades the layer: masked channels blend from the
/// base's value toward the overlay's before the hierarchy and IK are solved, so a chain is never blended as two solved
/// poses. A bend choice and the overlay's face win from half weight; a base contact on a masked chain eases out with it.
/// </summary>
public sealed record PoseLayer(MotionClip Clip, double Seconds, IReadOnlySet<string> Targets, float Weight = 1);

/// <summary>
/// The final pose: one evaluated frame per control in the character frame, with the residuals that produced it. Drawing,
/// sockets and collision all read this. <see cref="Points"/>, <see cref="Angles"/> and <see cref="Bones"/> are views of the
/// same placement, written together by <see cref="Place"/> so no consumer sees a position that disagrees with its frame.
/// </summary>
public sealed class EvaluatedPose
{
    private readonly Dictionary<string, Vector3> _points = new(StringComparer.Ordinal);
    private readonly Dictionary<string, float> _angles = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Matrix3x2> _bones = new(StringComparer.Ordinal);

    /// <summary>Each control's origin in the character frame; Z is its drawing layer.</summary>
    public IReadOnlyDictionary<string, Vector3> Points => _points;
    /// <summary>Accumulated XY rotation of each control's frame, unwrapped so full turns stay meaningful; inherited by its children.</summary>
    public IReadOnlyDictionary<string, float> Angles => _angles;
    /// <summary>Each control's frame in the character frame (row-vector convention: <c>nodeInCharacter = nodeLocal * parentInCharacter</c>).</summary>
    public IReadOnlyDictionary<string, Matrix3x2> Bones => _bones;
    public List<EvaluatedSlot2D> Slots { get; } = [];
    public Dictionary<string, Vector3> SocketAngles { get; } = new(StringComparer.Ordinal);
    /// <summary>The expression each visible face-bearing part shows: gameplay input, then the clip's face channel, then the model default.</summary>
    public Dictionary<string, string> Expressions { get; } = new(StringComparer.Ordinal);
    /// <summary>The locomotion frame's origin at this sample.</summary>
    public Vector3 Locomotion { get; set; }
    public List<ChainResult> Chains { get; } = [];
    public List<ContactResult> Contacts { get; } = [];
    public Vector3 World(string id) => _points[id];

    /// <summary>A control's evaluated placement: its bone frame at its drawing layer. Artwork, sockets and handles attach to this.</summary>
    public LayeredFrame2D Placement(string id) => new(new Frame2D(_bones[id]), _points[id].Z);

    /// <summary>Writes a control's frame, layer and accumulated angle together; the origin is read from the frame.</summary>
    internal void Place(string id, Matrix3x2 frame, float layer, float angle)
    {
        _bones[id] = frame;
        _points[id] = new(frame.M31, frame.M32, layer);
        _angles[id] = angle;
    }

    internal void Place(string id, LayeredFrame2D placement, float angle) => Place(id, placement.Frame.Matrix, placement.Layer, angle);

    /// <summary>Moves a control's origin without turning its frame: the legacy point-constraint adapter's write.</summary>
    internal void Translate(string id, Vector3 origin)
    {
        var frame = _bones[id];
        frame.M31 = origin.X;
        frame.M32 = origin.Y;
        _bones[id] = frame;
        _points[id] = origin;
    }
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
        ArgGuard.NonNegative(seconds);
        var cycles = repeat && clip.Loop ? Math.Floor(seconds / clip.Duration) : 0;
        var time = (float)(cycles > 0 ? seconds % clip.Duration : Math.Min(seconds, clip.Duration));
        float Ratio(string scale) => scale == CharacterModel.Unit ? 1 : model.Measure(scale) / clip.Reference[scale];

        var travelRatio = Ratio(clip.Travel.Scale);
        Vector2 Travel(float t)
        {
            var (value, _) = Interpolate(clip.Travel.Keys, t);
            return new Vector2(value.X, value.Y) * travelRatio;
        }
        var cycleTravel = (Travel(clip.Duration) - Travel(0)) * (float)cycles;
        var cycleOrigin = new Vector3(Travel(0) + cycleTravel, 0);
        var pose = new EvaluatedPose { Locomotion = new(Travel(time) + cycleTravel, 0) };
        if (input.InPlace)
        {
            cycleOrigin -= pose.Locomotion;
            pose.Locomotion = Vector3.Zero;
        }

        var tracks = clip.Tracks.ToDictionary(t => (t.Kind, t.Target));
        var overlay = input.Overlay is { Weight: > 0 } layer ? layer : null;
        var weight = overlay is null ? 0 : MathF.Min(overlay.Weight, 1);
        var overlayTracks = overlay?.Clip.Tracks.ToDictionary(t => (t.Kind, t.Target));
        var overlayTime = overlay is null ? 0 : (float)(overlay.Clip.Loop ? overlay.Seconds % overlay.Clip.Duration : Math.Min(overlay.Seconds, overlay.Clip.Duration));
        bool Masked(string target) => overlay?.Targets.Contains(target) == true;
        // A channel read from one layer: its track, the time to read it at and the clip whose reference units it uses.
        (ClipTrack? Track, float Time, MotionClip Clip) Read(bool fromOverlay, string kind, string target) => fromOverlay
            ? (overlayTracks!.GetValueOrDefault((kind, target)), overlayTime, overlay!.Clip)
            : (tracks.GetValueOrDefault((kind, target)), time, clip);
        Vector3 LayerDelta(bool fromOverlay, string kind, string target, string defaultScale)
        {
            var (track, at, source) = Read(fromOverlay, kind, target);
            if (track is null)
            {
                return default;
            }

            var (value, _) = Interpolate(track, at);
            var scale = track.Scale ?? defaultScale;
            var ratio = scale == CharacterModel.Unit ? 1 : model.Measure(scale) / source.Reference[scale];
            var delta = new Vector3(value.X * ratio, value.Y * ratio, value.Z);
            if (!input.ReverseHorizontalMotion || kind == MotionClip.ScaleKind)
            {
                return delta;
            }

            return new(-delta.X, kind == MotionClip.ShearKind ? -delta.Y : delta.Y, delta.Z);
        }
        Vector3 Delta(string kind, string target, string defaultScale) => !Masked(target) ? LayerDelta(false, kind, target, defaultScale)
            : weight >= 1 ? LayerDelta(true, kind, target, defaultScale)
            : Vector3.Lerp(LayerDelta(false, kind, target, defaultScale), LayerDelta(true, kind, target, defaultScale), weight);
        int LayerBend(bool fromOverlay, ModelChain chain) => Read(fromOverlay, MotionClip.TargetKind, chain.Id) is { Track: { } track, Time: var at }
            && track.Keys.LastOrDefault(k => k.Bend is not null && k.Time <= at) is { Bend: { } bend } ? bend : chain.Bend;
        int Bend(ModelChain chain) => LayerBend(Masked(chain.Id) && weight >= .5f, chain);
        float LayerAngle(bool fromOverlay, string target) => Read(fromOverlay, MotionClip.RotateKind, target) is { Track: { } track, Time: var at }
            ? Interpolate(track, at).Angle * (input.ReverseHorizontalMotion ? -1 : 1) : 0;
        float Angle(string target) => !Masked(target) ? LayerAngle(false, target) : LayerAngle(false, target) + (LayerAngle(true, target) - LayerAngle(false, target)) * weight;

        var locomotion = LayeredFrame2D.FromMatrix(Matrix3x2.CreateTranslation(pose.Locomotion.X, pose.Locomotion.Y));
        foreach (var control in model.Order)
        {
            // One frame graph: every node is a local similarity composed onto its carrier, frames multiply and layers
            // add. The authored rest Z is already split into per-node layer offsets and a translate-Z key is a further
            // local offset, so an animated parent layer carries its children without double counting.
            var node = model.Nodes[control.Id];
            var translate = Delta(MotionClip.TranslateKind, control.Id, control.Scale);
            var rotate = Angle(control.Id);
            var localLayer = node.Layer + translate.Z;
            var parent = node.Parent is null ? null : model.Nodes[node.Parent];
            var parentAngle = parent is null ? 0 : pose.Angles[parent.Id];
            // The legacy affine adapter carries its point children by rotation only; its scale and shear stay on its own frame.
            var carrier = parent is null ? locomotion
                : parent.Kind == RigNodeKinds.Affine ? LayeredFrame2D.FromRotation(parentAngle, pose.Points[parent.Id])
                : pose.Placement(parent.Id);
            if (node.Kind == RigNodeKinds.Affine)
            {
                LegacyAffineRig.Evaluate(pose, node, translate, rotate, Delta(MotionClip.ScaleKind, control.Id, CharacterModel.Unit), Delta(MotionClip.ShearKind, control.Id, CharacterModel.Unit), localLayer,
                    parent is null ? locomotion : pose.Placement(parent.Id));
                continue;
            }
            var origin = node.Origin(parent?.Length ?? 0) + translate.XY;
            var rotation = node.Rotation + rotate;
            // The uniform pose scale channel (ChannelSemantics.UniformPoseScale): one multiplier on the node's similarity,
            // carried by its children and their link positions; the bone's length stays a dimension.
            var scale = node.ScaleVector * (1 + Delta(MotionClip.ScaleKind, control.Id, CharacterModel.Unit).X);
            if (node.CharacterOriented)
            {
                // The origin follows the parent; the axes stay the character's, so the parent's turn and scale are not inherited.
                var at = carrier.At(new Vector3(origin, localLayer));
                pose.Place(control.Id, LayeredFrame2D.Local(scale, rotation, at.XY, at.Z), rotation);
                continue;
            }
            // The angle accumulates rather than being read back from the matrix, so full turns stay unwrapped; a reflected
            // parent reverses the sense of its children's turns.
            pose.Place(control.Id, LayeredFrame2D.Local(scale, rotation, origin, localLayer).Then(carrier), parentAngle + (parent?.Handedness ?? 1) * rotation);
        }
        var constraints = new RigConstraintEvaluator(model, pose);
        foreach (var chain in model.IkConstraints)
        {
            var target = RigConstraintEvaluator.TargetPosition(model, pose, chain, Delta(MotionClip.TargetKind, chain.Id, chain.Scale));
            constraints.Apply(new RigIkTarget(chain.Id, target, Bend(chain)));
        }
        foreach (var contact in clip.Contacts)
        {
            var masked = Masked(contact.Chain);
            if (masked && weight >= 1)
            {
                continue;
            }

            if (!(time >= contact.Start && (time < contact.Finish || time == clip.Duration && contact.Finish == clip.Duration)))
            {
                continue;
            }

            var chain = model.Chains[contact.Chain];
            var ratio = Ratio(chain.Scale);
            var target = cycleOrigin + model.Rest[chain.End] + new Vector3(contact.Target.X * ratio * (input.ReverseHorizontalMotion ? -1 : 1), contact.Target.Y * ratio, contact.Target.Z);
            // A masked chain fading in leaves its base contact gradually; it is neither held nor reported as planted.
            if (masked)
            {
                constraints.Apply(new RigContactPin(chain.Id, target, weight));
                continue;
            }
            if (input.Contact is { } hold)
            {
                target = hold(chain.Id, target);
            }

            constraints.Apply(new RigContactPin(chain.Id, target));
        }
        foreach (var socket in model.Base.Sockets)
        {
            Vector3 ReadOrientation(bool fromOverlay) => Read(fromOverlay, MotionClip.OrientKind, socket.Id) is { Track: { } t, Time: var at }
                ? ReverseOrientation(Interpolate(t.Keys, at).Value) : Vector3.Zero;
            Vector3 ReverseOrientation(Vector3 value) => input.ReverseHorizontalMotion ? new(value.X, -value.Y, -value.Z) : value;
            // Socket channels follow ownership of their frame, including an IK chain that owns their attachment end.
            var owned = Masked(socket.Id) || Masked(socket.Frame ?? socket.Control) || Masked(socket.Control)
                || model.Chains.Values.Any(c => c.End == socket.Control && Masked(c.Id));
            pose.SocketAngles[socket.Id] = owned ? Vector3.Lerp(ReadOrientation(false), ReadOrientation(true), weight) : ReadOrientation(false);
        }
        SkeletonAppearance2D.Evaluate(model, clip, time, input.Skin, pose);
        var overlayFace = overlay is not null && weight >= .5f;
        foreach (var part in model.Parts)
        {
            if (part.Hidden || part.Face == "none")
            {
                continue;
            }

            pose.Expressions[part.Id] = input.Expression ?? (overlayFace ? FaceAt(overlay!.Clip, part.Id, overlayTime) : null) ?? FaceAt(clip, part.Id, time) ?? part.Face;
        }
        return pose;
    }

    /// <summary>The locomotion frame's travel over one cycle on this model: the stride gameplay matches against ground distance.</summary>
    public static Vector2 CycleTravel(ResolvedModel model, MotionClip clip)
    {
        var keys = clip.Travel.Keys;
        if (keys.Count == 0)
        {
            return Vector2.Zero;
        }

        var ratio = clip.Travel.Scale == CharacterModel.Unit ? 1 : model.Measure(clip.Travel.Scale) / clip.Reference[clip.Travel.Scale];
        var (end, _) = Interpolate(keys, clip.Duration);
        var (start, _) = Interpolate(keys, 0);
        return new Vector2(end.X - start.X, end.Y - start.Y) * ratio;
    }

    /// <summary>The clip's expression for a part at a time: the last key at or before it, else the first key. Null without a track.</summary>
    public static string? FaceAt(MotionClip clip, string part, float time)
    {
        var track = clip.Faces.FirstOrDefault(f => f.Part == part);
        if (track is null || track.Keys.Count == 0)
        {
            return null;
        }

        return (track.Keys.LastOrDefault(k => k.Time <= time) ?? track.Keys[0]).Expression;
    }

    /// <summary>Eased interpolation, holding the first and last keys outside their range. No keys means a zero delta.</summary>
    public static (Vector3 Value, float Angle) Interpolate(List<ClipKey> keys, float time, bool rotate = false) =>
        KeyframeSampling.Sample(keys, time, (Value: Vector3.Zero, Angle: 0f), new MotionInterpolation(rotate));

    private readonly struct MotionInterpolation(bool rotate) : IKeyframeInterpolation<ClipKey, (Vector3 Value, float Angle)>
    {
        public (Vector3 Value, float Angle) GetValue(ClipKey key) => (key.Value, key.Angle);

        public (Vector3 Value, float Angle) Interpolate(ClipKey a, ClipKey b, float phase)
        {
            var sample = new ClipEase.Sampler(phase, a.Ease);
            return (new(sample.Interpolate(a.Value.X, b.Value.X, rotate ? null : a.Curve),
                sample.Interpolate(a.Value.Y, b.Value.Y, rotate ? null : a.CurveY ?? (a.Curve?.Absolute == true ? null : a.Curve)),
                sample.Interpolate(a.Value.Z, b.Value.Z, rotate ? null : a.CurveZ ?? (a.Curve?.Absolute == true ? null : a.Curve))),
                sample.Interpolate(a.Angle, b.Angle, rotate ? a.Curve : null));
        }
    }

    public static (Vector3 Value, float Angle) Interpolate(ClipTrack track, float time) =>
        track.SetupBeforeFirst && track.Keys.Count > 0 && time < track.Keys[0].Time ? default : Interpolate(track.Keys, time, track.Kind == MotionClip.RotateKind);
}
