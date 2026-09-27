# Copper Circuit — music experiment 01

The accepted rock revision 03 and the orchestral cue now ship in the game.
See [runtime integration](../../docs/music-and-zones.md) for zones, playback and
console controls. The listening pages below remain standalone authoring tools.

An original instrumental rock/chip cue for App2d. **144 BPM, 4/4, E minor,
32 bars, 53.333333 seconds.** No existing Nintendo composition, melody, MIDI or
recording was used. The brief informs the palette and adventure-game character.

## Listen

`listen-first.mp3` demonstrates Explore → Drive → Combat → Explore in one loop.
The listening page lets you change moods while the composition keeps its place.
The MP3 is a convenience preview, not a sample-accurate runtime loop.

From the repository root:

```powershell
python tools/MusicLab/serve.py
```

Open http://127.0.0.1:8766. Press Play and select a mood, or use Auto tour.
The page starts at 65% master volume. Changes arrive on the next bar and fade
over one bar. All four decoded stems start on the same audio clock.

## Deliverables

- `game/*.ogg`: four stereo Vorbis stems for compact deployment.
- `game/manifest.json`: exact frame count, BPM, loop points, mood gain vectors.
- `copper-circuit-game.zip`: runtime stems, manifest, notes and attribution.
- `stems/*.wav`: matched 16-bit PCM masters for engines without Ogg support.
- `mixes/*.wav` / `*.mp3`: three full-length static mood previews.
- `source/score.json`: notes, velocities, instruments, harmony and arrangement.
- `source/copper-circuit.mid`: editable notes. Chip voices use a GM square-lead
  approximation in MIDI; MIDI does not contain our exact patches or mix effects.
- `report.json`: measured levels, true-peak estimates, frame counts and sizes.

The complete experiment is generated under ignored `Assets/Work/music-lab/`.
Only the small build scripts and player template belong in source control.

## Musical construction

Four eight-bar sections: a minor-key main theme, a brighter relative-major lift,
a suspended bridge with space between notes, and a returning theme/turnaround.
Harmony and note sequences are explicitly composed, not randomly selected.

| Stem | Musical role |
|---|---|
| World | Picked bass, clean electric guitar arpeggios, quiet organ |
| Theme | Band-limited pulse melody with short stereo answers |
| Rock | Two guitar parts and acoustic kick/snare/hat kit |
| Overdrive | Additional lead guitar, low toms, tambourine and fills |

The combat part is an additional arrangement, not a second copy of the drum mix.
No vocals. No runtime generation. Instruments are rendered locally with
FluidSynth and GeneralUser GS, plus custom pulse synthesis for the chip voices.

## Rebuild

The local setup used Python 3.12, FluidSynth 2.6.1 (portable Windows x64), and
GeneralUser GS 2.0.3. Dependencies are isolated under `Assets/Work/music-lab/deps`.
Run `python tools/MusicLab/setup.py` on another Windows x64 machine to download
the pinned renderer, SoundFont and Python dependencies into that folder.

```powershell
python tools/MusicLab/build.py
# Or preserve hand edits to the exported score:
python tools/MusicLab/build.py --score Assets/Work/music-lab/copper-circuit/source/score.json
```

Author musical changes in `compose.py`; timbre, effects and balance in `build.py`.
The renderer bypasses MIDI tick rounding and schedules directly in sample frames.
At 44,100 Hz / 144 BPM, each beat is exactly 18,375 frames and the loop is exactly
2,352,000 frames. Sampled parts render two cycles and retain the second, carrying
release tails into the beginning. Circular effects preserve the same boundaries.
All stems receive one shared mastering gain; do not normalize each independently.

## Revision 02: more metal / classic rock

Revision 02 keeps the accepted melody, harmony, tempo and mood structure. The
World and Theme audio files are copied byte-for-byte from revision 01. Only the
band changes: tighter low guitar dyads and single-note riffs, additional
midrange/distortion, stronger kick/snare accents, and a more prominent lead.
The original render stays available for comparison. Hear changes in Drive and
Overdrive; Explore retains the original arrangement.

```powershell
python tools/MusicLab/revise.py
python tools/MusicLab/build.py --score Assets/Work/music-lab/copper-circuit/revisions/heavier-v2/input-score.json --out Assets/Work/music-lab/copper-circuit/revisions/heavier-v2 --reference Assets/Work/music-lab/copper-circuit
python tools/MusicLab/verify.py --out Assets/Work/music-lab/copper-circuit/revisions/heavier-v2
```

The reference option retains the original mastering gain and checks headroom.
The original authoring files are preserved under `source/v1-build/` in the
original output folder. Revision changes are authored in `revise.py`.

## Revision 03: lead guitar and drum fireworks

Only the Overdrive stem changes from revision 02. The lead is more prominent,
with short chord-aware runs; the additional kit plays tom/snare breaks and kick
pickups, leaving space during the guitar runs. Tempo and mood gains stay fixed.
World, Theme and Rock are copied exactly, preserving Explore and Drive.
The revision's listening page starts with Overdrive selected.

```powershell
python tools/MusicLab/overdrive.py
python tools/MusicLab/build.py --score Assets/Work/music-lab/copper-circuit/revisions/overdrive-v3/input-score.json --out Assets/Work/music-lab/copper-circuit/revisions/overdrive-v3 --reference Assets/Work/music-lab/copper-circuit/revisions/heavier-v2 --preserve-stems world theme rock
python tools/MusicLab/verify.py --out Assets/Work/music-lab/copper-circuit/revisions/overdrive-v3
```

## Game playback

Use the four baked stems and a **shared sample cursor**. Advance every stem even
while its gain is zero. Quantize changes to the next bar (73,500 frames) and ramp
gains over a bar. Use a short combat-exit delay in gameplay to avoid mood flicker.
Maintain the manifest's relative gains and leave SFX headroom on the music bus.

Ogg saves disk/download space, not decoded RAM. Preloading four full float32
stereo loops costs about 71.8 MiB. A production player can decode through small
ring buffers instead; verify exact decoded frame count and wrap each stream to
frame zero together. The existing App2d mixer is a short-SFX voice pool; do not
place music in that pool where effects could evict it. The game now uses a separate
streaming Ogg music player; see the runtime integration notes linked above.

## What this experiment establishes

The engineering question is whether a reproducible, editable score can produce
compact, synchronized mood layers without a music subscription. Measurements can
check lengths, alignment, numerical validity, levels and codec output.

The artistic question still needs listening: does the hook stick, do the guitars
feel satisfying, is the calm mix pleasant repeatedly, and does combat gain energy
without becoming cluttered? The assistant has not performed a perceptual listening
review. This is a candidate cue, not a claim of professionally mastered audio.

Keep feedback simple: **melody / instruments / intensity**. Change the relevant
part of the process while keeping the other two fixed. First try the current cue
in context before expanding the tooling or composing an entire soundtrack.

## Sources and attribution

GeneralUser GS 2.0.3 by S. Christian Collins:
https://github.com/mrbumpy409/GeneralUser-GS

Its supplied license permits private and commercial music creation and describes
the author's limits in tracing some older sample sources. The exact upstream
license and the downloaded bank's SHA-256 are included under `licenses/`.
The sound bank and renderer are authoring dependencies and are not in the game zip.

FluidSynth: https://github.com/FluidSynth/fluidsynth (LGPL-2.1).
Build uses NumPy, SciPy, SoundFile, Mido and imageio-ffmpeg. Their installed
distributions retain their licenses under the ignored dependency folder.
