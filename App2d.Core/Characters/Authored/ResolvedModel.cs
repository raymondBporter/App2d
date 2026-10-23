using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>A base model with one variant's overrides applied, compiled once and shared by every actor that uses it.</summary>
public sealed class ResolvedModel
{
    private ResolvedModel(CharacterModel model, ModelVariant? variant, IReadOnlyList<ModelControl> order, Dictionary<string, RigNode> nodes, List<PuppetPart> parts)
    {
        Base = model;
        Variant = variant;
        Parts = parts;
        Order = order;
        Nodes = nodes;
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

    /// <summary>A control's rest placement: its rest frame at its absolute rest layer.</summary>
    public LayeredFrame2D RestPlacement(string control) => new(new Frame2D(RestTransforms[control]), Rest[control].Z);
    public string TextureRoot { get; set; } = Environment.CurrentDirectory;
    public IReadOnlyList<PuppetPart> Parts { get; }
    public IReadOnlyDictionary<string, float> Measures { get; }

    /// <summary>Controls ordered so every parent precedes its children.</summary>
    public IReadOnlyList<ModelControl> Order { get; }
    public IReadOnlyDictionary<string, ModelControl> Controls { get; }
    public IReadOnlyDictionary<string, ModelChain> Chains { get; }
    /// <summary>The two-bone IK constraints in evaluation order: the model's one constraint list, in document order.</summary>
    public IReadOnlyList<ModelChain> IkConstraints { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Children { get; }

    /// <summary>The top of the rest pose's visible shapes above the feet: the height a drawn size is fitted to.</summary>
    public float DrawnHeight()
    {
        var rest = PoseEvaluator.Rest(this);
        return Parts.Where(p => !p.Hidden).SelectMany(p => PartContours2D.Contour(p, this, rest)).Select(p => p.Y).DefaultIfEmpty(1).Max();
    }

    public float Measure(string scale) => scale == CharacterModel.Unit ? 1 : Measures[scale];
    public float Length(string from, string to) => Vector2.Distance(new(Rest[from].X, Rest[from].Y), new(Rest[to].X, Rest[to].Y));

    public static ResolvedModel From(CharacterModel model, ModelVariant? variant = null)
    {
        model.Validate();
        variant?.Validate();
        var order = RigGraph.Order(model);
        var setups = model.Controls.Where(c => c.Transform is not null).ToDictionary(c => c.Id, c => c.Transform!, StringComparer.Ordinal);
        var rest = model.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal);
        // Bone nodes derive their rest; a build rule reads and writes complete character-frame positions, so give it the
        // derived values first and fit any bone node it moved afterwards (RigGraph.Fit, the P3b build adapter).
        var refit = new HashSet<string>(StringComparer.Ordinal);
        if (model.Controls.Any(c => c.Node == RigNodeKinds.Bone))
        {
            foreach (var (id, node) in RigGraph.Compile(model, order, rest, setups, refit))
            {
                if (node.Kind == RigNodeKinds.Bone)
                {
                    rest[id] = node.Rest;
                }
            }
        }
        var parts = model.Parts.ConvertAll(p => p with { });
        if (variant is not null)
        {
            var owner = $"Variant '{variant.Id}'";
            if (variant.Base != model.Id)
            {
                throw new InvalidDataException($"{owner} references base '{variant.Base}', not '{model.Id}'.");
            }

            BuildRules.CheckValues(model, variant.Build, owner);
            if (variant.Build.Count > 0)
            {
                var before = new Dictionary<string, Vector3>(rest, StringComparer.Ordinal);
                BuildRules.For(model)!.Apply(model, variant.Build, rest, parts);
                foreach (var control in model.Controls.Where(c => c.Node != RigNodeKinds.Point && rest[c.Id] != before[c.Id]))
                {
                    refit.Add(control.Id);
                }
            }

            foreach (var (id, point) in variant.Rest)
            {
                if (!rest.ContainsKey(id))
                {
                    throw new InvalidDataException($"{owner} rest.{id}: the base has no such control.");
                }

                var control = model.Controls.First(c => c.Id == id);
                if (control.Node == RigNodeKinds.Bone)
                {
                    // A bone node's rest is derived; freezing a character-frame position over it would reintroduce the
                    // absolute-rest override P6's typed property overrides replace.
                    throw new InvalidDataException($"{owner} rest.{id}: '{id}' is a bone node; its length and local setup are not variant-overridable until P6 typed overrides.");
                }

                rest[id] = point.XYZ;
                if (control.Node == RigNodeKinds.Affine)
                {
                    refit.Add(id);
                }
            }
            foreach (var (id, change) in variant.Parts)
            {
                var index = parts.FindIndex(p => p.Id == id);
                if (index < 0)
                {
                    throw new InvalidDataException($"{owner} parts.{id}: the base has no such part.");
                }

                parts[index] = Override(parts[index], change);
            }
        }
        Dictionary<string, RigNode> nodes;
        try
        {
            nodes = RigGraph.Compile(model, order, rest, setups, refit);
        }
        catch (InvalidDataException ex) when (variant is not null)
        {
            throw new InvalidDataException($"Variant '{variant.Id}': {ex.Message}", ex);
        }
        if (variant is not null)
        {
            CharacterModel.CheckGeometry(model, nodes.ToDictionary(n => n.Key, n => n.Value.Rest, StringComparer.Ordinal), $"Variant '{variant.Id}'");
        }

        for (var i = 0; i < parts.Count; i++)
        {
            parts[i] = parts[i] with { Material = parts[i].RenderMaterial.Resolve(model.Materials) };
        }
        return new(model, variant, order, nodes, parts);
    }

    public static PuppetPart Override(PuppetPart part, PartOverride change)
    {
        return part with
        {
            ScaleX = change.ScaleX ?? part.ScaleX,
            ScaleY = change.ScaleY ?? part.ScaleY,
            OffsetX = change.OffsetX ?? part.OffsetX,
            OffsetY = change.OffsetY ?? part.OffsetY,
            Material = part.RenderMaterial.WithOverride(change.Material),
            Face = change.Face ?? part.Face,
            FaceX = change.FaceX ?? part.FaceX,
            Hidden = change.Hidden ?? part.Hidden,
        };
    }
}
