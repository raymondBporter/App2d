"""Copper Circuit: an original, deterministic rock / chip adaptive music sketch.

The score is separate from the renderer. Beat positions, pitches and velocities
are ordinary data; edit phrases here, or edit exported score.json and re-render.
No copyrighted melody or MIDI input is used.
"""
from __future__ import annotations

import json
from pathlib import Path

BPM = 144
BARS = 32
BEATS_PER_BAR = 4
SEED = 260926


def pitch(name: str) -> int:
    if isinstance(name, int):
        return name
    pc = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}[name[0]]
    accidental = (1 if "#" in name else -1 if "b" in name else 0)
    return (int(name[-1]) + 1) * 12 + pc + accidental


def make_score() -> dict:
    # Programs are zero-based General MIDI. Percussion uses bank 128.
    tracks = {
        "bass": dict(stem="world", engine="sf2", program=34, bank=0, pan=0.0, level=0.90, notes=[]),
        "clean": dict(stem="world", engine="sf2", program=27, bank=0, pan=-0.25, level=0.58, notes=[]),
        "organ": dict(stem="world", engine="sf2", program=16, bank=0, pan=0.22, level=0.14, notes=[]),
        "chip": dict(stem="theme", engine="pulse", duty=0.25, pan=0.02, level=0.28, notes=[]),
        "answer": dict(stem="theme", engine="pulse", duty=0.50, pan=0.30, level=0.10, notes=[]),
        "guitar_l": dict(stem="rock", engine="sf2", program=29, bank=0, pan=-0.68, level=0.56, notes=[]),
        "guitar_r": dict(stem="rock", engine="sf2", program=30, bank=0, pan=0.68, level=0.41, notes=[]),
        "kit": dict(stem="rock", engine="sf2", program=0, bank=128, pan=0.0, level=1.12, notes=[]),
        "solo": dict(stem="overdrive", engine="sf2", program=29, bank=0, pan=-0.18, level=0.48, notes=[]),
        "power_kit": dict(stem="overdrive", engine="sf2", program=0, bank=128, pan=0.0, level=0.62, notes=[]),
    }

    def note(track, beat, name, duration, velocity=90):
        if name == "-":
            return
        tracks[track]["notes"].append(dict(beat=round(beat, 6), pitch=pitch(name),
                                         duration=round(duration, 6), velocity=velocity))

    # Harmonic rhythm: eight-bar adventurous minor theme; relative-major lift;
    # a quieter suspended bridge; then the opening theme with a final turnaround.
    chords = {
        "Em": ("E2", ["E3", "G3", "B3", "E4"]),
        "C": ("C2", ["E3", "G3", "C4", "E4"]),
        "G": ("G2", ["D3", "G3", "B3", "D4"]),
        "D": ("D2", ["D3", "F#3", "A3", "D4"]),
        "Am": ("A2", ["E3", "A3", "C4", "E4"]),
        "B": ("B1", ["D#3", "F#3", "A3", "B3"]),
        "Dsus": ("D2", ["D3", "G3", "A3", "D4"]),
    }
    progression = [
        "Em", "C", "G", "D", "Em", "Am", "C", "B",
        "G", "D", "Em", "C", "Am", "Em", "C", "B",
        "Am", "C", "Em", "Dsus", "Am", "C", "B", "B",
        "Em", "C", "G", "D", "Am", "C", "B", "B",
    ]

    # Each explicit phrase totals four beats. Rests are compositional space.
    phrases = [
        [("E5",.5),("G5",.5),("B5",.75),("A5",.25),("G5",.5),("E5",.5),("B4",.5),("-",.5)],
        [("E5",.75),("G5",.25),("A5",.5),("G5",.5),("E5",1),("D5",.5),("-",.5)],
        [("D5",.5),("G5",.5),("B5",1),("A5",.5),("G5",.5),("D5",.5),("-",.5)],
        [("F#5",.75),("A5",.25),("G5",.5),("F#5",.5),("E5",.5),("D5",.5),("F#5",.5),("-",.5)],
        [("E5",.5),("G5",.5),("B5",.5),("E6",.5),("D6",.5),("B5",.5),("G5",.5),("-",.5)],
        [("A5",1),("G5",.5),("E5",.5),("C5",.5),("E5",.5),("G5",.5),("-",.5)],
        [("G5",.5),("E5",.5),("D5",.5),("C5",.5),("E5",1),("G5",.5),("-",.5)],
        [("F#5",.5),("D#5",.5),("B4",1),("D#5",.5),("F#5",.5),("B5",.5),("-",.5)],
        [("B5",1),("D6",.5),("B5",.5),("A5",.5),("G5",1),("-",.5)],
        [("A5",.75),("F#5",.25),("D5",1),("F#5",.5),("A5",.5),("D6",.5),("-",.5)],
        [("B5",.5),("G5",.5),("E5",1),("G5",.5),("B5",.5),("E6",.5),("-",.5)],
        [("D6",.5),("C6",.5),("B5",.5),("G5",.5),("E5",1.5),("-",.5)],
        [("A5",.75),("C6",.25),("B5",.5),("A5",.5),("G5",.5),("E5",1),("-",.5)],
        [("G5",.5),("B5",.5),("E6",1),("D6",.5),("B5",.5),("G5",.5),("-",.5)],
        [("E5",.5),("G5",.5),("C6",1),("B5",.5),("G5",.5),("E5",.5),("-",.5)],
        [("D#5",.5),("F#5",.5),("B5",.75),("A5",.25),("F#5",.5),("D#5",.5),("B4",.5),("-",.5)],
        [("E5",1.5),("C5",.5),("A4",1),("-",1)],
        [("G4",.5),("C5",.5),("E5",1.5),("D5",.5),("-",1)],
        [("B4",1),("E5",1),("G5",1),("-",1)],
        [("A5",1),("G5",1.5),("D5",.5),("-",1)],
        [("E5",.75),("A5",.25),("C6",1),("B5",.5),("A5",.5),("-",1)],
        [("G5",1),("E5",.5),("D5",.5),("C5",1),("-",1)],
        [("D#5",1),("F#5",1),("A5",1),("-",1)],
        [("B5",1.5),("A5",.5),("F#5",.5),("D#5",.5),("F#5",.5),("-",.5)],
    ]
    phrases += [phrases[i] for i in [0,1,2,3,5,6,7]]
    phrases += [[("F#5",.5),("D#5",.5),("B4",.5),("D#5",.5),
                 ("F#5",.5),("A5",.5),("B5",.5),("D#6",.5)]]
    assert len(phrases) == BARS

    for bar, chord in enumerate(progression):
        start = bar * 4
        root_name, voicing = chords[chord]
        root = pitch(root_name)
        bridge = 16 <= bar < 24
        # Bass makes a recognizable rock groove, leaving room for the kick.
        bass_pattern = [(0,0,.85,105),(1,0,.38,82),(1.5,7,.35,89),
                        (2,12,.7,95),(3,7,.35,82),(3.5,0,.35,91)]
        if bridge:
            bass_pattern = [(0,0,1.65,84),(2,7,.8,75),(3,12,.65,78)]
        for off, interval, length, vel in bass_pattern:
            note("bass", start+off, root+interval, length, vel)
        # Clean arpeggios are heard in exploration, and support the rock mix.
        order = [0,2,1,3,2,1,3,2] if not bridge else [0,2,3,2]
        spacing = .5 if not bridge else 1
        for j, idx in enumerate(order):
            note("clean", start+j*spacing+.008, voicing[idx], spacing*.85,
                 [74,59,66,62][j % 4])
        for n in voicing[:3]:
            note("organ", start+.025, n, 3.8, 46 if not bridge else 58)

        cursor = 0
        assert abs(sum(d for _,d in phrases[bar])-4) < 1e-6
        for j, (n, length) in enumerate(phrases[bar]):
            note("chip", start+cursor, n, length*(.85 if length < 1 else .93),
                 (87 if bridge else 101) - (j % 3)*5)
            cursor += length
        # Brief stereo answers, rather than an unbroken wall of arpeggios.
        if bar % 2 == 1:
            for off, idx in [(3.0,0),(3.25,1),(3.5,2),(3.75,1)]:
                note("answer", start+off, pitch(voicing[idx])+12, .16, 72)

        guitar_root = root if root >= 40 else root+12
        # Palm-muted eighths alternate with longer open power chords.
        strokes = [(0,.35,108),(.75,.18,76),(1.5,.32,92),(2,.38,102),
                   (2.75,.16,77),(3.5,.38,96)]
        if bridge:
            strokes = [(0,1.6,86),(2,1.3,79)]
        for off, length, vel in strokes:
            for k, interval in enumerate([0,7,12]):
                note("guitar_l", start+off+k*.009, guitar_root+interval, length, vel-k*7)
                note("guitar_r", start+off+.014+k*.011, guitar_root+interval, length*.92, vel-k*8-5)

        # A played-in acoustic kit with deterministic velocity accents.
        kick_offsets = [0,1.5,2,2.75] if bar % 2 == 0 else [0,.75,2,3.5]
        if bridge:
            kick_offsets = [0,2.5]
        for off in kick_offsets:
            note("kit", start+off, 36, .13, 106 if off in (0,2) else 89)
        for off in ([2] if bridge else [1,3]):
            note("kit", start+off+.010, 38, .18, 104 if not bridge else 91)
        for j in range(8):
            if bridge and j % 2:
                continue
            is_open = j == 7 and bar % 4 == 3
            note("kit", start+j*.5+.003, 46 if is_open else 42, .16 if is_open else .09,
                 (67 if j%2 == 0 else 46) - (7 if bridge else 0))
        if bar in [0,8,16,24]:
            note("kit", start, 49, .5, 76)
        if bar % 8 == 7:
            for j,n in enumerate([38,48,45,43]):
                note("kit", start+3+j*.25, n, .16, 77+j*5)

        # Combat adds a distinct high guitar line and fills. It doesn't replay
        # the same kick/snare stem, which would just make the mix louder.
        if bar % 4 in [0,1]:
            cursor = 0
            for j,(n,length) in enumerate(phrases[bar]):
                if n != "-" and (j % 2 == 0 or length >= 1):
                    note("solo", start+cursor+.015, pitch(n)-12, length*.8, 86)
                cursor += length
        else:
            for off,idx in [(0,0),(.75,2),(1.5,3),(2.25,2),(3,1),(3.5,2)]:
                note("solo", start+off, pitch(voicing[idx])+12, .30, 82)
        for off in [.5,1.5,2.5,3.5]:
            note("power_kit", start+off, 54, .09, 54 if bridge else 65)
        if bar % 4 == 3:
            for j,n in enumerate([45,45,47,43,41,41]):
                note("power_kit", start+2.5+j*.25, n, .13, 78+j*4)
        elif bar % 2 == 0:
            for off in [0,2]:
                note("power_kit", start+off, 41, .22, 82)
        if bar % 8 == 0:
            note("power_kit", start, 57, .6, 88)

    return dict(title="Copper Circuit", bpm=BPM, bars=BARS, beats_per_bar=4,
                sample_rate=44100, seed=SEED, key="E minor", progression=progression,
                sections=[dict(bar=1,name="Copper paths"),dict(bar=9,name="Open sky"),
                          dict(bar=17,name="Under the machinery"),dict(bar=25,name="Home stretch")],
                stems=["world","theme","rock","overdrive"],
                moods={"explore":[1,.82,0,0],"drive":[.92,1,.68,0],"combat":[.86,.95,1,.85]},
                tracks=tracks)


if __name__ == "__main__":
    path = Path(__file__).with_name("score.json")
    path.write_text(json.dumps(make_score(),indent=2)+"\n",encoding="utf-8")
    print(path)
