using App2d.Core;
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
            v => ArgGuard.ThrowIfNotFinite(v, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrZero(v, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrNotPositive(v, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrNegative(v, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrLessThan(v, 0f, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrLessThanOrEqual(v, 0f, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrGreaterThan(v, 1f, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrGreaterThanOrEqual(v, 1f, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(v, 0f, 1f, "input"),
            v => ArgGuard.ThrowIfNotFiniteOrNotInOpenRange(v, 0f, 1f, "input")
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
        ArgGuard.ThrowIfNotFiniteOrNotPositive(1);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(long.MaxValue);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(double.MaxValue);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(decimal.MaxValue);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(float.Epsilon);
        ArgGuard.ThrowIfLessThan(long.MaxValue, long.MaxValue - 1);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfGreaterThan(long.MaxValue, long.MaxValue - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNegative(-1d));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotPositive(int.MinValue));
    }

    [Fact]
    public void FiniteVariantsAndPlainComparisonsHaveExplicitlyDifferentPolicies()
    {
        ArgGuard.ThrowIfNotPositive(float.PositiveInfinity);
        ArgGuard.ThrowIfLessThan(float.NegativeInfinity, float.NegativeInfinity);
        ArgGuard.ThrowIfGreaterThan(float.NaN, 1f); // Plain relational operators do not order NaN.
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotPositive(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotPositive(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrGreaterThan(float.NaN, 1f));
    }

    [Fact]
    public void ComparisonGuardsRespectStrictAndInclusiveBoundaries()
    {
        ArgGuard.ThrowIfLessThan(3, 3);
        ArgGuard.ThrowIfGreaterThan(3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfLessThan(2, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfGreaterThan(4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfLessThanOrEqual(3, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfGreaterThanOrEqual(3, 3));
        ArgGuard.ThrowIfNotInClosedRange(3, 3, 3);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotInOpenRange(3, 3, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotInOpenRange(4, 3, 4));
        ArgGuard.ThrowIfNotFiniteOrNotInOpenRange(.5f, 0f, 1f);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(0f, 0f, 1f);
        ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(1f, 0f, 1f);
    }

    [Fact]
    public void ZeroChecksIncludeNegativeZeroButNonnegativeGuardsAcceptIt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfZero(-0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrZero(-0d));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotPositive(0f));
        ArgGuard.ThrowIfNotFiniteOrNegative(-0f);
        ArgGuard.ThrowIfNotFiniteOrZero(-1f);
    }

    [Fact]
    public void InvalidBoundsHaveTheirOwnCallerNameAndActualValue()
    {
        var maximum = float.NaN;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrGreaterThan(1f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        Assert.True(float.IsNaN(Assert.IsType<float>(error.ActualValue)));
        var minimum = float.NegativeInfinity;
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrLessThan(1f, minimum));
        Assert.Equal(nameof(minimum), error.ParamName);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(1f, 0f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
        maximum = -1f;
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(0f, 1f, maximum));
        Assert.Equal(nameof(maximum), error.ParamName);
    }

    [Fact]
    public void CallerExpressionsAndExplicitPropertyNamesReachTheException()
    {
        var radius = -3f;
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotPositive(radius));
        Assert.Equal(nameof(radius), error.ParamName);
        Assert.Equal(radius, error.ActualValue);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotFiniteOrNotPositive(radius, "Radius"));
        Assert.Equal("Radius", error.ParamName);
        Assert.Contains("positive", error.Message);
        error = Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowLessThanException(radius, 1f));
        Assert.Equal(nameof(radius), error.ParamName);
        Assert.Contains("at least 1", error.Message);
    }

    [Fact]
    public void RayLimitsStillAllowInfinityAndCompatibilityAliasesRemainFinite()
    {
        ArgGuard.ThrowIfNegativeOrNaN(float.PositiveInfinity);
        ArgGuard.ThrowIfNegativeOrNaN(0f);
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNegativeOrNaN(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNegativeOrNaN(float.NegativeInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNegativeOrNotFinite(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfNotPositiveFiniteValue(float.PositiveInfinity));
    }

    [Fact]
    public void NullTextCollectionsAndStateKeepTheirExceptionContracts()
    {
        string? text = null;
        Assert.Equal(nameof(text), Assert.Throws<ArgumentNullException>(() => ArgGuard.ThrowIfNull(text)).ParamName);
        Assert.Throws<ArgumentNullException>(() => ArgGuard.RequireNotNull(text));
        Assert.Throws<ArgumentNullException>(() => ArgGuard.RequireNotNullOrWhiteSpace(text));
        text = " ";
        Assert.Equal(nameof(text), Assert.Throws<ArgumentException>(() => ArgGuard.RequireNotNullOrWhitespace(text)).ParamName);
        text = "valid";
        ArgGuard.ThrowIfNullOrWhiteSpace(text);
        Assert.Equal(5, text.Length); // The guard preserves nullable flow analysis.
        Assert.Throws<ArgumentException>(() => ArgGuard.ThrowIfContainsNull<string>(["first", null!]));
        Assert.Throws<ArgumentOutOfRangeException>(() => ArgGuard.ThrowIfTooShort<int>([], 1));
        Assert.Throws<InvalidOperationException>(() => StateGuard.ThrowIfNotPositive(float.PositiveInfinity));
        Assert.Throws<InvalidOperationException>(() => StateGuard.ThrowIfLessThan(0, 1));
        Assert.NotSame(ArgGuard.CreateInvalid("bad"), ArgGuard.CreateInvalid("bad"));
    }

    [Fact]
    public void SuccessfulNumericGuardsDoNotAllocate()
    {
        for (var i = 0; i < 100; i++) Check();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) Check();
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        static void Check()
        {
            ArgGuard.ThrowIfNotFiniteOrNotPositive(1f);
            ArgGuard.ThrowIfNotFiniteOrZero(1d);
            ArgGuard.ThrowIfNotFiniteOrLessThan(2L, 1L);
            ArgGuard.ThrowIfNotFiniteOrNotInClosedRange(.5f, 0f, 1f);
            ArgGuard.ThrowIfNotFiniteOrNotPositive(new Vector2(1));
            ArgGuard.ThrowIfNotFiniteOrZero(Vector3.UnitX);
            ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThan(new Vector4(1), new Vector4(2));
        }
    }
}
