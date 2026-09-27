# Music and world zones

Music starts automatically when the game starts. The first soundtrack uses two
original cues made in Music Lab: **Crown of Embers** (orchestra) and **Copper
Circuit**, revision 03 (rock). Both have Explore, Drive and Combat arrangements.
The current rule is deliberately authored: a zone selects a piece and a mood.
There is no enemy-proximity or combat-state heuristic yet.

| Region | World X range | Music |
| --- | --- | --- |
| Trailhead | -512 to 4096 | Crown of Embers / Explore |
| Copper Reach | 4096 to 12288 | Copper Circuit / Drive |
| High Keep | 12288 to 19968 | Crown of Embers / Combat |
| Outside all zones or an unassigned zone | — | Crown of Embers / Drive |

All three starter rectangles span Y=-640 to Y=2432. These are initial authoring
choices, independent of tile appearance, checkpoints and enemy placement.

## Audition in the game

Open the developer console with backtick. Examples:

```text
music_volume 0.45
music_mood combat
music_piece copper-circuit
music_status
draw_zones true
music_piece auto
music_mood auto
```

`music_volume 0` mutes music without resetting it or muting SFX. Values range
from 0 to 1; the default is 0.45. Mood names are `explore`, `drive`, `combat` or
`auto`; piece names are the two IDs above or `auto`. Overrides are temporary
developer controls, not saved preferences. `music_status` shows the selected
zone, requested piece/mood and volume. Audio changes may still be fading.
The outline of the player's current zone is gold; other visible zones are cyan.
Music continues during editor mode, following the player's position, not the
editor camera. Death/respawn does not explicitly restart the music; a respawn in
another zone changes the selection normally.

## Author rectangles and bindings

`Assets/Static/levels/cavern/zones.json` stores stable IDs, names, axis-aligned
rectangles and integer priorities in world units. Coordinates are minimum and
maximum corners, not width/height. Minimum edges are inclusive; maximum edges
are exclusive, so neighboring rectangles have one owner at their shared edge.
Overlaps choose the highest priority, breaking ties by ordinal ID. There is no
parent/child hierarchy; future subzones can initially use higher priority.
An absent zones file is supported for older levels. Invalid rectangles,
duplicate IDs, omitted coordinates and unknown properties are rejected.

Zones belong to `LevelContent2D`, travel with session snapshots/frames, and stay
available even when their terrain is streamed out. They contain no audio fields
and can support other world rules. The first version is authored through JSON;
the tile editor does not yet create or resize zones. Restart after edits. Debug
loads zone geometry beside the durable level database directly.

`Assets/Static/audio/music/soundtrack.json` assigns zone IDs to `{piece, mood}`
pairs, declares available cues and supplies the fallback. Bindings to missing
zones or moods fail with a content error. Run the regular asset pipeline after
editing static music configuration, or copy the changed soundtrack file into
`Assets/Runtime/audio/music/` for a quick local iteration. Restart the game to
load the change. Release/publish includes both geometry and music assets.

## Playback

`App2d.Audio` streams stereo, 44.1 kHz Ogg Vorbis through NVorbis into a separate
NAudio output. Each piece has one cursor and fixed-size decode buffers for all
stems. Muted layers continue advancing, and all decoders rewind together at the
manifest's exact frame boundary. Full decoded loops are never cached.
Readers for the small cue catalog are opened at startup; only the current cue
and, during crossfade, its outgoing cue are decoded.

Zone changes must settle for 0.75 seconds. Different pieces use a two-second
linear crossfade and restart the new piece from its beginning. Returning to a
previous piece also starts it from the beginning. Further piece changes during
a crossfade coalesce to the latest request. Mood changes within a piece preserve
position, begin on the next bar and fade over one bar. These timing decisions
use sample frames, not simulation tick timing. Volume changes ramp over 50 ms.
The SFX voice limit cannot evict music, and music has independent volume.

### Allocation and threading baseline

The frame-by-frame zone lookup uses a span, and selections are value types.
After warm-up, tests measure **zero managed bytes** for 10,000 director updates,
including zone changes. The mixer reuses its buffers; 1,000 blocks including
fades, wraps and control changes also allocate zero bytes when measured with
preallocated sources. The current two-cue catalog has about 80 KiB of our own
sample scratch buffers, excluding decoder state and NAudio device buffers.

NAudio 3's event-based WaveOut handles audio on its playback worker. Main-thread
control requests use a short mailbox lock that is not held during decoding or
file reads; a concurrency test verifies they complete even if decoding stalls.
Initialization and final disposal can perform I/O and allocate. They are not
per-frame operations.

The full decoder path is **not allocation-free**. With NVorbis 0.10.4, the shipped
four-stem cues measured roughly 1.87 MB/s (orchestra) and 2.02 MB/s (rock) of
temporary managed allocations per second of audio. A test records this cost and
caps regression at 2.5 MB/s per cue; that is a ceiling, not a performance target.
Two cues are decoded during a crossfade, so their decoder costs add. The measured
allocation figures exclude startup and do not establish frame-time or audio-device
latency guarantees. Reproduce with the `MusicPerformanceTests` test filter and a
detailed console logger.

Upstream [NVorbis release notes](https://github.com/NVorbis/NVorbis/releases)
include allocation fixes in the 1.0 prerelease series, along with breaking API
changes. This pass keeps the existing decoder dependency; adopting a new codec
version needs its own compatibility check, including MonoGame's use of NVorbis.
The current player opens readers for the small catalog at startup. Before adding
many pieces, bound that reader cache and prefetch upcoming cues rather than
opening an entire soundtrack or loading new decoders on the gameplay thread.

The eight compressed stems total 6,864,182 bytes (about 6.9 MB). No music AI,
SoundFont, FluidSynth, Python or authoring tool runs in the game. NVorbis is the
only added runtime package. License/provenance files ship beside each cue.

## Asset lifecycle and validation

The selected renders are promoted into durable `Assets/Static/audio/music/`.
The normal art pipeline copies them into disposable `Assets/Runtime`, hashes
them in the content manifest, and packages them in Release/publish. The game
does not depend on ignored Music Lab outputs. `tools/MusicLab/publish_game.py`
can promote a new accepted render and refresh just these runtime files plus the
manifest without rebuilding unrelated art. It expects the Music Lab revisions
to have been rendered first; it is not a prerequisite for playing the game.

Tests cover deterministic zone boundaries/overlaps, zone persistence in content,
selection debounce/fallback/overrides, muted-layer advancement, bar-aligned mood
ramps, crossfades, mute ramps, early EOF, and the actual shipped Ogg files across
complete loop boundaries. These are engineering checks, not a listening review.
