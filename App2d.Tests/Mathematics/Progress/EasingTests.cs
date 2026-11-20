using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Mathematics.Progress;

public sealed class EasingTests
{
    public static IEnumerable<object[]> CurvesAndDirections =>
        from curve in Enum.GetValues<EasingCurve>()
        from direction in Enum.GetValues<EasingDirection>()
        select new object[] { curve, direction };

    [Theory]
    [MemberData(nameof(CurvesAndDirections))]
    public void FloatCurvesMatchTheMathematicsWithinTheirOwnPrecision(EasingCurve curve, EasingDirection direction)
    {
        var single = Easingf.FromCurve(curve, direction);
        var reference = Easingd.FromCurve(curve, direction);
        Assert.Equal(curve != EasingCurve.Step, single.IsContinuous);
        Assert.Equal(0f, single.Sample(0f));
        Assert.Equal(1f, single.Sample(1f));
        foreach (var phase in new[] { -.137f, .1f, .25f, .37f, .75f, .9f, 1.137f })
        {
            var sample = single.Sample(phase);
            var derivative = single.Derivative(phase);
            var sampleTolerance = 128f * Realf.MachineEpsilon * MathF.Max(1f, MathF.Abs((float)reference.Sample(phase)));
            var derivativeTolerance = 32f * Realf.MachineEpsilon * MathF.Max(1f, MathF.Abs((float)reference.Derivative(phase)));
            Assert.InRange(Math.Abs(sample - reference.Sample(phase)), 0d, sampleTolerance);
            Assert.InRange(Math.Abs(derivative - reference.Derivative(phase)), 0d, derivativeTolerance);
            if (curve != EasingCurve.Step && phase is > 0f and < 1f)
                Assert.InRange(MathF.Abs(single.Inverse!(sample) - phase), 0f, 512f * Realf.MachineEpsilon);
        }
        if (curve == EasingCurve.Step) Assert.Null(single.Inverse);
    }

    [Theory]
    [MemberData(nameof(CurvesAndDirections))]
    public void CurvesAndDirectionsKeepTheirEndpointsAndAnalyticDerivatives(EasingCurve curve, EasingDirection direction)
    {
        var map = Easingd.FromCurve(curve, direction);
        Assert.Equal(0d, map.Sample(0d), 12);
        Assert.Equal(1d, map.Sample(1d), 12);
        Assert.Equal(curve != EasingCurve.Step, map.IsContinuous);

        // Include extrapolation, avoiding the step discontinuities and the InOut join.
        foreach (var phase in new[] { -.137d, .013d, .137d, .319d, .421d, .583d, .773d, .947d, 1.137d })
        {
            const double step = 1e-6;
            var expected = (map.Sample(phase + step) - map.Sample(phase - step)) / (2d * step);
            Assert.InRange(Math.Abs(map.Derivative(phase) - expected), 0d, 1e-7 * Math.Max(1d, Math.Abs(expected)));
        }

        if (curve == EasingCurve.Step)
        {
            Assert.Null(map.Inverse);
            return;
        }

        Assert.NotNull(map.Inverse);
        foreach (var phase in new[] { 0d, .1d, .25d, .5d, .75d, .9d, 1d })
        {
            var value = map.Sample(phase);
            Assert.InRange(value, -1e-12, 1d + 1e-12);
            Assert.InRange(Math.Abs(map.Inverse(value) - phase), 0d, 1e-7);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void PowerDegreeAndEaseDirectionAreIndependent(int degree)
    {
        var easeIn = Easingd.Power(degree);
        var easeOut = easeIn.WithDirection(EasingDirection.Out);
        var easeInOut = easeIn.WithDirection(EasingDirection.InOut);
        foreach (var x in new[] { -.25d, 0d, .25d, .5d, .75d, 1d, 1.25d })
        {
            Assert.Equal(Math.Pow(x, degree), easeIn.Sample(x), 12);
            Assert.Equal(1d - Math.Pow(1d - x, degree), easeOut.Sample(x), 12);
            Assert.Equal(x < .5d ? Math.Pow(2d * x, degree) / 2d : 1d - Math.Pow(2d - 2d * x, degree) / 2d,
                easeInOut.Sample(x), 12);
        }
        Assert.Equal(degree == 1 ? 1d : 0d, easeIn.Derivative(0d));
        Assert.Equal(degree, easeIn.Derivative(1d));
        Assert.Equal(degree, easeOut.Derivative(0d));
        Assert.Equal(degree == 1 ? 1d : 0d, easeOut.Derivative(1d));
        Assert.Equal(degree, easeInOut.Derivative(.5d));

        if (degree != 1) return;
        Assert.Same(Easingd.Linear, easeInOut);
        foreach (var phase in new[] { 1e-20, -double.MaxValue, double.MaxValue })
            Assert.Equal(phase, easeInOut.Sample(phase));
    }

    [Theory]
    [InlineData(1, 1.5d)]
    [InlineData(2, 1.875d)]
    [InlineData(3, 2.1875d)]
    public void SmoothstepOrderControlsEndpointFlatnessAndCenterSlope(int order, double slope)
    {
        var map = Easingd.SmoothStep(order);
        Assert.Equal(0d, map.Derivative(0d));
        Assert.Equal(0d, map.Derivative(1d));
        Assert.Equal(slope, map.Derivative(.5d));
        Assert.InRange(map.Sample(.1d), 0d, Easingd.Smooth.Sample(.1d));
    }

    [Fact]
    public void RawPolynomialsPreservePrecisionExtrapolationAndRoundoff()
    {
        for (var index = -100; index <= 1100; index++)
        {
            var single = index / 1000f;
            var wide = index / 1000d;
            Assert.Equal(single * single * (3f - 2f * single), Easingf.SmoothStepValue(single));
            Assert.Equal(single * single * (3f - 2f * single), Easingf.Smooth.Sample(single));
            Assert.Equal(wide * wide * (3d - 2d * wide), Easingd.SmoothStepValue(wide));
        }
        Assert.Equal(-4d, Easingd.Smooth.Sample(2d));
        Assert.Equal(5d, Easingd.Smooth.Sample(-1d));
        Assert.True(Easingd.Smoother.Sample(.9999999d) > 1d);
        Assert.Same(Easingd.Linear, Easingd.SmoothStep(0));
        Assert.Equal(-2f, Easingf.SmoothStepValue(-2f, 0));
    }

    [Fact]
    public void StepIsDiscontinuousAndHasNoInverse()
    {
        Assert.False(Easingd.Step.IsContinuous);
        Assert.Null(Easingd.Step.Inverse);
        Assert.Equal(0d, Easingd.Step.Sample(Math.BitDecrement(1d)));
        Assert.Equal(1d, Easingd.Step.Sample(1d));
        Assert.Equal(0d, Easingd.Step.Derivative(.5d));
        Assert.True(double.IsNaN(Easingd.Step.Derivative(1d)));
        Assert.True(double.IsNaN(Easingd.Step.Sample(double.NaN)));
    }

    [Fact]
    public void InvalidCurveParametersAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.Power(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.Power(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.SmoothStep(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.SmoothStep(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.FromCurve((EasingCurve)int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.Linear.WithDirection((EasingDirection)int.MaxValue));
    }
}
