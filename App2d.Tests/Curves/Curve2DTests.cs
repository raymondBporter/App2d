using App2d.Core.Curves;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class Curve2DTests
{
    [Fact]
    public void CubicBezierPreservesEndpointsAndHasAnalyticDerivative()
    {
        var curve = new CubicBezier2D(
            new Vector2(1f, 2f),
            new Vector2(3f, 8f),
            new Vector2(7f, 5f),
            new Vector2(9f, -3f));

        Assert.Equal(curve.Start, curve.Evaluate(-1f));
        Assert.Equal(curve.End, curve.Evaluate(2f));
        Assert.Equal(3f * (curve.Control1 - curve.Start), curve.EvaluateDerivative(0f));
        Assert.Equal(3f * (curve.End - curve.Control2), curve.EvaluateDerivative(1f));
    }

    [Fact]
    public void ClampedBSplinePreservesEndpoints()
    {
        Vector2[] controlPoints =
        [
            new(0f, 0f),
            new(20f, -10f),
            new(40f, 30f),
            new(60f, 20f),
            new(80f, 0f)
        ];
        var curve = new BSpline2D(controlPoints);

        Assert.Equal(controlPoints[0], curve.Evaluate(0f));
        Assert.Equal(controlPoints[^1], curve.Evaluate(1f));
        Assert.Equal(3, curve.Degree);
    }

    [Fact]
    public void LinearBSplineInterpolatesItsControlPolygon()
    {
        var curve = new BSpline2D(
            [new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(20f, 0f)],
            degree: 1);

        Assert.Equal(new Vector2(5f, 5f), curve.Evaluate(0.25f));
        Assert.Equal(new Vector2(15f, 5f), curve.Evaluate(0.75f));
        Assert.Equal(new Vector2(20f, 20f), curve.EvaluateDerivative(0.25f));
        Assert.Equal(new Vector2(20f, -20f), curve.EvaluateDerivative(0.75f));
    }

    [Fact]
    public void GaussianNormalOffsetCreatesALocalBump()
    {
        var baseline = new CubicBezier2D(
            new Vector2(0f, 0f),
            new Vector2(3f, 0f),
            new Vector2(7f, 0f),
            new Vector2(10f, 0f));
        var bumped = new NormalOffsetCurve2D(
            baseline,
            amount => 4f * Curve2D.Gaussian(amount, center: 0.5f, sigma: 0.1f));

        Assert.Equal(4f, bumped.Evaluate(0.5f).Y, 5);
        Assert.InRange(bumped.Evaluate(0f).Y, 0f, 0.0001f);
        Assert.InRange(bumped.Evaluate(1f).Y, 0f, 0.0001f);
    }

    [Fact]
    public void CurveSamplingIncludesBothEndpoints()
    {
        var curve = new CubicBezier2D(Vector2.Zero, Vector2.UnitX, new(2f, 0f), new(3f, 0f));
        var samples = Curve2D.Sample(curve, segmentCount: 3);

        Assert.Equal(4, samples.Length);
        Assert.Equal(curve.Start, samples[0]);
        Assert.Equal(curve.End, samples[^1]);
        Assert.Equal(Vector2.UnitX, Curve2D.Tangent(curve, 0.5f));
        Assert.Equal(Vector2.UnitY, Curve2D.Normal(curve, 0.5f));
    }
}
