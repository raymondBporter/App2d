using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>The Person starting template, with the shared short-legged proportions. A template, not an engine category.</summary>
public static class PersonTemplate
{
    public const string Id = "person";
    public const float LegLength = .8f;

    public static CharacterModel Model(string id = Id, string name = "Person")
    {
        var model = StudyReference();
        var built = ResolvedModel.From(model, new PersonBuild { Legs = LegLength }.Apply(model, "style", "Style"));
        foreach (var control in model.Controls)
        {
            control.Rest = PuppetPoint.From(built.Rest[control.Id]);
        }

        // Keep authored material references; the resolved drawing also contains inherited colors.
        for (var i = 0; i < model.Parts.Count; i++)
            model.Parts[i] = built.Parts[i] with { Material = model.Parts[i].Material };
        model.Id = id;
        model.Name = name;
        model.Validate();
        return model;
    }

    /// <summary>The original prototype proportions used to convert its absolute animation keys. Playback retargets these clips to the current model through their reference measures.</summary>
    public static CharacterModel StudyReference()
    {
        var study = PuppetTemplates.StepStudy();
        var parents = study.Bones.ToDictionary(b => b.To, b => b.From, StringComparer.Ordinal);
        // Pelvis bob, stride and hip swing follow leg reach; the upper body follows torso length.
        static string ScaleOf(string id) => id switch
        {
            Humanoid.Hips or Humanoid.LeftHip or Humanoid.RightHip => Humanoid.LegMeasure,
            Humanoid.Chest or Humanoid.Head or Humanoid.LeftShoulder or Humanoid.RightShoulder => Humanoid.TorsoMeasure,
            _ => CharacterModel.Unit,
        };
        var model = new CharacterModel
        {
            Id = Id,
            Name = "Person",
            Ink = study.Ink,
            LineWidth = study.LineWidth,
            Build = PersonBuild.RuleId,
            Controls = [.. study.Controls.Select(c => new ModelControl { Id = c.Id, Parent = parents.GetValueOrDefault(c.Id), Rest = c.Rest, Scale = ScaleOf(c.Id) })],
            Constraints = [.. study.Chains.Select(RigConstraint (c) =>
            {
                var arm = c.End is Humanoid.LeftHand or Humanoid.RightHand;
                return new ModelChain
                {
                    Id = c.End switch
                    {
                        Humanoid.LeftHand => Humanoid.LeftArm,
                        Humanoid.RightHand => Humanoid.RightArm,
                        Humanoid.LeftFoot => Humanoid.LeftLeg,
                        Humanoid.RightFoot => Humanoid.RightLeg,
                        _ => throw new InvalidDataException($"Unknown humanoid chain end '{c.End}'.")
                    },
                    Root = c.Root, Joint = c.Joint, End = c.End, Bend = c.Bend,
                    Frame = arm ? c.Root : CharacterModel.Locomotion, Scale = arm ? Humanoid.ArmMeasure : Humanoid.LegMeasure,
                };
            })],
            Measures =
            [
                new() { Id = Humanoid.LegMeasure, Path = [Humanoid.RightHip, Humanoid.RightKnee, Humanoid.RightFoot] },
                new() { Id = Humanoid.ArmMeasure, Path = [Humanoid.RightShoulder, Humanoid.RightElbow, Humanoid.RightHand] },
                new() { Id = Humanoid.TorsoMeasure, Path = [Humanoid.Hips, Humanoid.Chest] },
            ],
            Parts = [.. study.Parts.Select(p => p with { })],
        };
        StarterContent.AddPersonExtras(model);
        foreach (var side in new[] { "left", "right" })
        {
            foreach (var (start, joint, end, name) in new[] { ("shoulder", "elbow", "hand", "arm"), ("hip", "knee", "foot", "leg") })
            {
                // One rounded limb path over the three joints replaces the two straight strokes of the study.
                var a = side + "-" + start;
                var b = side + "-" + joint;
                var c = side + "-" + end;
                var part = model.Parts.Single(p => p.A == a && p.B == b);
                model.Parts.RemoveAll(p => p.A == b && p.B == c);
                part.Id = side + "-" + name;
                ModelAuthoring.SetRoundedPath(part, [a, b, c]);
            }
        }
        PersonWardrobe.AddBodyParts(model);
        model.Validate();
        return model;
    }

    /// <summary>Writes the Person model, its converted walk and run (and the heavy walk derived from it), the two reviewed builds and the starter entity content as authored assets.</summary>
    public static void WriteStudies(string authoredRoot)
    {
        var model = Model();
        var modelPath = Path.Combine(authoredRoot, "models", model.Id + ".json");
        if (File.Exists(modelPath))
        {
            foreach (var (id, material) in CharacterModel.Load(modelPath).Materials)
                model.Materials[id] = material;
        }
        var reference = StudyReference();
        var walk = PuppetTemplates.StepStudy();
        var run = PuppetTemplates.RunStudy();
        void Write(string folder, string id, string json)
        {
            var directory = Path.Combine(authoredRoot, folder);
            Directory.CreateDirectory(directory);
            AuthoredAsset.Write(Path.Combine(directory, id + ".json"), json);
        }
        Write("models", model.Id, model.ToJson());
        var walkClip = PuppetMotionConverter.Convert(walk, walk.Motions[0], reference, "person-walk", "Walk", Humanoid.LegMeasure);
        Write("animations", "person-walk", walkClip.ToJson());
        Write("animations", StarterContent.HeavyWalk, StarterContent.PersonHeavyWalk(walkClip).ToJson());
        Write("animations", "person-run", PuppetMotionConverter.Convert(run, run.Motions[0], reference, "person-run", "Run", Humanoid.LegMeasure).ToJson());
        Write("variants", "tall-thin", PersonBuild.TallThin.Apply(model, "tall-thin", "Tall and thin", "#d9e2ef").ToJson());
        Write("variants", "short-broad", PersonBuild.ShortBroad.Apply(model, "short-broad", "Short and broad", "#efdcc9").ToJson());
        StarterContent.Write(authoredRoot, model);
    }
}
