namespace App2d.Engine.Animation;

/// <summary>
/// An immutable sequence of animation frames with either uniform or per-frame timing.
/// Frames can be textures, shaders, values, or any other state a game wants to animate.
/// </summary>
public sealed class AnimationClip2D<TFrame>
{
    private readonly TFrame[] _frames;
    private readonly float[] _frameDurations;
    private readonly float[] _frameEndTimes;

    public AnimationClip2D(
        IEnumerable<TFrame> frames,
        float framesPerSecond,
        bool isLooping = true)
    {
        ArgGuard.ThrowIfNull(frames);
        ArgGuard.ThrowIfNotPositive(framesPerSecond);

        _frames = [.. frames];
        ArgGuard.ThrowIfTooShort(_frames.AsSpan(), 1, nameof(frames));
        ArgGuard.ThrowIfContainsNull(_frames.AsSpan(), nameof(frames));

        _frameDurations = new float[_frames.Length];
        Array.Fill(_frameDurations, 1f / framesPerSecond);
        _frameEndTimes = CreateFrameEndTimes(_frameDurations, out var duration);

        UniformFramesPerSecond = framesPerSecond;
        UniformFrameDuration = 1f / framesPerSecond;
        Duration = duration;
        IsLooping = isLooping;
    }

    public AnimationClip2D(
        IEnumerable<TFrame> frames,
        IEnumerable<float> frameDurations,
        bool isLooping = true)
    {
        ArgGuard.ThrowIfNull(frames);
        ArgGuard.ThrowIfNull(frameDurations);

        _frames = [.. frames];
        _frameDurations = [.. frameDurations];
        ArgGuard.ThrowIfTooShort(_frames.AsSpan(), 1, nameof(frames));
        ArgGuard.ThrowIfContainsNull(_frames.AsSpan(), nameof(frames));
        if (_frameDurations.Length != _frames.Length)
        {
            throw new ArgumentException(
                "Frame durations must contain exactly one value for every frame.",
                nameof(frameDurations));
        }
        for (var index = 0; index < _frameDurations.Length; index++)
        {
            var duration = _frameDurations[index];
            if (!float.IsFinite(duration) || duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(frameDurations),
                    duration,
                    $"Frame duration at index {index} must be positive and finite.");
            }
        }

        _frameEndTimes = CreateFrameEndTimes(_frameDurations, out var totalDuration);
        var firstDuration = _frameDurations[0];
        var hasUniformFrameDuration = _frameDurations.All(value => value == firstDuration);
        UniformFramesPerSecond = hasUniformFrameDuration ? 1f / firstDuration : null;
        UniformFrameDuration = hasUniformFrameDuration ? firstDuration : null;
        Duration = totalDuration;
        IsLooping = isLooping;
    }

    public IReadOnlyList<TFrame> Frames => _frames;
    public int FrameCount => _frames.Length;
    public bool HasUniformFrameDuration => UniformFrameDuration is not null;
    public float? UniformFramesPerSecond { get; }
    public float? UniformFrameDuration { get; }
    public float FramesPerSecond => UniformFramesPerSecond ??
        throw StateGuard.Create("This animation clip uses per-frame durations.");
    public float FrameDuration => UniformFrameDuration ??
        throw StateGuard.Create("This animation clip uses per-frame durations.");
    public float Duration { get; }
    public bool IsLooping { get; }

    public TFrame this[int index] => _frames[index];

    public float GetFrameDuration(int index) => _frameDurations[index];

    public int GetFrameIndexAtTime(float elapsedSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(elapsedSeconds);
        if (elapsedSeconds >= Duration)
            return FrameCount - 1;

        var low = 0;
        var high = _frameEndTimes.Length - 1;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (elapsedSeconds < _frameEndTimes[middle])
                high = middle;
            else
                low = middle + 1;
        }
        return low;
    }

    private static float[] CreateFrameEndTimes(
        float[] durations,
        out float totalDuration)
    {
        var endTimes = new float[durations.Length];
        double elapsed = 0d;
        for (var index = 0; index < durations.Length; index++)
        {
            elapsed += durations[index];
            if (elapsed > float.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durations),
                    "Total animation duration is too large.");
            }
            endTimes[index] = (float)elapsed;
        }
        totalDuration = (float)elapsed;
        return endTimes;
    }
}
