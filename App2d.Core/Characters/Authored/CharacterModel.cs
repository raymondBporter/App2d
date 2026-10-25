using App2d.Core.Mathematics.Transforms;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// One rig node. <see cref="Node"/> declares how its setup is authored (<see cref="RigNodeKinds"/>): a point control's
/// <see cref="Rest"/>/<see cref="RestAngle"/> are character-frame values the resolver turns into a parent-local similarity;
/// a bone node authors <see cref="Local"/>, <see cref="Length"/> and an optional <see cref="Link"/> and derives its rest; an
/// affine node keeps the legacy <see cref="Transform"/>. Scale names the measure this control's animated translation scales with.
/// </summary>
public sealed record ModelControl
{
    public string Id { get; set; } = "";
    public string? Parent { get; set; }
    public string? Name { get; set; }
    /// <summary>The declared setup kind, one of <see cref="RigNodeKinds"/>. Never inferred from the other members.</summary>
    public string Node { get; set; } = RigNodeKinds.Point;
    /// <summary>Legacy affine adapter setup; required by and only allowed on <see cref="RigNodeKinds.Affine"/> nodes.</summary>
    public Affine2D? Transform { get; set; }
    /// <summary>Canonical parent-local similarity setup of a <see cref="RigNodeKinds.Bone"/> node (null is the identity).</summary>
    public RigSetup? Local { get; set; }
    /// <summary>Binds a bone node's origin to a position along its parent bone instead of <see cref="Local"/>'s translation.</summary>
    public RigLink? Link { get; set; }
    /// <summary>Optional angular joint limits of a bone node (P4b): bounds on its parent-local rotation. A point or affine node has none.</summary>
    public RigLimit? Limit { get; set; }
    /// <summary>Character-frame rest of a point or affine node. Derived, and so omitted, for a bone node.</summary>
    public PuppetPoint Rest { get; set; }
    /// <summary>Character-frame XY direction of a point node's frame in rest, in radians. Derived for a bone node.</summary>
    public float RestAngle { get; set; }
    /// <summary>Bone length along local +X. Zero is an ordinary control, still useful as a handle, socket or target.</summary>
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

/// <summary>A two-bone IK constraint. Point controls use the point solver; explicit bones opt into frame propagation.</summary>
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
    /// <summary>The slot contract (P10b): the <see cref="PartAsset.Usage"/> of the parts this socket takes; null takes any.</summary>
    public string? Usage { get; set; }
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

/// <summary>
/// One part's appearance in a look: the material and visibility a look writes onto a variant as typed overrides
/// (<see cref="RigProperties.Material"/>, <see cref="RigProperties.Hidden"/>). Null leaves the variant's value alone. Never
/// sizes: a look restyles, it does not refit. A look's expressions sit on its animated instances (<see cref="LookPreset.Expressions"/>).
/// </summary>
public sealed record LookAppearance
{
    public RenderMaterialDefinition2D? Material { get; set; }
    public bool? Hidden { get; set; }
    [JsonIgnore]
    public bool IsEmpty => Material is null && Hidden is null;

    public static void Check(LookAppearance? part, string field)
    {
        if (part is null)
        {
            throw new InvalidDataException($"{field}: null look part.");
        }

        part.Material?.Validate(field + ".material");
    }

    /// <summary>The typed overrides this appearance writes on a part.</summary>
    public IEnumerable<PropertyOverride> Overrides(string part)
    {
        if (Material is not null) yield return PropertyOverride.Of(PropertyRef.OfPart(part, RigProperties.Material), Material);
        if (Hidden is { } hidden) yield return PropertyOverride.Of(PropertyRef.OfPart(part, RigProperties.Hidden), hidden);
    }
}

/// <summary>A named appearance an author applies to a variant: part appearance overrides and instance expressions it writes, never a live parent.</summary>
public sealed record LookPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, LookAppearance> Parts { get; set; } = [];
    /// <summary>The default expression each animated instance shows in this look (P7c): the attachment property <see cref="RigProperties.Expression"/>.</summary>
    public Dictionary<string, string> Expressions { get; set; } = [];
}

/// <summary>A reusable character structure. Controls without a parent hang from the locomotion frame.</summary>
public sealed class CharacterModel
{
    public const string FormatId = "app2d-model", Locomotion = "locomotion", Unit = "unit";
    /// <summary>
    /// Version 11 carries no face on a part (P7c: a face is an animated library part instanced on a socket, selected by
    /// expression word); version 10 carried the procedural face as the typed <c>face</c> payload (P2b) in place of the loose
    /// expression string and <c>faceX</c>; version 9 added <see cref="Attachments"/>, instances of
    /// library part assets (P5a); version 8 declared exposed <see cref="Parameters"/> with their bindings and
    /// <see cref="Presets"/> in place of the retired code build rule (P4a); version 7 declared each control's node kind,
    /// authored bone nodes as parent-local setups and kept one constraint list (P3a); version 6 moved a curve's wave into
    /// the typed <c>wave</c> modifier (P2a). Older files read through <see cref="LegacyModelReader"/>.
    /// </summary>
    public const int CurrentVersion = 11;
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Changes when controls, parents, chains or frames change; proportions and appearance never change it.</summary>
    public int StructureRevision { get; set; } = 1;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    /// <summary>
    /// The exposed parameters (plan section 3.5): stable IDs with units, defaults, ranges and simple bindings onto bone
    /// lengths, layout dimensions, part fitting and link positions. A variant stores values by ID; anatomy needs no code rule.
    /// </summary>
    public List<ModelParameter> Parameters { get; set; } = [];
    /// <summary>Named parameter value sets an author starts a variant from.</summary>
    public List<ParameterPreset> Presets { get; set; } = [];
    public List<ModelControl> Controls { get; set; } = [];
    /// <summary>The one ordered constraint list, evaluated in document order. A chain's solver declares its semantics.</summary>
    public List<RigConstraint> Constraints { get; set; } = [];
    /// <summary>The two-bone IK constraints, in evaluation order.</summary>
    [JsonIgnore] public IEnumerable<ModelChain> IkChains => Constraints.OfType<ModelChain>();
    public List<ModelMeasure> Measures { get; set; } = [];
    public List<PuppetPart> Parts { get; set; } = [];
    /// <summary>Shared surfaces selected by parts and variants through material.asset.</summary>
    public Dictionary<string, RenderMaterialDefinition2D> Materials { get; set; } = [];
    public List<SkeletonSlot2D> Slots { get; set; } = [];
    public List<SkeletonSkin2D> Skins { get; set; } = [];
    public List<ModelSocket> Sockets { get; set; } = [];
    /// <summary>Instances of library parts on this character's sockets (P5a), each with a stable instance ID that variants address.</summary>
    public List<PartInstance> Attachments { get; set; } = [];
    public List<MotionSet> MotionSets { get; set; } = [];
    public List<HurtLayout> HurtLayouts { get; set; } = [];
    public List<ControlGroup> Groups { get; set; } = [];
    public List<LookPreset> Looks { get; set; } = [];
    /// <summary>The prototype puppet this model was converted from, if any.</summary>
    public AssetSource? Source { get; set; }

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    internal static CharacterModel FromDraftJson(string json) => AuthoredAsset.Parse<CharacterModel>(LegacySourceFields.ReadModel(json), "model");
    public static CharacterModel FromJson(string json)
    {
        var model = FromDraftJson(json);
        model.Validate();
        return model;
    }
    public static CharacterModel Load(string path) => FromJson(File.ReadAllText(path));
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
        var owner = $"Model '{Id}'";
        Require(Format == FormatId && Version == CurrentVersion, $"{owner}: unsupported format/version.");
        AuthoredAsset.RequireId(Id, "model id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        Require(StructureRevision >= 1, $"{owner}: structureRevision must be at least 1.");
        Require(Materials is not null, $"{owner}: null materials.");
        foreach (var (id, material) in Materials)
        {
            AuthoredAsset.RequireId(id, $"{owner} material id");
            Require(material is not null, $"{owner}: null material '{id}'.");
            material.Resolve(Materials);
        }
        Require(Controls is not null && Constraints is not null && Measures is not null && Parts is not null && Sockets is not null && Attachments is not null && MotionSets is not null && HurtLayouts is not null && Groups is not null && Looks is not null, $"{owner}: collections cannot be null.");
        Require(Controls.Count <= 256 && Constraints.Count <= 64 && Measures.Count <= 64 && Parts.Count <= 512, $"{owner}: capacity exceeded.");
        Require(Constraints.All(c => c is ModelChain), $"{owner}: null or unsupported constraint.");
        Limit.Color(Ink, "ink");
        new Limit(.001f, 1).Check(LineWidth, "lineWidth");
        Source?.Validate(owner);

        var controls = new Dictionary<string, ModelControl>(StringComparer.Ordinal);
        foreach (var control in Controls)
        {
            Require(control is not null, $"{owner}: null control.");
            Require(control.Id != Locomotion && control.Id != Unit, $"{owner}: '{control.Id}' is a reserved name.");
            AuthoredAsset.RequireId(control.Id, $"{owner} control id");
            Require(controls.TryAdd(control.Id, control), $"{owner}: duplicate control '{control.Id}'.");
            var field = $"{owner} control '{control.Id}'";
            Require(RigNodeKinds.All.Contains(control.Node), $"{field}: unknown node kind '{control.Node}'.");
            new Limit(0, 100).Check(control.Length, field + " length");
            switch (control.Node)
            {
                case RigNodeKinds.Bone:
                    Require(control.Transform is null, $"{field}: a bone node has no affine transform.");
                    Require(control.Rest == default && control.RestAngle == 0, $"{field}: a bone node derives its rest from its local setup; author local, length and link instead.");
                    control.Local?.Validate(field + " local");
                    control.Link?.Validate(field + " link");
                    Require(control.Link is null || control.Parent is not null, $"{field}: a link needs a parent bone.");
                    Require(control.Link is null || control.Local is null || control.Local.X == 0 && control.Local.Y == 0, $"{field}: a linked origin cannot also author a local translation.");
                    control.Limit?.Validate(field + " limit");
                    Require(control.Limit is null || control.Limit.Contains(control.Local?.Rotation ?? 0), $"{field}: its rest rotation lies outside its joint limits; widen the limits or turn the rest.");
                    break;
                case RigNodeKinds.Affine:
                    Require(control.Transform is not null, $"{field}: an affine node needs its transform.");
                    Require(control.Local is null && control.Link is null && control.Limit is null, $"{field}: an affine node authors only its transform.");
                    control.Rest.Check(field + " rest");
                    control.Transform.Validate($"{owner} bone '{control.Id}'");
                    new Limit(-1000, 1000).Check(control.RestAngle, field + " restAngle");
                    break;
                default:
                    Require(control.Transform is null && control.Local is null && control.Link is null, $"{field}: a point control authors only rest and restAngle; declare node 'bone' or 'affine' for a local setup.");
                    Require(control.Limit is null, $"{field}: joint limits belong to bone nodes; convert the run to a bone chain first.");
                    control.Rest.Check(field + " rest");
                    new Limit(-1000, 1000).Check(control.RestAngle, field + " restAngle");
                    break;
            }
        }
        foreach (var control in Controls)
        {
            Require(control.Parent is null || control.Parent != control.Id && controls.ContainsKey(control.Parent), $"{owner}: control '{control.Id}' has unknown parent '{control.Parent}'.");
        }

        foreach (var control in Controls)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var current = control; current.Parent is not null; current = controls[current.Parent])
            {
                Require(seen.Add(current.Id), $"{owner}: parent cycle at '{control.Id}'.");
            }
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
        foreach (var control in Controls)
        {
            Require(scales.Contains(control.Scale), $"{owner}: control '{control.Id}' uses unknown scale '{control.Scale}'.");
        }

        var chainIds = new HashSet<string>(StringComparer.Ordinal);
        var solved = new HashSet<string>(StringComparer.Ordinal);
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
                // The legacy point-constraint adapter moves positions only. It never reads a declared length, so a
                // length is allowed only where the next control is bound to that tip and the two agree by construction.
                Require(controls[chain.Root].Node != RigNodeKinds.Affine && controls[chain.Joint].Node != RigNodeKinds.Affine && controls[chain.End].Node != RigNodeKinds.Affine,
                    $"{owner}: chain '{chain.Id}' uses affine bone frames; choose a bone constraint instead of point IK.");
                Require(TipBound(controls[chain.Root], controls[chain.Joint]) && TipBound(controls[chain.Joint], controls[chain.End]) && controls[chain.End].Length == 0,
                    $"{owner}: chain '{chain.Id}' declares bone lengths its point solver would ignore; bind each joint to its parent's tip or choose a bone constraint.");
                // The point solver moves positions without turning frames, so a limit on its solved joint or end would bound nothing it does.
                Require(controls[chain.Joint].Limit is null && controls[chain.End].Limit is null,
                    $"{owner}: chain '{chain.Id}' uses the point solver, which moves '{chain.Joint}' and '{chain.End}' without turning their frames; joint limits need a bone constraint.");
            }
            else
            {
                Require(controls[chain.Root].Length >= .01f && controls[chain.Joint].Length >= .01f, $"{owner}: bone constraint '{chain.Id}' needs positive root and joint lengths of at least 0.01.");
            }

            Require(solved.Add(chain.Joint) && solved.Add(chain.End), $"{owner}: chains cannot share solved controls ('{chain.Id}').");
            Require(scales.Contains(chain.Scale), $"{owner}: chain '{chain.Id}' uses unknown scale '{chain.Scale}'.");
            Require(chain.Frame == Locomotion || Known(chain.Frame), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' is not a control or '{Locomotion}'.");
        }
        IEnumerable<string> SelfAndAncestors(string id)
        {
            for (string? current = id; current is not null; current = controls[current].Parent)
            {
                yield return current;
            }
        }
        var writes = IkChains.SelectMany(c => (c.Solver == ModelChain.BoneSolver ? new[] { c.Root } : [c.Joint, c.End]).Select(id => (Chain: c, Id: id))).ToArray();
        foreach (var chain in IkChains)
        {
            Require(!SelfAndAncestors(chain.Root).Any(id => writes.Any(w => w.Chain != chain && w.Id == id)), $"{owner}: chain '{chain.Id}' overlaps or is nested inside another chain; nested IK is not supported.");
            Require(chain.Frame == Locomotion || !SelfAndAncestors(chain.Frame).Any(id => writes.Any(w => w.Id == id)), $"{owner}: chain '{chain.Id}' frame '{chain.Frame}' must not be moved by IK.");
        }
        var nodes = RigGraph.Compile(this);
        BoneFrameIk.ValidateFrames(controls, IkChains, nodes.ToDictionary(n => n.Key, n => n.Value.RestFrame, StringComparer.Ordinal));

        var parts = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Parts)
        {
            Require(part is not null, $"{owner}: null part.");
            AuthoredAsset.RequireId(part.Id, $"{owner} part id");
            Require(parts.Add(part.Id), $"{owner}: duplicate part '{part.Id}'.");
            part.Validate(Known);
            part.RenderMaterial.Resolve(Materials);
        }
        var sockets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var socket in Sockets)
        {
            Require(socket is not null, $"{owner}: null socket.");
            AuthoredAsset.RequireId(socket.Id, $"{owner} socket id");
            Require(sockets.Add(socket.Id), $"{owner}: duplicate socket '{socket.Id}'.");
            Require(Known(socket.Control) && (socket.Frame is null or Locomotion || Known(socket.Frame)), $"{owner}: socket '{socket.Id}' references an unknown control.");
            Require(socket.Toward is null || Known(socket.Toward) && socket.Toward != socket.Control, $"{owner}: socket '{socket.Id}' needs a different known toward control.");
            new Limit(-100, 100).Check(socket.OffsetX, $"{owner} socket '{socket.Id}' offsetX");
            new Limit(-100, 100).Check(socket.OffsetY, $"{owner} socket '{socket.Id}' offsetY");
            new Limit(-100, 100).Check(socket.OffsetZ, $"{owner} socket '{socket.Id}' offsetZ");
            new Limit(-10, 10).Check(socket.Angle, $"{owner} socket '{socket.Id}' angle");
            if (socket.Usage is not null)
            {
                AuthoredAsset.RequireId(socket.Usage, $"{owner} socket '{socket.Id}' usage");
            }
        }
        var attachments = new HashSet<string>(StringComparer.Ordinal);
        Require(Attachments.Count <= 256, $"{owner}: capacity exceeded.");
        foreach (var instance in Attachments)
        {
            Require(instance is not null, $"{owner}: null attachment.");
            instance.Validate($"{owner} attachment");
            Require(attachments.Add(instance.Id), $"{owner}: duplicate attachment '{instance.Id}'.");
            Require(!parts.Contains(instance.Id), $"{owner}: attachment '{instance.Id}' shares its id with a drawing part.");
            Require(sockets.Contains(instance.Socket), $"{owner} attachment '{instance.Id}': no socket '{instance.Socket}'.");
        }
        var sets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var set in MotionSets)
        {
            Require(set?.Roles is not null, $"{owner}: incomplete motion set.");
            AuthoredAsset.RequireId(set.Id, $"{owner} motion set id");
            Require(sets.Add(set.Id), $"{owner}: duplicate motion set '{set.Id}'.");
            Require(!string.IsNullOrWhiteSpace(set.Name), $"{owner} motion set '{set.Id}': a name is required.");
            foreach (var (role, clip) in set.Roles)
            {
                AuthoredAsset.RequireId(role, $"{owner} motion set '{set.Id}' role");
                AuthoredAsset.RequireId(clip, $"{owner} motion set '{set.Id}' role '{role}' clip");
            }
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
                // An attachment instance in a group is owned whole: its selection channel and every namespaced track; a drawing
                // part in a group is owned through its fit and show channels (P7a).
                Require(Known(target) || target is not null && (chainIds.Contains(target) || attachments.Contains(target) || parts.Contains(target)), $"{owner} group '{group.Id}': '{target}' is not a control, chain, part or attachment instance.");
                // A chain moves its joint and end; owning one of them without the chain would split it between two layers.
                Require(!solved.Contains(target), $"{owner} group '{group.Id}': '{target}' is solved by IK; name its chain instead.");
            }
        }
        var looks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var look in Looks)
        {
            Require(look?.Parts is not null && look.Expressions is not null, $"{owner}: incomplete look.");
            AuthoredAsset.RequireId(look.Id, $"{owner} look id");
            Require(looks.Add(look.Id), $"{owner}: duplicate look '{look.Id}'.");
            Require(!string.IsNullOrWhiteSpace(look.Name), $"{owner} look '{look.Id}': a name is required.");
            foreach (var (part, change) in look.Parts)
            {
                Require(parts.Contains(part), $"{owner} look '{look.Id}': no part '{part}'.");
                LookAppearance.Check(change, $"{owner} look '{look.Id}' parts.{part}");
                change.Material?.Resolve(Materials);
            }
            foreach (var (instance, expression) in look.Expressions)
            {
                Require(attachments.Contains(instance), $"{owner} look '{look.Id}': no attachment '{instance}'.");
                AuthoredAsset.RequireId(expression, $"{owner} look '{look.Id}' expressions.{instance}");
            }
        }
        CheckGeometry(this, nodes.ToDictionary(n => n.Key, n => n.Value.Rest, StringComparer.Ordinal), owner);
        SkeletonAppearance2D.Validate(this);
        ParameterCatalog.Compile(this);
    }

    /// <summary>A point-solved segment: the bone has no declared length, or its child is a bone node bound to its tip.</summary>
    private static bool TipBound(ModelControl bone, ModelControl child) =>
        bone.Length == 0 || child.Node == RigNodeKinds.Bone && child.Link is { IsTip: true };

    /// <summary>Checks lengths that depend on rest geometry, so a variant's overrides are held to the same rules as its base.</summary>
    public static void CheckGeometry(CharacterModel model, IReadOnlyDictionary<string, Vector3> rest, string owner)
    {
        float Length(string a, string b) => Vector2.Distance(new(rest[a].X, rest[a].Y), new(rest[b].X, rest[b].Y));
        foreach (var chain in model.IkChains.Where(c => c.Solver == ModelChain.PointSolver))
        {
            Require(Length(chain.Root, chain.Joint) >= .01f && Length(chain.Joint, chain.End) >= .01f, $"{owner}: chain '{chain.Id}' needs segment lengths of at least 0.01.");
        }

        foreach (var measure in model.Measures)
        {
            Require(measure.Path.Zip(measure.Path.Skip(1)).Sum(p => Length(p.First, p.Second)) >= .001f, $"{owner}: measure '{measure.Id}' has no length.");
        }
    }
}
