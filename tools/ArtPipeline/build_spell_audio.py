#!/usr/bin/env python3
"""Bake placeholder spell cues from the same tuning file used by gameplay."""
import argparse
import json
import math
import struct
import wave
from pathlib import Path
from build_gun_effects import sound


def heal_sound(path, kind, duration):
    rate = 44100
    frames = []
    for i in range(round(rate * duration)):
        t = i / rate
        p = t / duration
        attack = min(1, t / .025)
        if kind == "charge":
            envelope = attack * min(1, (duration - t) / .025) * (.18 + .22 * p)
            value = sum(math.sin(math.tau * f * t) for f in (220, 330, 440)) / 3
        elif kind == "complete":
            envelope = attack * (1 - p) ** 2 * .65
            value = sum(math.sin(math.tau * f * t) for f in (523.25, 659.25, 783.99)) / 3
        else:
            envelope = attack * (1 - p) ** 2 * .35
            value = math.sin(math.tau * (340 * t - 90 * t * p))
        frames.append(round(value * envelope * 32767))
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setparams((1, 2, rate, 0, "NONE", "not compressed"))
        output.writeframes(struct.pack(f"<{len(frames)}h", *frames))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--content-root", type=Path, required=True)
    root = parser.parse_args().content_root
    tuning = json.loads((root / "gameplay/player-spells.json").read_text())
    sound(root / "audio/sfx/gun-charge.wav", "charge", tuning["shotChargeSeconds"])
    for kind, duration in (("charge", tuning["healSeconds"]), ("complete", .45), ("cancel", .16)):
        heal_sound(root / f"audio/sfx/heal-{kind}.wav", kind, duration)
    print("Baked short spell charge and healing cues.")


if __name__ == "__main__":
    main()
