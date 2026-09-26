using System.Numerics;
using App2d.Core.Characters;
using static App2d.Tests.Authored.TestModels;

namespace App2d.Tests.Authored;

public sealed class SourceImportTests
{
    private static readonly Lazy<PointLibrary> PersonLibrary = new(() => PointLibrary.Load(TestAssets.GetPath("Characters", "person", "library.json")));

    [Theory, InlineData("walk"), InlineData("run")]
    public void AnImportedPuppetReproducesItsMotion(string which)
    {
        var puppet = which == "walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        var result = PuppetImport.Convert(puppet, "study", "Study", "studies/" + which + ".puppet.json", []);
        var clip = Assert.Single(result.Clips);
        var model = ResolvedModel.From(result.Model);
        clip.Validate(model);
        AssertMatchesPrototype(puppet, model, clip);
        Assert.Equal(new AssetSource { Kind = AssetSource.Puppet, File = "studies/" + which + ".puppet.json", Motion = puppet.Motions[0].Name }, clip.Source! with { Points = null });
        Assert.Equal(AssetSource.Puppet, result.Model.Source!.Kind);
    }

    [Fact]
    public void PuppetImportGroundsContactChainsAndKeysTheOthersFromTheirRoot()
    {
        var result = PuppetImport.Convert(PuppetTemplates.StepStudy(), "study", "Study", "walk.puppet.json", []);
        var legs = result.Model.Chains.Where(c => c.Id.EndsWith("-leg", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, legs.Length);
        Assert.All(legs, l => Assert.Equal(CharacterModel.Locomotion, l.Frame));
        Assert.Single(legs.Select(l => l.Scale).Distinct());
        Assert.Equal(legs[0].Scale, result.Clips[0].Travel.Scale);
        var arm = result.Model.Chains.Single(c => c.Id == "left-arm");
        Assert.Equal(arm.Root, arm.Frame);
        Assert.Equal("left-arm", arm.Scale);
    }

    [Fact]
    public void PuppetImportMakesIdsAndAvoidsTakenOnes()
    {
        var puppet = PuppetTemplates.StickFigure();
        puppet.Controls.Single(c => c.Id == "head").Id = "Big Head";
        foreach (var bone in puppet.Bones.Where(b => b.To == "head")) bone.To = "Big Head";
        foreach (var part in puppet.Parts) { if (part.A == "head") part.A = "Big Head"; if (part.B == "head") part.B = "Big Head"; }
        puppet.Motions[0].Name = "Pose Study!";
        puppet.Motions.Add(new() { Name = "Pose Study!", Duration = .5f });
        puppet.Validate();
        var result = PuppetImport.Convert(puppet, "knight", "Knight", "knight.puppet.json", ["knight-pose-study"]);
        Assert.Contains(result.Model.Controls, c => c.Id == "big-head" && c.Parent == "chest");
        Assert.Contains(result.Model.Parts, p => p.A == "big-head");
        Assert.Equal(new[] { "knight-pose-study-2", "knight-pose-study-3" }, result.Clips.Select(c => c.Id));
        Assert.Throws<InvalidDataException>(() => PuppetImport.Convert(puppet, "knight", "Knight", "knight.puppet.json", ["knight"]));
    }

    [Fact]
    public void TheReferenceFrameOfALibraryClipIsTheModelsRest()
    {
        var model = PersonTemplate.Model(); var library = PersonLibrary.Value;
        var clip = LibraryImport.Convert(library, "idle", model, LibraryImport.DefaultMapping(model, library), "imported-idle", "Idle");
        var resolved = ResolvedModel.From(model);
        var rest = PoseEvaluator.Rest(resolved); var first = PoseEvaluator.Sample(resolved, clip, 0);
        foreach (var control in model.Controls) Near(rest.World(control.Id), first.World(control.Id), 1e-4f, control.Id);
    }

    [Fact]
    public void ALibraryWalkMovesTheModelWithItsOwnLimbLengths()
    {
        var model = PersonTemplate.Model(); var library = PersonLibrary.Value;
        var mapping = LibraryImport.DefaultMapping(model, library);
        var clip = LibraryImport.Convert(library, "walk", model, mapping, "imported-walk", "Walk");
        var resolved = ResolvedModel.From(model);
        clip.Validate(resolved);
        Assert.Equal((float)library.Clips["walk"].Duration, clip.Duration, 5);
        Assert.True(clip.Loop);
        Assert.Empty(clip.Contacts);
        Assert.DoesNotContain(clip.Tracks, t => t.Target is "left-knee" or "right-knee" or "left-elbow" or "right-elbow");
        var foot = clip.Tracks.Single(t => t is { Kind: MotionClip.TargetKind, Target: "right-leg" });
        Assert.True(foot.Keys.Max(k => k.X) - foot.Keys.Min(k => k.X) > .3f, "the feet should stride");
        var thigh = resolved.Length("right-hip", "right-knee"); var shin = resolved.Length("right-knee", "right-foot");
        for (var i = 0; i <= 20; i++)
        {
            var pose = PoseEvaluator.Sample(resolved, clip, i * clip.Duration / 20);
            float Flat(string a, string b) => Vector2.Distance(new(pose.World(a).X, pose.World(a).Y), new(pose.World(b).X, pose.World(b).Y));
            Assert.Equal(thigh, Flat("right-hip", "right-knee"), 3);
            Assert.Equal(shin, Flat("right-knee", "right-foot"), 3);
        }
        Assert.Equal(new AssetSource { Kind = AssetSource.Library, File = "person", Motion = "walk", Rest = "idle" }, clip.Source! with { Points = null });
        Assert.Equal(mapping["hips"], clip.Source!.Points!["hips"]);
    }

    [Fact]
    public void TheDefaultPersonMappingPairsLimbsByDepth()
    {
        var model = PersonTemplate.Model(); var library = PersonLibrary.Value;
        var mapping = LibraryImport.DefaultMapping(model, library);
        var reference = new LibraryImport.Sampler(library, "idle", mapping).At(0);
        // The model's right side is the far side (larger Z); the mapped source points must agree.
        Assert.True(model.Controls.Single(c => c.Id == "right-shoulder").Rest.Z > model.Controls.Single(c => c.Id == "left-shoulder").Rest.Z);
        Assert.True(reference["right-shoulder"].Z > reference["left-shoulder"].Z);
        Assert.True(reference["right-hip"].Z > reference["left-hip"].Z);
    }

    [Fact]
    public void ImportedClipsRoundTripWithTheirSource()
    {
        var model = PersonTemplate.Model(); var library = PersonLibrary.Value;
        var clip = LibraryImport.Convert(library, "jump", model, LibraryImport.DefaultMapping(model, library), "imported-jump", "Jump");
        var copy = MotionClip.FromJson(clip.ToJson());
        Assert.Equal(clip.ToJson(), copy.ToJson());
        Assert.Contains("\"source\"", clip.ToJson());
        Assert.DoesNotContain("\"source\"", PersonTemplate.Model().ToJson());
    }

    [Fact]
    public void ABadMappingIsReportedByName()
    {
        var model = PersonTemplate.Model(); var library = PersonLibrary.Value;
        var missingPoint = Assert.Throws<InvalidDataException>(() => LibraryImport.Convert(library, "walk", model, new Dictionary<string, List<string>> { ["hips"] = ["tail"] }, "x", "X"));
        Assert.Contains("'tail'", missingPoint.Message);
        var missingControl = Assert.Throws<InvalidDataException>(() => LibraryImport.Convert(library, "walk", model, new Dictionary<string, List<string>> { ["wing"] = ["head"] }, "x", "X"));
        Assert.Contains("'wing'", missingControl.Message);
    }
}
