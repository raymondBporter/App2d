using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics;
using System.Numerics;
using System.Reflection;

namespace App2d.Tests.Mathematics;

public sealed class ProgressMapTests
{
    [Fact]
    public void ClampedProgressAndExtrapolatingEasingKeepDistinctDomains()
    {
        Assert.Equal(1, Progress.SmoothStep(2));
        Assert.Equal(-4, Easing.Smooth.Sample(2));
        Assert.Equal(0, Progress.SmoothStep(-1));
        Assert.Equal(5, Easing.Smooth.Sample(-1));
    }

    [Fact]
    public void SharedPolynomialsPreserveTheCallersFloatOrDoubleArithmetic()
    {
        for (var i = -100; i <= 1100; i++)
        {
            var single = i / 1000f;
            var wide = i / 1000d;
            Assert.Equal(single * single * (3f - 2f * single), Progress.SmoothStepUnclamped(single));
            Assert.Equal(wide * wide * (3d - 2d * wide), Progress.SmoothStepUnclamped(wide));
        }
    }

    [Fact]
    public void EveryNamedEasingRetainsItsEndpointsAndFiniteNormalizedSamples()
    {
        var fields = typeof(Easing).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(ProgressMap)).ToArray();
        Assert.True(fields.Length > 20);
        foreach (var field in fields)
        {
            var map = (ProgressMap)field.GetValue(null)!;
            Assert.InRange(Math.Abs(map.Sample(0)), 0, 1e-12);
            Assert.InRange(Math.Abs(map.Sample(1) - 1), 0, 1e-12);
            foreach (var phase in new[] { 0d, .1d, .5d, .9d, 1d })
            {
                Assert.True(double.IsFinite(map.Sample(phase)), field.Name);
            }
        }
    }

    [Fact]
    public void SmoothFamiliesHaveExactEndpointVelocitiesAndKnownCenterSlopes()
    {
        Assert.Same(Progress.SmoothStepMap, Easing.Smooth);
        Assert.Same(Progress.SmootherStepMap, Easing.Smoother);
        Assert.Same(Progress.SmoothestStepMap, Easing.Smoothest);
        foreach (var (map, slope) in new[] { (Easing.Smooth, 1.5d), (Easing.Smoother, 1.875d), (Easing.Smoothest, 2.1875d) })
        {
            Assert.Equal(0, map.Derivative(0));
            Assert.Equal(0, map.Derivative(1));
            Assert.Equal(slope, map.Derivative(.5));
        }
    }

    [Fact]
    public void CompositionAppliesTheChainRuleAndKeepsPingPongTurnaroundConvention()
    {
        var square = new ProgressMap(x => x * x, x => 2 * x);
        var composed = ProgressMaps.Compose(Easing.Smooth, square);
        Assert.Equal(.0244140625, composed.Sample(.25));
        Assert.Equal(.3515625, composed.Derivative(.25));
        Assert.Equal(1, ProgressMaps.PingPong.Sample(.5));
        Assert.Equal(-2, ProgressMaps.PingPong.Derivative(.5));
        var motion = ProgressMaps.Compose(ProgressMaps.PingPong, Easing.Smooth);
        Assert.Equal(0, motion.Derivative(.5));
        Assert.Equal(0, motion.Derivative(1));
    }

    [Fact]
    public void NumericalDerivativesUseOneSidedEndpointsAndSupportExtrapolation()
    {
        var normalized = ProgressMap.FromFunction(x =>
        {
            Assert.InRange(x, 0, 1);
            return x * x;
        });
        Assert.InRange(Math.Abs(normalized.Derivative(0)), 0, 2e-5);
        Assert.Equal(1, normalized.Derivative(.5), 9);
        Assert.InRange(Math.Abs(normalized.Derivative(1) - 2), 0, 2e-5);
        var extrapolated = ProgressMap.FromFunction(x => x * x);
        Assert.Equal(4, extrapolated.Derivative(2), 9);
    }

    [Fact]
    public void InterpolationAcceptsMapsDirectlyWithoutClippingOvershoot()
    {
        Assert.Equal(11.5625f, Interpolation.Lerp(10, 20, .25, Easing.Smooth));
        Assert.True(Easing.EaseInBack.Sample(.3) < 0);
        Assert.True(Interpolation.Lerp(0, 10, .7, Easing.EaseOutBack) > 10);
        var vector = Interpolation.Lerp(Vector2.Zero, new(10, -4), .7, Easing.EaseOutBack);
        Assert.True(vector.X > 10);
        Assert.True(vector.Y < -4);
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(.25f)]
    [InlineData(1f)]
    [InlineData(2f)]
    public void SavedClipEasingNamesKeepTheirFloatAndStepSemantics(float phase)
    {
        Assert.Equal(["linear", "smooth", "step"], ClipEase.All);
        Assert.Equal(phase, ClipEase.Apply("linear", phase));
        Assert.Equal(phase * phase * (3 - 2 * phase), ClipEase.Apply("smooth", phase));
        Assert.Equal(0, ClipEase.Apply("step", phase));
    }
}
