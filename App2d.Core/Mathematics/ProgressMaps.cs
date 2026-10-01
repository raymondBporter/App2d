using App2d.Core.Validation;

namespace App2d.Core.Mathematics;

/// <summary>Reusable phase maps and composition for periodic curve motion.</summary>
public static class ProgressMaps
{
    public static IProgressMap Loop => Easing.Linear;

    /// <summary>0 to 1 and back to 0 in one cycle.</summary>
    public static IProgressMap PingPong { get; } = new Map(
        phase => phase <= 0.5d ? 2d * phase : 2d * (1d - phase),
        phase => phase < 0.5d ? 2d : -2d);

    /// <summary>Apply <paramref name="first"/> and then <paramref name="second"/>.</summary>
    public static IProgressMap Compose(IProgressMap first, IProgressMap second)
    {
        ArgGuard.ThrowIfNull(first);
        ArgGuard.ThrowIfNull(second);
        return new Map(
            phase => second.Sample(first.Sample(phase)),
            phase => second.Derivative(first.Sample(phase)) * first.Derivative(phase));
    }

    private sealed record Map(Func<double, double> SampleFunction,
        Func<double, double> DerivativeFunction) : IProgressMap
    {
        public double Sample(double phase) => SampleFunction(phase);
        public double Derivative(double phase) => DerivativeFunction(phase);
    }
}
