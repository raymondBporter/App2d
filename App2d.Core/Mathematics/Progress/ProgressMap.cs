using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.Serialization;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics.Progress;

/// <summary>
/// A scalar function with its derivative and, when available, an inverse for its authored branch.
/// Evaluate and composition do not implicitly clamp; EvaluateClamped explicitly clamps the input phase.
/// A derivative may be NaN at a discontinuity.
/// </summary>
[JsonConverter(typeof(ProgressMapJsonConverterFactory))]
public sealed partial class ProgressMap<TReal> : IDifferentiableCurve<TReal, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private readonly Func<TReal, TReal> _sample;
    private readonly Func<TReal, TReal> _derivative;
    private readonly bool _isConstant;
    // Bound built-in functions use reference-type targets. JSON can read their parameters here.
    internal object? FunctionState => _sample.Target;
    private static TReal Two => Real<TReal>.Two;
    private static TReal Half => Real<TReal>.Half;
    private static readonly TReal DifferenceStep = TReal.Cbrt(Real<TReal>.MachineEpsilon);

    public ProgressMap(Func<TReal, TReal> sample, Func<TReal, TReal> derivative,
        Func<TReal, TReal>? inverse = null, bool? isContinuous = null)
        : this(sample, derivative, inverse, isContinuous, TReal.Zero, TReal.One)
    {
    }

    private ProgressMap(Func<TReal, TReal> sample, Func<TReal, TReal> derivative,
        Func<TReal, TReal>? inverse, bool? isContinuous, TReal inputBegin, TReal inputEnd, bool isConstant = false)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(derivative);
        _sample = sample;
        _derivative = derivative;
        Inverse = inverse;
        IsContinuous = isContinuous;
        InputBegin = inputBegin;
        InputEnd = inputEnd;
        _isConstant = isConstant;
    }

    /// <summary>An inverse for the authored input span, or null when unavailable or not unique.</summary>
    public Func<TReal, TReal>? Inverse { get; }

    /// <summary>Directed endpoints used by EvaluateClamped. Ordinary easing curves use zero and one.</summary>
    public TReal InputBegin { get; }
    public TReal InputEnd { get; }

    /// <summary>Continuity of the function on the real line, or null when unknown. Sharp turns are continuous.</summary>
    public bool? IsContinuous { get; }

    /// <summary>The declared input span as an ordered interval; [0, 1] for an ordinary easing curve.</summary>
    public Interval<TReal> Domain => Interval<TReal>.Builder.Create(TReal.Min(InputBegin, InputEnd), TReal.Max(InputBegin, InputEnd));

    public TReal Evaluate(TReal phase) => _sample(phase);

    /// <summary>Clamps only the input to the declared span. The sampled value may still overshoot.</summary>
    public TReal EvaluateClamped(TReal phase) => Evaluate(TReal.Clamp(phase, TReal.Min(InputBegin, InputEnd), TReal.Max(InputBegin, InputEnd)));

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

    /// <summary>A constant value with zero derivative and no unique inverse.</summary>
    public static ProgressMap<TReal> Constant(TReal value) => Constant(value, TReal.Zero, TReal.One);

    internal static ProgressMap<TReal> Constant(TReal value, TReal inputBegin, TReal inputEnd)
    {
        Ensure.Finite(inputBegin);
        Ensure.Finite(inputEnd);
        var function = new ConstantFunction(value);
        return new(function.Evaluate, static _ => TReal.Zero, null, true, inputBegin, inputEnd, isConstant: true);
    }

    /// <summary>scale * x + offset. Zero scale creates a constant; nonzero scales carry an analytic inverse.</summary>
    public static ProgressMap<TReal> Affine(TReal scale, TReal offset)
    {
        Ensure.Finite(scale);
        Ensure.Finite(offset);
        if (scale == TReal.Zero) return Constant(offset);
        if (scale == TReal.One && offset == TReal.Zero) return Forward;
        var function = new AffineFunction(scale, offset);
        return new(function.Evaluate, function.Derivative, function.Invert, isContinuous: true);
    }

    /// <summary>Maps begin to zero and end to one without requiring ordered endpoints.</summary>
    public static ProgressMap<TReal> Normalize(TReal begin, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(end);
        if (begin == end) return Constant(TReal.Zero, begin, end);
        var function = new NormalizeFunction(begin, end);
        return new(function.Evaluate, function.Derivative, function.Invert, true, begin, end);
    }

    /// <summary>Feeds a directed input span into this normalized curve. A collapsed span samples its beginning.</summary>
    public ProgressMap<TReal> FromRange(TReal begin, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(end);
        if (begin == end) return Constant(Evaluate(TReal.Zero), begin, end);
        var function = new InputRangeFunction(this, begin, end);
        return new(function.Evaluate, function.Derivative, Inverse is null ? null : function.Invert,
            IsContinuous, begin, end, _isConstant);
    }

    /// <summary>Maps this curve's values through begin + value * (end - begin), preserving overshoot and input span.</summary>
    public ProgressMap<TReal> ToRange(TReal begin, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(end);
        if (begin == end) return Constant(begin, InputBegin, InputEnd);
        var function = new OutputRangeFunction(this, begin, end);
        return new(function.Evaluate, function.Derivative, Inverse is null ? null : function.Invert,
            IsContinuous, InputBegin, InputEnd, _isConstant);
    }

    private static TReal DivideByRange(TReal begin, TReal end, TReal value)
    {
        var width = end - begin;
        return TReal.IsFinite(width) ? value / width : value / Two / (end / Two - begin / Two);
    }

    private static TReal ScaleByRange(TReal begin, TReal end, TReal derivative)
    {
        var width = end - begin;
        return TReal.IsFinite(width) ? width * derivative : end * derivative - begin * derivative;
    }

    /// <summary>Apply this function and then the next. Derivatives use the chain rule; inverses run in reverse order.</summary>
    public ProgressMap<TReal> Then(ProgressMap<TReal> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        if (ReferenceEquals(this, Forward)) return next;
        if (ReferenceEquals(next, Forward)) return this;
        var function = new ComposedFunction(this, next);
        var isConstant = _isConstant || next._isConstant;
        return new(function.Evaluate, function.Derivative, Inverse is null || next.Inverse is null ? null : function.Invert,
            isConstant || IsContinuous == true && next.IsContinuous == true ? true : null,
            InputBegin, InputEnd, isConstant);
    }

    /// <summary>
    /// In keeps the curve; Out uses 1 - f(1 - t); InOut fits the curve and its reflection into the two halves.
    /// InOut joins continuously when f(1) = 1. Apply to normalized curves before FromRange/ToRange.
    /// </summary>
    public ProgressMap<TReal> WithDirection(EasingDirection direction)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (direction == EasingDirection.In || ReferenceEquals(this, Forward)) return this;
        if (_isConstant && direction == EasingDirection.Out) return Constant(TReal.One - Evaluate(TReal.Zero));

        if (direction == EasingDirection.Out)
        {
            var reflected = new EaseOutFunction(this);
            return new(reflected.Evaluate, reflected.Derivative, Inverse is null ? null : reflected.Invert, IsContinuous);
        }

        var joinsContinuously = Evaluate(TReal.One) == TReal.One;
        if (_isConstant && joinsContinuously) return Constant(Half);
        var joined = new EaseInOutFunction(this, joinsContinuously);
        return new(joined.Evaluate, joined.Derivative, joinsContinuously && Inverse is not null ? joined.Invert : null,
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
