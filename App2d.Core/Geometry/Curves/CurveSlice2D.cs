using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A parameter interval of another curve, remapped to [0, 1] without resampling its geometry.</summary>
public readonly record struct CurveSlice2D<TCurve> : ICurve2D where TCurve : ICurve2D
{
    public CurveSlice2D(TCurve source, float from, float to)
    {
        ArgGuard.ThrowIfNull(source);
        ArgGuard.InRange(from, 0f, 1f);
        ArgGuard.InRange(to, from, 1f);
        Source = source;
        From = from;
        To = to;
    }

    public TCurve Source { get; }
    public float From { get; }
    public float To { get; }

    public Vector2 Evaluate(float amount) => Source.Evaluate(Parameter(amount));

    public Vector2 EvaluateDerivative(float amount) => From == To ? Vector2.Zero
        : Source.EvaluateDerivative(Parameter(amount)) * (To - From);

    /// <summary>Distance to the restricted curve, approximated by a sampled polyline.</summary>
    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 128);

    private float Parameter(float amount)
    {
        amount = Clamping.Clamp01(amount);
        return amount == 1f ? To : From + (To - From) * amount;
    }
}
