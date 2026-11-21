using App2d.Core.Validation;
using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>The value kinds an exposed property can hold (character-system migration, P6a). Overrides, inspectors and conversions switch on this once.</summary>
public static class PropertyKinds
{
    public const string Number = "number";
    public const string Flag = "flag";
    /// <summary>A discrete choice by stable ID or vocabulary word: a library part, a clip, an expression.</summary>
    public const string Selection = "selection";
    public const string Material = "material";
}

/// <summary>
/// One exposed property's declaration: its catalog name, value kind, units and hard range (numbers), fixed vocabulary
/// (selections with <see cref="Options"/>) or the resource kind a free selection names (<see cref="References"/>: "part",
/// "animation"), and what an explicit clear restores. Registered once in <see cref="RigProperties"/>; every override class,
/// inspector and conversion reads the declaration instead of carrying matching fields.
/// </summary>
public sealed record PropertyDeclaration(string Name, string Kind, string? Units = null, Limit? Range = null, IReadOnlyList<string>? Options = null, string? References = null, string? Help = null)
{
    /// <summary>Parameter bindings produce numbers only.</summary>
    public bool Bindable => Kind == PropertyKinds.Number;
    /// <summary>A cleared selection falls back to this word; null means a selection cannot be cleared (an instance needs an asset).</summary>
    public string? Cleared { get; init; }
    /// <summary>Whether an explicit clear is meaningful: false where the base value is required (a part needs a material).</summary>
    public bool Clearable { get; init; } = true;
    /// <summary>Whether an override may clear this property.</summary>
    public bool CanClear => Clearable && (Kind != PropertyKinds.Selection || Cleared is not null);
}

/// <summary>
/// An explicit override of one exposed property on a variant (plan section 3.7): the target by stable IDs, never by index,
/// and either a value of the property's declared kind or an explicit clear. Absent means inherit: the base's value, including
/// what its parameter bindings produce, keeps following base edits. Set pins this variant's value and masks a binding. Clear
/// pins the property's empty value: a number's authored value before any binding, no material, no face, not hidden, not
/// mirrored. Resetting removes the entry; it never writes a copy of the base value.
/// </summary>
public sealed record PropertyOverride
{
    public PropertyRef Target { get; set; } = new();
    public float? Value { get; set; }
    public bool? Flag { get; set; }
    public string? Selection { get; set; }
    public RenderMaterialDefinition2D? Material { get; set; }
    /// <summary>Explicit clear: the property's empty value, declared per property.</summary>
    public bool Clear { get; set; }

    [JsonIgnore] public string Key => Target.Key;

    /// <summary>The kind the stored value implies, or null for a clear.</summary>
    [JsonIgnore]
    public string? Kind => Value is not null ? PropertyKinds.Number : Flag is not null ? PropertyKinds.Flag : Selection is not null ? PropertyKinds.Selection : Material is not null ? PropertyKinds.Material : null;

    /// <summary>Structural validation without the base: one owner, one property, exactly one value member or a clear.</summary>
    public void Validate(string field)
    {
        if (Target is null)
        {
            throw new InvalidDataException($"{field}: a target is required.");
        }

        Target.Validate(field + " target");
        var members = new[] { Value is not null, Flag is not null, Selection is not null, Material is not null }.Count(set => set);
        if (Clear ? members != 0 : members != 1)
        {
            throw new InvalidDataException($"{field} {Target}: an override holds exactly one of value, flag, selection or material, or clears the property.");
        }

        if (Value is { } value) Ensure.Finite(value, field);

        Material?.Validate($"{field} {Target} material");
    }

    /// <summary>Checks the stored value against the property's declaration: kind, range, vocabulary, and whether a clear is allowed.</summary>
    public void Check(PropertyDeclaration declaration, string field)
    {
        if (Clear)
        {
            if (!declaration.CanClear)
            {
                throw new InvalidDataException($"{field} {Target}: '{declaration.Name}' cannot be cleared; set another {declaration.References ?? "value"} or reset the override.");
            }

            return;
        }

        if (Kind != declaration.Kind)
        {
            throw new InvalidDataException($"{field} {Target}: '{declaration.Name}' takes a {declaration.Kind}, not a {Kind}.");
        }

        if (Value is { } value)
        {
            declaration.Range?.Check(value, $"{field} {Target}");
        }

        if (Selection is { } selection)
        {
            if (declaration.Options is { } options)
            {
                if (!options.Contains(selection, StringComparer.Ordinal))
                {
                    throw new InvalidDataException($"{field} {Target}: unknown {declaration.Name} '{selection}' (one of {string.Join(", ", options)}).");
                }
            }
            else
            {
                AuthoredAsset.RequireId(selection, $"{field} {Target} {declaration.Name}");
            }
        }
    }
}
