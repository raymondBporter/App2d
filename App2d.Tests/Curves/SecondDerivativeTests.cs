using App2d.Core.Geometry.Curves;

namespace App2d.Tests.Curves;

/// <summary>Second derivatives are analytic wherever they are well defined, and replace estimates that needed them.</summary>
public sealed class SecondDerivativeTests
{
    private static readonly LineSegmentCurve2D Line = LineSegmentCurve2D.Create(new(0, 0), new(10, 0));

    [Fact]
    public void GenericBeziersHaveExactSecondDerivatives()
    {
        Assert.Equal(3d, new CubicBezier<double, double>(0d, 0d, 0d, 1d).SecondDerivative(.5d));
        Assert.Equal(2d, new QuadraticBezier<double, double>(0d, 0d, 1d).SecondDerivative(.3d));
    }

    [Fact]
    public void TwoDimensionalBeziersMatchTheGenericOnes()
    {
        var cubic = CubicBezier2D.Builder.Create(new(1, 2), new(3, 8), new(7, 5), new(9, -3));
        var generic = new CubicBezier<Vector2f, float>(cubic.Start, cubic.Control1, cubic.Control2, cubic.End);
        var parabola = QuadraticBezier2D.Builder.Create(new(-1, 1), new(0, -1), new(1, 1));

        Assert.Equal(generic.SecondDerivative(.3f), cubic.SecondDerivative(.3f));
        Assert.Equal(new Vector2f(0, 8), parabola.SecondDerivative(.7f));
    }

    [Fact]
    public void StraightPiecesHaveNone()
    {
        var polyline = PolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1)]);

        Assert.Equal(Vector2f.Zero, Line.SecondDerivative(.4f));
        Assert.Equal(Vector2f.Zero, polyline.SecondDerivative(.4f));
        Assert.Equal(Vector2f.Zero, BSpline2D.Builder.Create([new(0, 0), new(2, 4)], 1).SecondDerivative(.4f));
    }

    [Fact]
    public void ArcAcceleratesTowardItsCentre()
    {
        var arc = Arc2D.Builder.Create(new(5, 5), 2, 0, MathF.PI / 2);

        var acceleration = arc.SecondDerivative(0);

        Assert.Equal(-2f * (MathF.PI / 2) * (MathF.PI / 2), acceleration.X, 4);
        Assert.Equal(0f, acceleration.Y, 4);
    }

    [Fact]
    public void DegreeTwoBSplineOfThreePointsIsItsQuadraticBezier()
    {
        var spline = BSpline2D.Builder.Create([new(-1, 1), new(0, -1), new(1, 1)], 2);

        var acceleration = spline.SecondDerivative(.25f);

        Assert.Equal(0f, acceleration.X, 4);
        Assert.Equal(8f, acceleration.Y, 4);
    }

    [Fact]
    public void RoundedPolylineBendsOnlyInItsCorners()
    {
        var rounded = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(10, 0), new(10, 10)]);

        Assert.Equal(Vector2f.Zero, rounded.SecondDerivative(.05f));
        Assert.True(rounded.SecondDerivative(.5f).Length() > 1f);
    }

    [Fact]
    public void TangentAtAStationaryStartFollowsTheSecondDerivative()
    {
        var curve = CubicBezier2D.Builder.Create(new(0, 0), new(0, 0), new(1, 1), new(2, 0));

        var tangent = Curve2D.Tangent(curve, 0);

        Assert.Equal(MathF.Sqrt(.5f), tangent.X, 6);
        Assert.Equal(MathF.Sqrt(.5f), tangent.Y, 6);
    }

    [Fact]
    public void TangentAtAStationaryEndStillPointsForward()
    {
        var curve = CubicBezier2D.Builder.Create(new(0, 0), new(1, 1), new(2, 0), new(2, 0));

        var tangent = Curve2D.Tangent(curve, 1);

        Assert.Equal(MathF.Sqrt(.5f), tangent.X, 6);
        Assert.Equal(-MathF.Sqrt(.5f), tangent.Y, 6);
    }

    [Fact]
    public void TangentJustBeforeAStationaryEndStillPointsForward()
    {
        var curve = CubicBezier2D.Builder.Create(new(0, 0), new(1, 1), new(2, 0), new(2, 0));

        var tangent = Curve2D.Tangent(curve, 0.99999994f);

        Assert.Equal(MathF.Sqrt(.5f), tangent.X, 3);
        Assert.Equal(-MathF.Sqrt(.5f), tangent.Y, 3);
    }

    [Fact]
    public void OffsetAtAStationaryStartStillTurnsWithTheSource()
    {
        var curve = CubicBezier2D.Builder.Create(new(0, 0), new(0, 0), new(1, 1), new(2, 0));
        var offset = new NormalOffsetCurve2D(curve, ProgressMapf.Constant(1f));

        var atRest = offset.Derivative(0);
        var beside = offset.Derivative(.001f);

        // The tangent turns at (.3536, -.3536) here, so the unit offset moves at its perpendicular, (.3536, .3536).
        Assert.Equal(.3536f, atRest.X, 3);
        Assert.Equal(.3536f, atRest.Y, 3);
        Assert.InRange((atRest - beside).Length(), 0, .02f);
    }

    [Fact]
    public void OffsetOfALineByALinearProfileIsExact()
    {
        var offset = new NormalOffsetCurve2D(Line, ProgressMapf.Forward);

        Assert.Equal(new Vector2f(3, .3f), offset.Evaluate(.3f));
        Assert.Equal(new Vector2f(10, 1), offset.Derivative(.3f));
    }

    [Fact]
    public void OffsetOfAnArcByAConstantScalesItsTangent()
    {
        var arc = Arc2D.Builder.Create(Vector2f.Zero, 2, 0, MathF.PI / 2);
        var inner = new NormalOffsetCurve2D(arc, ProgressMapf.Constant(1f));

        var expected = arc.Derivative(.3f) * .5f;
        var actual = inner.Derivative(.3f);

        Assert.Equal(expected.X, actual.X, 4);
        Assert.Equal(expected.Y, actual.Y, 4);
    }

    [Fact]
    public void OffsetOverAStationarySourceStaysFinite()
    {
        var curve = CubicBezier2D.Builder.Create(new(0, 0), new(0, 0), new(1, 1), new(2, 0));

        var derivative = new NormalOffsetCurve2D(curve, ProgressMapf.Forward).Derivative(0);

        Assert.True(float.IsFinite(derivative.X) && float.IsFinite(derivative.Y));
    }

    [Fact]
    public void OffsetOfACurveWithoutASecondDerivativeCannotBeDifferentiated()
    {
        var once = new NormalOffsetCurve2D(Line, ProgressMapf.Constant(1f));
        var twice = new NormalOffsetCurve2D(once, ProgressMapf.Constant(1f));

        Assert.Equal(new Vector2f(5, 2), twice.Evaluate(.5f));
        Assert.Throws<NotSupportedException>(() => twice.Derivative(.5f));
    }

    [Fact]
    public void GaussianProfileHasItsAnalyticSlope()
    {
        var bump = Easingd.Gaussian(.5d, .1d);
        const double step = 1e-6d;

        Assert.Equal(1d, bump.Evaluate(.5d));
        Assert.Equal(0d, bump.Derivative(.5d));
        Assert.Equal((bump.Evaluate(.4d + step) - bump.Evaluate(.4d - step)) / (2 * step), bump.Derivative(.4d), 5);
        Assert.Throws<ArgumentOutOfRangeException>(() => Easingd.Gaussian(.5d, 0d));
    }
}
