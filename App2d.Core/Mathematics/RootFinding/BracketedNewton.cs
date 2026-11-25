using System.Numerics;

namespace App2d.Core.Mathematics.RootFinding;

internal static class BracketedNewton
{
    internal interface IFunction<TSelf, TReal> where TSelf : struct, IFunction<TSelf, TReal>
    {
        static abstract TReal Evaluate(TSelf function, TReal parameter);
        static abstract TReal Derivative(TSelf function, TReal parameter);
        static abstract TReal Midpoint(TReal lower, TReal upper);
        static abstract bool HasConverged(TReal parameter, TReal next, TReal lower, TReal upper);
    }

    // Invert an increasing function with normalized input and output. On exhaustion, return the last estimate.
    internal static bool TrySolve<TFunction, TReal>(TFunction function, TReal target, int iterationLimit, out TReal parameter)
        where TFunction : struct, IFunction<TFunction, TReal>
        where TReal : IFloatingPointIeee754<TReal>
    {
        var lower = TReal.Zero;
        var upper = TReal.One;
        parameter = target;
        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var value = TFunction.Evaluate(function, parameter);
            if (value == target) return true;
            if (value < target) lower = parameter;
            else upper = parameter;

            var next = parameter - (value - target) / TFunction.Derivative(function, parameter);
            if (!(next > lower && next < upper)) next = TFunction.Midpoint(lower, upper);
            if (TFunction.HasConverged(parameter, next, lower, upper)) return true;
            parameter = next;
        }
        return false;
    }
}
