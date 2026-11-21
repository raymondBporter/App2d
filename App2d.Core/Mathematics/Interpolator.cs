using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>A reusable function from two endpoint values and time to a value. No timing or output is implicitly clamped.</summary>
public readonly record struct Interpolator<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private readonly Func<TValue, TValue, TReal, TValue> _function;
    private readonly ProgressMap<TReal>? _progress;

    public Interpolator(Func<TValue, TValue, TReal, TValue> function) : this(function, null) { }

    private Interpolator(Func<TValue, TValue, TReal, TValue> function, ProgressMap<TReal>? progress)
    {
        Ensure.ThrowIfNull(function);
        _function = function;
        _progress = progress;
        InputBegin = progress is null ? TReal.Zero : progress.InputBegin;
        InputEnd = progress is null ? TReal.One : progress.InputEnd;
    }

    public TReal InputBegin { get; private init; }
    public TReal InputEnd { get; private init; }

    public TValue Evaluate(TValue start, TValue end, TReal time) =>
        (_function ?? throw new InvalidOperationException("Initialize an interpolator with a function or a factory."))
            (start, end, _progress is null ? time : _progress.Sample(time));

    /// <summary>Clamps the input to the outer progress map's input span, or [0, 1] without one. Output remains unclamped.</summary>
    public TValue EvaluateClamped(TValue start, TValue end, TReal time) => Evaluate(start, end,
        TReal.Clamp(time, TReal.Min(InputBegin, InputEnd), TReal.Max(InputBegin, InputEnd)));

    /// <summary>Transforms time before this entire strategy, including any channel overrides.</summary>
    public Interpolator<TValue, TReal> WithProgress(ProgressMap<TReal> progress)
    {
        Ensure.ThrowIfNull(progress);
        return new(_function, _progress is null ? progress : progress.Then(_progress));
    }

    /// <summary>
    /// Replaces one channel after evaluating this strategy. The child reads the original endpoints and original time.
    /// Later overrides win on overlapping channels. A null child leaves the strategy unchanged.
    /// </summary>
    public Interpolator<TValue, TReal> With<TChannel>(InterpolationChannel<TValue, TChannel> channel,
        Interpolator<TChannel, TReal>? interpolation)
    {
        if (interpolation is not { } child) return this;
        Ensure.ThrowIfNull(channel);
        var parent = this;
        return new((start, end, time) => channel.Replace(parent.Evaluate(start, end, time),
            child.Evaluate(channel.Read(start), channel.Read(end), time))) { InputBegin = InputBegin, InputEnd = InputEnd };
    }

    /// <summary>Interpolates a scalar channel through its own progress map.</summary>
    public Interpolator<TValue, TReal> With(InterpolationChannel<TValue, TReal> channel, ProgressMap<TReal> progress) =>
        With(channel, Interpolation.Linear<TReal>().WithProgress(progress));

    /// <summary>Interpolates in another representation, then converts the result back.</summary>
    public Interpolator<TOutput, TReal> InCoordinates<TOutput>(Func<TOutput, TValue> toCoordinates, Func<TValue, TOutput> fromCoordinates)
    {
        Ensure.ThrowIfNull(toCoordinates);
        Ensure.ThrowIfNull(fromCoordinates);
        var source = this;
        return new((start, end, time) => fromCoordinates(source.Evaluate(toCoordinates(start), toCoordinates(end), time)))
            { InputBegin = InputBegin, InputEnd = InputEnd };
    }
}
