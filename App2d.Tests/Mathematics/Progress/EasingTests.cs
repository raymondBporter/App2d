using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Mathematics.Progress;

public sealed class EasingTests
{
    [Fact]
    public void TimedBezierFactoriesPreserveCustomTimingInBothPrecisions()
    {
        // X(u) = u^3 and Y(u) = u.
        var single = Easingf.TimedBezier(0f, 1f / 3f, 0f, 2f / 3f);
        var wide = Easingd.TimedBezier(0d, 1d / 3d, 0d, 2d / 3d);
        Assert.Equal(.5f, single.Sample(.125f), 6);
        Assert.Equal(.5d, wide.Sample(.125d), 12);
        Assert.Equal(4f / 3f, single.Derivative(.125f), 5);
        Assert.Equal(4d / 3d, wide.Derivative(.125d), 12);
        Assert.True(float.IsNaN(single.Sample(float.NaN)));
        Assert.True(double.IsNaN(wide.Derivative(double.NaN)));
        Assert.Equal(1f, single.SampleClamped(float.PositiveInfinity));
        Assert.Equal(0d, wide.SampleClamped(double.NegativeInfinity));
    }

    [Fact]
    public void TimedBezierDerivativesHandleFlattenedAndVerticalParameterizations()
    {
        var flat = Easingf.TimedBezier(1f, 1f, 0f, 0f);
        Assert.Equal(1f, flat.Derivative(.5f));
        var cubic = Easingd.TimedBezier(0d, 0d, 0d, 0d);
        Assert.Equal(1d, cubic.Derivative(0d));
        var vertical = Easingf.TimedBezier(0f, .5f, 1f, .5f);
        Assert.True(float.IsPositiveInfinity(vertical.Derivative(0f)));
        Assert.True(float.IsPositiveInfinity(vertical.Derivative(1f)));
        Assert.Equal(-.125f, vertical.Sample(-.25f));
        Assert.Equal(1.125f, vertical.Sample(1.25f));
        Assert.Equal(.5f, vertical.Derivative(-.25f));
        var cusp = Easingd.TimedBezier(1d, 2d, 0d, 1d);
        Assert.True(double.IsNaN(cusp.Derivative(.5d)));
    }

    [Fact]
    public void TimedBezierSamplesAgreeWithExistingGeometricCurves()
    {
        var random = new Random(1512);
        for (var index = 0; index < 30; index++)
        {
            var firstTime = random.NextSingle();
            var secondTime = random.NextSingle();
            var firstValue = random.NextSingle() * 12f - 4f;
            var secondValue = random.NextSingle() * 12f - 4f;
            var geometry = CubicBezier2D.Builder.Create(new(0f, 3f), new(firstTime, firstValue), new(secondTime, secondValue), new(1f, 7f));
            var single = Easingf.TimedBezier(3f, firstTime, firstValue, secondTime, secondValue, 7f);
            var wide = Easingd.TimedBezier(3d, firstTime, firstValue, secondTime, secondValue, 7d);
            foreach (var phase in new[] { .013f, .137f, .319f, .583f, .773f, .947f })
            {
                var expected = geometry.Evaluate(geometry.ParameterAtX(phase)).Y;
                Assert.InRange(MathF.Abs(single.Sample(phase) - expected), 0f, .00003f);
                Assert.InRange(Math.Abs(wide.Sample(phase) - expected), 0d, .00001d);
                const double step = 1e-6;
                var numerical = (wide.Sample(phase + step) - wide.Sample(phase - step)) / (2d * step);
                Assert.InRange(Math.Abs(wide.Derivative(phase) - numerical), 0d, 1e-7 * Math.Max(1d, Math.Abs(numerical)));
            }
        }
    }

    [Fact]
    public void TimedBezierValidatesItsParameters()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingf.TimedBezier(-.1f, 0f, .8f, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.TimedBezier(.2d, 0d, 1.1d, 1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingf.TimedBezier(.2f, float.NaN, .8f, 1f));
    }

    [Fact]
    public void ScalarBezierFactoriesProvideValueCurvesWithoutTimeHandlesOrAnInverseSolve()
    {
        var single = Easingf.Bezier(0f, 1f);
        var wide = Easingd.Bezier(0d, 1d);
        Assert.Equal(.15625f, single.Sample(.25f));
        Assert.Equal(.15625d, wide.Sample(.25d));
        Assert.Equal(1.125f, single.Derivative(.25f));
        Assert.Equal(1.125d, wide.Derivative(.25d));
        var arch = Easingf.Bezier(10f, 20f, 20f, 10f);
        Assert.Equal(17.5f, arch.Sample(.5f));
        Assert.Equal(15f, arch.Derivative(.25f));
        Assert.Equal(2.5f, arch.Sample(-.25f));
        Assert.Equal(10f, arch.SampleClamped(-.25f));
        Assert.Equal(1.625d, Easingd.Bezier(2d, 2d).Sample(.5d));
    }

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
