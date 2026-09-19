using App2d.Core;
using System.Numerics;

namespace App2d.Gameplay.Audio;

/// <summary>Adds listener-relative falloff to a sound bank. Update once per game frame.</summary>
public sealed class SpatialSoundEffectSink2D(ISoundEffectSink2D sounds, Func<Vector2> listenerPosition) : ISoundEffectSink2D
{
    private readonly ISoundEffectSink2D _sounds = ArgGuard.RequireNotNull(sounds);
    private readonly Func<Vector2> _listenerPosition = ArgGuard.RequireNotNull(listenerPosition);
    private readonly List<SpatialVoice> _voices = [];

    // Global/UI cues intentionally bypass spatial attenuation.
    public void Play(SoundEffect2D effect) => _sounds.Play(effect);
    public SoundEffectVoice2D Begin(SoundEffect2D effect, float initialVolumeScale = 1f) =>
        _sounds.Begin(effect, initialVolumeScale);

    public void PlayAt(SoundEffect2D effect, Vector2 position)
    {
        var falloff = GetFalloff(effect);
        var attenuation = falloff.Evaluate(position, _listenerPosition());
        // Inaudible one-shots must not evict audible sounds from the mixer pool.
        if (attenuation > 0f)
            _ = BeginSpatial(effect, position, 1f, falloff, attenuation);
    }

    public SoundEffectVoice2D BeginAt(
        SoundEffect2D effect, Vector2 position, float initialVolumeScale = 1f)
    {
        ArgGuard.ThrowIfNotInClosedRange(initialVolumeScale, 0f, 1f);
        var falloff = GetFalloff(effect);
        // Controllable voices keep their timeline even when initially out of range.
        return BeginSpatial(effect, position, initialVolumeScale, falloff,
            falloff.Evaluate(position, _listenerPosition()));
    }

    public void Update()
    {
        var listener = _listenerPosition();
        ArgGuard.ThrowIfNotFinite(listener);
        for (var i = _voices.Count - 1; i >= 0; i--)
        {
            var voice = _voices[i];
            if (voice.IsPlaying)
                voice.Update(listener);
            else
                _voices.RemoveAt(i);
        }
    }

    private SoundEffectVoice2D BeginSpatial(
        SoundEffect2D effect, Vector2 position, float volumeScale,
        SoundFalloff2D falloff, float attenuation)
    {
        var playback = _sounds.Begin(effect, volumeScale * attenuation);
        if (!playback.IsPlaying)
            return playback;
        var voice = new SpatialVoice(playback, position, volumeScale, falloff, attenuation);
        _voices.Add(voice);
        return new SoundEffectVoice2D(voice);
    }

    private static SoundFalloff2D GetFalloff(SoundEffect2D effect) =>
        effect == SoundEffect2D.HammerImpact ? SoundFalloff2D.HammerImpact : SoundFalloff2D.Default;

    private sealed class SpatialVoice(
        SoundEffectVoice2D playback, Vector2 position, float volumeScale,
        SoundFalloff2D falloff, float attenuation) : ISoundEffectVoice2D
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
                return;
            _volumeScale = volumeScale;
            playback.SetVolumeScale(_volumeScale * _attenuation, rampSeconds);
        }

        public void Update(Vector2 listener)
        {
            if (_stopping)
                return;
            var next = falloff.Evaluate(_position, listener);
            if (next == _attenuation)
                return;
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
