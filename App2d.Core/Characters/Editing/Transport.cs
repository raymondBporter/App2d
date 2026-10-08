using App2d.Core.Characters.Authored;
using App2d.Core.Timing;

namespace App2d.Core.Characters.Editing;

/// <summary>
/// The one playback clock: session state, never an asset field. Preview speed is separate from clip duration. A looping clip
/// counts cycles while playing so authored travel accumulates; pausing, seeking or reaching a one-shot's end returns to the
/// first cycle and holds the time, so an endpoint stays on screen.
/// </summary>
public sealed class Transport
{
    private readonly PlaybackClock _clock = new();
    public float Time => (float)_clock.TimeSeconds;
    public long Cycles => _clock.Cycles;
    public bool Playing => _clock.IsPlaying;
    public float Speed { get => (float)_clock.Speed; set => _clock.Speed = value; }
    /// <summary>Optional editor preview behavior; null uses the clip's loop setting.</summary>
    public PlaybackEndMode? EndModeOverride { get; set; }

    public PlaybackEndMode EndMode(MotionClip clip) => EndModeOverride ?? (clip.Loop ? PlaybackEndMode.Loop : PlaybackEndMode.Hold);
    private void Configure(MotionClip clip) => _clock.Configure(clip.Duration, EndMode(clip));
    /// <summary>Only authored loops accumulate travel beyond one cycle; preview overrides sample one cycle repeatedly.</summary>
    public bool Repeats(MotionClip clip) => Playing && clip.Loop && EndMode(clip) == PlaybackEndMode.Loop;

    public void Play(MotionClip clip)
    {
        Configure(clip);
        if (_clock.EndMode == PlaybackEndMode.Hold && Time >= clip.Duration) _clock.Seek(0);
        _clock.Play();
    }
    public void Pause()
    {
        _clock.Pause();
        if (_clock.EndMode == PlaybackEndMode.Loop) _clock.Seek(_clock.TimeSeconds);
    }
    public void Toggle(MotionClip clip) { if (Playing) Pause(); else Play(clip); }
    public void Seek(MotionClip? clip, float time)
    {
        if (clip is not null) Configure(clip);
        _clock.Seek(clip is null ? 0 : Math.Clamp(time, 0, clip.Duration));
    }

    public void Advance(MotionClip clip, float seconds)
    {
        Configure(clip);
        _clock.Advance(seconds);
    }

    /// <summary>The time to sample, counting completed cycles.</summary>
    public double Seconds(MotionClip clip) => Repeats(clip) ? _clock.ElapsedSeconds : Time;
    public float Phase(MotionClip clip) => Time / clip.Duration;
}
