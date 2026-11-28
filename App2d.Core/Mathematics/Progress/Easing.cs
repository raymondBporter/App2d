using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.RootFinding;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Base easing curves. Direction, travel, playback, and value interpolation are independent operations.</summary>
public static class Easing<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private static TReal Two => Real<TReal>.Two;
    private static TReal Half => Real<TReal>.Half;

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

    // Reuse the directed maps when building motion from easing presets.
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

    /// <summary>
    /// A unit-height bell centred at <paramref name="center"/>, with its analytic slope. It is a profile for offsets and
    /// weights, not an easing: it returns toward zero on both sides and has no inverse.
    /// </summary>
    public static ProgressMap<TReal> Gaussian(TReal center, TReal sigma)
    {
        Ensure.Finite(center);
        Ensure.Positive(sigma);
        var function = new GaussianFunction(center, sigma);
        return new(function.Evaluate, function.Derivative, isContinuous: true);
    }

    /// <summary>x raised to any positive integer degree. Degree 1 is linear; choose ease direction separately.</summary>
    public static ProgressMap<TReal> Power(int degree)
    {
        Ensure.Positive(degree);
        if (degree == 1) return Linear;
        var function = new PowerFunction(degree);
        return new(function.Evaluate, function.Derivative, function.Invert, isContinuous: true);
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
        var function = new BezierFunction(new(begin, firstControlValue, secondControlValue, end));
        return new(function.Evaluate, function.Derivative, isContinuous: true);
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

        var function = new TimedBezierFunction(begin, firstControlTime, firstControlValue, secondControlTime, secondControlValue, end);
        return new(function.Evaluate, function.Derivative, isContinuous: true);
    }

    internal sealed class GaussianFunction(TReal center, TReal sigma)
    {
        private readonly TReal _scale = -TReal.One / (Two * sigma * sigma);
        internal TReal Center { get; } = center;
        internal TReal Sigma { get; } = sigma;

        internal TReal Evaluate(TReal x) => TReal.Exp((x - Center) * (x - Center) * _scale);
        internal TReal Derivative(TReal x) => Two * _scale * (x - Center) * Evaluate(x);
    }

    internal sealed class PowerFunction(int degree)
    {
        private readonly TReal _exponent = TReal.CreateChecked(degree);
        internal int Degree { get; } = degree;

        internal TReal Evaluate(TReal x) => TReal.Pow(x, _exponent);
        internal TReal Derivative(TReal x) => _exponent * TReal.Pow(x, TReal.CreateChecked(Degree - 1));
        internal TReal Invert(TReal x) => TReal.Pow(x, TReal.One / _exponent);
    }

    internal sealed class BezierFunction(CubicBezier<TReal, TReal> curve)
    {
        private readonly Extrapolated<CubicBezier<TReal, TReal>, TReal, TReal> _extended =
            new(curve, Extrapolation.Extend, Extrapolation.Extend);

        internal CubicBezier<TReal, TReal> Curve => _extended.Curve;
        internal TReal Evaluate(TReal time) => _extended.Evaluate(time);
        internal TReal Derivative(TReal time) => _extended.Derivative(time);
    }

    internal sealed class TimedBezierFunction(TReal begin, TReal firstTime, TReal firstValue,
        TReal secondTime, TReal secondValue, TReal end)
    {
        private readonly TReal _beginningSlope = BeginningSlope(begin, firstTime, firstValue, secondTime, secondValue, end);
        private readonly TReal _endingSlope = EndingSlope(begin, firstTime, firstValue, secondTime, secondValue, end);

        internal TReal Begin { get; } = begin;
        internal TReal FirstTime { get; } = firstTime;
        internal TReal FirstValue { get; } = firstValue;
        internal TReal SecondTime { get; } = secondTime;
        internal TReal SecondValue { get; } = secondValue;
        internal TReal End { get; } = end;

        internal TReal Evaluate(TReal time) => TimedBezierValue(Begin, FirstTime, FirstValue, SecondTime, SecondValue, End, time);
        internal TReal Derivative(TReal time) => time < TReal.Zero ? _beginningSlope : time > TReal.One ? _endingSlope
            : BezierValueDerivative(BezierParameter(time, FirstTime, SecondTime), Begin, FirstTime, FirstValue, SecondTime, SecondValue, End);
    }

    // Shared by value interpolators whose endpoint values are supplied at evaluation time.
    internal static TReal TimedBezierValue(TReal begin, TReal firstTime, TReal firstValue,
        TReal secondTime, TReal secondValue, TReal end, TReal time)
    {
        if (time == TReal.Zero) return begin;
        if (time == TReal.One) return end;
        if (time < TReal.Zero) return begin + time * BeginningSlope(begin, firstTime, firstValue, secondTime, secondValue, end);
        if (time > TReal.One) return end + (time - TReal.One) * EndingSlope(begin, firstTime, firstValue, secondTime, secondValue, end);
        var three = TReal.CreateChecked(3);
        var parameter = firstTime == TReal.One / three && secondTime == Two / three
            ? time : BezierParameter(time, firstTime, secondTime);
        return BezierMath.Cubic(begin, firstValue, secondValue, end, parameter);
    }

    // dValue/dTime where the function leaves and arrives. A control at the endpoint's own time gives no slope there,
    // so the next control is used, and then the chord.
    private static TReal BeginningSlope(TReal begin, TReal firstTime, TReal firstValue, TReal secondTime, TReal secondValue, TReal end) =>
        firstTime > TReal.Zero ? (firstValue - begin) / firstTime : secondTime > TReal.Zero ? (secondValue - begin) / secondTime : end - begin;

    private static TReal EndingSlope(TReal begin, TReal firstTime, TReal firstValue, TReal secondTime, TReal secondValue, TReal end) =>
        secondTime < TReal.One ? (end - secondValue) / (TReal.One - secondTime) : firstTime < TReal.One ? (end - firstValue) / (TReal.One - firstTime) : end - begin;

    private static TReal BezierParameter(TReal time, TReal firstTime, TReal secondTime)
    {
        if (time == TReal.Zero || time == TReal.One || TReal.IsNaN(time)) return time;
        // The normalized time coordinate is itself a cubic Bezier curve, so the solver takes it directly.
        BracketedNewton.TrySolve(new CubicBezier<TReal, TReal>(TReal.Zero, firstTime, secondTime, TReal.One), time, 2048, out var parameter);
        return parameter;
    }

    private static TReal BezierValueDerivative(TReal parameter, TReal begin, TReal firstTime, TReal firstValue,
        TReal secondTime, TReal secondValue, TReal end)
    {
        var timeSlope = BezierMath.CubicDerivative(TReal.Zero, firstTime, secondTime, TReal.One, parameter);
        var valueSlope = BezierMath.CubicDerivative(begin, firstValue, secondValue, end, parameter);
        if (timeSlope != TReal.Zero || valueSlope != TReal.Zero) return valueSlope / timeSlope;

        // Cancel matching zero derivatives: a flattened parameterization can still describe an ordinary linear function.
        var three = TReal.CreateChecked(3);
        var timeSecond = BezierMath.CubicSecondDerivative(TReal.Zero, firstTime, secondTime, TReal.One, parameter);
        var valueSecond = BezierMath.CubicSecondDerivative(begin, firstValue, secondValue, end, parameter);
        if (timeSecond != TReal.Zero) return valueSecond / timeSecond;
        if (valueSecond != TReal.Zero)
        {
            return parameter == TReal.Zero ? TReal.CopySign(TReal.PositiveInfinity, valueSecond)
                : parameter == TReal.One ? TReal.CopySign(TReal.PositiveInfinity, -valueSecond) : TReal.NaN;
        }

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

    private static ProgressMap<TReal> CreateSmoothStep(int order)
    {
        var polynomial = new SmoothStepPolynomial(order);
        return new(polynomial.Evaluate, polynomial.Derivative, order == 1 ? InverseCubicSmoothStep : polynomial.Invert, isContinuous: true);
    }

    private static TReal InverseCubicSmoothStep(TReal value) => value == TReal.Zero || value == TReal.One
        ? value : Half - TReal.Sin(TReal.Asin(TReal.One - Two * value) / TReal.CreateChecked(3));

    private sealed class SmoothStepPolynomial(int order) : IDifferentiableCurve<TReal, TReal>
    {
        public Interval<TReal> Domain => Interval.UnitInterval<TReal>();
        public TReal Evaluate(TReal t) => SmoothStepValue(t, order);
        public TReal Derivative(TReal t) => TReal.CreateChecked(order switch { 1 => 6, 2 => 30, _ => 140 }) * TReal.Pow(t * (TReal.One - t), TReal.CreateChecked(order));

        // The higher orders use the shared monotonic solver with this same polynomial.
        internal TReal Invert(TReal value)
        {
            if (value == TReal.Zero || value == TReal.One) return value;
            if (!(value > TReal.Zero && value < TReal.One)) return TReal.NaN;
            BracketedNewton.TrySolve(this, value, 2048, out var phase);
            return phase;
        }
    }
}
