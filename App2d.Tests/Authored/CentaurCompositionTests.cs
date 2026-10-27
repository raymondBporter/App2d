using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P10c: the centaur study is composed from library modules and resources alone and plays through the generic evaluator.</summary>
public sealed class CentaurCompositionTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private static ResolvedModel Centaur => Catalog.Resolve(CentaurStudy.Id);

    [Fact]
    public void TheCentaurIsResourcesOverTheShippedModulesWithOneRootMotionOwner()
    {
        Assert.Empty(Catalog.Errors);
        var centaur = Centaur;
        Assert.Single(centaur.Base.Controls);
        Assert.Equal([CentaurStudy.Lower, CentaurStudy.Upper], centaur.Attachments.Select(a => a.Id));
        Assert.Equal((ModulePlans.QuadrupedLower, ModulePlans.PersonUpper), (centaur.Attachment(CentaurStudy.Lower)!.Asset.Id, centaur.Attachment(CentaurStudy.Upper)!.Asset.Id));
        Assert.Equal(CentaurStudy.Lower, centaur.MotionOwner!.Id);
        Assert.Equal([CentaurStudy.Lower, CentaurStudy.Upper], centaur.EvaluationOrder.Select(a => a.Id));
        // The saddle is a socket on the lower module's own control; the template publishes the composition's contract.
        var saddle = centaur.Base.Sockets.Single(s => s.Id == CentaurStudy.Saddle);
        Assert.Equal((CentaurStudy.Lower, "body"), ResolvedModel.NestedSocket(saddle)!.Value);
        Assert.Equal(CentaurStudy.Model().ToJson(), File.ReadAllText(Catalog.PathOf(CentaurStudy.Id)));
        var template = ModelTemplate.FromJson(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "templates", "centaur.json")));
        Assert.Empty(template.ContractProblems(centaur.Base, Catalog.Parts.GetValueOrDefault));
        Assert.Equal([CentaurStudy.Mount, CentaurStudy.Saddle], template.Slots.Select(s => s.Id));
    }

    [Fact]
    public void TheLowerBodyRunsTheGaitAndTheTorsoFollowsItWhileTheUpperBodyActsOnItsOwn()
    {
        var centaur = Centaur;
        var walk = Catalog.Animations["centaur-walk"];
        var slam = Catalog.Animations["centaur-slam"];
        var quadrupedWalk = Catalog.Parts[ModulePlans.QuadrupedLower].Clip("quadruped-walk")!;
        var lowerRig = centaur.Attachment(CentaurStudy.Lower)!.Rig!;
        foreach (var seconds in new[] { .2, .55, 1.0, 1.7 })
        {
            var pose = PoseEvaluator.Sample(centaur, walk, seconds, repeat: true);
            var lower = pose.Attachments[CentaurStudy.Lower];
            var upper = pose.Attachments[CentaurStudy.Upper];
            Assert.Equal(("walk", "walk"), (lower.Expression, upper.Expression));
            // The gait: every leg chain reached and the planted feet holding their anchors, as the module's own clip has them.
            Assert.All(lower.Pose.Chains, c => Assert.True(c.Reached, c.Chain));
            Assert.All(lower.Pose.Contacts, c => Assert.True(c.Residual < 1e-4f));
            Assert.NotEmpty(lower.Pose.Contacts);
            // The torso follows the lower body's own control: the saddle socket sits on lower/body, so the chest lands at that control plus the socket offset in its frame.
            var body = lower.Frame("body");
            var expected = body.At(new Vector3(.42f, .98f, 0));
            TestModels.Near(new Vector3(expected.X, expected.Y, 0), new Vector3(upper.World(Humanoid.Chest).X, upper.World(Humanoid.Chest).Y, 0), 1e-4f);
            // Root motion belongs to the lower body alone: the pose's locomotion is the quadruped walk's travel, cycles included.
            var travel = PoseEvaluator.TravelOf(lowerRig, quadrupedWalk, seconds, repeat: true);
            Assert.Equal(travel.X, pose.Locomotion.X, 4);
            Assert.Equal(travel.Y, pose.Locomotion.Y, 4);
            Assert.True(MathF.Abs(pose.Locomotion.X) > 0 || seconds == 0);
        }
        Assert.Equal(PoseEvaluator.CycleTravel(lowerRig, quadrupedWalk).X, PoseEvaluator.CycleTravel(centaur, walk).X, 5);
        Assert.Equal(0, PoseEvaluator.CycleTravel(centaur, Catalog.Animations["centaur-swing"]).X, 5);
        // An independently bound action: the slam keeps the walk underneath and plays the hammer slam on top; the swing as an
        // overlay owning the upper instance leaves the gait, the planted feet and the root motion exactly as the walk had them.
        var slammed = PoseEvaluator.Sample(centaur, slam, .55, repeat: true);
        Assert.Equal(("walk", "hammer-slam"), (slammed.Attachments[CentaurStudy.Lower].Expression, slammed.Attachments[CentaurStudy.Upper].Expression));
        var swing = Catalog.Animations["centaur-swing"];
        var mask = centaur.Base.Groups.Single(g => g.Id == CentaurStudy.UpperGroup).Targets.ToHashSet(StringComparer.Ordinal);
        var walking = PoseEvaluator.Sample(centaur, walk, .55, repeat: true);
        var layered = PoseEvaluator.Sample(centaur, walk, .55, repeat: true, new() { Overlay = new(swing, .3, mask, 1) });
        Assert.Equal(("walk", "player-sword-side-cut"), (layered.Attachments[CentaurStudy.Lower].Expression, layered.Attachments[CentaurStudy.Upper].Expression));
        foreach (var control in lowerRig.Order)
        {
            TestModels.Near(walking.Attachments[CentaurStudy.Lower].World(control.Id), layered.Attachments[CentaurStudy.Lower].World(control.Id), 1e-5f, control.Id);
        }
        Assert.Equal(walking.Locomotion, layered.Locomotion);
        Assert.NotEqual(walking.Attachments[CentaurStudy.Upper].World(Humanoid.RightHand), layered.Attachments[CentaurStudy.Upper].World(Humanoid.RightHand));
        // The composition's own dimensions reach each module's inputs: the tall preset lengthens the horse's legs and the rider's arms.
        var tall = new ModelVariant { Id = "tall-centaur", Name = "Tall", Base = CentaurStudy.Id };
        ModelAuthoring.ApplyPreset(centaur.Base, tall, "tall");
        var taller = ResolvedModel.From(centaur.Base, tall, Catalog.Parts.GetValueOrDefault);
        Assert.Equal(1.3f, taller.Attachment(CentaurStudy.Lower)!.Rig!.ParameterValues[QuadrupedTemplate.LegsId], 4);
        Assert.Equal(1.15f, taller.Attachment(CentaurStudy.Upper)!.Rig!.ParameterValues[PersonBuild.ArmsId], 4);
        Assert.True(taller.Attachment(CentaurStudy.Lower)!.Rig!.Rest["near-front-foot"].Y < lowerRig.Rest["near-front-foot"].Y - .1f, "longer legs reach lower below the connection; the wearer decides the mounting height");
    }

    [Fact]
    public void OneOwnerOnlyAndCyclesAndWearerTravelAreRefusedWithPaths()
    {
        var model = CentaurStudy.Model();
        Func<string, Part?> parts = Catalog.Parts.GetValueOrDefault;
        // The owner must be an instance mounted on the wearer's own control.
        var onModule = CentaurStudy.Model();
        onModule.MotionOwner = CentaurStudy.Upper;
        Assert.Contains("must mount on one of the wearer's own controls, not on another instance (path: centaur > motionOwner)", Assert.Throws<InvalidDataException>(onModule.Validate).Message);
        var missing = CentaurStudy.Model();
        missing.MotionOwner = "wheels";
        Assert.Contains("'wheels' is not an attachment instance (path: centaur > motionOwner)", Assert.Throws<InvalidDataException>(missing.Validate).Message);
        // A wearer clip may neither travel nor plant while a module owns the motion.
        var resolved = ResolvedModel.From(model, null, parts);
        var travelling = ClipAuthoring.New(resolved, "drift", "Drift", 1, true);
        travelling.Travel.Keys = [new(), new() { Time = 1, Value = new(1, 0, 0) }];
        Assert.Contains("root motion is owned by attachment 'lower' (path: centaur > motionOwner); this clip may not travel itself", Assert.Throws<InvalidDataException>(() => travelling.Validate(resolved)).Message);
        // Instances mounting on each other are refused with the cycle named.
        var cyclic = CentaurStudy.Model();
        cyclic.Sockets.First(s => s.Id == CentaurStudy.Mount).Control = CentaurStudy.Upper + Part.Namespace + Humanoid.Chest;
        cyclic.MotionOwner = null;
        cyclic.Validate();
        Assert.Contains("mount on each other in a cycle: lower > upper > lower (path: centaur > attachments > lower > socket mount)", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(cyclic, null, parts)).Message);
        // A socket on a control the module does not have, and on an instance that does not exist, name their paths.
        var noControl = CentaurStudy.Model();
        noControl.Sockets.First(s => s.Id == CentaurStudy.Saddle).Control = CentaurStudy.Lower + Part.Namespace + "neck";
        noControl.Validate();
        Assert.Contains("part 'quadruped-lower' has no control 'neck' (path: centaur > sockets > saddle > control > attachments > lower > part quadruped-lower > controls)", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(noControl, null, parts)).Message);
        var noInstance = CentaurStudy.Model();
        noInstance.Sockets.First(s => s.Id == CentaurStudy.Saddle).Control = "horse/body";
        Assert.Contains("no attachment 'horse' for 'horse/body'; path: centaur > sockets > saddle > control", Assert.Throws<InvalidDataException>(noInstance.Validate).Message);
        // A socket on a module control frames on that instance only.
        var crossFramed = CentaurStudy.Model();
        crossFramed.Sockets.First(s => s.Id == CentaurStudy.Saddle).Frame = "root";
        Assert.Contains("may frame on the same instance's control or the locomotion frame only (path: centaur > sockets > saddle > frame)", Assert.Throws<InvalidDataException>(crossFramed.Validate).Message);
    }

    [Fact]
    public void ThreeAnatomyFamiliesTheLinkageAndTheCentaurSampleThroughTheOneEvaluator()
    {
        // The gate: no model-specific evaluator, template switch or generated class; every family is data on PoseEvaluator.Sample.
        var subjects = new (ResolvedModel Model, MotionClip Clip)[]
        {
            (Catalog.Resolve("person"), Catalog.Animations["person-walk"]),
            (Catalog.Resolve("quadruped"), Catalog.Animations["quadruped-walk"]),
            (Catalog.Resolve("pterosaur"), Catalog.Animations["pterosaur-hover"]),
            (ResolvedModel.From(TestModels.Linkage()), null!),
            (Centaur, Catalog.Animations["centaur-walk"]),
        };
        foreach (var (model, clip) in subjects)
        {
            var pose = PoseEvaluator.Sample(model, clip, .4, repeat: true);
            Assert.NotEmpty(pose.Points);
            Assert.All(pose.Chains, c => Assert.True(c.Reached, $"{model.Id}: {c.Chain}"));
        }
        Assert.DoesNotContain(typeof(PoseEvaluator).Assembly.GetTypes(), t => t.Name.Contains("Centaur", StringComparison.OrdinalIgnoreCase));
    }
}
