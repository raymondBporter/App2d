using App2d.Engine.Audio;

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
                    "player_footstep_01", "player_footstep_02",
                    "player_footstep_03", "player_footstep_04"),
                [SoundEffect2D.PlayerJump] = Load(rootPath, 0.62f, "player_jump"),
                [SoundEffect2D.PlayerLandSoft] = Load(rootPath, 0.48f, "player_land_soft"),
                [SoundEffect2D.PlayerLandHard] = Load(rootPath, 0.72f, "player_land_hard"),
                [SoundEffect2D.WeaponSwitch] = Load(rootPath, 0.5f, "weapon_switch"),
                [SoundEffect2D.SwordSwing] = Load(rootPath, 0.6f,
                    "sword_swing_01", "sword_swing_02"),
                [SoundEffect2D.SwordHit] = Load(rootPath, 0.75f,
                    "sword_hit_01", "sword_hit_02", "sword_hit_03"),
                [SoundEffect2D.GrappleFire] = Load(rootPath, 0.65f, "grapple_fire"),
                [SoundEffect2D.GrappleLatch] = Load(rootPath, 0.75f, "grapple_latch"),
                [SoundEffect2D.GrappleRetract] = Load(rootPath, 0.5f, "grapple_retract"),
                [SoundEffect2D.GrappleRelease] = Load(rootPath, 0.55f, "grapple_release"),
                [SoundEffect2D.BallThrow] = Load(rootPath, 0.68f, "ball_throw"),
                [SoundEffect2D.BallLand] = Load(rootPath, 0.78f, "ball_land"),
                [SoundEffect2D.BallYank] = Load(rootPath, 0.65f, "ball_yank"),
                [SoundEffect2D.FireballLaunch] = Load(rootPath, 0.64f, "fireball_launch"),
                [SoundEffect2D.FireballImpact] = Load(rootPath, 0.7f, "fireball_impact"),
                [SoundEffect2D.PlayerHurt] = Load(rootPath, 0.8f, "player_hurt"),
                [SoundEffect2D.EnemyHurt] = Load(rootPath, 0.55f,
                    "enemy_hurt_01", "enemy_hurt_02"),
                [SoundEffect2D.EnemyDeath] = Load(rootPath, 0.75f, "enemy_death"),
                [SoundEffect2D.HammerWindup] = Load(rootPath, 0.62f, "hammer_windup"),
                [SoundEffect2D.HammerImpact] = Load(rootPath, 0.86f, "hammer_impact"),
                [SoundEffect2D.PlayerRespawn] = Load(rootPath, 0.7f, "player_respawn"),
                [SoundEffect2D.GoalReached] = Load(rootPath, 0.82f, "goal_reached")
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

            var index = _lastIndex < 0
                ? Random.Shared.Next(clips.Length)
                : Random.Shared.Next(clips.Length - 1);
            if (_lastIndex >= 0 && index >= _lastIndex)
                index += 1;
            _lastIndex = index;
            return clips[index];
        }
    }
}
