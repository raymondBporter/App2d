namespace App2d.Engine.Animation;

/// <summary>
/// An immutable, evenly timed sequence of animation frames.
/// Frames can be textures, shaders, values, or any other state a game wants to animate.
/// </summary>
public sealed class AnimationClip2D<TFrame>
{
    private readonly TFrame[] _frames;

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

        FramesPerSecond = framesPerSecond;
        IsLooping = isLooping;
    }

    public IReadOnlyList<TFrame> Frames => _frames;
    public int FrameCount => _frames.Length;
    public float FramesPerSecond { get; }
    public float FrameDuration => 1f / FramesPerSecond;
    public float Duration => FrameCount / FramesPerSecond;
    public bool IsLooping { get; }

    public TFrame this[int index] => _frames[index];
}
