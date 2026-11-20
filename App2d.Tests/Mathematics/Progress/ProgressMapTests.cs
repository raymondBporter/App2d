using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Mathematics.Progress;

public sealed class ProgressMapTests
{
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
        Assert.Equal(11.5625f, Interpolation.Lerp(10f, 20f, .25d, Easingd.Smooth));
        var overshoot = new ProgressMapd(_ => 1.2d, _ => 0d);
        Assert.True(Interpolation.Lerp(0f, 10f, .7d, overshoot) > 10f);
        var vector = Interpolation.Lerp(Vector2f.Zero, new(10f, -4f), .7d, overshoot);
        Assert.True(vector.X > 10f);
        Assert.True(vector.Y < -4f);
    }
}
