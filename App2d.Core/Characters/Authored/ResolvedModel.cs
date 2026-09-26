using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>A base model with one variant's overrides applied, compiled once and shared by every actor that uses it.</summary>
public sealed class ResolvedModel
{
    private ResolvedModel(CharacterModel model, ModelVariant? variant, Dictionary<string, Vector3> rest, List<PuppetPart> parts)
    {
        Base = model; Variant = variant; Rest = rest; Parts = parts;
        Controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Chains = model.Chains.ToDictionary(c => c.Id, StringComparer.Ordinal);
        Children = model.Controls.ToDictionary(c => c.Id, c => (IReadOnlyList<string>)model.Controls.Where(child => child.Parent == c.Id).Select(child => child.Id).ToList(), StringComparer.Ordinal);
        Measures = model.Measures.ToDictionary(m => m.Id, m => m.Path.Zip(m.Path.Skip(1)).Sum(p => Length(p.First, p.Second)), StringComparer.Ordinal);
        var order = new List<ModelControl>(); var placed = new HashSet<string>(StringComparer.Ordinal);
        void Place(ModelControl control)
        {
            if (!placed.Add(control.Id)) return;
            if (control.Parent is not null) Place(Controls[control.Parent]);
            order.Add(control);
        }
        foreach (var control in model.Controls) Place(control);
        Order = order;
    }

    public CharacterModel Base { get; }
    public ModelVariant? Variant { get; }
    public string Id => Variant?.Id ?? Base.Id;
    public IReadOnlyDictionary<string, Vector3> Rest { get; }
    public IReadOnlyList<PuppetPart> Parts { get; }
    public IReadOnlyDictionary<string, float> Measures { get; }
    /// <summary>Controls ordered so every parent precedes its children.</summary>
    public IReadOnlyList<ModelControl> Order { get; }
    public IReadOnlyDictionary<string, ModelControl> Controls { get; }
    public IReadOnlyDictionary<string, ModelChain> Chains { get; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Children { get; }

    /// <summary>The top of the rest pose's visible shapes above the feet: the height a drawn size is fitted to.</summary>
    public float DrawnHeight()
    {
        var rest = PoseEvaluator.Rest(this);
        return Parts.Where(p => !p.Hidden).SelectMany(p => PartGeometry.Contour(p, rest.World)).Select(p => p.Y).DefaultIfEmpty(1).Max();
    }

    public float Measure(string scale) => scale == CharacterModel.Unit ? 1 : Measures[scale];
    public float Length(string from, string to) => Vector2.Distance(new(Rest[from].X, Rest[from].Y), new(Rest[to].X, Rest[to].Y));

    public static ResolvedModel From(CharacterModel model, ModelVariant? variant = null)
    {
        model.Validate(); variant?.Validate();
        var rest = model.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ, StringComparer.Ordinal);
        var parts = model.Parts.Select(p => p with { }).ToList();
        if (variant is not null)
        {
            var owner = $"Variant '{variant.Id}'";
            if (variant.Base != model.Id) throw new InvalidDataException($"{owner} references base '{variant.Base}', not '{model.Id}'.");
            BuildRules.CheckValues(model, variant.Build, owner);
            if (variant.Build.Count > 0) BuildRules.For(model)!.Apply(model, variant.Build, rest, parts);
            foreach (var (id, point) in variant.Rest)
            {
                if (!rest.ContainsKey(id)) throw new InvalidDataException($"{owner} rest.{id}: the base has no such control.");
                rest[id] = point.XYZ;
            }
            foreach (var (id, change) in variant.Parts)
            {
                var index = parts.FindIndex(p => p.Id == id);
                if (index < 0) throw new InvalidDataException($"{owner} parts.{id}: the base has no such part.");
                parts[index] = Override(parts[index], change);
            }
            CharacterModel.CheckGeometry(model, rest, owner);
        }
        return new(model, variant, rest, parts);
    }

    public static PuppetPart Override(PuppetPart part, PartOverride change) => part with
    {
        Width = change.Width ?? part.Width, Height = change.Height ?? part.Height,
        OffsetX = change.OffsetX ?? part.OffsetX, OffsetY = change.OffsetY ?? part.OffsetY, Fill = change.Fill ?? part.Fill,
        Face = change.Face ?? part.Face, FaceX = change.FaceX ?? part.FaceX, Hidden = change.Hidden ?? part.Hidden,
    };
}
