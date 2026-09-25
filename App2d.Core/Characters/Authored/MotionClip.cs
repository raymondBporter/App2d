using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>A keyed value at a time. Translate, target and travel keys use X/Y/Z; rotate keys use Angle (radians).</summary>
public sealed record ClipKey
{
    public float Time { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Angle { get; set; }
}

/// <summary>
/// translate: a control's parent-local offset delta. rotate: a control's XY rotation, inherited by its children.
/// target: an IK chain's end-target delta in the chain's frame. Deltas are from rest, in reference units.
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
    public static readonly IReadOnlyList<string> TrackKinds = [TranslateKind, RotateKind, TargetKind];
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
        Require(Reference is not null && Travel is not null && Travel.Keys is not null && Tracks is not null && Contacts is not null, $"{owner}: collections cannot be null.");
        Require(Travel.Scale is not null, $"{owner} travel: a scale is required.");
        foreach (var (measure, length) in Reference) { AuthoredAsset.RequireId(measure, $"{owner} reference"); new Limit(.001f, 1000).Check(length, $"{owner} reference.{measure}"); }
        CheckKeys(Travel.Keys, $"{owner} travel", k => k.Z == 0 && k.Angle == 0, "travel keys use x and y only");
        var seen = new HashSet<(string, string)>();
        foreach (var track in Tracks)
        {
            Require(track is not null && track.Keys is not null && track.Target is not null, $"{owner}: incomplete track.");
            EntityVocabulary.Require(track.Kind, TrackKinds, $"{owner} track kind");
            Require(seen.Add((track.Kind, track.Target)), $"{owner}: duplicate {track.Kind} track for '{track.Target}'.");
            var rotate = track.Kind == RotateKind;
            CheckKeys(track.Keys, $"{owner} {track.Kind} track '{track.Target}'",
                rotate ? k => k.X == 0 && k.Y == 0 && k.Z == 0 : k => k.Angle == 0, rotate ? "rotate keys use angle only" : "keys use x, y and z only");
        }
        foreach (var contact in Contacts)
        {
            Require(contact is not null && contact.Chain is not null, $"{owner}: incomplete contact.");
            var field = $"{owner} contact on '{contact.Chain}'";
            new Limit(0, Duration).Check(contact.Start, field + " start"); new Limit(0, Duration).Check(contact.Finish, field + " finish");
            Require(contact.Finish > contact.Start, $"{field}: finish must follow start.");
            contact.Target.Check(field + " target");
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
        }
    }

    /// <summary>Checks this clip against the model it will play on. Matching labels alone never establish compatibility.</summary>
    public void Validate(ResolvedModel model)
    {
        Validate();
        var owner = $"Clip '{Id}'"; var basis = model.Base;
        Require(Model == basis.Id, $"{owner} is for model '{Model}', not '{basis.Id}'.");
        Require(StructureRevision == basis.StructureRevision, $"{owner} was authored against structure revision {StructureRevision} of '{Model}'; the model is at revision {basis.StructureRevision}.");
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
            if (track.Kind == TargetKind)
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
    }
}
