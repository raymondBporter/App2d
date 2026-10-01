using App2d.Core.Characters.Authored;

namespace App2d.CharacterStudio.PlayerMoves;

internal static partial class PlayerMoves
{
    // Normalized one-second gathering phases: presentation maps simulation progress onto these clips.
    private static MotionClip GunCharge(ResolvedModel m) => New(m, "player-gun-charge", "Spell shot: gather", 1, false)
        .Key(0, k => k.Chest(0).Head(.02f).RightHand(.645f, -.02f).LeftHand(.04f, -.5f).Blade(0))
        .Key(.45f, k => k.Chest(0).Head(-.03f).RightHand(.645f, -.02f).LeftHand(.35f, -.12f).Blade(0))
        .Key(1, k => k.Chest(0).Head(-.06f).RightHand(.645f, -.02f).LeftHand(.44f, -.06f).Blade(0))
        .Face(0, "focused").Face(.7f, "determined").Build();

    private static MotionClip HealGather(ResolvedModel m) => New(m, "player-heal-gather", "Heal: gather", 1, false)
        .Key(0, k => k.Hips(0, StanceY).Chest(0).Head(0).RightHand(.12f, -.35f).LeftHand(.18f, -.35f))
        .Key(.25f, k => k.Hips(0, StanceY - .04f).Chest(-.1f).Head(-.16f).RightHand(.2f, -.2f).LeftHand(.24f, -.16f))
        .Key(1, k => k.Hips(0, StanceY - .07f).Chest(-.14f).Head(-.2f).RightHand(.19f, -.18f).LeftHand(.23f, -.14f))
        .Plant("left-leg", 0, 1, Back).Plant("right-leg", 0, 1, Front)
        .Face(0, "focused").Build();

    public static void WriteSpells(string root)
    {
        var person = CharacterModel.FromJson(File.ReadAllText(Path.Combine(root, "models", "person.json")));
        var reference = CharacterModel.FromJson(person.ToJson());
        var study = PersonTemplate.StudyReference();
        foreach (var control in reference.Controls) control.Rest = study.Controls.Single(c => c.Id == control.Id).Rest;
        var model = ResolvedModel.From(reference);
        foreach (var clip in new[] { GunCharge(model), HealGather(model) })
            clip.Save(Path.Combine(root, "animations", clip.Id + ".json"));
    }
}
