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
            Id = Id, Name = "Person", Ink = study.Ink, LineWidth = study.LineWidth, Build = PersonBuild.RuleId,
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
        StarterContent.AddPersonExtras(model);
        model.Validate(); return model;
    }

    /// <summary>Writes the Person model, its converted walk and run, the two reviewed builds and the starter entity content as authored assets.</summary>
    public static void WriteStudies(string authoredRoot)
    {
        var model = Model(); var walk = PuppetTemplates.StepStudy(); var run = PuppetTemplates.RunStudy();
        void Write(string folder, string id, string json)
        {
            var directory = Path.Combine(authoredRoot, folder); Directory.CreateDirectory(directory);
            AuthoredAsset.Write(Path.Combine(directory, id + ".json"), json);
        }
        Write("models", model.Id, model.ToJson());
        Write("animations", "person-walk", PuppetMotionConverter.Convert(walk, walk.Motions[0], model, "person-walk", "Walk", "leg").ToJson());
        Write("animations", "person-run", PuppetMotionConverter.Convert(run, run.Motions[0], model, "person-run", "Run", "leg").ToJson());
        Write("variants", "tall-thin", PersonBuild.TallThin.Apply(model, "tall-thin", "Tall and thin", "#d9e2ef").ToJson());
        Write("variants", "short-broad", PersonBuild.ShortBroad.Apply(model, "short-broad", "Short and broad", "#efdcc9").ToJson());
        StarterContent.Write(authoredRoot, model);
    }
}
