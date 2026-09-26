using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>One exposed build value, such as leg length: a multiplier on the template's proportions.</summary>
public sealed record BuildValue(string Id, string Name, Limit Range, float Default = 1);

/// <summary>A named starting build: values an author applies, never a live parent.</summary>
public sealed record BuildPreset(string Id, string Name, IReadOnlyDictionary<string, float> Values);

/// <summary>
/// Explicit, deterministic geometry for a template's build values. A model opts in by naming a rule; a variant stores only
/// the values. Resolution applies the rule to the base's rest pose first, then the variant's explicit overrides, so base
/// edits keep propagating. Deliberately code, not a parameter language: new templates add a rule here.
/// </summary>
public interface IBuildRule
{
    string Id { get; }
    IReadOnlyList<BuildValue> Values { get; }
    IReadOnlyList<BuildPreset> Presets { get; }
    /// <summary>Throws when the model lacks a control or part the rule's geometry needs.</summary>
    void Check(CharacterModel model);
    /// <summary>Rewrites rest positions and parts in place. Missing values take their defaults.</summary>
    void Apply(CharacterModel model, IReadOnlyDictionary<string, float> values, Dictionary<string, Vector3> rest, List<PuppetPart> parts);
}

public static class BuildRules
{
    private static readonly Dictionary<string, IBuildRule> Rules = new(StringComparer.Ordinal) { [PersonBuild.Rule.Id] = PersonBuild.Rule };
    public static IReadOnlyCollection<IBuildRule> All => Rules.Values;
    public static IBuildRule Get(string id) => Rules.TryGetValue(id, out var rule) ? rule : throw new InvalidDataException($"Unknown build rule '{id}'.");
    public static IBuildRule? For(CharacterModel model) => model.Build is { } id ? Get(id) : null;

    /// <summary>Checks a variant's stored values against its base's rule: known IDs, values in range.</summary>
    public static void CheckValues(CharacterModel model, IReadOnlyDictionary<string, float> values, string owner)
    {
        if (values.Count == 0) return;
        var rule = For(model) ?? throw new InvalidDataException($"{owner}: base '{model.Id}' exposes no build values.");
        foreach (var (id, value) in values)
        {
            var spec = rule.Values.FirstOrDefault(v => v.Id == id) ?? throw new InvalidDataException($"{owner} build.{id}: '{model.Id}' has no such build value.");
            spec.Range.Check(value, $"{owner} build.{id}");
        }
    }
}
