using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Mathematics.Progress;

public sealed class ProgressMapTests
{
    [Theory]
    [InlineData(2d, 6d, 10d, 20d)]
    [InlineData(6d, 2d, 10d, 20d)]
    [InlineData(2d, 6d, 20d, 10d)]
    [InlineData(6d, 2d, 20d, 10d)]
    public void InputAndOutputRangesComposeWithDerivativesInversesAndClamping(double begin, double end, double valueBegin, double valueEnd)
    {
        var map = Easingd.Smooth.FromRange(begin, end).ToRange(valueBegin, valueEnd);
        var time = begin + .25d * (end - begin);
        var expected = valueBegin + .15625d * (valueEnd - valueBegin);
        Assert.Equal(expected, map.Sample(time));
        Assert.Equal((valueEnd - valueBegin) * 1.125d / (end - begin), map.Derivative(time));
        Assert.Equal(time, map.Inverse!(expected), 12);
        Assert.Equal(begin, map.InputBegin);
        Assert.Equal(end, map.InputEnd);
        Assert.Equal(begin < end ? valueBegin : valueEnd, map.SampleClamped(-100d));
        Assert.Equal(begin < end ? valueEnd : valueBegin, map.SampleClamped(100d));
        var composed = ProgressMapd.Normalize(begin, end).Then(Easingd.Smooth).ToRange(valueBegin, valueEnd);
        Assert.Equal(map.Sample(time), composed.Sample(time));
        var shifted = map.Then(ProgressMapd.Affine(2d, 3d));
        Assert.Equal(2d * map.SampleClamped(-100d) + 3d, shifted.SampleClamped(-100d));
    }

    [Fact]
    public void FloatRangeAdaptationAndAffineMapsUseTheSameBuildingBlocks()
    {
        var map = Easingf.Power(2).FromRange(2f, 6f).ToRange(10f, 20f);
        Assert.Equal(10.625f, map.Sample(3f));
        Assert.Equal(1.25f, map.Derivative(3f));
        Assert.Equal(3f, map.Inverse!(10.625f));
        var affine = ProgressMapf.Affine(-2f, 7f);
        Assert.Equal(1f, affine.Sample(3f));
        Assert.Equal(-2f, affine.Derivative(3f));
        Assert.Equal(3f, affine.Inverse!(1f));
    }

    [Fact]
    public void ConstantAndCollapsedRangesStayConstantThroughComposition()
    {
        var constant = ProgressMapf.Constant(1f).Then(Easingf.Step);
        Assert.Equal(1f, constant.Sample(100f));
        Assert.Equal(0f, constant.Derivative(100f));
        Assert.True(constant.IsContinuous);
        Assert.Null(constant.Inverse);
        var reflectedConstant = ProgressMapf.Constant(0f).WithDirection(EasingDirection.Out).Then(Easingf.Step);
        Assert.Equal(1f, reflectedConstant.Sample(.5f));
        Assert.Equal(0f, reflectedConstant.Derivative(.5f));
        Assert.True(float.IsNaN(ProgressMapf.Constant(.25f).WithDirection(EasingDirection.InOut).Derivative(.5f)));
        var collapsedInput = Easingf.Smooth.FromRange(4f, 4f).ToRange(10f, 20f);
        Assert.Equal(10f, collapsedInput.Sample(-100f));
        Assert.Equal(0f, collapsedInput.Derivative(4f));
        Assert.Null(collapsedInput.Inverse);
        var collapsedOutput = Easingd.Smooth.FromRange(2d, 6d).ToRange(7d, 7d);
        Assert.Equal(7d, collapsedOutput.Sample(100d));
        Assert.Equal(0d, collapsedOutput.Derivative(100d));
        Assert.Equal(7d, collapsedOutput.SampleClamped(-100d));
        Assert.Null(collapsedOutput.Inverse);
        Assert.Equal(7d, ProgressMapd.Affine(0d, 7d).Sample(100d));
    }

    [Fact]
    public void ExtremeFiniteRangesRetainRepresentableSamplesAndDerivatives()
    {
        var normalize = ProgressMapf.Normalize(-float.MaxValue, float.MaxValue);
        Assert.Equal(.5f, normalize.Sample(0f));
        Assert.True(normalize.Derivative(0f) > 0f);
        var smooth = Easingf.Smooth.ToRange(-float.MaxValue, float.MaxValue);
        Assert.Equal(0f, smooth.Sample(.5f));
        Assert.Equal(0f, smooth.Derivative(0f));
        Assert.Equal(0f, smooth.Derivative(1f));
        var tiny = Easingf.Smooth.FromRange(0f, float.Epsilon);
        Assert.Equal(0f, tiny.Derivative(0f));
        Assert.Equal(0f, tiny.Derivative(float.Epsilon));
    }

    [Fact]
    public void ClampedSamplingRestrictsOnlyTheInputInBothPrecisions()
    {
        var single = new ProgressMapf(x => 2f * x, _ => 2f);
        var wide = new ProgressMapd(x => 2d * x, _ => 2d);
        Assert.Equal(2.5f, single.Sample(1.25f));
        Assert.Equal(2f, single.SampleClamped(1.25f));
        Assert.Equal(0f, single.SampleClamped(-.25f));
        Assert.Equal(2.5d, wide.Sample(1.25d));
        Assert.Equal(2d, wide.SampleClamped(1.25d));
        Assert.Equal(0d, wide.SampleClamped(-.25d));
    }

    [Fact]
    public void FloatCompositionCarriesItsOwnDerivativesAndInverse()
    {
        var composed = Easingf.Smooth.Then(Easingf.Power(2));
        Assert.Equal(.0244140625f, composed.Sample(.25f));
        Assert.Equal(.3515625f, composed.Derivative(.25f));
        Assert.InRange(MathF.Abs(composed.Inverse!(composed.Sample(.25f)) - .25f), 0f, 4f * Realf.MachineEpsilon);
        Assert.Equal(1f, ProgressMapf.PingPong.Sample(.5f));
        Assert.Equal(-2f, ProgressMapf.PingPong.Derivative(.5f));
        Assert.Equal(1f, ProgressMapf.Reverse.Sample(0f));
        Assert.Equal(-1f, ProgressMapf.Reverse.Derivative(.5f));
        Assert.Equal(0f, ProgressMapf.ReversePingPong.Sample(.5f));
        Assert.Equal(2f, ProgressMapf.ReversePingPong.Derivative(.5f));
        var roundTrip = ProgressMapf.PingPong.Then(Easingf.Smooth);
        Assert.Equal(0f, roundTrip.Derivative(.5f));
        Assert.Null(roundTrip.Inverse);
        Assert.True(float.IsNaN(Easingf.Step.Derivative(1f)));
    }

    [Fact]
    public void FloatNumericalDerivativesUsePrecisionAppropriateSteps()
    {
        var normalized = ProgressMapf.FromFunction(x => { Assert.InRange(x, 0f, 1f); return x * x; });
        var step = MathF.Cbrt(Realf.MachineEpsilon);
        Assert.InRange(MathF.Abs(normalized.Derivative(0f)), 0f, 2f * step);
        Assert.InRange(MathF.Abs(normalized.Derivative(1f) - 2f), 0f, 2f * step);
        Assert.InRange(MathF.Abs(normalized.Derivative(.5f) - 1f), 0f, 8f * step * step);
        var extrapolated = ProgressMapf.FromFunction(x => x * x);
        Assert.InRange(MathF.Abs(extrapolated.Derivative(1_000_000f) / 2_000_000f - 1f), 0f, 8f * step * step);
    }

    [Theory]
    [InlineData(ProgressTravel.Forward, 0d, 1d, 1d)]
    [InlineData(ProgressTravel.Reverse, 1d, 0d, -1d)]
    [InlineData(ProgressTravel.PingPong, 0d, 0d, -2d)]
    [InlineData(ProgressTravel.ReversePingPong, 1d, 1d, 2d)]
    public void TravelIsIndependentOfCurveAndPlayback(ProgressTravel travel, double start, double end, double midpointSlope)
    {
        var map = ProgressMapd.FromTravel(travel);
        Assert.Equal(start, map.Sample(0d));
        Assert.Equal(end, map.Sample(1d));
        Assert.Equal(midpointSlope, map.Derivative(.5d));
        Assert.True(map.IsContinuous);
        foreach (var x in new[] { -.25d, 0d, .25d, .5d, .75d, 1d, 1.25d })
        {
            var expected = travel switch
            {
                ProgressTravel.Forward => x,
                ProgressTravel.Reverse => 1d - x,
                ProgressTravel.PingPong => 1d - Math.Abs(2d * x - 1d),
                _ => Math.Abs(2d * x - 1d)
            };
            Assert.Equal(expected, map.Sample(x));
        }
    }

    [Fact]
    public void CompositionPreservesOrderAndUsesTheChainRule()
    {
        var square = Easingd.Power(2);
        var composed = Easingd.Smooth.Then(square);
        Assert.Equal(.0244140625d, composed.Sample(.25d));
        Assert.Equal(.3515625d, composed.Derivative(.25d));
        Assert.NotEqual(composed.Sample(.25d), square.Then(Easingd.Smooth).Sample(.25d));
        Assert.Equal(.25d, composed.Inverse!(composed.Sample(.25d)), 12);

        var roundTrip = ProgressMapd.PingPong.Then(Easingd.Smooth);
        Assert.Equal(0d, roundTrip.Derivative(.5d));
        Assert.Equal(0d, roundTrip.Derivative(1d));
        Assert.True(roundTrip.IsContinuous);
        Assert.Null(roundTrip.Inverse);
        Assert.Null(ProgressMapd.ReversePingPong.Inverse);
        Assert.Equal(.25d, ProgressMapd.Reverse.Inverse!(.75d));
    }

    [Fact]
    public void DirectionCarriesAnalyticDerivativesAndDoesNotReverseTravel()
    {
        var derivativeCalls = 0;
        var source = new ProgressMapd(x => x * x * x, x => { derivativeCalls++; return 3d * x * x; });
        var easeOut = source.WithDirection(EasingDirection.Out);
        var easeInOut = source.WithDirection(EasingDirection.InOut);
        Assert.Equal(1.6875d, easeOut.Derivative(.25d));
        Assert.Equal(.75d, easeInOut.Derivative(.25d));
        Assert.Equal(.75d, easeInOut.Derivative(.75d));
        Assert.Equal(3d, easeInOut.Derivative(.5d));
        Assert.Equal(4, derivativeCalls);
        Assert.Equal(0d, easeOut.Sample(0d));
        Assert.Equal(1d, easeOut.Sample(1d));
        Assert.Equal(1d, ProgressMapd.Reverse.Then(source).Sample(0d));
        Assert.Equal(0d, ProgressMapd.Reverse.Then(source).Sample(1d));
    }

    [Fact]
    public void MetadataDoesNotInventContinuityOrAnInverse()
    {
        var unknown = ProgressMapd.FromFunction(x => x * x);
        Assert.Null(unknown.IsContinuous);
        Assert.Null(unknown.Inverse);
        var half = new ProgressMapd(x => x / 2d, _ => .5d, x => 2d * x, isContinuous: true);
        var split = half.WithDirection(EasingDirection.InOut);
        Assert.False(split.IsContinuous);
        Assert.Null(split.Inverse);
        Assert.True(split.Sample(Math.BitDecrement(.5d)) < .3d);
        Assert.True(split.Sample(.5d) > .7d);
    }

    [Fact]
    public void NumericalDerivativesUseOneSidedEndpointsAndSupportExtrapolation()
    {
        var normalized = ProgressMapd.FromFunction(x => { Assert.InRange(x, 0d, 1d); return x * x; });
        Assert.InRange(Math.Abs(normalized.Derivative(0d)), 0d, 2e-5);
        Assert.Equal(1d, normalized.Derivative(.5d), 9);
        Assert.InRange(Math.Abs(normalized.Derivative(1d) - 2d), 0d, 2e-5);
        Assert.Equal(4d, ProgressMapd.FromFunction(x => x * x).Derivative(2d), 9);
    }

    [Fact]
    public void InterpolationPreservesIntentionalOvershoot()
    {
        Assert.Equal(11.5625f, Interpolation.Interpolate(10f, 20f, Easingd.Smooth, .25d));
        var overshoot = new ProgressMapd(_ => 1.2d, _ => 0d);
        Assert.True(Interpolation.Interpolate(0f, 10f, overshoot, .7d) > 10f);
        var vector = Vector2d.Zero.Interpolate(new(10d, -4d), overshoot, .7d);
        Assert.True(vector.X > 10f);
        Assert.True(vector.Y < -4f);
    }
}
