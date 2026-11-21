using App2d.Core.Animation;
using App2d.Core.Characters.Authored;
using App2d.Core.Json;
using App2d.Core.Mathematics.Progress;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Animation;

public sealed class EasingKeyframeTests
{
    [Theory]
    [InlineData("linear", EasingCurve.Linear)]
    [InlineData("smooth", EasingCurve.Smooth)]
    [InlineData("step", EasingCurve.Step)]
    [InlineData("sine", EasingCurve.Sine)]
    public void MotionAndColorReadAndWriteCurveAndDirectionSeparately(string name, EasingCurve curve)
    {
        var json = "{\"ease\":\"" + name + "\",\"easeDirection\":\"in-out\"}";
        var motion = JsonSerializer.Deserialize<ClipKey>(json, AuthoredJson.Options)!;
        var color = JsonSerializer.Deserialize<SlotColorKey2D>(json, AuthoredJson.Options)!;
        Assert.Equal(curve, motion.Ease);
        Assert.Equal(curve, color.Ease);
        var restoredMotion = JsonSerializer.Deserialize<ClipKey>(JsonSerializer.Serialize(motion, AuthoredJson.Options), AuthoredJson.Options)!;
        var restoredColor = JsonSerializer.Deserialize<SlotColorKey2D>(JsonSerializer.Serialize(color, AuthoredJson.Options), AuthoredJson.Options)!;
        Assert.Equal(curve, restoredMotion.Ease);
        Assert.Equal(curve, restoredColor.Ease);
        Assert.Equal(EasingDirection.InOut, restoredMotion.EaseDirection);
        Assert.Equal(EasingDirection.InOut, restoredColor.EaseDirection);
    }

    [Theory]
    [InlineData("{\"ease\":\"unknown\"}")]
    [InlineData("{\"ease\":\"reverse\"}")]
    [InlineData("{\"ease\":1}")]
    [InlineData("{\"ease\":\"1\"}")]
    [InlineData("{\"easeDirection\":\"backward\"}")]
    [InlineData("{\"easeDirection\":1}")]
    public void InvalidNamesAndNumericModesAreRefused(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClipKey>(json, AuthoredJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SlotColorKey2D>(json, AuthoredJson.Options));
    }

    [Fact]
    public void AllCurvesAndDirectionsDriveMotionAndColor()
    {
        foreach (var curve in Enum.GetValues<EasingCurve>())
        foreach (var direction in Enum.GetValues<EasingDirection>())
        {
            MotionClip.ValidateEase(curve, direction, "test ease");
            List<ClipKey> motion = [new() { Ease = curve, EaseDirection = direction }, new() { Time = 1f, Value = Vector3f.One }];
            List<SlotColorKey2D> color = [new() { Ease = curve, EaseDirection = direction, Value = Vector4.Zero }, new() { Time = 1f, Value = Vector4.One }];
            foreach (var phase in new[] { .37f, .5f, .63f })
            {
                var expected = Easingf.FromCurve(curve, direction).Sample(phase);
                Assert.Equal(new Vector3f(expected), PoseEvaluator.Sample(motion, phase).Value);
                Assert.Equal(new Vector4(expected), SlotColorTrack2D.Sample(color, phase));
            }
            Assert.Equal(Vector3f.One, PoseEvaluator.Sample(motion, 1f).Value);
            Assert.Equal(Vector4.One, SlotColorTrack2D.Sample(color, 1f));
        }
    }

    [Fact]
    public void ValidationRefusesUnknownCurveOrDirection()
    {
        Assert.Throws<InvalidDataException>(() => MotionClip.ValidateEase((EasingCurve)99, EasingDirection.In, "test"));
        Assert.Throws<InvalidDataException>(() => MotionClip.ValidateEase(EasingCurve.Linear, (EasingDirection)99, "test"));
    }

    [Fact]
    public void SharedSmoothMapKeepsExistingFloatSamplesWithinRounding()
    {
        var rule = Interpolation.Linear<float>().WithProgress(Easingf.Smooth);
        for (var index = -100; index <= 1100; index++)
        {
            var phase = index / 1000f;
            var expected = phase * phase * (3f - 2f * phase);
            Assert.InRange(Math.Abs(rule.Evaluate(0f, 1f, phase) - expected), 0f, 1e-6f);
        }
    }
}
