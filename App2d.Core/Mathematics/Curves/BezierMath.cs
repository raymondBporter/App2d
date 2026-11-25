using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Bernstein evaluation shared by scalar attributes and vectors of any dimension. Nothing clamps: outside [0, 1] the polynomial continues.</summary>
public static class BezierMath
{
    internal static TValue Blend<TValue, TReal>(TValue start, TValue end, TReal startWeight, TReal endWeight)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue> =>
        start * startWeight + end * endWeight;

    // The caller supplies a parameter in the desired domain. This also works on restricted spans.
    internal static TValue EvaluateControls<TValue, TReal>(ReadOnlySpan<TValue> controls, TReal amount)
        where TValue : unmanaged, IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal>
    {
        Span<TValue> work = controls.Length <= 32 ? stackalloc TValue[controls.Length] : new TValue[controls.Length];
        controls.CopyTo(work);
        var inverse = TReal.One - amount;
        for (var length = work.Length - 1; length > 0; length--)
            for (var i = 0; i < length; i++) work[i] = Blend(work[i], work[i + 1], inverse, amount);
        return work[0];
    }

    public static TValue Quadratic<TValue, TReal>(TValue start, TValue control, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal>
    {
        var inverse = TReal.One - amount;
        return start * (inverse * inverse) + control * (TReal.CreateChecked(2) * inverse * amount) + end * (amount * amount);
    }

    public static TValue QuadraticDerivative<TValue, TReal>(TValue start, TValue control, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal>
    {
        return Blend(control - start, end - control, TReal.One - amount, amount) * TReal.CreateChecked(2);
    }

    public static TValue QuadraticSecondDerivative<TValue, TReal>(TValue start, TValue control, TValue end)
        where TValue : ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal> =>
        (end - control - (control - start)) * TReal.CreateChecked(2);

    public static TValue CubicSecondDerivative<TValue, TReal>(TValue start, TValue control1, TValue control2, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal> =>
        Blend(control2 - control1 - (control1 - start), end - control2 - (control2 - control1), TReal.One - amount, amount) * TReal.CreateChecked(6);

    public static TValue Cubic<TValue, TReal>(TValue start, TValue control1, TValue control2, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal>
    {
        var inverse = TReal.One - amount;
        var three = TReal.CreateChecked(3);
        return start * (inverse * inverse * inverse) + control1 * (three * inverse * inverse * amount) +
            control2 * (three * inverse * amount * amount) + end * (amount * amount * amount);
    }

    public static TValue CubicDerivative<TValue, TReal>(TValue start, TValue control1, TValue control2, TValue end, TReal amount)
        where TValue : IAdditionOperators<TValue, TValue, TValue>, ISubtractionOperators<TValue, TValue, TValue>, IMultiplyOperators<TValue, TReal, TValue>
        where TReal : IFloatingPointIeee754<TReal>
    {
        var inverse = TReal.One - amount;
        return (control1 - start) * (TReal.CreateChecked(3) * inverse * inverse) +
            (control2 - control1) * (TReal.CreateChecked(6) * inverse * amount) +
            (end - control2) * (TReal.CreateChecked(3) * amount * amount);
    }
}
