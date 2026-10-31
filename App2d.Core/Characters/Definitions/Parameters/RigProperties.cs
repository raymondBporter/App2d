namespace App2d.Core.Characters.Authored;

/// <summary>
/// The exposed property registry (character-system migration, P4a/P6a): every property a binding or override may address,
/// declared once with its kind, units, range or vocabulary. Control properties are the canonical node's dimension and setup:
/// its bone length, its parent-local translation, rotation and layer offset, and its link's normalized position and offset
/// along the parent bone. Part properties are the leaf geometry fit of plan section 3.4 (never inherited by children) plus
/// its material and visibility. Attachment properties are an instance's placement, fit, asset selection, mirror,
/// material and visibility. A role property binds an animation role to a clip. Names are stable IDs resolved once per model
/// by <see cref="ParameterCatalog"/> and <see cref="ResolvedModel"/>; a new property registers here and nowhere else.
/// </summary>
public static class RigProperties
{
    public const string Length = "length";
    public const string LocalX = "local.x", LocalY = "local.y", LocalRotation = "local.rotation", LocalLayer = "local.layer";
    public const string LinkT = "link.t", LinkOffsetX = "link.offsetX", LinkOffsetY = "link.offsetY";
    public const string ScaleX = "scaleX", ScaleY = "scaleY", OffsetX = "offsetX", OffsetY = "offsetY";
    public const string Material = "material", Hidden = "hidden";
    /// <summary>An attachment instance's placement on its socket and its leaf fit (P5b); never inherited by anything.</summary>
    public const string X = "x", Y = "y", Angle = "angle", Scale = "scale", Layer = "layer", FitX = "fitX", FitY = "fitY";
    public const string Asset = "asset", Mirror = "mirror";
    /// <summary>An animated part instance's default expression (P7a): one of the words its asset binds, or none.</summary>
    public const string Expression = "expression";
    /// <summary>The prefix of an instance's module inputs (P10b): <c>input.&lt;parameter&gt;</c>, declared per instance by the inputs it sets.</summary>
    public const string Input = "input.";
    private static readonly Limit Inputs = new(-10000, 10000);

    /// <summary>The declaration of one instance input: a bindable number whose range is the module parameter's own (checked at resolution).</summary>
    public static PropertyDeclaration InputDeclaration(string input) => new(Input + input, PropertyKinds.Number, ParameterUnits.Factor, Inputs, Help: $"The module's '{input}' input on this instance; the part declares its range.");
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
        new(Hidden, PropertyKinds.Flag),
    ];

    /// <summary>
    /// The typed overrides that take a part from <paramref name="was"/> to <paramref name="now"/> (every registered part
    /// property but the material, which merges separately): the registry reads each property once here, so a part-edit
    /// command carries no feature field.
    /// </summary>
    public static IEnumerable<PropertyOverride> PartChanges(PartGeometry was, PartGeometry now)
    {
        PropertyRef Target(string property) => PropertyRef.OfPart(was.Id, property);
        if (now.ScaleX != was.ScaleX) yield return PropertyOverride.Of(Target(ScaleX), now.ScaleX);
        if (now.ScaleY != was.ScaleY) yield return PropertyOverride.Of(Target(ScaleY), now.ScaleY);
        if (now.OffsetX != was.OffsetX) yield return PropertyOverride.Of(Target(OffsetX), now.OffsetX);
        if (now.OffsetY != was.OffsetY) yield return PropertyOverride.Of(Target(OffsetY), now.OffsetY);
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

    /// <summary>The properties a control exposes by its declared kind and link.</summary>
    public static IReadOnlyList<PropertyDeclaration> Declarations(ModelControl control)
    {
        IReadOnlyList<PropertyDeclaration> declarations = control.Node switch
        {
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
        RigNodeKinds.Bone when control.Limit is null => [(Limits, "no joint limits declared on this bone node; add them on the base model to override or bind them")],
        RigNodeKinds.Bone => [],
        _ => [(Limits, "a point control has no joint limits; convert the run to a bone chain on the base model")],
    };

    /// <summary>Why a property is not exposed on a control, or null when it is.</summary>
    public static string? Refusal(ModelControl control, string property)
    {
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
            var instance = model.Attachments.FirstOrDefault(a => a.Id == attachment) ?? throw new InvalidDataException($"{field}: no attachment '{attachment}'.");
            if (property.Property.StartsWith(Input, StringComparison.Ordinal))
            {
                // An input exists on the instance that sets it (P10b); the part's own declaration is checked when the instance resolves.
                var input = property.Property[Input.Length..];
                return instance.Inputs.ContainsKey(input) ? InputDeclaration(input)
                    : throw new InvalidDataException($"{field}: attachment '{attachment}' sets no input '{input}'{(instance.Inputs.Count == 0 ? "" : $" (one of {string.Join(", ", instance.Inputs.Keys)})")}; declare it on the instance first (path: {model.Id} > attachments > {attachment} > inputs).");
            }

            return AttachmentDeclarations.FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: no attachment property '{property.Property}' (one of {string.Join(", ", AttachmentDeclarations.Select(d => d.Name).Append(Input + "<input>"))}).");
        }

        if (property.Part is { } part)
        {
            var owner = model.Parts.FirstOrDefault(p => p.Id == part) ?? throw new InvalidDataException($"{field}: no part '{part}'.");
            _ = owner;
            return PartDeclarations.FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: no part property '{property.Property}' (one of {string.Join(", ", PartDeclarations.Select(d => d.Name))}).");
        }

        var control = model.Controls.FirstOrDefault(c => c.Id == property.Control) ?? throw new InvalidDataException($"{field}: no control '{property.Control}'.");
        return Declarations(control).FirstOrDefault(d => d.Name == property.Property) ?? throw new InvalidDataException($"{field}: {Refusal(control, property.Property)}");
    }
}
