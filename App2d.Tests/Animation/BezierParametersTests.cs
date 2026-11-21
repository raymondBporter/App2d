using App2d.Core.Animation;
using App2d.Core.Characters.Authored;
using App2d.Core.Json;
using App2d.Core.Mathematics.Progress;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Animation;

public sealed class BezierParametersTests
{
    [Theory]
    [InlineData(EasingDirection.In, 0f)]
    [InlineData(EasingDirection.Out, .125f)]
    [InlineData(EasingDirection.InOut, .125f)]
    public void SavedStepPrecedenceIsResolvedBeforeCreatingChannelOverrides(EasingDirection direction, float expected)
    {
        var curve = new BezierParameters { FirstControlTime = 1f / 3f, SecondControlTime = 2f / 3f, SecondControlValue = 0f };
        List<ClipKey> motion = [new() { Ease = EasingCurve.Step, EaseDirection = direction, Curve = curve }, new() { Time = 1f, Value = Vector3f.One }];
        List<SlotColorKey2D> colors = [new() { Ease = EasingCurve.Step, EaseDirection = direction, Value = Vector4.Zero, CurveR = curve }, new() { Time = 1f }];
        Assert.Equal(expected, PoseEvaluator.Sample(motion, .5f).Value.X);
        Assert.Equal(expected, SlotColorTrack2D.Sample(colors, .5f).X);
        Assert.Equal(Vector3f.One, PoseEvaluator.Sample(motion, 1f).Value);
    }

    [Fact]
    public void ExistingSavedCurveObjectsDeserializeWithoutChangingTheirFields()
    {
        const string json = "{\"curve\":{\"x1\":0.2,\"y1\":15,\"x2\":0.8,\"y2\":-5,\"absolute\":true}}";
        var key = JsonSerializer.Deserialize<ClipKey>(json, AuthoredJson.Options)!;
        Assert.Equal(.2f, key.Curve!.FirstControlTime);
        Assert.Equal(15f, key.Curve.FirstControlValue);
        Assert.Equal(.8f, key.Curve.SecondControlTime);
        Assert.Equal(-5f, key.Curve.SecondControlValue);
        using var saved = JsonDocument.Parse(JsonSerializer.Serialize(key, AuthoredJson.Options));
        var curve = saved.RootElement.GetProperty("curve");
        Assert.Equal(.2f, curve.GetProperty("x1").GetSingle());
        Assert.Equal(-5f, curve.GetProperty("y2").GetSingle());
        Assert.True(curve.GetProperty("absolute").GetBoolean());
        var defaults = JsonSerializer.Deserialize<BezierParameters>("{}", AuthoredJson.Options)!;
        Assert.Equal(0f, defaults.FirstControlTime);
        Assert.Equal(1f, defaults.SecondControlTime);
        Assert.Equal(0f, defaults.FirstControlValue);
        Assert.Equal(1f, defaults.SecondControlValue);
    }

    [Fact]
    public void SavedAbsoluteExcursionsStillSampleInMotionAndColor()
    {
        const string curveJson = "{\"x1\":0.33333334,\"y1\":20,\"x2\":0.6666667,\"y2\":20,\"absolute\":true}";
        var curve = JsonSerializer.Deserialize<BezierParameters>(curveJson, AuthoredJson.Options)!;
        List<ClipKey> motion = [new() { Value = new(10f, 2f, 4f), Curve = curve }, new() { Time = 1f, Value = new(10f, 6f, 12f) }];
        Assert.Equal(new Vector3f(17.5f, 4f, 8f), PoseEvaluator.Sample(motion, .5f).Value);
        List<SlotColorKey2D> color = [new() { Value = new Vector4(10f), CurveR = curve }, new() { Time = 1f, Value = new Vector4(10f) }];
        Assert.Equal(17.5f, SlotColorTrack2D.Sample(color, .5f).X, 5);
    }

    [Fact]
    public void SavedRelativeCurvesPreserveNonuniformTiming()
    {
        var key = JsonSerializer.Deserialize<ClipKey>("{\"curve\":{\"x1\":0,\"y1\":0.33333334,\"x2\":0,\"y2\":0.6666667}}", AuthoredJson.Options)!;
        List<ClipKey> keys = [key, new() { Time = 1f, Value = new(10f, 0f, 0f) }];
        Assert.Equal(5f, PoseEvaluator.Sample(keys, .125f).Value.X, 5);
        Assert.Equal(0f, PoseEvaluator.Sample(keys, -1f).Value.X);
        Assert.Equal(10f, PoseEvaluator.Sample(keys, 2f).Value.X);
    }

    [Fact]
    public void MotionKeepsIndependentChannelsAndAppliesRotationCurvesOnlyToTheAngle()
    {
        var slow = new BezierParameters { FirstControlTime = 1f / 3f, SecondControlTime = 2f / 3f, SecondControlValue = 0f };
        var fast = slow with { FirstControlValue = 1f, SecondControlValue = 1f };
        List<ClipKey> keys = [new() { Curve = slow, CurveY = fast }, new() { Time = 1f, Value = new(8f, 16f, 24f), Angle = 2f * MathF.Tau }];

        var position = PoseEvaluator.Sample(keys, .5f);
        Assert.Equal(new Vector3f(1f, 14f, 3f), position.Value);
        Assert.Equal(MathF.Tau, position.Angle);

        var rotation = PoseEvaluator.Sample(keys, .5f, rotate: true);
        Assert.Equal(new Vector3f(4f, 8f, 12f), rotation.Value);
        Assert.Equal(MathF.PI / 2f, rotation.Angle, 6);
    }

    [Fact]
    public void ColorChannelsKeepTheirOwnCurvesAndClampOnlyWhenProducingAColor()
    {
        var slow = new BezierParameters { FirstControlTime = 1f / 3f, SecondControlTime = 2f / 3f, SecondControlValue = 0f };
        var fast = slow with { FirstControlValue = 1f, SecondControlValue = 1f };
        var absolute = slow with { Absolute = true, FirstControlValue = 2f, SecondControlValue = 2f };
        List<SlotColorKey2D> keys = [new() { Value = Vector4.Zero, CurveR = slow, CurveG = fast, CurveB = absolute }, new() { Time = 1f }];
        Assert.Equal(new Vector4(.125f, .875f, 1.625f, .5f), SlotColorTrack2D.Sample(keys, .5f));

        var clip = new MotionClip { Colors = [new() { Slot = "tint", Keys = keys }] };
        Assert.Equal(new Vector4(.125f, .875f, 1f, .5f), SlotColorTrack2D.Evaluate(clip, new() { Id = "tint" }, .5f));
    }
}
