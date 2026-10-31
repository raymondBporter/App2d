using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.IO;
using App2d.Core.Json;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters;

public sealed record PuppetControl
{
    public string Id { get; set; } = "point";
    public PuppetPoint Rest { get; set; }
}

public sealed record PuppetBone
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public sealed record PuppetChain
{
    public string Root { get; set; } = "";
    public string Joint { get; set; } = "";
    public string End { get; set; } = "";
    public int Bend { get; set; } = 1;
}

public sealed record PuppetKey
{
    public float Time { get; set; }
    public PuppetPoint Position { get; set; }
    public Dictionary<string, PuppetPoint> Points { get; set; } = [];
}

/// <summary>A world-space target during a closed contact interval. Looping advances the target by the clip's travel.</summary>
public sealed record PuppetContact
{
    public string End { get; set; } = "";
    public float Start { get; set; }
    public float Finish { get; set; } = .5f;
    public PuppetPoint Target { get; set; }
}

public sealed record PuppetMotion
{
    public string Name { get; set; } = "New motion";
    public float Duration { get; set; } = 1;
    public bool Loop { get; set; }
    public List<PuppetKey> Keys { get; set; } = [];
    public List<PuppetContact> Contacts { get; set; } = [];
}

public sealed class PuppetDefinition
{
    public const string FormatId = "app2d-puppet";
    /// <summary>The current prototype puppet schema; other versions are refused.</summary>
    public const int CurrentVersion = 5;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Name { get; set; } = "New character";
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    public List<PuppetControl> Controls { get; set; } = [];
    public List<PuppetBone> Bones { get; set; } = [];
    public List<PuppetChain> Chains { get; set; } = [];
    public List<PartGeometry> Parts { get; set; } = [];
    public List<PuppetMotion> Motions { get; set; } = [new()];
    public static JsonSerializerOptions JsonOptions => JsonProfiles.Puppet;

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public static PuppetDefinition FromJson(string json)
    {
        var definition = JsonSerializer.Deserialize<PuppetDefinition>(json, JsonOptions) ?? throw new InvalidDataException("Empty puppet file.");
        definition.Validate();
        return definition;
    }
    public void Save(string path)
    {
        Validate();
        AtomicFile.WriteAllText(path, ToJson());
    }

    public void Validate()
    {
        void Require([DoesNotReturnIf(false)] bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
            }
        }
        void Number(float value, float min, float max, string field) => new Limit(min, max).Check(value, field);
        void Point(PuppetPoint point, string field) => point.Check(field);
        Require(Format == FormatId && Version == CurrentVersion, "Unsupported puppet format/version.");
        Require(!string.IsNullOrWhiteSpace(Name), "A character name is required.");
        Require(Controls is not null && Bones is not null && Chains is not null && Parts is not null && Motions is not null, "Puppet collections cannot be null.");
        Require(Controls.Count <= 256 && Parts.Count <= 512 && Motions.Count is > 0 and <= 128, "Puppet capacity exceeded or no motions defined.");
        Limit.Color(Ink, "ink");
        Number(LineWidth, .001f, 1, "lineWidth");
        var controls = new Dictionary<string, PuppetControl>(StringComparer.Ordinal);
        foreach (var control in Controls)
        {
            Require(control is not null && !string.IsNullOrWhiteSpace(control.Id), "Every control needs an ID.");
            Require(controls.TryAdd(control.Id, control), "Duplicate control: " + control.Id);
            Point(control.Rest, control.Id);
        }
        void Reference(string? id) => Require(id is not null && controls.ContainsKey(id), "Unknown control: " + id);
        var parents = new Dictionary<string, string>();
        foreach (var bone in Bones)
        {
            Require(bone is not null, "Null bone.");
            Reference(bone.From);
            Reference(bone.To);
            Require(bone.From != bone.To && parents.TryAdd(bone.To, bone.From), "Bones must form a forest with one parent per control.");
        }
        foreach (var id in controls.Keys)
        {
            var seen = new HashSet<string>();
            var current = id;
            while (parents.TryGetValue(current, out var parent))
            {
                Require(seen.Add(current), "Bone cycle at " + current);
                current = parent;
            }
        }
        var solvedControls = new HashSet<string>();
        foreach (var chain in Chains)
        {
            Require(chain is not null, "Null IK chain.");
            Reference(chain.Root);
            Reference(chain.Joint);
            Reference(chain.End);
            Require(chain.Bend is -1 or 1, "IK bend must be -1 or 1.");
            Require(parents.GetValueOrDefault(chain.Joint) == chain.Root && parents.GetValueOrDefault(chain.End) == chain.Joint, "IK needs two connected bones.");
            Require(solvedControls.Add(chain.Joint) && solvedControls.Add(chain.End), "IK chains cannot share solved controls.");
            Number(Vector2.Distance(controls[chain.Root].Rest.XY, controls[chain.Joint].Rest.XY), .01f, 100, "IK first length");
            Number(Vector2.Distance(controls[chain.Joint].Rest.XY, controls[chain.End].Rest.XY), .01f, 100, "IK second length");
        }
        foreach (var chain in Chains)
        {
            var current = chain.Root;
            while (true)
            {
                Require(!solvedControls.Contains(current), "Nested IK chains are not supported yet.");
                if (!parents.TryGetValue(current, out var parent))
                {
                    break;
                }

                current = parent;
            }
        }
        var partIds = new HashSet<string>();
        foreach (var part in Parts)
        {
            Require(part is not null && !string.IsNullOrWhiteSpace(part.Id) && partIds.Add(part.Id), "Part IDs must be nonempty and unique.");
            part.Validate(controls.ContainsKey);
            Require(part.Frame is null, "Prototype puppet parts cannot use authored bone frames.");
        }
        foreach (var motion in Motions)
        {
            Require(motion?.Keys is not null && motion.Contacts is not null, "Incomplete motion.");
            Number(motion.Duration, .05f, 60, "motion.duration");
            Require(motion.Keys.Count <= 4096, "Too many keys.");
            var previous = -1f;
            foreach (var key in motion.Keys)
            {
                Require(key?.Points is not null, "Incomplete key.");
                Number(key.Time, 0, motion.Duration, "key.time");
                Require(key.Time > previous, "Key times must be strictly increasing.");
                previous = key.Time;
                Point(key.Position, "key.position");
                foreach (var (id, p) in key.Points)
                {
                    Reference(id);
                    Point(p, "key." + id);
                }
            }
            foreach (var contact in motion.Contacts)
            {
                Require(contact is not null && Chains.Any(c => c.End == contact.End), "Contacts need an IK end control.");
                Number(contact.Start, 0, motion.Duration, "contact.start");
                Number(contact.Finish, 0, motion.Duration, "contact.finish");
                Require(contact.Finish > contact.Start, "Contact finish must follow its start.");
                Point(contact.Target, "contact.target");
            }
            foreach (var group in motion.Contacts.GroupBy(c => c.End))
            {
                PuppetContact? previousContact = null;
                foreach (var contact in group.OrderBy(c => c.Start))
                {
                    Require(previousContact is null || previousContact.Finish < contact.Start, "Contact intervals for the same control must not overlap or share an endpoint.");
                    previousContact = contact;
                }
            }
        }
    }
}
