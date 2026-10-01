using App2d.Core.Validation;
using App2d.Core.Timing;

namespace App2d.Core.Animation;

/// <summary>
/// Advances a frame animation using update-loop time rather than render frequency.
/// </summary>
public sealed class AnimationPlayer2D<TFrame>
{
    private readonly PlaybackClock _clock = new();

    public AnimationClip2D<TFrame>? Clip { get; private set; }
    public float ElapsedSeconds => (float)_clock.TimeSeconds;
    public int CurrentFrameIndex { get; private set; }
    public bool IsPlaying => _clock.IsPlaying;
    public bool IsFinished => _clock.IsComplete;

    public float PlaybackSpeed
    {
        get => (float)_clock.Speed;
        set
        {
            ArgGuard.ThrowIfNotFiniteOrNegative(value, nameof(PlaybackSpeed));
            _clock.Speed = value;
        }
    }

    public TFrame CurrentFrame => StateGuard.RequireNotNull(Clip, "Play a clip before reading its current frame.")[CurrentFrameIndex];

    public void Play(AnimationClip2D<TFrame> clip, bool restart = false, PlaybackEndMode? endMode = null)
    {
        ArgGuard.ThrowIfNull(clip);
        var mode = endMode ?? (clip.IsLooping ? PlaybackEndMode.Loop : PlaybackEndMode.Hold);

        if (ReferenceEquals(Clip, clip) && !restart && IsPlaying && _clock.EndMode == mode)
            return;

        _clock.Configure(clip.Duration, mode);
        Clip = clip;
        _clock.Restart();
        CurrentFrameIndex = 0;
    }

    public void Pause() => _clock.Pause();

    public void Resume()
    {
        if (Clip is not null && !IsFinished)
            _clock.Play();
    }

    public void Stop(bool resetToFirstFrame = true)
    {
        _clock.Stop(resetToFirstFrame);
        if (!resetToFirstFrame)
            return;

        CurrentFrameIndex = 0;
    }

    public void Update(float deltaSeconds)
    {
        ArgGuard.ThrowIfNotFiniteOrNegative(deltaSeconds);
        if (!IsPlaying || Clip is null || deltaSeconds == 0f || PlaybackSpeed == 0f)
            return;

        _clock.Advance(deltaSeconds);
        CurrentFrameIndex = Clip.GetFrameIndexAtTime(ElapsedSeconds);
    }
}
