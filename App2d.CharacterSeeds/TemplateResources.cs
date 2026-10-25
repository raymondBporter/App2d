using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>
/// The anatomy templates as published resources (character-system migration, P10a): the version-2 recipes that declare part
/// slots, exposed dimensions and presets and the default animation library of the Person, the quadrupeds and the flier.
/// `--publish-templates` writes them beside the models with their declared dimensions through
/// <see cref="TemplatePublication"/>; nothing in the runtime or Studio names an anatomy.
/// </summary>
public static class TemplateResources
{
    public const string Face = "face", Shoe = "shoe", Clothing = "clothing", Hand = "hand", Headgear = "headgear", Saddle = "saddle";

    public static ModelTemplate Person() => new()
    {
        Id = "person",
        Name = "Person",
        Description = "A human rig with editable proportions, appearance, IK limbs and seven starter animations.",
        Model = "person",
        Animations = new()
        {
            ["idle"] = "person-idle",
            ["walk"] = "person-walk",
            ["run"] = "person-run",
            ["jump"] = "person-jump",
            ["hit"] = "person-hit",
            ["death"] = "person-death",
            ["heavy-walk"] = "person-heavy-walk",
        },
        PreviewAnimation = "idle",
        Slots =
        [
            new() { Id = "head-art", Usage = Face, Required = true, Default = ProceduralFace.AssetId },
            new() { Id = "body-art", Usage = Clothing },
            new() { Id = "left-hand-art", Usage = Hand },
            new() { Id = "right-hand-art", Usage = Hand },
            new() { Id = "left-foot-art", Usage = Shoe },
            new() { Id = "right-foot-art", Usage = Shoe },
        ],
        Dimensions = [PersonBuild.LegsId, PersonBuild.TorsoId, PersonBuild.ArmsId, PersonBuild.WidthId, PersonBuild.HeadId],
        Presets = ["standard", "tall-thin", "short-broad"],
        Library = new()
        {
            Roles = new() { ["idle"] = "idle", ["walk"] = "walk", ["run"] = "run", ["jump"] = "jump", ["hit"] = "hit", ["death"] = "death" },
            Masks = [Humanoid.UpperGroup, Humanoid.LowerGroup],
            Mappings = new() { [Humanoid.LegMeasure] = PersonBuild.LegsId, [Humanoid.ArmMeasure] = PersonBuild.ArmsId, [Humanoid.TorsoMeasure] = PersonBuild.TorsoId },
        },
    };

    public static ModelTemplate Quadruped(bool triceratops) => new()
    {
        Id = triceratops ? "triceratops" : "quadruped",
        Name = triceratops ? "Triceratops" : "Quadruped",
        Description = triceratops
            ? "A four-legged rig with editable frill, horns, cutout artwork and eight starter animations."
            : "A four-legged creature with planted IK targets, head and tail controls, and eight starter animations.",
        Model = triceratops ? "triceratops" : "quadruped",
        Animations = QuadrupedTemplate.Roles.ToDictionary(r => r, r => (triceratops ? "triceratops" : "quadruped") + "-" + r, StringComparer.Ordinal),
        PreviewAnimation = "idle",
        Slots =
        [
            new() { Id = "head-art", Usage = Headgear },
            new() { Id = "body-art", Usage = Saddle },
        ],
        Dimensions = [QuadrupedTemplate.LegsId, QuadrupedTemplate.BodyId, QuadrupedTemplate.HeadId],
        Presets = ["standard", "long-legged", "stocky"],
        Library = new()
        {
            Roles = QuadrupedTemplate.Roles.ToDictionary(r => r, r => r, StringComparer.Ordinal),
            Masks = ["head", "legs"],
            Mappings = new() { [QuadrupedTemplate.LegMeasure] = QuadrupedTemplate.LegsId, [QuadrupedTemplate.BodyMeasure] = QuadrupedTemplate.BodyId },
        },
    };

    public static ModelTemplate Pterosaur() => new()
    {
        Id = "pterosaur",
        Name = "Pterosaur",
        Description = "Articulated wings with attached membrane points, a jaw, and the flight pose studies.",
        Model = "pterosaur",
        Animations = PterosaurTemplate.ClipIds.ToDictionary(c => c, c => "pterosaur-" + c, StringComparer.Ordinal),
        PreviewAnimation = "hover",
        Slots =
        [
            new() { Id = "head-art", Usage = Headgear },
            new() { Id = "body-art", Usage = Saddle },
        ],
        Dimensions = [PterosaurTemplate.WingsId, PterosaurTemplate.BodyId, PterosaurTemplate.HeadId],
        Presets = ["standard", "broad-winged", "compact"],
        Library = new()
        {
            Roles = new() { ["idle"] = "hover", ["hover"] = "hover", ["fold"] = "fold", ["dive"] = "dive", ["brake"] = "brake", ["low-recovery"] = "low-recovery", ["reset"] = "reset" },
            Masks = ["wings", "head"],
            Mappings = new() { [PterosaurTemplate.WingMeasure] = PterosaurTemplate.WingsId, [PterosaurTemplate.BodyMeasure] = PterosaurTemplate.BodyId },
        },
    };

    /// <summary>Every published template with the declaration its model needs (null when the shipped model already declares its dimensions).</summary>
    public static IReadOnlyList<(ModelTemplate Template, Action<CharacterModel>? Declare)> All =>
    [
        (Person(), null),
        (Quadruped(false), QuadrupedTemplate.Dimensions),
        (Quadruped(true), QuadrupedTemplate.Dimensions),
        (Pterosaur(), PterosaurTemplate.Dimensions),
    ];

    /// <summary>Publishes every template on a scratch tree; the caller moves what it accepts into the authored tree.</summary>
    public static IReadOnlyList<TemplatePublication.Result> Publish(string root, string output) =>
        [.. All.Select(entry => TemplatePublication.Publish(root, entry.Template, entry.Declare, output))];
}
