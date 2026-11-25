using App2d.Core.Geometry.Distance;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

/// <summary>Exposes an exact parameter restriction to geometric queries without sampling or reflection.</summary>
public interface ICurveSlice2D : ICurve2D
{
    ICurve2D SourceCurve { get; }
    float From { get; }
    float To { get; }
}

/// <summary>A parameter interval of another curve, remapped to [0, 1] without resampling its geometry.</summary>
public readonly record struct CurveSlice2D<TCurve> : ICurveSlice2D where TCurve : ICurve2D
{
    public CurveSlice2D(TCurve source, float from, float to)
    {
        Ensure.ThrowIfNull(source);
        Ensure.InRange(from, 0f, 1f);
        Ensure.InRange(to, from, 1f);
        Source = source;
        From = from;
        To = to;
    }

    public TCurve Source { get; }
    ICurve2D ICurveSlice2D.SourceCurve => Source;
    public GeometryKind Kind => GeometryKind.CurveSlice;
    public float From { get; }
    public float To { get; }

    public Vector2f Evaluate(float amount) => Source.Evaluate(Parameter(amount));

    public Vector2f EvaluateDerivative(float amount) => From == To ? Vector2f.Zero
        : Source.EvaluateDerivative(Parameter(amount)) * (To - From);

    /// <summary>Distance to the source curve over this parameter interval.</summary>
    public float Distance(Vector2f point) => CurveDistance2D.DistanceOnInterval(point, Source, From, To);

    private float Parameter(float amount) => float.IsNaN(amount) ? amount : Interval.LerpClamped(From, To, amount);
}
