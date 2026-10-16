using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A cubic Bezier curve with normalized parameter range [0, 1].</summary>
public readonly record struct CubicBezier2D : ICurve2D
{
    public CubicBezier2D(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end)
    {
        ArgGuard.Finite(start);
        ArgGuard.Finite(control1);
        ArgGuard.Finite(control2);
        ArgGuard.Finite(end);
        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;
    }

    public Vector2 Start
    {
        get;
    }
    public Vector2 Control1
    {
        get;
    }
    public Vector2 Control2
    {
        get;
    }
    public Vector2 End
    {
        get;
    }

    public Vector2 Evaluate(float amount)
    {
        amount = Clamping.Clamp01(amount);
        var inverse = 1f - amount;
        return inverse * inverse * inverse * Start +
            3f * inverse * inverse * amount * Control1 +
            3f * inverse * amount * amount * Control2 +
            amount * amount * amount * End;
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        amount = Clamping.Clamp01(amount);
        var inverse = 1f - amount;
        return 3f * inverse * inverse * (Control1 - Start) +
            6f * inverse * amount * (Control2 - Control1) +
            3f * amount * amount * (End - Control2);
    }

    /// <summary>Splits at a finite parameter in [0, 1], preserving the exact curve on each side.</summary>
    public (CubicBezier2D Left, CubicBezier2D Right) Split(float amount)
    {
        ArgGuard.InRange(amount, 0f, 1f);
        var a = Vector2.Lerp(Start, Control1, amount);
        var b = Vector2.Lerp(Control1, Control2, amount);
        var c = Vector2.Lerp(Control2, End, amount);
        var d = Vector2.Lerp(a, b, amount);
        var e = Vector2.Lerp(b, c, amount);
        var point = Vector2.Lerp(d, e, amount);
        return (new(Start, a, d, point), new(point, e, c, End));
    }

    /// <summary>Restricts the curve to ordered parameters in [0, 1], then maps that interval to [0, 1]. Equal parameters produce a point.</summary>
    public CubicBezier2D Slice(float from, float to)
    {
        ArgGuard.ThrowIf(!(from >= 0 && to <= 1 && from <= to), "The parameter interval must be ordered within [0, 1].", nameof(to));
        if (from == to)
        {
            var point = Evaluate(from);
            return new(point, point, point, point);
        }
        if (from == 0 && to == 1)
        {
            return this;
        }
        var left = Split(to).Left;
        return from == 0 ? left : left.Split(from / to).Right;
    }

    /// <summary>Writes the sorted, distinct parameters where the curve has the given X coordinate. The curve may cross it up to three times.</summary>
    /// <param name="x">The finite coordinate to find.</param>
    /// <param name="parameters">Storage for at least three parameters; unused entries are untouched.</param>
    /// <returns>The number of intersections in [0, 1], including endpoints and tangencies.</returns>
    /// <exception cref="InvalidOperationException">The entire curve has this X coordinate, so infinitely many parameters match.</exception>
    public int FindParametersAtX(float x, Span<double> parameters)
    {
        ArgGuard.Finite(x);
        ArgGuard.ThrowIfTooShort<double>(parameters, 3);
        var start = (double)Start.X;
        var first = (double)Control1.X - start;
        var second = (double)Control2.X - Control1.X;
        var third = (double)End.X - Control2.X;
        if (start == x && first == 0 && second == 0 && third == 0)
        {
            throw new InvalidOperationException("Every parameter on this curve has the requested X coordinate.");
        }
        Span<double> coefficients = stackalloc double[] { start - x, 3 * first, 3 * (second - first), third - 2 * second + first };
        return PolynomialRoots.FindRealRoots(coefficients, 0, 1, parameters);
    }

    /// <summary>Finds the parameter for a unique X intersection, as used by monotonic time/value curves.</summary>
    /// <exception cref="InvalidOperationException">There is no intersection, or more than one parameter matches.</exception>
    public float ParameterAtX(float x)
    {
        ArgGuard.Finite(x);
        var width = (double)End.X - Start.X;
        if (width != 0)
        {
            var first = ((double)Control1.X - Start.X) / width;
            var second = ((double)Control2.X - Start.X) / width;
            var target = ((double)x - Start.X) / width;
            // Handles within the endpoint range guarantee monotonic X, even when the handles cross.
            if (first is >= 0 and <= 1 && second is >= 0 and <= 1 && target is >= 0 and <= 1)
            {
                if (target is 0 or 1)
                {
                    return (float)target;
                }
                var a = 1 + 3 * (first - second);
                var b = 3 * (second - 2 * first);
                var c = 3 * first;
                var low = 0d;
                var high = 1d;
                var amount = target;
                for (var i = 0; i < 12; i++)
                {
                    var value = ((a * amount + b) * amount + c) * amount;
                    if (value == target)
                    {
                        return (float)amount;
                    }
                    if (value < target)
                    {
                        low = amount;
                    }
                    else
                    {
                        high = amount;
                    }
                    var derivative = (3 * a * amount + 2 * b) * amount + c;
                    var next = amount - (value - target) / derivative;
                    if (!(next > low && next < high))
                    {
                        next = (low + high) / 2;
                    }
                    if ((float)next == (float)amount)
                    {
                        return (float)amount;
                    }
                    amount = next;
                }
            }
        }
        // Use the full solver for nonmonotonic curves and difficult, nearly flat time coordinates.
        Span<double> parameters = stackalloc double[3];
        var count = FindParametersAtX(x, parameters);
        return count == 1 ? (float)parameters[0]
            : throw new InvalidOperationException("The curve must have exactly one parameter at the requested X coordinate.");
    }

    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 128);
}
