using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;

namespace App2d.Tests.Constraints;

public sealed class ConstraintMath1DTests
{
    [Fact]
    public void LimitEvaluationReportsSideTargetAndSignedError()
    {
        var limits = new Interval<float>(2f, 8f);

        var below = ConstraintMath1D.EvaluateLimit(limits, 1f);
        Assert.Equal(ConstraintLimitState1D.Lower, below.State);
        Assert.Equal(2f, below.Target);
        Assert.Equal(-1f, below.Error);

        var inside = ConstraintMath1D.EvaluateLimit(limits, 5f);
        Assert.Equal(ConstraintLimitState1D.Inactive, inside.State);
        Assert.True(inside.IsSatisfied);

        var above = ConstraintMath1D.EvaluateLimit(limits, 11f);
        Assert.Equal(ConstraintLimitState1D.Upper, above.State);
        Assert.Equal(8f, above.Target);
        Assert.Equal(3f, above.Error);
    }

    [Fact]
    public void ActivationToleranceMarksApproachingLimitWithoutInventingError()
    {
        var evaluation = ConstraintMath1D.EvaluateLimit(Interval.AtMost(10f), 9.95f, 0.1f);

        Assert.Equal(ConstraintLimitState1D.Upper, evaluation.State);
        Assert.Equal(0f, evaluation.Error);
        Assert.Equal(9.95f, evaluation.Target);
    }

    [Fact]
    public void LimitEvaluationHandlesEntireSingletonAndExactBoundaries()
    {
        var entire = ConstraintMath1D.EvaluateLimit(Interval.Entire<float>(), 42f);
        Assert.Equal(ConstraintLimitState1D.Inactive, entire.State);
        Assert.False(entire.IsActive);
        Assert.True(entire.IsSatisfied);
        Assert.Equal(42f, entire.Target);

        var locked = ConstraintMath1D.EvaluateLimit(Interval<float>.Singleton(4f), 6f);
        Assert.Equal(ConstraintLimitState1D.Locked, locked.State);
        Assert.True(locked.IsActive);
        Assert.False(locked.IsSatisfied);
        Assert.Equal(4f, locked.Target);
        Assert.Equal(2f, locked.Error);
        Assert.Equal(-2f, locked.Correction);
        Assert.Equal(ConstraintLimitState1D.Locked, ConstraintMath1D.EvaluateLimit(default, 0f).State);

        var limits = new Interval<float>(2f, 8f);
        var lower = ConstraintMath1D.EvaluateLimit(limits, 2f);
        var upper = ConstraintMath1D.EvaluateLimit(limits, 8f);
        Assert.Equal(ConstraintLimitState1D.Lower, lower.State);
        Assert.True(lower.IsSatisfied);
        Assert.Equal(ConstraintLimitState1D.Upper, upper.State);
        Assert.True(upper.IsSatisfied);

        var approachingLower = ConstraintMath1D.EvaluateLimit(limits, 2.05f, 0.1f);
        Assert.Equal(ConstraintLimitState1D.Lower, approachingLower.State);
        Assert.Equal(2.05f, approachingLower.Target);
        Assert.Equal(0f, approachingLower.Error);
        Assert.Equal(ConstraintLimitState1D.Lower, ConstraintMath1D.EvaluateLimit(limits, 5f, 4f).State);
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(float.PositiveInfinity, 0f)]
    [InlineData(float.NegativeInfinity, 0f)]
    [InlineData(0f, -1f)]
    [InlineData(0f, float.NaN)]
    [InlineData(0f, float.PositiveInfinity)]
    public void LimitEvaluationRequiresFiniteCoordinateAndNonnegativeFiniteTolerance(float coordinate, float tolerance)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConstraintMath1D.EvaluateLimit(Interval.Entire<float>(), coordinate, tolerance));
    }
}
