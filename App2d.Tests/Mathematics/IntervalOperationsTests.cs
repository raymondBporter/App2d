using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class IntervalOperationsTests
{
    [Theory]
    [InlineData(float.NaN, 1f)]
    [InlineData(0f, float.NaN)]
    [InlineData(2f, 1f)]
    [InlineData(float.PositiveInfinity, float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity, float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity, 1f)]
    [InlineData(0f, float.NegativeInfinity)]
    public void ReusableValidationMatchesTheConstructor(float min, float max)
    {
        Assert.ThrowsAny<ArgumentException>(() => Interval<float>.Validate(min, max));
        Assert.False(Interval<float>.IsValid(min, max));
        Assert.ThrowsAny<ArgumentException>(() => Interval<float>.From(new EndpointsOnly<float>(min, max)));
        Assert.ThrowsAny<ArgumentException>(() => new Interval<float>(min, max));
    }

    [Fact]
    public void ValidationAcceptsFiniteSingletonAndOutwardInfiniteEndpoints()
    {
        Interval<float>.Validate(float.NegativeInfinity, float.PositiveInfinity);
        Interval<double>.Validate(2, double.PositiveInfinity);
        Interval<decimal>.Validate(2m, 8m);
        default(Interval<int>).Validate();
        new Interval<double>(2, 8).Validate();
        Assert.Equal(Interval.AtMost(0f), Interval<float>.From(new EndpointsOnly<float>(float.NegativeInfinity, 0)));
        Assert.True(Interval<float>.IsValid(float.NegativeInfinity, float.PositiveInfinity));
    }

    [Theory]
    [InlineData(0, 2, 5, 8, 3, 8, 3)]
    [InlineData(0, 5, 3, 8, 0, 8, -2)]
    [InlineData(0, 10, 3, 4, 0, 7, -4)]
    [InlineData(0, 2, 2, 5, 0, 5, 0)]
    [InlineData(3, 3, 0, 10, 0, 7, -3)]
    [InlineData(3, 3, 3, 3, 0, 0, 0)]
    [InlineData(0, 10, 0, 10, 0, 10, -10)]
    public void DistancesAreSymmetricAndContainmentUsesEscapeDepth(
        double aMin, double aMax, double bMin, double bMax, double nearest, double farthest, double signed)
    {
        var first = new Interval<double>(aMin, aMax);
        var second = new Interval<double>(bMin, bMax);
        var bare = Interval<double>.From(new EndpointsOnly<double>(aMin, aMax));

        Assert.Equal(nearest, first.Distance(second));
        Assert.Equal(nearest, second.Distance(first));
        Assert.Equal(nearest, bare.Distance(second));
        Assert.Equal(farthest, first.MaxDistance(second));
        Assert.Equal(farthest, second.MaxDistance(first));
        Assert.Equal(farthest, bare.MaxDistance(second));
        Assert.Equal(signed, first.SignedDistance(second));
        Assert.Equal(signed, second.SignedDistance(first));
        Assert.Equal(signed, bare.SignedDistance(second));
    }

    [Fact]
    public void SmallIntegerDistancesMatchAnExhaustivePointAndTranslationSearch()
    {
        for (var aMin = -3; aMin <= 3; aMin++)
        for (var aMax = aMin; aMax <= 3; aMax++)
        for (var bMin = -3; bMin <= 3; bMin++)
        for (var bMax = bMin; bMax <= 3; bMax++)
        {
            var nearest = int.MaxValue;
            var farthest = 0;
            for (var a = aMin; a <= aMax; a++)
            for (var b = bMin; b <= bMax; b++)
            {
                var distance = Math.Abs(b - a);
                nearest = Math.Min(nearest, distance);
                farthest = Math.Max(farthest, distance);
            }

            var escape = int.MaxValue;
            for (var shift = -8; shift <= 8; shift++)
            {
                if (aMax <= bMin + shift || bMax + shift <= aMin)
                    escape = Math.Min(escape, Math.Abs(shift));
            }

            var first = new Interval<int>(aMin, aMax);
            var second = new Interval<int>(bMin, bMax);
            Assert.Equal(nearest, first.Distance(second));
            Assert.Equal(farthest, first.MaxDistance(second));
            Assert.Equal(nearest > 0 ? nearest : -escape, first.SignedDistance(second));
        }
    }

    [Fact]
    public void UnboundedDistancesDoNotSubtractMatchingInfinities()
    {
        var entire = Interval.Entire<double>();
        var bounded = new Interval<double>(2, 5);
        Assert.Equal(0d, entire.Distance(entire));
        Assert.Equal(0d, entire.Distance(bounded));
        Assert.Equal(double.PositiveInfinity, entire.MaxDistance(bounded));
        Assert.Equal(double.NegativeInfinity, entire.SignedDistance(bounded));
        Assert.Equal(double.NegativeInfinity, Interval.AtLeast(2d).SignedDistance(Interval.AtLeast(5d)));
        Assert.Equal(3d, Interval.AtMost(2d).Distance(Interval.AtLeast(5d)));
        Assert.Equal(-3d, Interval.AtMost(5d).SignedDistance(Interval.AtLeast(2d)));
    }

    [Theory]
    [InlineData(-0.5, -1)]
    [InlineData(0, 2)]
    [InlineData(0.25, 3.5)]
    [InlineData(0.5, 5)]
    [InlineData(1, 8)]
    [InlineData(1.5, 11)]
    public void ParameterMappingAndRecoveryRoundTrip(double t, double value)
    {
        var interval = new Interval<double>(2, 8);
        var bare = Interval<double>.From(new EndpointsOnly<double>(2, 8));
        Assert.Equal(value, interval.Lerp(t));
        Assert.Equal(t, interval.InverseLerp(value));
        Assert.Equal(value, bare.Lerp(t));
        Assert.Equal(t, bare.InverseLerp(value));
        Assert.Equal(Math.Clamp(value, 2, 8), interval.LerpClamped(t));
        Assert.Equal(Math.Clamp(t, 0, 1), interval.InverseLerpClamped(value));
        Assert.Equal(Math.Clamp(value, 2, 8), bare.LerpClamped(t));
        Assert.Equal(Math.Clamp(t, 0, 1), bare.InverseLerpClamped(value));
    }

    [Fact]
    public void ParametersHandleSingletonsNaNAndOverflowingFloatingDifferences()
    {
        var point = Interval<double>.Singleton(4);
        Assert.Equal(4d, point.Lerp(-10));
        Assert.Equal(4d, point.LerpClamped(10));
        Assert.Equal(0d, point.InverseLerp(100));
        Assert.Equal(0d, point.InverseLerpClamped(-100));

        var wide = new Interval<float>(-float.MaxValue, float.MaxValue);
        Assert.Equal(-float.MaxValue, wide.Lerp(0));
        Assert.Equal(float.MaxValue, wide.Lerp(1));
        Assert.Equal(0f, wide.Lerp(0.5f));
        Assert.Equal(0.5f, wide.InverseLerp(0));
        Assert.Equal(4f, new Interval<float>(-float.MaxValue, -float.MaxValue / 2).InverseLerp(float.MaxValue));
        Assert.True(float.IsNaN(wide.Lerp(float.NaN)));
        Assert.True(float.IsNaN(wide.LerpClamped(float.NaN)));
        Assert.True(float.IsNaN(wide.InverseLerp(float.NaN)));
        Assert.True(float.IsNaN(wide.InverseLerpClamped(float.NaN)));
    }

    [Fact]
    public void ParameterizationRequiresFiniteEndpointsAndRetainsTheNumericType()
    {
        var unbounded = Interval.AtLeast(2d);
        Assert.Throws<ArgumentOutOfRangeException>(() => unbounded.Lerp(0.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => unbounded.LerpClamped(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => unbounded.InverseLerp(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => unbounded.InverseLerpClamped(2));

        Assert.Equal(5m, new Interval<decimal>(2, 8).Lerp(0.5m));
        Assert.Equal(0.5m, new Interval<decimal>(2, 8).InverseLerp(5));
        Assert.Equal(14, new Interval<int>(2, 8).Lerp(2));
        Assert.Equal(0, new Interval<int>(2, 8).InverseLerp(5));
        Assert.Equal(0, new Interval<int>(int.MinValue, 0).InverseLerpClamped(int.MinValue));
        Assert.Equal(1, new Interval<int>(int.MinValue, 0).InverseLerpClamped(int.MaxValue));
        Assert.Equal((Half)2, new Interval<Half>((Half)(-4), (Half)8).Lerp((Half)0.5));
    }

    [Fact]
    public void TranslationReflectionAndScalingPreserveOrderedEndpoints()
    {
        var interval = new Interval<double>(2, 5);
        var bare = Interval<double>.From(new EndpointsOnly<double>(2, 5));
        Assert.Equal(new Interval<double>(5, 8), interval.Translate(3));
        Assert.Equal(new Interval<double>(-5, -2), interval.Reflect());
        Assert.Equal(new Interval<double>(1, 4), interval.Reflect(3));
        Assert.Equal(new Interval<double>(4, 10), interval.Scale(2));
        Assert.Equal(new Interval<double>(-10, -4), interval.Scale(-2));
        Assert.Equal(new Interval<double>(-1, 5), interval.Scale(-2, 3));
        Assert.Equal(Interval<double>.Singleton(3), interval.Scale(0, 3));
        Assert.Equal(interval, interval.Scale(1, double.MaxValue));
        Assert.Equal(interval, interval.Reflect(3).Reflect(3));
        Assert.Equal(interval, interval.Translate(3).Translate(-3));

        Assert.Equal(interval.Translate(3), bare.Translate(3d));
        Assert.Equal(interval.Reflect(), bare.Reflect());
        Assert.Equal(interval.Reflect(3), bare.Reflect(3d));
        Assert.Equal(interval.Scale(-2), bare.Scale(-2d));
        Assert.Equal(interval.Scale(-2, 3), bare.Scale(-2d, 3d));
        Assert.Equal(Interval<double>.Singleton(double.MaxValue), Interval<double>.Singleton(double.MaxValue).Reflect(double.MaxValue));
        Assert.Equal(new Interval<double>(0, double.MaxValue), new Interval<double>(-double.MaxValue, double.MaxValue).Scale(0.5, double.MaxValue));
    }

    [Fact]
    public void UnboundedTransformsHandleZeroScaleAndReversedRays()
    {
        Assert.Equal(Interval.Entire<double>(), Interval.Entire<double>().Translate(3));
        Assert.Equal(Interval.AtMost(-2d), Interval.AtLeast(2d).Reflect());
        Assert.Equal(Interval.AtMost(-4d), Interval.AtLeast(2d).Scale(-2));
        Assert.Equal(Interval.AtMost(4d), Interval.AtLeast(2d).Reflect(3));
        Assert.Equal(default, Interval.Entire<double>().Scale(0));
        Assert.Equal(Interval<double>.Singleton(4), Interval.Entire<double>().Scale(0, 4));
        Assert.Equal(Interval.Entire<double>(), Interval.Entire<double>().Scale(-2, 4));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void AffineTransformParametersMustBeFinite(double value)
    {
        var interval = new Interval<double>(2, 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => interval.Translate(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => interval.Reflect(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => interval.Scale(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => interval.Scale(2, value));
    }

    [Fact]
    public void IncludeExpandsEitherSideAndKeepsContainedValues()
    {
        var interval = new Interval<double>(2, 5);
        Assert.Equal(new Interval<double>(-1, 5), interval.Include(-1));
        Assert.Equal(new Interval<double>(2, 8), interval.Include(8));
        Assert.Equal(interval, interval.Include(3));
        Assert.Equal(interval.Include(8), Interval<double>.From(new EndpointsOnly<double>(2, 5)).Include(8d));
        Assert.Equal(Interval.AtMost(5d), interval.Include(double.NegativeInfinity));
        Assert.Equal(Interval.AtLeast(2d), interval.Include(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => interval.Include(double.NaN));
    }

    [Fact]
    public void CheckedIntegralArithmeticRejectsOverflowAndUnsignedPenetration()
    {
        Assert.Throws<OverflowException>(() => Interval<int>.Singleton(int.MinValue).Distance(Interval<int>.Singleton(int.MaxValue)));
        Assert.Throws<OverflowException>(() => Interval<int>.Singleton(int.MaxValue).Translate(1));
        Assert.Throws<OverflowException>(() => Interval<int>.Singleton(int.MinValue).Reflect());
        Assert.Throws<OverflowException>(() => new Interval<int>(0, int.MaxValue).Scale(2));
        Assert.Equal(3u, new Interval<uint>(0, 1).Distance(new Interval<uint>(4, 5)));
        Assert.Equal(5u, new Interval<uint>(0, 1).MaxDistance(new Interval<uint>(4, 5)));
        Assert.Throws<OverflowException>(() => new Interval<uint>(0, 2).SignedDistance(new Interval<uint>(1, 3)));
        Assert.Equal(new Interval<uint>(3, 6), new Interval<uint>(2, 5).Reflect(4));
        Assert.Equal(new Interval<uint>(0, 6), new Interval<uint>(2, 5).Scale(2, 4));
    }

    [Fact]
    public void ToStringShowsTheClosedEndpoints()
    {
        Assert.Equal("[-1.5, 3.25]", new Interval<double>(-1.5, 3.25).ToString());
        Assert.Equal("[4, 4]", Interval<int>.Singleton(4).ToString());
    }

    private readonly record struct EndpointsOnly<T>(T Min, T Max) : IInterval<T> where T : INumber<T>;
}
