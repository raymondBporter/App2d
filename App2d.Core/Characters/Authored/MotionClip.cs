using App2d.Core.Animation;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Progress;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>A keyed value at a time. Translate, target and travel keys use X/Y/Z; rotate keys use Angle (radians).</summary>
public sealed record ClipKey : ITimedKey
{
    public float Time { get; set; }
    public Vector3 Value { get; set; }
    public float Angle { get; set; }
    /// <summary>How the value moves from this key to the next: see <see cref="ClipEase"/>.</summary>
    public string Ease { get; set; } = ClipEase.Linear;
    public KeyCurve2D? Curve { get; set; }
    public KeyCurve2D? CurveY { get; set; }
    public KeyCurve2D? CurveZ { get; set; }
    /// <summary>
    /// Target keys only: which side the chain's joint bends to (1 or -1) from this key until the next key that sets one,
    /// such as an elbow or knee mirrored for a back view. Flip it where the limb passes straight so the change never
    /// shows. Null keeps the current side; before any key sets one, the chain's own bend applies.
    /// </summary>
    public int? Bend { get; set; }
}

/// <summary>Per-key easing. Linear is the default; step holds the value until the next key.</summary>
public static class ClipEase
{
    public const string Linear = "linear", Smooth = "smooth", Step = "step";
    public static readonly IReadOnlyList<string> All = [Linear, Smooth, Step];
    public static float Apply(string ease, float u) => ease switch { Smooth => Progress.SmoothStepUnclamped(u), Step => 0, _ => u };
    public static float Interpolate(float start, float end, float phase, string ease, KeyCurve2D? curve) =>
        new Sampler(phase, ease).Interpolate(start, end, curve);

    /// <summary>One segment sample shared by up to four scalar channels. No state survives the sample.</summary>
    internal struct Sampler(float phase, string ease)
    {
        private readonly float _progress = Apply(ease, phase);
        private Timings _timings;
        private int _count;

        public float Interpolate(float start, float end, KeyCurve2D? curve)
        {
            if (ease == Step) return start;
            if (curve is null) return Interpolation.Lerp(start, end, _progress);
            if (phase <= 0) return start;
            if (phase >= 1) return end;

            var geometry = curve.ToCurve(start, end);
            var handles = new Vector2(geometry.Control1.X, geometry.Control2.X);
            for (var i = 0; i < _count; i++)
            {
                if (_timings[i].Handles == handles)
                {
                    return geometry.Evaluate(_timings[i].Parameter).Y;
                }
            }

            // Only X determines the parameter; Y handles and Absolute stay specific to this channel.
            var parameter = geometry.ParameterAtX(phase);
            if (_count < 4) _timings[_count++] = (handles, parameter);
            return geometry.Evaluate(parameter).Y;
        }

        [InlineArray(4)]
        private struct Timings
        {
            private (Vector2 Handles, float Parameter) _element0;
        }
    }
}

/// <summary>A named visual moment, such as a footstep or a strike. Gameplay binds events to markers; the clip owns only the time.</summary>
public sealed record ClipMarker
{
    public string Id { get; set; } = "";
    public float Time { get; set; }
}

/// <summary>An expression held from this time until the next key; "none" draws no face.</summary>
public sealed record ClipFaceKey
{
    public float Time { get; set; }
    public string Expression { get; set; } = "relaxed";
}

/// <summary>Appearance channel for one face-bearing part. Never baked into limb keys; gameplay can override it.</summary>
public sealed record ClipFaceTrack
{
    public string Part { get; set; } = "";
    public List<ClipFaceKey> Keys { get; set; } = [];
}

/// <summary>
/// translate: a control's parent-local offset delta. rotate: a control's XY rotation, inherited by its children.
/// target: an IK chain's end-target delta in the chain's frame. Deltas are from rest, in reference units.
/// orient: a socket's local twist/tilt/turn in X/Y/Z radians, applied in that order before its inherited XY frame.
/// </summary>
public sealed record ClipTrack
{
    public string Kind { get; set; } = MotionClip.TranslateKind;
    public string Target { get; set; } = "";
    /// <summary>Overrides the control's or chain's default measure. Null uses the model default.</summary>
    public string? Scale { get; set; }
    public List<ClipKey> Keys { get; set; } = [];
    /// <summary>Imported timelines use the setup value before their first key.</summary>
    public bool SetupBeforeFirst { get; set; }
}

/// <summary>The locomotion frame's travel in authored preview. The controller owns travel in gameplay.</summary>
public sealed record ClipTravel
{
    public string Scale { get; set; } = CharacterModel.Unit;
    public List<ClipKey> Keys { get; set; } = [];
}

/// <summary>A chain end held over [Start, Finish) at a target in the locomotion frame at the cycle's start. Target is a delta from the end's rest position, in the chain's scale.</summary>
public sealed record ClipContact
{
    public string Chain { get; set; } = "";
    public float Start { get; set; }
    public float Finish { get; set; } = .5f;
    public PuppetPoint Target { get; set; }
}

/// <summary>A named animation compatible with one model revision. Owns motion and appearance timelines, not proportions or gameplay.</summary>
public sealed class MotionClip
{
    public const string FormatId = "app2d-clip", TranslateKind = "translate", RotateKind = "rotate", TargetKind = "target";
    /// <summary>Socket-local Euler angles in radians: X twist, Y tilt, Z turn. Unwrapped angles preserve authored full turns.</summary>
    public const string OrientKind = "orient";
    public const string ScaleKind = "scale", ShearKind = "shear";
    /// <summary>
    /// A normalized bend (<see cref="ChannelSemantics.NormalizedParameter"/>, P4b): key x in 0..1 maps through the target bone
    /// node's declared joint limits onto its parent-local rotation, exactly at both ends. It owns the node's rotation in the
    /// clip, so a rotate track on the same node is refused; changing the limits changes this performance by design.
    /// </summary>
    public const string BendKind = "bend";
    public static readonly IReadOnlyList<string> TrackKinds = [TranslateKind, RotateKind, TargetKind, OrientKind, ScaleKind, ShearKind, BendKind];
    public string Format { get; set; } = FormatId;
    public const int CurrentVersion = 3;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public int StructureRevision { get; set; } = 1;
    public float Duration { get; set; } = 1;
    public bool Loop { get; set; }

    /// <summary>Measure lengths the clip was authored against, so a base edit never silently reinterprets authored units.</summary>
    public Dictionary<string, float> Reference { get; set; } = [];
    public ClipTravel Travel { get; set; } = new();
    public List<ClipTrack> Tracks { get; set; } = [];
    public List<ClipContact> Contacts { get; set; } = [];
    public List<ClipMarker> Markers { get; set; } = [];
    public List<ClipFaceTrack> Faces { get; set; } = [];
    public List<SlotAttachmentTrack2D> Attachments { get; set; } = [];
    public List<SlotColorTrack2D> Colors { get; set; } = [];
    public List<DrawOrderKey2D> DrawOrder { get; set; } = [];
    public List<AnimationEvent2D> Events { get; set; } = [];
    /// <summary>The imported motion this clip was converted from, if any. Null for clips authored here.</summary>
    public AssetSource? Source { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static MotionClip FromJson(string json)
    {
        var clip = AuthoredAsset.Parse<MotionClip>(json, "clip");
        clip.Validate();
        return clip;
    }
    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }

    public void Validate()
    {
        var owner = $"Clip '{Id}'";
        Require(Format == FormatId && Version == CurrentVersion, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "clip id");
        AuthoredAsset.RequireId(Model, $"{owner} model");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        new Limit(.05f, 60).Check(Duration, $"{owner} duration");
        Require(Reference is not null && Travel?.Keys is not null && Tracks is not null && Contacts is not null && Markers is not null && Faces is not null, $"{owner}: collections cannot be null.");
        Require(Travel.Scale is not null, $"{owner} travel: a scale is required.");
        Source?.Validate(owner);
        if (Attachments is null || Colors is null || DrawOrder is null || Events is null)
        {
            throw new InvalidDataException($"{owner}: skeleton timelines cannot be null.");
        }

        var attachmentSlots = new HashSet<string>();
        foreach (var track in Attachments)
        {
            if (track is null || track.Keys is null || !attachmentSlots.Add(track.Slot))
            {
                throw new InvalidDataException($"{owner}: invalid attachment timeline.");
            }

            AuthoredAsset.RequireId(track.Slot, $"{owner} attachment slot");
            var previous = -1f;
            foreach (var key in track.Keys)
            {
                if (key is null || !float.IsFinite(key.Time) || key.Time < 0 || key.Time > Duration || key.Time <= previous)
                {
                    throw new InvalidDataException($"{owner}: invalid attachment key time.");
                }

                Require(key.Attachment is null || !string.IsNullOrWhiteSpace(key.Attachment), $"{owner}: attachment names cannot be empty.");
                previous = key.Time;
            }
        }
        var colorChannels = new HashSet<(string Slot, int Component)>();
        foreach (var track in Colors)
        {
            Require(track?.Keys is not null, $"{owner}: incomplete color timeline.");
            AuthoredAsset.RequireId(track.Slot, $"{owner} color slot");
            Require(track.Kind is SlotColorTrack2D.Rgba or SlotColorTrack2D.Rgb or SlotColorTrack2D.Alpha, $"{owner}: unknown color timeline kind.");
            foreach (var component in track.Kind == SlotColorTrack2D.Alpha ? new[] { 3 } : track.Kind == SlotColorTrack2D.Rgb ? [0, 1, 2] : [0, 1, 2, 3])
            {
                Require(colorChannels.Add((track.Slot, component)), $"{owner}: overlapping color timelines for slot '{track.Slot}'.");
            }

            Require(track.Keys.Count <= 4096, $"{owner}: too many color keys.");
            var previous = -1f;
            foreach (var key in track.Keys)
            {
                Require(key is not null, $"{owner}: null color key.");
                new Limit(0, Duration).Check(key.Time, $"{owner} color key time");
                Require(key.Time > previous, $"{owner}: color key times must be strictly increasing.");
                previous = key.Time;
                foreach (var value in new[] { key.Value.X, key.Value.Y, key.Value.Z, key.Value.W })
                {
                    new Limit(0, 1).Check(value, $"{owner} color component");
                }

                EntityVocabulary.Require(key.Ease, ClipEase.All, $"{owner} color key ease");
                key.CurveR?.Validate();
                key.CurveG?.Validate();
                key.CurveB?.Validate();
                key.CurveA?.Validate();
            }
        }
        var previousOrder = -1f;
        foreach (var key in DrawOrder)
        {
            if (key is null || key.Slots is null || !float.IsFinite(key.Time) || key.Time < 0 || key.Time > Duration || key.Time <= previousOrder)
            {
                throw new InvalidDataException($"{owner}: invalid draw order key.");
            }

            previousOrder = key.Time;
        }
        foreach (var key in Events)
        {
            if (key is null || string.IsNullOrWhiteSpace(key.Name) || !float.IsFinite(key.Time) || key.Time < 0 || key.Time > Duration || !float.IsFinite(key.Float))
            {
                throw new InvalidDataException($"{owner}: invalid event.");
            }
        }

        foreach (var (measure, length) in Reference)
        {
            AuthoredAsset.RequireId(measure, $"{owner} reference");
            new Limit(.001f, 1000).Check(length, $"{owner} reference.{measure}");
        }
        CheckKeys(Travel.Keys, $"{owner} travel", k => k.Value.Z == 0 && k.Angle == 0, "travel keys use x and y only");
        var seen = new HashSet<(string, string)>();
        foreach (var track in Tracks)
        {
            Require(track?.Keys is not null && track.Target is not null, $"{owner}: incomplete track.");
            EntityVocabulary.Require(track.Kind, TrackKinds, $"{owner} track kind");
            Require(seen.Add((track.Kind, track.Target)), $"{owner}: duplicate {track.Kind} track for '{track.Target}'.");
            Require(track.Keys.All(k => k is null || k.Bend is null || track.Kind == TargetKind && k.Bend is 1 or -1), $"{owner} {track.Kind} track '{track.Target}': bend is 1 or -1, on target keys only.");
            var rotate = track.Kind == RotateKind;
            CheckKeys(track.Keys, $"{owner} {track.Kind} track '{track.Target}'",
                rotate ? k => k.Value.X == 0 && k.Value.Y == 0 && k.Value.Z == 0 : k => k.Angle == 0, rotate ? "rotate keys use angle only" : "keys use x, y and z only");
        }
        foreach (var contact in Contacts)
        {
            Require(contact?.Chain is not null, $"{owner}: incomplete contact.");
            var field = $"{owner} contact on '{contact.Chain}'";
            new Limit(0, Duration).Check(contact.Start, field + " start");
            new Limit(0, Duration).Check(contact.Finish, field + " finish");
            Require(contact.Finish > contact.Start, $"{field}: finish must follow start.");
            contact.Target.Check(field + " target");
        }
        var markers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var marker in Markers)
        {
            Require(marker is not null, $"{owner}: null marker.");
            AuthoredAsset.RequireId(marker.Id, $"{owner} marker id");
            Require(markers.Add(marker.Id), $"{owner}: duplicate marker '{marker.Id}'.");
            new Limit(0, Duration).Check(marker.Time, $"{owner} marker '{marker.Id}' time");
        }
        var faceParts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var face in Faces)
        {
            Require(face?.Keys is not null && face.Part is not null, $"{owner}: incomplete face track.");
            Require(faceParts.Add(face.Part), $"{owner}: duplicate face track for '{face.Part}'.");
            var previous = -1f;
            foreach (var key in face.Keys)
            {
                Require(key is not null, $"{owner} face '{face.Part}': null key.");
                new Limit(0, Duration).Check(key.Time, $"{owner} face '{face.Part}' key time");
                Require(key.Time > previous, $"{owner} face '{face.Part}': key times must be strictly increasing.");
                previous = key.Time;
                // "none" hides the face for the key's span, as in a back view.
                Require(key.Expression == "none" || FaceExpressions.Contains(key.Expression), $"{owner} face '{face.Part}': unknown expression '{key.Expression}'.");
            }
        }
        foreach (var group in Contacts.GroupBy(c => c.Chain))
        {
            ClipContact? previous = null;
            foreach (var contact in group.OrderBy(c => c.Start))
            {
                Require(previous is null || previous.Finish <= contact.Start, $"{owner}: contacts on '{contact.Chain}' must not overlap.");
                previous = contact;
            }
        }
    }

    private void CheckKeys(List<ClipKey> keys, string field, Func<ClipKey, bool> shape, string shapeMessage)
    {
        Require(keys.Count <= 4096, $"{field}: too many keys.");
        var previous = -1f;
        foreach (var key in keys)
        {
            Require(key is not null, $"{field}: null key.");
            new Limit(0, Duration).Check(key.Time, field + " key time");
            Require(key.Time > previous, $"{field}: key times must be strictly increasing.");
            previous = key.Time;
            foreach (var value in new[] { key.Value.X, key.Value.Y, key.Value.Z, key.Angle })
            {
                new Limit(-1000, 1000).Check(value, field + " key value");
            }

            Require(shape(key), $"{field}: {shapeMessage}.");
            EntityVocabulary.Require(key.Ease, ClipEase.All, field + " key ease");
            key.Curve?.Validate();
            key.CurveY?.Validate();
            key.CurveZ?.Validate();
        }
    }

    /// <summary>
    /// Checks this clip against the model it will play on. Matching labels alone never establish compatibility. Saved assets
    /// must match the structure revision exactly; editor drafts pass <paramref name="exactRevision"/> false while a model's
    /// unsaved structural edit is pending, and are then held to the structural checks alone.
    /// </summary>
    public void Validate(ResolvedModel model, bool exactRevision = true)
    {
        Validate();
        var owner = $"Clip '{Id}'";
        var basis = model.Base;
        Require(Model == basis.Id, $"{owner} is for model '{Model}', not '{basis.Id}'.");
        Require(!exactRevision || StructureRevision == basis.StructureRevision, $"{owner} was authored against structure revision {StructureRevision} of '{Model}'; the model is at revision {basis.StructureRevision}.");
        var solved = model.Chains.Values.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        void Scale(string scale, string field)
        {
            if (scale == CharacterModel.Unit)
            {
                return;
            }

            Require(model.Measures.ContainsKey(scale), $"{field}: the model has no measure '{scale}'.");
            Require(Reference.ContainsKey(scale), $"{field}: no reference measurement for '{scale}'.");
        }
        Scale(Travel.Scale, $"{owner} travel");
        foreach (var track in Attachments)
        {
            Require(basis.Slots.Any(s => s.Id == track.Slot), $"{owner}: unknown slot '{track.Slot}'.");
            foreach (var key in track.Keys.Where(k => k.Attachment is not null))
            {
                Require(basis.Skins.Any(s => s.Attachments.GetValueOrDefault(track.Slot)?.ContainsKey(key.Attachment!) == true), $"{owner}: unknown attachment '{key.Attachment}' for slot '{track.Slot}'.");
            }
        }
        foreach (var track in Colors)
        {
            Require(basis.Slots.Any(s => s.Id == track.Slot), $"{owner}: unknown color slot '{track.Slot}'.");
        }

        foreach (var key in DrawOrder)
        {
            Require(key.Slots.Count == basis.Slots.Count && key.Slots.Distinct().Count() == key.Slots.Count && key.Slots.All(id => basis.Slots.Any(s => s.Id == id)), $"{owner}: draw order must contain every slot exactly once.");
        }

        foreach (var track in Tracks)
        {
            var field = $"{owner} {track.Kind} track '{track.Target}'";
            if (track.Kind == OrientKind)
            {
                Require(basis.Sockets.Any(s => s.Id == track.Target), $"{field}: unknown socket.");
                Require(track.Scale is null or CharacterModel.Unit, $"{field}: orientation cannot scale with a measurement.");
            }
            else if (track.Kind == TargetKind)
            {
                Require(model.Chains.TryGetValue(track.Target, out var chain), $"{field}: unknown chain.");
                Scale(track.Scale ?? chain.Scale, field);
            }
            else
            {
                Require(model.Controls.TryGetValue(track.Target, out var control), $"{field}: unknown control.");
                Require(!solved.Contains(track.Target), $"{field}: this control is solved by IK; key its chain's target instead.");
                Require(track.Kind is not (RotateKind or BendKind) || !model.Chains.Values.Any(c => c.Solver == ModelChain.BoneSolver && c.Root == track.Target), $"{field}: this bone's rotation is solved by IK; key its constraint target instead.");
                if (track.Kind == BendKind)
                {
                    // The normalized bend channel: its owner is the node's declared limit, and it owns the rotation in this clip.
                    Require(control.Node == RigNodeKinds.Bone && control.Limit is not null, $"{field}: a normalized bend needs a bone node with declared joint limits; key its rotation as an angle instead.");
                    Require(!Tracks.Any(t => t.Kind == RotateKind && t.Target == track.Target), $"{field}: the node's rotation is also keyed as an angle; a clip keys a bend or an angle on one node, not both.");
                    Require(track.Keys.All(k => k.Value.X is >= 0 and <= 1 && k.Value.Y == 0 && k.Value.Z == 0), $"{field}: bend keys use x in 0..1 only.");
                    Require(track.Scale is null or CharacterModel.Unit, $"{field}: a normalized bend has no measurement scale.");
                }
                else if (track.Kind is ScaleKind or ShearKind)
                {
                    // Declared semantics (ChannelSemantics): on a canonical node a scale track is one uniform pose multiplier,
                    // so its keys must agree in x and y; shear is artwork fit on the legacy affine adapter only.
                    Require(track.Kind == ScaleKind || control.Node == RigNodeKinds.Affine, $"{field}: a shear channel belongs to the legacy affine adapter; a canonical node is a similarity with a length.");
                    Require(control.Node == RigNodeKinds.Affine || track.Keys.All(k => k.Value.X == k.Value.Y && k.Value.Z == 0),
                        $"{field}: a canonical node's scale is a uniform pose scale; key equal x and y (nonuniform fitting belongs to artwork, not the rig).");
                    Require(track.Scale is null or CharacterModel.Unit, $"{field}: scale and shear cannot use proportion measurements.");
                }
                else
                {
                    Scale(track.Scale ?? control.Scale, field);
                }
            }
        }
        foreach (var contact in Contacts)
        {
            var field = $"{owner} contact on '{contact.Chain}'";
            Require(model.Chains.TryGetValue(contact.Chain, out var chain), $"{field}: unknown chain.");
            Require(chain.Frame == CharacterModel.Locomotion, $"{field}: contacts need a chain keyed in the locomotion frame.");
            Require(chain.Scale == Travel.Scale, $"{field}: the chain's scale '{chain.Scale}' must match the travel scale '{Travel.Scale}'.");
        }
        foreach (var face in Faces)
        {
            // The face payload owns the track's target: only a part carrying one can be animated by it.
            Require(model.Parts.Any(p => p.Id == face.Part && p.Face is not null), $"{owner} face track '{face.Part}': the model has no part with a face by that ID.");
        }
    }
}
