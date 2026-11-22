namespace App2d.Core.Mathematics.RootFinding;

internal static class Bisection
{
    public static double Bisect(Func<double, double> evaluate, double low, double high, double lowValue)
        => Bisect(evaluate, static (function, x) => function(x), low, high, lowValue);

    public static double Bisect<TState>(scoped TState state, Func<TState, double, double> evaluate, double low, double high, double lowValue)
        where TState : allows ref struct
    {
        // Most brackets take about 53 iterations; this also covers the full double exponent range.
        for (var i = 0; i < 2048; i++)
        {
            var middle = .5d * low + .5d * high;
            if (middle == low || middle == high)
            {
                return middle;
            }

            var value = evaluate(state, middle);
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