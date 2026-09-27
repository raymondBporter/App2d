using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters.Authored;

/// <summary>A keyed value at a time. Translate, target and travel keys use X/Y/Z; rotate keys use Angle (radians).</summary>
public sealed record ClipKey
{
    public float Time { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Angle { get; set; }
    /// <summary>How the value moves from this key to the next: see <see cref="ClipEase"/>.</summary>
    public string Ease { get; set; } = ClipEase.Linear;
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
    public static float Apply(string ease, float u) => ease switch { Smooth => u * u * (3 - 2 * u), Step => 0, _ => u };
}
public sealed class Easing
{
    private readonly Func<double, double> _easingFunction;

    private Easing(Func<double, double> easingFunction)
    {
        _easingFunction = easingFunction;
    }

    public double Sample(double time) => _easingFunction(time);
    public double SampleOverTime(double currentTime, double maxTime) => Sample(currentTime / maxTime);
    public static implicit operator Easing(Func<double, double> easingFunction) => new Easing(easingFunction);
    public static implicit operator Func<double, double>(Easing easingFunction) => easingFunction._easingFunction;
    public static readonly Easing Linear = new Easing(x => x);
    public static readonly Easing EaseInQuad = new Easing(x => x * x);
    public static readonly Easing EaseOutQuad = new Easing(x => 1 - (1 - x) * (1 - x));
    public static readonly Easing EaseInOutQuad = new Easing(x => x < 0.5 ? 2 * x * x : 1 - Math.Pow(-2 * x + 2, 2) / 2);
    public static readonly Easing EaseInCubic = new Easing(x => x * x * x);
    public static readonly Easing EaseOutCubic = new Easing(x => 1 - Math.Pow(1 - x, 3));
    public static readonly Easing EaseInOutCubic = new Easing(x => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2);
    public static readonly Easing EaseInQuart = new Easing(x => x * x * x * x);
    public static readonly Easing EaseOutQuart = new Easing(x => 1 - Math.Pow(1 - x, 4));
    public static readonly Easing EaseInOutQuart = new Easing(x => x < 0.5 ? 8 * x * x * x * x : 1 - Math.Pow(-2 * x + 2, 4) / 2);
    public static readonly Easing EaseInQuint = new Easing(x => x * x * x * x * x);
    public static readonly Easing EaseOutQuint = new Easing(x => 1 - Math.Pow(1 - x, 5));
    public static readonly Easing EaseInOutQuint = new Easing(x => x < 0.5 ? 16 * x * x * x * x * x : 1 - Math.Pow(-2 * x + 2, 5) / 2);
    public static readonly Easing EaseInSine = new Easing(x => 1 - Math.Cos((x * Math.PI) / 2));
    public static readonly Easing EaseOutSine = new Easing(x => Math.Sin((x * Math.PI) / 2));
    public static readonly Easing EaseInOutSine = new Easing(x => -(Math.Cos(Math.PI * x) - 1) / 2);
    public static readonly Easing EaseInExpo = new Easing(x => x == 0 ? 0 : Math.Pow(2, 10 * x - 10));
    public static readonly Easing EaseOutExpo = new Easing(x => x == 1 ? 1 : 1 - Math.Pow(2, -10 * x));
    public static readonly Easing EaseInOutExpo = new Easing(x => x == 0 ? 0 : x == 1 ? 1 : x < 0.5 ? Math.Pow(2, 20 * x - 10) / 2 : (2 - Math.Pow(2, -20 * x + 10)) / 2);
    public static readonly Easing EaseInCirc = new Easing(x => 1 - Math.Sqrt(1 - Math.Pow(x, 2)));
    public static readonly Easing EaseOutCirc = new Easing(x => Math.Sqrt(1 - Math.Pow(x - 1, 2)));
    public static readonly Easing EaseInOutCirc = new Easing(x => x < 0.5 ? (1 - Math.Sqrt(1 - Math.Pow(2 * x, 2))) / 2 : (Math.Sqrt(1 - Math.Pow(-2 * x + 2, 2)) + 1) / 2);

    private const double c1 = 1.70158;
    private const double c2 = c1 * 1.525;
    private const double c3 = c1 + 1;
    private const double c4 = (2 * Math.PI) / 3;
    private const double c5 = (2 * Math.PI) / 4.5;

    public static readonly Easing EaseInBack = new Easing(x => c3 * x * x * x - c1 * x * x);
    public static readonly Easing EaseOutBack = new Easing(x => 1 + c3 * Math.Pow(x - 1, 3) + c1 * Math.Pow(x - 1, 2));
    public static readonly Easing EaseInOutBack = new Easing(x => x < 0.5 ? (Math.Pow(2 * x, 2) * ((c2 + 1) * 2 * x - c2)) / 2 : (Math.Pow(2 * x - 2, 2) * ((c2 + 1) * (x * 2 - 2) + c2) + 2) / 2);
    public static readonly Easing EaseInElastic = new Easing(x => x == 0 ? 0 : x == 1 ? 1 : -Math.Pow(2, 10 * x - 10) * Math.Sin((x * 10 - 10.75) * c4));
    public static readonly Easing EaseOutElastic = new Easing(x => x == 0 ? 0 : x == 1 ? 1 : Math.Pow(2, -10 * x) * Math.Sin((x * 10 - 0.75) * c4) + 1);
    public static readonly Easing EaseInOutElastic = new Easing(x => x == 0 ? 0 : x == 1 ? 1 : x < 0.5 ? -(Math.Pow(2, 20 * x - 10) * Math.Sin((20 * x - 11.125) * c5)) / 2 : (Math.Pow(2, -20 * x + 10) * Math.Sin((20 * x - 11.125) * c5)) / 2 + 1);

    private static double BounceOut(double x)
    {
        double n1 = 7.5625;
        double d1 = 2.75;

        if (x < 1 / d1)
        {
            return n1 * x * x;
        }
        else if (x < 2 / d1)
        {
            x -= 1.5 / d1;
            return n1 * x * x + 0.75;
        }
        else if (x < 2.5 / d1)
        {
            x -= 2.25 / d1;
            return n1 * x * x + 0.9375;
        }
        else
        {
            x -= 2.625 / d1;
            return n1 * x * x + 0.984375;
        }
    }

    public static readonly Easing EaseInBounce = new Easing(x => 1 - BounceOut(1 - x));
    public static readonly Easing EaseOutBounce = new Easing(x => BounceOut(x));
    public static readonly Easing EaseInOutBounce = new Easing(x => x < 0.5 ? (1 - BounceOut(1 - 2 * x)) / 2 : (1 + BounceOut(2 * x - 1)) / 2);
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

/// <summary>A named animation compatible with one base model's structure revision. Owns no colors, proportions or gameplay.</summary>
public sealed class MotionClip
{
    public const string FormatId = "app2d-clip", TranslateKind = "translate", RotateKind = "rotate", TargetKind = "target";
    /// <summary>Socket-local Euler angles in radians: X twist, Y tilt, Z turn. Unwrapped angles preserve authored full turns.</summary>
    public const string OrientKind = "orient";
    public static readonly IReadOnlyList<string> TrackKinds = [TranslateKind, RotateKind, TargetKind, OrientKind];
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
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
    /// <summary>The imported motion this clip was converted from, if any. Null for clips authored here.</summary>
    public AssetSource? Source { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static MotionClip FromJson(string json) { var clip = AuthoredAsset.Parse<MotionClip>(json, "clip"); clip.Validate(); return clip; }
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Clip '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "clip id"); AuthoredAsset.RequireId(Model, $"{owner} model");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        new Limit(.05f, 60).Check(Duration, $"{owner} duration");
        Require(Reference is not null && Travel?.Keys is not null && Tracks is not null && Contacts is not null && Markers is not null && Faces is not null, $"{owner}: collections cannot be null.");
        Require(Travel.Scale is not null, $"{owner} travel: a scale is required.");
        Source?.Validate(owner);
        foreach (var (measure, length) in Reference) { AuthoredAsset.RequireId(measure, $"{owner} reference"); new Limit(.001f, 1000).Check(length, $"{owner} reference.{measure}"); }
        CheckKeys(Travel.Keys, $"{owner} travel", k => k.Z == 0 && k.Angle == 0, "travel keys use x and y only");
        var seen = new HashSet<(string, string)>();
        foreach (var track in Tracks)
        {
            Require(track?.Keys is not null && track.Target is not null, $"{owner}: incomplete track.");
            EntityVocabulary.Require(track.Kind, TrackKinds, $"{owner} track kind");
            Require(seen.Add((track.Kind, track.Target)), $"{owner}: duplicate {track.Kind} track for '{track.Target}'.");
            Require(track.Keys.All(k => k is null || k.Bend is null || track.Kind == TargetKind && k.Bend is 1 or -1), $"{owner} {track.Kind} track '{track.Target}': bend is 1 or -1, on target keys only.");
            var rotate = track.Kind == RotateKind;
            CheckKeys(track.Keys, $"{owner} {track.Kind} track '{track.Target}'",
                rotate ? k => k.X == 0 && k.Y == 0 && k.Z == 0 : k => k.Angle == 0, rotate ? "rotate keys use angle only" : "keys use x, y and z only");
        }
        foreach (var contact in Contacts)
        {
            Require(contact?.Chain is not null, $"{owner}: incomplete contact.");
            var field = $"{owner} contact on '{contact.Chain}'";
            new Limit(0, Duration).Check(contact.Start, field + " start"); new Limit(0, Duration).Check(contact.Finish, field + " finish");
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
                Require(key.Time > previous, $"{owner} face '{face.Part}': key times must be strictly increasing."); previous = key.Time;
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
            Require(key.Time > previous, $"{field}: key times must be strictly increasing."); previous = key.Time;
            foreach (var value in new[] { key.X, key.Y, key.Z, key.Angle }) new Limit(-1000, 1000).Check(value, field + " key value");
            Require(shape(key), $"{field}: {shapeMessage}.");
            EntityVocabulary.Require(key.Ease, ClipEase.All, field + " key ease");
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
        var owner = $"Clip '{Id}'"; var basis = model.Base;
        Require(Model == basis.Id, $"{owner} is for model '{Model}', not '{basis.Id}'.");
        Require(!exactRevision || StructureRevision == basis.StructureRevision, $"{owner} was authored against structure revision {StructureRevision} of '{Model}'; the model is at revision {basis.StructureRevision}.");
        var solved = basis.Chains.SelectMany(c => new[] { c.Joint, c.End }).ToHashSet(StringComparer.Ordinal);
        void Scale(string scale, string field)
        {
            if (scale == CharacterModel.Unit) return;
            Require(model.Measures.ContainsKey(scale), $"{field}: the model has no measure '{scale}'.");
            Require(Reference.ContainsKey(scale), $"{field}: no reference measurement for '{scale}'.");
        }
        Scale(Travel.Scale, $"{owner} travel");
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
                Scale(track.Scale ?? control.Scale, field);
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
            Require(model.Parts.Any(p => p.Id == face.Part && p.Kind != "stroke"), $"{owner} face track '{face.Part}': the model has no such shape part.");
    }
}
