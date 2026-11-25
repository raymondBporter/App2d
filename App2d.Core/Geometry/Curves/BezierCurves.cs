using System.Numerics;

namespace App2d.Core.Geometry.Curves;

public readonly record struct QuadraticBezier<TValue, TReal>(TValue Start, TValue Control, TValue End) : ICurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal amount) => BezierMath.Quadratic(Start, Control, End, amount);
    public TValue EvaluateDerivative(TReal amount) => BezierMath.QuadraticDerivative(Start, Control, End, amount);
}

public readonly record struct CubicBezier<TValue, TReal>(TValue Start, TValue Control1, TValue Control2, TValue End) : ICurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : unmanaged, IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal amount) => BezierMath.Cubic(Start, Control1, Control2, End, amount);
    public TValue EvaluateDerivative(TReal amount) => BezierMath.CubicDerivative(Start, Control1, Control2, End, amount);
}
