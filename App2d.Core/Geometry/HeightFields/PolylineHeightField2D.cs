using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.HeightFields;
using System.Numerics;

namespace App2d.Core.Geometry.HeightFields;

/// <summary>A piecewise-linear top surface with strictly increasing X coordinates.</summary>
public sealed class PolylineHeightField2D : IHeightField2D
{
    public PolylineHeightField2D(IEnumerable<Vector2> points, float yMin)
        : this(PolylineCurve2D.Builder.Create(points), yMin) { }

    public PolylineHeightField2D(PolylineCurve2D curve, float yMin)
    {
        curve.Validate();
        HeightField2D.ValidateIncreasingX(curve.Points);
        Curve = curve;
        XMin = curve.Points[0].X;
        XMax = curve.Points[^1].X;
        YMin = yMin;
        var maximum = float.NegativeInfinity;
        foreach (var point in curve.Points) maximum = MathF.Max(maximum, point.Y);
        YMax = maximum;
        HeightField2D.ValidateDomain(XMin, XMax, YMin, YMax);
        foreach (var point in curve.Points) HeightField2D.ValidateHeight(this, point.Y);
    }

    public PolylineCurve2D Curve { get; }
    public float XMin { get; }
    public float XMax { get; }
    public float YMin { get; }
    public float YMax { get; }

    public float HeightAt(float x)
    {
        var index = SegmentAt(x);
        var start = Curve.Points[index];
        var end = Curve.Points[index + 1];
        return float.Lerp(start.Y, end.Y, (x - start.X) / (end.X - start.X));
    }

    /// <summary>Uses the segment to the right at corners, and the final segment at XMax.</summary>
    public float SlopeAt(float x)
    {
        var index = SegmentAt(x);
        var start = Curve.Points[index];
        var end = Curve.Points[index + 1];
        return (end.Y - start.Y) / (end.X - start.X);
    }

    private int SegmentAt(float x)
    {
        HeightField2D.ValidateX(this, x);
        var points = Curve.Points;
        var lower = 0;
        var upper = points.Length - 1;
        while (upper - lower > 1)
        {
            var middle = (lower + upper) / 2;
            if (points[middle].X <= x) lower = middle;
            else upper = middle;
        }
        return lower;
    }
}
