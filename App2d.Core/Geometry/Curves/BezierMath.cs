using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>Bernstein evaluation shared by scalar attributes and vectors of any dimension.</summary>
public static class BezierMath
{
    public static TValue Quadratic<TValue, TReal>(TValue start, TValue control, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        amount = TReal.Clamp(amount, TReal.Zero, TReal.One);
        var inverse = TReal.One - amount;
        return start * (inverse * inverse) + control * (TReal.CreateChecked(2) * inverse * amount) + end * (amount * amount);
    }

    public static TValue QuadraticDerivative<TValue, TReal>(TValue start, TValue control, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        amount = TReal.Clamp(amount, TReal.Zero, TReal.One);
        return ((control - start) * (TReal.One - amount) + (end - control) * amount) * TReal.CreateChecked(2);
    }

    public static TValue Cubic<TValue, TReal>(TValue start, TValue control1, TValue control2, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        amount = TReal.Clamp(amount, TReal.Zero, TReal.One);
        var inverse = TReal.One - amount;
        var three = TReal.CreateChecked(3);
        return start * (inverse * inverse * inverse) + control1 * (three * inverse * inverse * amount) +
            control2 * (three * inverse * amount * amount) + end * (amount * amount * amount);
    }

    public static TValue CubicDerivative<TValue, TReal>(TValue start, TValue control1, TValue control2, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        amount = TReal.Clamp(amount, TReal.Zero, TReal.One);
        var inverse = TReal.One - amount;
        return (control1 - start) * (TReal.CreateChecked(3) * inverse * inverse) +
            (control2 - control1) * (TReal.CreateChecked(6) * inverse * amount) +
            (end - control2) * (TReal.CreateChecked(3) * amount * amount);
    }
}
