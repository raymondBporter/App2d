using App2d.Core.Geometry.Curves;

namespace App2d.Core.Mathematics.RootFinding;

/// <summary>Roots on a restricted Bernstein polynomial interval. No curve tessellation is involved.</summary>
internal static class BernsteinRoots
{
    private static readonly double RootTolerance = 16 * Reald.MachineEpsilon;

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
        var lowValue = Evaluator.Evaluate(normalized, low);
        for (var i = 0; i <= criticalCount; i++)
        {
            var high = i == criticalCount ? to : critical[i];
            var highValue = Evaluator.Evaluate(normalized, high);
            if (NearZero(lowValue, degree)) Append(roots, ref count, low);
            if (!NearZero(lowValue, degree) && !NearZero(highValue, degree) && RootFinding.OppositeSigns(lowValue, highValue))
            {
                Append(roots, ref count, Bisection.Bisect<Evaluator>(normalized, low, high, lowValue));
            }
            low = high;
            lowValue = highValue;
        }
        if (NearZero(lowValue, degree)) Append(roots, ref count, low);
        return count;
    }

    private readonly struct Evaluator : Bisection.IEvaluator<Evaluator>
    {
        public static double Evaluate(ReadOnlySpan<double> coefficients, double parameter) =>
            BezierMath.EvaluateControls(coefficients, parameter);
    }

    private static bool NearZero(double value, int degree) => Math.Abs(value) <= RootTolerance * (degree + 1);
    private static void Append(Span<double> roots, ref int count, double root)
    {
        if (count == 0 || Math.Abs(root - roots[count - 1]) > RootTolerance) roots[count++] = root;
    }
}
