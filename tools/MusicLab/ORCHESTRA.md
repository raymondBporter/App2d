# Crown of Embers

An original orchestral game cue: 120 BPM, D minor, 4/4, 32 bars, 64 seconds.
Created as a separate musical direction from Copper Circuit, with a new melody.

## Listening

The page starts with **Epic** selected. Press Play, then try Explore and Advance.
Auto tour spends eight bars in Explore, eight in Advance, eight in Epic and
eight back in Explore. Mood changes fade at bar boundaries; all layers share
one playback position. `listen-first.mp3` is a standalone guided preview.

## Four layers

| Stem | Instruments |
| --- | --- |
| world / Landscape | Long low strings, soft upper strings and harp |
| theme / Hero theme | Horn melody with upper violin doubling |
| motion / String motion | Short string figures and cello pulses |
| battle / Battle | Brass accents, pitched timpani and drum/cymbal percussion |

The arrangement uses broad melodic phrases, an eight-bar lift, a restrained
middle section and a higher return of the opening theme. A shared simulated
hall connects the sampled sections. This is a synthesized orchestral sketch
using GeneralUser GS samples, not a recording of live players.

## Rebuild

From the repository root, with the existing Music Lab dependencies installed:

```powershell
python tools/MusicLab/orchestra.py
python tools/MusicLab/build.py --score Assets/Work/music-lab/copper-circuit/revisions/epic-orchestra/input-score.json --out Assets/Work/music-lab/copper-circuit/revisions/epic-orchestra --notes tools/MusicLab/ORCHESTRA.md
python tools/MusicLab/verify.py --out Assets/Work/music-lab/copper-circuit/revisions/epic-orchestra
```

The existing server serves this cue at `/revisions/epic-orchestra/`.
Edit composition data in `orchestra.py`; render and orchestral effects in
`build.py`. Dependencies are the same as Copper Circuit: Python, FluidSynth
2.6.1 and GeneralUser GS 2.0.3. `setup.py` installs the pinned local authoring
dependencies on Windows x64. No new paid service is required.

## Game delivery

`crown-of-embers-game.zip` contains four Ogg stems, playback manifest, this
document and the sound bank license/provenance. PCM masters are in `stems/`;
editable note data and MIDI are in `source/`. MP3 mixes are listening previews.

Every stem is 2,822,400 stereo frames at 44,100 Hz. Keep a shared sample cursor
and advance muted stems. Quantize mood changes to 88,200-frame bars and ramp
over one bar. Keep the manifest's relative gains and leave room for game SFX.
All four Ogg files together occupy about 3.6 MB; decoded float32 audio occupies
about 86.1 MiB. Stream through synchronized decoder buffers for lower RAM use.
Game playback requires no synthesizer or AI. Game integration is not included.

Validation checks frame alignment, finite samples, loop-boundary outliers,
headroom across all stem gain combinations, MIDI and archive integrity.
These checks do not judge musical quality; the assistant has not performed
a perceptual listening review.

## Attribution

GeneralUser GS 2.0.3 by S. Christian Collins:
https://github.com/mrbumpy409/GeneralUser-GS

The supplied license permits private and commercial music creation and explains
the author's limits in tracing some older sample sources. Its exact text and
the bank hash are included in `licenses/`. The bank and renderer are authoring
dependencies and are not shipped in the game package.

FluidSynth: https://github.com/FluidSynth/fluidsynth (LGPL-2.1).
Build dependencies: NumPy, SciPy, SoundFile, Mido and imageio-ffmpeg.
