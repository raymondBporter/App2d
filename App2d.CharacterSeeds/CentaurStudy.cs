using App2d.Core.Characters.Authored;

namespace App2d.CharacterSeeds;

/// <summary>
/// The centaur study (character-system migration, P10c): a character composed entirely from library modules and resources.
/// The wearer is one root control with two usage-declared sockets; the quadruped lower body mounts on the root and owns the
/// root motion, the Person upper body mounts on the lower body's own `body` control (a socket on a module control), so the
/// torso follows the gait; every clip selects each module's word through its play track, so the lower body runs a gait while
/// the upper body performs an independently bound action. Nothing here is a character class: `--write-centaur` writes the
/// model, its clips and its template, which the generic evaluator plays like any other model.
/// </summary>
public static class CentaurStudy
{
    public const string Id = "centaur", Lower = "lower", Upper = "upper", Mount = "mount", Saddle = "saddle";
    public const string UpperGroup = "upper-body", LowerGroup = "lower-body";
    public const string LegsId = "legs", ArmsId = "arms";

    public static CharacterModel Model()
    {
        var model = new CharacterModel { Id = Id, Name = "Centaur study", Ink = "#242a29", LineWidth = .045f };
        model.Controls.Add(new() { Id = "root", Rest = new(0, .92f, 0) });
        // A model carries at least one drawing part; the root's mark is hidden, the modules draw everything.
        model.Parts.Add(new() { Id = "root-mark", A = "root", Geometry = App2d.Core.Shapes.Ellipse2D.Builder.Create(new(.02f, .02f)), Material = new() { Fill = "#000000" }, Hidden = true });
        model.Sockets.Add(new() { Id = Mount, Control = "root", Frame = CharacterModel.Locomotion, Usage = ModulePlans.LowerBody });
        // The saddle sits on the lower module's body control (instance/control), just above the withers, so the torso rides the gait.
        model.Sockets.Add(new() { Id = Saddle, Control = Lower + PartAsset.Namespace + "body", OffsetX = .42f, OffsetY = .98f, Usage = ModulePlans.UpperBody });
        model.Attachments.Add(new() { Id = Lower, Asset = ModulePlans.QuadrupedLower, Socket = Mount, Inputs = { [QuadrupedTemplate.LegsId] = 1, [QuadrupedTemplate.BodyId] = 1 } });
        model.Attachments.Add(new() { Id = Upper, Asset = ModulePlans.PersonUpper, Socket = Saddle, Scale = .9f, Layer = -.05f, Inputs = { [PersonBuild.ArmsId] = 1, [PersonBuild.HeadId] = 1 } });
        model.MotionOwner = Lower;
        model.Groups.Add(new() { Id = UpperGroup, Targets = [Upper] });
        model.Groups.Add(new() { Id = LowerGroup, Targets = [Lower] });
        ParameterBinding Input(string instance, string input) => new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment(instance, RigProperties.Input + input) };
        model.Parameters.Add(new() { Id = LegsId, Name = "Leg length", Default = 1, Min = .5f, Max = 2, SoftMin = .7f, SoftMax = 1.5f, Bindings = [Input(Lower, QuadrupedTemplate.LegsId)] });
        model.Parameters.Add(new() { Id = ArmsId, Name = "Arm length", Default = 1, Min = .5f, Max = 2, SoftMin = .7f, SoftMax = 1.5f, Bindings = [Input(Upper, PersonBuild.ArmsId)] });
        model.Presets.Add(new() { Id = "standard", Name = "Standard", Values = new() { [LegsId] = 1, [ArmsId] = 1 } });
        model.Presets.Add(new() { Id = "tall", Name = "Tall", Values = new() { [LegsId] = 1.3f, [ArmsId] = 1.15f } });
        model.MotionSets.Add(new() { Id = "standard", Name = "Centaur", Roles = new() { ["idle"] = Id + "-idle", ["walk"] = Id + "-walk", ["run"] = Id + "-run", ["slam"] = Id + "-slam" } });
        model.Validate();
        return model;
    }

    /// <summary>The study's clips: each selects the lower body's gait and the upper body's action by word; the swing is an action alone, for layering over a gait.</summary>
    public static IReadOnlyList<MotionClip> Clips(ResolvedModel resolved)
    {
        var clips = new List<MotionClip>();
        MotionClip Clip(string role, float duration, bool loop, string? lower, string? upper)
        {
            var clip = ClipAuthoring.New(resolved, Id + "-" + role, "Centaur " + role, duration, loop);
            if (lower is not null)
            {
                ClipAuthoring.SetPlay(clip, Lower, 0, lower);
            }

            if (upper is not null)
            {
                ClipAuthoring.SetPlay(clip, Upper, 0, upper);
            }

            clip.Validate(resolved);
            clips.Add(clip);
            return clip;
        }
        Clip("idle", 2, true, "idle", "idle");
        Clip("walk", 1.2f, true, "walk", "walk");
        Clip("run", .65f, true, "run", "run");
        Clip("slam", 1.2f, true, "walk", "hammer-slam");
        Clip("swing", 1, false, null, "player-sword-side-cut");
        return clips;
    }

    public static ModelTemplate Template() => new()
    {
        Id = Id,
        Name = "Centaur",
        Description = "A composed study: the quadruped lower body owns the gait, the Person upper body rides its back and acts on its own.",
        Model = Id,
        Animations = new() { ["idle"] = Id + "-idle", ["walk"] = Id + "-walk", ["run"] = Id + "-run", ["slam"] = Id + "-slam", ["swing"] = Id + "-swing" },
        PreviewAnimation = "walk",
        Slots =
        [
            new() { Id = Mount, Usage = ModulePlans.LowerBody, Required = true, Default = ModulePlans.QuadrupedLower },
            new() { Id = Saddle, Usage = ModulePlans.UpperBody, Required = true, Default = ModulePlans.PersonUpper },
        ],
        Dimensions = [LegsId, ArmsId],
        Presets = ["standard", "tall"],
        Library = new()
        {
            Roles = new() { ["idle"] = "idle", ["walk"] = "walk", ["run"] = "run", ["slam"] = "slam" },
            Masks = [UpperGroup, LowerGroup],
        },
    };

    /// <summary>Explicit regeneration into an authored root whose parts folder holds the modules.</summary>
    public static void Write(string root)
    {
        var catalog = AuthoredCatalog.Load(root);
        var model = Model();
        var resolved = ResolvedModel.From(model, null, catalog.Parts.GetValueOrDefault);
        Directory.CreateDirectory(Path.Combine(root, "models"));
        Directory.CreateDirectory(Path.Combine(root, "animations"));
        Directory.CreateDirectory(Path.Combine(root, "templates"));
        model.Save(Path.Combine(root, "models", model.Id + ".json"));
        foreach (var clip in Clips(resolved))
        {
            clip.Save(Path.Combine(root, "animations", clip.Id + ".json"));
        }

        Template().Save(Path.Combine(root, "templates", Id + ".json"));
    }
}
