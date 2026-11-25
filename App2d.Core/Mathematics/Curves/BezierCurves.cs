using System.Numerics;

namespace App2d.Core.Mathematics;

public readonly record struct QuadraticBezier<TValue, TReal>(TValue Start, TValue Control, TValue End) : ITwiceDifferentiableCurve<TValue, TReal>, IControlPointCurve<TValue, QuadraticBezier<TValue, TReal>>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal amount) => BezierMath.Quadratic(Start, Control, End, amount);
    public TValue Derivative(TReal amount) => BezierMath.QuadraticDerivative(Start, Control, End, amount);
    public TValue SecondDerivative(TReal amount) => BezierMath.QuadraticSecondDerivative<TValue, TReal>(Start, Control, End);

    public int ControlPointCount => 3;
    public TValue GetControlPoint(int index) => index switch { 0 => Start, 1 => Control, 2 => End, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    /// <summary>Splits at any parameter. Both halves are exact, and together they are this curve.</summary>
    public (QuadraticBezier<TValue, TReal> Left, QuadraticBezier<TValue, TReal> Right) Split(TReal amount) =>
        (new(Start, Blossom(TReal.Zero, amount), Blossom(amount, amount)), new(Blossom(amount, amount), Blossom(amount, TReal.One), End));

    /// <summary>The exact curve over [from, to], remapped to [0, 1]. The interval may be reversed, empty, or reach outside [0, 1].</summary>
    public QuadraticBezier<TValue, TReal> Slice(TReal from, TReal to) => new(Blossom(from, from), Blossom(from, to), Blossom(to, to));

    // The polar form: de Casteljau with a different parameter at each level. Equal arguments give a point on the curve.
    private TValue Blossom(TReal first, TReal second) =>
        BezierMath.Blend(BezierMath.Blend(Start, Control, TReal.One - first, first), BezierMath.Blend(Control, End, TReal.One - first, first), TReal.One - second, second);

    public QuadraticBezier<TValue, TReal> WithControlPoints(ReadOnlySpan<TValue> points) => points.Length == 3 ? new(points[0], points[1], points[2]) : throw new InvalidDataException("Curve point count must match its definition.");
}

public readonly record struct CubicBezier<TValue, TReal>(TValue Start, TValue Control1, TValue Control2, TValue End) : ITwiceDifferentiableCurve<TValue, TReal>, IControlPointCurve<TValue, CubicBezier<TValue, TReal>>
    where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
    where TReal : IFloatingPointIeee754<TReal>
{
    public TValue Evaluate(TReal amount) => BezierMath.Cubic(Start, Control1, Control2, End, amount);
    public TValue Derivative(TReal amount) => BezierMath.CubicDerivative(Start, Control1, Control2, End, amount);
    public TValue SecondDerivative(TReal amount) => BezierMath.CubicSecondDerivative(Start, Control1, Control2, End, amount);

    public int ControlPointCount => 4;
    public TValue GetControlPoint(int index) => index switch { 0 => Start, 1 => Control1, 2 => Control2, 3 => End, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    /// <summary>Creates a cubic from endpoints and tangents with respect to its normalized parameter.</summary>
    public static CubicBezier<TValue, TReal> FromHermite(TValue start, TValue end, TValue startTangent, TValue endTangent)
    {
        var third = TReal.One / TReal.CreateChecked(3);
        return new(start, start + startTangent * third, end - endTangent * third, end);
    }

    /// <summary>Splits at any parameter. Both halves are exact, and together they are this curve.</summary>
    public (CubicBezier<TValue, TReal> Left, CubicBezier<TValue, TReal> Right) Split(TReal amount)
    {
        var point = Blossom(amount, amount, amount);
        return (new(Start, Blossom(TReal.Zero, TReal.Zero, amount), Blossom(TReal.Zero, amount, amount), point),
            new(point, Blossom(amount, amount, TReal.One), Blossom(amount, TReal.One, TReal.One), End));
    }

    /// <summary>The exact curve over [from, to], remapped to [0, 1]. The interval may be reversed, empty, or reach outside [0, 1].</summary>
    public CubicBezier<TValue, TReal> Slice(TReal from, TReal to) =>
        new(Blossom(from, from, from), Blossom(from, from, to), Blossom(from, to, to), Blossom(to, to, to));

    // The polar form: de Casteljau with a different parameter at each level. Equal arguments give a point on the curve.
    private TValue Blossom(TReal first, TReal second, TReal third)
    {
        var a = BezierMath.Blend(Start, Control1, TReal.One - first, first);
        var b = BezierMath.Blend(Control1, Control2, TReal.One - first, first);
        var c = BezierMath.Blend(Control2, End, TReal.One - first, first);
        return BezierMath.Blend(BezierMath.Blend(a, b, TReal.One - second, second), BezierMath.Blend(b, c, TReal.One - second, second), TReal.One - third, third);
    }

    public CubicBezier<TValue, TReal> WithControlPoints(ReadOnlySpan<TValue> points) => points.Length == 4 ? new(points[0], points[1], points[2], points[3]) : throw new InvalidDataException("Curve point count must match its definition.");
}
