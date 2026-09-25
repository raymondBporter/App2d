namespace App2d.Core.Characters.Editing;

/// <summary>
/// The one playback clock: session state, never an asset field. Preview speed is separate from clip duration. A looping clip
/// counts cycles while playing so authored travel accumulates; pausing, seeking or reaching a one-shot's end returns to the
/// first cycle and holds the time, so an endpoint stays on screen.
/// </summary>
public sealed class Transport
{
    public float Time { get; private set; }
    public int Cycles { get; private set; }
    public bool Playing { get; private set; }
    public float Speed { get; set; } = 1;

    public void Play(MotionClip clip) { if (!clip.Loop && Time >= clip.Duration) Time = 0; Playing = true; }
    public void Pause() { Playing = false; Cycles = 0; }
    public void Toggle(MotionClip clip) { if (Playing) Pause(); else Play(clip); }
    public void Seek(MotionClip? clip, float time) { Time = clip is null ? 0 : Math.Clamp(time, 0, clip.Duration); Cycles = 0; }

    public void Advance(MotionClip clip, float seconds)
    {
        if (!Playing) return;
        var next = Time + seconds * Speed;
        if (clip.Loop) { var cycles = (int)MathF.Floor(next / clip.Duration); Cycles += cycles; Time = next - cycles * clip.Duration; return; }
        if (next >= clip.Duration) { Time = clip.Duration; Pause(); return; }
        Time = next;
    }

    /// <summary>The time to sample, counting completed cycles.</summary>
    public double Seconds(MotionClip clip) => Cycles * (double)clip.Duration + Math.Min(Time, clip.Duration);
    public float Phase(MotionClip clip) => Time / clip.Duration;
}
