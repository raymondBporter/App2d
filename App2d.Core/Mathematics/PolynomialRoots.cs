using App2d.Core.Validation;

namespace App2d.Core.Mathematics;

/// <summary>Real roots of linear through quartic polynomials on a finite interval.</summary>
public static class PolynomialRoots
{
    // Derivative isolation and bisection adapted from David Eberly's RootsPolynomial.h.
    // Copyright (c) 1998-2026 David Eberly, Geometric Tools, Redmond WA 98052.
    // Distributed under the Boost Software License 1.0; see THIRD-PARTY-NOTICES.md.
    private const double EvaluationTolerance = 16d * 2.2204460492503131e-16;

    /// <summary>
    /// Writes sorted, distinct real roots in [minimum, maximum], returning the number written.
    /// Repeated roots are reported once. Roots indistinguishable within double arithmetic may merge.
    /// </summary>
    /// <param name="coefficients">Finite coefficients in ascending order: c0 + c1*x + ... + c4*x^4.</param>
    /// <param name="minimum">Finite lower bound, inclusive.</param>
    /// <param name="maximum">Finite upper bound, inclusive and at least minimum.</param>
    /// <param name="roots">Space for at least the degree, excluding trailing zero coefficients.</param>
    /// <remarks>The identically zero polynomial has infinitely many roots and is rejected.</remarks>
    public static int FindRealRoots(ReadOnlySpan<double> coefficients, double minimum, double maximum, Span<double> roots)
    {
        ArgGuard.ThrowIfTooShort(coefficients, 1);
        ArgGuard.ThrowIfNotFinite(minimum);
        ArgGuard.ThrowIfNotFinite(maximum);
        ArgGuard.ThrowIf(maximum < minimum, "The interval must be ordered.", nameof(maximum));
        var scale = 0d;
        foreach (var coefficient in coefficients)
        {
            ArgGuard.ThrowIfNotFinite(coefficient, nameof(coefficients));
            scale = Math.Max(scale, Math.Abs(coefficient));
        }
        ArgGuard.ThrowIf(scale == 0d, "The zero polynomial has infinitely many roots.", nameof(coefficients));
        var degree = coefficients.Length - 1;
        while (degree > 0 && coefficients[degree] == 0d)
        {
            degree--;
        }

        ArgGuard.ThrowIf(degree > 4, "The degree must be at most four.", nameof(coefficients));
        ArgGuard.ThrowIf(roots.Length < degree, "The root buffer must hold at least the polynomial degree.", nameof(roots));
        if (degree == 0)
        {
            return 0;
        }

        Span<double> normalized = stackalloc double[degree + 1];
        for (var i = 0; i <= degree; i++)
        {
            normalized[i] = coefficients[i] / scale;
        }

        return Find(normalized, minimum, maximum, roots);
    }

    private static int Find(ReadOnlySpan<double> coefficients, double minimum, double maximum, Span<double> roots)
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
        var criticalCount = Find(derivative, minimum, maximum, critical);
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

            if (OppositeSigns(lowValue, highValue))
            {
                Append(roots, ref count, Bisect(coefficients, low, high, lowValue));
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

    private static double Bisect(ReadOnlySpan<double> coefficients, double low, double high, double lowValue)
    {
        // Most brackets take about 53 iterations; this also covers the full double exponent range.
        for (var i = 0; i < 2048; i++)
        {
            var middle = .5d * low + .5d * high;
            if (middle == low || middle == high)
            {
                return middle;
            }

            var value = Evaluate(coefficients, middle, detectRepeatedRoot: false);
            if (value == 0d)
            {
                return middle;
            }

            if (OppositeSigns(lowValue, value))
            {
                high = middle;
            }
            else
            {
                low = middle;
                lowValue = value;
            }
        }
        return .5d * low + .5d * high;
    }

    private static double Evaluate(ReadOnlySpan<double> coefficients, double x, bool detectRepeatedRoot)
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

    private static bool OppositeSigns(double a, double b) => a < 0d && b > 0d || a > 0d && b < 0d;

    private static void Append(Span<double> roots, ref int count, double root)
    {
        if (count == 0 || root != roots[count - 1])
        {
            roots[count++] = root;
        }
    }
}
