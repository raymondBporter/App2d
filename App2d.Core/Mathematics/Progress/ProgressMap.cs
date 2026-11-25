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
public sealed class ProgressMap<TReal> : IDifferentiableCurve<TReal, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private readonly Func<TReal, TReal> _sample;
    private readonly Func<TReal, TReal> _derivative;
    private readonly bool _isConstant;
    internal ProgressRecipe<TReal>? Recipe { get; private set; }
    internal ProgressMap<TReal> WithRecipe(ProgressRecipe<TReal>? recipe) { Recipe = recipe; return this; }
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

    public TReal Evaluate(TReal phase) => _sample(phase);

    /// <summary>Clamps only the input to the declared span. The sampled value may still overshoot.</summary>
    public TReal EvaluateClamped(TReal phase) => Evaluate(TReal.Clamp(phase, TReal.Min(InputBegin, InputEnd), TReal.Max(InputBegin, InputEnd)));

    public TReal Derivative(TReal phase) => _derivative(phase);

    public static ProgressMap<TReal> Forward { get; } = new ProgressMap<TReal>(x => x, _ => TReal.One, x => x, isContinuous: true)
        .WithRecipe(new(ProgressOperation.Linear, []));
    public static ProgressMap<TReal> Reverse { get; } = new ProgressMap<TReal>(x => TReal.One - x, _ => -TReal.One, x => TReal.One - x, isContinuous: true)
        .WithRecipe(new(ProgressOperation.Reverse, []));

    /// <summary>1 - |2t - 1|. At the midpoint the derivative follows the returning leg.</summary>
    public static ProgressMap<TReal> PingPong { get; } = new ProgressMap<TReal>(
        x => x <= Half ? Two * x : Two * (TReal.One - x),
        x => x < Half ? Two : -Two, isContinuous: true).WithRecipe(new(ProgressOperation.PingPong, []));

    /// <summary>|2t - 1|. At the midpoint the derivative follows the returning leg.</summary>
    public static ProgressMap<TReal> ReversePingPong { get; } = new ProgressMap<TReal>(
        x => x <= Half ? TReal.One - Two * x : Two * x - TReal.One,
        x => x < Half ? -Two : Two, isContinuous: true).WithRecipe(new(ProgressOperation.ReversePingPong, []));

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
        return new ProgressMap<TReal>(_ => value, _ => TReal.Zero, null, true, inputBegin, inputEnd, isConstant: true)
            .WithRecipe(new(ProgressOperation.Constant, [value, inputBegin, inputEnd]));
    }

    /// <summary>scale * x + offset. Zero scale creates a constant; nonzero scales carry an analytic inverse.</summary>
    public static ProgressMap<TReal> Affine(TReal scale, TReal offset)
    {
        Ensure.Finite(scale);
        Ensure.Finite(offset);
        if (scale == TReal.Zero) return Constant(offset);
        if (scale == TReal.One && offset == TReal.Zero) return Forward;
        return new ProgressMap<TReal>(x => scale * x + offset, _ => scale, y => (y - offset) / scale, isContinuous: true)
            .WithRecipe(new(ProgressOperation.Affine, [scale, offset]));
    }

    /// <summary>Maps begin to zero and end to one without requiring ordered endpoints.</summary>
    public static ProgressMap<TReal> Normalize(TReal begin, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(end);
        if (begin == end) return Constant(TReal.Zero, begin, end);
        var slope = DivideByRange(begin, end, TReal.One);
        return new ProgressMap<TReal>(x => Interval.InverseLerp(begin, end, x), _ => slope,
            y => Interval.Lerp(begin, end, y), true, begin, end).WithRecipe(new(ProgressOperation.Normalize, [begin, end]));
    }

    /// <summary>Feeds a directed input span into this normalized curve. A collapsed span samples its beginning.</summary>
    public ProgressMap<TReal> FromRange(TReal begin, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(end);
        if (begin == end) return Constant(Evaluate(TReal.Zero), begin, end);
        return new ProgressMap<TReal>(x => Evaluate(Interval.InverseLerp(begin, end, x)),
            x => DivideByRange(begin, end, Derivative(Interval.InverseLerp(begin, end, x))),
            Inverse is { } inverse ? y => Interval.Lerp(begin, end, inverse(y)) : null,
            IsContinuous, begin, end, _isConstant).WithRecipe(new(ProgressOperation.FromRange, [begin, end], Source: this));
    }

    /// <summary>Maps this curve's values through begin + value * (end - begin), preserving overshoot and input span.</summary>
    public ProgressMap<TReal> ToRange(TReal begin, TReal end)
    {
        Ensure.Finite(begin);
        Ensure.Finite(end);
        if (begin == end) return Constant(begin, InputBegin, InputEnd);
        return new ProgressMap<TReal>(x => Interval.Lerp(begin, end, Evaluate(x)),
            x => ScaleByRange(begin, end, Derivative(x)),
            Inverse is { } inverse ? y => inverse(Interval.InverseLerp(begin, end, y)) : null,
            IsContinuous, InputBegin, InputEnd, _isConstant).WithRecipe(new(ProgressOperation.ToRange, [begin, end], Source: this));
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
        return new ProgressMap<TReal>(
            phase => next.Evaluate(Evaluate(phase)),
            phase => _isConstant || next._isConstant ? TReal.Zero : next.Derivative(Evaluate(phase)) * Derivative(phase),
            Inverse is { } firstInverse && next.Inverse is { } nextInverse
                ? value => firstInverse(nextInverse(value)) : null,
            _isConstant || next._isConstant || IsContinuous == true && next.IsContinuous == true ? true : null,
            InputBegin, InputEnd, _isConstant || next._isConstant).WithRecipe(new(ProgressOperation.Then, [], Source: this, Next: next));
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
            return new ProgressMap<TReal>(
                phase => TReal.One - Evaluate(TReal.One - phase),
                phase => Derivative(TReal.One - phase),
                Inverse is { } inverse ? value => TReal.One - inverse(TReal.One - value) : null,
                IsContinuous).WithRecipe(new(ProgressOperation.Direction, [], Source: this, Direction: direction));
        }

        var joinsContinuously = Evaluate(TReal.One) == TReal.One;
        if (_isConstant && joinsContinuously) return Constant(Half);
        var inOutInverse = joinsContinuously ? Inverse : null;
        return new ProgressMap<TReal>(
            phase => phase < Half ? Evaluate(Two * phase) / Two : TReal.One - Evaluate(Two * (TReal.One - phase)) / Two,
            phase => phase == Half && !joinsContinuously ? TReal.NaN
                : Derivative(phase < Half ? Two * phase : Two * (TReal.One - phase)),
            inOutInverse is { } inverseHalf ? value => value < Half
                ? inverseHalf(Two * value) / Two : TReal.One - inverseHalf(Two * (TReal.One - value)) / Two : null,
            isContinuous: IsContinuous == true ? joinsContinuously : IsContinuous)
            .WithRecipe(new(ProgressOperation.Direction, [], Source: this, Direction: direction));
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
