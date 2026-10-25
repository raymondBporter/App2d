namespace App2d.Core.Characters.Authored;

/// <summary>
/// The exposed property registry (character-system migration, P4a/P6a): every property a binding or override may address,
/// declared once with its kind, units, range or vocabulary. Control properties are the canonical node's dimension and setup:
/// its bone length, its parent-local translation, rotation and layer offset, and its link's normalized position and offset
/// along the parent bone. Part properties are the leaf geometry fit of plan section 3.4 (never inherited by children) plus
/// its material, face and visibility. Attachment properties are an instance's placement, fit, asset selection, mirror,
/// material and visibility. A role property binds an animation role to a clip. Names are stable IDs resolved once per model
/// by <see cref="ParameterCatalog"/> and <see cref="ResolvedModel"/>; a new property registers here and nowhere else.
/// </summary>
public static class RigProperties
{
    public const string Length = "length";
    public const string LocalX = "local.x", LocalY = "local.y", LocalRotation = "local.rotation", LocalLayer = "local.layer";
    public const string LinkT = "link.t", LinkOffsetX = "link.offsetX", LinkOffsetY = "link.offsetY";
    public const string ScaleX = "scaleX", ScaleY = "scaleY", OffsetX = "offsetX", OffsetY = "offsetY";
    public const string Material = "material", Face = "face", FaceX = "faceX", Hidden = "hidden";
    /// <summary>An attachment instance's placement on its socket and its leaf fit (P5b); never inherited by anything.</summary>
    public const string X = "x", Y = "y", Angle = "angle", Scale = "scale", Layer = "layer", FitX = "fitX", FitY = "fitY";
    public const string Asset = "asset", Mirror = "mirror";
    /// <summary>An animated part instance's default expression (P7a): one of the words its asset binds, or none.</summary>
    public const string Expression = "expression";
    /// <summary>An animation role's clip (P6a): the wearer's motion-set role on this variant.</summary>
    public const string Clip = "clip";
    /// <summary>A bone node's angular joint limits (P4b): bounds on its parent-local rotation, exposed only while the node declares a limit.</summary>
    public const string LimitMin = "limit.min", LimitMax = "limit.max";
    /// <summary>The limits group, named in an inspector's unsupported row with the reason a node exposes none.</summary>
    public const string Limits = "limits";

    private static readonly Limit Lengths = new(0, 100), Translations = new(-10000, 10000), Rotations = new(-1000, 1000), Layers = new(-32, 32);
    private static readonly Limit LinkPositions = new(-10, 10), LinkOffsets = new(-100, 100), Fits = new(-100, 100), PartOffsets = new(-100, 100);
    private static readonly Limit Placements = new(-100, 100), Sizes = new(.001f, 100), AttachmentLayers = new(-16, 16);
    private static readonly PropertyDeclaration LengthDeclaration = new(Length, PropertyKinds.Number, ParameterUnits.Length, Lengths, Help: "Bone length along local +X; bound children follow the tip.");
    private static readonly PropertyDeclaration LocalXDeclaration = new(LocalX, PropertyKinds.Number, ParameterUnits.Length, Translations, Help: "Origin in the parent's local axes.");
    private static readonly PropertyDeclaration LocalYDeclaration = new(LocalY, PropertyKinds.Number, ParameterUnits.Length, Translations, Help: "Origin in the parent's local axes.");
    private static readonly PropertyDeclaration LocalRotationDeclaration = new(LocalRotation, PropertyKinds.Number, ParameterUnits.Radians, Rotations, Help: "Rest rotation in the parent's frame.");
    private static readonly PropertyDeclaration LocalLayerDeclaration = new(LocalLayer, PropertyKinds.Number, ParameterUnits.Layer, Layers, Help: "Drawing-layer offset from the parent.");
    private static readonly PropertyDeclaration LinkTDeclaration = new(LinkT, PropertyKinds.Number, ParameterUnits.Normalized, LinkPositions, Help: "Position along the parent bone (0 origin, 1 tip).");
    private static readonly PropertyDeclaration LinkOffsetXDeclaration = new(LinkOffsetX, PropertyKinds.Number, ParameterUnits.Length, LinkOffsets, Help: "Offset from the bound position in the parent's axes.");
    private static readonly PropertyDeclaration LinkOffsetYDeclaration = new(LinkOffsetY, PropertyKinds.Number, ParameterUnits.Length, LinkOffsets, Help: "Offset from the bound position in the parent's axes.");
    private static readonly PropertyDeclaration LimitMinDeclaration = new(LimitMin, PropertyKinds.Number, ParameterUnits.Radians, RigLimit.Range, Help: "Lower bound of the parent-local rotation; a normalized bend of 0 lands here.");
    private static readonly PropertyDeclaration LimitMaxDeclaration = new(LimitMax, PropertyKinds.Number, ParameterUnits.Radians, RigLimit.Range, Help: "Upper bound of the parent-local rotation; a normalized bend of 1 lands here.");
    /// <summary>The joint-limit properties, exposed on a bone node that declares a limit.</summary>
    public static readonly IReadOnlyList<string> LimitProperties = [LimitMin, LimitMax];

    /// <summary>The part properties: the leaf fit (bindable) and its appearance.</summary>
    public static readonly IReadOnlyList<PropertyDeclaration> PartDeclarations =
    [
        new(ScaleX, PropertyKinds.Number, ParameterUnits.Factor, Help: "Leaf artwork fit; nothing inherits it."),
        new(ScaleY, PropertyKinds.Number, ParameterUnits.Factor),
        new(OffsetX, PropertyKinds.Number, ParameterUnits.Length, PartOffsets),
        new(OffsetY, PropertyKinds.Number, ParameterUnits.Length, PartOffsets),
        new(Material, PropertyKinds.Material, Help: "Fill, outline, paint or a shared material over the base part's.") { Clearable = false },
        new(Face, PropertyKinds.Selection, Options: [.. FaceExpressions.Names, FaceAppearance.None], Help: "Default expression of the part's face payload; clear hides the face.") { Cleared = FaceAppearance.None },
        new(FaceX, PropertyKinds.Number, ParameterUnits.Length, FaceAppearance.OffsetLimit, Help: "Offset of the face payload's features along the part frame's X axis."),
        new(Hidden, PropertyKinds.Flag),
    ];

    /// <summary>The part properties that belong to the face payload: exposed only while the part carries one.</summary>
    public static readonly IReadOnlyList<string> FaceProperties = [Face, FaceX];

    /// <summary>Why a declared part property is not exposed on this part, or null when it is: a feature's properties need its payload.</summary>
    public static string? Refusal(PuppetPart part, string property) =>
        FaceProperties.Contains(property) && part.Face is null ? $"part '{part.Id}' has no face; add a face to the base part before overriding or binding '{property}'." : null;

    /// <summary>The part properties this part exposes: its fit, material and visibility, plus a feature's properties while it carries the payload.</summary>
    public static IEnumerable<PropertyDeclaration> Declarations(PuppetPart part) => PartDeclarations.Where(d => Refusal(part, d.Name) is null);

    /// <summary>
    /// The typed overrides that take a part from <paramref name="was"/> to <paramref name="now"/> (every registered part
    /// property but the material, which merges separately): the registry reads each property once here, so a part-edit
    /// command carries no feature field. A payload cannot be added or removed this way; the base part owns it.
    /// </summary>
    public static IEnumerable<PropertyOverride> PartChanges(PuppetPart was, PuppetPart now)
    {
        PropertyRef Target(string property) => PropertyRef.OfPart(was.Id, property);
        if (now.ScaleX != was.ScaleX) yield return PropertyOverride.Of(Target(ScaleX), now.ScaleX);
        if (now.ScaleY != was.ScaleY) yield return PropertyOverride.Of(Target(ScaleY), now.ScaleY);
        if (now.OffsetX != was.OffsetX) yield return PropertyOverride.Of(Target(OffsetX), now.OffsetX);
        if (now.OffsetY != was.OffsetY) yield return PropertyOverride.Of(Target(OffsetY), now.OffsetY);
        if (now.Face is null != was.Face is null)
        {
            throw new InvalidOperationException($"Part '{was.Id}': a variant cannot add or remove a face; the base part owns the payload.");
        }

        if (now.Face is { } face && was.Face is { } before)
        {
            if (face.Expression != before.Expression) yield return PropertyOverride.Of(Target(Face), face.Expression);
            if (face.OffsetX != before.OffsetX) yield return PropertyOverride.Of(Target(FaceX), face.OffsetX);
        }

        if (now.Hidden != was.Hidden) yield return PropertyOverride.Of(Target(Hidden), now.Hidden);
    }

    /// <summary>The attachment-instance properties: placement and fit (bindable), the asset selection, mirror, material and visibility.</summary>
    public static readonly IReadOnlyList<PropertyDeclaration> AttachmentDeclarations =
    [
        new(X, PropertyKinds.Number, ParameterUnits.Length, Placements, Help: "Offset of the asset's origin in the socket's axes."),
        new(Y, PropertyKinds.Number, ParameterUnits.Length, Placements),
        new(Angle, PropertyKinds.Number, ParameterUnits.Radians, Rotations),
        new(Scale, PropertyKinds.Number, ParameterUnits.Factor, Sizes),
        new(Layer, PropertyKinds.Number, ParameterUnits.Layer, AttachmentLayers),
        new(FitX, PropertyKinds.Number, ParameterUnits.Factor, Fits),
        new(FitY, PropertyKinds.Number, ParameterUnits.Factor, Fits),
        new(Asset, PropertyKinds.Selection, References: "part", Help: "Which library part this instance shows on this wearer.") { Clearable = false },
        new(Mirror, PropertyKinds.Flag),
        new(Material, PropertyKinds.Material, Help: "Inline override over every piece; a fill, a shared material of the asset, a tint. Clear shows the asset's own materials."),
        new(Hidden, PropertyKinds.Flag),
        new(Expression, PropertyKinds.Selection, References: "expression", Help: "The expression this instance shows by default (one the asset binds, or none to hide it); clear follows the asset's default.") { Cleared = "" },
    ];

    /// <summary>The animation-role properties: the clip a role plays on this wearer.</summary>
    public static readonly IReadOnlyList<PropertyDeclaration> RoleDeclarations =
    [
        new(Clip, PropertyKinds.Selection, References: "animation", Help: "The clip this role plays on this variant, over the base's motion set.") { Clearable = false },
    ];

    /// <summary>The bindable part property names.</summary>
    public static readonly IReadOnlyList<string> Part = [.. PartDeclarations.Where(d => d.Bindable).Select(d => d.Name)];
    /// <summary>The bindable attachment property names.</summary>
    public static readonly IReadOnlyList<string> Attachment = [.. AttachmentDeclarations.Where(d => d.Bindable).Select(d => d.Name)];

    /// <summary>The properties a control exposes by its declared kind and link: an affine node exposes none (adapter until P11).</summary>
    public static IReadOnlyList<PropertyDeclaration> Declarations(ModelControl control)
    {
        IReadOnlyList<PropertyDeclaration> declarations = control.Node switch
        {
            RigNodeKinds.Affine => [],
            RigNodeKinds.Bone when control.Link is not null => [LengthDeclaration, LocalRotationDeclaration, LocalLayerDeclaration, LinkTDeclaration, LinkOffsetXDeclaration, LinkOffsetYDeclaration],
            RigNodeKinds.Bone => [LengthDeclaration, LocalXDeclaration, LocalYDeclaration, LocalRotationDeclaration, LocalLayerDeclaration],
            _ => [LocalXDeclaration, LocalYDeclaration, LocalRotationDeclaration, LocalLayerDeclaration],
        };
        // Joint limits are a feature of the node that declares them (P4b); a node without a limit exposes no limit property.
        return control.Node == RigNodeKinds.Bone && control.Limit is not null ? [.. declarations, LimitMinDeclaration, LimitMaxDeclaration] : declarations;
    }

    /// <summary>The bindable control property names.</summary>
    public static IReadOnlyList<string> ForControl(ModelControl control) => [.. Declarations(control).Where(d => d.Bindable).Select(d => d.Name)];

    /// <summary>
    /// Property groups a control does not expose, with the reason: an inspector shows the row as unsupported instead of
    /// hiding it or offering a handle that silently does nothing. Joint limits (P4b) are exposed only by a bone node that
    /// declares them on the base model.
    /// </summary>
    public static IReadOnlyList<(string Name, string Reason)> Unsupported(ModelControl control) => control.Node switch
    {
        RigNodeKinds.Affine => [(Limits, "a legacy affine node; the adapter exposes nothing until P11")],
        RigNodeKinds.Bone when control.Limit is null => [(Limits, "no joint limits declared on this bone node; add them on the base model to override or bind them")],
        RigNodeKinds.Bone => [],
        _ => [(Limits, "a point control has no joint limits; convert the run to a bone chain on the base model")],
    };

    /// <summary>Why a property is not exposed on a control, or null when it is.</summary>
    public static string? Refusal(ModelControl control, string property)
    {
        if (control.Node == RigNodeKinds.Affine)
        {
            return $"'{control.Id}' is a legacy affine node; the adapter exposes no bindable properties.";
        }

        if (Declarations(control).Any(d => d.Name == property))
        {
            return null;
        }

        return property switch
        {
            Length => $"'{control.Id}' is a point control; only a bone node exposes a length.",
            LocalX or LocalY => $"'{control.Id}' is bound to its parent bone; bind its link instead of a local translation.",
            LinkT or LinkOffsetX or LinkOffsetY => $"'{control.Id}' has no link; bind its local translation instead.",
            Limits or LimitMin or LimitMax => $"'{control.Id}': {Unsupported(control).FirstOrDefault(u => u.Name == Limits).Reason ?? "no joint limits"}.",
            _ => $"no control property '{property}' (one of {string.Join(", ", Declarations(control).Select(d => d.Name))}).",
        };
    }

    /// <summary>Resolves a property reference against a model to its declaration, or throws naming what is missing.</summary>
    public static PropertyDeclaration Declaration(PropertyRef property, CharacterModel model, string field)
    {
        if (property.Role is { } role)
        {
            if (model.MotionSets.Count == 0 || !model.MotionSets.Any(s => s.Roles.ContainsKey(role)))
            {
                throw new InvalidDataException($"{field}: no motion set of '{model.Id}' assigns role '{role}'.");
            }

            return RoleDeclarations.FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: no role property '{property.Property}' (one of {string.Join(", ", RoleDeclarations.Select(d => d.Name))}).");
        }

        if (property.Attachment is { } attachment)
        {
            if (model.Attachments.All(a => a.Id != attachment))
            {
                throw new InvalidDataException($"{field}: no attachment '{attachment}'.");
            }

            return AttachmentDeclarations.FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: no attachment property '{property.Property}' (one of {string.Join(", ", AttachmentDeclarations.Select(d => d.Name))}).");
        }

        if (property.Part is { } part)
        {
            var owner = model.Parts.FirstOrDefault(p => p.Id == part) ?? throw new InvalidDataException($"{field}: no part '{part}'.");
            var declaration = PartDeclarations.FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: no part property '{property.Property}' (one of {string.Join(", ", PartDeclarations.Select(d => d.Name))}).");
            return Refusal(owner, declaration.Name) is { } refusal ? throw new InvalidDataException($"{field}: {refusal}") : declaration;
        }

        var control = model.Controls.FirstOrDefault(c => c.Id == property.Control) ?? throw new InvalidDataException($"{field}: no control '{property.Control}'.");
        return Declarations(control).FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: {Refusal(control, property.Property)}");
    }
}

/// <summary>
/// The resolution state between the authored rig and the final compile: every node's parent-local setup, length and link
/// (point controls' setups derived from their authored rest), the parts and the attachment instances. Parameter bindings and
/// explicit overrides write here in the order of plan section 3.7; <see cref="RigGraph.Compile(CharacterModel, IReadOnlyList{ModelControl}, RigLayout)"/>
/// then derives positions and frames from the final values.
/// </summary>
internal sealed class RigLayout
{
    private RigLayout(Dictionary<string, RigSetup> setups, Dictionary<string, float> lengths, Dictionary<string, RigLink?> links, Dictionary<string, RigLimit?> limits, List<PuppetPart> parts, List<PartInstance> attachments)
    {
        Setups = setups;
        Lengths = lengths;
        Links = links;
        Limits = limits;
        Parts = parts;
        Attachments = attachments;
    }

    public Dictionary<string, RigSetup> Setups { get; }
    public Dictionary<string, float> Lengths { get; }
    public Dictionary<string, RigLink?> Links { get; }
    /// <summary>Each bone node's joint limits (null where none are declared), written by limit bindings and overrides.</summary>
    public Dictionary<string, RigLimit?> Limits { get; }
    public List<PuppetPart> Parts { get; }
    /// <summary>The attachment instances with bound placement and fit, then the wearer's overrides, written.</summary>
    public List<PartInstance> Attachments { get; }
    /// <summary>Controls whose setup a binding or override changed. An untouched point control keeps its authored rest bit for bit.</summary>
    public HashSet<string> Moved { get; } = new(StringComparer.Ordinal);

    public static RigLayout From(CharacterModel model, IReadOnlyDictionary<string, RigNode> baseNodes) => new(
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Setup with { }, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Length, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Link is { } link ? link with { } : null, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Limit is { } limit ? limit with { } : null, StringComparer.Ordinal),
        model.Parts.ConvertAll(p => p with { }),
        model.Attachments.ConvertAll(a => a with { }));

    public float Get(PropertyRef property)
    {
        if (property.Attachment is { } attachment)
        {
            var a = Attachments[AttachmentIndex(attachment)];
            return property.Property switch
            {
                RigProperties.X => a.X,
                RigProperties.Y => a.Y,
                RigProperties.Angle => a.Angle,
                RigProperties.Scale => a.Scale,
                RigProperties.Layer => a.Layer,
                RigProperties.FitX => a.FitX,
                RigProperties.FitY => a.FitY,
                _ => throw Unknown(property),
            };
        }

        if (property.Part is { } part)
        {
            var p = Parts[PartIndex(part)];
            return property.Property switch
            {
                RigProperties.ScaleX => p.ScaleX,
                RigProperties.ScaleY => p.ScaleY,
                RigProperties.OffsetX => p.OffsetX,
                RigProperties.OffsetY => p.OffsetY,
                RigProperties.FaceX => Face(p, property).OffsetX,
                _ => throw Unknown(property),
            };
        }

        var id = property.Control ?? throw Unknown(property);
        return property.Property switch
        {
            RigProperties.Length => Lengths[id],
            RigProperties.LocalX => Setups[id].X,
            RigProperties.LocalY => Setups[id].Y,
            RigProperties.LocalRotation => Setups[id].Rotation,
            RigProperties.LocalLayer => Setups[id].Layer,
            RigProperties.LinkT => Links[id]!.T,
            RigProperties.LinkOffsetX => Links[id]!.OffsetX,
            RigProperties.LinkOffsetY => Links[id]!.OffsetY,
            RigProperties.LimitMin => Limit(id, property).Min,
            RigProperties.LimitMax => Limit(id, property).Max,
            _ => throw Unknown(property),
        };
    }

    /// <summary>The joint limits a limit property writes to; a node without declared limits exposes none.</summary>
    private RigLimit Limit(string id, PropertyRef property) =>
        Limits[id] ?? throw new InvalidDataException($"{property}: '{id}' declares no joint limits; add them on the base model.");

    public void Set(PropertyRef property, float value)
    {
        if (!float.IsFinite(value))
        {
            throw new InvalidDataException($"{property}: the bound value is not finite.");
        }

        // A binding at its identity (a multiplier of 1) leaves the authored value, and the node, untouched.
        if (value == Get(property))
        {
            return;
        }

        if (property.Attachment is { } attachment)
        {
            var index = AttachmentIndex(attachment);
            Attachments[index] = property.Property switch
            {
                RigProperties.X => Attachments[index] with { X = value },
                RigProperties.Y => Attachments[index] with { Y = value },
                RigProperties.Angle => Attachments[index] with { Angle = value },
                RigProperties.Scale => Attachments[index] with { Scale = value },
                RigProperties.Layer => Attachments[index] with { Layer = value },
                RigProperties.FitX => Attachments[index] with { FitX = value },
                RigProperties.FitY => Attachments[index] with { FitY = value },
                _ => throw Unknown(property),
            };
            Attachments[index].Validate($"{property}");
            return;
        }

        if (property.Part is { } part)
        {
            var index = PartIndex(part);
            Parts[index] = property.Property switch
            {
                RigProperties.ScaleX => Parts[index] with { ScaleX = value },
                RigProperties.ScaleY => Parts[index] with { ScaleY = value },
                RigProperties.OffsetX => Parts[index] with { OffsetX = value },
                RigProperties.OffsetY => Parts[index] with { OffsetY = value },
                RigProperties.FaceX => Parts[index] with { Face = Face(Parts[index], property) with { OffsetX = value } },
                _ => throw Unknown(property),
            };
            return;
        }

        var id = property.Control ?? throw Unknown(property);
        switch (property.Property)
        {
            case RigProperties.Length:
                new Limit(0, 100).Check(value, $"{property}");
                Lengths[id] = value;
                break;
            case RigProperties.LocalX: Setups[id].X = value; break;
            case RigProperties.LocalY: Setups[id].Y = value; break;
            case RigProperties.LocalRotation: Setups[id].Rotation = value; break;
            case RigProperties.LocalLayer: Setups[id].Layer = value; break;
            case RigProperties.LinkT: Links[id]!.T = value; break;
            case RigProperties.LinkOffsetX: Links[id]!.OffsetX = value; break;
            case RigProperties.LinkOffsetY: Links[id]!.OffsetY = value; break;
            // A limit bounds the rotation without moving the rest: the node stays where it was.
            case RigProperties.LimitMin: Limit(id, property).Min = value; return;
            case RigProperties.LimitMax: Limit(id, property).Max = value; return;
            default: throw Unknown(property);
        }
        Moved.Add(id);
    }

    /// <summary>
    /// Writes one explicit override after the bindings (plan section 3.7 step 3): a number pins the value (a clear pins the
    /// authored value read from <paramref name="authored"/>, bypassing the binding); a flag, selection or material replaces
    /// the base's, a clear restoring the property's empty value.
    /// </summary>
    public void Apply(PropertyOverride change, PropertyDeclaration declaration, RigLayout authored)
    {
        var target = change.Target;
        if (declaration.Kind == PropertyKinds.Number)
        {
            Set(target, change.Clear ? authored.Get(target) : change.Value!.Value);
            return;
        }

        if (target.Part is { } part)
        {
            var index = PartIndex(part);
            var current = Parts[index];
            Parts[index] = declaration.Name switch
            {
                RigProperties.Material => current with { Material = current.RenderMaterial.WithOverride(change.Material) },
                RigProperties.Face => current with { Face = Face(current, target) with { Expression = change.Clear ? declaration.Cleared! : change.Selection! } },
                RigProperties.Hidden => current with { Hidden = !change.Clear && change.Flag!.Value },
                _ => throw Unknown(target),
            };
            return;
        }

        if (target.Attachment is { } attachment)
        {
            var index = AttachmentIndex(attachment);
            var current = Attachments[index];
            Attachments[index] = declaration.Name switch
            {
                RigProperties.Asset => current with { Asset = change.Selection! },
                RigProperties.Mirror => current with { Mirror = !change.Clear && change.Flag!.Value },
                RigProperties.Material => current with { Material = change.Clear ? null : current.Material is null ? change.Material : current.Material.WithOverride(change.Material) },
                RigProperties.Hidden => current with { Hidden = !change.Clear && change.Flag!.Value },
                RigProperties.Expression => current with { Expression = change.Clear ? null : change.Selection },
                _ => throw Unknown(target),
            };
            return;
        }

        throw Unknown(target);
    }

    private int PartIndex(string part)
    {
        var index = Parts.FindIndex(p => p.Id == part);
        return index >= 0 ? index : throw new InvalidDataException($"No part '{part}'.");
    }

    /// <summary>The face payload a face property writes to; a part without one exposes no face property.</summary>
    private static FaceAppearance Face(PuppetPart part, PropertyRef property) =>
        part.Face ?? throw new InvalidDataException($"{property}: {RigProperties.Refusal(part, property.Property)}");

    private int AttachmentIndex(string attachment)
    {
        var index = Attachments.FindIndex(a => a.Id == attachment);
        return index >= 0 ? index : throw new InvalidDataException($"No attachment '{attachment}'.");
    }

    private static InvalidDataException Unknown(PropertyRef property) => new($"{property}: no such property.");
}

/// <summary>
/// A model's declared parameters compiled and validated once: IDs unique, defaults and presets in range, every binding
/// aimed at an exposed property that exists, one binding per property, references resolvable and the dependency graph
/// acyclic. <see cref="Apply"/> evaluates the bindings in dependency order onto a <see cref="RigLayout"/>; the inspectors ask
/// <see cref="BindingOf(PropertyRef)"/> which parameter produces a property.
/// </summary>
public sealed class ParameterCatalog
{
    private sealed record Entry(ModelParameter Parameter, ParameterBinding Binding);
    private readonly Dictionary<string, Entry> _byTarget = new(StringComparer.Ordinal);
    private readonly List<Entry> _order = [];

    private readonly Dictionary<string, ModelParameter> _byId = new(StringComparer.Ordinal);

    private ParameterCatalog(CharacterModel model)
    {
        Model = model;
        Parameters = model.Parameters;
    }

    public CharacterModel Model { get; }
    public IReadOnlyList<ModelParameter> Parameters { get; }
    public IReadOnlyDictionary<string, ModelParameter> ById => _byId;
    public IReadOnlyList<ParameterPreset> Presets => Model.Presets;
    public bool IsEmpty => Parameters.Count == 0;

    /// <summary>Compiles and validates; throws <see cref="InvalidDataException"/> naming the offending declaration.</summary>
    public static ParameterCatalog Compile(CharacterModel model)
    {
        var owner = $"Model '{model.Id}'";
        if (model.Parameters is null || model.Presets is null)
        {
            throw new InvalidDataException($"{owner}: parameters and presets cannot be null.");
        }

        var catalog = new ParameterCatalog(model);
        foreach (var parameter in model.Parameters)
        {
            if (parameter is null)
            {
                throw new InvalidDataException($"{owner}: null parameter.");
            }

            parameter.Validate(owner);
            if (!catalog._byId.TryAdd(parameter.Id, parameter))
            {
                throw new InvalidDataException($"{owner}: duplicate parameter '{parameter.Id}'.");
            }

            for (var i = 0; i < parameter.Bindings.Count; i++)
            {
                var binding = parameter.Bindings[i];
                var field = $"{owner} parameter '{parameter.Id}' binding {i + 1}";
                Check(binding.Target, field + " target", model);
                if (binding.Reference is { } reference)
                {
                    Check(reference, field + " reference", model);
                }

                var entry = new Entry(parameter, binding);
                if (!catalog._byTarget.TryAdd(binding.Target.Key, entry))
                {
                    throw new InvalidDataException($"{field}: {binding.Target} is already bound by parameter '{catalog._byTarget[binding.Target.Key].Parameter.Id}'; a property has one binding.");
                }
            }
        }
        var presets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var preset in model.Presets)
        {
            if (preset?.Values is null)
            {
                throw new InvalidDataException($"{owner}: incomplete preset.");
            }

            AuthoredAsset.RequireId(preset.Id, $"{owner} preset id");
            if (!presets.Add(preset.Id))
            {
                throw new InvalidDataException($"{owner}: duplicate preset '{preset.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(preset.Name))
            {
                throw new InvalidDataException($"{owner} preset '{preset.Id}': a name is required.");
            }

            foreach (var (id, value) in preset.Values)
            {
                var parameter = catalog.ById.GetValueOrDefault(id) ?? throw new InvalidDataException($"{owner} preset '{preset.Id}': no parameter '{id}'.");
                new Limit(parameter.Min, parameter.Max).Check(value, $"{owner} preset '{preset.Id}' {id}");
            }
        }
        catalog.Sort(owner);
        return catalog;
    }

    /// <summary>A binding's target or reference must be a declared, bindable (number) property of an existing owner.</summary>
    private static void Check(PropertyRef property, string field, CharacterModel model)
    {
        if (property.Role is not null)
        {
            throw new InvalidDataException($"{field}: an animation role is not bindable; override its clip on a variant instead.");
        }

        var declaration = RigProperties.Declaration(property, model, field);
        if (!declaration.Bindable)
        {
            throw new InvalidDataException($"{field}: '{declaration.Name}' is a {declaration.Kind}; only number properties are bindable.");
        }
    }

    /// <summary>Orders the bindings so a reference to a bound property reads its final value; a cycle is reported with its path.</summary>
    private void Sort(string owner)
    {
        var state = new Dictionary<string, int>(StringComparer.Ordinal);
        var path = new Stack<string>();
        void Visit(Entry entry)
        {
            var key = entry.Binding.Target.Key;
            if (state.TryGetValue(key, out var seen))
            {
                if (seen == 1)
                {
                    var cycle = path.Reverse().SkipWhile(k => k != key).Append(key).Select(k => _byTarget[k].Binding.Target.ToString());
                    throw new InvalidDataException($"{owner}: parameter bindings form a cycle: {string.Join(" -> ", cycle)}.");
                }

                return;
            }

            state[key] = 1;
            path.Push(key);
            if (entry.Binding.Reference is { } reference && _byTarget.TryGetValue(reference.Key, out var upstream))
            {
                Visit(upstream);
            }

            path.Pop();
            state[key] = 2;
            _order.Add(entry);
        }
        foreach (var parameter in Parameters)
        {
            foreach (var binding in parameter.Bindings)
            {
                Visit(_byTarget[binding.Target.Key]);
            }
        }
    }

    /// <summary>Defaults with <paramref name="overrides"/> applied, each checked against its declaration; an unknown ID is an error.</summary>
    public Dictionary<string, float> Values(IReadOnlyDictionary<string, float>? overrides, string owner)
    {
        var values = Parameters.ToDictionary(p => p.Id, p => p.Default, StringComparer.Ordinal);
        if (overrides is null || overrides.Count == 0)
        {
            return values;
        }

        if (IsEmpty)
        {
            throw new InvalidDataException($"{owner}: base '{Model.Id}' exposes no parameters.");
        }

        foreach (var (id, value) in overrides)
        {
            var parameter = ById.GetValueOrDefault(id) ?? throw new InvalidDataException($"{owner} build.{id}: '{Model.Id}' has no such parameter.");
            new Limit(parameter.Min, parameter.Max).Check(value, $"{owner} build.{id}");
            values[id] = value;
        }

        return values;
    }

    /// <summary>Writes every bound property from the parameter values, references first.</summary>
    internal void Apply(RigLayout layout, IReadOnlyDictionary<string, float> values)
    {
        foreach (var (parameter, binding) in _order)
        {
            // A target is written once, so reading it here is its authored value; a named reference reads its final value.
            var reference = binding.Reference is { } named ? layout.Get(named) : layout.Get(binding.Target);
            layout.Set(binding.Target, binding.Evaluate(values[parameter.Id], reference));
        }
    }

    /// <summary>The parameter and binding producing a property, or null when it is authored directly.</summary>
    public (ModelParameter Parameter, ParameterBinding Binding)? BindingOf(PropertyRef property) =>
        _byTarget.TryGetValue(property.Key, out var entry) ? (entry.Parameter, entry.Binding) : null;

    public (ModelParameter Parameter, ParameterBinding Binding)? BindingOfControl(string control, string property) => BindingOf(PropertyRef.OfControl(control, property));
    public (ModelParameter Parameter, ParameterBinding Binding)? BindingOfPart(string part, string property) => BindingOf(PropertyRef.OfPart(part, property));
    public (ModelParameter Parameter, ParameterBinding Binding)? BindingOfAttachment(string attachment, string property) => BindingOf(PropertyRef.OfAttachment(attachment, property));

    /// <summary>Every binding, with its parameter, in evaluation order.</summary>
    public IEnumerable<(ModelParameter Parameter, ParameterBinding Binding)> Bindings => _order.Select(e => (e.Parameter, e.Binding));
}
