using App2d.Core.Constraints;
using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Tests.Constraints;

public sealed class ConstraintMath2DTests
{
    [Fact]
    public void DistanceProjectionHandlesUpperAndCoincidentLowerLimits()
    {
        Assert.Equal(
            new Vector2(5f, 0f),
            ConstraintMath2D.ProjectPointToDistance(
                Vector2.Zero,
                new Vector2(10f, 0f),
                Interval.AtMost(5f)));

        Assert.Equal(
            new Vector2(3f, 0f),
            ConstraintMath2D.ProjectPointToDistance(
                Vector2.Zero,
                Vector2.Zero,
                Interval.AtLeast(3f)));
    }

    [Fact]
    public void AxisProjectionRemovesPerpendicularMotionAndClampsTranslation()
    {
        var projected = ConstraintMath2D.ProjectPointToAxis(
            new Vector2(2f, 3f),
            new Vector2(20f, 12f),
            Vector2.UnitX,
            new Interval<float>(-4f, 6f));

        Assert.Equal(new Vector2(8f, 3f), projected);
    }

    [Fact]
    public void PolarProjectionKeepsRadiusAndClampsAngle()
    {
        var projected = ConstraintMath2D.ProjectPointToPolarArc(
            Vector2.Zero,
            Vector2.UnitY * 10f,
            5f,
            new Interval<float>(-0.5f, 0.5f));

        Assert.Equal(5f, projected.Length(), 5);
        Assert.Equal(0.5f, MathF.Atan2(projected.Y, projected.X), 5);
    }
}

