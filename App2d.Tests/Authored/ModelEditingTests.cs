using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class ModelEditingTests
{
    [Fact]
    public void PartEditsKeepUneditedAppearanceFollowingTheBase()
    {
        var model = TestModels.Creature();
        var variant = new ModelVariant { Id = "custom", Name = "Custom", Base = model.Id };
        var part = ResolvedModel.From(model, variant).Parts.First(p => p.Id == "torso");
        var before = model.ToJson();

        ModelAuthoring.EditPartOverride(variant, part, edited => edited.Width = .6f);

        Assert.Equal(before, model.ToJson());
        Assert.Null(variant.Parts[part.Id].Height);
        Assert.Null(variant.Parts[part.Id].Material);
        model.Parts.First(p => p.Id == part.Id).Material = new() { Fill = "#123456", Outline = new() { Width = .08f } };
        var updated = ResolvedModel.From(model, variant).Parts.First(p => p.Id == part.Id);
        Assert.Equal(.6f, updated.Width);
        Assert.Equal("#123456", updated.RenderMaterial.Fill);
        Assert.Equal(.08f, updated.RenderMaterial.Outline!.Width);
    }

    [Fact]
    public void SuccessivePartEditsPreserveOtherOverridesAndInheritedFields()
    {
        var model = TestModels.Creature();
        var variant = new ModelVariant
        {
            Id = "custom",
            Name = "Custom",
            Base = model.Id,
            Parts = { ["torso"] = new() { Hidden = true, Material = new() { Fill = "#654321" } } }
        };
        PuppetPart Part() => ResolvedModel.From(model, variant).Parts.First(p => p.Id == "torso");

        ModelAuthoring.EditPartOverride(variant, Part(), edited =>
            edited.Material = edited.RenderMaterial with { Outline = edited.RenderMaterial.Outline! with { Width = .09f } });
        ModelAuthoring.EditPartOverride(variant, Part(), edited => edited.OffsetX = .2f);

        var stored = variant.Parts["torso"];
        Assert.True(stored.Hidden);
        Assert.Equal("#654321", stored.Material!.Fill);
        Assert.Equal(.09f, stored.Material.Outline!.Width);
        Assert.Null(stored.Material.Outline.Color);
        Assert.Null(stored.Width);
        Assert.Null(stored.Height);
        Assert.Null(stored.OffsetY);
        Assert.Equal(.2f, Part().OffsetX);
        Assert.Equal("#fff8e7", model.Parts.First(p => p.Id == "torso").RenderMaterial.Fill);
    }

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(2f, 2f)]
    [InlineData(-2f, 2f)]
    [InlineData(2f, .5f)]
    public void BoneTipEditsRespectParentScaleAndReflection(float scaleX, float scaleY)
    {
        var model = ModelAuthoring.Empty("bones", "Bones");
        var parent = ModelAuthoring.AddBone(model, null, "root");
        parent.Transform = new() { X = 3, Y = -2, Rotation = .3f, ScaleX = scaleX, ScaleY = scaleY };
        var child = ModelAuthoring.AddBone(model, parent.Id, "child");
        var origin = ResolvedModel.From(model).Rest[child.Id];
        var target = new Vector2(origin.X + 1.2f, origin.Y + .8f);

        ModelAuthoring.MoveBoneTip(model, child.Id, target);

        var resolved = ResolvedModel.From(model);
        TestModels.Near(origin, resolved.Rest[child.Id]);
        var tip = Vector2.Transform(new(child.Length, 0), resolved.RestTransforms[child.Id]);
        Assert.InRange(Vector2.Distance(target, tip), 0, 1e-4f);
    }
}
