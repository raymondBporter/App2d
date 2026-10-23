using App2d.Core.Mathematics;

namespace App2d.Core.Geometry.Operations.Intersection;

/// <summary>Roots on a restricted Bernstein polynomial interval. No curve tessellation is involved.</summary>
internal static class BernsteinRoots2D
{
    private const double MachineEpsilon = 2.2204460492503131e-16;

    internal static int Find(ReadOnlySpan<double> values, double from, double to, Span<double> roots)
    {
        var degree = values.Length - 1;
        var scale = 0d;
        foreach (var value in values) scale = Math.Max(scale, Math.Abs(value));
        if (degree == 0 || scale == 0) return 0;
        Span<double> normalized = values.Length <= 32 ? stackalloc double[values.Length] : new double[values.Length];
        for (var i = 0; i < values.Length; i++) normalized[i] = values[i] / scale;
        if (degree <= 4)
        {
            Span<double> differences = stackalloc double[values.Length];
            normalized.CopyTo(differences);
            Span<double> coefficients = stackalloc double[values.Length];
            coefficients[0] = differences[0];
            var binomial = 1d;
            for (var order = 1; order <= degree; order++)
            {
                for (var i = 0; i <= degree - order; i++) differences[i] = differences[i + 1] - differences[i];
                binomial *= (degree - order + 1d) / order;
                coefficients[order] = binomial * differences[0];
            }
            return PolynomialRoots.FindRealRoots(coefficients, from, to, roots);
        }

        // Higher-degree B-splines stay in Bernstein form. Derivative roots partition the interval into
        // monotone pieces, so sign brackets and stationary tangencies cover the isolated real roots.
        Span<double> derivative = degree <= 32 ? stackalloc double[degree] : new double[degree];
        for (var i = 0; i < degree; i++) derivative[i] = normalized[i + 1] - normalized[i];
        Span<double> critical = degree <= 32 ? stackalloc double[degree - 1] : new double[degree - 1];
        var criticalCount = Find(derivative, from, to, critical);
        var count = 0;
        var low = from;
        var lowValue = Evaluate(normalized, low);
        for (var i = 0; i <= criticalCount; i++)
        {
            var high = i == criticalCount ? to : critical[i];
            var highValue = Evaluate(normalized, high);
            if (NearZero(lowValue, degree)) Append(roots, ref count, low);
            if (!NearZero(lowValue, degree) && !NearZero(highValue, degree) && OppositeSigns(lowValue, highValue))
            {
                var left = low;
                var right = high;
                var leftValue = lowValue;
                for (var iteration = 0; iteration < 2048; iteration++)
                {
                    var middle = (left + right) / 2;
                    if (middle == left || middle == right) break;
                    var value = Evaluate(normalized, middle);
                    if (value == 0) { left = right = middle; break; }
                    if (OppositeSigns(leftValue, value))
                    {
                        right = middle;
                    }
                    else { left = middle; leftValue = value; }
                }
                Append(roots, ref count, (left + right) / 2);
            }
            low = high;
            lowValue = highValue;
        }
        if (NearZero(lowValue, degree)) Append(roots, ref count, low);
        return count;
    }

    private static double Evaluate(ReadOnlySpan<double> controls, double amount)
    {
        Span<double> work = controls.Length <= 32 ? stackalloc double[controls.Length] : new double[controls.Length];
        controls.CopyTo(work);
        for (var length = work.Length - 1; length > 0; length--)
            for (var i = 0; i < length; i++) work[i] = work[i] * (1 - amount) + work[i + 1] * amount;
        return work[0];
    }

    private static bool NearZero(double value, int degree) => Math.Abs(value) <= 16 * MachineEpsilon * (degree + 1);
    private static bool OppositeSigns(double a, double b) => a < 0 && b > 0 || a > 0 && b < 0;
    private static void Append(Span<double> roots, ref int count, double root)
    {
        if (count == 0 || Math.Abs(root - roots[count - 1]) > 16 * MachineEpsilon) roots[count++] = root;
    }
}
