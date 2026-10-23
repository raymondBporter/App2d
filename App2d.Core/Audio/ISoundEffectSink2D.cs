using System.Numerics;

namespace App2d.Core.Audio;

/// <summary>Plays the cues of a game's sound enum. Silent and test sinks need only implement <see cref="Play"/>.</summary>
public interface ISoundEffectSink2D<TEffect> where TEffect : struct, Enum
{
    void Play(TEffect effect);
    /// <summary>Non-spatial sinks ignore the source position.</summary>
    void PlayAt(TEffect effect, Vector2 position) => Play(effect);

    SoundEffectVoice2D BeginAt(TEffect effect, Vector2 position, float initialVolumeScale = 1f) => Begin(effect, initialVolumeScale);

    /// <summary>Optional controllable voice; a sink without one plays the cue and returns a default voice.</summary>
    SoundEffectVoice2D Begin(TEffect effect, float initialVolumeScale = 1f)
    {
        Play(effect);
        return default;
    }
}
