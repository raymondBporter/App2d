using App2d.Audio;
using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Audio;

/// <summary>A gameplay cue instance whose relative level can change while playing.</summary>
public readonly record struct SoundEffectVoice2D
{
    private readonly ISoundEffectVoice2D? _voice;
    private readonly AudioVoice2D _audioVoice;
    private readonly float _fullVolume;

    internal SoundEffectVoice2D(AudioVoice2D voice, float fullVolume)
    {
        _audioVoice = voice;
        _fullVolume = fullVolume;
    }

    internal SoundEffectVoice2D(ISoundEffectVoice2D voice) => _voice = voice;

    public bool IsPlaying => _voice?.IsPlaying ?? _audioVoice.IsPlaying;

    public void SetVolumeScale(float volumeScale, float rampSeconds = 0f)
    {
        ArgGuard.ThrowIfNotInClosedRange(volumeScale, 0f, 1f);
        ArgGuard.ThrowIfNegativeOrNotFinite(rampSeconds);
        if (_voice is not null)
            _voice.SetVolumeScale(volumeScale, rampSeconds);
        else
            _audioVoice.SetVolume(_fullVolume * volumeScale, rampSeconds);
    }

    /// <summary>Moves a spatial voice's source; non-spatial voices ignore this.</summary>
    public void SetPosition(Vector2 position)
    {
        ArgGuard.ThrowIfNotFinite(position);
        _voice?.SetPosition(position);
    }

    public void Stop(float fadeOutSeconds = 0f)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(fadeOutSeconds);
        if (_voice is not null)
            _voice.Stop(fadeOutSeconds);
        else
            _audioVoice.Stop(fadeOutSeconds);
    }
}

internal interface ISoundEffectVoice2D
{
    bool IsPlaying { get; }
    void SetVolumeScale(float volumeScale, float rampSeconds);
    void Stop(float fadeOutSeconds);
    void SetPosition(Vector2 position) { }
}
