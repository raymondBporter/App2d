using System.Numerics;

namespace App2d.Core.Mathematics.Progress;

/// <summary>
/// A scalar function with its derivative and, when available, its inverse on [0, 1].
/// Sampling and composition never clamp inputs or outputs. A derivative may be NaN at a discontinuity.
/// </summary>
public sealed class ProgressMap<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private readonly Func<TReal, TReal> _sample;
    private readonly Func<TReal, TReal> _derivative;
    private static TReal Two => Real<TReal>.Two;
    private static TReal Half => Real<TReal>.Half;
    private static readonly TReal DifferenceStep = TReal.Cbrt(Real<TReal>.MachineEpsilon);

    public ProgressMap(Func<TReal, TReal> sample, Func<TReal, TReal> derivative,
        Func<TReal, TReal>? inverse = null, bool? isContinuous = null)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(derivative);
        _sample = sample;
        _derivative = derivative;
        Inverse = inverse;
        IsContinuous = isContinuous;
    }

    /// <summary>An inverse on the normalized domain, or null when unavailable or not unique.</summary>
    public Func<TReal, TReal>? Inverse { get; }

    /// <summary>Continuity of the function on the real line, or null when unknown. Sharp turns are continuous.</summary>
    public bool? IsContinuous { get; }

    public TReal Sample(TReal phase) => _sample(phase);
    public TReal Derivative(TReal phase) => _derivative(phase);

    public static ProgressMap<TReal> Forward { get; } = new(x => x, _ => TReal.One, x => x, isContinuous: true);
    public static ProgressMap<TReal> Reverse { get; } = new(x => TReal.One - x, _ => -TReal.One, x => TReal.One - x, isContinuous: true);

    /// <summary>1 - |2t - 1|. At the midpoint the derivative follows the returning leg.</summary>
    public static ProgressMap<TReal> PingPong { get; } = new(
        x => x <= Half ? Two * x : Two * (TReal.One - x),
        x => x < Half ? Two : -Two, isContinuous: true);

    /// <summary>|2t - 1|. At the midpoint the derivative follows the returning leg.</summary>
    public static ProgressMap<TReal> ReversePingPong { get; } = new(
        x => x <= Half ? TReal.One - Two * x : Two * x - TReal.One,
        x => x < Half ? -Two : Two, isContinuous: true);

    public static ProgressMap<TReal> FromTravel(ProgressTravel travel) => travel switch
    {
        ProgressTravel.Forward => Forward,
        ProgressTravel.Reverse => Reverse,
        ProgressTravel.PingPong => PingPong,
        ProgressTravel.ReversePingPong => ReversePingPong,
        _ => throw new ArgumentOutOfRangeException(nameof(travel), travel, "Unknown progress travel.")
    };

    /// <summary>Apply this function and then the next. Derivatives use the chain rule; inverses run in reverse order.</summary>
    public ProgressMap<TReal> Then(ProgressMap<TReal> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (ReferenceEquals(this, Forward)) return next;
        if (ReferenceEquals(next, Forward)) return this;
        return new(
            phase => next.Sample(Sample(phase)),
            phase => next.Derivative(Sample(phase)) * Derivative(phase),
            Inverse is { } firstInverse && next.Inverse is { } nextInverse
                ? value => firstInverse(nextInverse(value)) : null,
            isContinuous: IsContinuous == true && next.IsContinuous == true ? true : null);
    }

    /// <summary>
    /// In keeps the curve; Out uses 1 - f(1 - t); InOut fits the curve and its reflection into the two halves.
    /// InOut joins continuously when f(1) = 1. This operation does not reverse travel.
    /// </summary>
    public ProgressMap<TReal> WithDirection(EasingDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (direction == EasingDirection.In || ReferenceEquals(this, Forward)) return this;

        if (direction == EasingDirection.Out)
            return new(
                phase => TReal.One - Sample(TReal.One - phase),
                phase => Derivative(TReal.One - phase),
                Inverse is { } inverse ? value => TReal.One - inverse(TReal.One - value) : null,
                IsContinuous);

        var joinsContinuously = Sample(TReal.One) == TReal.One;
        var inOutInverse = joinsContinuously ? Inverse : null;
        return new(
            phase => phase < Half ? Sample(Two * phase) / Two : TReal.One - Sample(Two * (TReal.One - phase)) / Two,
            phase => Derivative(phase < Half ? Two * phase : Two * (TReal.One - phase)),
            inOutInverse is { } inverseHalf ? value => value < Half
                ? inverseHalf(Two * value) / Two : TReal.One - inverseHalf(Two * (TReal.One - value)) / Two : null,
            isContinuous: IsContinuous == true ? joinsContinuously : IsContinuous);
    }

    /// <summary>
    /// Explicitly estimates a derivative with finite differences, using one-sided differences at normalized endpoints.
    /// The step uses the cube root of machine epsilon, scaled to the phase's magnitude. Continuity and invertibility remain unknown.
    /// </summary>
    public static ProgressMap<TReal> FromFunction(Func<TReal, TReal> sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        return new(sample, phase =>
        {
            var step = DifferenceStep * TReal.Max(TReal.One, TReal.Abs(phase));
            var lower = phase - step;
            var upper = phase + step;
            if (phase >= TReal.Zero && phase <= TReal.One)
            {
                lower = TReal.Max(TReal.Zero, lower);
                upper = TReal.Min(TReal.One, upper);
            }
            return upper == lower ? TReal.Zero : (sample(upper) - sample(lower)) / (upper - lower);
        });
    }
}
