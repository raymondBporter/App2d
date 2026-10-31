using App2d.CharacterStudio.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P10b: rig modules extracted from the Person and the quadruped through the P7 instancing mechanism, with declared contracts and no target collisions between instances.</summary>
public sealed class ModuleExtractionTests : IDisposable
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "module-extraction-" + Guid.NewGuid().ToString("N"));
    private string Authored => Path.Combine(_root, "authored");
    private string Output => Path.Combine(_root, "scratch");

    public ModuleExtractionTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(Authored, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    /// <summary>A wearer with a root, a control that shares its name with a module target, and two sockets taking upper bodies.</summary>
    private static CharacterModel Stand(string usage = ModulePlans.UpperBody)
    {
        var stand = new CharacterModel { Id = "rider-stand", Name = "Rider stand" };
        stand.Controls.Add(new() { Id = "root", Rest = new(0, 1, 0) });
        stand.Controls.Add(new() { Id = "head", Parent = "root", Rest = new(0, 1.3f, 0) });
        stand.Parts.Add(new() { Id = "beam", A = "root", B = "head", Geometry = Core.Shapes.Ellipse2D.Builder.Create(new(.05f, .15f)), Material = new() { Fill = "#888888" } });
        stand.Sockets.Add(new() { Id = "left-seat", Control = "root", OffsetX = -.6f, Frame = CharacterModel.Locomotion, Usage = usage });
        stand.Sockets.Add(new() { Id = "right-seat", Control = "root", OffsetX = .6f, Frame = CharacterModel.Locomotion, Usage = usage });
        stand.Validate();
        return stand;
    }

    [Fact]
    public void TheShippedModulesAreTheExtractionOfTheShippedModelsAndRerunUnchanged()
    {
        var results = ModulePlans.Extract(Authored, Output);
        Assert.All(results, r => Assert.True(r.Converted, $"{r.PartId}: {r.Status} {string.Join("; ", r.Differences)}"));
        foreach (var result in results)
        {
            Assert.Equal(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "parts", result.PartId + ".json")), File.ReadAllText(Path.Combine(Output, "parts", result.PartId + ".json")));
            Assert.True(File.Exists(Path.Combine(Output, $"module-conversion-{result.PartId}.md")));
        }
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            Assert.Equal(File.ReadAllText(file), File.ReadAllText(Path.Combine(Authored, Path.GetRelativePath(TestModels.AuthoredRoot, file))));
        }
        Assert.All(ModulePlans.Extract(Authored, Output), r => Assert.StartsWith("unchanged", r.Status));
        var edited = Part.FromJson(File.ReadAllText(Path.Combine(Output, "parts", ModulePlans.QuadrupedHead + ".json")));
        edited.Name = "Edited head";
        edited.Save(Path.Combine(Output, "parts", ModulePlans.QuadrupedHead + ".json"));
        Assert.StartsWith("kept", ModulePlans.Extract(Authored, Output).Single(r => r.PartId == ModulePlans.QuadrupedHead).Status);
        // The report names what stayed with the wearer: the Person's legs, its face selections and its root travel.
        var upper = results.Single(r => r.PartId == ModulePlans.PersonUpper);
        Assert.Contains(upper.Report, line => line.StartsWith("measure 'leg' left with the wearer", StringComparison.Ordinal));
        Assert.Contains(upper.Report, line => line.StartsWith("stand-in 'waist'", StringComparison.Ordinal));
        Assert.Contains(upper.Report, line => line.Contains("clip 'person-walk' -> module clip") && line.Contains("track(s) left with the wearer") && line.Contains("root travel left with the wearer"));
        Assert.Contains(upper.Report, line => line.Contains("clip 'person-hit' -> module clip") && line.Contains("face selections left with the wearer"));
        var lower = results.Single(r => r.PartId == ModulePlans.QuadrupedLower);
        Assert.Contains(lower.Report, line => line.Contains("clip 'quadruped-walk' -> module clip") && line.Contains("6 contact(s)") && line.Contains("root travel carried"));
    }

    [Fact]
    public void TheModulesDeclareTheirContractsConnectionInputsPointsAndTargets()
    {
        var upper = Catalog.Parts[ModulePlans.PersonUpper];
        Assert.Equal((PartPayloads.Animated, ModulePlans.UpperBody, Humanoid.Chest), (upper.Payload, upper.Usage, upper.Connection));
        Assert.Equal(["torso", "arms", "width", "head"], upper.Parameters.Select(p => p.Id));
        Assert.Equal(["arm", "torso"], upper.Measures.Select(m => m.Id));
        Assert.Equal(["left-arm", "right-arm"], upper.Constraints.Select(c => c.Id));
        Assert.Contains("waist", upper.Controls.Select(c => c.Id));
        Assert.DoesNotContain("waist", upper.Exposed);
        Assert.Contains(Humanoid.Head, upper.Exposed);
        Assert.Contains("body", upper.Exposed);
        Assert.Equal("idle", upper.Expression);
        Assert.True(upper.Binds("walk") && upper.Binds("player-sword-side-cut") && upper.Binds("club-caveman-slam"));
        Assert.Equal(Vector3.Zero, upper.Controls.First(c => c.Id == Humanoid.Chest).Rest.XYZ);
        Assert.Contains(upper.Contract(), line => line.StartsWith("connection: control 'chest' at the origin on a socket taking 'upper-body'", StringComparison.Ordinal));
        Assert.Contains(upper.Contract(), line => line.StartsWith("inputs: torso (factor 1 in 0.5..2)", StringComparison.Ordinal));
        var lower = Catalog.Parts[ModulePlans.QuadrupedLower];
        Assert.Equal((ModulePlans.LowerBody, "body"), (lower.Usage, lower.Connection));
        Assert.Equal([ModulePlans.Withers], lower.Locations.Select(l => l.Id));
        Assert.Equal("body", lower.Locations[0].Control);
        Assert.Equal(["legs", "body"], lower.Parameters.Select(p => p.Id));
        Assert.Equal(4, lower.Constraints.Count);
        Assert.True(lower.Clip("quadruped-walk")!.Travel.Keys.Count > 0 && lower.Clip("quadruped-walk")!.Contacts.Count == 6);
        Assert.Equal(["head", "head-up"], Catalog.Parts[ModulePlans.QuadrupedHead].Controls.Select(c => c.Id));
        Assert.Equal([Humanoid.Hips, Humanoid.LeftHip, Humanoid.LeftKnee, Humanoid.LeftFoot, Humanoid.RightHip, Humanoid.RightKnee, Humanoid.RightFoot], Catalog.Parts[ModulePlans.PersonLower].Controls.Select(c => c.Id));
        // A connection control must be a root at the origin; a static part declares no contract members.
        var bent = Part.FromJson(lower.ToJson());
        bent.Connection = "tail";
        Assert.Contains("must be a rig root at the origin", Assert.Throws<InvalidDataException>(bent.Validate).Message);
        var boot = Part.FromJson(Catalog.Parts["boot"].ToJson());
        boot.Parameters.Add(new() { Id = "size" });
        Assert.Contains("declares no inputs", Assert.Throws<InvalidDataException>(boot.Validate).Message);
    }

    [Fact]
    public void TwoInstancesOfOneModuleKeepTheirOwnTargetsBesideTheWearersAndInputsScaleEachRig()
    {
        var stand = Stand();
        stand.Attachments.Add(new() { Id = "a", Asset = ModulePlans.PersonUpper, Socket = "left-seat" });
        stand.Attachments.Add(new() { Id = "b", Asset = ModulePlans.PersonUpper, Socket = "right-seat", Inputs = { ["arms"] = 1.5f } });
        stand.Validate();
        var resolved = ResolvedModel.From(stand, null, Catalog.Parts.GetValueOrDefault);
        Assert.Equal(["a", "b"], resolved.Attachments.Select(a => a.Id));
        Assert.Same(resolved.Attachments[0].Asset, resolved.Attachments[1].Asset);
        // The wearer's own 'head' and each module's 'head' are three targets; a clip keys them apart.
        var clip = ClipAuthoring.New(resolved, "wave", "Wave", 1, true);
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "head"), 0, Vector3.Zero, .2f);
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "a/head"), 0, Vector3.Zero, -.5f);
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "b/head"), 0, new(0, .1f, 0), 0);
        ClipAuthoring.SetPlay(clip, "a", 0, "walk");
        ClipAuthoring.SetPlay(clip, "b", 0, "hit");
        clip.Validate(resolved);
        var pose = PoseEvaluator.Sample(resolved, clip, .3, repeat: true);
        Assert.Equal(.2f, pose.Angles["head"], 5);
        var a = pose.Attachments["a"];
        var b = pose.Attachments["b"];
        Assert.Equal(("walk", "hit"), (a.Expression, b.Expression));
        Assert.Equal(-.5f, a.Pose.Angles["head"], 5);
        Assert.Equal(0, b.Pose.Angles["head"], 5);
        Assert.True(b.Pose.Points["head"].Y - b.Rig.Rest["head"].Y > .09f, "b's head is lifted by its own namespaced key");
        Assert.True(MathF.Abs(a.Pose.Points["head"].Y - a.Rig.Rest["head"].Y) < .02f, "a's head keeps its height: b's key never reaches a");
        // The inputs are each instance's: b's arms are 1.5 times a's, through the module's own parameter bindings.
        var reachA = Vector3.Distance(a.Rig.Rest["left-hand"], a.Rig.Rest["left-shoulder"]);
        var reachB = Vector3.Distance(b.Rig.Rest["left-hand"], b.Rig.Rest["left-shoulder"]);
        Assert.Equal(1.5f, reachB / reachA, 3);
        Assert.Equal(1.5f, b.Rig.ParameterValues["arms"], 5);
        Assert.Equal(1, a.Rig.ParameterValues["arms"], 5);
        // Each instance sits on its own socket and the poses are placed there.
        Assert.True(a.World("chest").X < 0 && b.World("chest").X > 0);
        // A wearer parameter binds an input through the attachment property input.<id>, with provenance.
        stand.Parameters.Add(new() { Id = "size", Name = "Size", Default = 1, Min = .5f, Max = 2, Bindings = [new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment("b", RigProperties.Input + "arms") }] });
        stand.Validate();
        var big = new ModelVariant { Id = "big", Name = "Big", Base = stand.Id, Parameters = { ["size"] = 1.2f } };
        var scaled = ResolvedModel.From(stand, big, Catalog.Parts.GetValueOrDefault);
        Assert.Equal(1.8f, scaled.Attachment("b")!.Instance.Inputs["arms"], 4);
        Assert.Equal(1.8f, scaled.Attachment("b")!.Rig!.ParameterValues["arms"], 4);
        Assert.Equal(PropertySource.ParameterBinding, scaled.Provenance(PropertyRef.OfAttachment("b", RigProperties.Input + "arms")));
        var pinned = new ModelVariant { Id = "pinned", Name = "Pinned", Base = stand.Id, Overrides = [PropertyOverride.Of(PropertyRef.OfAttachment("b", RigProperties.Input + "arms"), .8f)] };
        Assert.Equal(.8f, ResolvedModel.From(stand, pinned, Catalog.Parts.GetValueOrDefault).Attachment("b")!.Rig!.ParameterValues["arms"], 4);
    }

    [Fact]
    public void IncompatibleBindingsAreRefusedWithAReferencePath()
    {
        // A socket that takes upper bodies refuses a lower body; the path names the instance, the socket and the usage.
        var wrong = Stand();
        wrong.Attachments.Add(new() { Id = "legs", Asset = ModulePlans.QuadrupedLower, Socket = "left-seat" });
        wrong.Validate();
        var usage = Assert.Throws<InvalidDataException>(() => ResolvedModel.From(wrong, null, Catalog.Parts.GetValueOrDefault)).Message;
        Assert.Contains("socket 'left-seat' takes 'upper-body', part 'quadruped-lower' is a 'lower-body' (path: Model 'rider-stand' > attachments > legs > socket left-seat > usage)", usage);
        // An input the module does not declare, with the module's inputs listed.
        var unknown = Stand();
        unknown.Attachments.Add(new() { Id = "a", Asset = ModulePlans.PersonUpper, Socket = "left-seat", Inputs = { ["legs"] = 1.2f } });
        unknown.Validate();
        var input = Assert.Throws<InvalidDataException>(() => ResolvedModel.From(unknown, null, Catalog.Parts.GetValueOrDefault)).Message;
        Assert.Contains("part 'person-upper' declares no input 'legs' (one of torso, arms, width, head) (path: Model 'rider-stand' > attachments > a > inputs > legs)", input);
        // An input outside the module parameter's range is the module's refusal, by the parameter's own range.
        var outside = Stand();
        outside.Attachments.Add(new() { Id = "a", Asset = ModulePlans.PersonUpper, Socket = "left-seat", Inputs = { ["arms"] = 3 } });
        outside.Validate();
        Assert.Contains("arms", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(outside, null, Catalog.Parts.GetValueOrDefault)).Message);
        // A wearer binding onto an input the instance does not set names the path to declare it.
        var unset = Stand();
        unset.Attachments.Add(new() { Id = "a", Asset = ModulePlans.PersonUpper, Socket = "left-seat" });
        unset.Parameters.Add(new() { Id = "size", Default = 1, Min = .5f, Max = 2, Bindings = [new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfAttachment("a", RigProperties.Input + "arms") }] });
        Assert.Contains("attachment 'a' sets no input 'arms'; declare it on the instance first (path: rider-stand > attachments > a > inputs)", Assert.Throws<InvalidDataException>(unset.Validate).Message);
        // A wearer clip keying a target the module does not expose (the stand-in) reports the reference path.
        var worn = Stand();
        worn.Attachments.Add(new() { Id = "a", Asset = ModulePlans.PersonUpper, Socket = "left-seat" });
        var resolved = ResolvedModel.From(worn, null, Catalog.Parts.GetValueOrDefault);
        var clip = ClipAuthoring.New(resolved, "bad", "Bad", 1, false);
        clip.Tracks.Add(new() { Kind = MotionClip.TranslateKind, Target = "a/waist", Keys = [new()] });
        Assert.Contains("path: rider-stand > attachments > a > part person-upper > exposed", Assert.Throws<InvalidDataException>(() => clip.Validate(resolved)).Message);
        // The extraction itself refuses a straddling piece, a chain reaching outside and a dropped measure, by name.
        var person = Catalog.Models["person"];
        var clips = Catalog.Animations.Values.Where(c => c.Model == "person").ToList();
        Assert.Contains("Piece 'body' references both module controls and others (chest)", Assert.Throws<InvalidDataException>(() => ModuleConversion.Build(person, clips, new("hips-only", "Hips", Humanoid.Hips) { Exclude = { Humanoid.Chest } })).Message);
        Assert.Contains("Chain 'left-leg' is rooted inside the module but reaches outside it", Assert.Throws<InvalidDataException>(() => ModuleConversion.Build(person, clips, new("hips-only", "Hips", Humanoid.Hips) { Exclude = { Humanoid.Chest, Humanoid.LeftKnee }, Leave = { Humanoid.Body } })).Message);
        Assert.Contains("scales by measure 'torso', whose path reaches outside the module", Assert.Throws<InvalidDataException>(() => ModuleConversion.Build(person, clips, new("no-waist", "No waist", Humanoid.Chest) { Leave = { Humanoid.Body } })).Message);
    }

    [Fact]
    public void TheLowerModuleRunsItsGaitWithPlantedFeetOnAStand()
    {
        var lower = Catalog.Parts[ModulePlans.QuadrupedLower];
        var stand = ModuleConversion.Stand(Catalog.Resolve("quadruped"), ModulePlans.All.Single(p => p.Plan.PartId == ModulePlans.QuadrupedLower).Plan, lower);
        var resolved = ResolvedModel.From(stand, null, Catalog.Parts.GetValueOrDefault);
        var clip = ClipAuthoring.New(resolved, "walk-stand", "Walk stand", 1.2f, true);
        ClipAuthoring.SetPlay(clip, "module", 0, "walk");
        clip.Validate(resolved);
        foreach (var time in new[] { .1, .4, .7, 1.0 })
        {
            var pose = PoseEvaluator.Sample(resolved, clip, time, repeat: true, new() { InPlace = true });
            var module = pose.Attachments["module"];
            Assert.Equal("walk", module.Expression);
            Assert.All(module.Pose.Chains, chain => Assert.True(chain.Reached, chain.Chain));
            Assert.True(module.Pose.Contacts.Count >= 2, "at least two feet are planted at any instant of the walk");
            Assert.All(module.Pose.Contacts, contact => Assert.True(contact.Residual < 1e-4f));
        }
    }
}
