using App2d.Core.Constraints;
using System.Numerics;

namespace App2d.Tests.Constraints;

public sealed class ConstraintMath2DTests
{
    [Fact]
    public void LimitFactoriesRepresentFreeOneSidedBoundedAndLockedCoordinates()
    {
        Assert.True(ConstraintLimit1D.Free.IsFree);

        var lower = ConstraintLimit1D.AtLeast(2f);
        Assert.True(lower.HasMinimum);
        Assert.False(lower.HasMaximum);
        Assert.Equal(2f, lower.Clamp(-1f));

        var upper = ConstraintLimit1D.AtMost(8f);
        Assert.False(upper.HasMinimum);
        Assert.True(upper.HasMaximum);
        Assert.Equal(8f, upper.Clamp(12f));

        var bounded = ConstraintLimit1D.Between(2f, 8f);
        Assert.Equal(5f, bounded.Clamp(5f));

        var locked = ConstraintLimit1D.Locked(4f);
        Assert.True(locked.IsLocked);
        Assert.Equal(4f, locked.Clamp(100f));
    }

    [Fact]
    public void LimitEvaluationReportsSideTargetAndSignedError()
    {
        var limits = ConstraintLimit1D.Between(2f, 8f);

        var below = limits.Evaluate(1f);
        Assert.Equal(ConstraintLimitState1D.Lower, below.State);
        Assert.Equal(2f, below.Target);
        Assert.Equal(-1f, below.Error);

        var inside = limits.Evaluate(5f);
        Assert.Equal(ConstraintLimitState1D.Inactive, inside.State);
        Assert.True(inside.IsSatisfied);

        var above = limits.Evaluate(11f);
        Assert.Equal(ConstraintLimitState1D.Upper, above.State);
        Assert.Equal(8f, above.Target);
        Assert.Equal(3f, above.Error);
    }

    [Fact]
    public void ActivationToleranceMarksApproachingLimitWithoutInventingError()
    {
        var evaluation = ConstraintLimit1D.AtMost(10f).Evaluate(9.95f, 0.1f);

        Assert.Equal(ConstraintLimitState1D.Upper, evaluation.State);
        Assert.Equal(0f, evaluation.Error);
        Assert.Equal(9.95f, evaluation.Target);
    }

    [Fact]
    public void DistanceProjectionHandlesUpperAndCoincidentLowerLimits()
    {
        Assert.Equal(
            new Vector2(5f, 0f),
            ConstraintMath2D.ProjectPointToDistance(
                Vector2.Zero,
                new Vector2(10f, 0f),
                ConstraintLimit1D.AtMost(5f)));

        Assert.Equal(
            new Vector2(3f, 0f),
            ConstraintMath2D.ProjectPointToDistance(
                Vector2.Zero,
                Vector2.Zero,
                ConstraintLimit1D.AtLeast(3f)));
    }

    [Fact]
    public void AxisProjectionRemovesPerpendicularMotionAndClampsTranslation()
    {
        var projected = ConstraintMath2D.ProjectPointToAxis(
            new Vector2(2f, 3f),
            new Vector2(20f, 12f),
            Vector2.UnitX,
            ConstraintLimit1D.Between(-4f, 6f));

        Assert.Equal(new Vector2(8f, 3f), projected);
    }

    [Fact]
    public void PolarProjectionKeepsRadiusAndClampsAngle()
    {
        var projected = ConstraintMath2D.ProjectPointToPolarArc(
            Vector2.Zero,
            Vector2.UnitY * 10f,
            5f,
            ConstraintLimit1D.Between(-0.5f, 0.5f));

        Assert.Equal(5f, projected.Length(), 5);
        Assert.Equal(0.5f, MathF.Atan2(projected.Y, projected.X), 5);
    }
}

