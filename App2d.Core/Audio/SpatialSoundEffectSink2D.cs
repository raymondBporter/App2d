using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Audio;

/// <summary>Adds listener-relative falloff to a sink; the falloff function says how each cue carries. Update once per game frame.</summary>
public sealed class SpatialSoundEffectSink2D<TEffect>(ISoundEffectSink2D<TEffect> sounds, Func<Vector2> listenerPosition, Func<TEffect, SoundFalloff2D> falloff) : ISoundEffectSink2D<TEffect> where TEffect : struct, Enum
{
    private readonly ISoundEffectSink2D<TEffect> _sounds = ArgGuard.RequireNotNull(sounds);
    private readonly Func<Vector2> _listenerPosition = ArgGuard.RequireNotNull(listenerPosition);
    private readonly Func<TEffect, SoundFalloff2D> _falloff = ArgGuard.RequireNotNull(falloff);
    private readonly List<SpatialVoice> _voices = [];

    // Global/UI cues intentionally bypass spatial attenuation.
    public void Play(TEffect effect) => _sounds.Play(effect);
    public SoundEffectVoice2D Begin(TEffect effect, float initialVolumeScale = 1f) => _sounds.Begin(effect, initialVolumeScale);

    public void PlayAt(TEffect effect, Vector2 position)
    {
        var falloff = GetFalloff(effect);
        var attenuation = falloff.Evaluate(position, _listenerPosition());
        // Inaudible one-shots must not evict audible sounds from the mixer pool.
        if (attenuation > 0f)
        {
            _ = BeginSpatial(effect, position, 1f, falloff, attenuation);
        }
    }

    public SoundEffectVoice2D BeginAt(TEffect effect, Vector2 position, float initialVolumeScale = 1f)
    {
        ArgGuard.InRange(initialVolumeScale, 0f, 1f);
        var falloff = GetFalloff(effect);
        // Controllable voices keep their timeline even when initially out of range.
        return BeginSpatial(effect, position, initialVolumeScale, falloff, falloff.Evaluate(position, _listenerPosition()));
    }

    public void Update()
    {
        var listener = _listenerPosition();
        ArgGuard.Finite(listener);
        for (var i = _voices.Count - 1; i >= 0; i--)
        {
            var voice = _voices[i];
            if (voice.IsPlaying)
            {
                voice.Update(listener);
            }
            else
            {
                _voices.RemoveAt(i);
            }
        }
    }

    private SoundEffectVoice2D BeginSpatial(TEffect effect, Vector2 position, float volumeScale, SoundFalloff2D falloff, float attenuation)
    {
        var playback = _sounds.Begin(effect, volumeScale * attenuation);
        if (!playback.IsPlaying)
        {
            return playback;
        }

        var voice = new SpatialVoice(playback, position, volumeScale, falloff, attenuation);
        _voices.Add(voice);
        return new SoundEffectVoice2D(voice);
    }

    private SoundFalloff2D GetFalloff(TEffect effect) => _falloff(effect) ?? throw new InvalidOperationException($"No falloff was supplied for {effect}.");

    private sealed class SpatialVoice(SoundEffectVoice2D playback, Vector2 position, float volumeScale, SoundFalloff2D falloff, float attenuation) : ISoundEffectVoice2D
    {
        private const float VolumeRampSeconds = 0.02f;
        private Vector2 _position = position;
        private float _volumeScale = volumeScale;
        private float _attenuation = attenuation;
        private bool _stopping;

        public bool IsPlaying => playback.IsPlaying;
        public void SetPosition(Vector2 position) => _position = position;

        public void SetVolumeScale(float volumeScale, float rampSeconds)
        {
            if (_stopping)
            {
                return;
            }

            _volumeScale = volumeScale;
            playback.SetVolumeScale(_volumeScale * _attenuation, rampSeconds);
        }

        public void Update(Vector2 listener)
        {
            if (_stopping)
            {
                return;
            }

            var next = falloff.Evaluate(_position, listener);
            if (next == _attenuation)
            {
                return;
            }

            _attenuation = next;
            playback.SetVolumeScale(_volumeScale * _attenuation, VolumeRampSeconds);
        }

        public void Stop(float fadeOutSeconds)
        {
            _stopping = true;
            playback.Stop(fadeOutSeconds);
        }
    }
}
