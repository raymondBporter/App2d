using App2d.Audio;
using App2d.Core;

namespace App2d.Gameplay.Audio;

/// <summary>Maps gameplay sound events to cached clips and playback settings.</summary>
public sealed class SoundEffectBank2D : ISoundEffectSink2D, IDisposable
{
    private readonly AudioMixer2D _mixer;
    private readonly Dictionary<SoundEffect2D, Cue> _cues;

    public SoundEffectBank2D(string rootPath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(rootPath);
        _mixer = new AudioMixer2D();
        try
        {
            _cues = new Dictionary<SoundEffect2D, Cue>
            {
                [SoundEffect2D.PlayerFootstep] = Load(rootPath, 0.36f,
                    "player-footstep-01", "player-footstep-02",
                    "player-footstep-03", "player-footstep-04"),
                [SoundEffect2D.PlayerJump] = Load(rootPath, 0.62f, "player-jump"),
                [SoundEffect2D.PlayerLandSoft] = Load(rootPath, 0.48f, "player-land-soft"),
                [SoundEffect2D.PlayerLandHard] = Load(rootPath, 0.72f, "player-land-hard"),
                [SoundEffect2D.SwordSwing] = Load(rootPath, 0.6f,
                    "sword-swing-01", "sword-swing-02"),
                [SoundEffect2D.SwordHit] = Load(rootPath, 0.75f,
                    "sword-hit-01", "sword-hit-02", "sword-hit-03"),
                [SoundEffect2D.FireballLaunch] = Load(rootPath, 0.64f, "fireball-launch"),
                [SoundEffect2D.FireballImpact] = Load(rootPath, 0.7f, "fireball-impact"),
                [SoundEffect2D.PlayerHurt] = Load(rootPath, 0.8f, "player-hurt"),
                [SoundEffect2D.EnemyHurt] = Load(rootPath, 0.55f,
                    "enemy-hurt-01", "enemy-hurt-02"),
                [SoundEffect2D.EnemyDeath] = Load(rootPath, 0.75f, "enemy-death"),
                [SoundEffect2D.HammerWindup] = Load(rootPath, 0.62f, "hammer-windup"),
                [SoundEffect2D.HammerImpact] = Load(rootPath, 0.86f, "hammer-impact"),
                [SoundEffect2D.PlayerRespawn] = Load(rootPath, 0.7f, "player-respawn"),
                [SoundEffect2D.GoalReached] = Load(rootPath, 0.82f, "goal-reached")
            };
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

    public void Play(SoundEffect2D effect)
    {
        if (!_cues.TryGetValue(effect, out var cue))
            throw ArgGuard.CreateOutOfRange(effect, "Unknown sound effect.");
        _mixer.Play(cue.NextClip(), cue.Volume);
    }

    public void Dispose() => _mixer.Dispose();

    private Cue Load(string rootPath, float volume, params string[] stems)
    {
        var clips = new AudioClip2D[stems.Length];
        for (var i = 0; i < stems.Length; i++)
            clips[i] = _mixer.Load(Path.Combine(rootPath, $"{stems[i]}.wav"));
        return new Cue(clips, volume);
    }

    private sealed class Cue(AudioClip2D[] clips, float volume)
    {
        private int _lastIndex = -1;

        public float Volume { get; } = volume;

        public AudioClip2D NextClip()
        {
            if (clips.Length == 1)
                return clips[0];

            var index = Random.Shared.Next(_lastIndex < 0 ? clips.Length : clips.Length - 1);
            if (_lastIndex >= 0 && index >= _lastIndex)
                index++;
            _lastIndex = index;
            return clips[index];
        }
    }
}
