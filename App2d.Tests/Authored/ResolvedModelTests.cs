using System.Text.Json;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class ResolvedModelTests
{
    private static ModelVariant LongArm() => new()
    {
        Id = "long-arm", Name = "Long arm", Base = "creature",
        Rest = { ["elbow"] = new(.6f, 1.2f), ["hand"] = new(1.2f, 1.5f) },
        Parts = { ["torso"] = new() { Width = .9f, Fill = "#aa3300" } },
    };

    [Fact]
    public void VariantOverridesApplyWithoutEditingTheBase()
    {
        var model = TestModels.Creature();
        var resolved = ResolvedModel.From(model, LongArm());
        Assert.Equal("long-arm", resolved.Id);
        TestModels.Near(new(1.2f, 1.5f, 0), resolved.Rest["hand"]);
        Assert.Equal(new PuppetPoint(.8f, 1.5f), model.Controls.Single(c => c.Id == "hand").Rest);
        var torso = resolved.Parts.Single(p => p.Id == "torso");
        Assert.Equal(.9f, torso.Width); Assert.Equal("#aa3300", torso.Fill);
        Assert.Equal(.4f, model.Parts.Single(p => p.Id == "torso").Width);
        Assert.Equal(1.5f * 2 * MathF.Sqrt(.2f), resolved.Measures["arm"], 4);
        Assert.Equal(2 * MathF.Sqrt(.26f), resolved.Measures["leg"], 4);
    }

    [Fact]
    public void UnoverriddenBaseValuesPropagate()
    {
        var model = TestModels.Creature(); var variant = LongArm();
        model.Controls.Single(c => c.Id == "knee").Rest = new(.15f, .5f);
        TestModels.Near(new(.15f, .5f, 0), ResolvedModel.From(model, variant).Rest["knee"]);
    }

    [Fact]
    public void OrderPlacesParentsFirstAndChildrenAreIndexed()
    {
        var model = TestModels.Creature(); model.Controls.Reverse();
        var resolved = ResolvedModel.From(model);
        var index = resolved.Order.Select((c, i) => (c.Id, i)).ToDictionary(p => p.Id, p => p.i);
        foreach (var control in model.Controls.Where(c => c.Parent is not null)) Assert.True(index[control.Parent!] < index[control.Id]);
        Assert.Equal(new[] { "hip", "shoulder" }, resolved.Children["body"].Order());
        Assert.Empty(resolved.Children["foot"]);
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "wrong-base", "references base 'hound'" }, { "unknown-control", "rest.tail" },
        { "unknown-part", "parts.wing" }, { "zero-bone", "long-arm" }, { "bad-fill", "fill" },
    };

    [Theory, MemberData(nameof(Rejections))]
    public void BrokenVariantsFailToResolveWithTheVariantAndFieldNamed(string edit, string fragment)
    {
        var variant = LongArm();
        switch (edit)
        {
            case "wrong-base": variant.Base = "hound"; break;
            case "unknown-control": variant.Rest["tail"] = new(1, 1); break;
            case "unknown-part": variant.Parts["wing"] = new() { Width = 1 }; break;
            case "zero-bone": variant.Rest["elbow"] = new(0, 1.5f); break;
            case "bad-fill": variant.Parts["torso"].Fill = "red"; break;
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
        Assert.Throws<JsonException>(() => ModelVariant.FromJson(json.Replace("\"parts\":", "\"prats\":")));
    }
}
