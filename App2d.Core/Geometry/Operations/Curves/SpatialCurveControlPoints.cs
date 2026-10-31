using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics;

namespace App2d.Core.Geometry.Operations.Curves;

/// <summary>Authoring access to the supported spatial Bézier control points.</summary>
public static class SpatialCurveControlPoints
{
    public static IReadOnlyList<Vector3f> Get(ICurve<Vector3f, float> curve) => curve switch
    {
        QuadraticBezier<Vector3f, float> q => [q.Start, q.Control, q.End],
        CubicBezier<Vector3f, float> c => [c.Start, c.Control1, c.Control2, c.End],
        _ => throw new InvalidDataException("Spatial curve authoring supports quadratic and cubic Béziers.")
    };

    public static ICurve<Vector3f, float> With(ICurve<Vector3f, float> curve, IReadOnlyList<Vector3f> points) => curve switch
    {
        QuadraticBezier<Vector3f, float> q when points.Count == 3 => q with { Start = points[0], Control = points[1], End = points[2] },
        CubicBezier<Vector3f, float> c when points.Count == 4 => c with { Start = points[0], Control1 = points[1], Control2 = points[2], End = points[3] },
        _ => throw new InvalidDataException("Curve point count must match its definition.")
    };
}
