using App2d.Core.Geometry.Curves;

namespace App2d.Tests.Curves;

/// <summary>Leaf curves evaluate their own formula outside [0, 1]; clamping is the caller's choice.</summary>
public sealed class CurveExtrapolationTests
{
    [Fact]
    public void BezierContinuesItsPolynomial()
    {
        var curve = new QuadraticBezier<double, double>(0d, 0d, 1d);

        Assert.Equal(4d, curve.Evaluate(2d));
        Assert.Equal(4d, curve.Derivative(2d));
        Assert.Equal(-27d, new CubicBezier<double, double>(0d, 0d, 0d, 1d).Evaluate(-3d));
    }

    [Fact]
    public void LineContinues()
    {
        var line = LineSegmentCurve2D.Create(new(1, 2), new(4, 6));

        Assert.Equal(new Vector2f(7, 10), line.Evaluate(2));
        Assert.Equal(new Vector2f(-2, -2), line.Evaluate(-1));
    }

    [Fact]
    public void ArcContinuesRoundItsCircle()
    {
        var arc = Arc2D.Builder.Create(Vector2f.Zero, 1, 0, MathF.PI / 2);

        var point = arc.Evaluate(2);
        var tangent = arc.Derivative(2);

        Assert.Equal(-1f, point.X, 6);
        Assert.Equal(0f, point.Y, 6);
        Assert.Equal(0f, tangent.X, 5);
        Assert.Equal(-MathF.PI / 2, tangent.Y, 5);
    }

    [Fact]
    public void PolylineContinuesItsEndSegments()
    {
        var polyline = PolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1)]);

        Assert.Equal(new Vector2f(1, 2), polyline.Evaluate(1.5f));
        Assert.Equal(new Vector2f(-1, 0), polyline.Evaluate(-.5f));
        Assert.Equal(new Vector2f(-200, 0), polyline.Evaluate(-100f));
        Assert.Equal(new Vector2f(0, 2), polyline.Derivative(100f));
    }

    [Fact]
    public void BSplineContinuesItsEndSpan()
    {
        // A degree-1 spline through two points is a line, so its continuation is known exactly.
        var spline = BSpline2D.Builder.Create([new(0, 0), new(2, 4)], 1);

        Assert.Equal(new Vector2f(4, 8), spline.Evaluate(2));
        Assert.Equal(new Vector2f(-2, -4), spline.Evaluate(-1));
    }

    [Fact]
    public void NaNDerivativeStaysNaNEvenWhereTheTangentIsConstant()
    {
        Assert.True(float.IsNaN(LineSegmentCurve2D.Create(new(1, 2), new(4, 6)).Derivative(float.NaN).X));
        Assert.True(float.IsNaN(PolylineCurve2D.Builder.Create([new(0, 0), new(1, 0), new(1, 1)]).Derivative(float.NaN).Y));
        Assert.True(float.IsNaN(BSpline2D.Builder.Create([new(0, 0), new(2, 4)], 1).Derivative(float.NaN).X));
    }

    [Fact]
    public void NaNStaysNaN()
    {
        Assert.True(double.IsNaN(new QuadraticBezier<double, double>(0d, 0d, 1d).Evaluate(double.NaN)));
        Assert.True(float.IsNaN(LineSegmentCurve2D.Create(new(1, 2), new(4, 6)).Evaluate(float.NaN).X));
    }
}
