using App2d.Core.Characters.Authored;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class SlotTimelineTests
{
    private static (ResolvedModel Model, MotionClip Clip) Scene()
    {
        var source = TestModels.Creature();
        var part = source.Parts[0];
        source.Slots = [new() { Id = "paint", Bone = part.A, Attachment = "art" }];
        source.Skins = [new() { Attachments = { ["paint"] = new() { ["art"] = part.Id } } }];
        var model = ResolvedModel.From(source);
        return (model, TestModels.Clip(model));
    }

    [Fact]
    public void AllAppearanceKeysMoveCopyDeleteAndRetimeTogether()
    {
        var (model, clip) = Scene();
        clip.Attachments = [new() { Slot = "paint", Keys = [new() { Time = .25f, Attachment = null }, new() { Time = .75f, Attachment = "art" }] }];
        clip.DrawOrder = [new() { Time = .25f, Slots = ["paint"] }];
        ClipAuthoring.SetColorKey(clip, "paint", SlotColorTrack2D.Rgba, .25f, new(.2f, .3f, .4f, .5f));
        ClipAuthoring.MoveKeys(clip, .25f, .5f, copy: true);
        Assert.Equal([.25f, .5f, .75f], ClipAuthoring.KeyTimes(clip));
        Assert.Equal(2, clip.Colors[0].Keys.Count);
        Assert.Equal(2, clip.DrawOrder.Count);
        ClipAuthoring.DeleteKeys(clip, .25f);
        Assert.Equal([.5f, .75f], ClipAuthoring.KeyTimes(clip));
        ClipAuthoring.Retime(clip, 2);
        Assert.Equal([1f, 1.5f], ClipAuthoring.KeyTimes(clip));
        clip.Validate(model);
        var restored = MotionClip.FromJson(clip.ToJson());
        Assert.Equal(clip.ToJson(), restored.ToJson());
    }

    [Fact]
    public void CurrentClipsCanOmitOptionalAppearanceKeys()
    {
        var (_, clip) = Scene();
        var json = JsonNode.Parse(clip.ToJson())!;
        json.AsObject().Remove("colors");
        var restored = MotionClip.FromJson(json.ToJsonString());
        Assert.Equal(MotionClip.CurrentVersion, restored.Version);
        Assert.Empty(restored.Colors);
    }

    [Theory]
    [InlineData("unknown-slot")]
    [InlineData("invalid-value")]
    [InlineData("invalid-time")]
    [InlineData("invalid-curve")]
    [InlineData("duplicate-component")]
    [InlineData("unknown-attachment")]
    [InlineData("bad-order")]
    public void InvalidAppearanceDataIsRejected(string problem)
    {
        var (model, clip) = Scene();
        ClipAuthoring.SetColorKey(clip, "paint", SlotColorTrack2D.Rgba, 0, Vector4.One);
        switch (problem)
        {
            case "unknown-slot":
                clip.Colors[0].Slot = "missing";
                break;
            case "invalid-value":
                clip.Colors[0].Keys[0].Value = clip.Colors[0].Keys[0].Value with { X = float.NaN };
                break;
            case "invalid-time":
                clip.Colors[0].Keys.Add(new()
                {
                    Time = 0
                });
                break;
            case "invalid-curve":
                clip.Colors[0].Keys[0].CurveA = new()
                {
                    Control1 = new(-1, 0)
                };
                break;
            case "duplicate-component":
                clip.Colors.Add(new()
                {
                    Slot = "paint",
                    Kind = SlotColorTrack2D.Alpha
                });
                break;
            case "unknown-attachment":
                clip.Attachments.Add(new()
                {
                    Slot = "paint",
                    Keys = [new() { Attachment = "missing" }]
                });
                break;
            case "bad-order":
                clip.DrawOrder.Add(new()
                {
                    Slots = ["missing"]
                });
                break;
        }
        Assert.Throws<InvalidDataException>(() => clip.Validate(model));
    }

    [Fact]
    public void ExcerptsPreserveBezierMotionTintAndHeldAppearance()
    {
        var (model, clip) = Scene();
        clip.Tracks = [new() { Target = "body", Scale = CharacterModel.Unit, Keys = [
            new() { Curve = new() { Control1 = new(.15f, 1), Control2 = new(.8f, -1), Absolute = true } }, new() { Time = 1 } ] }];
        clip.Colors = [new() { Slot = "paint", Keys = [
            new() { Value = new(0, 1, 1, 1), CurveR = new() { Control1 = new(.15f, 1), Control2 = new(.8f, 1), Absolute = true } }, new() { Time = 1, Value = new(0, 1, 1, 1) } ] }];
        clip.Attachments = [new() { Slot = "paint", Keys = [new() { Time = .1f, Attachment = null }, new() { Time = .6f, Attachment = "art" }] }];
        clip.DrawOrder = [new() { Time = .1f, Slots = ["paint"] }];
        clip.Events = [new() { Time = .1f, Name = "outside" }, new() { Time = .4f, Name = "inside" }];
        var excerpt = ClipAuthoring.Excerpt(clip, .2f, .8f, "excerpt", "Excerpt");
        excerpt.Validate(model);
        Assert.Null(excerpt.Attachments[0].Keys[0].Attachment);
        Assert.Equal(0, excerpt.DrawOrder[0].Time);
        Assert.Equal("inside", Assert.Single(excerpt.Events).Name);
        foreach (var time in Enumerable.Range(0, 21).Select(i => .6f * i / 20))
        {
            var a = PoseEvaluator.Sample(model, clip, .2f + time);
            var b = PoseEvaluator.Sample(model, excerpt, time);
            TestModels.Near(a.World("body"), b.World("body"), 2e-5f);
            Assert.True(Vector4.Distance(a.Slots[0].Color, b.Slots[0].Color) < 2e-5f);
            Assert.Equal(a.Slots[0].Part is null, b.Slots[0].Part is null);
        }
    }

    [Fact]
    public void NormalizedEasingAndIndependentZCurvesRemainEditable()
    {
        var (_, clip) = Scene();
        var channel = new Channel(MotionClip.TranslateKind, "body");
        ClipAuthoring.SetKey(clip, channel, 0, Vector3.Zero);
        ClipAuthoring.SetKey(clip, channel, 1, new(2, 4, 0));
        ClipAuthoring.SetCurve(clip, channel, 0, 0, new()
        {
            Control1 = new(1f / 3, 0),
            Control2 = new(2f / 3, 0)
        });
        ClipAuthoring.SetCurve(clip, channel, 0, 2, new()
        {
            Control1 = new(1f / 3, 1),
            Control2 = new(2f / 3, 1),
            Absolute = true
        });
        var value = ClipAuthoring.Value(clip, channel, .5f).Value;
        TestModels.Near(new(.25f, .5f, .75f), value);
        ClipAuthoring.SetKey(clip, channel, 0, Vector3.Zero);
        Assert.NotNull(clip.Tracks[0].Keys[0].CurveZ);
        ClipAuthoring.SetEase(clip, 0, ClipEase.Step);
        Assert.Null(clip.Tracks[0].Keys[0].CurveZ);
        TestModels.Near(Vector3.Zero, ClipAuthoring.Value(clip, channel, .5f).Value);
    }
}
