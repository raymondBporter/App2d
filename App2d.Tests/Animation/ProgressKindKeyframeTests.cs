using App2d.Core.Animation;
using App2d.Core.Characters.Authored;
using App2d.Core.Json;
using App2d.Core.Mathematics.Progress;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Animation;

public sealed class ProgressKindKeyframeTests
{
    [Theory]
    [InlineData("linear", ProgressMapKind.Linear)]
    [InlineData("smooth", ProgressMapKind.Smooth)]
    [InlineData("step", ProgressMapKind.Step)]
    [InlineData("ease-in-out-sine", ProgressMapKind.EaseInOutSine)]
    public void MotionAndColorReadAndWriteNamedEnumValues(string name, ProgressMapKind kind)
    {
        var json = "{\"ease\":\"" + name + "\"}";
        var motion = JsonSerializer.Deserialize<ClipKey>(json, AuthoredJson.Options)!;
        var color = JsonSerializer.Deserialize<SlotColorKey2D>(json, AuthoredJson.Options)!;
        Assert.Equal(kind, motion.Ease);
        Assert.Equal(kind, color.Ease);
        Assert.Equal(kind, JsonSerializer.Deserialize<ClipKey>(JsonSerializer.Serialize(motion, AuthoredJson.Options), AuthoredJson.Options)!.Ease);
        Assert.Equal(kind, JsonSerializer.Deserialize<SlotColorKey2D>(JsonSerializer.Serialize(color, AuthoredJson.Options), AuthoredJson.Options)!.Ease);
        Assert.Equal("\"" + name + "\"", JsonSerializer.Serialize(kind));
    }

    [Theory]
    [InlineData("{\"ease\":\"unknown\"}")]
    [InlineData("{\"ease\":1}")]
    [InlineData("{\"ease\":\"1\"}")]
    public void InvalidNamesAndNumericModesAreRefused(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ClipKey>(json, AuthoredJson.Options));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<SlotColorKey2D>(json, AuthoredJson.Options));
    }

    [Fact]
    public void AllForwardMapsDriveMotionAndColorThroughFromKind()
    {
        foreach (var kind in ProgressMaps.ForTravel(EasingTravel.Forward))
        {
            MotionClip.ValidateEase(kind, "test ease");
            List<ClipKey> motion = [new() { Ease = kind }, new() { Time = 1f, Value = Vector3.One }];
            List<SlotColorKey2D> color = [new() { Ease = kind, Value = Vector4.Zero }, new() { Time = 1f, Value = Vector4.One }];
            var expected = (float)ProgressMaps.FromKind(kind).Sample(.37f);
            Assert.Equal(new Vector3(expected), PoseEvaluator.Interpolate(motion, .37f).Value);
            Assert.Equal(new Vector4(expected), SlotColorTrack2D.Sample(color, .37f));
            Assert.Equal(Vector3.One, PoseEvaluator.Interpolate(motion, 1f).Value);
            Assert.Equal(Vector4.One, SlotColorTrack2D.Sample(color, 1f));
        }
    }

    [Theory]
    [InlineData(ProgressMapKind.Reverse)]
    [InlineData(ProgressMapKind.PingPong)]
    [InlineData(ProgressMapKind.ReversePingPong)]
    [InlineData((ProgressMapKind)int.MaxValue)]
    public void KeyValidationRefusesOtherTravelDirectionsAndUnknownKinds(ProgressMapKind kind)
    {
        Assert.Throws<InvalidDataException>(() => MotionClip.ValidateEase(kind, "test ease"));
    }

    [Fact]
    public void SharedSmoothMapKeepsLegacyProgressWithinFloatRounding()
    {
        for (var i = -100; i <= 1100; i++)
        {
            var phase = i / 1000f;
            var legacy = phase * phase * (3f - 2f * phase);
            var sample = new KeyframeSegmentSampler(phase, ProgressMaps.FromKind(ProgressMapKind.Smooth));
            Assert.InRange(Math.Abs(sample.Interpolate(0f, 1f) - legacy), 0f, 1e-6f);
        }
    }
}
