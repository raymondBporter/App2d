using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Graphics-free edits to a base model and a variant. Each structural edit validates the whole model before returning; callers
/// that must keep the previous state on failure (the editor's documents) roll back from their own snapshot.
/// </summary>
public static class ModelAuthoring
{
    public static CharacterModel Empty(string id, string name)
    {
        var model = new CharacterModel { Id = id, Name = name }; model.Validate(); return model;
    }

    /// <summary>The Person template under a new ID: a separate base, not a variant.</summary>
    public static CharacterModel FromPerson(string id, string name)
    {
        var model = PersonTemplate.Model(); model.Id = id; model.Name = name; model.Validate(); return model;
    }

    /// <summary>Flattens a resolved variant into a new independent base. Its clips must be checked or converted explicitly.</summary>
    public static CharacterModel Flatten(ResolvedModel resolved, string id, string name)
    {
        var model = CharacterModel.FromJson(resolved.Base.ToJson());
        model.Id = id; model.Name = name; model.StructureRevision = 1; model.Build = null;
        foreach (var control in model.Controls) control.Rest = PuppetPoint.From(resolved.Rest[control.Id]);
        model.Parts = [.. resolved.Parts.Select(p => p with { })];
        model.Validate(); return model;
    }

    /// <summary>What decides clip compatibility: controls and parents, chains, frames and default scales. Proportions and appearance never change it.</summary>
    public static string StructureSignature(CharacterModel model) => string.Join("|",
        model.Controls.Select(c => $"c:{c.Id}<{c.Parent}~{c.Scale}").Order(StringComparer.Ordinal)
            .Concat(model.Chains.Select(c => $"k:{c.Id}={c.Root}>{c.Joint}>{c.End}@{c.Frame}~{c.Scale}").Order(StringComparer.Ordinal))
            .Concat(model.Measures.Select(m => $"m:{m.Id}").Order(StringComparer.Ordinal)));

    public static string UniqueId(string basis, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal); var id = basis;
        for (var index = 2; used.Contains(id); index++) id = $"{basis}-{index}";
        return id;
    }

    private static IEnumerable<string> Names(CharacterModel model) =>
        model.Controls.Select(c => c.Id).Concat([CharacterModel.Locomotion, CharacterModel.Unit]);

    public static ModelControl AddControl(CharacterModel model, string? parent, Vector3 rest, string? id = null)
    {
        var control = new ModelControl { Id = id ?? UniqueId(parent is null ? "control" : parent + "-child", Names(model)), Parent = parent, Rest = PuppetPoint.From(rest) };
        model.Controls.Add(control); model.Validate(); return control;
    }

    /// <summary>
    /// Removes a control and the drawing parts attached to it, which are returned. Refused while other structure depends on it:
    /// children, chains, measures or chain frames must be changed first, so nothing is discarded silently.
    /// </summary>
    public static IReadOnlyList<PuppetPart> RemoveControl(CharacterModel model, string id)
    {
        var owner = $"Model '{model.Id}'";
        if (model.Controls.FirstOrDefault(c => c.Parent == id) is { } child) throw new InvalidOperationException($"{owner}: '{id}' still has child '{child.Id}'.");
        if (model.Chains.FirstOrDefault(c => c.Root == id || c.Joint == id || c.End == id || c.Frame == id) is { } chain) throw new InvalidOperationException($"{owner}: chain '{chain.Id}' uses '{id}'.");
        if (model.Measures.FirstOrDefault(m => m.Path.Contains(id)) is { } measure) throw new InvalidOperationException($"{owner}: measure '{measure.Id}' uses '{id}'.");
        var parts = model.Parts.Where(p => p.A == id || p.B == id).ToList();
        model.Parts.RemoveAll(parts.Contains); model.Controls.RemoveAll(c => c.Id == id);
        model.Validate(); return parts;
    }

    public static void Reparent(CharacterModel model, string id, string? parent)
    {
        Control(model, id).Parent = parent; model.Validate();
    }

    /// <summary>Makes <paramref name="end"/> the tip of a two-bone chain over its parent and grandparent.</summary>
    public static ModelChain AddChain(CharacterModel model, string end)
    {
        var joint = Control(model, end).Parent ?? throw new InvalidOperationException($"'{end}' needs a parent and grandparent to end a chain.");
        var root = Control(model, joint).Parent ?? throw new InvalidOperationException($"'{joint}' needs a parent to root a chain.");
        var chain = new ModelChain { Id = UniqueId(end + "-chain", model.Chains.Select(c => c.Id)), Root = root, Joint = joint, End = end };
        model.Chains.Add(chain); model.Validate(); return chain;
    }

    public static void RemoveChain(CharacterModel model, string id)
    {
        if (model.Chains.RemoveAll(c => c.Id == id) == 0) throw new InvalidOperationException($"No chain '{id}'.");
        model.Validate();
    }

    /// <summary>A measure along a chain's two bones, which its channels can then scale with.</summary>
    public static ModelMeasure AddMeasure(CharacterModel model, string id, IReadOnlyList<string> path)
    {
        AuthoredAsset.RequireId(id, "measure id");
        var measure = new ModelMeasure { Id = id, Path = [.. path] };
        model.Measures.Add(measure); model.Validate(); return measure;
    }

    public static void RemoveMeasure(CharacterModel model, string id)
    {
        if (model.Controls.Any(c => c.Scale == id) || model.Chains.Any(c => c.Scale == id)) throw new InvalidOperationException($"Measure '{id}' is still used as a scale.");
        model.Measures.RemoveAll(m => m.Id == id); model.Validate();
    }

    public static PuppetPart AddPart(CharacterModel model, string kind, string a, string? b = null)
    {
        var part = new PuppetPart { Id = UniqueId(kind, model.Parts.Select(p => p.Id)), Kind = kind, A = a, B = b, Face = "none" };
        if (kind == "stroke") part.Width = model.LineWidth;
        model.Parts.Add(part); model.Validate(); return part;
    }

    public static void RemovePart(CharacterModel model, string id)
    {
        if (model.Parts.RemoveAll(p => p.Id == id) == 0) throw new InvalidOperationException($"No part '{id}'.");
    }

    /// <summary>Moves a control's rest position, and with <paramref name="children"/> its whole subtree by the same amount.</summary>
    public static void MoveRest(CharacterModel model, string id, Vector3 position, bool children)
    {
        var delta = position - Control(model, id).Rest.XYZ;
        IEnumerable<ModelControl> moved = children ? Subtree(model.Controls, id) : [Control(model, id)];
        foreach (var control in moved) control.Rest = PuppetPoint.From(control.Rest.XYZ + delta);
    }

    /// <summary>
    /// Moves a control of a variant: writes rest overrides from its resolved positions, so unmoved controls keep following the base.
    /// </summary>
    public static void MoveRest(ResolvedModel resolved, ModelVariant variant, string id, Vector3 position, bool children)
    {
        var delta = position - resolved.Rest[id];
        IEnumerable<ModelControl> moved = children ? Subtree(resolved.Base.Controls, id) : [resolved.Controls[id]];
        foreach (var control in moved)
            variant.Rest[control.Id] = PuppetPoint.From(resolved.Rest[control.Id] + delta);
    }

    private static IEnumerable<ModelControl> Subtree(IReadOnlyList<ModelControl> controls, string id)
    {
        var members = new HashSet<string>(StringComparer.Ordinal) { id };
        // Parents precede children after enough passes; the tree is small, so repeat until nothing is added.
        for (var added = true; added;)
        {
            added = false;
            foreach (var control in controls) if (control.Parent is { } parent && members.Contains(parent) && members.Add(control.Id)) added = true;
        }
        return controls.Where(c => members.Contains(c.Id));
    }

    private static ModelControl Control(CharacterModel model, string id) =>
        model.Controls.FirstOrDefault(c => c.Id == id) ?? throw new InvalidOperationException($"Model '{model.Id}' has no control '{id}'.");
}
