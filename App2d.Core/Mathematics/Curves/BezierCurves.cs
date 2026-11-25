using System.Numerics;

namespace App2d.Core.Mathematics;

public readonly record struct QuadraticBezier<TValue, TReal>(TValue Start, TValue Control, TValue End) : IDifferentiableCurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal amount) => BezierMath.Quadratic(Start, Control, End, amount);
    public TValue Derivative(TReal amount) => BezierMath.QuadraticDerivative(Start, Control, End, amount);
}

public readonly record struct CubicBezier<TValue, TReal>(TValue Start, TValue Control1, TValue Control2, TValue End) : IDifferentiableCurve<TValue, TReal>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal amount) => BezierMath.Cubic(Start, Control1, Control2, End, amount);
    public TValue Derivative(TReal amount) => BezierMath.CubicDerivative(Start, Control1, Control2, End, amount);
}
