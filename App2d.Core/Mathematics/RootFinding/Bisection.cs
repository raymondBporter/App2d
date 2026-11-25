namespace App2d.Core.Mathematics.RootFinding;

internal static class Bisection
{
    internal interface IEvaluator<TSelf> where TSelf : struct, IEvaluator<TSelf>
    {
        static abstract double Evaluate(ReadOnlySpan<double> coefficients, double parameter);
    }

    public static double Bisect<TEvaluator>(ReadOnlySpan<double> coefficients, double low, double high, double lowValue)
        where TEvaluator : struct, IEvaluator<TEvaluator>
    {
        // Most brackets take about 53 iterations; this also covers the full double exponent range.
        for (var i = 0; i < 2048; i++)
        {
            var middle = .5d * low + .5d * high;
            if (middle == low || middle == high)
            {
                return middle;
            }

            var value = TEvaluator.Evaluate(coefficients, middle);
            if (value == 0d)
            {
                return middle;
            }

            if (RootFinding.OppositeSigns(lowValue, value))
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
}
