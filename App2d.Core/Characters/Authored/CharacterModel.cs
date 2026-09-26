using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Rest is in model space; the parent-local rest offset is derived. Scale names the measure this control's animated translation scales with.</summary>
public sealed record ModelControl
{
    public string Id { get; set; } = "";
    public string? Parent { get; set; }
    public PuppetPoint Rest { get; set; }
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A two-bone IK chain. Its end target is keyed in Frame: the locomotion frame, or a control that no chain moves.</summary>
public sealed record ModelChain
{
    public string Id { get; set; } = "";
    public string Root { get; set; } = "";
    public string Joint { get; set; } = "";
    public string End { get; set; } = "";
    public int Bend { get; set; } = 1;
    public string Frame { get; set; } = CharacterModel.Locomotion;
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A named length: the sum of rest XY distances along a path of controls, such as leg reach.</summary>
public sealed record ModelMeasure
{
    public string Id { get; set; } = "";
    public List<string> Path { get; set; } = [];
}

/// <summary>
/// A named attachment frame for faces, equipment and collision. Its origin is Control plus (OffsetX, OffsetY) turned by
/// Frame's accumulated rotation; its axis points at Angle radians from that frame's X axis. Not structural.
/// </summary>
public sealed record ModelSocket
{
    public string Id { get; set; } = "";
    public string Control { get; set; } = "";
    /// <summary>The control whose rotation orients the socket. Null uses Control's own.</summary>
    public string? Frame { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float Angle { get; set; }
}

/// <summary>Role-to-animation assignments, such as idle → person-idle. Sets may share clips; there is no set inheritance.</summary>
public sealed record MotionSet
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, string> Roles { get; set; } = [];
}

/// <summary>A hurt region suggestion: the XY bounds of some controls, grown by Pad. Geometry only, no damage.</summary>
public sealed record HurtShape
{
    public string Id { get; set; } = "";
    public List<string> Controls { get; set; } = [];
    public float Pad { get; set; } = .1f;
}

/// <summary>A reusable hurt-region layout. An entity selects one and may override or disable individual regions.</summary>
public sealed record HurtLayout
{
    public string Id { get; set; } = "";
    public List<HurtShape> Regions { get; set; } = [];
}

/// <summary>
/// A named set of controls and chains, such as "upper": the channels a masked action owns over locomotion. A chain is owned
/// whole. Not structural: a group only selects existing targets.
/// </summary>
public sealed record ControlGroup
{
    public string Id { get; set; } = "";
    public List<string> Targets { get; set; } = [];
}

/// <summary>A named appearance an author applies to a variant: part overrides it writes, never a live parent.</summary>
public sealed record LookPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, PartOverride> Parts { get; set; } = [];
}

/// <summary>A reusable character structure. Controls without a parent hang from the locomotion frame.</summary>
public sealed class CharacterModel
{
    public const string FormatId = "app2d-model", Locomotion = "locomotion", Unit = "unit";
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Changes when controls, parents, chains or frames change; proportions and appearance never change it.</summary>
    public int StructureRevision { get; set; } = 1;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    /// <summary>The explicit build rule behind this model's exposed build values, such as "person". Null exposes none.</summary>
    public string? Build { get; set; }
    public List<ModelControl> Controls { get; set; } = [];
    public List<ModelChain> Chains { get; set; } = [];
    public List<ModelMeasure> Measures { get; set; } = [];
    public List<PuppetPart> Parts { get; set; } = [];
    public List<ModelSocket> Sockets { get; set; } = [];
    public List<MotionSet> MotionSets { get; set; } = [];
    public List<HurtLayout> HurtLayouts { get; set; } = [];
    public List<ControlGroup> Groups { get; set; } = [];
    public List<LookPreset> Looks { get; set; } = [];
    /// <summary>The prototype puppet this model was converted from, if any.</summary>
    public AssetSource? Source { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    public static CharacterModel FromJson(string json) { var model = AuthoredAsset.Parse<CharacterModel>(json, "model"); model.Validate(); return model; }
    public static CharacterModel Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Model '{Id}'";
        Require(Format == FormatId && Version == 1, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "model id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        Require(Controls is not null && Chains is not null && Measures is not null && Parts is not null && Sockets is not null && MotionSets is not null && HurtLayouts is not null && Groups is not null && Looks is not null, $"{owner}: collections cannot be null.");
        Require(Controls.Count <= 256 && Chains.Count <= 64 && Measures.Count <= 64 && Parts.Count <= 512, $"{owner}: capacity exceeded.");
        Limit.Color(Ink, "ink"); new Limit(.001f, 1).Check(LineWidth, "lineWidth");
        Source?.Validate(owner);

        var controls = new Dictionary<string, ModelControl>(StringComparer.Ordinal);
        foreach (var control in Controls)
        {
            Require(control is not null, $"{owner}: null control.");
            Require(control.Id != Locomotion && control.Id != Unit, $"{owner}: '{control.Id}' is a reserved name.");
            AuthoredAsset.RequireId(control.Id, $"{owner} control id");
            Require(controls.TryAdd(control.Id, control), $"{owner}: duplicate control '{control.Id}'.");
            control.Rest.Check($"{owner} control '{control.Id}' rest");
        }
        foreach (var control in Controls)
            Require(control.Parent is null || control.Parent != control.Id && controls.ContainsKey(control.Parent), $"{owner}: control '{control.Id}' has unknown parent '{control.Parent}'.");
        foreach (var control in Controls)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var current = control; current.Parent is not null; current = controls[current.Parent])
                Require(seen.Add(current.Id), $"{owner}: parent cycle at '{control.Id}'.");
        }

        // Hand-edited files may hold a null where an ID belongs; treat it as unknown rather than letting a lookup throw.
        bool Known(string? id) => id is not null && controls.ContainsKey(id);
        var scales = new HashSet<string>(StringComparer.Ordinal) { Unit };
        foreach (var measure in Measures)
        {
            Require(measure is not null && measure.Path is not null, $"{owner}: incomplete measure.");
            AuthoredAsset.RequireId(measure.Id, $"{owner} measure id");
            Require(measure.Id != Unit && scales.Add(measure.Id), $"{owner}: duplicate or reserved measure '{measure.Id}'.");
            Require(measure.Path.Count >= 2 && measure.Path.All(Known), $"{owner}: measure '{measure.Id}' needs a path of at least two known controls.");
        }
        foreach (var control in Controls) Require(scales.Contains(control.Scale), $"{owner}: control '{control.Id}' uses unknown scale '{control.Scale}'.");

        var chainIds = new HashSet<string>(StringComparer.Ordinal); var solved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in Chains)
        {
            Require(chain is not null, $"{owner}: null chain.");
            AuthoredAsset.RequireId(chain.Id, $"{owner} chain id");
            Require(chainIds.Add(chain.Id), $"{owner}: duplicate chain '{chain.Id}'.");
            Require(chain.Bend is -1 or 1, $"{owner}: chain '{chain.Id}' bend must be -1 or 1.");
            Require(Known(chain.Root) && Known(chain.Joint) && Known(chain.End), $"{owner}: chain '{chain.Id}' references an unknown control.");
            Require(controls[chain.Joint].Parent == chain.Root && controls[chain.End].Parent == chain.Joint, $"{owner}: chain '{chain.Id}' needs two connected bones (root → joint → end).");
            Require(solved.Add(chain.Joint) && solved.Add(chain.End), $"{owner}: chains cannot share solved controls ('{chain.Id}').");
            Require(scales.Contains(chain.Scale), $"{owner}: chain '{chain.Id}' uses unknown scale '{chain.Scale}'.");
            Require(chain.Frame == Locomotion || Known(chain.Frame), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' is not a control or '{Locomotion}'.");
        }
        IEnumerable<string> SelfAndAncestors(string id) { for (string? current = id; current is not null; current = controls[current].Parent) yield return current; }
        foreach (var chain in Chains)
        {
            Require(!SelfAndAncestors(chain.Root).Any(solved.Contains), $"{owner}: chain '{chain.Id}' is nested inside another chain; nested IK is not supported.");
            Require(chain.Frame == Locomotion || !SelfAndAncestors(chain.Frame).Any(solved.Contains), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' must not be moved by IK.");
        }

        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            Require(part is not null, $"{owner}: null part.");
            AuthoredAsset.RequireId(part.Id, $"{owner} part id");
            Require(parts.Add(part.Id), $"{owner}: duplicate part '{part.Id}'.");
            part.Validate(Known);
        }
        var sockets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var socket in Sockets)
        {
            Require(socket is not null, $"{owner}: null socket.");
            AuthoredAsset.RequireId(socket.Id, $"{owner} socket id");
            Require(sockets.Add(socket.Id), $"{owner}: duplicate socket '{socket.Id}'.");
            Require(Known(socket.Control) && (socket.Frame is null || Known(socket.Frame)), $"{owner}: socket '{socket.Id}' references an unknown control.");
            new Limit(-100, 100).Check(socket.OffsetX, $"{owner} socket '{socket.Id}' offsetX"); new Limit(-100, 100).Check(socket.OffsetY, $"{owner} socket '{socket.Id}' offsetY");
            new Limit(-10, 10).Check(socket.Angle, $"{owner} socket '{socket.Id}' angle");
        }
        var sets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in MotionSets)
        {
            Require(set is not null && set.Roles is not null, $"{owner}: incomplete motion set.");
            AuthoredAsset.RequireId(set.Id, $"{owner} motion set id");
            Require(sets.Add(set.Id), $"{owner}: duplicate motion set '{set.Id}'.");
            Require(!string.IsNullOrWhiteSpace(set.Name), $"{owner} motion set '{set.Id}': a name is required.");
            foreach (var (role, clip) in set.Roles) { AuthoredAsset.RequireId(role, $"{owner} motion set '{set.Id}' role"); AuthoredAsset.RequireId(clip, $"{owner} motion set '{set.Id}' role '{role}' clip"); }
        }
        var layouts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layout in HurtLayouts)
        {
            Require(layout is not null && layout.Regions is not null, $"{owner}: incomplete hurt layout.");
            AuthoredAsset.RequireId(layout.Id, $"{owner} hurt layout id");
            Require(layouts.Add(layout.Id), $"{owner}: duplicate hurt layout '{layout.Id}'.");
            var regions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in layout.Regions)
            {
                var field = $"{owner} hurt layout '{layout.Id}' region";
                Require(region is not null && region.Controls is not null, $"{field}: incomplete region.");
                AuthoredAsset.RequireId(region.Id, field + " id");
                Require(regions.Add(region.Id), $"{field}: duplicate '{region.Id}'.");
                Require(region.Controls.Count > 0 && region.Controls.All(Known), $"{field} '{region.Id}': needs at least one known control.");
                new Limit(0, 10).Check(region.Pad, $"{field} '{region.Id}' pad");
            }
        }
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in Groups)
        {
            Require(group is not null && group.Targets is not null, $"{owner}: incomplete control group.");
            AuthoredAsset.RequireId(group.Id, $"{owner} group id");
            Require(groups.Add(group.Id), $"{owner}: duplicate group '{group.Id}'.");
            foreach (var target in group.Targets)
            {
                Require(Known(target) || target is not null && chainIds.Contains(target), $"{owner} group '{group.Id}': '{target}' is not a control or chain.");
                // A chain moves its joint and end; owning one of them without the chain would split it between two layers.
                Require(!solved.Contains(target!), $"{owner} group '{group.Id}': '{target}' is solved by IK; name its chain instead.");
            }
        }
        var looks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var look in Looks)
        {
            Require(look is not null && look.Parts is not null, $"{owner}: incomplete look.");
            AuthoredAsset.RequireId(look.Id, $"{owner} look id");
            Require(looks.Add(look.Id), $"{owner}: duplicate look '{look.Id}'.");
            Require(!string.IsNullOrWhiteSpace(look.Name), $"{owner} look '{look.Id}': a name is required.");
            foreach (var (part, change) in look.Parts)
            {
                Require(parts.Contains(part), $"{owner} look '{look.Id}': no part '{part}'.");
                PartOverride.Check(change, $"{owner} look '{look.Id}' parts.{part}");
            }
        }
        CheckGeometry(this, Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal), owner);
        if (Build is not null) BuildRules.Get(Build).Check(this);
    }

    /// <summary>Checks lengths that depend on rest geometry, so a variant's overrides are held to the same rules as its base.</summary>
    public static void CheckGeometry(CharacterModel model, IReadOnlyDictionary<string, Vector3> rest, string owner)
    {
        float Length(string a, string b) => Vector2.Distance(new(rest[a].X, rest[a].Y), new(rest[b].X, rest[b].Y));
        foreach (var chain in model.Chains)
            Require(Length(chain.Root, chain.Joint) >= .01f && Length(chain.Joint, chain.End) >= .01f, $"{owner}: chain '{chain.Id}' needs segment lengths of at least 0.01.");
        foreach (var measure in model.Measures)
            Require(measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Length(p.First, p.Second)) >= .001f, $"{owner}: measure '{measure.Id}' has no length.");
    }
}
