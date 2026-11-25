using App2d.Core.Geometry.Distance;
using App2d.Core.Mathematics.RootFinding;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

/// <summary>A cubic Bezier curve with normalized parameter range [0, 1].</summary>
public readonly partial record struct CubicBezier2D : ICurve2D
{
    public GeometryKind Kind => GeometryKind.CubicBezier;

    public CubicBezier2D(Vector2f start, Vector2f control1, Vector2f control2, Vector2f end)
    {
        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;
    }

    public readonly Vector2f Start { get; init; }
    public readonly Vector2f Control1 { get; init; }
    public readonly Vector2f Control2 { get; init; }
    public readonly Vector2f End { get; init; }

    /// <summary>Creates a cubic from endpoints and tangents with respect to its normalized parameter.</summary>
    public static CubicBezier2D FromHermite(Vector2f start, Vector2f end, Vector2f startTangent, Vector2f endTangent) =>
        Builder.Create(start, start + startTangent / 3f, end - endTangent / 3f, end);

    public Vector2f Evaluate(float amount) =>
        BezierMath.Cubic(Start, Control1, Control2, End, amount);

    public Vector2f Derivative(float amount) =>
        BezierMath.CubicDerivative(Start, Control1, Control2, End, amount);

    /// <summary>Splits at a finite parameter in [0, 1], preserving the exact curve on each side.</summary>
    public (CubicBezier2D Left, CubicBezier2D Right) Split(float amount)
    {
        Ensure.InRange(amount, 0f, 1f);
        var a = Vector2f.Lerp(Start, Control1, amount);
        var b = Vector2f.Lerp(Control1, Control2, amount);
        var c = Vector2f.Lerp(Control2, End, amount);
        var d = Vector2f.Lerp(a, b, amount);
        var e = Vector2f.Lerp(b, c, amount);
        var point = Vector2f.Lerp(d, e, amount);
        return (new(Start, a, d, point), new(point, e, c, End));
    }

    /// <summary>Restricts the curve to ordered parameters in [0, 1], then maps that interval to [0, 1]. Equal parameters produce a point.</summary>
    public CubicBezier2D Slice(float from, float to)
    {
        Ensure.NonNegative(from);
        Ensure.LessThanOrEqual(to, 1);
        Ensure.LessThanOrEqual(from, to);
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
        Ensure.Finite(x);
        Ensure.ThrowIfTooShort(parameters, 3);
        var start = (double)Start.X;
        var first = Control1.X - start;
        var second = (double)Control2.X - Control1.X;
        var third = (double)End.X - Control2.X;
        if (start == x && first == 0 && second == 0 && third == 0)
        {
            throw new InvalidOperationException("Every parameter on this curve has the requested X coordinate.");
        }
        Span<double> coefficients = [start - x, 3 * first, 3 * (second - first), third - 2 * second + first];
        return PolynomialRoots.FindRealRoots(coefficients, 0, 1, parameters);
    }

    /// <summary>Finds the parameter for a unique X intersection, as used by monotonic time/value curves.</summary>
    /// <exception cref="InvalidOperationException">There is no intersection, or more than one parameter matches.</exception>
    public float ParameterAtX(float x)
    {
        Ensure.Finite(x);
        var width = (double)End.X - Start.X;
        if (width != 0)
        {
            var first = Interval.InverseLerp((double)Start.X, End.X, Control1.X);
            var second = Interval.InverseLerp((double)Start.X, End.X, Control2.X);
            var target = Interval.InverseLerp((double)Start.X, End.X, x);
            // Handles within the endpoint range guarantee monotonic X, even when the handles cross.
            if (first is >= 0 and <= 1 && second is >= 0 and <= 1 && target is >= 0 and <= 1)
            {
                if (target is 0 or 1)
                {
                    return (float)target;
                }
                // The standard control times make X(u) linear; these common presets need no inverse solve.
                if (Control1.X == Interval.Lerp(Start.X, End.X, 1f / 3f) && Control2.X == Interval.Lerp(Start.X, End.X, 2f / 3f))
                    return (float)target;
                // The normalized X coordinate is a scalar cubic Bezier; invert it as a curve.
                if (BracketedNewton.TrySolve(new CubicBezier<double, double>(0, first, second, 1), target, 64, out var amount))
                {
                    return (float)amount;
                }
            }
        }
        // Use the full solver for nonmonotonic curves and difficult, nearly flat time coordinates.
        Span<double> parameters = stackalloc double[3];
        var count = FindParametersAtX(x, parameters);
        return count == 1 ? (float)parameters[0]
            : throw new InvalidOperationException("The curve must have exactly one parameter at the requested X coordinate.");
    }

    public float Distance(Vector2f point) => CurveDistance2D.SampledDistance(point, this, 128);
}
