using App2d.Core.Geometry.Curves;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Curves;

/// <summary>Editing control points in curve order, independent of character bindings.</summary>
public static class CurveControlPoints2D
{
    public static IReadOnlyList<Vector2> Get(ICurve2D curve) => curve switch
    {
        QuadraticBezier2D q => [q.Start, q.Control, q.End],
        CubicBezier2D c => [c.Start, c.Control1, c.Control2, c.End],
        RoundedPolylineCurve2D r => r.Points.ToArray(),
        _ => throw new InvalidDataException("Control point editing requires a quadratic or cubic Bézier or rounded polyline.")
    };

    public static ICurve2D With(ICurve2D curve, IReadOnlyList<Vector2> points) => curve switch
    {
        QuadraticBezier2D q when points.Count == 3 => q with { Start = points[0], Control = points[1], End = points[2] },
        CubicBezier2D c when points.Count == 4 => c with { Start = points[0], Control1 = points[1], Control2 = points[2], End = points[3] },
        RoundedPolylineCurve2D r when points.Count == r.Points.Length => r with { Points = [.. points] },
        _ => throw new InvalidDataException("Curve point count must match its curve definition.")
    };
}
