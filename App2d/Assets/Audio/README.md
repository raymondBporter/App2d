# App2d placeholder SFX pack

Thirty short, mono sound effects curated for the current App2d side-scroller. The set
leans industrial/cyberpunk: concrete footsteps, metal impacts, restrained energy sounds,
and mechanical grapple/chain cues.

## Format

`Sfx/` contains PCM 16-bit mono WAV files at 44.1 kHz. They need no codec and are ideal
for short, latency-sensitive effects.

For this set, caching every decoded effect is reasonable: the entire WAV directory is
under 1 MB. Compressed audio is normally decoded to PCM on the CPU before playback;
hardware codec acceleration is not a meaningful concern for tiny one-shot game sounds.
Reserve compressed formats for music, ambience, dialogue, or much larger SFX libraries.

## Playback notes

- Footstep, sword-swing, sword-hit, and enemy-hurt cues choose variants without an
  immediate repeat.
- Footsteps are intentionally quieter than combat impacts.
- `player_land_soft` and `player_land_hard` are separate so landing speed can choose the cue.
- `fireball_impact` is deliberately the longest effect at about 1.25 seconds.

See `SOUND-MANIFEST.md` for event mappings and `PROVENANCE.md` for exact source files.
