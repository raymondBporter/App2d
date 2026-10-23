using App2d.Core.Mathematics;
using App2d.Core.Validation;

namespace App2d.Core.Audio;

/// <summary>Maps a game's sound enum to cached clips and playback settings from a cue table.</summary>
public sealed class SoundEffectBank2D<TEffect> : ISoundEffectSink2D<TEffect>, IDisposable where TEffect : struct, Enum
{
    private const float MinimumVolumeScale = 0.95f;
    private const float MaximumVolumeScale = 1.05f;
    private const float MinimumPlaybackRate = 0.96f;
    private const float MaximumPlaybackRate = 1.04f;

    private readonly AudioMixer2D _mixer;
    private readonly Dictionary<TEffect, LoadedCue> _cues;

    /// <summary>Loads every cue's <c>{stem}.wav</c> from the root path.</summary>
    public SoundEffectBank2D(string rootPath, IReadOnlyDictionary<TEffect, SoundCue2D> cues)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(rootPath);
        ArgGuard.ThrowIfNull(cues);
        _mixer = new AudioMixer2D();
        try
        {
            _cues = new Dictionary<TEffect, LoadedCue>(cues.Count);
            foreach (var (effect, cue) in cues)
            {
                _cues[effect] = Load(rootPath, cue);
            }
        }
        catch
        {
            _mixer.Dispose();
            throw;
        }
    }

    public float Volume
    {
        get => _mixer.Volume;
        set => _mixer.Volume = value;
    }

    public void Play(TEffect effect) => _ = Begin(effect);

    public SoundEffectVoice2D Begin(TEffect effect, float initialVolumeScale = 1f)
    {
        ArgGuard.InRange(initialVolumeScale, 0f, 1f);
        if (!_cues.TryGetValue(effect, out var cue))
        {
            throw ArgGuard.CreateOutOfRange(effect, "Unknown sound effect.");
        }

        var fullVolume = cue.Volume * Random.Shared.NextFloat(MinimumVolumeScale, MaximumVolumeScale);
        var rate = cue.PitchJitter ? Random.Shared.NextFloat(MinimumPlaybackRate, MaximumPlaybackRate) : 1f;
        return new SoundEffectVoice2D(_mixer.Begin(cue.NextClip(), fullVolume * initialVolumeScale, rate), fullVolume);
    }

    public void Dispose() => _mixer.Dispose();

    private LoadedCue Load(string rootPath, SoundCue2D cue)
    {
        ArgGuard.ThrowIfNull(cue);
        ArgGuard.ThrowIf(cue.Stems.IsDefaultOrEmpty, "A cue needs at least one stem.", nameof(cue));
        ArgGuard.InRange(cue.Volume, 0f, 1f);
        var clips = new AudioClip2D[cue.Stems.Length];
        for (var i = 0; i < clips.Length; i++)
        {
            clips[i] = _mixer.Load(Path.Combine(rootPath, $"{cue.Stems[i]}.wav"));
        }

        return new LoadedCue(clips, cue.Volume, cue.PitchJitter);
    }

    private sealed class LoadedCue(AudioClip2D[] clips, float volume, bool pitchJitter)
    {
        private int _lastIndex = -1;

        public float Volume { get; } = volume;
        public bool PitchJitter { get; } = pitchJitter;

        /// <summary>A random clip, never the same one twice in a row.</summary>
        public AudioClip2D NextClip()
        {
            if (clips.Length == 1)
            {
                return clips[0];
            }

            var index = Random.Shared.Next(_lastIndex < 0 ? clips.Length : clips.Length - 1);
            if (_lastIndex >= 0 && index >= _lastIndex)
            {
                index++;
            }

            _lastIndex = index;
            return clips[index];
        }
    }
}
