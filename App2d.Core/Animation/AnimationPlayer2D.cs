namespace App2d.Engine.Animation;

/// <summary>
/// Advances a frame animation using update-loop time rather than render frequency.
/// </summary>
public sealed class AnimationPlayer2D<TFrame>
{
    private float _elapsedSeconds;
    private float _playbackSpeed = 1f;

    public AnimationClip2D<TFrame>? Clip { get; private set; }
    public float ElapsedSeconds => _elapsedSeconds;
    public int CurrentFrameIndex { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool IsFinished { get; private set; }

    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            ArgGuard.ThrowIfNegativeOrNotFinite(value, nameof(PlaybackSpeed));
            _playbackSpeed = value;
        }
    }

    public TFrame CurrentFrame => StateGuard.RequireNotNull(
        Clip,
        "Play a clip before reading its current frame.")[CurrentFrameIndex];

    public void Play(AnimationClip2D<TFrame> clip, bool restart = false)
    {
        ArgGuard.ThrowIfNull(clip);

        if (ReferenceEquals(Clip, clip) && !restart && IsPlaying)
            return;

        Clip = clip;
        _elapsedSeconds = 0f;
        CurrentFrameIndex = 0;
        IsFinished = false;
        IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;

    public void Resume()
    {
        if (Clip is not null && !IsFinished)
            IsPlaying = true;
    }

    public void Stop(bool resetToFirstFrame = true)
    {
        IsPlaying = false;
        IsFinished = false;
        if (!resetToFirstFrame)
            return;

        _elapsedSeconds = 0f;
        CurrentFrameIndex = 0;
    }

    public void Update(float deltaSeconds)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(deltaSeconds);
        if (!IsPlaying || Clip is null || deltaSeconds == 0f || PlaybackSpeed == 0f)
            return;

        _elapsedSeconds += deltaSeconds * PlaybackSpeed;
        if (Clip.IsLooping)
        {
            _elapsedSeconds %= Clip.Duration;
            CurrentFrameIndex = Clip.GetFrameIndexAtTime(_elapsedSeconds);
            return;
        }

        if (_elapsedSeconds >= Clip.Duration)
        {
            _elapsedSeconds = Clip.Duration;
            CurrentFrameIndex = Clip.FrameCount - 1;
            IsPlaying = false;
            IsFinished = true;
            return;
        }

        CurrentFrameIndex = Clip.GetFrameIndexAtTime(_elapsedSeconds);
    }
}
