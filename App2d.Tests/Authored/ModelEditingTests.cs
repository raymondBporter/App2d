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

        ModelAuthoring.EditPartOverride(variant, model, part, edited => edited.ScaleX = .6f);

        Assert.Equal(before, model.ToJson());
        Assert.Single(variant.Overrides);
        Assert.Equal(.6f, variant.Override(PropertyRef.OfPart(part.Id, RigProperties.ScaleX))?.Value);
        Assert.False(variant.HasOverride(PropertyRef.OfPart(part.Id, RigProperties.ScaleY)));
        Assert.False(variant.HasOverride(PropertyRef.OfPart(part.Id, RigProperties.Material)));
        model.Parts.First(p => p.Id == part.Id).Material = new()
        {
            Fill = "#123456",
            Outline = new()
            {
                Width = .08f
            }
        };
        var updated = ResolvedModel.From(model, variant).Parts.First(p => p.Id == part.Id);
        Assert.Equal(.6f, updated.ScaleX);
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
            Overrides =
            [
                PropertyOverride.Of(PropertyRef.OfPart("torso", RigProperties.Hidden), true),
                PropertyOverride.Of(PropertyRef.OfPart("torso", RigProperties.Material), new RenderMaterialDefinition2D { Fill = "#654321" }),
            ],
        };
        PuppetPart Part() => ResolvedModel.From(model, variant).Parts.First(p => p.Id == "torso");

        ModelAuthoring.EditPartOverride(variant, model, Part(), edited =>
            edited.Material = edited.RenderMaterial with
            {
                Outline = edited.RenderMaterial.Outline! with
                {
                    Width = .09f
                }
            });
        ModelAuthoring.EditPartOverride(variant, model, Part(), edited => edited.OffsetX = .2f);

        PropertyOverride? Stored(string property) => variant.Override(PropertyRef.OfPart("torso", property));
        Assert.True(Stored(RigProperties.Hidden)?.Flag);
        var material = Stored(RigProperties.Material)!.Material!;
        Assert.Equal("#654321", material.Fill);
        Assert.Equal(.09f, material.Outline!.Width);
        Assert.Null(material.Outline.Color);
        Assert.Null(Stored(RigProperties.ScaleX));
        Assert.Null(Stored(RigProperties.ScaleY));
        Assert.Null(Stored(RigProperties.OffsetY));
        Assert.Equal(3, variant.Overrides.Count); // hidden, one merged material, offsetX
        Assert.Equal(.2f, Part().OffsetX);
        Assert.Equal("#fff8e7", model.Parts.First(p => p.Id == "torso").RenderMaterial.Fill);
    }

    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(2f, 2f)]
    [InlineData(-2f, 2f)]
    public void BoneTipEditsRespectParentScaleAndReflection(float scaleX, float scaleY)
    {
        // A uniform parent, with or without reflection, is a canonical bone (nonuniform parent scale left with the affine adapter in P11c).
        var model = ModelAuthoring.Empty("bones", "Bones");
        var parent = ModelAuthoring.AddBone(model, null, "root");
        parent.Local = new() { X = 3, Y = -2, Rotation = .3f, Scale = MathF.Abs(scaleX), Reflect = scaleX * scaleY < 0 };
        model.Validate();
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
