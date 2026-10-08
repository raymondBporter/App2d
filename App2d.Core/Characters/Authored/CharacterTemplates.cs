namespace App2d.Core.Characters.Authored;

/// <summary>A starting document bundle. Templates are authoring conveniences, never runtime character categories.</summary>
public sealed record CharacterTemplate(string Id, string Name, string Description,
    Func<string, string, CharacterModel> Model, Func<CharacterModel, IReadOnlyList<MotionClip>> Clips, string? Preview = null);

/// <summary>The editor discovers all model starters here; adding a creature needs no species branch in the editor.</summary>
public static class CharacterTemplates
{
    public static IReadOnlyList<CharacterTemplate> All { get; } =
    [
        new("empty", "Empty", "Start with an empty rig and add controls and artwork.", ModelAuthoring.Empty, _ => []),
        new("person", "Person", "Independent person base with the shared human build controls.", ModelAuthoring.FromPerson, _ => []),
        new("quadruped", "Quadruped", "Four planted IK legs, head and tail; eight editable motion studies.", (id, name) => QuadrupedTemplate.Model(id, name), QuadrupedTemplate.Clips, "idle"),
        new("triceratops", "Triceratops", "Quadruped rig with a cutout frill, horns, painted spots and eight motion studies.", (id, name) => QuadrupedTemplate.Model(id, name, true), QuadrupedTemplate.Clips, "idle"),
        new("pterosaur", "Pterosaur", "Articulated wings with attached membrane points, a jaw, and six flight pose studies.", PterosaurTemplate.Model, PterosaurTemplate.Clips, "hover"),
    ];

    public static CharacterTemplate Get(string id) => All.FirstOrDefault(t => t.Id == id)
        ?? throw new InvalidDataException($"Unknown model template '{id}'.");
}
