using System.Text.Json.Serialization;

namespace App2d.Core.Characters.Authored;

/// <summary>
/// A stable address of one exposed property (character-system migration, P4a): the owning control or part by ID, never by
/// index, and the property's catalog name (<see cref="RigProperties"/>). Resolved into a typed accessor once per model.
/// </summary>
public sealed record PropertyRef
{
    public string? Control { get; set; }
    public string? Part { get; set; }
    /// <summary>A library part instance on the character (P5b): its placement and leaf fit are bindable too.</summary>
    public string? Attachment { get; set; }
    /// <summary>An animation role of the base's motion sets (P6a): a variant overrides the clip it plays. Not bindable.</summary>
    public string? Role { get; set; }
    public string Property { get; set; } = "";

    [JsonIgnore] public string Owner => Control ?? Part ?? Attachment ?? Role ?? "";
    [JsonIgnore] public string OwnerKind => Control is not null ? ControlKind : Part is not null ? PartKind : Attachment is not null ? AttachmentKind : RoleKind;
    [JsonIgnore] public string Key => OwnerKind + ":" + Owner + "." + Property;
    public const string ControlKind = "control", PartKind = "part", AttachmentKind = "attachment", RoleKind = "role";
    public override string ToString() => $"{OwnerKind} '{Owner}' {Property}";

    public static PropertyRef OfControl(string control, string property) => new() { Control = control, Property = property };
    public static PropertyRef OfPart(string part, string property) => new() { Part = part, Property = property };
    public static PropertyRef OfAttachment(string attachment, string property) => new() { Attachment = attachment, Property = property };
    public static PropertyRef OfRole(string role) => new() { Role = role, Property = RigProperties.Clip };

    public void Validate(string field)
    {
        if (new[] { Control, Part, Attachment, Role }.Count(o => o is not null) != 1)
        {
            throw new InvalidDataException($"{field}: name exactly one of control, part, attachment or role.");
        }

        if (string.IsNullOrWhiteSpace(Property))
        {
            throw new InvalidDataException($"{field}: a property name is required.");
        }
    }
}
