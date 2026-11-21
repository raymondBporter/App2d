using App2d.Core.Validation;
using App2d.Core.Geometry.Curves;
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

    /// <summary>A normalized cubic segment with two value controls. Control times are fixed at one third and two thirds.</summary>
    public static ProgressMap<TReal> Bezier(TReal firstControlValue, TReal secondControlValue) =>
        Bezier(TReal.Zero, firstControlValue, secondControlValue, TReal.One);

    /// <summary>
    /// A cubic value segment with linear time, analytic derivative, and straight endpoint extensions.
    /// Value controls may overshoot and endpoint values may be equal. No unique inverse is assumed.
    /// </summary>
    public static ProgressMap<TReal> Bezier(TReal begin, TReal firstControlValue, TReal secondControlValue, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(firstControlValue);
        Ensure.Finite(secondControlValue);
        Ensure.Finite(end);
        var three = TReal.CreateChecked(3);
        var beginningSlope = three * (firstControlValue - begin);
        var endingSlope = three * (end - secondControlValue);
        return new(
            phase => phase < TReal.Zero ? begin + phase * beginningSlope
                : phase > TReal.One ? end + (phase - TReal.One) * endingSlope
                : BezierMath.Cubic<TReal, TReal>(begin, firstControlValue, secondControlValue, end, phase),
            phase => phase < TReal.Zero ? beginningSlope : phase > TReal.One ? endingSlope
                : BezierMath.CubicDerivative<TReal, TReal>(begin, firstControlValue, secondControlValue, end, phase),
            isContinuous: true);
    }

    /// <summary>A normalized Bezier function with explicit control times and values, preserving nonuniform timing.</summary>
    public static ProgressMap<TReal> TimedBezier(TReal firstControlTime, TReal firstControlValue, TReal secondControlTime, TReal secondControlValue) =>
        TimedBezier(TReal.Zero, firstControlTime, firstControlValue, secondControlTime, secondControlValue, TReal.One);

    /// <summary>A Bezier value function, including absolute controls, analytic dValue/dTime, and straight endpoint extensions.</summary>
    public static ProgressMap<TReal> TimedBezier(TReal begin, TReal firstControlTime, TReal firstControlValue,
        TReal secondControlTime, TReal secondControlValue, TReal end)
    {
        Ensure.InRange(firstControlTime, TReal.Zero, TReal.One);
        Ensure.InRange(secondControlTime, TReal.Zero, TReal.One);
        Ensure.Finite(begin);
        Ensure.Finite(firstControlValue);
        Ensure.Finite(secondControlValue);
        Ensure.Finite(end);
        var three = TReal.CreateChecked(3);
        if (firstControlTime == TReal.One / three && secondControlTime == Two / three)
            return Bezier(begin, firstControlValue, secondControlValue, end);

        var beginningSlope = firstControlTime > TReal.Zero ? (firstControlValue - begin) / firstControlTime
            : secondControlTime > TReal.Zero ? (secondControlValue - begin) / secondControlTime : end - begin;
        var endingSlope = secondControlTime < TReal.One ? (end - secondControlValue) / (TReal.One - secondControlTime)
            : firstControlTime < TReal.One ? (end - firstControlValue) / (TReal.One - firstControlTime) : end - begin;
        return new(
            time => TimedBezierValue(begin, firstControlTime, firstControlValue, secondControlTime, secondControlValue, end, time),
            time => time < TReal.Zero ? beginningSlope : time > TReal.One ? endingSlope
                : BezierValueDerivative(BezierParameter(time, firstControlTime, secondControlTime),
                    begin, firstControlTime, firstControlValue, secondControlTime, secondControlValue, end),
            isContinuous: true);
    }

    // Shared by value interpolators whose endpoint values are supplied at evaluation time.
    internal static TReal TimedBezierValue(TReal begin, TReal firstTime, TReal firstValue,
        TReal secondTime, TReal secondValue, TReal end, TReal time)
    {
        if (time == TReal.Zero) return begin;
        if (time == TReal.One) return end;
        if (time < TReal.Zero)
        {
            var slope = firstTime > TReal.Zero ? (firstValue - begin) / firstTime
                : secondTime > TReal.Zero ? (secondValue - begin) / secondTime : end - begin;
            return begin + time * slope;
        }
        if (time > TReal.One)
        {
            var slope = secondTime < TReal.One ? (end - secondValue) / (TReal.One - secondTime)
                : firstTime < TReal.One ? (end - firstValue) / (TReal.One - firstTime) : end - begin;
            return end + (time - TReal.One) * slope;
        }
        var three = TReal.CreateChecked(3);
        var parameter = firstTime == TReal.One / three && secondTime == Two / three
            ? time : BezierParameter(time, firstTime, secondTime);
        return BezierMath.Cubic<TReal, TReal>(begin, firstValue, secondValue, end, parameter);
    }

    private static TReal BezierParameter(TReal time, TReal firstTime, TReal secondTime)
    {
        if (time == TReal.Zero || time == TReal.One || TReal.IsNaN(time)) return time;
        var lower = TReal.Zero;
        var upper = TReal.One;
        var parameter = time;
        // Safeguarded Newton steps converge quickly; bisection also handles flat or vertical time tangents.
        for (var iteration = 0; iteration < 2048; iteration++)
        {
            var value = BezierMath.Cubic<TReal, TReal>(TReal.Zero, firstTime, secondTime, TReal.One, parameter);
            if (value == time) return parameter;
            if (value < time) lower = parameter;
            else upper = parameter;
            var slope = BezierMath.CubicDerivative<TReal, TReal>(TReal.Zero, firstTime, secondTime, TReal.One, parameter);
            var next = parameter - (value - time) / slope;
            if (!(next > lower && next < upper)) next = lower + (upper - lower) / Two;
            if (next == parameter || next == lower || next == upper) return parameter;
            parameter = next;
        }
        return parameter;
    }

    private static TReal BezierValueDerivative(TReal parameter, TReal begin, TReal firstTime, TReal firstValue,
        TReal secondTime, TReal secondValue, TReal end)
    {
        var timeSlope = BezierMath.CubicDerivative<TReal, TReal>(TReal.Zero, firstTime, secondTime, TReal.One, parameter);
        var valueSlope = BezierMath.CubicDerivative<TReal, TReal>(begin, firstValue, secondValue, end, parameter);
        if (timeSlope != TReal.Zero || valueSlope != TReal.Zero) return valueSlope / timeSlope;

        // Cancel matching zero derivatives: a flattened parameterization can still describe an ordinary linear function.
        var six = TReal.CreateChecked(6);
        var three = TReal.CreateChecked(3);
        var timeSecond = six * ((secondTime - Two * firstTime) * (TReal.One - parameter) +
            (TReal.One - Two * secondTime + firstTime) * parameter);
        var valueSecond = six * ((secondValue - Two * firstValue + begin) * (TReal.One - parameter) +
            (end - Two * secondValue + firstValue) * parameter);
        if (timeSecond != TReal.Zero) return valueSecond / timeSecond;
        if (valueSecond != TReal.Zero)
            return parameter == TReal.Zero ? TReal.CopySign(TReal.PositiveInfinity, valueSecond)
                : parameter == TReal.One ? TReal.CopySign(TReal.PositiveInfinity, -valueSecond) : TReal.NaN;
        return (end - begin + three * (firstValue - secondValue)) / (TReal.One + three * (firstTime - secondTime));
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
