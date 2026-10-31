namespace App2d.Core.Mathematics.Progress;

/// <summary>A progress function and its derivative. Neither inputs nor outputs are implicitly clamped.</summary>
public sealed class ProgressMap : IProgressMap
{
    private readonly Func<double, double> _sample;
    private readonly Func<double, double> _derivative;

    public ProgressMap(Func<double, double> sample, Func<double, double> derivative, ProgressMapKind? kind = null,
        EasingTravel? travel = null, bool? isContinuous = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(derivative);
        _sample = sample;
        _derivative = derivative;
        Kind = kind;
        Travel = travel;
        IsContinuous = isContinuous;
    }

    /// <summary>Present for built-in maps; custom functions need their own authored description.</summary>
    public ProgressMapKind? Kind { get; }
    /// <summary>Declared normalized travel, or null when an arbitrary function has not declared it.</summary>
    public EasingTravel? Travel { get; }

    /// <summary>Continuity on [0, 1], or null when unknown. A continuous map may still have sharp turns.</summary>
    public bool? IsContinuous { get; }

    internal ProgressMap Named(ProgressMapKind kind) => new(_sample, _derivative, kind, Travel, IsContinuous);

    public double Sample(double phase) => _sample(phase);
    public double Derivative(double phase) => _derivative(phase);

    /// <summary>
    /// Wraps a function with a numerical derivative. Uses one-sided differences at normalized endpoints,
    /// central differences elsewhere. Prefer an explicit derivative when exact velocities matter.
    /// </summary>
    public static ProgressMap FromFunction(Func<double, double> sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return new(sample, phase =>
        {
            const double step = 0.00001d;
            var lower = phase - step;
            var upper = phase + step;
            if (phase is >= 0d and <= 1d)
            {
                lower = Math.Max(0d, lower);
                upper = Math.Min(1d, upper);
            }
            return upper == lower ? 0d : (sample(upper) - sample(lower)) / (upper - lower);
        });
    }
}
