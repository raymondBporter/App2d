using App2d.Core.Animation;
using App2d.Core.Characters.Geometry;
using App2d.Core.Constraints;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Validation;
using App2d.Core.Timing;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One constraint's outcome. <see cref="Reached"/> is residual-based (P4b): the end lies within <see cref="ReachTolerance"/> of
/// the target; a solve that clamped to full reach, a minimum fold or a joint limit reports the gap it left instead of claiming
/// the target. <see cref="Limited"/> says a joint limit shaped the pose.
/// </summary>
public sealed record ChainResult(string Chain, float Residual, bool Reached, bool Limited = false)
{
    /// <summary>The position tolerance of the reference set: authored chains reach to ~3e-8, so anything above this is a real gap.</summary>
    public const float ReachTolerance = 1e-4f;

    public static ChainResult Of(string chain, float residual, bool limited = false) => new(chain, residual, residual <= ReachTolerance, limited);
}
public sealed record ContactResult(string Chain, Vector3 Target, float Residual);

/// <summary>A gameplay request that one animated part instance play an expression (P7a), optionally at a clip time gameplay owns.</summary>
public readonly record struct PartPlay(string Instance, string Expression, double? Seconds = null);

/// <summary>
/// Gameplay inputs to evaluation. An expression here is a semantic request every animated part instance whose asset binds
/// it answers; it wins over the clip's selection channel and the instance default.
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
    /// <summary>Further masked layers over <see cref="Overlay"/>, each over the ones before it.</summary>
    public IReadOnlyList<PoseLayer>? Overlays { get; init; }
    /// <summary>Reverse authored horizontal offsets and rotation, without turning the actor's rest pose.</summary>
    public bool ReverseHorizontalMotion { get; init; }
    public string? Skin { get; init; }
    /// <summary>The actor's own continuous clock in seconds, driving part layers with a local clock (a blink that never restarts with the walk).</summary>
    public double Clock { get; init; }
    /// <summary>Explicit per-instance requests, each winning over <see cref="Expression"/> for its instance.</summary>
    public IReadOnlyList<PartPlay>? Plays { get; init; }

    /// <summary>Every layer in order, bottom first, that has any weight.</summary>
    internal IEnumerable<PoseLayer> Layers()
    {
        if (Overlay is { Weight: > 0 } first)
        {
            yield return first;
        }

        foreach (var layer in Overlays ?? [])
        {
            if (layer.Weight > 0)
            {
                yield return layer;
            }
        }
    }
}

/// <summary>
/// One override clip over the base, such as a gun shot's arms on any legs. <see cref="Targets"/> names the controls, chains
/// and animated part instances the overlay owns, whole: a masked channel without an overlay track rests rather than falling
/// back to the base. Its travel and contacts are ignored; the base owns locomotion. <see cref="Weight"/> fades the layer:
/// masked channels blend from the base's value toward the overlay's before the hierarchy and IK are solved, so a chain is
/// never blended as two solved poses. A bend choice and the overlay's face win from half weight; a base contact on a masked
/// chain eases out with it. <see cref="Namespace"/> reads the clip's tracks for one part instance (<c>instance/target</c>)
/// as that part's own targets: how a wearer's clip reaches into a nested rig.
/// </summary>
public sealed record PoseLayer(MotionClip Clip, double Seconds, IReadOnlySet<string> Targets, float Weight = 1)
{
    public string? Namespace { get; init; }

    /// <summary>The targets a clip keys inside one instance, stripped of the namespace: the mask of the wearer-driven layer on that part.</summary>
    public static HashSet<string> KeyedTargets(MotionClip clip, string instance)
    {
        var prefix = instance + Part.Namespace;
        var targets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var track in clip.Tracks)
        {
            if (track.Target.StartsWith(prefix, StringComparison.Ordinal))
            {
                targets.Add(track.Target[prefix.Length..]);
            }
        }

        return targets;
    }
}

/// <summary>
/// One animated part instance evaluated inside its wearer's pose (P7a): its own evaluated pose in the asset's space, its
/// placement in the character frame, the expression it shows, whether it is hidden and the layout fit its controls were
/// repositioned by. Drawing, picking, contours and editor handles read the nested pose through <see cref="World"/>,
/// <see cref="Frame"/>, <see cref="Rest"/> and <see cref="RestFrame"/>, which carry it into the wearer's frame.
/// </summary>
public sealed record EvaluatedAttachment(ResolvedAttachment Attachment, EvaluatedPose Pose, LayeredFrame2D Placement, string? Expression, bool Hidden, Vector2 LayoutFit)
{
    public string Id => Attachment.Id;
    public ResolvedModel Rig => Attachment.Rig!;

    private Vector3 Fitted(Vector3 point) => new(point.X * LayoutFit.X, point.Y * LayoutFit.Y, point.Z);

    /// <summary>A rig control's origin in the wearer's frame.</summary>
    public Vector3 World(string control) => Placement.At(Fitted(Pose.Points[control]));
    /// <summary>A rig control's placement in the wearer's frame: the artwork carrier of a bone-attached piece.</summary>
    public LayeredFrame2D Frame(string control)
    {
        var local = Pose.Placement(control);
        return (LayoutFit == Vector2.One ? local : local.WithOrigin(Fitted(local.Origin))).Then(Placement);
    }

    public Vector3 Rest(string control) => Placement.At(Fitted(Rig.Rest[control]));
    public LayeredFrame2D RestFrame(string control)
    {
        var local = Rig.RestPlacement(control);
        return (LayoutFit == Vector2.One ? local : local.WithOrigin(Fitted(local.Origin))).Then(Placement);
    }

    /// <summary>The pieces drawn for this instance: fitted and visible per the nested pose.</summary>
    public IEnumerable<PartGeometry> DrawnParts() => Attachment.Parts.Where(Pose.Visible).Select(Pose.Drawn);

    /// <summary>A wearer-side namespaced name for one of this instance's targets.</summary>
    public string Qualify(string target) => Id + Part.Namespace + target;
}

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
    /// <summary>The expression each animated part instance shows (P7a/P7c): a gameplay request, then the clip's selection channel, then the instance's and the asset's default.</summary>
    public Dictionary<string, string> Expressions { get; } = new(StringComparer.Ordinal);
    /// <summary>The locomotion frame's origin at this sample.</summary>
    public Vector3 Locomotion { get; set; }
    public List<ChainResult> Chains { get; } = [];
    public List<ContactResult> Contacts { get; } = [];
    /// <summary>Each limited bone node's joint-limit state at this sample: the requested local rotation, the held one and whether a bound was active.</summary>
    public Dictionary<string, ConstraintLimitEvaluation1D> Limits { get; } = new(StringComparer.Ordinal);
    /// <summary>Each drawing part's animated artwork fit at this sample (fit tracks), as multipliers of its leaf scale; absent is 1.</summary>
    public Dictionary<string, Vector2> Fits { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, Vector4> Tints { get; } = new(StringComparer.Ordinal);
    /// <summary>Time sampled by this pose, shared by texture frames. No rendering clock advances it.</summary>
    public double SampleSeconds { get; init; }
    /// <summary>Drawing parts a show track hides at this sample.</summary>
    public HashSet<string> HiddenParts { get; } = new(StringComparer.Ordinal);
    /// <summary>Every animated part instance evaluated inside this pose, by instance ID: the nested runtime graph (P7a).</summary>
    public Dictionary<string, EvaluatedAttachment> Attachments { get; } = new(StringComparer.Ordinal);
    public Vector3 World(string id) => _points[id];

    /// <summary>A control's evaluated placement: its bone frame at its drawing layer. Artwork, sockets and handles attach to this.</summary>
    public LayeredFrame2D Placement(string id) => new(new Frame2D(_bones[id]), _points[id].Z);

    /// <summary>The part with its animated artwork fit applied to its leaf scale; the part itself when nothing fits it.</summary>
    public PartGeometry Fitted(PartGeometry part) => Fits.TryGetValue(part.Id, out var fit) ? part with { ScaleX = part.ScaleX * fit.X, ScaleY = part.ScaleY * fit.Y } : part;

    public PartGeometry Drawn(PartGeometry part)
    {
        var fitted = Fitted(part);
        if (part.Material is not { } material) return fitted;
        var sampled = material.Sample(SampleSeconds, Tints.GetValueOrDefault(part.Id, Vector4.One));
        return ReferenceEquals(sampled, material) ? fitted : fitted with { Material = sampled };
    }

    /// <summary>Whether a part draws at this sample: not hidden on the part and not hidden by a show track.</summary>
    public bool Visible(PartGeometry part) => !part.Hidden && !HiddenParts.Contains(part.Id);

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

/// <summary>Samples channels, builds the hierarchy, then solves IK and contacts, then evaluates the nested part instances. No graphics.</summary>
public static class PoseEvaluator
{
    private static readonly MotionClip Still = new() { Id = "rest", Name = "Rest", Model = "rest" };

    /// <summary>The rest pose: no channels, no travel, no contacts.</summary>
    public static EvaluatedPose Rest(ResolvedModel model, PoseInput input = default) => Sample(model, null, 0, false, input);

    /// <summary>One layer's sampled view: its tracks by (kind, target) inside its namespace, the time to read them at, its clip, mask and weight.</summary>
    private sealed class Layer
    {
        public Layer(PoseLayer source)
        {
            Source = source;
            Clip = source.Clip;
            Weight = MathF.Min(source.Weight, 1);
            Time = (float)PlaybackClock.SampleTime(source.Seconds, Clip.Duration, Clip.Loop ? PlaybackEndMode.Loop : PlaybackEndMode.Hold);
            var prefix = source.Namespace is null ? null : source.Namespace + Part.Namespace;
            foreach (var track in Clip.Tracks)
            {
                if (prefix is null)
                {
                    if (!track.Target.Contains(Part.Namespace))
                    {
                        Tracks[(track.Kind, track.Target)] = track;
                    }
                }
                else if (track.Target.StartsWith(prefix, StringComparison.Ordinal))
                {
                    Tracks[(track.Kind, track.Target[prefix.Length..])] = track;
                }
            }
        }

        public PoseLayer Source { get; }
        public MotionClip Clip { get; }
        public float Time { get; }
        public float Weight { get; }
        public Dictionary<(string Kind, string Target), ClipTrack> Tracks { get; } = [];
        public bool Masks(string target) => Source.Targets.Contains(target);
    }

    /// <summary>The clip must already have passed <see cref="MotionClip.Validate(ResolvedModel, bool)"/> for this model. A null clip samples rest.</summary>
    public static EvaluatedPose Sample(ResolvedModel model, MotionClip? clip, double seconds, bool repeat = false, PoseInput input = default)
    {
        var restOnly = clip is null;
        clip ??= Still;
        ArgGuard.NonNegative(seconds);
        var cycles = repeat && clip.Loop ? Math.Floor(seconds / clip.Duration) : 0;
        var time = (float)PlaybackClock.SampleTime(seconds, clip.Duration, repeat && clip.Loop ? PlaybackEndMode.Loop : PlaybackEndMode.Hold);
        float Ratio(string scale) => scale == CharacterModel.Unit ? 1 : model.Measure(scale) / clip.Reference[scale];

        var travelRatio = Ratio(clip.Travel.Scale);
        Vector2 Travel(float t)
        {
            var (value, _) = Interpolate(clip.Travel.Keys, t);
            return new Vector2(value.X, value.Y) * travelRatio;
        }
        var cycleTravel = (Travel(clip.Duration) - Travel(0)) * (float)cycles;
        var cycleOrigin = new Vector3(Travel(0) + cycleTravel, 0);
        var pose = new EvaluatedPose { Locomotion = new(Travel(time) + cycleTravel, 0), SampleSeconds = restOnly ? seconds : time };
        var layers = input.Layers().Select(l => new Layer(l)).ToArray();
        if (model.MotionOwner is { } mover)
        {
            // The owning module's travel, through its socket's rest frame (P10c); the wearer's own clip carries none.
            pose.Locomotion = new(OwnerTravel(model, mover, clip, time, seconds, repeat, input, layers), 0);
            cycleOrigin = pose.Locomotion;
        }
        if (input.InPlace)
        {
            cycleOrigin -= pose.Locomotion;
            pose.Locomotion = Vector3.Zero;
        }

        // The base clip's own tracks (a namespaced target belongs to a nested instance, read there), then the layers over it.
        var tracks = new Dictionary<(string, string), ClipTrack>();
        foreach (var track in clip.Tracks)
        {
            if (!track.Target.Contains(Part.Namespace))
            {
                tracks[(track.Kind, track.Target)] = track;
            }
        }
        // A channel read from one layer: its track, the time to read it at and the clip whose reference units it uses.
        (ClipTrack? Track, float Time, MotionClip Clip) Read(Layer? layer, string kind, string target) => layer is null
            ? (tracks.GetValueOrDefault((kind, target)), time, clip)
            : (layer.Tracks.GetValueOrDefault((kind, target)), layer.Time, layer.Clip);
        Vector3 LayerDelta(Layer? layer, string kind, string target, string defaultScale)
        {
            var (track, at, source) = Read(layer, kind, target);
            if (track is null)
            {
                return MotionClip.DefaultValue(kind);
            }

            var (value, _) = Interpolate(track, at);
            var scale = track.Scale ?? defaultScale;
            // A wearer's namespaced layer keys a nested part in the part's own units (P7a/P10b): no measure scales it.
            var ratio = scale == CharacterModel.Unit || layer?.Source.Namespace is not null ? 1 : model.Measure(scale) / source.Reference[scale];
            var delta = new Vector3(value.X * ratio, value.Y * ratio, value.Z);
            if (!input.ReverseHorizontalMotion || kind == MotionClip.ScaleKind || MotionClip.PartKinds.Contains(kind))
            {
                return delta;
            }

            return new(-delta.X, delta.Y, delta.Z);
        }
        // Every masking layer in order blends the value below it toward its own; an unkeyed masked channel rests.
        Vector3 Delta(string kind, string target, string defaultScale)
        {
            var value = LayerDelta(null, kind, target, defaultScale);
            foreach (var layer in layers)
            {
                if (layer.Masks(target))
                {
                    value = layer.Weight >= 1 ? LayerDelta(layer, kind, target, defaultScale) : Vector3.Lerp(value, LayerDelta(layer, kind, target, defaultScale), layer.Weight);
                }
            }

            return value;
        }
        int LayerBend(Layer? layer, ModelChain chain) => Read(layer, MotionClip.TargetKind, chain.Id) is { Track: { } track, Time: var at }
            && track.Keys.LastOrDefault(k => k.Bend is not null && k.Time <= at) is { Bend: { } bend } ? bend : chain.Bend;
        int Bend(ModelChain chain)
        {
            // A discrete choice: the topmost layer owning the chain from half weight.
            for (var i = layers.Length - 1; i >= 0; i--)
            {
                if (layers[i].Masks(chain.Id) && layers[i].Weight >= .5f)
                {
                    return LayerBend(layers[i], chain);
                }
            }

            return LayerBend(null, chain);
        }
        float LayerAngle(Layer? layer, string target) => Read(layer, MotionClip.RotateKind, target) is { Track: { } track, Time: var at }
            ? Interpolate(track, at).Angle * (input.ReverseHorizontalMotion ? -1 : 1) : 0;
        float Angle(string target)
        {
            var value = LayerAngle(null, target);
            foreach (var layer in layers)
            {
                if (layer.Masks(target))
                {
                    value += (LayerAngle(layer, target) - value) * layer.Weight;
                }
            }

            return value;
        }
        // The normalized bend channel: 0..1 through the node's limits, absolute (it owns the rotation), never reversed or
        // scaled by a measure; before a setup-before-first track's first key the node keeps its setup rotation (null).
        float? LayerNormalized(Layer? layer, string target) => Read(layer, MotionClip.BendKind, target) is { Track: { } track, Time: var at }
            && !(track.SetupBeforeFirst && track.Keys.Count > 0 && at < track.Keys[0].Time) ? Interpolate(track.Keys, at).Value.X : null;
        float? BendOf(string target)
        {
            var value = LayerNormalized(null, target);
            foreach (var layer in layers)
            {
                if (!layer.Masks(target))
                {
                    continue;
                }

                var over = LayerNormalized(layer, target);
                value = layer.Weight >= 1 ? over : value is { } a && over is { } b ? a + (b - a) * layer.Weight : over ?? value;
            }

            return value;
        }

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
            var carrier = parent is null ? locomotion : pose.Placement(parent.Id);
            var origin = node.Origin(parent?.Length ?? 0) + translate.XY;
            var rotation = node.Rotation + rotate;
            if (node.Limit is { } limit)
            {
                // Joint limits (P4b): a bend maps through the range; an angle channel keeps its authored value and is
                // held at the bound, with the state recorded so nothing clamps silently. The bound value is the local
                // rotation, so reflection and facing never invert the convention.
                if (BendOf(control.Id) is { } bend)
                {
                    rotation = limit.FromNormalized(bend);
                }

                var state = limit.Evaluate(rotation);
                pose.Limits[control.Id] = state;
                rotation = state.Target;
            }
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
            // A masked chain fading in leaves its base contact gradually; it is neither held nor reported as planted.
            var owning = 0f;
            foreach (var layer in layers)
            {
                if (layer.Masks(contact.Chain))
                {
                    owning = MathF.Max(owning, layer.Weight);
                }
            }

            if (owning >= 1)
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
            if (owning > 0)
            {
                constraints.Apply(new RigContactPin(chain.Id, target, owning));
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
            Vector3 ReadOrientation(Layer? layer) => Read(layer, MotionClip.OrientKind, socket.Id) is { Track: { } t, Time: var at }
                ? ReverseOrientation(Interpolate(t.Keys, at).Value) : Vector3.Zero;
            Vector3 ReverseOrientation(Vector3 value) => input.ReverseHorizontalMotion ? new(value.X, -value.Y, -value.Z) : value;
            // Socket channels follow ownership of their frame, including an IK chain that owns their attachment end.
            var orientation = ReadOrientation(null);
            foreach (var layer in layers)
            {
                var owned = layer.Masks(socket.Id) || layer.Masks(socket.Frame ?? socket.Control) || layer.Masks(socket.Control)
                    || model.Chains.Values.Any(c => c.End == socket.Control && layer.Masks(c.Id));
                if (owned)
                {
                    orientation = Vector3.Lerp(orientation, ReadOrientation(layer), layer.Weight);
                }
            }

            pose.SocketAngles[socket.Id] = orientation;
        }
        SkeletonAppearance2D.Evaluate(model, clip, time, input.Skin, pose);
        // Part channels (P7a): an explicit artwork fit and a held visibility per drawing part, layered like any other channel.
        foreach (var part in model.Parts)
        {
            var tint = new Vector4(Delta(MotionClip.TintKind, part.Id, CharacterModel.Unit), Delta(MotionClip.AlphaKind, part.Id, CharacterModel.Unit).X);
            if (tint != Vector4.One) pose.Tints[part.Id] = Vector4.Clamp(tint, Vector4.Zero, Vector4.One);
            var fit = Delta(MotionClip.FitKind, part.Id, CharacterModel.Unit);
            if (fit.X != 0 || fit.Y != 0)
            {
                pose.Fits[part.Id] = new(1 + fit.X, 1 + fit.Y);
            }

            if (ShownAt(part.Id) is false)
            {
                pose.HiddenParts.Add(part.Id);
            }
        }
        bool? ShownAt(string part)
        {
            // Held, discrete: the topmost layer owning the part from half weight, else the base; no track keeps the part shown.
            bool? Read1(Layer? layer) => Read(layer, MotionClip.ShowKind, part) is { Track: { } track, Time: var at }
                ? Interpolate(track, at).Value.X != 0 : null;
            for (var i = layers.Length - 1; i >= 0; i--)
            {
                if (layers[i].Masks(part) && layers[i].Weight >= .5f)
                {
                    return Read1(layers[i]);
                }
            }

            return Read1(null);
        }
        if (model.Attachments.Count > 0)
        {
            EvaluateAttachments(model, pose, clip, time, seconds, input, layers);
        }

        return pose;
    }

    // ---- Nested part instances (P7a) --------------------------------------------------------------------------------

    /// <summary>
    /// Evaluates every animated attachment instance after the wearer's pose is final: the instance's placement on its
    /// socket, the expression it shows (gameplay request, the topmost owning layer's or the base clip's selection channel,
    /// else the instance's default), each of its layers' clip and clock, and the wearer's namespaced tracks as masked
    /// layers over the part's own. The result is pure: part clips dispatch nothing.
    /// </summary>
    private static void EvaluateAttachments(ResolvedModel model, EvaluatedPose pose, MotionClip clip, float time, double seconds, PoseInput input, Layer[] layers)
    {
        var actor = new ActorPose(pose, Vector2.Zero, 1);
        // Dependency order (P10c): an instance mounted on another instance's control reads that instance's evaluated frames.
        foreach (var attachment in model.EvaluationOrder)
        {
            if (!attachment.IsAnimated)
            {
                continue;
            }

            var placement = attachment.Placement(actor.SocketPlacement(attachment.Socket));
            var (expression, since, owned) = Select(attachment, clip, time, input, layers);
            var binding = attachment.Asset.Binding(expression);
            var hidden = attachment.Instance.Hidden || expression == Part.None || binding?.Hidden == true;
            pose.Attachments[attachment.Id] = EvaluateAttachment(attachment, placement, expression, binding, hidden, time, seconds, since, owned, input, clip, layers);
            if (expression is not null)
            {
                pose.Expressions[attachment.Id] = expression;
            }
        }
    }

    /// <summary>
    /// The selection for one instance: an explicit gameplay play, a semantic request the asset binds, the topmost owning
    /// layer's selection channel from half weight, the base clip's, then the instance's own default.
    /// </summary>
    private static (string? Expression, double? Since, double? Owned) Select(ResolvedAttachment attachment, MotionClip clip, float time, PoseInput input, Layer[] layers)
    {
        if (input.Plays?.FirstOrDefault(p => p.Instance == attachment.Id) is { Instance: not null } play)
        {
            return (play.Expression, null, play.Seconds);
        }

        if (input.Expression is { } requested && attachment.Asset.Binds(requested))
        {
            return (requested, null, null);
        }

        string? expression = null;
        double? since = null;
        for (var i = layers.Length - 1; i >= 0 && expression is null; i--)
        {
            if (layers[i].Masks(attachment.Id) && layers[i].Weight >= .5f && layers[i].Clip.PlayAt(attachment.Id, layers[i].Time) is { } selected)
            {
                (expression, since) = selected;
            }
        }

        if (expression is null && clip.PlayAt(attachment.Id, time) is { } held)
        {
            (expression, since) = held;
        }

        return (expression ?? attachment.DefaultExpression, since, null);
    }

    /// <summary>The base-layer clip and its seconds for a selection, as <see cref="EvaluateAttachment"/> plays them.</summary>
    private static (MotionClip? Clip, double Seconds) BasePlay(ResolvedAttachment attachment, string? expression, double? since, double? owned, float time, double seconds, PoseInput input)
    {
        var asset = attachment.Asset;
        var binding = asset.Binding(expression);
        var layer = asset.BaseLayer!;
        var expressionLayer = binding?.Layer ?? layer.Id;
        if (binding is not null && binding.Clip is not null && layer.Id == expressionLayer)
        {
            var at = binding.Time ?? owned ?? (since is { } key ? Math.Max(0, time - key) * (layer.Clock == PartClocks.Mapped ? layer.Rate : 1) : Math.Max(0, layer.Time(seconds, input.Clock)));
            return (asset.Clip(binding.Clip), at);
        }

        if (binding?.Mutes.Contains(layer.Id) == true)
        {
            return (null, 0);
        }

        return (asset.Clip(layer.Clip), Math.Max(0, layer.Time(seconds, input.Clock)));
    }

    /// <summary>
    /// The root travel the owning module's base clip has accumulated, in the wearer's frame (P10c): the module clip's travel
    /// (with its cycles) turned by the instance's placement on its socket at rest. The wearer's rig is still at rest when
    /// this is read, which is why the owner must mount on one of the wearer's own controls.
    /// </summary>
    private static Vector2 OwnerTravel(ResolvedModel model, ResolvedAttachment mover, MotionClip clip, float time, double seconds, bool repeat, PoseInput input, Layer[] layers)
    {
        var (expression, since, owned) = Select(mover, clip, time, input, layers);
        var (gait, at) = BasePlay(mover, expression, since, owned, time, seconds, input);
        if (gait is null)
        {
            return Vector2.Zero;
        }

        var travel = TravelOf(mover.Rig!, gait, at, repeat: true);
        if (since is { } key && owned is null)
        {
            // A gait selected by the wearer clip's key restarts at that key every wearer cycle; the travel of the completed
            // cycles accumulates, so an actor keeps walking instead of snapping back each loop.
            var cycles = repeat && clip.Loop ? Math.Floor(seconds / clip.Duration) : 0;
            if (cycles > 0)
            {
                travel += TravelOf(mover.Rig!, gait, Math.Max(0, clip.Duration - key), repeat: true) * (float)cycles;
            }
        }

        return OwnerFrame(model, mover).Direction(travel);
    }

    /// <summary>The owner's placement on its socket at the wearer's rest, whose axes carry the module's travel into the wearer's frame.</summary>
    private static LayeredFrame2D OwnerFrame(ResolvedModel model, ResolvedAttachment mover)
    {
        var socket = mover.Socket;
        var angle = socket.Frame == CharacterModel.Locomotion ? 0 : model.Nodes[socket.Frame ?? socket.Control].RestAngle;
        var carrier = LayeredFrame2D.FromRotation(angle, model.Rest[socket.Control]);
        return mover.Placement(LayeredFrame2D.Local(Vector2.One, socket.Angle, new(socket.OffsetX, socket.OffsetY), socket.OffsetZ).Then(carrier));
    }

    /// <summary>A clip's root travel at a time on a model, cycles included, as the pose's locomotion reads it.</summary>
    public static Vector2 TravelOf(ResolvedModel model, MotionClip clip, double seconds, bool repeat)
    {
        var cycles = repeat && clip.Loop ? Math.Floor(seconds / clip.Duration) : 0;
        var time = (float)(cycles > 0 ? seconds % clip.Duration : Math.Min(seconds, clip.Duration));
        var ratio = clip.Travel.Scale == CharacterModel.Unit ? 1 : model.Measure(clip.Travel.Scale) / clip.Reference[clip.Travel.Scale];
        Vector2 Travel(float t)
        {
            var (value, _) = Interpolate(clip.Travel.Keys, t);
            return new Vector2(value.X, value.Y) * ratio;
        }
        return Travel(time) + (Travel(clip.Duration) - Travel(0)) * (float)cycles;
    }

    private static EvaluatedAttachment EvaluateAttachment(ResolvedAttachment attachment, LayeredFrame2D placement, string? expression, PartExpression? binding, bool hidden,
        float time, double seconds, double? since, double? owned, PoseInput input, MotionClip clip, Layer[] layers)
    {
        var asset = attachment.Asset;
        var rig = attachment.Rig!;
        var expressionLayer = binding?.Layer ?? asset.BaseLayer!.Id;
        MotionClip? baseClip = null;
        double baseSeconds = 0;
        var overlays = new List<PoseLayer>();
        foreach (var layer in asset.Layers)
        {
            MotionClip? playing;
            double at;
            if (binding is not null && binding.Clip is not null && layer.Id == expressionLayer)
            {
                playing = asset.Clip(binding.Clip);
                // A pose holds one time; a clip gameplay plays runs on gameplay's seconds; one a selection key chose runs
                // from that key (at the layer's rate); one a request chose runs on the layer's clock.
                at = binding.Time ?? owned ?? (since is { } key ? Math.Max(0, time - key) * (layer.Clock == PartClocks.Mapped ? layer.Rate : 1) : Math.Max(0, layer.Time(seconds, input.Clock)));
            }
            else if (binding?.Mutes.Contains(layer.Id) == true)
            {
                playing = null;
                at = 0;
            }
            else
            {
                playing = asset.Clip(layer.Clip);
                at = Math.Max(0, layer.Time(seconds, input.Clock));
            }

            if (layer == asset.BaseLayer)
            {
                baseClip = playing;
                baseSeconds = at;
            }
            else if (playing is not null)
            {
                var mask = asset.Groups.First(g => g.Id == layer.Mask).Targets.ToHashSet(StringComparer.Ordinal);
                overlays.Add(new(playing, at, mask));
            }
        }
        // The wearer reaches in: its base clip's namespaced tracks own what they key at full weight; each wearer layer
        // that owns the instance adds its namespaced tracks at its own weight.
        var keyed = PoseLayer.KeyedTargets(clip, attachment.Id);
        if (keyed.Count > 0)
        {
            overlays.Add(new(clip, time, keyed) { Namespace = attachment.Id });
        }

        foreach (var layer in layers)
        {
            if (!layer.Masks(attachment.Id))
            {
                continue;
            }

            var owns = PoseLayer.KeyedTargets(layer.Clip, attachment.Id);
            if (owns.Count > 0)
            {
                overlays.Add(new(layer.Clip, layer.Time, owns, layer.Weight) { Namespace = attachment.Id });
            }
        }
        var nested = Sample(rig, baseClip, baseSeconds, repeat: true, new()
        {
            InPlace = true,
            Overlays = overlays,
            Clock = input.Clock,
        });
        return new(attachment, nested, placement, expression, hidden, attachment.LayoutFit);
    }

    /// <summary>
    /// One animated instance evaluated on its own at a placement, as a library preview does before the part is attached:
    /// the asset's default expression and clocks at <paramref name="seconds"/>, no wearer clip.
    /// </summary>
    public static EvaluatedAttachment Preview(ResolvedAttachment attachment, LayeredFrame2D placement, double seconds, string? expression = null)
    {
        expression ??= attachment.DefaultExpression;
        var binding = attachment.Asset.Binding(expression);
        var hidden = attachment.Instance.Hidden || expression == Part.None || binding?.Hidden == true;
        return EvaluateAttachment(attachment, placement, expression, binding, hidden, (float)seconds, seconds, null, null, new() { Clock = seconds }, Still, []);
    }

    /// <summary>The locomotion frame's travel over one cycle on this model: the stride gameplay matches against ground distance.</summary>
    public static Vector2 CycleTravel(ResolvedModel model, MotionClip clip)
    {
        if (model.MotionOwner is { } mover)
        {
            // The owning module's gait selected at the clip's start carries the travel; one module clip cycle is the stride.
            var (expression, _, _) = Select(mover, clip, 0, default, []);
            var gait = mover.Asset.Binding(expression) is { Clip: { } id } binding && (binding.Layer ?? mover.Asset.BaseLayer!.Id) == mover.Asset.BaseLayer!.Id ? mover.Asset.Clip(id) : mover.Asset.Clip(mover.Asset.BaseLayer!.Clip);
            return gait is null ? Vector2.Zero : OwnerFrame(model, mover).Direction(CycleTravel(mover.Rig!, gait));
        }

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
        track.Keys.Count == 0 || track.SetupBeforeFirst && time < track.Keys[0].Time ? (MotionClip.DefaultValue(track.Kind), 0)
        : track.Kind == MotionClip.ShowKind ? (KeyframeSampling.Held(track.Keys, time)!.Value, 0)
        : Interpolate(track.Keys, time, track.Kind == MotionClip.RotateKind);
}
