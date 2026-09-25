using App2d.Core.Characters;
using App2d.Rendering.Characters;

namespace App2d.Tests.Authored;

public sealed class ModelDrawingTests
{
    [Fact]
    public void AVariantDrawsFromTheSameEvaluatedPoseAsItsBase()
    {
        var person = PersonTemplate.Model();
        var clip = PuppetMotionConverter.Convert(PuppetTemplates.StepStudy(), PuppetTemplates.StepStudy().Motions[0], person, "person-walk", "Walk", "leg");
        var standard = ResolvedModel.From(person);
        var tall = ResolvedModel.From(person, PersonBuild.TallThin.Apply(person, "tall-thin", "Tall"));
        var a = new PuppetDrawing(); a.Build(standard, PoseEvaluator.Sample(standard, clip, .3));
        var b = new PuppetDrawing(); b.Build(tall, PoseEvaluator.Sample(tall, clip, .3));
        Assert.True(a.Mesh.Count > 0);
        Assert.Equal(a.Mesh.Count, b.Mesh.Count);
        Assert.True(b.Mesh.Max.Y > a.Mesh.Max.Y + .2f, "The tall build should draw taller.");
    }
}
