using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Curves;
using System.Numerics;

namespace App2d.Tests.Curves;

public sealed class CurvePolylineSamplingTests
{
    [Fact]
    public void CubicSplineSamplingPreservesEndpointsAndBoundsChordLengthAndDeviation()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(100, 0), new(150, 130), new(200, -100), new(300, 50), new(400, 0)]);
        var polyline = Curve2D.ToPolyline(spline, 12f, 0.05f);
        Assert.Equal(spline.Evaluate(0), polyline.Points[0]);
        Assert.Equal(spline.Evaluate(1), polyline.Points[^1]);
        for (var index = 1; index < polyline.Points.Length; index++)
            Assert.InRange(Vector2.Distance(polyline.Points[index - 1], polyline.Points[index]), 0f, 12.001f);
        for (var sample = 0; sample <= 2000; sample++)
            Assert.InRange(polyline.Distance(spline.Evaluate(sample / 2000f)), 0f, 0.055f);
    }

    [Fact]
    public void PolylineSamplingPreservesCornersInsteadOfRoundingThem()
    {
        Vector2[] corners = [new(0, 0), new(30, 0), new(30, 40), new(80, 20)];
        var curve = PolylineCurve2D.Builder.Create(corners);
        var sampled = Curve2D.ToPolyline(curve, 8f);
        foreach (var corner in corners) Assert.Contains(corner, sampled.Points.ToArray());
        for (var sample = 0; sample <= 100; sample++)
            Assert.InRange(sampled.Distance(curve.Evaluate(sample / 100f)), 0f, 0.00001f);
    }

    [Fact]
    public void CubicSplinePositionAndSlopeAgreeAcrossInteriorKnots()
    {
        var spline = BSpline2D.Builder.Create([new(0, 0), new(100, 0), new(150, 130), new(200, -100), new(300, 50), new(400, 0)]);
        var spans = spline.ControlPoints.Length - spline.Degree;
        for (var knot = 1; knot < spans; knot++)
        {
            var amount = knot / (float)spans;
            Assert.InRange(Vector2.Distance(spline.Evaluate(amount - 1e-5f), spline.Evaluate(amount + 1e-5f)), 0f, 0.02f);
            Assert.InRange(Vector2.Distance(spline.EvaluateDerivative(amount - 1e-5f), spline.EvaluateDerivative(amount + 1e-5f)), 0f, 0.1f);
        }
    }
}
