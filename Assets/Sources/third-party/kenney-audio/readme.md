# App2d placeholder SFX pack

Twenty-two short, mono sound effects curated for the current App2d side-scroller. The set
leans industrial/cyberpunk: concrete footsteps, metal impacts, and restrained energy
sounds.

## Format

`Assets/Static/audio/sfx/` contains PCM 16-bit mono WAV files at 44.1 kHz. The asset
pipeline copies them to `Assets/Runtime/audio/sfx/`. They need no codec and are ideal
for short, latency-sensitive effects.

For this set, caching every decoded effect is reasonable: the entire WAV directory is
under 1 MB. Compressed audio is normally decoded to PCM on the CPU before playback;
hardware codec acceleration is not a meaningful concern for tiny one-shot game sounds.
Reserve compressed formats for music, ambience, dialogue, or much larger SFX libraries.

## Playback notes

- Footstep, sword-swing, sword-hit, and enemy-hurt cues choose variants without an
  immediate repeat.
- The jump impulse starts quietly, then its own level ramps with held-jump power.
- Footsteps are intentionally quieter than combat impacts.
- `player-land-soft` and `player-land-hard` are separate so landing speed can choose the cue.
- `fireball-impact` is deliberately the longest effect at about 1.25 seconds.

See `sound-manifest.md` for event mappings and `provenance.md` for exact source files.
