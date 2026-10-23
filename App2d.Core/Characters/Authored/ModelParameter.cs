using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>The declared units of a parameter or channel. Display vocabulary; the evaluator reads the binding kind, not the unit.</summary>
public static class ParameterUnits
{
    /// <summary>A dimensionless multiplier, 1 being the authored value.</summary>
    public const string Factor = "factor";
    /// <summary>Authored (character) length units.</summary>
    public const string Length = "length";
    public const string Radians = "radians";
    /// <summary>0..1 along a declared range (a bone position <c>t</c>, a bend mapped through its limits).</summary>
    public const string Normalized = "normalized";
    /// <summary>Character drawing-layer units.</summary>
    public const string Layer = "layer";
    public static readonly string[] All = [Factor, Length, Radians, Normalized, Layer];
}

/// <summary>
/// A stable address of one exposed property (character-system migration, P4a): the owning control or part by ID, never by
/// index, and the property's catalog name (<see cref="RigProperties"/>). Resolved into a typed accessor once per model.
/// </summary>
public sealed record PropertyRef
{
    public string? Control { get; set; }
    public string? Part { get; set; }
    public string Property { get; set; } = "";

    [JsonIgnore] public string Owner => Control ?? Part ?? "";
    [JsonIgnore] public string Key => (Control is not null ? "control:" : "part:") + Owner + "." + Property;
    public override string ToString() => $"{(Control is not null ? "control" : "part")} '{Owner}' {Property}";

    public static PropertyRef OfControl(string control, string property) => new() { Control = control, Property = property };
    public static PropertyRef OfPart(string part, string property) => new() { Part = part, Property = property };

    public void Validate(string field)
    {
        if ((Control is null) == (Part is null))
        {
            throw new InvalidDataException($"{field}: name exactly one of control or part.");
        }

        if (string.IsNullOrWhiteSpace(Property))
        {
            throw new InvalidDataException($"{field}: a property name is required.");
        }
    }
}

/// <summary>The simple binding forms of plan section 3.5. No expression language.</summary>
public static class BindingKinds
{
    /// <summary>Direct assignment: the property takes the parameter's value.</summary>
    public const string Set = "set";
    /// <summary>
    /// Multiplier: <c>about + (reference - about) * parameter</c>, where the reference is the target's own authored value or
    /// a named property's final value. A plain multiplier has <c>about</c> 0; a leg stack scaled about the ground line uses
    /// the ground's height.
    /// </summary>
    public const string Scale = "scale";
    /// <summary>Linear range mapping: the parameter's <c>[fromMin, fromMax]</c> maps onto <c>[toMin, toMax]</c>, unclamped.</summary>
    public const string Map = "map";
    public static readonly string[] All = [Set, Scale, Map];
}

/// <summary>One target of a parameter. A property has at most one binding; an explicit override of the property wins over it.</summary>
public sealed record ParameterBinding
{
    public string Kind { get; set; } = BindingKinds.Set;
    public PropertyRef Target { get; set; } = new();
    /// <summary><see cref="BindingKinds.Scale"/> only: the property whose final value is multiplied. Null multiplies the target's own authored value.</summary>
    public PropertyRef? Reference { get; set; }
    /// <summary><see cref="BindingKinds.Scale"/> only: the value the multiplication is centred on.</summary>
    public float About { get; set; }
    public float FromMin { get; set; }
    public float FromMax { get; set; } = 1;
    public float ToMin { get; set; }
    public float ToMax { get; set; } = 1;

    /// <summary>The bound value for a parameter value, given the reference (the target's authored value when none is named).</summary>
    public float Evaluate(float parameter, float reference) => Kind switch
    {
        BindingKinds.Scale => About + (reference - About) * parameter,
        BindingKinds.Map => ToMin + (parameter - FromMin) / (FromMax - FromMin) * (ToMax - ToMin),
        _ => parameter,
    };

    /// <summary>A short description for inspectors: how the target is produced.</summary>
    public string Describe(string parameter) => Kind switch
    {
        BindingKinds.Scale when Reference is { } reference => About == 0 ? $"{reference} × {parameter}" : $"{About:0.###} + ({reference} − {About:0.###}) × {parameter}",
        BindingKinds.Scale => About == 0 ? $"authored × {parameter}" : $"{About:0.###} + (authored − {About:0.###}) × {parameter}",
        BindingKinds.Map => $"{parameter} [{FromMin:0.###}..{FromMax:0.###}] → [{ToMin:0.###}..{ToMax:0.###}]",
        _ => $"= {parameter}",
    };

    public void Validate(string field)
    {
        if (!BindingKinds.All.Contains(Kind, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{field}: unknown binding kind '{Kind}'.");
        }

        if (Target is null)
        {
            throw new InvalidDataException($"{field}: a target is required.");
        }

        Target.Validate(field + " target");
        Reference?.Validate(field + " reference");
        if (Kind != BindingKinds.Scale && (Reference is not null || About != 0))
        {
            throw new InvalidDataException($"{field}: reference and about belong to a scale binding.");
        }

        foreach (var (value, name) in new[] { (About, "about"), (FromMin, "fromMin"), (FromMax, "fromMax"), (ToMin, "toMin"), (ToMax, "toMax") })
        {
            new Limit(-10000, 10000).Check(value, $"{field} {name}");
        }

        if (Kind == BindingKinds.Map && FromMax == FromMin)
        {
            throw new InvalidDataException($"{field}: a map needs a non-empty source range.");
        }
    }
}

/// <summary>
/// A declared, exposed parameter (plan section 3.5): a stable ID, units, default, hard range, optional suggested editor
/// range and its simple target bindings. Variants store values by ID; resolution applies defaults, then the variant's
/// values, then the bindings, then any explicit property override, then derives positions, fitting and measures.
/// </summary>
public sealed record ModelParameter
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string Units { get; set; } = ParameterUnits.Factor;
    public float Default { get; set; } = 1;
    /// <summary>The hard range a file may hold.</summary>
    public float Min { get; set; }
    public float Max { get; set; } = 1;
    /// <summary>The editor's suggested slider span; null uses the hard range.</summary>
    public float? SoftMin { get; set; }
    public float? SoftMax { get; set; }
    public List<ParameterBinding> Bindings { get; set; } = [];

    [JsonIgnore] public string Label => string.IsNullOrWhiteSpace(Name) ? Id : Name;
    [JsonIgnore] public Limit Range => new Limit(Min, Max).Soft(SoftMin ?? Min, SoftMax ?? Max);

    public void Validate(string owner)
    {
        AuthoredAsset.RequireId(Id, owner + " parameter id");
        var field = $"{owner} parameter '{Id}'";
        if (!ParameterUnits.All.Contains(Units, StringComparer.Ordinal))
        {
            throw new InvalidDataException($"{field}: unknown units '{Units}'.");
        }

        if (!float.IsFinite(Min) || !float.IsFinite(Max) || Min >= Max)
        {
            throw new InvalidDataException($"{field}: the range needs min below max.");
        }

        new Limit(Min, Max).Check(Default, field + " default");
        if (SoftMin is { } softMin)
        {
            new Limit(Min, Max).Check(softMin, field + " softMin");
        }

        if (SoftMax is { } softMax)
        {
            new Limit(Min, Max).Check(softMax, field + " softMax");
        }

        if ((SoftMin ?? Min) >= (SoftMax ?? Max))
        {
            throw new InvalidDataException($"{field}: the suggested range needs softMin below softMax.");
        }

        if (Bindings is null)
        {
            throw new InvalidDataException($"{field}: bindings cannot be null.");
        }

        for (var i = 0; i < Bindings.Count; i++)
        {
            if (Bindings[i] is null)
            {
                throw new InvalidDataException($"{field}: null binding.");
            }

            Bindings[i].Validate($"{field} binding {i + 1}");
        }
    }
}

/// <summary>A named set of parameter values an author applies to a variant: a starting point, never a live parent.</summary>
public sealed record ParameterPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, float> Values { get; set; } = [];
}

/// <summary>
/// The declared meaning of every animated channel (plan section 3.5). Each existing timeline kind maps to one semantics
/// here, so a consumer reads the declaration instead of inferring units from a track name. Continuous channels go through the
/// shared keyframe sampler and curve rules; discrete channels hold their value until the next key.
/// </summary>
public static class ChannelSemantics
{
    public const string Angle = "angle";
    public const string NormalizedParameter = "normalized-parameter";
    public const string LocalTranslation = "local-translation";
    public const string IkTarget = "ik-target";
    public const string UniformPoseScale = "uniform-pose-scale";
    public const string ArtworkFit = "artwork-fit";
    public const string MaterialValue = "material-value";
    public const string AttachmentSelection = "attachment-selection";
    public const string EnabledState = "enabled-state";

    /// <summary>One declared channel meaning: its units, whether keys interpolate or hold, and what carries it today.</summary>
    public sealed record Declaration(string Semantics, string Units, bool Held, string Carrier);

    /// <summary>Every semantics the plan names, with the timeline that carries it; two are declared ahead of their first channel.</summary>
    public static readonly IReadOnlyList<Declaration> All =
    [
        new(LocalTranslation, ParameterUnits.Length, false, "translate tracks (x, y in the control's scale; z is a layer offset)"),
        new(Angle, ParameterUnits.Radians, false, "rotate tracks (accumulated, unwrapped) and socket orient tracks (twist, tilt, turn)"),
        new(IkTarget, ParameterUnits.Length, false, "target tracks in the chain's frame and scale"),
        new(UniformPoseScale, ParameterUnits.Factor, false, "scale tracks on canonical nodes (one multiplier, length stays a dimension)"),
        new(ArtworkFit, ParameterUnits.Factor, false, "scale and shear tracks on legacy affine nodes (adapter until P11)"),
        new(MaterialValue, ParameterUnits.Factor, false, "slot color timelines"),
        new(AttachmentSelection, ParameterUnits.Factor, true, "face keys, slot attachment keys and draw-order keys (held)"),
        new(NormalizedParameter, ParameterUnits.Normalized, false, "declared; first channels arrive with angular limits (P4b)"),
        new(EnabledState, ParameterUnits.Factor, true, "declared; first channels arrive with attached colliders (P8b)"),
    ];

    /// <summary>The semantics of a control, chain or socket track by its kind and the node it targets.</summary>
    public static string OfTrack(string kind, string? nodeKind) => kind switch
    {
        MotionClip.TranslateKind => LocalTranslation,
        MotionClip.RotateKind or MotionClip.OrientKind => Angle,
        MotionClip.TargetKind => IkTarget,
        MotionClip.ScaleKind => nodeKind == RigNodeKinds.Affine ? ArtworkFit : UniformPoseScale,
        MotionClip.ShearKind => ArtworkFit,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown track kind."),
    };

    public static Declaration Of(string semantics) => All.First(d => d.Semantics == semantics);
}
