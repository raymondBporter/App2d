using App2d.Core.Mathematics.Progress;
using System.Reflection;

namespace App2d.Tests.Mathematics.Progress;

public sealed class EasingTests
{
    public static IEnumerable<object[]> NamedEasings => typeof(Easing)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(member => member.FieldType == typeof(ProgressMap))
        .Select(member => new object[] { member.Name });

    [Theory]
    [MemberData(nameof(NamedEasings))]
    public void AnalyticDerivativesMatchIndependentDifferencesAwayFromCorners(string name)
    {
        var map = (ProgressMap)typeof(Easing).GetField(name)!.GetValue(null)!;
        // Check normalized samples and extrapolation away from endpoints.
        foreach (var phase in new[] { -.137d, .013d, .137d, .319d, .421d, .583d, .773d, .947d, 1.137d })
        {
            const double step = 1e-6;
            var lower = map.Sample(phase - step);
            var upper = map.Sample(phase + step);
            var actual = map.Derivative(phase);
            if (double.IsNaN(lower) || double.IsNaN(upper))
            {
                Assert.True(double.IsNaN(actual), $"{name} at {phase}");
                continue;
            }
            var expected = (upper - lower) / (2d * step);
            Assert.InRange(Math.Abs(actual - expected), 0d, 1e-7 * Math.Max(1d, Math.Abs(expected)));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    public void PowerFamiliesSupportArbitraryPositiveDegreesAndExactEndpointSlopes(int power)
    {
        var easeIn = Easing.EaseInPower(power);
        var easeOut = Easing.EaseOutPower(power);
        var easeInOut = Easing.EaseInOutPower(power);
        if (power == 1)
        {
            foreach (var map in new[] { easeIn, easeOut, easeInOut })
            {
                Assert.Same(Easing.Linear, map);
                foreach (var phase in new[] { 1e-20, -double.MaxValue, double.MaxValue })
                {
                    Assert.Equal(phase, map.Sample(phase));
                    Assert.Equal(1d, map.Derivative(phase));
                }
            }
        }
        foreach (var x in new[] { -.25d, 0d, .25d, .5d, .75d, 1d, 1.25d })
        {
            Assert.Equal(Math.Pow(x, power), easeIn.Sample(x), 12);
            Assert.Equal(1d - Math.Pow(1d - x, power), easeOut.Sample(x), 12);
            Assert.Equal(x < .5d ? Math.Pow(2d * x, power) / 2d : 1d - Math.Pow(2d - 2d * x, power) / 2d,
                easeInOut.Sample(x), 12);
        }
        Assert.Equal(power == 1 ? 1d : 0d, easeIn.Derivative(0));
        Assert.Equal(power, easeIn.Derivative(1));
        Assert.Equal(power, easeOut.Derivative(0));
        Assert.Equal(power == 1 ? 1d : 0d, easeOut.Derivative(1));
        Assert.Equal(power, easeInOut.Derivative(.5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PowerFamiliesRejectNonpositiveDegrees(int power)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Easing.EaseInPower(power));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easing.EaseOutPower(power));
        Assert.Throws<ArgumentOutOfRangeException>(() => Easing.EaseInOutPower(power));
    }

    [Fact]
    public void NamedPowersKeepTheirDegrees()
    {
        foreach (var (power, easeIn, easeOut, easeInOut) in new[]
        {
            (2, Easing.EaseInQuad, Easing.EaseOutQuad, Easing.EaseInOutQuad),
            (3, Easing.EaseInCubic, Easing.EaseOutCubic, Easing.EaseInOutCubic),
            (4, Easing.EaseInQuart, Easing.EaseOutQuart, Easing.EaseInOutQuart),
            (5, Easing.EaseInQuint, Easing.EaseOutQuint, Easing.EaseInOutQuint)
        })
        {
            Assert.Equal(Math.Pow(.25d, power), easeIn.Sample(.25));
            Assert.Equal(1d - Math.Pow(.75d, power), easeOut.Sample(.25));
            Assert.Equal(Math.Pow(.5d, power) / 2d, easeInOut.Sample(.25));
            Assert.Equal(power * Math.Pow(.25d, power - 1), easeIn.Derivative(.25));
            Assert.Equal(power * Math.Pow(.75d, power - 1), easeOut.Derivative(.25));
            Assert.Equal(power * Math.Pow(.5d, power - 1), easeInOut.Derivative(.25));
        }
    }

    [Fact]
    public void EasingTransformationsPropagateDerivativesWithoutNumericalSampling()
    {
        var sampleCalls = 0;
        var derivativeCalls = 0;
        var source = new ProgressMap(x =>
        {
            sampleCalls++;
            return x * x * x;
        },
            x =>
            {
                derivativeCalls++;
                return 3d * x * x;
            });
        var easeOut = ProgressMaps.EaseOut(source);
        var easeInOut = ProgressMaps.EaseInOut(source);

        Assert.Equal(1.6875d, easeOut.Derivative(.25));
        Assert.Equal(.75d, easeInOut.Derivative(.25));
        Assert.Equal(.75d, easeInOut.Derivative(.75));
        Assert.Equal(3d, easeInOut.Derivative(.5));
        Assert.Equal(0, sampleCalls);
        Assert.Equal(4, derivativeCalls);
        Assert.Throws<ArgumentNullException>(() => ProgressMaps.EaseOut(null!));
        Assert.Throws<ArgumentNullException>(() => ProgressMaps.EaseInOut(null!));
    }

}
