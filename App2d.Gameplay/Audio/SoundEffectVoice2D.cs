using App2d.Audio;
using App2d.Core;

namespace App2d.Gameplay.Audio;

/// <summary>A gameplay cue instance whose relative level can change while playing.</summary>
public readonly record struct SoundEffectVoice2D
{
    private readonly AudioVoice2D _voice;
    private readonly float _fullVolume;

    internal SoundEffectVoice2D(AudioVoice2D voice, float fullVolume)
    {
        _voice = voice;
        _fullVolume = fullVolume;
    }

    public bool IsPlaying => _voice.IsPlaying;

    public void SetVolumeScale(float volumeScale, float rampSeconds = 0f)
    {
        ArgGuard.ThrowIfNotInClosedRange(volumeScale, 0f, 1f);
        _voice.SetVolume(_fullVolume * volumeScale, rampSeconds);
    }

    public void Stop(float fadeOutSeconds = 0f) =>
        _voice.Stop(fadeOutSeconds);
}
