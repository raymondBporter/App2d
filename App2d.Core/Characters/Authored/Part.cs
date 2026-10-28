using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>What a part asset's payload is (plan section 3.6).</summary>
public static class PartPayloads
{
    /// <summary>Geometry, images and meshes placed rigidly on the attachment frame, fitted per instance.</summary>
    public const string Static = "static";
    /// <summary>
    /// A payload with its own small rig, clips, layers and clocks (P7a): its pieces hang on its controls, the wearer animates
    /// its exposed targets through namespaced tracks (<c>instance/target</c>) and selects what it plays through expression
    /// bindings. The instance's evaluated pose joins the wearer's runtime graph (<see cref="EvaluatedPose.Attachments"/>).
    /// </summary>
    public const string Animated = "animated";
    public static readonly string[] All = [Static, Animated];
}

/// <summary>How an instance's leaf fit reaches a composite asset (plan section 3.4), declared per asset.</summary>
public static class PartFittings
{
    /// <summary>The fit stretches the whole artwork and rig as one leaf deformation on the instance placement (the P5 behavior).</summary>
    public const string Stretch = "stretch";
    /// <summary>The fit repositions the asset's controls and locations (layout); each piece keeps its own size and the rig its similarity poses.</summary>
    public const string Layout = "layout";
    public static readonly string[] All = [Stretch, Layout];
}

/// <summary>Who owns an animated part layer's clock (plan section 3.8).</summary>
public static class PartClocks
{
    /// <summary>The wearer's clip time: the layer runs in phase with whatever the wearer plays.</summary>
    public const string Shared = "shared";
    /// <summary>The wearer's clip time mapped through the layer's rate and offset.</summary>
    public const string Mapped = "mapped";
    /// <summary>The actor's own continuous clock (<see cref="PoseInput.Clock"/>), independent of what the wearer plays or restarts.</summary>
    public const string Local = "local";
    public static readonly string[] All = [Shared, Mapped, Local];
}

/// <summary>
/// One animation layer of an animated part: which of the asset's targets it owns (a control group, or everything for the
/// first layer), which clock drives it and which clip it plays by default. An expression binding may put another clip on it.
/// The first layer is the base; every later layer is masked over it.
/// </summary>
public sealed record PartLayer
{
    public string Id { get; set; } = "";
    /// <summary>A control group of the asset's rig naming the targets this layer owns; null owns everything (base layer only).</summary>
    public string? Mask { get; set; }
    /// <summary>One of <see cref="PartClocks"/>.</summary>
    public string Clock { get; set; } = PartClocks.Shared;
    public float Rate { get; set; } = 1;
    public float Offset { get; set; }
    /// <summary>The clip this layer plays when no expression puts another on it; null leaves the layer at rest.</summary>
    public string? Clip { get; set; }

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        if (!PartClocks.All.Contains(Clock, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{field}: unknown clock '{Clock}' (one of {string.Join(", ", PartClocks.All)}).");
        }

        new Limit(-100, 100).Check(Rate, field + " rate");
        new Limit(-3600, 3600).Check(Offset, field + " offset");
    }

    /// <summary>This layer's clip time from the wearer's clip seconds and the actor's clock.</summary>
    public double Time(double wearerSeconds, double actorClock) => Clock switch
    {
        PartClocks.Mapped => Offset + Rate * wearerSeconds,
        PartClocks.Local => Offset + actorClock,
        _ => wearerSeconds,
    };
}

/// <summary>
/// One semantic expression the asset binds (plan section 3.8): the name gameplay, a wearer's clip or an instance asks for,
/// and what the asset does for it. A clip plays on a layer from the moment the expression is selected, or holds one time
/// of it as a pose; the binding may hide the whole instance or mute other layers (a knocked-out face stops blinking).
/// </summary>
public sealed record PartExpression
{
    /// <summary>The layer the clip plays on; null is the base layer.</summary>
    public string? Layer { get; set; }
    public string? Clip { get; set; }
    /// <summary>Holds this time of the clip instead of playing it: a pose.</summary>
    public float? Time { get; set; }
    public bool Hidden { get; set; }
    public List<string> Mutes { get; set; } = [];
}

/// <summary>
/// A named frame inside a part asset's local space that the payload attaches to. <c>origin</c> is implicit: the frame the
/// instance places on the wearer's socket. Other locations let a payload hang several pieces off one asset and give later
/// passes named points for sockets and colliders. In an animated asset a location may ride on a rig control.
/// </summary>
public sealed record PartLocation
{
    public const string Origin = "origin";
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Physical depth in the part's local space, independent of drawing layer.</summary>
    public float Z { get; set; }
    /// <summary>Rotation in radians in the asset's local axes.</summary>
    public float Angle { get; set; }
    /// <summary>Drawing-layer offset from the asset's origin.</summary>
    public float Layer { get; set; }
    /// <summary>Animated assets: the rig control whose evaluated placement carries this location (the offset is in its axes).</summary>
    public string? Control { get; set; }

    [JsonIgnore] public Vector2 Position => new(X, Y);
    [JsonIgnore] public PuppetPoint Point => new(X, Y, Z);

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        if (Id == Origin)
        {
            throw new InvalidDataException($"{field}: '{Origin}' is the implicit location.");
        }

        new Limit(-100, 100).Check(X, field + " x");
        new Limit(-100, 100).Check(Y, field + " y");
        new Limit(-100, 100).Check(Z, field + " z");
        new Limit(-1000, 1000).Check(Angle, field + " angle");
        new Limit(-16, 16).Check(Layer, field + " layer");
    }
}

/// <summary>
/// A reusable library part (character-system migration, P5a): its own asset kind under <c>parts/</c>, owning geometry,
/// images, meshes and shared materials in a local frame whose origin lands on a wearer's socket. A character references it
/// through a <see cref="PartInstance"/> (asset ID, attachment location, instance ID), so one glove asset can sit on both
/// hands as two instances with independent placement, fitting and overrides. The same asset is never copied into a wearer;
/// editing it reaches every dependent preview through the workspace's reference graph.
/// An animated asset (P7a) also owns a small rig (<see cref="Controls"/>, <see cref="Constraints"/>, <see cref="Groups"/>),
/// its own <see cref="Clips"/>, the <see cref="Layers"/> they play on with their clocks, the targets it <see cref="Exposed"/>
/// to the wearer's clips and the <see cref="Expressions"/> it binds for gameplay; its pieces hang on its controls.
/// </summary>
public sealed record Part
{
    public const string FormatId = "app2d-part";
    public const int CurrentVersion = 4;
    /// <summary>The expression word that hides an animated instance for its span: a back view, a cleared face.</summary>
    public const string None = "none";
    /// <summary>Separates an instance ID from a target inside it in a wearer's clip (<c>face/left-brow</c>).</summary>
    public const char Namespace = '/';
    public string Format { get; set; } = FormatId;
    public int Version { get; set; } = CurrentVersion;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Optional reference to the existing model/variant rig envelope.</summary>
    public string? Model { get; set; }
    public List<EquipmentBinding> Equipment { get; set; } = [];
    /// <summary>Opt-in gameplay configuration; generic geometry and animation do not read it.</summary>
    public Behavior? Behavior { get; set; }
    /// <summary>A library category such as glove, shoe, hair or shirt: browser grouping, not engine behavior.</summary>
    public string Usage { get; set; } = "part";
    /// <summary>Optional editor attachment suggestion and alternate artwork selected by gameplay.</summary>
    public string? Attachment { get; set; }
    public string? BackView { get; set; }
    /// <summary>The local location placed on the instance's socket; origin by default.</summary>
    public string Pivot { get; set; } = PartLocation.Origin;
    public float Scale { get; set; } = 1;
    public PuppetPoint? Point(string name) => name == PartLocation.Origin ? default(PuppetPoint) : Locations.FirstOrDefault(l => l.Id == name)?.Point;
    /// <summary>One of <see cref="PartPayloads"/>.</summary>
    public string Payload { get; set; } = PartPayloads.Static;
    /// <summary>One of <see cref="PartFittings"/>: what an instance's leaf fit does to this composite asset.</summary>
    public string Fitting { get; set; } = PartFittings.Stretch;
    public string Ink { get; set; } = "#222b32";
    public float LineWidth { get; set; } = .045f;
    /// <summary>Shared surfaces the payload selects through <c>material.asset</c>, like a model's.</summary>
    public Dictionary<string, RenderMaterialDefinition2D> Materials { get; set; } = [];
    public List<PartLocation> Locations { get; set; } = [];
    /// <summary>All geometry entries share placement, material, visibility, animation and picking.</summary>
    public List<PartGeometry> Geometry { get; set; } = [];

    public App2d.Core.Physics.PhysicsBodySettings2D? Physics { get; set; }
    public App2d.Core.Objects.ObjectSurface2D? Surface { get; set; }
    public App2d.Core.Physics.Motion.CurveMotionDefinition2D? Motion { get; set; }
    [JsonIgnore] public string TextureRoot { get; set; } = Environment.CurrentDirectory;

    // ---- Animated payload (P7a) ---------------------------------------------------------------------------------------

    /// <summary>The local rig: ordinary controls whose roots hang from the asset's origin.</summary>
    public List<ModelControl> Controls { get; set; } = [];
    public List<RigConstraint> Constraints { get; set; } = [];
    /// <summary>Control groups: layer masks and what a wearer's masked action may own.</summary>
    public List<ControlGroup> Groups { get; set; } = [];
    /// <summary>Rig controls and pieces a wearer's clip may key through <c>instance/target</c> tracks.</summary>
    public List<string> Exposed { get; set; } = [];
    /// <summary>The module contract's dimension inputs (P10b): parameters of the local rig a wearer's instance sets through <see cref="PartInstance.Inputs"/>.</summary>
    public List<ModelParameter> Parameters { get; set; } = [];
    public List<ParameterPreset> Presets { get; set; } = [];
    /// <summary>Measures of the local rig the module's clips scale channels by.</summary>
    public List<ModelMeasure> Measures { get; set; } = [];
    /// <summary>The rig control the payload's origin coincides with: the module's connection frame on the wearer's socket (null for a part without a declared connection).</summary>
    public string? Connection { get; set; }
    public List<PartLayer> Layers { get; set; } = [];
    /// <summary>The asset's own clips, authored against its rig (<see cref="MotionClip.Model"/> is the asset ID).</summary>
    public List<MotionClip> Clips { get; set; } = [];
    /// <summary>The expression bindings, by the semantic name a wearer or gameplay asks for.</summary>
    public Dictionary<string, PartExpression> Expressions { get; set; } = [];
    /// <summary>The expression an instance shows when neither gameplay, the wearer's clip nor the instance names one.</summary>
    public string? Expression { get; set; }

    [JsonIgnore] public bool IsModule => Connection is not null;
    [JsonIgnore] public bool IsAnimated => Payload == PartPayloads.Animated;

    public string ToJson() => JsonSerializer.Serialize(this, AuthoredJson.Options);
    internal string ToSnapshotJson() => JsonSerializer.Serialize(this, AuthoredJson.SnapshotOptions);
    internal static Part FromDraftJson(string json) => AuthoredAsset.Parse<Part>(json, "part");
    public static Part FromJson(string json)
    {
        var part = FromDraftJson(json);
        part.RebuildMeshes();
        part.Validate();
        return part;
    }
    public static Part Load(string path) => FromJson(File.ReadAllText(path)) with { TextureRoot = Path.GetDirectoryName(Path.GetFullPath(path))! };
    public void Save(string path)
    {
        Validate();
        AuthoredAsset.Write(path, ToJson());
    }

    /// <summary>Rebuilds meshes from their editable outlines.</summary>
    public void RebuildMeshes()
    {
        foreach (var entry in Geometry ?? []) entry?.Mesh?.Rebuild();
    }

    public bool IsLocation(string? id) => id == PartLocation.Origin || id is not null && Locations.Any(l => l.Id == id);

    /// <summary>A location's frame in the asset's local space: identity for the origin (a control-carried location's frame is in the control's axes).</summary>
    public LayeredFrame2D LocationFrame(string id)
    {
        if (id == PartLocation.Origin)
        {
            return LayeredFrame2D.Identity;
        }

        var location = Locations.FirstOrDefault(l => l.Id == id) ?? throw new KeyNotFoundException($"Part '{Id}' has no location '{id}'.");
        return LayeredFrame2D.Local(Vector2.One, location.Angle, location.Position, location.Layer);
    }

    /// <summary>Whether a wearer may key this target (a rig control or a piece) through a namespaced track.</summary>
    public bool Exposes(string target) => IsAnimated && Exposed.Contains(target, StringComparer.Ordinal);

    /// <summary>The base layer: the first declared one.</summary>
    [JsonIgnore] public PartLayer? BaseLayer => Layers.Count > 0 ? Layers[0] : null;

    public MotionClip? Clip(string? id) => id is null ? null : Clips.FirstOrDefault(c => c.Id == id);

    /// <summary>The binding of an expression word, null for <see cref="None"/> or an unbound word.</summary>
    public PartExpression? Binding(string? expression) => expression is null ? null : Expressions.GetValueOrDefault(expression);

    /// <summary>Whether an expression word is meaningful on this asset: a binding or the hiding word.</summary>
    public bool Binds(string expression) => expression == None || Expressions.ContainsKey(expression);

    /// <summary>
    /// The animated payload's rig as a model the shared evaluator compiles: the asset's controls, constraints, groups and
    /// pieces (with <paramref name="parts"/> standing in for the pieces when an instance's materials are resolved over them).
    /// A static asset has no rig.
    /// </summary>
    public CharacterModel RigModel(IReadOnlyList<PartGeometry>? parts = null)
    {
        if (!IsAnimated)
        {
            throw new InvalidOperationException($"Part '{Id}' is static; it has no rig.");
        }

        return new()
        {
            Id = Id,
            Name = Name,
            Ink = Ink,
            LineWidth = LineWidth,
            Controls = Controls,
            Constraints = Constraints,
            Groups = Groups,
            Materials = Materials,
            Parameters = Parameters,
            Presets = Presets,
            Measures = Measures,
            Parts = [.. parts ?? Geometry],
        };
    }

    /// <summary>
    /// The module contract (plan section 3.6, P10b): the connection frame, the dimension inputs with their ranges, the named
    /// points and the exposed animation targets, as one summary per line for inspectors and reports.
    /// </summary>
    public IReadOnlyList<string> Contract()
    {
        var lines = new List<string>
        {
            $"connection: {(Connection is null ? "the origin" : $"control '{Connection}' at the origin")} on a socket{(Usage == "part" ? "" : $" taking '{Usage}'")}",
            $"inputs: {(Parameters.Count == 0 ? "none" : string.Join(", ", Parameters.Select(p => $"{p.Id} ({p.Units} {p.Default:0.###} in {p.Min:0.###}..{p.Max:0.###})")))}",
            $"points: {(Locations.Count == 0 ? "the origin only" : string.Join(", ", Locations.Select(l => l.Control is null ? l.Id : $"{l.Id} on {l.Control}")))}",
            $"targets: {(Exposed.Count == 0 ? "none" : string.Join(", ", Exposed))}",
        };
        if (IsAnimated)
        {
            lines.Add($"chains: {(Constraints.Count == 0 ? "none" : string.Join(", ", Constraints.Select(c => c.Id)))}; expressions: {(Expressions.Count == 0 ? "none" : string.Join(", ", Expressions.Keys))}");
        }

        return lines;
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
        var owner = $"Part '{Id}'";
        Require(Format == FormatId && Version == CurrentVersion, $"{owner}: unsupported format/version.");
        // An outline is the editable source of a solid; its mesh is derived here as on load, so an authored draft validates too.
        RebuildMeshes();
        AuthoredAsset.RequireId(Id, "part id");
        Require(!string.IsNullOrWhiteSpace(Name), $"{owner}: a name is required.");
        AuthoredAsset.RequireId(Usage, owner + " usage");
        Require(PartPayloads.All.Contains(Payload, StringComparer.Ordinal), $"{owner}: unknown payload '{Payload}'.");
        Require(PartFittings.All.Contains(Fitting, StringComparer.Ordinal), $"{owner}: unknown fitting '{Fitting}' (one of {string.Join(", ", PartFittings.All)}).");
        Limit.Color(Ink, owner + " ink");
        new Limit(.001f, 1).Check(LineWidth, owner + " lineWidth");
        Require(Materials is not null && Locations is not null && Geometry is not null, $"{owner}: collections cannot be null.");
        Require(Controls is not null && Constraints is not null && Groups is not null && Exposed is not null && Layers is not null && Clips is not null && Expressions is not null, $"{owner}: collections cannot be null.");
        Require(Parameters is not null && Presets is not null && Measures is not null, $"{owner}: collections cannot be null.");
        Require(IsAnimated || Controls.Count == 0 && Constraints.Count == 0 && Groups.Count == 0 && Exposed.Count == 0 && Layers.Count == 0 && Clips.Count == 0 && Expressions.Count == 0 && Expression is null,
            $"{owner}: a static payload carries no rig, layers, clips, exposed targets or expressions; declare the payload animated.");
        Require(IsAnimated || Parameters.Count == 0 && Presets.Count == 0 && Measures.Count == 0 && Connection is null,
            $"{owner}: a static payload declares no inputs, presets, measures or connection control; declare the payload animated.");
        foreach (var (id, material) in Materials)
        {
            AuthoredAsset.RequireId(id, $"{owner} material id");
            Require(material is not null, $"{owner}: null material '{id}'.");
            material.Resolve(Materials);
        }
        Require(Equipment is not null, owner + ": equipment cannot be null.");
        var equipmentIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in Equipment)
        {
            Require(binding is not null, owner + ": null equipment.");
            AuthoredAsset.RequireId(binding.Prop, owner + " equipment part");
            AuthoredAsset.RequireId(binding.Socket, owner + " equipment socket");
            Require(equipmentIds.Add(binding.Prop), $"{owner}: part '{binding.Prop}' is equipped twice.");
        }
        if (Model is not null) AuthoredAsset.RequireId(Model, owner + " model");
        Behavior?.Validate(this);
        var locations = new HashSet<string>(StringComparer.Ordinal) { PartLocation.Origin };
        foreach (var location in Locations)
        {
            Require(location is not null, $"{owner}: null location.");
            location.Validate($"{owner} location");
            Require(locations.Add(location.Id), $"{owner}: duplicate location '{location.Id}'.");
            Require(location.Control is null || IsAnimated && Controls.Any(c => c.Id == location.Control), $"{owner} location '{location.Id}': no rig control '{location.Control}' to ride on.");
        }
        new Limit(.001f, 100).Check(Scale, owner + " scale");
        Require(Point(Pivot) is not null, owner + ": unknown pivot location.");
        if (Attachment is not null) AuthoredAsset.RequireId(Attachment, owner + " attachment");
        if (BackView is not null)
        {
            AuthoredAsset.RequireId(BackView, owner + " backView");
            Require(BackView != Id, owner + ": back view cannot reference itself.");
        }
        Require(Geometry.Count > 0 || Model is not null, $"{owner}: a part needs at least one shape, curve, image or solid.");
        Require(Geometry.Count <= 384, $"{owner}: capacity exceeded.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in Geometry)
        {
            Require(part is not null, $"{owner}: null drawing part.");
            AuthoredAsset.RequireId(part.Id, $"{owner} drawing part id");
            Require(ids.Add(part.Id), $"{owner}: duplicate drawing part '{part.Id}'.");
            if (!IsAnimated)
            {
                try
                {
                    part.Validate(id => locations.Contains(id) || Model is not null);
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException($"{owner} part '{part.Id}': {ex.Message}", ex);
                }
            }

            part.ResolveMaterial(Materials);
        }
        if (IsAnimated) ValidateAnimated(owner);
        Physics?.Validate();
        Motion?.Validate();
        if (Physics is not null)
        {
            _ = App2d.Core.Characters.Geometry.PartShapes2D.Collision(this);
            Require(Motion is null || Physics.MotionType == App2d.Core.Physics.BodyMotionType2D.Kinematic,
                owner + ": a physical part with path motion must be kinematic.");
        }
        if (Surface is not null)
        {
            Require(Physics is not null, owner + ": surface behavior requires physics.");
            Surface.Validate();
        }
    }

    /// <summary>The animated payload: its rig compiles like a model, its clips play on that rig, and every layer, exposure and binding names something that exists.</summary>
    private void ValidateAnimated(string owner)
    {
        Require(Controls.Count > 0, $"{owner}: an animated payload needs at least one rig control for its pieces to hang on.");
        ResolvedModel rig;
        try
        {
            rig = ResolvedModel.From(RigModel());
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidDataException($"{owner} rig: {ex.Message}", ex);
        }

        var controls = Controls.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var pieces = Geometry.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        Require(Connection is null || controls.Contains(Connection), $"{owner}: the connection control '{Connection}' is not a rig control.");
        Require(Connection is null || Controls.First(c => c.Id == Connection).Parent is null && Controls.First(c => c.Id == Connection).Rest.XYZ == System.Numerics.Vector3.Zero,
            $"{owner}: the connection control '{Connection}' must be a rig root at the origin (the payload's origin is what lands on the wearer's socket).");
        foreach (var target in Exposed)
        {
            Require(target is not null && (controls.Contains(target) || pieces.Contains(target)), $"{owner} exposed: '{target}' is neither a rig control nor a piece.");
            Require(!target.Contains(Namespace), $"{owner} exposed: '{target}' may not contain '{Namespace}'.");
        }
        Require(Exposed.Distinct(StringComparer.Ordinal).Count() == Exposed.Count, $"{owner}: duplicate exposed target.");
        var clips = new HashSet<string>(StringComparer.Ordinal);
        foreach (var clip in Clips)
        {
            Require(clip is not null, $"{owner}: null clip.");
            Require(clips.Add(clip.Id), $"{owner}: duplicate clip '{clip.Id}'.");
            Require(clip.Model == Id, $"{owner} clip '{clip.Id}': authored for '{clip.Model}', not this part.");
            try
            {
                clip.Validate(rig);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{owner} clip '{clip.Id}': {ex.Message}", ex);
            }
            Require(clip.Plays.Count == 0 && clip.Tracks.All(t => !t.Target.Contains(Namespace)), $"{owner} clip '{clip.Id}': a part's own clip animates its rig alone; nested parts inside a part are not supported.");
        }
        Require(Layers.Count > 0, $"{owner}: an animated payload declares at least one layer (the base), each with its clock.");
        var layers = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < Layers.Count; i++)
        {
            var layer = Layers[i];
            Require(layer is not null, $"{owner}: null layer.");
            layer.Validate($"{owner} layer");
            Require(layers.Add(layer.Id), $"{owner}: duplicate layer '{layer.Id}'.");
            Require(i > 0 || layer.Mask is null, $"{owner} layer '{layer.Id}': the base layer owns every target; a mask belongs to a later layer.");
            Require(i == 0 || layer.Mask is not null, $"{owner} layer '{layer.Id}': a layer over the base needs a mask naming the targets it owns.");
            Require(layer.Mask is null || Groups.Any(g => g.Id == layer.Mask), $"{owner} layer '{layer.Id}': no control group '{layer.Mask}' to mask with.");
            Require(layer.Clip is null || clips.Contains(layer.Clip), $"{owner} layer '{layer.Id}': no clip '{layer.Clip}'.");
        }
        foreach (var (name, binding) in Expressions)
        {
            var field = $"{owner} expression '{name}'";
            AuthoredAsset.RequireId(name, $"{owner} expression");
            Require(name != None, $"{owner}: '{None}' is the hiding word and cannot be bound.");
            Require(binding is not null, $"{field}: null binding.");
            Require(binding.Mutes is not null, $"{field}: mutes cannot be null.");
            Require(binding.Layer is null || layers.Contains(binding.Layer), $"{field}: no layer '{binding.Layer}'.");
            Require(binding.Clip is null || clips.Contains(binding.Clip), $"{field}: no clip '{binding.Clip}'.");
            Require(binding.Time is null || binding.Clip is not null, $"{field}: a pose time needs a clip.");
            if (binding.Time is { } time)
            {
                new Limit(0, Clip(binding.Clip)!.Duration).Check(time, field + " time");
            }

            foreach (var mute in binding.Mutes)
            {
                Require(mute is not null && layers.Contains(mute), $"{field}: no layer '{mute}' to mute.");
            }
        }
        Require(Expression is null || Binds(Expression), $"{owner}: the default expression '{Expression}' is not bound.");
    }
}

/// <summary>
/// One occurrence of a library part on a character: the instance ID (stable, addressed by overrides), the asset ID
/// (which library resource) and the socket ID (the wearer's named attachment location), plus this instance's placement on
/// the socket, mirroring, leaf fitting and local overrides. Two instances of one asset are independent; neither edits it.
/// </summary>
public sealed record PartInstance
{
    public string Id { get; set; } = "";
    public string Asset { get; set; } = "";
    public string Socket { get; set; } = "";
    /// <summary>Offset of the asset's origin in the socket's axes.</summary>
    public float X { get; set; }
    public float Y { get; set; }
    /// <summary>Rotation in radians in the socket's axes.</summary>
    public float Angle { get; set; }
    /// <summary>Uniform size, part of the similarity.</summary>
    public float Scale { get; set; } = 1;
    /// <summary>Reflects the asset across the socket's Y axis: the same glove on the other hand.</summary>
    public bool Mirror { get; set; }
    /// <summary>Drawing-layer offset from the socket.</summary>
    public float Layer { get; set; }
    /// <summary>Leaf geometry fit (plan section 3.4): stretches this instance's artwork, or repositions its layout, as the asset declares; nothing inherits it.</summary>
    public float FitX { get; set; } = 1;
    public float FitY { get; set; } = 1;
    /// <summary>Inline material override over every piece of the payload (a fill, a shared material of the asset, a tint); null inherits.</summary>
    public RenderMaterialDefinition2D? Material { get; set; }
    public bool Hidden { get; set; }
    /// <summary>Animated assets: the expression this instance shows by default, over the asset's own default (P7a).</summary>
    public string? Expression { get; set; }
    /// <summary>The module's dimension inputs this instance sets (P10b): parameter id → value, each exposed as the attachment property <c>input.&lt;id&gt;</c>.</summary>
    public Dictionary<string, float> Inputs { get; set; } = [];

    /// <summary>
    /// The instance's placement on its socket: fit and size, then rotation, then offset, then the layer; a mirrored instance
    /// is reflected in the socket's axes afterwards, so the mirror image lands where the socket's other side would.
    /// </summary>
    [JsonIgnore]
    public LayeredFrame2D Local => Placement(stretch: true);

    /// <summary>The placement with the fit in the frame (<see cref="PartFittings.Stretch"/>) or left to the asset's layout (<see cref="PartFittings.Layout"/>).</summary>
    public LayeredFrame2D Placement(bool stretch)
    {
        var fit = stretch ? new Vector2(FitX, FitY) : Vector2.One;
        var local = LayeredFrame2D.Local(fit * Scale, Angle, new(X, Y), Layer);
        return Mirror ? local.Then(LayeredFrame2D.Local(new(-1, 1), 0, Vector2.Zero, 0)) : local;
    }

    public void Validate(string field)
    {
        AuthoredAsset.RequireId(Id, field + " id");
        AuthoredAsset.RequireId(Asset, field + " asset");
        AuthoredAsset.RequireId(Socket, field + " socket");
        new Limit(-100, 100).Check(X, field + " x");
        new Limit(-100, 100).Check(Y, field + " y");
        new Limit(-1000, 1000).Check(Angle, field + " angle");
        new Limit(.001f, 100).Check(Scale, field + " scale");
        new Limit(-16, 16).Check(Layer, field + " layer");
        new Limit(-100, 100).Check(FitX, field + " fitX");
        new Limit(-100, 100).Check(FitY, field + " fitY");
        if (FitX == 0 || FitY == 0)
        {
            throw new InvalidDataException($"{field}: a zero fit collapses the artwork.");
        }

        if (Expression is not null)
        {
            AuthoredAsset.RequireId(Expression, field + " expression");
        }

        if (Inputs is null)
        {
            throw new InvalidDataException($"{field}: inputs cannot be null.");
        }

        foreach (var (input, value) in Inputs)
        {
            AuthoredAsset.RequireId(input, field + " input");
            new Limit(-10000, 10000).Check(value, $"{field} input '{input}'");
        }

        Material?.Validate(field + " material");
    }
}

/// <summary>
/// An attachment instance resolved for one wearer: the merged instance, its asset, the socket it hangs from, and the payload
/// with materials resolved against the asset's shared materials and the instance's override. An animated asset's rig is
/// compiled here too (<see cref="Rig"/>), over the resolved pieces. The placement is derived per pose from the socket's
/// evaluated placement; nothing here is a copy of the asset.
/// </summary>
public sealed record ResolvedAttachment(PartInstance Instance, Part Asset, ModelSocket Socket, IReadOnlyList<PartGeometry> Parts, ResolvedModel? Rig = null)
{
    public string Id => Instance.Id;
    public bool IsAnimated => Rig is not null;
    /// <summary>Whether the instance's fit stretches the artwork in the placement (else it repositions the asset's layout).</summary>
    public bool Stretches => Asset.Fitting == PartFittings.Stretch;
    /// <summary>The fit the asset's layout repositions by: the instance fit under the layout policy, none under stretch.</summary>
    public Vector2 LayoutFit => Stretches ? Vector2.One : new(Instance.FitX, Instance.FitY);

    /// <summary>The instance's placement in the frame its socket placement is given in (actor-local or world).</summary>
    public LayeredFrame2D Placement(LayeredFrame2D socketPlacement) => Instance.Placement(Stretches).Then(socketPlacement);

    /// <summary>A payload location's placement for a given instance placement: what a static payload's pieces attach to.</summary>
    public LayeredFrame2D LocationPlacement(string location, LayeredFrame2D placement)
    {
        var fit = LayoutFit;
        var frame = Asset.LocationFrame(location);
        return fit == Vector2.One ? frame.Then(placement) : frame.WithOrigin(new(frame.Origin.X * fit.X, frame.Origin.Y * fit.Y, frame.Origin.Z)).Then(placement);
    }

    /// <summary>The static payload's drawing inputs for an instance placement: each location's origin and frame, as the shared part drawing expects.</summary>
    public (Func<string, Vector3> World, Func<string, LayeredFrame2D> Frame) Lookups(LayeredFrame2D placement) =>
        (location => LocationPlacement(location, placement).Origin, location => LocationPlacement(location, placement));

    /// <summary>The expression this instance shows before any clip or request: its own, else the asset's default.</summary>
    public string? DefaultExpression => Instance.Expression ?? Asset.Expression;

    /// <summary>Resolves an instance for a wearer: the asset must exist and the socket must be the wearer's.</summary>
    public static ResolvedAttachment Resolve(PartInstance instance, Func<string, Part?> partOf, IReadOnlyDictionary<string, ModelSocket> sockets, string owner)
    {
        var asset = partOf(instance.Asset) ?? throw new InvalidDataException($"{owner} attachment '{instance.Id}': no part asset '{instance.Asset}'.");
        if (!sockets.TryGetValue(instance.Socket, out var socket))
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': the wearer has no socket '{instance.Socket}'.");
        }

        // The slot contract (P10b): a socket that declares a usage takes only parts of that usage; a reference path names the binding.
        if (socket.Usage is { } usage && asset.Usage != usage)
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': socket '{socket.Id}' takes '{usage}', part '{asset.Id}' is a '{asset.Usage}' (path: {owner} > attachments > {instance.Id} > socket {socket.Id} > usage).");
        }

        foreach (var input in instance.Inputs.Keys)
        {
            if (asset.Parameters.All(p => p.Id != input))
            {
                throw new InvalidDataException($"{owner} attachment '{instance.Id}': part '{asset.Id}' declares no input '{input}'{(asset.Parameters.Count == 0 ? "" : $" (one of {string.Join(", ", asset.Parameters.Select(p => p.Id))})")} (path: {owner} > attachments > {instance.Id} > inputs > {input}).");
            }
        }

        var parts = asset.Geometry.ConvertAll(p => p.ResolveMaterial(asset.Materials, instance.Material));
        ResolvedModel? rig = null;
        if (asset.IsAnimated)
        {
            try
            {
                // The instance's inputs are the module rig's parameter values (plan section 3.7 step 2: module selections and parameters before bindings).
                var inputs = instance.Inputs.Count == 0 ? null : new ModelVariant { Id = instance.Id, Name = instance.Id, Base = asset.Id, Parameters = new(instance.Inputs, StringComparer.Ordinal) };
                rig = ResolvedModel.From(asset.RigModel(parts), inputs);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException($"{owner} attachment '{instance.Id}' (part '{asset.Id}'): {ex.Message}", ex);
            }
        }

        if (instance.Expression is { } expression && !asset.Binds(expression))
        {
            throw new InvalidDataException($"{owner} attachment '{instance.Id}': part '{asset.Id}' binds no expression '{expression}'" + (asset.IsAnimated ? $" (one of {string.Join(", ", asset.Expressions.Keys.Append(Part.None))})." : "; a static part shows no expression."));
        }

        return new(instance, asset, socket, parts, rig);
    }
}
