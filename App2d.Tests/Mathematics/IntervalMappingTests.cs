namespace App2d.Tests.Mathematics;

public sealed class IntervalMappingTests
{
    [Theory]
    [InlineData(12.5d, 10d, 20d, -4d, 8d, -1d)]
    [InlineData(17.5d, 20d, 10d, 8d, -4d, 5d)]
    [InlineData(5d, 10d, 20d, 0d, 100d, -50d)]
    [InlineData(25d, 10d, 20d, 0d, 100d, 150d)]
    public void DirectRemappingPreservesDirectedEndpointsAndExtrapolation(double value, double begin, double end, double targetBegin, double targetEnd, double expected)
    {
        Assert.Equal(expected, Interval.Remap(value, begin, end, targetBegin, targetEnd));
    }

    [Fact]
    public void ClampingSupportsReversedAndCollapsedSpansWithoutAnIntervalValue()
    {
        Assert.Equal(.25f, Interval.InverseLerp(20f, 10f, 17.5f));
        Assert.Equal(17.5f, Interval.Lerp(20f, 10f, .25f));
        Assert.Equal(0f, Interval.InverseLerpClamped(20f, 10f, 25f));
        Assert.Equal(1f, Interval.InverseLerpClamped(20f, 10f, 5f));
        Assert.Equal(8f, Interval.RemapClamped(25f, 20f, 10f, 8f, -4f));
        Assert.Equal(-4f, Interval.RemapClamped(5f, 20f, 10f, 8f, -4f));
        Assert.Equal(0f, Interval.InverseLerp(3f, 3f, 100f));
        Assert.Equal(10f, Interval.Remap(100f, 3f, 3f, 10f, 20f));
        Assert.Equal(7f, Interval.Lerp(7f, 7f, float.NaN));
    }

    [Fact]
    public void DirectedHelpersRetainNumericPrecisionAndIntegerArithmetic()
    {
        Assert.Equal(1.000000000025d, Interval.Lerp(1d, 1.0000000001d, .25d));
        Assert.Equal(30, Interval.Lerp(10, 20, 2));
        Assert.Equal(2, Interval.InverseLerp(10, 20, 30));
        Assert.Throws<OverflowException>(() => Interval.Lerp(int.MaxValue - 2, int.MaxValue, 2));
        Assert.Equal(0d, Interval.Lerp(-double.MaxValue, double.MaxValue, .5d));
        Assert.Equal(0d, Interval.Lerp(double.MaxValue, -double.MaxValue, .5d));
        Assert.Equal(.5d, Interval.InverseLerp(-double.MaxValue, double.MaxValue, 0d));
        Assert.Equal(.5d, Interval.InverseLerp(double.MaxValue, -double.MaxValue, 0d));
    }
}
