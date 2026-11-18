using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;

namespace App2d.Tests.Constraints;

public sealed class ConstraintMath2DTests
{
    [Fact]
    public void DistanceProjectionHandlesUpperAndCoincidentLowerLimits()
    {
        Assert.Equal(
            new Vector2f(5f, 0f),
            ConstraintMath2D.ProjectPointToDistance(
                Vector2f.Zero,
                new Vector2f(10f, 0f),
                Interval.AtMost(5f)));

        Assert.Equal(
            new Vector2f(3f, 0f),
            ConstraintMath2D.ProjectPointToDistance(
                Vector2f.Zero,
                Vector2f.Zero,
                Interval.AtLeast(3f)));
    }

    [Fact]
    public void AxisProjectionRemovesPerpendicularMotionAndClampsTranslation()
    {
        var projected = ConstraintMath2D.ProjectPointToAxis(
            new Vector2f(2f, 3f),
            new Vector2f(20f, 12f),
            Vector2f.UnitX,
            new Interval<float>(-4f, 6f));

        Assert.Equal(new Vector2f(8f, 3f), projected);
    }

    [Fact]
    public void PolarProjectionKeepsRadiusAndClampsAngle()
    {
        var projected = ConstraintMath2D.ProjectPointToPolarArc(
            Vector2f.Zero,
            Vector2f.UnitY * 10f,
            5f,
            new Interval<float>(-0.5f, 0.5f));

        Assert.Equal(5f, projected.Length(), 5);
        Assert.Equal(0.5f, projected.AngleRadians, 5);
    }
}

