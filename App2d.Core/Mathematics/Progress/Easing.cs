using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Base easing curves. Direction, travel, playback, and value interpolation are independent operations.</summary>
public static class Easing<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private static TReal Two => Real<TReal>.Two;
    private static TReal Half => Real<TReal>.Half;
    private const int InverseToleranceScale = 4;
    private static readonly TReal InverseTolerance = TReal.CreateChecked(InverseToleranceScale) * Real<TReal>.MachineEpsilon;

    public static readonly ProgressMap<TReal> Linear = ProgressMap<TReal>.Forward;
    public static readonly ProgressMap<TReal> Smooth = CreateSmoothStep(1);
    public static readonly ProgressMap<TReal> Smoother = CreateSmoothStep(2);
    public static readonly ProgressMap<TReal> Smoothest = CreateSmoothStep(3);

    public static readonly ProgressMap<TReal> Sine = new(
        x => x == TReal.One ? TReal.One : TReal.One - TReal.Cos(x * TReal.Pi / Two),
        x => TReal.Pi / Two * TReal.Sin(x * TReal.Pi / Two),
        x => Two / TReal.Pi * TReal.Acos(TReal.One - x), isContinuous: true);

    /// <summary>Holds zero before phase 1, then jumps to one. Its derivative is NaN at the jump; no unique inverse exists.</summary>
    public static readonly ProgressMap<TReal> Step = new(
        x => TReal.IsNaN(x) ? TReal.NaN : x < TReal.One ? TReal.Zero : TReal.One,
        x => TReal.IsNaN(x) || x == TReal.One ? TReal.NaN : TReal.Zero, isContinuous: false);

    // Animation resolves these on every sampled segment; create the directed curves only once.
    private static readonly Dictionary<(EasingCurve, EasingDirection), ProgressMap<TReal>> Presets = CreatePresets();

    public static ProgressMap<TReal> FromCurve(EasingCurve curve, EasingDirection direction = EasingDirection.In)
    {
        if (Presets.TryGetValue((curve, direction), out var map)) return map;
        if (!Enum.IsDefined(curve)) throw new ArgumentOutOfRangeException(nameof(curve), curve, "Unknown easing curve.");
        throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown easing direction.");
    }

    private static Dictionary<(EasingCurve, EasingDirection), ProgressMap<TReal>> CreatePresets()
    {
        var presets = new Dictionary<(EasingCurve, EasingDirection), ProgressMap<TReal>>();
        foreach (var curve in Enum.GetValues<EasingCurve>())
        {
            var basis = curve switch
            {
                EasingCurve.Linear => Linear,
                EasingCurve.Smooth => Smooth,
                EasingCurve.Smoother => Smoother,
                EasingCurve.Smoothest => Smoothest,
                EasingCurve.Quad => Power(2),
                EasingCurve.Cubic => Power(3),
                EasingCurve.Quart => Power(4),
                EasingCurve.Quint => Power(5),
                EasingCurve.Sine => Sine,
                EasingCurve.Step => Step,
                _ => throw new ArgumentOutOfRangeException(nameof(curve))
            };
            foreach (var direction in Enum.GetValues<EasingDirection>())
                presets.Add((curve, direction), basis.WithDirection(direction));
        }
        return presets;
    }

    /// <summary>x raised to any positive integer degree. Degree 1 is linear; choose ease direction separately.</summary>
    public static ProgressMap<TReal> Power(int degree)
    {
        Ensure.Positive(degree);
        var exponent = TReal.CreateChecked(degree);
        return degree == 1 ? Linear : new(
            x => TReal.Pow(x, exponent), x => exponent * TReal.Pow(x, TReal.CreateChecked(degree - 1)),
            x => TReal.Pow(x, TReal.One / exponent), isContinuous: true);
    }

    /// <summary>Smoothstep orders 0 through 3: linear, cubic, quintic, and septic.</summary>
    public static ProgressMap<TReal> SmoothStep(int order = 1) => order switch
    {
        0 => Linear,
        1 => Smooth,
        2 => Smoother,
        3 => Smoothest,
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "Supported smoothstep orders are 0 through 3.")
    };

    /// <summary>
    /// The raw smoothstep polynomial, using the caller's floating-point precision. Neither input nor output is clamped.
    /// Use the map returned by SmoothStep for derivatives, inversion, and composition.
    /// </summary>
    public static TReal SmoothStepValue(TReal value, int order = 1) => order switch
    {
        0 => value,
        1 => value * value * (TReal.CreateChecked(3) - Two * value),
        2 => value * value * value * (value * (value * TReal.CreateChecked(6) - TReal.CreateChecked(15)) + TReal.CreateChecked(10)),
        3 => value * value * value * value *
            (TReal.CreateChecked(35) + value * (-TReal.CreateChecked(84) + value * (TReal.CreateChecked(70) - TReal.CreateChecked(20) * value))),
        _ => throw new ArgumentOutOfRangeException(nameof(order), order, "Supported smoothstep orders are 0 through 3.")
    };

    private static ProgressMap<TReal> CreateSmoothStep(int order) => new(
        x => SmoothStepValue(x, order),
        x => TReal.CreateChecked(order switch { 1 => 6, 2 => 30, _ => 140 }) * TReal.Pow(x * (TReal.One - x), TReal.CreateChecked(order)),
        order == 1 ? InverseCubicSmoothStep : x => InverseSmoothStep(x, order),
        isContinuous: true);

    private static TReal InverseCubicSmoothStep(TReal value) => value == TReal.Zero || value == TReal.One
        ? value : Half - TReal.Sin(TReal.Asin(TReal.One - Two * value) / TReal.CreateChecked(3));

    private static TReal InverseSmoothStep(TReal value, int order)
    {
        if (value == TReal.Zero || value == TReal.One) return value;
        if (!(value > TReal.Zero && value < TReal.One)) return TReal.NaN;

        var lower = TReal.Zero;
        var upper = TReal.One;
        while (upper - lower > InverseTolerance)
        {
            var middle = (lower + upper) / Two;
            if (SmoothStepValue(middle, order) < value) lower = middle;
            else upper = middle;
        }
        return (lower + upper) / Two;
    }
}
