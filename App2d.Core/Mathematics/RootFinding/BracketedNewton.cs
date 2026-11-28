using App2d.Core.Mathematics.Curves;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics.RootFinding;

public static class BracketedNewton
{
    /// <summary>
    /// Inverts an increasing function with normalized input and output: finds the parameter in [0, 1] where the
    /// function equals the target. Takes any differentiable scalar curve, including a progress map or a chain of
    /// curve operations. A Newton step that leaves the bracket is replaced by bisection. Stops when the estimate
    /// no longer moves; on exhaustion returns false with the last estimate.
    /// <para>
    /// The target must lie in [0, 1]. The function must not decrease on [0, 1] and must reach the target there;
    /// that is the caller's promise and is not checked, because checking costs two more evaluations per solve.
    /// At a jump across the target, the result is the parameter of the jump.
    /// </para>
    /// </summary>
    public static bool TrySolve<TFunction, TReal>(TFunction function, TReal target, int iterationLimit, out TReal parameter)
        where TFunction : IDifferentiableCurve<TReal, TReal>
        where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.InRange(target, TReal.Zero, TReal.One);
        var lower = TReal.Zero;
        var upper = TReal.One;
        parameter = target;
        for (var iteration = 0; iteration < iterationLimit; iteration++)
        {
            var value = function.Evaluate(parameter);
            if (value == target) return true;
            if (value < target) lower = parameter;
            else upper = parameter;

            var next = parameter - (value - target) / function.Derivative(parameter);
            if (!(next > lower && next < upper)) next = lower + (upper - lower) / Real<TReal>.Two;
            if (next == parameter || next == lower || next == upper) return true;
            parameter = next;
        }
        return false;
    }
}
