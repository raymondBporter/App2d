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
        ArgumentNullException.ThrowIfNull(frames);
        if (!float.IsFinite(framesPerSecond) || framesPerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(framesPerSecond),
                "Frame rate must be positive and finite.");
        }

        _frames = [.. frames];
        if (_frames.Length == 0)
            throw new ArgumentException("An animation clip needs at least one frame.", nameof(frames));
        if (_frames.Any(static frame => frame is null))
            throw new ArgumentException("Animation frames cannot contain null values.", nameof(frames));

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
