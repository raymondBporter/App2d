using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;
using System.Text.Json;
using ProgressMath = App2d.Core.Mathematics.Progress.Progress;

namespace App2d.Tests.Authored;

public sealed class KeyCurve2DTests
{
    [Fact]
    public void DiagonalHandlesProduceLinearTimingWhileUniformXHandlesProduceSmoothstep()
    {
        var linear = new KeyCurve2D();
        var smooth = new KeyCurve2D { Control1 = new(1f / 3, 0), Control2 = new(2f / 3, 1) };
        for (var i = 0; i <= 100; i++)
        {
            var time = i / 100f;
            Near(time, linear.Apply(time));
            Near(2 + 4 * time, linear.Evaluate(time, 2, 6));
            Near(ProgressMath.SmoothStep(time), smooth.Apply(time));
            Near(2 + 4 * ProgressMath.SmoothStep(time), smooth.Evaluate(time, 2, 6));
        }
        Assert.Equal(2f, smooth.Evaluate(-1, 2, 6));
        Assert.Equal(6f, smooth.Evaluate(2, 2, 6));
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0f, 1f)]
    [InlineData(1f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(.15f, .8f)]
    [InlineData(.9f, .05f)]
    public void TimeLookupFollowsTheGeometryWithFlatAndCrossedTimeHandles(float x1, float x2)
    {
        var key = new KeyCurve2D { Control1 = new(x1, 1), Control2 = new(x2, -1), Absolute = true };
        key.Validate();
        var curve = key.ToCurve(0, 0);
        foreach (var u in new[] { 0f, .125f, .25f, .5f, .75f, .875f, 1f })
        {
            var point = curve.Evaluate(u);
            Near(point.Y, key.Evaluate(point.X, 0, 0));
        }
    }

    [Fact]
    public void AbsoluteHandlesKeepMotionBetweenEqualEndpointsAndCannotAlwaysBeNormalized()
    {
        var key = new KeyCurve2D { Control1 = new(.2f, 9), Control2 = new(.8f, 9), Absolute = true };
        Assert.Equal(8f, key.Evaluate(.5f, 5, 5));
        Assert.False(key.TryConvertValueControls(false, 5, 5, out _));
        var relative = new KeyCurve2D { Control1 = new(0, 2), Control2 = new(1, -1) };
        Assert.True(relative.TryConvertValueControls(true, 2, 6, out var absolute));
        Assert.Equal(new Vector2(0, 2), absolute.ToCurve(2, 6).Start);
        Assert.Equal(relative.ToCurve(2, 6), absolute.ToCurve(2, 6));
    }

    [Theory]
    [InlineData(0f, 1f, false)]
    [InlineData(0f, .7f, false)]
    [InlineData(.2f, 1f, false)]
    [InlineData(.2f, .8f, false)]
    [InlineData(.2f, .8f, true)]
    public void SlicingUsesTimeCoordinatesAndPreservesOvershootingValues(float from, float to, bool equalEndpoints)
    {
        const float start = 5;
        var end = equalEndpoints ? start : -3;
        var key = new KeyCurve2D { Control1 = new(.05f, 12), Control2 = new(.6f, -7), Absolute = true };
        var slice = key.Slice(from, to, start, end);
        slice.Validate();
        Assert.True(slice.Absolute);
        var first = key.Evaluate(from, start, end);
        var last = key.Evaluate(to, start, end);
        for (var i = 0; i <= 100; i++)
        {
            var time = i / 100f;
            Near(key.Evaluate(from + time * (to - from), start, end), slice.Evaluate(time, first, last));
        }
    }

    [Fact]
    public void AuthoredJsonKeepsTheExistingHandleFieldsAndDefaults()
    {
        var key = new KeyCurve2D { Control1 = new(.2f, 2), Control2 = new(.8f, -1), Absolute = true };
        var json = JsonSerializer.Serialize(key, AuthoredJson.Options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["absolute", "x1", "x2", "y1", "y2"], document.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(key, JsonSerializer.Deserialize<KeyCurve2D>(json, AuthoredJson.Options));
        Assert.Equal(new KeyCurve2D(), JsonSerializer.Deserialize<KeyCurve2D>("{}", AuthoredJson.Options));
    }

    private static void Near(float expected, float actual) =>
        Assert.InRange(MathF.Abs(expected - actual), 0, 2e-5f);

    [Theory]
    [InlineData(0f)]
    [InlineData(.3f)]
    [InlineData(1f)]
    public void SplitUsesTimeAndPreservesOvershootBetweenEqualEndpoints(float at)
    {
        var curve = new KeyCurve2D { Control1 = new(.05f, 12), Control2 = new(.6f, -7), Absolute = true };
        var (left, right) = curve.Split(at, 5, 5);
        left.Validate();
        right.Validate();
        var middle = curve.Evaluate(at, 5, 5);
        for (var i = 0; i <= 100; i++)
        {
            var u = i / 100f;
            Near(curve.Evaluate(at * u, 5, 5), left.Evaluate(u, 5, middle));
            Near(curve.Evaluate(at + (1 - at) * u, 5, 5), right.Evaluate(u, middle, 5));
        }
    }
}
