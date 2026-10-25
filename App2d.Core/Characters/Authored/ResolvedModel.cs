using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A base model with one variant's overrides applied, compiled once and shared by every actor that uses it.</summary>
public sealed class ResolvedModel
{
    private readonly Func<string, PartAsset?>? _partOf;
    private readonly RigLayout _layout;
    private readonly RigLayout _authored;
    private readonly Dictionary<string, PropertyOverride> _overrides;

    private ResolvedModel(CharacterModel model, ModelVariant? variant, IReadOnlyList<ModelControl> order, Dictionary<string, RigNode> nodes, List<PuppetPart> parts,
        ParameterCatalog parameters, Dictionary<string, float> parameterValues, List<ResolvedAttachment> attachments, Dictionary<string, string> roleClips,
        RigLayout layout, RigLayout authored, Dictionary<string, PropertyOverride> overrides, Func<string, PartAsset?>? partOf)
    {
        Base = model;
        Variant = variant;
        Parts = parts;
        Attachments = attachments;
        RoleClips = roleClips;
        Order = order;
        Nodes = nodes;
        Parameters = parameters;
        ParameterValues = parameterValues;
        _layout = layout;
        _authored = authored;
        _overrides = overrides;
        _partOf = partOf;
        Controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        IkConstraints = [.. model.IkChains];
        Chains = IkConstraints.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Children = model.Controls.ToDictionary(c => c.Id, c => (IReadOnlyList<string>)[.. model.Controls.Where(child => child.Parent == c.Id).Select(child => child.Id)], StringComparer.Ordinal);
        Rest = order.ToDictionary(c => c.Id, c => nodes[c.Id].Rest, StringComparer.Ordinal);
        RestTransforms = order.ToDictionary(c => c.Id, c => nodes[c.Id].RestFrame, StringComparer.Ordinal);
        SetupTransforms = order.Where(c => nodes[c.Id].Affine is not null).ToDictionary(c => c.Id, c => nodes[c.Id].Affine!, StringComparer.Ordinal);
        // Authored rest Z is an absolute character layer; the evaluator composes layers by addition, so each node
        // carries only its offset from its parent (a root keeps its rest Z). Summing offsets down the tree reproduces
        // every authored value without double counting (character-migration-baseline.md, section 7.4).
        LayerOffsets = order.ToDictionary(c => c.Id, c => nodes[c.Id].Layer, StringComparer.Ordinal);
        BoneFrameIk.ValidateFrames(Controls, Chains.Values, RestTransforms);
        Measures = model.Measures.ToDictionary(m => m.Id, m => m.Path.Zip(m.Path.Skip(1)).Sum(p => Length(p.First, p.Second)), StringComparer.Ordinal);
    }

    public CharacterModel Base { get; }
    public ModelVariant? Variant { get; }
    public string Id => Variant?.Id ?? Base.Id;
    /// <summary>The canonical rig graph: every control's parent-local similarity setup, dimension, link and derived rest frame.</summary>
    public IReadOnlyDictionary<string, RigNode> Nodes { get; }
    /// <summary>Each control's derived rest position in the character frame, at its absolute rest layer.</summary>
    public IReadOnlyDictionary<string, Vector3> Rest { get; }
    public IReadOnlyDictionary<string, Matrix3x2> RestTransforms { get; }
    /// <summary>The legacy affine adapter's setups: only <see cref="RigNodeKinds.Affine"/> nodes have one.</summary>
    public IReadOnlyDictionary<string, Affine2D> SetupTransforms { get; }
    /// <summary>Each control's drawing layer relative to its parent (a root's is its rest Z). Composed layers reproduce the authored absolute rest Z.</summary>
    public IReadOnlyDictionary<string, float> LayerOffsets { get; }
    /// <summary>The base's declared parameters, compiled and validated once with their bindings.</summary>
    public ParameterCatalog Parameters { get; }
    /// <summary>The parameter values this resolution used: the declared defaults with the variant's values over them.</summary>
    public IReadOnlyDictionary<string, float> ParameterValues { get; }
    /// <summary>The variant's animation-role overrides: role to clip ID, applied over the base's motion set when an entity compiles.</summary>
    public IReadOnlyDictionary<string, string> RoleClips { get; }

    /// <summary>A control's rest placement: its rest frame at its absolute rest layer.</summary>
    public LayeredFrame2D RestPlacement(string control) => new(new Frame2D(RestTransforms[control]), Rest[control].Z);
    public string TextureRoot { get; set; } = Environment.CurrentDirectory;
    public IReadOnlyList<PuppetPart> Parts { get; }
    /// <summary>Library part instances resolved for this wearer, in the base's order, with the variant's overrides applied.</summary>
    public IReadOnlyList<ResolvedAttachment> Attachments { get; }
    public ResolvedAttachment? Attachment(string? id) => Attachments.FirstOrDefault(a => a.Id == id);
    public IReadOnlyDictionary<string, float> Measures { get; }

    /// <summary>Controls ordered so every parent precedes its children.</summary>
    public IReadOnlyList<ModelControl> Order { get; }
    public IReadOnlyDictionary<string, ModelControl> Controls { get; }
    public IReadOnlyDictionary<string, ModelChain> Chains { get; }
    /// <summary>The two-bone IK constraints in evaluation order: the model's one constraint list, in document order.</summary>
    public IReadOnlyList<ModelChain> IkConstraints { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Children { get; }

    // ---- Provenance (plan section 3.10) -----------------------------------------------------------------------------

    /// <summary>Where a property's resolved value came from: the base, a parameter binding, or this variant's override or clear.</summary>
    public PropertySource Provenance(PropertyRef property) =>
        _overrides.TryGetValue(property.Key, out var change) ? change.Clear ? PropertySource.Cleared : PropertySource.Override
        : Parameters.BindingOf(property) is not null ? PropertySource.ParameterBinding
        : PropertySource.Base;

    /// <summary>The variant's override of a property, if any.</summary>
    public PropertyOverride? Override(PropertyRef property) => _overrides.GetValueOrDefault(property.Key);
    /// <summary>Whether a variant override masks a parameter binding on the same property.</summary>
    public bool Masks(PropertyRef property) => _overrides.ContainsKey(property.Key) && Parameters.BindingOf(property) is not null;
    /// <summary>A number property's final resolved value.</summary>
    public float Number(PropertyRef property) => _layout.Get(property);
    /// <summary>A number property's authored value before any binding or override.</summary>
    public float AuthoredNumber(PropertyRef property) => _authored.Get(property);
    /// <summary>The same base resolved with another variant, through the same part library.</summary>
    public ResolvedModel With(ModelVariant? variant) => From(Base, variant, _partOf);

    /// <summary>The top of the rest pose's visible shapes above the feet: the height a drawn size is fitted to.</summary>
    public float DrawnHeight()
    {
        var rest = PoseEvaluator.Rest(this);
        return Parts.Where(p => !p.Hidden).SelectMany(p => PartContours2D.Contour(p, this, rest)).Select(p => p.Y).DefaultIfEmpty(1).Max();
    }

    public float Measure(string scale) => scale == CharacterModel.Unit ? 1 : Measures[scale];
    public float Length(string from, string to) => Vector2.Distance(new(Rest[from].X, Rest[from].Y), new(Rest[to].X, Rest[to].Y));

    /// <summary>
    /// Resolution order (plan section 3.7): the authored rig and resource defaults; the variant's parameter values over the
    /// declared defaults; the parameter bindings onto the exposed properties; the variant's explicit typed overrides over
    /// bound values (a set pins the value, a clear pins the authored or empty value); then the derived positions, frames,
    /// fitting, attachments and measures.
    /// </summary>
    public static ResolvedModel From(CharacterModel model, ModelVariant? variant = null, Func<string, PartAsset?>? partOf = null) => Resolve(model, variant, partOf, rigOnly: false);

    /// <summary>
    /// The rig alone: rest geometry, frames, parameters and overrides, with the wearer's attachment instances left
    /// unresolved (<see cref="Attachments"/> is empty). For geometry edits, retargeting and rest comparisons that never
    /// draw or validate instances; anything that draws, validates clips with plays or builds an entity resolves through
    /// <see cref="From"/> with the part library.
    /// </summary>
    public static ResolvedModel Rig(CharacterModel model, ModelVariant? variant = null) => Resolve(model, variant, null, rigOnly: true);

    private static ResolvedModel Resolve(CharacterModel model, ModelVariant? variant, Func<string, PartAsset?>? partOf, bool rigOnly)
    {
        model.Validate();
        variant?.Validate();
        var order = RigGraph.Order(model);
        var parameters = ParameterCatalog.Compile(model);
        var baseNodes = RigGraph.Compile(model, order, null);
        var layout = RigLayout.From(model, baseNodes);
        var authored = RigLayout.From(model, baseNodes);
        var owner = variant is null ? $"Model '{model.Id}'" : $"Variant '{variant.Id}'";
        if (variant is not null && variant.Base != model.Id)
        {
            throw new InvalidDataException($"{owner} references base '{variant.Base}', not '{model.Id}'.");
        }

        var values = parameters.Values(variant?.Parameters, owner);
        parameters.Apply(layout, values);
        var overrides = new Dictionary<string, PropertyOverride>(StringComparer.Ordinal);
        var roleClips = new Dictionary<string, string>(StringComparer.Ordinal);
        if (variant is not null)
        {
            foreach (var change in variant.Overrides)
            {
                var field = $"{owner} overrides";
                var declaration = RigProperties.Declaration(change.Target, model, field);
                change.Check(declaration, field);
                overrides[change.Key] = change;
                if (change.Target.Role is { } role)
                {
                    roleClips[role] = change.Selection!;
                }
                else
                {
                    layout.Apply(change, declaration, authored);
                }
            }
        }
        // Attachment instances resolve against the library: the asset by ID, the socket on this wearer, the variant's
        // overrides on this instance alone. Nothing copies the asset into the wearer.
        var attachments = new List<ResolvedAttachment>();
        if (model.Attachments.Count > 0 && !rigOnly)
        {
            if (partOf is null)
            {
                throw new InvalidDataException($"{owner}: resolving its {model.Attachments.Count} attachment(s) needs the part library (pass partOf).");
            }

            var sockets = model.Sockets.ToDictionary(s => s.Id, StringComparer.Ordinal);
            foreach (var instance in layout.Attachments)
            {
                // Bound placement and fit first (parameter bindings), then the wearer's explicit overrides already won in the layout.
                attachments.Add(ResolvedAttachment.Resolve(instance, partOf, sockets, owner));
            }
        }
        Dictionary<string, RigNode> nodes;
        try
        {
            nodes = RigGraph.Compile(model, order, layout);
        }
        catch (InvalidDataException ex) when (variant is not null)
        {
            throw new InvalidDataException($"{owner}: {ex.Message}", ex);
        }
        if (variant is not null || values.Any(v => v.Value != parameters.ById[v.Key].Default))
        {
            CharacterModel.CheckGeometry(model, nodes.ToDictionary(n => n.Key, n => n.Value.Rest, StringComparer.Ordinal), owner);
        }

        var parts = layout.Parts;
        for (var i = 0; i < parts.Count; i++)
        {
            parts[i] = parts[i] with { Material = parts[i].RenderMaterial.Resolve(model.Materials) };
        }
        return new(model, variant, order, nodes, parts, parameters, values, attachments, roleClips, layout, authored, overrides, partOf);
    }
}
