using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class ResolvedModelTests
{
    /// <summary>A longer arm as typed overrides: the elbow and hand pinned through their parent-local setups, the torso narrowed and recolored.</summary>
    private static ModelVariant LongArm()
    {
        var variant = new ModelVariant
        {
            Id = "long-arm",
            Name = "Long arm",
            Base = "creature",
            Overrides =
            [
                PropertyOverride.Of(PropertyRef.OfPart("torso", RigProperties.ScaleX), .9f),
                PropertyOverride.Of(PropertyRef.OfPart("torso", RigProperties.Material), new RenderMaterialDefinition2D { Fill = "#aa3300" }),
            ],
        };
        VariantConversion.PinRest(TestModels.Creature(), variant, new Dictionary<string, PuppetPoint> { ["elbow"] = new(.6f, 1.2f), ["hand"] = new(1.2f, 1.5f) });
        return variant;
    }

    [Fact]
    public void VariantOverridesApplyWithoutEditingTheBase()
    {
        var model = TestModels.Creature();
        var resolved = ResolvedModel.From(model, LongArm());
        Assert.Equal("long-arm", resolved.Id);
        TestModels.Near(new(1.2f, 1.5f, 0), resolved.Rest["hand"]);
        Assert.Equal(new PuppetPoint(.8f, 1.5f), model.Controls.Single(c => c.Id == "hand").Rest);
        var torso = resolved.Parts.Single(p => p.Id == "torso");
        Assert.Equal(.9f, torso.ScaleX);
        Assert.Equal("#aa3300", torso.RenderMaterial.Fill);
        Assert.Equal(1f, model.Parts.Single(p => p.Id == "torso").ScaleX);
        Assert.Equal(1.5f * 2 * MathF.Sqrt(.2f), resolved.Measures["arm"], 4);
        Assert.Equal(2 * MathF.Sqrt(.26f), resolved.Measures["leg"], 4);
    }

    [Fact]
    public void UnoverriddenBaseValuesPropagate()
    {
        var model = TestModels.Creature();
        var variant = LongArm();
        model.Controls.Single(c => c.Id == "knee").Rest = new(.15f, .5f);
        TestModels.Near(new(.15f, .5f, 0), ResolvedModel.From(model, variant).Rest["knee"]);
    }

    [Fact]
    public void OrderPlacesParentsFirstAndChildrenAreIndexed()
    {
        var model = TestModels.Creature();
        model.Controls.Reverse();
        var resolved = ResolvedModel.From(model);
        var index = resolved.Order.Select((c, i) => (c.Id, i)).ToDictionary(p => p.Id, p => p.i);
        foreach (var control in model.Controls.Where(c => c.Parent is not null))
        {
            Assert.True(index[control.Parent!] < index[control.Id]);
        }

        Assert.Equal(expected, resolved.Children["body"].Order());
        Assert.Empty(resolved.Children["foot"]);
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "wrong-base", "references base 'hound'" }, { "unknown-control", "no control 'tail'" },
        { "unknown-part", "no part 'wing'" }, { "zero-bone", "long-arm" }, { "bad-fill", "fill" },
    };

    private static readonly string[] expected = ["hip", "shoulder"];

    [Theory, MemberData(nameof(Rejections))]
    public void BrokenVariantsFailToResolveWithTheVariantAndFieldNamed(string edit, string fragment)
    {
        var variant = LongArm();
        switch (edit)
        {
            case "wrong-base":
                variant.Base = "hound";
                break;
            case "unknown-control":
                variant.Overrides.Add(PropertyOverride.Of(PropertyRef.OfControl("tail", RigProperties.LocalX), 1));
                break;
            case "unknown-part":
                variant.Overrides.Add(PropertyOverride.Of(PropertyRef.OfPart("wing", RigProperties.ScaleX), 1));
                break;
            case "zero-bone":
                // The elbow on top of the shoulder: a zero-length first segment.
                variant.Overrides.Add(PropertyOverride.Of(PropertyRef.OfControl("elbow", RigProperties.LocalX), 0));
                variant.Overrides.Add(PropertyOverride.Of(PropertyRef.OfControl("elbow", RigProperties.LocalY), 0));
                break;
            case "bad-fill":
                variant.Overrides.Add(PropertyOverride.Of(PropertyRef.OfPart("torso", RigProperties.Material), new RenderMaterialDefinition2D { Fill = "red" }));
                break;
        }
        var error = Assert.Throws<InvalidDataException>(() => ResolvedModel.From(TestModels.Creature(), variant));
        Assert.Contains(fragment, error.Message);
    }

    [Fact]
    public void VariantJsonRoundTripsAndRejectsMisspelledFields()
    {
        var json = LongArm().ToJson();
        Assert.Equal(json, ModelVariant.FromJson(json).ToJson());
        Assert.Contains("\"base\": \"creature\"", json);
        Assert.Throws<JsonException>(() => ModelVariant.FromJson(json.Replace("\"overrides\":", "\"overrdies\":")));
    }
}
