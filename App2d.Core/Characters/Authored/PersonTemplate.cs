namespace App2d.Core.Characters;

/// <summary>The Person starting template. Built from the walk study's rest pose so converted studies reproduce at reference proportions. A template, not an engine category.</summary>
public static class PersonTemplate
{
    public const string Id = "person";

    public static CharacterModel Model()
    {
        var study = PuppetTemplates.StepStudy();
        var parents = study.Bones.ToDictionary(b => b.To, b => b.From, StringComparer.Ordinal);
        // Pelvis bob, stride and hip swing follow leg reach; the upper body follows torso length.
        static string ScaleOf(string id) => id switch
        {
            "hips" or "left-hip" or "right-hip" => "leg",
            "chest" or "head" or "left-shoulder" or "right-shoulder" => "torso",
            _ => CharacterModel.Unit,
        };
        var model = new CharacterModel
        {
            Id = Id, Name = "Person", Ink = study.Ink, LineWidth = study.LineWidth,
            Controls = [.. study.Controls.Select(c => new ModelControl { Id = c.Id, Parent = parents.GetValueOrDefault(c.Id), Rest = c.Rest, Scale = ScaleOf(c.Id) })],
            Chains = [.. study.Chains.Select(c =>
            {
                var arm = c.End.EndsWith("-hand", StringComparison.Ordinal);
                return new ModelChain
                {
                    Id = c.End.Replace(arm ? "-hand" : "-foot", arm ? "-arm" : "-leg"), Root = c.Root, Joint = c.Joint, End = c.End, Bend = c.Bend,
                    Frame = arm ? c.Root : CharacterModel.Locomotion, Scale = arm ? "arm" : "leg",
                };
            })],
            Measures =
            [
                new() { Id = "leg", Path = ["right-hip", "right-knee", "right-foot"] },
                new() { Id = "arm", Path = ["right-shoulder", "right-elbow", "right-hand"] },
                new() { Id = "torso", Path = ["hips", "chest"] },
            ],
            Parts = [.. study.Parts.Select(p => p with { })],
        };
        model.Validate(); return model;
    }
}
