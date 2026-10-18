using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.HeightFields;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.HeightFields;

/// <summary>
/// A B-spline top with strictly increasing control-point X coordinates (a sufficient monotonicity rule).
/// Solves curve.X(t) = x before querying Y or dy/dx; normalized curve progress is not world X.
/// </summary>
public sealed class BSplineHeightField2D : IHeightField2D
{
    public BSplineHeightField2D(BSpline2D curve, float yMin)
    {
        curve.Validate();
        HeightField2D.ValidateIncreasingX(curve.ControlPoints);
        Curve = curve;
        XMin = curve.ControlPoints[0].X;
        XMax = curve.ControlPoints[^1].X;
        YMin = yMin;
        var maximum = float.NegativeInfinity;
        foreach (var point in curve.ControlPoints) maximum = MathF.Max(maximum, point.Y);
        YMax = maximum; // Convex-hull bound; no extrema calculation or tessellation.
        HeightField2D.ValidateDomain(XMin, XMax, YMin, YMax);
        _ = HeightAt(XMin);
        _ = HeightAt(XMax);
    }

    public BSpline2D Curve { get; }
    public float XMin { get; }
    public float XMax { get; }
    public float YMin { get; }
    public float YMax { get; }

    public float HeightAt(float x) => HeightField2D.ValidateHeight(this, Curve.Evaluate(ParameterAt(x)).Y);

    public float SlopeAt(float x)
    {
        var derivative = Curve.EvaluateDerivative(ParameterAt(x));
        return ArgGuard.Finite(derivative.Y / derivative.X);
    }

    private float ParameterAt(float x)
    {
        HeightField2D.ValidateX(this, x);
        if (x == XMin) return 0f;
        if (x == XMax) return 1f;
        var lower = 0f;
        var upper = 1f;
        for (var iteration = 0; iteration < 40; iteration++)
        {
            var middle = (lower + upper) * 0.5f;
            if (middle == lower || middle == upper) break;
            var curveX = Curve.Evaluate(middle).X;
            if (curveX == x) return middle;
            if (curveX < x) lower = middle;
            else upper = middle;
        }
        return (lower + upper) * 0.5f;
    }
}
