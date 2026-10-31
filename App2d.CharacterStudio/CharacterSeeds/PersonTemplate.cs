using App2d.CharacterStudio.Authoring;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;

namespace App2d.CharacterStudio.CharacterSeeds;

/// <summary>The Person starting template, with the shared short-legged proportions. A template, not an engine category.</summary>
public static class PersonTemplate
{
    public const string Id = "person";
    public const float LegLength = .8f;
    /// <summary>The Person's face instance (P7c): the baked procedural face part on the head socket.</summary>
    public const string FaceInstance = "face";
    private static Part? _face;

    /// <summary>The Person face part the procedural generator bakes, as the shipped `parts/face-person.json` carries it.</summary>
    public static Part Face => _face ??= ProceduralFace.Bake();

    /// <summary>The seed part library: the face. The shipped tree's library is the catalog's.</summary>
    public static Part? PartOf(string id) => id == ProceduralFace.AssetId ? Face : null;

    /// <summary>
    /// Instances the face on a Person model exactly as the P7c conversion placed it: on the head socket at the old payload
    /// offset, fitted by the head part's authored scale (bound to the head parameter like the part), just in front of it.
    /// </summary>
    public static PartInstance AttachFace(CharacterModel model)
    {
        var head = model.Parts.First(p => p.Id == Humanoid.Head);
        var instance = ModelAuthoring.AddAttachment(model, ProceduralFace.AssetId, Humanoid.HeadSocket, PartOf, FaceInstance);
        instance.X = .065f;
        instance.FitX = head.ScaleX;
        instance.FitY = head.ScaleY;
        instance.Layer = head.Depth - .002f;
        BindFace(model);
        var looks = LookPreset.ReadFrom(model);
        foreach (var (look, word) in new[] { ("slate", "focused"), ("ember", "determined"), ("night", "smug") })
        {
            if (looks.FirstOrDefault(l => l.Id == look) is { } preset)
            {
                preset.Expressions[FaceInstance] = word;
            }
        }
        LookPreset.WriteTo(model, looks);
        model.Validate();
        return instance;
    }

    /// <summary>The face instance's fit follows the head part's fit bindings (P7c): the same parameter drives both.</summary>
    private static void BindFace(CharacterModel model)
    {
        if (model.Attachments.All(a => a.Id != FaceInstance))
        {
            return;
        }

        foreach (var parameter in model.Parameters)
        {
            foreach (var binding in parameter.Bindings.Where(b => b.Target.Part == Humanoid.Head && b.Target.Property is RigProperties.ScaleX or RigProperties.ScaleY).ToArray())
            {
                var fit = binding.Target.Property == RigProperties.ScaleX ? RigProperties.FitX : RigProperties.FitY;
                if (parameter.Bindings.All(b => b.Target.Attachment != FaceInstance || b.Target.Property != fit))
                {
                    parameter.Bindings.Add(binding with { Target = PropertyRef.OfAttachment(FaceInstance, fit) });
                }
            }
        }
    }

    /// <summary>
    /// The one writer of the Person's proportion declarations (P4a, P11b): the five parameters with their bindings on the
    /// canonical limbs, the presets, and the face instance's fit bound like the head part. Template publication applies it to
    /// the shipped model, so the file carries exactly what the seed declares.
    /// </summary>
    public static void Declare(CharacterModel model)
    {
        PersonBuild.Declare(model);
        BindFace(model);
    }

    /// <summary>The Person model; with <paramref name="face"/> it also wears the face part (resolve it with <see cref="PartOf"/>).</summary>
    public static CharacterModel Model(string id = Id, string name = "Person", bool face = false)
    {
        var model = StudyReference();
        var built = ResolvedModel.From(model, new PersonBuild { Legs = LegLength }.Apply(model, "style", "Style"));
        // The study's legs baked at the template's length: a point control takes its built rest, a bone node its built setup and length.
        foreach (var control in model.Controls)
        {
            var node = built.Nodes[control.Id];
            if (control.Node == RigNodeKinds.Bone)
            {
                control.Local = node.Setup with { };
                control.Length = node.Length;
                control.Link = node.Link is { } link ? link with { } : null;
            }
            else
            {
                control.Rest = PuppetPoint.From(built.Rest[control.Id]);
            }
        }

        // Keep authored material references; the resolved drawing also contains inherited colors.
        for (var i = 0; i < model.Parts.Count; i++)
            model.Parts[i] = built.Parts[i] with { Material = model.Parts[i].Material };
        model.Id = id;
        model.Name = name;
        // The proportion parameters are declared on the baked structure (the limbs are bone constraints since the study reference is canonical, P11b).
        model.Parameters = [];
        model.Presets = [];
        Declare(model);
        model.Validate();
        if (face)
        {
            AttachFace(model);
        }

        return model;
    }

    /// <summary>The solved bone that each arm constraint roots on beneath its shoulder pivot (P11a).</summary>
    public static string? UpperArmBone(ModelChain chain) => chain.Id switch
    {
        Humanoid.LeftArm => Humanoid.LeftUpperArmBone,
        Humanoid.RightArm => Humanoid.RightUpperArmBone,
        _ => null,
    };

    /// <summary>Converts a point-rigged Person's four limbs onto bone constraints exactly as the P11a bundle conversion did.</summary>
    public static void ConvertLimbs(CharacterModel model)
    {
        var byId = model.Controls.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var chain in model.IkChains.Where(c => byId[c.Root].Node == RigNodeKinds.Point).ToArray())
        {
            RigConversion.ToBoneConstraint(model, chain.Id, [], UpperArmBone(chain));
        }
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
        // P11a/P11b: the limbs are canonical bone constraints (the shoulders as pivots, the upper arms as solved bones, the hips as
        // leg roots) before the proportions are declared as parameters with bindings (P4a), not a Core rule.
        ConvertLimbs(model);
        PersonBuild.Declare(model);
        model.Validate();
        return model;
    }

    /// <summary>Writes the Person model, its converted walk and run (and the heavy walk derived from it), the two reviewed builds and the starter entity content as authored assets.</summary>
    public static void WriteStudies(string authoredRoot, SeedReport? report = null)
    {
        report ??= new();
        var model = Model(face: true);
        var modelPath = Path.Combine(authoredRoot, "models", model.Id + ".json");
        if (File.Exists(modelPath))
        {
            foreach (var (id, material) in CharacterModel.Load(modelPath).Materials)
                model.Materials[id] = material;
        }
        var reference = StudyReference();
        var walk = PuppetTemplates.StepStudy();
        var run = PuppetTemplates.RunStudy();
        void Write(string folder, string id, string json) => report.Write(Path.Combine(authoredRoot, folder, id + ".json"), json);
        Write("parts", Face.Id, Face.ToJson());
        Write("models", model.Id, model.ToJson());
        var walkClip = PuppetMotionConverter.Convert(walk, walk.Motions[0], reference, "person-walk", "Walk", Humanoid.LegMeasure);
        Write("animations", "person-walk", walkClip.ToJson());
        Write("animations", StarterContent.HeavyWalk, StarterContent.PersonHeavyWalk(walkClip).ToJson());
        Write("animations", "person-run", PuppetMotionConverter.Convert(run, run.Motions[0], reference, "person-run", "Run", Humanoid.LegMeasure).ToJson());
        Write("variants", "tall-thin", PersonBuild.TallThin.Apply(model, "tall-thin", "Tall and thin", "#d9e2ef").ToJson());
        Write("variants", "short-broad", PersonBuild.ShortBroad.Apply(model, "short-broad", "Short and broad", "#efdcc9").ToJson());
        StarterContent.Write(authoredRoot, model, report);
    }
}
