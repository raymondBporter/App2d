using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
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
    public void RotationPreservesFullTurnsAndUsesItsOwnCurveChannel()
    {
        List<ClipKey> keys =
        [
            new() { Value = new(2, 4, 6), Angle = 0, Curve = new() { Control1 = new(1f / 3, 0), Control2 = new(2f / 3, 0) } },
            new() { Time = 1, Value = new(4, 6, 8), Angle = 2 * MathF.Tau }
        ];
        var sample = PoseEvaluator.Interpolate(keys, .5f, rotate: true);
        Assert.Equal(new Vector3(3, 5, 7), sample.Value);
        Assert.Equal(MathF.Tau / 4, sample.Angle, 5);
        Assert.Equal(2 * MathF.Tau, PoseEvaluator.Interpolate(keys, 1, rotate: true).Angle);
    }

    [Fact]
    public void AbsoluteMotionCurveDoesNotLeakIntoOtherComponentsOrRotation()
    {
        List<ClipKey> keys =
        [
            new() { Value = new(0, 2, 4), Angle = 0, Curve = new() { Absolute = true, Control1 = new(1f / 3, 4), Control2 = new(2f / 3, 4) } },
            new() { Time = 1, Value = new(0, 4, 6), Angle = MathF.Tau }
        ];
        var sample = PoseEvaluator.Interpolate(keys, .5f);
        Assert.Equal(new Vector3(3, 3, 5), sample.Value);
        Assert.Equal(MathF.PI, sample.Angle);
    }

    [Fact]
    public void RawColorSamplingKeepsOvershootAndRenderingClampsTheResult()
    {
        var curve = new KeyCurve2D { Absolute = true, Control1 = new(1f / 3, 2), Control2 = new(2f / 3, 2) };
        var track = new SlotColorTrack2D { Slot = "paint", Keys =
            [new() { Value = Vector4.Zero, CurveR = curve }, new() { Time = 1, Value = Vector4.Zero }] };
        Assert.Equal(new Vector4(1.5f, 0, 0, 0), SlotColorTrack2D.Sample(track.Keys, .5f));
        Assert.Equal(new Vector4(1, 0, 0, 0), SlotColorTrack2D.Evaluate(new() { Colors = [track] }, new() { Id = "paint", Color = "ffffffff" }, .5f));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(.15f, .8f)]
    [InlineData(.9f, .05f)]
    public void SharedTimingPreservesDifferentValueHandlesAndIndependentChannels(float x1, float x2)
    {
        var r = new KeyCurve2D { Control1 = new(x1, 1.5f), Control2 = new(x2, -.5f) };
        var g = new KeyCurve2D { Absolute = true, Control1 = new(x1, 4), Control2 = new(x2, -2) };
        var b = new KeyCurve2D { Control1 = new(.25f, -1), Control2 = new(.6f, 2) };
        var a = new KeyCurve2D { Absolute = true, Control1 = new(x1, -3), Control2 = new(x2, 5) };
        foreach (var ease in ClipEase.All)
        {
            List<ClipKey> motion = [new() { Value = new(2, 3, 4), Angle = 0, Ease = ease, Curve = r, CurveY = g, CurveZ = b },
                new() { Time = 1, Value = new(-2, 3, 6), Angle = 2 * MathF.Tau }];
            List<SlotColorKey2D> color = [new() { Value = new(2, 3, 4, 1), Ease = ease, CurveR = r, CurveG = g, CurveB = b, CurveA = a },
                new() { Time = 1, Value = new(-2, 3, 6, 0) }];
            for (var i = 1; i < 100; i++)
            {
                var time = i / 100f;
                float Expected(float start, float end, KeyCurve2D curve) => ease == ClipEase.Step ? start : curve.Evaluate(time, start, end);
                var expected = new Vector4(Expected(2, -2, r), Expected(3, 3, g), Expected(4, 6, b), Expected(1, 0, a));
                Assert.Equal(expected, SlotColorTrack2D.Sample(color, time));
                var sample = PoseEvaluator.Interpolate(motion, time);
                Assert.Equal(new Vector3(expected.X, expected.Y, expected.Z), sample.Value);
                Assert.Equal(ease == ClipEase.Step ? 0 : Interpolation.Lerp(0, 2 * MathF.Tau, ClipEase.Apply(ease, time)), sample.Angle);
            }
        }
    }

    [Fact]
    public void EditingSharedHandlesTakesEffectOnTheNextSample()
    {
        var curve = new KeyCurve2D { Control1 = new(.1f, 2), Control2 = new(.6f, -1) };
        List<ClipKey> motion = [new() { Value = new(1, 2, 3), Curve = curve }, new() { Time = 1, Value = new(4, 5, 6) }];
        List<SlotColorKey2D> color = [new() { Value = new(1, 2, 3, 4), CurveR = curve, CurveG = curve, CurveB = curve, CurveA = curve },
            new() { Time = 1, Value = new(4, 5, 6, 7) }];
        var before = SlotColorTrack2D.Sample(color, .37f);
        PoseEvaluator.Interpolate(motion, .37f);
        curve.Control1 = new(.9f, -2);
        curve.Control2 = new(.2f, 3);
        var expected = new Vector4(curve.Evaluate(.37f, 1, 4), curve.Evaluate(.37f, 2, 5),
            curve.Evaluate(.37f, 3, 6), curve.Evaluate(.37f, 4, 7));
        Assert.NotEqual(before, expected);
        Assert.Equal(expected, SlotColorTrack2D.Sample(color, .37f));
        Assert.Equal(new Vector3(expected.X, expected.Y, expected.Z), PoseEvaluator.Interpolate(motion, .37f).Value);
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    public void MotionAndColorSamplingDoNotAllocate(int distinctTimings)
    {
        KeyCurve2D? Curve(int channel) => distinctTimings == 0 ? null : new()
        {
            Control1 = new(.1f + channel % distinctTimings * .2f, 2 + channel),
            Control2 = new(.8f - channel % distinctTimings * .2f, -1 - channel),
            Absolute = channel % 2 == 0
        };
        var x = Curve(0); var y = Curve(1); var z = Curve(2); var w = Curve(3);
        List<ClipKey> motion = [new() { Value = new(1, 2, 3), Ease = ClipEase.Smooth, Curve = x, CurveY = y, CurveZ = z },
            new() { Time = 1, Value = new(4, 5, 6) }];
        List<SlotColorKey2D> color = [new() { Ease = ClipEase.Smooth, CurveR = x, CurveG = y, CurveB = z, CurveA = w },
            new() { Time = 1, Value = Vector4.Zero }];
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
