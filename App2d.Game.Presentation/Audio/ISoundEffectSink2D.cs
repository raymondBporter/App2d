using System.Numerics;

namespace App2d.Gameplay.Audio;

public interface ISoundEffectSink2D
{
    void Play(SoundEffect2D effect);
    // Non-spatial and test sinks can ignore source positions.
    void PlayAt(SoundEffect2D effect, Vector2 position) => Play(effect);

    SoundEffectVoice2D BeginAt(
        SoundEffect2D effect,
        Vector2 position,
        float initialVolumeScale = 1f) => Begin(effect, initialVolumeScale);

    // Optional controllable voice; silent/test sinks need only implement Play.
    SoundEffectVoice2D Begin(SoundEffect2D effect, float initialVolumeScale = 1f)
    {
        Play(effect);
        return default;
    }
}
