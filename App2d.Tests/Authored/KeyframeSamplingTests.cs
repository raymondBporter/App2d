using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class KeyframeSamplingTests
{
    [Theory]
    [InlineData(ClipEase.Linear)]
    [InlineData(ClipEase.Smooth)]
    [InlineData(ClipEase.Step)]
    public void MotionAndColorShareTimingWhilePreservingComponentCurves(string ease)
    {
        var curve = new KeyCurve2D { Control1 = new(.1f, 1.5f), Control2 = new(.8f, -.5f) };
        List<ClipKey> motion = [new() { Time = .2f, Value = new(.2f, .4f, .6f), Ease = ease, Curve = curve },
            new() { Time = 1, Value = new(.8f, .6f, .2f) }];
        List<SlotColorKey2D> color = [new() { Time = .2f, Value = new(.2f, .4f, .6f, 1), Ease = ease, CurveR = curve, CurveG = curve, CurveB = curve },
            new() { Time = 1, Value = new(.8f, .6f, .2f, 0) }];
        foreach (var time in new[] { -1f, .2f, .3f, .6f, .9f, 1f, 2f })
        {
            var value = PoseEvaluator.Interpolate(motion, time).Value;
            var tint = SlotColorTrack2D.Sample(color, time);
            Assert.Equal(value, new Vector3(tint.X, tint.Y, tint.Z));
        }
        Assert.Equal(color[1].Value, SlotColorTrack2D.Sample(color, 1));
        Assert.Equal(default, PoseEvaluator.Interpolate([], 0));
        Assert.Equal(Vector4.One, SlotColorTrack2D.Sample([], 0));
    }

    [Fact]
    public void VectorStorageReadsAndWritesTheExistingSparseKeyFormat()
    {
        const string motionJson = """{"time":0.5,"x":2,"z":-3,"curve":{"x1":0.2,"y1":2,"x2":0.8,"y2":-1}}""";
        var motion = JsonSerializer.Deserialize<ClipKey>(motionJson, AuthoredJson.Options)!;
        Assert.Equal(new Vector3(2, 0, -3), motion.Value);
        Assert.Equal(new Vector2(.2f, 2), motion.Curve!.Control1);
        var json = JsonSerializer.Serialize(motion, AuthoredJson.Options);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("value", out _));
        Assert.False(document.RootElement.GetProperty("curve").TryGetProperty("control1", out _));
        Assert.Equal(motion, JsonSerializer.Deserialize<ClipKey>(json, AuthoredJson.Options));

        var color = JsonSerializer.Deserialize<SlotColorKey2D>("""{"r":0.2,"b":0.8}""", AuthoredJson.Options)!;
        Assert.Equal(new Vector4(.2f, 1, .8f, 1), color.Value);
        json = JsonSerializer.Serialize(color, AuthoredJson.Options);
        using var tint = JsonDocument.Parse(json);
        Assert.Equal(["r", "b"], tint.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(color, JsonSerializer.Deserialize<SlotColorKey2D>(json, AuthoredJson.Options));
    }

    [Fact]
    public void MotionAndColorSamplingDoNotAllocate()
    {
        List<ClipKey> motion = [new() { Value = new(1, 2, 3), Ease = ClipEase.Smooth }, new() { Time = 1, Value = new(4, 5, 6) }];
        List<SlotColorKey2D> color = [new() { Ease = ClipEase.Smooth }, new() { Time = 1, Value = Vector4.Zero }];
        for (var i = 0; i < 100; i++) Check();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Check();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        void Check()
        {
            PoseEvaluator.Interpolate(motion, .37f);
            SlotColorTrack2D.Sample(color, .37f);
        }
    }
}
