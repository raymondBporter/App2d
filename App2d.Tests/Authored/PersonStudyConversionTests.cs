using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class PersonStudyConversionTests
{
    [Theory, InlineData("walk"), InlineData("run")]
    public void ConvertedStudiesReproduceThePrototypeAtReferenceProportions(string which)
    {
        var puppet = which == "walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        var model = PersonTemplate.Model();
        var clip = PuppetMotionConverter.Convert(puppet, puppet.Motions[0], model, "person-" + which, which, "leg");
        var resolved = ResolvedModel.From(model);
        clip.Validate(resolved);
        TestModels.AssertMatchesPrototype(puppet, resolved, clip);
    }

    [Fact]
    public void ConversionKeysTargetsNotSolvedJointsAndDropsEmptyTracks()
    {
        var puppet = PuppetTemplates.StepStudy(); var model = PersonTemplate.Model();
        var clip = PuppetMotionConverter.Convert(puppet, puppet.Motions[0], model, "person-walk", "Walk", "leg");
        Assert.DoesNotContain(clip.Tracks, t => t.Target.EndsWith("-knee") || t.Target.EndsWith("-elbow") || t.Target.EndsWith("-foot") || t.Target.EndsWith("-hand"));
        Assert.Contains(clip.Tracks, t => t is { Kind: MotionClip.TargetKind, Target: "left-leg" });
        Assert.Contains(clip.Tracks, t => t is { Kind: MotionClip.TranslateKind, Target: "hips" });
        Assert.DoesNotContain(clip.Tracks, t => t.Keys.All(k => k.X == 0 && k.Y == 0 && k.Z == 0));
        Assert.Equal(new[] { "left-leg", "right-leg" }, clip.Contacts.Select(c => c.Chain).Order());
        Assert.Equal(.5f, clip.Travel.Keys[^1].X, 5);
    }

    [Fact]
    public void PersonModelDeclaresFramesAndMeasures()
    {
        var model = PersonTemplate.Model();
        Assert.Equal("left-shoulder", model.Chains.Single(c => c.Id == "left-arm").Frame);
        Assert.Equal(CharacterModel.Locomotion, model.Chains.Single(c => c.Id == "right-leg").Frame);
        Assert.Equal(new[] { "arm", "leg", "torso" }, model.Measures.Select(m => m.Id).Order());
        Assert.Equal("leg", model.Controls.Single(c => c.Id == "hips").Scale);
    }
}
