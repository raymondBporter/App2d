using App2d.Core.Timing;
using App2d.Core.Validation;

namespace App2d.Core.Animation;

/// <summary>
/// An immutable sequence of animation frames with either uniform or per-frame timing.
/// Frames can be textures, shaders, values, or any other state a game wants to animate.
/// </summary>
public sealed class AnimationClip2D<TFrame>
{
    private readonly TFrame[] _frames;
    // Variable clips retain exact widths; subtracting rounded end times can lose short frame durations.
    private readonly float[]? _frameDurations;
    private readonly IReadOnlyList<float> _frameEndTimes;

    public AnimationClip2D(IEnumerable<TFrame> frames, float framesPerSecond, bool isLooping = true)
    {
        Ensure.ThrowIfNull(frames);
        Ensure.Positive(framesPerSecond);

        _frames = [.. frames];
        Ensure.ThrowIfTooShort(_frames.AsSpan(), 1, nameof(frames));
        Ensure.ThrowIfContainsNull(_frames.AsSpan(), nameof(frames));

        UniformFramesPerSecond = framesPerSecond;
        UniformFrameDuration = 1f / framesPerSecond;
        _frameEndTimes = new UniformSequence<float>(FrameDuration, FrameDuration, FrameCount);
        Duration = Ensure.LessThan(_frameEndTimes[^1], float.MaxValue, nameof(framesPerSecond));
        IsLooping = isLooping;
    }

    public AnimationClip2D(IEnumerable<TFrame> frames, IEnumerable<float> frameDurations, bool isLooping = true)
    {
        Ensure.ThrowIfNull(frames);
        Ensure.ThrowIfNull(frameDurations);

        _frames = [.. frames];
        float[] durations = [.. frameDurations];
        Ensure.ThrowIfTooShort(_frames.AsSpan(), 1, nameof(frames));
        Ensure.ThrowIfContainsNull(_frames.AsSpan(), nameof(frames));
        Ensure.ThrowIf(durations.Length != _frames.Length, "Frame durations must contain exactly one value for every frame.", nameof(frameDurations));
        for (var index = 0; index < durations.Length; index++)
        {
            if (!NumericValidation.IsPositive(durations[index]))
                Ensure.ThrowOutOfRange(durations[index], $"Frame duration at index {index} must be positive and finite.", nameof(frameDurations));
        }

        var firstDuration = durations[0];
        var hasUniformFrameDuration = durations.All(value => value == firstDuration);
        UniformFramesPerSecond = hasUniformFrameDuration ? 1f / firstDuration : null;
        UniformFrameDuration = hasUniformFrameDuration ? firstDuration : null;
        _frameDurations = hasUniformFrameDuration ? null : durations;
        _frameEndTimes = hasUniformFrameDuration
            ? new UniformSequence<float>(firstDuration, firstDuration, FrameCount)
            : CreateFrameEndTimes(durations);
        Duration = Ensure.LessThan(_frameEndTimes[^1], float.MaxValue, nameof(frameDurations));
        IsLooping = isLooping;
    }

    public IReadOnlyList<TFrame> Frames => _frames;
    public int FrameCount => _frames.Length;
    public bool HasUniformFrameDuration => UniformFrameDuration is not null;
    public float? UniformFramesPerSecond { get; }
    public float? UniformFrameDuration { get; }
    public float FramesPerSecond => UniformFramesPerSecond ?? throw Ensure.Create("This animation clip uses per-frame durations.");
    public float FrameDuration => UniformFrameDuration ?? throw Ensure.Create("This animation clip uses per-frame durations.");
    public float Duration { get; }
    public bool IsLooping { get; }

    public TFrame this[int index] => _frames[index];

    public float GetFrameDuration(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, FrameCount);
        return _frameDurations?[index] ?? FrameDuration;
    }

    /// <summary>Samples the same loop/hold rules as a player without creating or advancing one.</summary>
    public TFrame Sample(double seconds) => this[GetFrameIndexAtTime((float)PlaybackClock.SampleTime(seconds, Duration,
        IsLooping ? PlaybackEndMode.Loop : PlaybackEndMode.Hold))];

    public int GetFrameIndexAtTime(float elapsedSeconds)
    {
        Ensure.NonNegative(elapsedSeconds);
        return Math.Min(OrderedSearch.UpperBound(_frameEndTimes, elapsedSeconds), FrameCount - 1);
    }

    private static float[] CreateFrameEndTimes(float[] durations)
    {
        var endTimes = new float[durations.Length];
        double elapsed = 0d;
        for (var index = 0; index < durations.Length; index++)
        {
            elapsed += durations[index];
            Ensure.LessThan((float)elapsed, float.MaxValue, nameof(durations));
            endTimes[index] = (float)elapsed;
        }
        return endTimes;
    }
}
