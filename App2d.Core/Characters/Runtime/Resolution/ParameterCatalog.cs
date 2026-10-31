namespace App2d.Core.Characters.Authored;

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
