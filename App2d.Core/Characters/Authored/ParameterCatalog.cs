namespace App2d.Core.Characters.Authored;

/// <summary>
/// The exposed property names a binding or override may address (character-system migration, P4a). Control properties are
/// the canonical node's dimension and layout: its bone length, its parent-local translation and layer offset, and its link's
/// normalized position and offset along the parent bone. Part properties are the leaf geometry fit of plan section 3.4,
/// never inherited by children. Names are stable IDs resolved once per model by <see cref="ParameterCatalog"/>.
/// </summary>
public static class RigProperties
{
    public const string Length = "length";
    public const string LocalX = "local.x", LocalY = "local.y", LocalLayer = "local.layer";
    public const string LinkT = "link.t", LinkOffsetX = "link.offsetX", LinkOffsetY = "link.offsetY";
    public const string ScaleX = "scaleX", ScaleY = "scaleY", OffsetX = "offsetX", OffsetY = "offsetY";
    public static readonly IReadOnlyList<string> Part = [ScaleX, ScaleY, OffsetX, OffsetY];

    /// <summary>The properties a control exposes by its declared kind and link: an affine node exposes none (adapter until P11).</summary>
    public static IReadOnlyList<string> ForControl(ModelControl control) => control.Node switch
    {
        RigNodeKinds.Affine => [],
        RigNodeKinds.Bone when control.Link is not null => [Length, LocalLayer, LinkT, LinkOffsetX, LinkOffsetY],
        RigNodeKinds.Bone => [Length, LocalX, LocalY, LocalLayer],
        _ => [LocalX, LocalY, LocalLayer],
    };

    /// <summary>Why a property is not exposed on a control, or null when it is.</summary>
    public static string? Refusal(ModelControl control, string property)
    {
        if (control.Node == RigNodeKinds.Affine)
        {
            return $"'{control.Id}' is a legacy affine node; the adapter exposes no bindable properties.";
        }

        if (ForControl(control).Contains(property, StringComparer.Ordinal))
        {
            return null;
        }

        return property switch
        {
            Length => $"'{control.Id}' is a point control; only a bone node exposes a length.",
            LocalX or LocalY => $"'{control.Id}' is bound to its parent bone; bind its link instead of a local translation.",
            LinkT or LinkOffsetX or LinkOffsetY => $"'{control.Id}' has no link; bind its local translation instead.",
            _ => $"no control property '{property}' (one of {string.Join(", ", ForControl(control))}).",
        };
    }
}

/// <summary>
/// The resolution state between the authored rig and the final compile: every node's parent-local setup, length and link
/// (point controls' setups derived from their authored rest), and the parts. Parameter bindings and explicit overrides write
/// here in the order of plan section 3.7; <see cref="RigGraph.Compile(CharacterModel, IReadOnlyList{ModelControl}, RigLayout, IReadOnlyDictionary{string, System.Numerics.Vector3})"/>
/// then derives positions and frames from the final values.
/// </summary>
internal sealed class RigLayout
{
    private RigLayout(Dictionary<string, RigSetup> setups, Dictionary<string, float> lengths, Dictionary<string, RigLink?> links, List<PuppetPart> parts)
    {
        Setups = setups;
        Lengths = lengths;
        Links = links;
        Parts = parts;
    }

    public Dictionary<string, RigSetup> Setups { get; }
    public Dictionary<string, float> Lengths { get; }
    public Dictionary<string, RigLink?> Links { get; }
    public List<PuppetPart> Parts { get; }
    /// <summary>Controls whose setup a binding or override changed. An untouched point control keeps its authored rest bit for bit.</summary>
    public HashSet<string> Moved { get; } = new(StringComparer.Ordinal);

    public static RigLayout From(CharacterModel model, IReadOnlyDictionary<string, RigNode> baseNodes) => new(
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Setup with { }, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Length, StringComparer.Ordinal),
        model.Controls.ToDictionary(c => c.Id, c => baseNodes[c.Id].Link is { } link ? link with { } : null, StringComparer.Ordinal),
        model.Parts.ConvertAll(p => p with { }));

    public float Get(PropertyRef property)
    {
        if (property.Part is { } part)
        {
            var p = Parts[PartIndex(part)];
            return property.Property switch
            {
                RigProperties.ScaleX => p.ScaleX,
                RigProperties.ScaleY => p.ScaleY,
                RigProperties.OffsetX => p.OffsetX,
                RigProperties.OffsetY => p.OffsetY,
                _ => throw Unknown(property),
            };
        }

        var id = property.Control!;
        return property.Property switch
        {
            RigProperties.Length => Lengths[id],
            RigProperties.LocalX => Setups[id].X,
            RigProperties.LocalY => Setups[id].Y,
            RigProperties.LocalLayer => Setups[id].Layer,
            RigProperties.LinkT => Links[id]!.T,
            RigProperties.LinkOffsetX => Links[id]!.OffsetX,
            RigProperties.LinkOffsetY => Links[id]!.OffsetY,
            _ => throw Unknown(property),
        };
    }

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

        if (property.Part is { } part)
        {
            var index = PartIndex(part);
            Parts[index] = property.Property switch
            {
                RigProperties.ScaleX => Parts[index] with { ScaleX = value },
                RigProperties.ScaleY => Parts[index] with { ScaleY = value },
                RigProperties.OffsetX => Parts[index] with { OffsetX = value },
                RigProperties.OffsetY => Parts[index] with { OffsetY = value },
                _ => throw Unknown(property),
            };
            return;
        }

        var id = property.Control!;
        switch (property.Property)
        {
            case RigProperties.Length:
                new Limit(0, 100).Check(value, $"{property}");
                Lengths[id] = value;
                break;
            case RigProperties.LocalX: Setups[id].X = value; break;
            case RigProperties.LocalY: Setups[id].Y = value; break;
            case RigProperties.LocalLayer: Setups[id].Layer = value; break;
            case RigProperties.LinkT: Links[id]!.T = value; break;
            case RigProperties.LinkOffsetX: Links[id]!.OffsetX = value; break;
            case RigProperties.LinkOffsetY: Links[id]!.OffsetY = value; break;
            default: throw Unknown(property);
        }
        Moved.Add(id);
    }

    private int PartIndex(string part)
    {
        var index = Parts.FindIndex(p => p.Id == part);
        return index >= 0 ? index : throw new InvalidDataException($"No part '{part}'.");
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
        var controls = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        var parts = model.Parts.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
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
                Check(binding.Target, field + " target", controls, parts);
                if (binding.Reference is { } reference)
                {
                    Check(reference, field + " reference", controls, parts);
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

    private static void Check(PropertyRef property, string field, Dictionary<string, ModelControl> controls, HashSet<string> parts)
    {
        if (property.Part is { } part)
        {
            if (!parts.Contains(part))
            {
                throw new InvalidDataException($"{field}: no part '{part}'.");
            }

            if (!RigProperties.Part.Contains(property.Property, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"{field}: no part property '{property.Property}' (one of {string.Join(", ", RigProperties.Part)}).");
            }

            return;
        }

        if (!controls.TryGetValue(property.Control!, out var control))
        {
            throw new InvalidDataException($"{field}: no control '{property.Control}'.");
        }

        if (RigProperties.Refusal(control, property.Property) is { } refusal)
        {
            throw new InvalidDataException($"{field}: {refusal}");
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

    /// <summary>Every binding, with its parameter, in evaluation order.</summary>
    public IEnumerable<(ModelParameter Parameter, ParameterBinding Binding)> Bindings => _order.Select(e => (e.Parameter, e.Binding));
}
