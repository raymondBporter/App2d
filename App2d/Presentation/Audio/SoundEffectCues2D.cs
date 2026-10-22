using App2d.Core.Audio;
using System.Collections.Immutable;

namespace App2d.Presentation.Audio;

/// <summary>The game's sound table: which clips each <see cref="SoundEffect2D"/> plays, how loud, and how it carries.</summary>
public static class SoundEffectCues2D
{
    /// <summary>Attenuation for cues without their own: full volume within 96 world units, silent beyond 800.</summary>
    public static SoundFalloff2D DefaultFalloff { get; } = new(96f, 800f);

    public static ImmutableDictionary<SoundEffect2D, SoundCue2D> Cues { get; } = new Dictionary<SoundEffect2D, SoundCue2D>
    {
        [SoundEffect2D.PlayerFootstep] = SoundCue2D.Of(0.36f, "player-footstep-01", "player-footstep-02", "player-footstep-03", "player-footstep-04"),
        [SoundEffect2D.PlayerJump] = SoundCue2D.Of(0.62f, "player-jump"),
        [SoundEffect2D.PlayerLandSoft] = SoundCue2D.Of(0.48f, "player-land-soft"),
        [SoundEffect2D.PlayerLandHard] = SoundCue2D.Of(0.72f, "player-land-hard"),
        [SoundEffect2D.SwordSwing] = SoundCue2D.Of(0.60f, "sword-swing-01", "sword-swing-02"),
        [SoundEffect2D.SwordHit] = SoundCue2D.Of(0.75f, "sword-hit-01", "sword-hit-02", "sword-hit-03"),
        [SoundEffect2D.FireballLaunch] = SoundCue2D.Of(0.64f, "fireball-launch"),
        [SoundEffect2D.FireballImpact] = SoundCue2D.Of(0.70f, "fireball-impact"),
        [SoundEffect2D.PlayerHurt] = SoundCue2D.Of(0.80f, "player-hurt"),
        [SoundEffect2D.EnemyHurt] = SoundCue2D.Of(0.55f, "enemy-hurt-01", "enemy-hurt-02"),
        [SoundEffect2D.EnemyDeath] = SoundCue2D.Of(0.75f, "enemy-death"),
        [SoundEffect2D.HammerWindup] = SoundCue2D.Of(0.62f, "hammer-windup"),
        // The hammer is the one impact that should be heard from across the arena.
        [SoundEffect2D.HammerImpact] = SoundCue2D.Of(0.86f, "hammer-impact") with { Falloff = new(128f, 1200f) },
        [SoundEffect2D.PlayerRespawn] = SoundCue2D.Of(0.70f, "player-respawn"),
        [SoundEffect2D.GoalReached] = SoundCue2D.Of(0.82f, "goal-reached"),
        // A sustained charge must hold its pitch or it warbles.
        [SoundEffect2D.GunCharge] = SoundCue2D.Of(0.45f, "gun-charge") with { PitchJitter = false },
        [SoundEffect2D.GunFire] = SoundCue2D.Of(0.72f, "gun-fire"),
        [SoundEffect2D.GunCancel] = SoundCue2D.Of(0.34f, "gun-cancel"),
        [SoundEffect2D.GunImpact] = SoundCue2D.Of(0.5f, "gun-impact"),
        [SoundEffect2D.HealCharge] = SoundCue2D.Of(0.3f, "heal-charge"),
        [SoundEffect2D.HealComplete] = SoundCue2D.Of(0.55f, "heal-complete"),
        [SoundEffect2D.HealCancel] = SoundCue2D.Of(0.25f, "heal-cancel")
    }.ToImmutableDictionary();

    /// <summary>Attenuation for a cue played at a position.</summary>
    public static SoundFalloff2D Falloff(SoundEffect2D effect) => Cues.TryGetValue(effect, out var cue) && cue.Falloff is { } falloff ? falloff : DefaultFalloff;
}
