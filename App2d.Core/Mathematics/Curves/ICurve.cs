using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A function of one real parameter. Evaluation computes the curve's own formula and never clamps:
/// the caller chooses clamping or extension through <see cref="Curve{TCurve, TValue, TReal}"/>.
/// Two geometry types still clamp for themselves until they are reworked: NormalOffsetCurve2D
/// and RoundedPolylineCurve2D. NaN in gives NaN out, for values and derivatives.
/// </summary>
public interface ICurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    TValue Evaluate(TReal t);
}

/// <summary>A curve with an analytic derivative with respect to its parameter.</summary>
public interface IDifferentiableCurve<TValue, TReal> : ICurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    TValue Derivative(TReal t);
}

/// <summary>
/// A curve with an analytic second derivative. Curvature, the normal's rate of turn and the tangent at a
/// stationary point all need it. A constant second derivative is returned for any parameter.
/// </summary>
public interface ITwiceDifferentiableCurve<TValue, TReal> : IDifferentiableCurve<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    TValue SecondDerivative(TReal t);
}
