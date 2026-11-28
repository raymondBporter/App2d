using System.Numerics;

namespace App2d.Core.Mathematics.Curves;

/// <summary>A quadratic Bezier over scalar or vector values with a normalized parameter.</summary>
public readonly record struct QuadraticBezier<TValue, TReal>(TValue Start, TValue Control, TValue End)
    : ITwiceDifferentiableCurve<TValue, TReal>, IControlPointCurve<TValue, QuadraticBezier<TValue, TReal>>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public Interval<TReal> Domain => Interval.UnitInterval<TReal>();
    public TValue Evaluate(TReal amount)
    {
        return BezierMath.Quadratic(Start, Control, End, amount);
    }

    public TValue Derivative(TReal amount)
    {
        return BezierMath.QuadraticDerivative(Start, Control, End, amount);
    }

    public TValue SecondDerivative(TReal amount)
    {
        return BezierMath.QuadraticSecondDerivative<TValue, TReal>(Start, Control, End);
    }

    public int ControlPointCount => 3;
    public TValue GetControlPoint(int index)
    {
        return index switch { 0 => Start, 1 => Control, 2 => End, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    }

    /// <summary>Splits at any parameter. Both halves are exact, and together they are this curve.</summary>
    public (QuadraticBezier<TValue, TReal> Left, QuadraticBezier<TValue, TReal> Right) Split(TReal amount)
    {
        return (new(Start, Blossom(TReal.Zero, amount), Blossom(amount, amount)), new(Blossom(amount, amount), Blossom(amount, TReal.One), End));
    }

    /// <summary>The exact curve over [from, to], remapped to [0, 1]. The interval may be reversed, empty, or reach outside [0, 1].</summary>
    public QuadraticBezier<TValue, TReal> Slice(TReal from, TReal to)
    {
        return new(Blossom(from, from), Blossom(from, to), Blossom(to, to));
    }

    // The polar form: de Casteljau with a different parameter at each level. Equal arguments give a point on the curve.
    private TValue Blossom(TReal first, TReal second)
    {
        return BezierMath.Blend(BezierMath.Blend(Start, Control, TReal.One - first, first), BezierMath.Blend(Control, End, TReal.One - first, first), TReal.One - second, second);
    }

    public QuadraticBezier<TValue, TReal> WithControlPoints(ReadOnlySpan<TValue> points)
    {
        return points.Length == 3
            ? new(points[0], points[1], points[2])
            : throw new InvalidDataException("Curve point count must match its definition.");
    }
}
