using App2d.Core;

namespace App2d.Audio;

/// <summary>A safe handle for adjusting one sound while it is playing.</summary>
public readonly record struct AudioVoice2D
{
    private readonly AudioMixer2D? _mixer;
    private readonly long _sequence;

    internal AudioVoice2D(AudioMixer2D mixer, long sequence)
    {
        _mixer = mixer;
        _sequence = sequence;
    }

    public bool IsPlaying =>
        _mixer is not null && _mixer.IsVoicePlaying(_sequence);

    public void SetVolume(float volume, float rampSeconds = 0f)
    {
        ArgGuard.ThrowIfNotInClosedRange(volume, 0f, 1f);
        ArgGuard.ThrowIfNegativeOrNotFinite(rampSeconds);
        _mixer?.SetVoiceVolume(_sequence, volume, rampSeconds);
    }

    public void Stop(float fadeOutSeconds = 0f)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(fadeOutSeconds);
        _mixer?.StopVoice(_sequence, fadeOutSeconds);
    }
}
