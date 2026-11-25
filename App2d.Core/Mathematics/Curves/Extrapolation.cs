using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>What a curve does outside [0, 1]. The user of a curve chooses; the curve itself never does.</summary>
public enum Extrapolation
{
    /// <summary>The curve's own formula continues.</summary>
    None,
    /// <summary>Holds the endpoint value. The derivative is zero outside and the curve's own at exactly 0 or 1.</summary>
    Clamp,
    /// <summary>Continues in a straight line along the endpoint tangent.</summary>
    Extend
}

/// <summary>A curve with a chosen behaviour before 0 and after 1. Inside [0, 1], and for NaN, it is the source curve.</summary>
public readonly record struct Extrapolated<TCurve, TValue, TReal>(TCurve Curve, Extrapolation Before, Extrapolation After) : IDifferentiableCurve<TValue, TReal>
    where TCurve : IDifferentiableCurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>, IAdditiveIdentity<TValue, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public void Validate()
    {
        CurveArgument.NotNull(Curve, "curve");
        if (!Enum.IsDefined(Before)) throw new ArgumentOutOfRangeException("before", Before, "Unknown extrapolation.");
        if (!Enum.IsDefined(After)) throw new ArgumentOutOfRangeException("after", After, "Unknown extrapolation.");
    }

    public Extrapolated<TCurve, TValue, TReal> Validated() { Validate(); return this; }

    public TValue Evaluate(TReal t) => t < TReal.Zero ? EvaluateOutside(Before, TReal.Zero, t) : t > TReal.One ? EvaluateOutside(After, TReal.One, t) : Curve.Evaluate(t);

    public TValue Derivative(TReal t) => t < TReal.Zero ? DerivativeOutside(Before, TReal.Zero, t) : t > TReal.One ? DerivativeOutside(After, TReal.One, t) : Curve.Derivative(t);

    private TValue EvaluateOutside(Extrapolation mode, TReal end, TReal t) => mode switch
    {
        Extrapolation.None => Curve.Evaluate(t),
        Extrapolation.Clamp => Curve.Evaluate(end),
        Extrapolation.Extend => Curve.Evaluate(end) + Curve.Derivative(end) * (t - end),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown extrapolation.")
    };

    private TValue DerivativeOutside(Extrapolation mode, TReal end, TReal t) => mode switch
    {
        Extrapolation.None => Curve.Derivative(t),
        Extrapolation.Clamp => TValue.AdditiveIdentity,
        Extrapolation.Extend => Curve.Derivative(end),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown extrapolation.")
    };
}
