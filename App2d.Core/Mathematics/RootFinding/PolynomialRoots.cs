using App2d.Core.Validation;

namespace App2d.Core.Mathematics.RootFinding;

/// <summary>Real roots of linear through quintic polynomials on a finite interval.</summary>
public static class PolynomialRoots
{
    // Derivative isolation and bisection adapted from David Eberly's RootsPolynomial.h.
    // Copyright (c) 1998-2026 David Eberly, Geometric Tools, Redmond WA 98052.
    // Distributed under the Boost Software License 1.0; see THIRD-PARTY-NOTICES.md.
    private static readonly double EvaluationTolerance = 16d * Reald.MachineEpsilon;

    /// <summary>
    /// Writes sorted, distinct real roots in [minimum, maximum], returning the number written.
    /// Repeated roots are reported once. Roots indistinguishable within double arithmetic may merge.
    /// </summary>
    /// <param name="coefficients">Finite coefficients in ascending order: c0 + c1*x + ... + c5*x^5.</param>
    /// <param name="minimum">Finite lower bound, inclusive.</param>
    /// <param name="maximum">Finite upper bound, inclusive and at least minimum.</param>
    /// <param name="roots">Space for at least the degree, excluding trailing zero coefficients.</param>
    /// <remarks>The identically zero polynomial has infinitely many roots and is rejected.</remarks>
    public static int FindRealRoots(ReadOnlySpan<double> coefficients, double minimum, double maximum, Span<double> roots)
        => FindRealRoots(coefficients, minimum, maximum, roots, useCubicFormula: false);

    /// <summary>Writes the sorted, distinct roots of a*x^2 + b*x + c in the inclusive interval.</summary>
    public static int SolveQuadratic(double a, double b, double c, double minimum, double maximum, Span<double> roots)
        => FindRealRoots([c, b, a], minimum, maximum, roots);

    /// <summary>Writes the sorted, distinct roots of a*x^4 + b*x^3 + c*x^2 + d*x + e in the inclusive interval.</summary>
    public static int SolveQuartic(double a, double b, double c, double d, double e, double minimum, double maximum, Span<double> roots)
        => FindRealRoots([e, d, c, b, a], minimum, maximum, roots, useCubicFormula: true);

    private static int FindRealRoots(ReadOnlySpan<double> coefficients, double minimum, double maximum, Span<double> roots, bool useCubicFormula)
    {
        Ensure.ThrowIfTooShort(coefficients, 1);
        Ensure.Finite(minimum);
        Ensure.Finite(maximum);
        Ensure.GreaterThanOrEqual(maximum, minimum);
        var scale = 0d;
        foreach (var coefficient in coefficients)
        {
            Ensure.Finite(coefficient, nameof(coefficients));
            scale = Math.Max(scale, Math.Abs(coefficient));
        }
        Ensure.NonZero(scale);
        var degree = coefficients.Length - 1;
        while (degree > 0 && coefficients[degree] == 0d)
        {
            degree--;
        }

        Ensure.LessThanOrEqual(degree, 5);
        Ensure.ThrowIf(roots.Length < degree, "The root buffer must hold at least the polynomial degree.", nameof(roots));
        if (degree == 0)
        {
            return 0;
        }

        Span<double> normalized = stackalloc double[degree + 1];
        for (var i = 0; i <= degree; i++)
        {
            normalized[i] = coefficients[i] / scale;
        }

        return Find(normalized, minimum, maximum, roots, useCubicFormula);
    }

    private static int Find(ReadOnlySpan<double> coefficients, double minimum, double maximum, Span<double> roots, bool useCubicFormula = false)
    {
        var degree = coefficients.Length - 1;
        if (degree == 1)
        {
            var root = -coefficients[0] / coefficients[1];
            if (root < minimum || root > maximum || !double.IsFinite(root))
            {
                return 0;
            }

            roots[0] = root;
            return 1;
        }

        if (degree == 2)
        {
            return FindQuadratic(coefficients, minimum, maximum, roots);
        }

        Span<double> derivative = stackalloc double[degree];
        for (var i = 1; i <= degree; i++)
        {
            derivative[i - 1] = coefficients[i] * i;
        }

        Span<double> critical = stackalloc double[degree - 1];
        if (!useCubicFormula || degree != 4 || !TryFindCubicCriticalPoints(derivative, critical, out var criticalCount))
        {
            criticalCount = Find(derivative, minimum, maximum, critical);
        }
        Span<double> partition = stackalloc double[degree + 1];
        var partitionCount = 1;
        partition[0] = minimum;
        for (var i = 0; i < criticalCount; i++)
        {
            if (critical[i] > minimum && critical[i] < maximum)
            {
                partition[partitionCount++] = critical[i];
            }
        }

        partition[partitionCount++] = maximum;

        var count = 0;
        var low = partition[0];
        var lowValue = Evaluate(coefficients, low, detectRepeatedRoot: true);
        for (var i = 1; i < partitionCount; i++)
        {
            var high = partition[i];
            var highValue = Evaluate(coefficients, high, detectRepeatedRoot: true);
            if (lowValue == 0d)
            {
                Append(roots, ref count, low);
            }

            if (RootFinding.OppositeSigns(lowValue, highValue))
            {
                Append(roots, ref count, Bisection.Bisect<Evaluator>(coefficients, low, high, lowValue));
            }

            low = high;
            lowValue = highValue;
        }
        if (lowValue == 0d)
        {
            Append(roots, ref count, low);
        }

        return count;
    }

    private readonly struct Evaluator : Bisection.IEvaluator<Evaluator>
    {
        public static double Evaluate(ReadOnlySpan<double> coefficients, double parameter) =>
            PolynomialRoots.Evaluate(coefficients, parameter, detectRepeatedRoot: false);
    }

    private static bool TryFindCubicCriticalPoints(ReadOnlySpan<double> derivative, Span<double> roots, out int count)
    {
        count = 0;
        // Restrict the analytic path to derivatives with modest monic coefficients.
        var b = derivative[2] / derivative[3];
        var c = derivative[1] / derivative[3];
        var d = derivative[0] / derivative[3];
        if (!(Math.Abs(b) <= 4d && Math.Abs(c) <= 4d && Math.Abs(d) <= 4d))
        {
            return false;
        }

        // Shift x = y - b/3 to remove the quadratic term.
        var shift = b / 3d;
        var p = Math.FusedMultiplyAdd(-b, shift, c);
        var q = Math.FusedMultiplyAdd(shift, 2d * shift * shift - c, d);
        if (!TrySolveDepressedCubic(p, q, roots, out count))
        {
            return false;
        }

        for (var i = 0; i < count; i++)
        {
            roots[i] -= shift;
            if (Evaluate(derivative, roots[i], detectRepeatedRoot: true) != 0d)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TrySolveDepressedCubic(double p, double q, Span<double> roots, out int count)
    {
        var thirdP = p / 3d;
        var halfQ = q / 2d;
        var qSquared = halfQ * halfQ;
        var pCubed = thirdP * thirdP * thirdP;
        var discriminant = qSquared + pCubed;
        count = 0;
        // Fall back to derivative isolation near multiple roots or underflow.
        if (qSquared + Math.Abs(pCubed) < 1e-280 ||
            Math.Abs(discriminant) <= EvaluationTolerance * (qSquared + Math.Abs(pCubed)))
        {
            return false;
        }

        if (discriminant > 0d)
        {
            var u = Math.Cbrt(-halfQ - Math.CopySign(Math.Sqrt(discriminant), halfQ));
            roots[0] = u - thirdP / u;
            count = 1;
            return true;
        }

        var radius = Math.Sqrt(-thirdP);
        var angle = Math.Acos(Math.Clamp(-halfQ / (radius * radius * radius), -1d, 1d)) / 3d;
        roots[0] = 2d * radius * Math.Cos(angle + Math.Tau / 3d);
        roots[1] = 2d * radius * Math.Cos(angle - Math.Tau / 3d);
        roots[2] = 2d * radius * Math.Cos(angle);
        count = 3;
        return true;
    }

    private static int FindQuadratic(ReadOnlySpan<double> coefficients, double minimum, double maximum, Span<double> roots)
    {
        // Derivative equations have their own scale, independent of the original cubic or quartic.
        var scale = Math.Max(Math.Abs(coefficients[0]), Math.Max(Math.Abs(coefficients[1]), Math.Abs(coefficients[2])));
        if (scale == 0d)
        {
            return 0;
        }
        var a = coefficients[2] / scale;
        var b = coefficients[1] / scale;
        var c = coefficients[0] / scale;
        if (a == 0d)
        {
            return b == 0d ? 0 : Find(coefficients[..2], minimum, maximum, roots);
        }

        double first;
        double second;
        if (c == 0d)
        {
            // Factoring x avoids both division by zero and underflow of b*b for tiny nonzero roots.
            first = 0d;
            second = -b / a;
        }
        else
        {
            var product = 4d * a * c;
            var productError = Math.FusedMultiplyAdd(4d * a, c, -product);
            var discriminant = Math.FusedMultiplyAdd(b, b, -product) - productError;
            var vertex = -.5d * b / a;
            // Keep the numerical solver's repeated-root convention near a tangent.
            if (Math.Abs(discriminant) <= EvaluationTolerance * (3d * b * b + Math.Abs(product)) &&
                double.IsFinite(vertex) && Evaluate(coefficients, vertex, detectRepeatedRoot: true) == 0d)
            {
                first = second = vertex;
            }
            else
            {
                if (discriminant < 0d)
                {
                    return 0;
                }
                // Choose the sign that adds magnitudes; c/q recovers the root cancellation would lose.
                var q = -.5d * (b + Math.CopySign(Math.Sqrt(discriminant), b));
                first = q / a;
                second = c / q;
            }
        }
        if (first > second)
        {
            (first, second) = (second, first);
        }
        var count = 0;
        AppendQuadraticRoot(coefficients, first, minimum, maximum, roots, ref count);
        AppendQuadraticRoot(coefficients, second, minimum, maximum, roots, ref count);
        return count;
    }

    private static void AppendQuadraticRoot(ReadOnlySpan<double> coefficients, double root, double minimum, double maximum, Span<double> roots, ref int count)
    {
        if (!double.IsFinite(root))
        {
            return;
        }
        if (root < minimum || root > maximum)
        {
            var boundary = root < minimum ? minimum : maximum;
            if (Math.Abs(root - boundary) > EvaluationTolerance * Math.Max(Math.Abs(root), Math.Abs(boundary)) ||
                Evaluate(coefficients, boundary, detectRepeatedRoot: true) != 0d)
            {
                return;
            }
            root = boundary;
        }
        Append(roots, ref count, root);
    }

    public static double Evaluate(ReadOnlySpan<double> coefficients, double x, bool detectRepeatedRoot)
    {
        var degree = coefficients.Length - 1;
        var reciprocal = Math.Abs(x) > 1d;
        var argument = reciprocal ? 1d / x : x;
        var result = reciprocal ? coefficients[0] : coefficients[degree];
        var magnitude = Math.Abs(result);
        for (var step = 1; step <= degree; step++)
        {
            var coefficient = coefficients[reciprocal ? step : degree - step];
            result = Math.FusedMultiplyAdd(result, argument, coefficient);
            magnitude = magnitude * Math.Abs(argument) + Math.Abs(coefficient);
        }
        // At large x, evaluate p(x)/x^degree to keep sign tests finite.
        if (reciprocal && x < 0d && (degree & 1) != 0)
        {
            result = -result;
        }

        return detectRepeatedRoot && Math.Abs(result) <= EvaluationTolerance * magnitude ? 0d : result;
    }

    private static void Append(Span<double> roots, ref int count, double root)
    {
        if (count == 0 || root != roots[count - 1])
        {
            roots[count++] = root;
        }
    }
}
