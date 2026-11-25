using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Distance;

namespace App2d.Tests.Curves;

public sealed class Curve2DTests
{
    [Fact]
    public void LineAndPolylineDistanceUseTheirFiniteSegments()
    {
        LineSegmentCurve2D line = LineSegmentCurve2D.Builder.Create(new(0, 0), new(2, 0));
        Assert.Equal(3, line.Distance(new(1, 3)), 5);
        Assert.Equal(MathF.Sqrt(10), CurveDistance2D.Distance(new(3, 3), line), 5);
        Assert.Equal(0, line.Distance(new(1, 0)), 5);

        PolylineCurve2D polyline = PolylineCurve2D.Builder.Create([new(0, 0), new(2, 0), new(2, 2)]);
        Assert.Equal(1, polyline.Distance(new(1, 1)), 5);
        Assert.Equal(1, polyline.Distance(new(3, 1)), 5);
        Assert.Equal(MathF.Sqrt(2), polyline.Distance(new(3, 3)), 5);
    }

    [Fact]
    public void SmoothCurveDistancesIncludeTheInteriorAndEndpoints()
    {
        QuadraticBezier2D quadratic = QuadraticBezier2D.Builder.Create(new(0, 0), new(1, 1), new(2, 0));
        Assert.InRange(quadratic.Distance(new(1, 1)), .49f, .51f);

        CubicBezier2D cubic = CubicBezier2D.Builder.Create(new(0, 0), new(1, 0), new(2, 0), new(3, 0));
        Assert.Equal(2, cubic.Distance(new(1, 2)), 4);
        Assert.Equal(1, cubic.Distance(new(4, 0)), 4);

        BSpline2D spline = BSpline2D.Builder.Create([new(0, 0), new(1, 0), new(2, 0)], degree: 1);
        Assert.Equal(1, spline.Distance(new(1, 1)), 4);

        NormalOffsetCurve2D offset = new NormalOffsetCurve2D(cubic, ProgressMapf.Constant(1f));
        Assert.InRange(offset.Distance(new(1.5f, 2)), .99f, 1.01f);
    }

    [Fact]
    public void QuadraticDistanceChecksBothMinimaAndHandlesRetracing()
    {
        var parabola = QuadraticBezier2D.Builder.Create(new(-1, 1), new(0, -1), new(1, 1));
        Assert.Equal(MathF.Sqrt(.75f), parabola.Distance(new(0, 1)), 6);

        var retraced = QuadraticBezier2D.Builder.Create(new(0, 0), new(2, 0), new(0, 0));
        Assert.Equal(1, retraced.Distance(new(2, 0)), 6);
        Assert.Equal(5, default(QuadraticBezier2D).Distance(new(3, 4)), 6);
    }

    [Fact]
    public void QuadraticDistanceKeepsARootNearAStationaryEndpoint()
    {
        var curve = QuadraticBezier2D.Builder.Create(new(1e20f, 0), Vector2f.Zero, Vector2f.Zero);
        var query = new Vector2f(1, 3);

        Assert.Equal(3, curve.Distance(query), 6);
        Assert.Equal(3, CurveDistance2D.Distance(query, curve, .99f, 1), 6);
    }

    [Fact]
    public void NestedSliceCanKeepASecondaryMinimumOfItsSource()
    {
        var parabola = QuadraticBezier2D.Builder.Create(new(-1, 1), new(0, -1), new(1, 1));
        var query = new Vector2f(.13671875f, 1);
        var nearest = new Vector2f(-.625f, .390625f);
        var expected = Vector2f.Distance(query, nearest);

        Assert.True(parabola.Distance(query) < expected);
        // [.25, .75] of [0, .5] is [.125, .375] of the parabola.
        Assert.Equal(expected, CurveDistance2D.Distance(query, parabola, .125f, .375f), 6);
        Assert.Equal(expected, CurveDistance2D.Distance(query, parabola, .1875f, .1875f), 6);
    }

    [Fact]
    public void QuadraticDistanceFindsAPointBetweenPolylineSamples()
    {
        var parabola = QuadraticBezier2D.Builder.Create(new(-1, 1), new(0, -1), new(1, 1));
        const float x = 2 * (49f / 128f) - 1;
        var point = new Vector2f(x, x * x);

        Assert.InRange(parabola.Distance(point), 0, 1e-7f);
        Assert.InRange(CurveDistance2D.Distance(point, parabola, .1f, .5f), 0, 1e-7f);
    }

    [Fact]
    public void RoundedPolylineDistanceFindsAFilletOnAShortSpan()
    {
        var curve = RoundedPolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1), new(10000, 1)]);
        var filletMidpoint = new Vector2f(.88f, .12f);

        Assert.InRange(curve.Distance(filletMidpoint), 0, 1e-7f);
        Assert.InRange(CurveDistance2D.Distance(filletMidpoint, curve, .00007f, .00013f), 0, 1e-7f);
        Assert.Equal(Vector2f.Distance(filletMidpoint, curve.Evaluate(.0003f)), CurveDistance2D.Distance(filletMidpoint, curve, .0003f, .0004f), 5);
    }

    [Fact]
    public void ArcAndPolylineSlicesRestrictTheNearestPoint()
    {
        var arc = Arc2D.Builder.Create(Vector2f.Zero, 2, 0, MathF.PI);
        Assert.Equal(MathF.Sqrt(13 - 6 * MathF.Sqrt(2)), CurveDistance2D.Distance(new(3, 0), arc, .25f, .75f), 5);

        var polyline = PolylineCurve2D.Builder.Create([new(0, 0), new(2, 0), new(2, 2)]);
        Assert.Equal(1, CurveDistance2D.Distance(Vector2f.Zero, polyline, .25f, .75f), 5);
    }

    [Fact]
    public void QuadraticBezierSamplesTheFaceArcIntoCallerStorage()
    {
        var curve = QuadraticBezier2D.Builder.Create(new(0f, 0f), new(1f, 2f), new(2f, 0f));
        Span<Vector2f> points = stackalloc Vector2f[5];
        curve.Ops.Sample(points);

        Assert.Equal(curve.Start, points[0]);
        Assert.Equal(new Vector2f(1f, 1f), points[2]);
        Assert.Equal(curve.End, points[^1]);
        Assert.Equal(new Vector2f(2f, 4f), curve.Derivative(0f));
        Assert.Equal(new Vector2f(2f, -4f), curve.Derivative(1f));
    }

    [Fact]
    public void CubicBezierPreservesEndpointsAndHasAnalyticDerivative()
    {
        var curve = CubicBezier2D.Builder.Create(
            new Vector2f(1f, 2f),
            new Vector2f(3f, 8f),
            new Vector2f(7f, 5f),
            new Vector2f(9f, -3f));

        Assert.Equal(curve.Start, curve.Ops.EvaluateClamped(-1f));
        Assert.Equal(curve.End, curve.Ops.EvaluateClamped(2f));
        Assert.Equal(3f * (curve.Control1 - curve.Start), curve.Derivative(0f));
        Assert.Equal(3f * (curve.End - curve.Control2), curve.Derivative(1f));
    }

    [Fact]
    public void ClampedBSplinePreservesEndpoints()
    {
        Vector2f[] controlPoints =
        [
            new(0f, 0f),
            new(20f, -10f),
            new(40f, 30f),
            new(60f, 20f),
            new(80f, 0f)
        ];
        var curve = BSpline2D.Builder.Create(controlPoints);

        Assert.Equal(controlPoints[0], curve.Evaluate(0f));
        Assert.Equal(controlPoints[^1], curve.Evaluate(1f));
        Assert.Equal(3, curve.Degree);
    }

    [Fact]
    public void LinearBSplineInterpolatesItsControlPolygon()
    {
        var curve = BSpline2D.Builder.Create(
            [new Vector2f(0f, 0f), new Vector2f(10f, 10f), new Vector2f(20f, 0f)],
            degree: 1);

        Assert.Equal(new Vector2f(5f, 5f), curve.Evaluate(0.25f));
        Assert.Equal(new Vector2f(15f, 5f), curve.Evaluate(0.75f));
        Assert.Equal(new Vector2f(20f, 20f), curve.Derivative(0.25f));
        Assert.Equal(new Vector2f(20f, -20f), curve.Derivative(0.75f));
    }

    [Fact]
    public void GaussianNormalOffsetCreatesALocalBump()
    {
        var baseline = CubicBezier2D.Builder.Create(
            new Vector2f(0f, 0f),
            new Vector2f(3f, 0f),
            new Vector2f(7f, 0f),
            new Vector2f(10f, 0f));
        var bumped = new NormalOffsetCurve2D(
            baseline,
            Easingf.Gaussian(center: 0.5f, sigma: 0.1f).ToRange(0f, 4f));

        Assert.Equal(4f, bumped.Evaluate(0.5f).Y, 5);
        Assert.InRange(bumped.Evaluate(0f).Y, 0f, 0.0001f);
        Assert.InRange(bumped.Evaluate(1f).Y, 0f, 0.0001f);
    }

    [Fact]
    public void CurveSamplingIncludesBothEndpoints()
    {
        var curve = CubicBezier2D.Builder.Create(Vector2f.Zero, Vector2f.UnitX, new(2f, 0f), new(3f, 0f));
        var samples = curve.Ops.Sample(segmentCount: 3);

        Assert.Equal(4, samples.Length);
        Assert.Equal(curve.Start, samples[0]);
        Assert.Equal(curve.End, samples[^1]);
        Assert.Equal(Vector2f.UnitX, Curve2D.Tangent(curve, 0.5f));
        Assert.Equal(Vector2f.UnitY, Curve2D.Normal(curve, 0.5f));
    }
}
