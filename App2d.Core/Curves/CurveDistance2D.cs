using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>Point distance queries for finite curves. Smooth curves without an analytic query use a sampled polyline.</summary>
public static class CurveDistance2D
{
    public static float Distance(Vector2 point, ICurve2D curve)
    {
        ArgGuard.ThrowIfNull(curve);
        return curve.Distance(point);
    }

    public static float Distance(ICurve2D curve, Vector2 point) => Distance(point, curve);

    /// <summary>Polyline approximation in parameter space; callers can increase segments for tighter results.</summary>
    public static float SampledDistance<TCurve>(Vector2 point, TCurve curve, int segmentCount) where TCurve : ICurve2D
    {
        ArgGuard.ThrowIfNull(curve);
        ArgGuard.ThrowIfNotPositive(segmentCount);
        var previous = curve.Evaluate(0f);
        var nearestSquared = float.PositiveInfinity;
        for (var i = 1; i <= segmentCount; i++)
        {
            var next = curve.Evaluate(i / (float)segmentCount);
            nearestSquared = MathF.Min(nearestSquared, Distance2D.DistanceSquaredToSegment(point, previous, next));
            previous = next;
        }
        return MathF.Sqrt(nearestSquared);
    }
}
