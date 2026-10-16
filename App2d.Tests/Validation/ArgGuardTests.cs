using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Tests.Validation;

public sealed class ArgGuardTests
{
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void EveryFiniteScalarGuardRejectsNonfiniteInput(float value)
    {
        Action<float>[] guards =
        [
            v => ArgGuard.Finite(v, "input"),
            v => ArgGuard.NonZero(v, "input"),
            v => ArgGuard.Positive(v, "input"),
            v => ArgGuard.NonNegative(v, "input"),
            v => ArgGuard.AtLeast(v, 0f, "input"),
            v => ArgGuard.GreaterThan(v, 0f, "input"),
            v => ArgGuard.AtMost(v, 1f, "input"),
            v => ArgGuard.LessThan(v, 1f, "input"),
            v => ArgGuard.InRange(v, 0f, 1f, "input"),
            v => ArgGuard.InRange(v, 0f, 1f, "input", bounds: RangeBounds.Open)
        ];
        foreach (var guard in guards)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => guard(value));
            Assert.Equal("input", error.ParamName);
            Assert.Equal(value, Assert.IsType<float>(error.ActualValue));
        }
    }

    [Fact]
    public void NumericTypesShareTheSameConditionsWithoutConvertingToFloat()
    {
        ArgGuard.Positive(1);
        ArgGuard.Positive(long.MaxValue);
        ArgGuard.Positive(double.MaxValue);
        ArgGuard.Positive(decimal.MaxValue);
        ArgGuard.Positive(float.Epsilon);
        ArgGuard.AtLeast(long.MaxValue, long.MaxValue - 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtMost(long.MaxValue, long.MaxValue - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(-1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(int.MinValue));
    }

    [Fact]
    public void AllNumericGuardsAreFiniteByDefaultAndNumberAllowsInfinity()
    {
        ArgGuard.Number(float.PositiveInfinity);
        ArgGuard.Number(float.NegativeInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Number(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtMost(float.NaN, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtLeast(float.NegativeInfinity, float.NegativeInfinity));
    }

    [Fact]
    public void ComparisonGuardsRespectStrictAndInclusiveBoundaries()
    {
        ArgGuard.AtLeast(3, 3);
        ArgGuard.AtMost(3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtLeast(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtMost(4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.GreaterThan(3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.LessThan(3, 3));
        ArgGuard.InRange(3, 3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(3, 3, 4, bounds: RangeBounds.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(4, 3, 4, bounds: RangeBounds.Open));
        ArgGuard.InRange(.5f, 0f, 1f, bounds: RangeBounds.Open);
        ArgGuard.InRange(0f, 0f, 1f);
        ArgGuard.InRange(1f, 0f, 1f);
    }

    [Fact]
    public void ZeroChecksIncludeNegativeZeroButNonnegativeGuardsAcceptIt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(-0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonZero(-0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(0f));
        ArgGuard.NonNegative(-0f);
        ArgGuard.NonZero(-1f);
    }

    [Fact]
    public void InvalidBoundsHaveTheirOwnCallerNameAndActualValue()
    {
        var maximum = float.NaN;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtMost(1f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        Assert.True(float.IsNaN(Assert.IsType<float>(error.ActualValue)));
        const float minimum = float.NegativeInfinity;
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtLeast(1f, minimum));
        Assert.Equal(nameof(minimum), error.ParamName);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(1f, 0f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        maximum = -1f;
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(0f, 1f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
    }

    [Fact]
    public void CallerExpressionsAndExplicitPropertyNamesReachTheException()
    {
        const float radius = -3f;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(radius));
        Assert.Equal(nameof(radius), error.ParamName);
        Assert.Equal(radius, error.ActualValue);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(radius, "Radius"));
        Assert.Equal("Radius", error.ParamName);
        Assert.Contains("positive", error.Message);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.AtLeast(radius, 1f));
        Assert.Equal(nameof(radius), error.ParamName);
        Assert.Contains("at least 1", error.Message);
    }

    [Fact]
    public void RayLimitsAllowInfinityAndFiniteGuardsRejectIt()
    {
        ArgGuard.NonNegative(float.PositiveInfinity, allowInfinity: true);
        ArgGuard.NonNegative(0f, allowInfinity: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(float.NaN, allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(float.NegativeInfinity, allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.NonNegative(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Positive(float.PositiveInfinity));
    }

    [Fact]
    public void NullTextCollectionsAndStateKeepTheirExceptionContracts()
    {
        string? text = null;
        Assert.Equal(nameof(text), Assert.Throws<ArgumentNullException>(() => ArgGuard.ThrowIfNull(text)).ParamName);
        Assert.Throws<ArgumentNullException>(() => ArgGuard.RequireNotNull(text));
        Assert.Throws<ArgumentNullException>(() => ArgGuard.RequireNotNullOrWhiteSpace(text));
        text = " ";
        Assert.Equal(nameof(text), Assert.Throws<ArgumentException>(() => ArgGuard.RequireNotNullOrWhiteSpace(text)).ParamName);
        text = "valid";
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        Assert.Equal(5, text.Length); // The guard preserves nullable flow analysis.
        Assert.Throws<ArgumentException>(() => ArgGuard.ThrowIfContainsNull(["first", null]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfTooShort<int>([], 1));
        Assert.Throws<InvalidOperationException>(() => StateGuard.ThrowIfNot(false));
        Assert.NotSame(ArgGuard.CreateInvalid("bad"), ArgGuard.CreateInvalid("bad"));
    }

    [Theory]
    [InlineData(RangeBounds.Closed, true, true)]
    [InlineData(RangeBounds.Open, false, false)]
    [InlineData(RangeBounds.ClosedOpen, true, false)]
    [InlineData(RangeBounds.OpenClosed, false, true)]
    public void RangeEndpointsAreExactForIntegersAndFloatingPoint(RangeBounds bounds, bool includesMinimum, bool includesMaximum)
    {
        Check(0, includesMinimum);
        Check(1, true);
        Check(2, includesMaximum);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(-1, 0, 2, bounds: bounds));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(3, 0, 2, bounds: bounds));

        void Check(int value, bool valid)
        {
            if (valid)
            {
                Assert.Equal(value, ArgGuard.InRange(value, 0, 2, bounds: bounds));
                Assert.Equal((double)value, ArgGuard.InRange((double)value, 0d, 2d, bounds: bounds));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(value, 0, 2, bounds: bounds));
                Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange((double)value, 0d, 2d, bounds: bounds));
            }
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DoubleGuardsRejectNonfiniteValuesWithoutLosingPrecision(double value)
    {
        Action<double>[] guards = [v => ArgGuard.Finite(v), v => ArgGuard.Positive(v), v => ArgGuard.NonNegative(v),
            v => ArgGuard.NonZero(v), v => ArgGuard.AtLeast(v, 0d), v => ArgGuard.GreaterThan(v, 0d),
            v => ArgGuard.AtMost(v, 1d), v => ArgGuard.LessThan(v, 1d), v => ArgGuard.InRange(v, 0d, 1d)];
        foreach (var guard in guards)
            Assert.Equal(value, Assert.IsType<double>(Assert.Throws<ArgumentOutOfRangeException>(() => guard(value)).ActualValue));
    }

    [Fact]
    public void InfiniteRangeBoundsDoNotAllowInfiniteValues()
    {
        Assert.Equal(double.MaxValue, ArgGuard.InRange(double.MaxValue, 0d, double.PositiveInfinity));
        Assert.Equal(double.Epsilon, ArgGuard.InRange(double.Epsilon, 0d, double.PositiveInfinity, bounds: RangeBounds.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(double.PositiveInfinity, 0d, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(0d, 0d, double.PositiveInfinity, bounds: RangeBounds.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(0, 0, 1, bounds: (RangeBounds)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.InRange(0, 0, 0, bounds: RangeBounds.ClosedOpen));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void IndexChecksReportTheIndexEvenForEmptyCollections(int index, int count)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Index(index, count));
        Assert.Equal(nameof(index), error.ParamName);
        Assert.Equal(index, error.ActualValue);
    }

    [Fact]
    public void CustomConditionsCaptureExpressionsAndAllowMessageOverrides()
    {
        var count = 0;
        var stateError = Assert.Throws<InvalidOperationException>(() => StateGuard.ThrowIfNot(count > 0));
        Assert.Contains("count > 0", stateError.Message);
        Assert.Contains("count == 0", Assert.Throws<InvalidOperationException>(() => StateGuard.ThrowIf(count == 0)).Message);
        var argumentError = Assert.Throws<ArgumentException>(() => ArgGuard.ThrowIfNot(count > 0, paramName: nameof(count)));
        Assert.Equal(nameof(count), argumentError.ParamName);
        Assert.Contains("count > 0", argumentError.Message);
        Assert.Contains("count == 0", Assert.Throws<ArgumentException>(() => ArgGuard.ThrowIf(count == 0)).Message);
        Assert.Equal("Explain the domain rule.", Assert.Throws<InvalidOperationException>(() => StateGuard.ThrowIfNot(false, "Explain the domain rule.")).Message);
        ArgGuard.ThrowIfNot(count == 0);
        StateGuard.ThrowIfNot(count == 0);
        Assert.Equal(0, ArgGuard.Index(0, 1));
        Assert.Equal(int.MaxValue - 1, ArgGuard.Index(int.MaxValue - 1, int.MaxValue));
        var invalidCount = -1;
        Assert.Equal(nameof(invalidCount), Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.Index(0, invalidCount)).ParamName);
    }

    [Fact]
    public void SuccessfulNumericGuardsDoNotAllocate()
    {
        for (var i = 0; i < 100; i++)
        {
            Check();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            Check();
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        static void Check()
        {
            ArgGuard.Positive(1f);
            ArgGuard.NonZero(1d);
            ArgGuard.AtLeast(2L, 1L);
            ArgGuard.InRange(.5f, 0f, 1f);
            ArgGuard.Positive(new Vector2(1));
            ArgGuard.NonZero(Vector3.UnitX);
            ArgGuard.LessThan(new Vector4(1), new Vector4(2));
        }
    }
}
