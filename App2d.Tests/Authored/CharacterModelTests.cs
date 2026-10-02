using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class CharacterModelTests
{
    [Fact]
    public void LegacyRestDepthIsPreservedByLayerEditingAndJson()
    {
        var model = TestModels.Creature();
        var control = model.Controls.First();
        control.Rest = new(.25f, 1.5f, -.14338458f);

        var layered = control.Rest.Layered;
        Assert.Equal(new Vector2(.25f, 1.5f), layered.Position);
        Assert.Equal(-.14338458f, layered.Layer.Order);
        control.Rest = PuppetPoint.From(layered with { Position = new(.5f, 1.5f) });

        var json = model.ToJson();
        Assert.Contains("\"z\": -0.14338458", json);
        Assert.DoesNotContain("\"layered\"", json);
        Assert.Equal(control.Rest, CharacterModel.FromJson(json).Controls.First().Rest);
    }

    [Fact]
    public void JsonRoundTripIsExactAndRejectsMisspelledFields()
    {
        var model = TestModels.Creature(); model.Validate();
        var json = model.ToJson();
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
        Assert.Contains("\"frame\": \"shoulder\"", json);
        Assert.DoesNotContain("\"scale\": \"unit\"", json);
        Assert.Throws<JsonException>(() => CharacterModel.FromJson(json.Replace("\"chains\":", "\"chanes\":")));
    }

    [Fact]
    public void NullCollectionIsRejected()
    {
        var error = Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson("{\"id\":\"x\",\"name\":\"X\",\"controls\":null}"));
        Assert.Contains("null", error.Message);
    }

    public static TheoryData<string, string> Rejections => new()
    {
        { "cycle", "cycle" }, { "unknown-parent", "unknown parent" }, { "joint-not-child", "connected" },
        { "shared-solve", "share" }, { "frame-solved", "frame" }, { "unknown-scale", "scale" },
        { "reserved-id", "reserved" }, { "bad-id", "lowercase" }, { "short-bone", "length" }, { "part-control", "Unknown control" },
    };

    [Theory, MemberData(nameof(Rejections))]
    public void StructuralErrorsAreRejectedWithAUsefulMessage(string edit, string fragment)
    {
        var model = TestModels.Creature();
        ModelControl Control(string id) => model.Controls.Single(c => c.Id == id);
        switch (edit)
        {
            case "cycle": Control("body").Parent = "foot"; break;
            case "unknown-parent": Control("hip").Parent = "tail"; break;
            case "joint-not-child": Control("knee").Parent = "body"; break;
            case "shared-solve": model.Chains.Add(new() { Id = "leg2", Root = "hip", Joint = "knee", End = "foot" }); break;
            case "frame-solved": model.Chains[1].Frame = "hand"; break;
            case "unknown-scale": Control("body").Scale = "tail"; break;
            case "reserved-id": Control("body").Id = "locomotion"; break;
            case "bad-id": model.Measures[0].Id = "Leg Length"; break;
            case "short-bone": Control("knee").Rest = new(0, 1.001f); break;
            case "part-control": model.Parts[0].A = "tail"; break;
        }
        var error = Assert.Throws<InvalidDataException>(model.Validate);
        Assert.Contains(fragment, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaveAndLoadAreAtomicAndEquivalent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"model-{Guid.NewGuid():N}.json");
        try
        {
            var model = TestModels.Creature(); model.Save(path);
            Assert.False(File.Exists(path + ".tmp"));
            Assert.Equal(model.ToJson(), CharacterModel.Load(path).ToJson());
        }
        finally { File.Delete(path); }
    }
}
