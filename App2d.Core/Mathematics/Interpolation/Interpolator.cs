using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Serialization;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Mathematics;

/// <summary>A reusable function from two endpoint values and time to a value. No timing or output is implicitly clamped.</summary>
[JsonConverter(typeof(InterpolatorJsonConverterFactory))]
public readonly record struct Interpolator<TValue, TReal> where TReal : IFloatingPointIeee754<TReal>
{
    private readonly Func<TValue, TValue, TReal, TValue> _function;
    internal InterpolationKind Kind { get; }
    internal object? FunctionState => _function?.Target;
    internal ProgressMap<TReal>? Progress { get; }
    internal bool IsInitialized => _function is not null;
    internal Interpolator<TValue, TReal> FunctionOnly => new(_function, null, Kind);

    public Interpolator(Func<TValue, TValue, TReal, TValue> function) : this(function, InterpolationKind.Custom) { }

    internal Interpolator(Func<TValue, TValue, TReal, TValue> function, InterpolationKind kind) : this(function, null, kind) { }

    private Interpolator(Func<TValue, TValue, TReal, TValue> function, ProgressMap<TReal>? progress, InterpolationKind kind)
    {
        Ensure.ThrowIfNull(function);
        _function = function;
        Progress = progress;
        Kind = kind;
        InputBegin = progress is null ? TReal.Zero : progress.InputBegin;
        InputEnd = progress is null ? TReal.One : progress.InputEnd;
    }

    public TReal InputBegin { get; private init; }
    public TReal InputEnd { get; private init; }

    public TValue Evaluate(TValue start, TValue end, TReal time) =>
        (_function ?? throw new InvalidOperationException("Initialize an interpolator with a function or a factory."))
            (start, end, Progress is null ? time : Progress.Evaluate(time));

    /// <summary>Clamps the input to the outer progress map's input span, or [0, 1] without one. Output remains unclamped.</summary>
    public TValue EvaluateClamped(TValue start, TValue end, TReal time) => Evaluate(start, end,
        TReal.Clamp(time, TReal.Min(InputBegin, InputEnd), TReal.Max(InputBegin, InputEnd)));

    /// <summary>Transforms time before this entire strategy, including any channel overrides.</summary>
    public Interpolator<TValue, TReal> WithProgress(ProgressMap<TReal> progress)
    {
        Ensure.ThrowIfNull(progress);
        return new(_function, Progress is null ? progress : progress.Then(Progress), Kind);
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
        var function = new ChannelInterpolation<TValue, TChannel, TReal>(this, channel, child);
        return new(function.Evaluate) { InputBegin = InputBegin, InputEnd = InputEnd };
    }

    /// <summary>Interpolates a scalar channel through its own progress map.</summary>
    public Interpolator<TValue, TReal> With(InterpolationChannel<TValue, TReal> channel, ProgressMap<TReal> progress) =>
        With(channel, Interpolation.Linear<TReal>().WithProgress(progress));

    /// <summary>Interpolates in another representation, then converts the result back.</summary>
    public Interpolator<TOutput, TReal> InCoordinates<TOutput>(Func<TOutput, TValue> toCoordinates, Func<TValue, TOutput> fromCoordinates)
    {
        Ensure.ThrowIfNull(toCoordinates);
        Ensure.ThrowIfNull(fromCoordinates);
        var function = new CoordinateInterpolation<TOutput, TValue, TReal>(this, toCoordinates, fromCoordinates);
        return new(function.Evaluate)
        {
            InputBegin = InputBegin,
            InputEnd = InputEnd
        };
    }
}

internal enum InterpolationKind : byte
{
    Custom, Default, Linear, Hold, ShortestAngle
}
