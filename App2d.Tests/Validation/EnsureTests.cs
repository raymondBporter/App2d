using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Tests.Validation;

public sealed class EnsureTests
{
    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void EveryFiniteScalarGuardRejectsNonfiniteInput(float value)
    {
        Action<float>[] guards =
        [
            v => Ensure.Finite(v, "input"),
            v => Ensure.NonZero(v, "input"),
            v => Ensure.Positive(v, "input"),
            v => Ensure.Negative(v, "input"),
            v => Ensure.NonNegative(v, "input"),
            v => Ensure.GreaterThanOrEqual(v, 0f, "input"),
            v => Ensure.GreaterThan(v, 0f, "input"),
            v => Ensure.LessThanOrEqual(v, 1f, "input"),
            v => Ensure.LessThan(v, 1f, "input"),
            v => Ensure.InRange(v, 0f, 1f, "input"),
            v => Ensure.InRange(v, 0f, 1f, "input", bounds: RangeBounds.Open)
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
        Ensure.Positive(1);
        Ensure.Positive(long.MaxValue);
        Ensure.Positive(double.MaxValue);
        Ensure.Positive(decimal.MaxValue);
        Ensure.Positive(float.Epsilon);
        Ensure.GreaterThanOrEqual(long.MaxValue, long.MaxValue - 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqual(long.MaxValue, long.MaxValue - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(-1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(int.MinValue));
    }

    [Fact]
    public void AllNumericGuardsAreFiniteByDefaultAndNumberAllowsInfinity()
    {
        Ensure.Number(float.PositiveInfinity);
        Ensure.Number(float.NegativeInfinity);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Number(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqual(float.NaN, 1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqual(float.NegativeInfinity, float.NegativeInfinity));
    }

    [Fact]
    public void ComparisonGuardsRespectStrictAndInclusiveBoundaries()
    {
        Ensure.GreaterThanOrEqual(3, 3);
        Ensure.LessThanOrEqual(3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqual(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqual(4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThan(3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThan(3, 3));
        Ensure.InRange(3, 3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(3, 3, 4, bounds: RangeBounds.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(4, 3, 4, bounds: RangeBounds.Open));
        Ensure.InRange(.5f, 0f, 1f, bounds: RangeBounds.Open);
        Ensure.InRange(0f, 0f, 1f);
        Ensure.InRange(1f, 0f, 1f);
    }

    [Fact]
    public void ZeroChecksIncludeNegativeZeroButNonnegativeGuardsAcceptIt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(-0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonZero(-0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(0f));
        Ensure.NonNegative(-0f);
        Ensure.NonZero(-1f);
    }

    [Fact]
    public void InvalidBoundsHaveTheirOwnCallerNameAndActualValue()
    {
        var maximum = float.NaN;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.LessThanOrEqual(1f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        Assert.True(float.IsNaN(Assert.IsType<float>(error.ActualValue)));
        const float minimum = float.NegativeInfinity;
        error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqual(1f, minimum));
        Assert.Equal(nameof(minimum), error.ParamName);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(1f, 0f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        maximum = -1f;
        error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(0f, 1f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
    }

    [Fact]
    public void CallerExpressionsAndExplicitPropertyNamesReachTheException()
    {
        const float radius = -3f;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(radius));
        Assert.Equal(nameof(radius), error.ParamName);
        Assert.Equal(radius, error.ActualValue);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(radius, "Radius"));
        Assert.Equal("Radius", error.ParamName);
        Assert.Contains("positive", error.Message);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.GreaterThanOrEqual(radius, 1f));
        Assert.Equal(nameof(radius), error.ParamName);
    }

    [Fact]
    public void RayLimitsAllowInfinityAndFiniteGuardsRejectIt()
    {
        Ensure.NonNegative(float.PositiveInfinity, allowInfinity: true);
        Ensure.NonNegative(0f, allowInfinity: true);
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(float.NaN, allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(float.NegativeInfinity, allowInfinity: true));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.NonNegative(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Positive(float.PositiveInfinity));
    }

    [Fact]
    public void NegativeGuardRequiresStrictlyNegativeValuesWithExplicitInfinityOptIn()
    {
        Assert.Equal(int.MinValue, Ensure.Negative(int.MinValue));
        Assert.Equal(-double.Epsilon, Ensure.Negative(-double.Epsilon));
        Assert.Equal(decimal.MinValue, Ensure.Negative(decimal.MinValue));
        Assert.Equal(float.NegativeInfinity, Ensure.Negative(float.NegativeInfinity, allowNegativeInfinity: true));

        foreach (var value in new[] { -0f, 0f, 1f, float.NaN, float.PositiveInfinity })
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Negative(value));
            Assert.Equal(nameof(value), error.ParamName);
            Assert.Equal(value, Assert.IsType<float>(error.ActualValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Negative(value, allowNegativeInfinity: true));
        }
    }

    [Fact]
    public void NullTextCollectionsAndStateKeepTheirExceptionContracts()
    {
        string? text = null;
        Assert.Equal(nameof(text), Assert.Throws<ArgumentNullException>(() => Ensure.ThrowIfNull(text)).ParamName);
        Assert.Throws<ArgumentNullException>(() => Ensure.RequireNotNull(text));
        Assert.Throws<ArgumentNullException>(() => Ensure.RequireNotNullOrWhiteSpace(text));
        text = " ";
        Assert.Equal(nameof(text), Assert.Throws<ArgumentException>(() => Ensure.RequireNotNullOrWhiteSpace(text)).ParamName);
        text = "valid";
        Ensure.ThrowIfNullOrWhiteSpace(text);
        Assert.Equal(5, text.Length); // The guard preserves nullable flow analysis.
        Assert.Throws<ArgumentException>(() => Ensure.ThrowIfContainsNull(["first", null]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.ThrowIfTooShort<int>([], 1));
        Assert.Throws<ArgumentException>(() => Ensure.ThrowIfNot(false));
        Assert.NotSame(Ensure.CreateInvalid("bad"), Ensure.CreateInvalid("bad"));
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
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(-1, 0, 2, bounds: bounds));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(3, 0, 2, bounds: bounds));

        void Check(int value, bool valid)
        {
            if (valid)
            {
                Assert.Equal(value, Ensure.InRange(value, 0, 2, bounds: bounds));
                Assert.Equal(value, Ensure.InRange(value, 0d, 2d, bounds: bounds));
            }
            else
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(value, 0, 2, bounds: bounds));
                Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(value, 0d, 2d, bounds: bounds));
            }
        }
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void DoubleGuardsRejectNonfiniteValuesWithoutLosingPrecision(double value)
    {
        Action<double>[] guards = [v => Ensure.Finite(v), v => Ensure.Positive(v), v => Ensure.NonNegative(v),
            v => Ensure.NonZero(v), v => Ensure.GreaterThanOrEqual(v, 0d), v => Ensure.GreaterThan(v, 0d),
            v => Ensure.LessThanOrEqual(v, 1d), v => Ensure.LessThan(v, 1d), v => Ensure.InRange(v, 0d, 1d)];
        foreach (var guard in guards)
            Assert.Equal(value, Assert.IsType<double>(Assert.Throws<ArgumentOutOfRangeException>(() => guard(value)).ActualValue));
    }

    [Fact]
    public void InfiniteRangeBoundsDoNotAllowInfiniteValues()
    {
        Assert.Equal(double.MaxValue, Ensure.InRange(double.MaxValue, 0d, double.PositiveInfinity));
        Assert.Equal(double.Epsilon, Ensure.InRange(double.Epsilon, 0d, double.PositiveInfinity, bounds: RangeBounds.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(double.PositiveInfinity, 0d, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(0d, 0d, double.PositiveInfinity, bounds: RangeBounds.Open));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(0, 0, 1, bounds: (RangeBounds)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.InRange(0, 0, 0, bounds: RangeBounds.ClosedOpen));
    }

    [Theory]
    [InlineData(-1, 2)]
    [InlineData(2, 2)]
    [InlineData(0, 0)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void IndexChecksReportTheIndexEvenForEmptyCollections(int index, int count)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Index(index, count));
        Assert.Equal(nameof(index), error.ParamName);
        Assert.Equal(index, error.ActualValue);
    }

    [Fact]
    public void CustomConditionsCaptureExpressionsAndAllowMessageOverrides()
    {
        const int count = 0;
        var stateError = Assert.Throws<ArgumentException>(() => Ensure.ThrowIfNot(count > 0));
        Assert.Contains("count > 0", stateError.Message);
        Assert.Contains("count == 0", Assert.Throws<ArgumentException>(() => Ensure.ThrowIf(count == 0)).Message);
        var argumentError = Assert.Throws<ArgumentException>(() => Ensure.ThrowIfNot(count > 0, paramName: nameof(count)));
        Assert.Equal(nameof(count), argumentError.ParamName);
        Assert.Contains("count > 0", argumentError.Message);
        Assert.Contains("count == 0", Assert.Throws<ArgumentException>(() => Ensure.ThrowIf(count == 0)).Message);
        Assert.Equal("Explain the domain rule.", Assert.Throws<ArgumentException>(() => Ensure.ThrowIfNot(false, "Explain the domain rule.")).Message);
        Ensure.ThrowIfNot(count == 0);
        Ensure.ThrowIfNot(count == 0);
        Assert.Equal(0, Ensure.Index(0, 1));
        Assert.Equal(int.MaxValue - 1, Ensure.Index(int.MaxValue - 1, int.MaxValue));
        const int invalidCount = -1;
        Assert.Equal(nameof(invalidCount), Assert.Throws<ArgumentOutOfRangeException>(() => Ensure.Index(0, invalidCount)).ParamName);
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
            Ensure.Positive(1f);
            Ensure.NonZero(1d);
            Ensure.GreaterThanOrEqual(2L, 1L);
            Ensure.InRange(.5f, 0f, 1f);
            Ensure.Positive(new Vector2(1));
            Ensure.NonZero(Vector3.UnitX);
            Ensure.LessThan(new Vector4(1), new Vector4(2));
        }
    }
}
