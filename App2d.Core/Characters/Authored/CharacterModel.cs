using App2d.Core.Mathematics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>Rest is in model space; the parent-local rest offset is derived. Scale names the measure this control's animated translation scales with.</summary>
public sealed record ModelControl
{
    public string Id { get; set; } = "";
    public string? Parent { get; set; }
    public string? Name { get; set; }
    /// <summary>Explicit parent-local affine setup pose. Null keeps the existing model-space point-control convention.</summary>
    public Affine2D? Transform { get; set; }
    public PuppetPoint Rest { get; set; }
    /// <summary>World XY direction of this bone in rest, in radians. Zero preserves legacy point controls.</summary>
    public float RestAngle { get; set; }
    /// <summary>Bone length along local +X. Zero is an ordinary point control.</summary>
    public float Length { get; set; }
    public string Scale { get; set; } = CharacterModel.Unit;
}

/// <summary>A typed rig operation. The discriminator selects code; references and parameters belong to the resource.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ModelChain), "two-bone-ik")]
public abstract record RigConstraint
{
    public string Id { get; set; } = "";
}

/// <summary>A two-bone IK constraint. Legacy chains use the point solver; explicit bones opt into frame propagation.</summary>
public sealed record ModelChain : RigConstraint
{
    public const string PointSolver = "point", BoneSolver = "bone";
    public string Solver { get; set; } = PointSolver;
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
/// Frame's accumulated rotation, plus OffsetZ in depth. Angle supplies its rest XY direction; orient tracks add local 3D rotation. Not structural.
/// </summary>
public sealed record ModelSocket
{
    public string Id { get; set; } = "";
    public string Control { get; set; } = "";
    /// <summary>The control whose rotation orients the socket. Null uses Control's own; locomotion keeps the screen axes.</summary>
    public string? Frame { get; set; }
    /// <summary>When set, local +Y points from Control to this control in XY. Useful for clothing following a torso segment.</summary>
    public string? Toward { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
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
    public const int CurrentVersion = 4;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Changes when controls, parents, chains or frames change; proportions and appearance never change it.</summary>
    public int StructureRevision { get; set; } = 1;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    /// <summary>The explicit build rule behind this model's exposed build values, such as "person". Null exposes none.</summary>
    public string? Build { get; set; }
    public List<ModelControl> Controls { get; set; } = [];
    /// <summary>Legacy point IK definitions, evaluated first without changing their historical semantics.</summary>
    public List<ModelChain> Chains { get; set; } = [];
    public List<RigConstraint> Constraints { get; set; } = [];
    /// <summary>The compatibility view used by authoring, animation and runtime. IDs are unique across both collections.</summary>
    [JsonIgnore] public IEnumerable<ModelChain> IkChains => Chains.Concat(Constraints.OfType<ModelChain>());
    public List<ModelMeasure> Measures { get; set; } = [];
    public List<PuppetPart> Parts { get; set; } = [];
    public List<SkeletonSlot2D> Slots { get; set; } = [];
    public List<SkeletonSkin2D> Skins { get; set; } = [];
    public List<ModelSocket> Sockets { get; set; } = [];
    public List<MotionSet> MotionSets { get; set; } = [];
    public List<HurtLayout> HurtLayouts { get; set; } = [];
    public List<ControlGroup> Groups { get; set; } = [];
    public List<LookPreset> Looks { get; set; } = [];
    /// <summary>The prototype puppet this model was converted from, if any.</summary>
    public AssetSource? Source { get; set; }

    public string ToJson() => PartAssetJson.Write(this, Parts, AuthoredJson.Options);
    internal static CharacterModel FromDraftJson(string json)
    {
        PartAssetJson.RequireTypedJson(json);
        var model = AuthoredAsset.Parse<CharacterModel>(json, "model");
        if (model.Version is 2 or 3) model.Version = CurrentVersion;
        PartAssetJson.Restore(model.Parts);
        return model;
    }
    public static CharacterModel FromJson(string json) { var model = FromDraftJson(json); model.Validate(); return model; }
    public static CharacterModel Load(string path) => FromJson(File.ReadAllText(path));
    public void Save(string path) { Validate(); AuthoredAsset.Write(path, ToJson()); }

    private static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }

    public void Validate()
    {
        var owner = $"Model '{Id}'";
        Require(Format == FormatId && Version == CurrentVersion, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "model id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        Require(Controls is not null && Chains is not null && Constraints is not null && Measures is not null && Parts is not null && Sockets is not null && MotionSets is not null && HurtLayouts is not null && Groups is not null && Looks is not null, $"{owner}: collections cannot be null.");
        Require(Controls.Count <= 256 && Chains.Count + Constraints.Count <= 64 && Measures.Count <= 64 && Parts.Count <= 512, $"{owner}: capacity exceeded.");
        Require(Constraints.All(c => c is ModelChain), $"{owner}: null or unsupported constraint.");
        Require(Chains.All(c => c?.Solver == ModelChain.PointSolver), $"{owner}: legacy chains require the point solver; declare bone IK in constraints.");
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
            control.Transform?.Validate($"{owner} bone '{control.Id}'");
            new Limit(-1000, 1000).Check(control.RestAngle, $"{owner} control '{control.Id}' restAngle");
            new Limit(0, 100).Check(control.Length, $"{owner} control '{control.Id}' length");
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
            Require(measure?.Path is not null, $"{owner}: incomplete measure.");
            AuthoredAsset.RequireId(measure.Id, $"{owner} measure id");
            Require(measure.Id != Unit && scales.Add(measure.Id), $"{owner}: duplicate or reserved measure '{measure.Id}'.");
            Require(measure.Path.Count >= 2 && measure.Path.All(Known), $"{owner}: measure '{measure.Id}' needs a path of at least two known controls.");
        }
        foreach (var control in Controls) Require(scales.Contains(control.Scale), $"{owner}: control '{control.Id}' uses unknown scale '{control.Scale}'.");

        var chainIds = new HashSet<string>(StringComparer.Ordinal); var solved = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chain in IkChains)
        {
            Require(chain is not null, $"{owner}: null chain.");
            AuthoredAsset.RequireId(chain.Id, $"{owner} chain id");
            Require(chainIds.Add(chain.Id), $"{owner}: duplicate chain '{chain.Id}'.");
            Require(chain.Bend is -1 or 1, $"{owner}: chain '{chain.Id}' bend must be -1 or 1.");
            Require(Known(chain.Root) && Known(chain.Joint) && Known(chain.End), $"{owner}: chain '{chain.Id}' references an unknown control.");
            Require(controls[chain.Joint].Parent == chain.Root && controls[chain.End].Parent == chain.Joint, $"{owner}: chain '{chain.Id}' needs two connected bones (root → joint → end).");
            Require(chain.Solver is ModelChain.PointSolver or ModelChain.BoneSolver, $"{owner}: chain '{chain.Id}' has unknown solver '{chain.Solver}'.");
            if (chain.Solver == ModelChain.PointSolver)
            {
                Require(controls[chain.Root].Length == 0 && controls[chain.Joint].Length == 0 && controls[chain.End].Length == 0 && controls[chain.Root].Transform is null && controls[chain.Joint].Transform is null && controls[chain.End].Transform is null,
                    $"{owner}: chain '{chain.Id}' uses explicit bone frames; choose a bone constraint instead of point IK.");
            }
            else
            {
                Require(controls[chain.Root].Length >= .01f && controls[chain.Joint].Length >= .01f, $"{owner}: bone constraint '{chain.Id}' needs positive root and joint lengths of at least 0.01.");
            }

            Require(solved.Add(chain.Joint) && solved.Add(chain.End), $"{owner}: chains cannot share solved controls ('{chain.Id}').");
            Require(scales.Contains(chain.Scale), $"{owner}: chain '{chain.Id}' uses unknown scale '{chain.Scale}'.");
            Require(chain.Frame == Locomotion || Known(chain.Frame), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' is not a control or '{Locomotion}'.");
        }
        IEnumerable<string> SelfAndAncestors(string id) { for (string? current = id; current is not null; current = controls[current].Parent) yield return current; }
        var writes = IkChains.SelectMany(c => (c.Solver == ModelChain.BoneSolver ? new[] { c.Root } : [c.Joint, c.End]).Select(id => (Chain: c, Id: id))).ToArray();
        foreach (var chain in IkChains)
        {
            Require(!SelfAndAncestors(chain.Root).Any(id => writes.Any(w => w.Chain != chain && w.Id == id)), $"{owner}: chain '{chain.Id}' overlaps or is nested inside another chain; nested IK is not supported.");
            Require(chain.Frame == Locomotion || !SelfAndAncestors(chain.Frame).Any(id => writes.Any(w => w.Id == id)), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' must not be moved by IK.");
        }
        BoneFrameIk.ValidateSetup(this);

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
            Require(Known(socket.Control) && (socket.Frame is null or Locomotion || Known(socket.Frame)), $"{owner}: socket '{socket.Id}' references an unknown control.");
            Require(socket.Toward is null || Known(socket.Toward) && socket.Toward != socket.Control, $"{owner}: socket '{socket.Id}' needs a different known toward control.");
            new Limit(-100, 100).Check(socket.OffsetX, $"{owner} socket '{socket.Id}' offsetX"); new Limit(-100, 100).Check(socket.OffsetY, $"{owner} socket '{socket.Id}' offsetY");
            new Limit(-100, 100).Check(socket.OffsetZ, $"{owner} socket '{socket.Id}' offsetZ");
            new Limit(-10, 10).Check(socket.Angle, $"{owner} socket '{socket.Id}' angle");
        }
        var sets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in MotionSets)
        {
            Require(set?.Roles is not null, $"{owner}: incomplete motion set.");
            AuthoredAsset.RequireId(set.Id, $"{owner} motion set id");
            Require(sets.Add(set.Id), $"{owner}: duplicate motion set '{set.Id}'.");
            Require(!string.IsNullOrWhiteSpace(set.Name), $"{owner} motion set '{set.Id}': a name is required.");
            foreach (var (role, clip) in set.Roles) { AuthoredAsset.RequireId(role, $"{owner} motion set '{set.Id}' role"); AuthoredAsset.RequireId(clip, $"{owner} motion set '{set.Id}' role '{role}' clip"); }
        }
        var layouts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layout in HurtLayouts)
        {
            Require(layout?.Regions is not null, $"{owner}: incomplete hurt layout.");
            AuthoredAsset.RequireId(layout.Id, $"{owner} hurt layout id");
            Require(layouts.Add(layout.Id), $"{owner}: duplicate hurt layout '{layout.Id}'.");
            var regions = new HashSet<string>(StringComparer.Ordinal);
            foreach (var region in layout.Regions)
            {
                var field = $"{owner} hurt layout '{layout.Id}' region";
                Require(region?.Controls is not null, $"{field}: incomplete region.");
                AuthoredAsset.RequireId(region.Id, field + " id");
                Require(regions.Add(region.Id), $"{field}: duplicate '{region.Id}'.");
                Require(region.Controls.Count > 0 && region.Controls.All(Known), $"{field} '{region.Id}': needs at least one known control.");
                new Limit(0, 10).Check(region.Pad, $"{field} '{region.Id}' pad");
            }
        }
        var groups = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in Groups)
        {
            Require(group?.Targets is not null, $"{owner}: incomplete control group.");
            AuthoredAsset.RequireId(group.Id, $"{owner} group id");
            Require(groups.Add(group.Id), $"{owner}: duplicate group '{group.Id}'.");
            foreach (var target in group.Targets)
            {
                Require(Known(target) || target is not null && chainIds.Contains(target), $"{owner} group '{group.Id}': '{target}' is not a control or chain.");
                // A chain moves its joint and end; owning one of them without the chain would split it between two layers.
                Require(!solved.Contains(target), $"{owner} group '{group.Id}': '{target}' is solved by IK; name its chain instead.");
            }
        }
        var looks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var look in Looks)
        {
            Require(look?.Parts is not null, $"{owner}: incomplete look.");
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
        SkeletonAppearance2D.Validate(this);
        if (Build is not null) BuildRules.Get(Build).Check(this);
    }

    /// <summary>Checks lengths that depend on rest geometry, so a variant's overrides are held to the same rules as its base.</summary>
    public static void CheckGeometry(CharacterModel model, IReadOnlyDictionary<string, Vector3> rest, string owner)
    {
        float Length(string a, string b) => Vector2.Distance(new(rest[a].X, rest[a].Y), new(rest[b].X, rest[b].Y));
        foreach (var chain in model.IkChains.Where(c => c.Solver == ModelChain.PointSolver))
            Require(Length(chain.Root, chain.Joint) >= .01f && Length(chain.Joint, chain.End) >= .01f, $"{owner}: chain '{chain.Id}' needs segment lengths of at least 0.01.");
        foreach (var measure in model.Measures)
            Require(measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Length(p.First, p.Second)) >= .001f, $"{owner}: measure '{measure.Id}' has no length.");
    }
}
