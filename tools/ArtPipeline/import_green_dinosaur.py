#!/usr/bin/env python3
"""Import the user-provided green dinosaur walk cycle."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from PIL import Image


FRAME_COUNT = 6
SOURCE_CELL_WIDTH = 362
SOURCE_HEIGHT = 724
FRAME_CROP_TOP = 184
OUTPUT_SIZE = 362


def import_walk_cycle(source: Path, content_root: Path) -> None:
    character_root = content_root / "characters/green-dinosaur"
    if character_root.exists():
        shutil.rmtree(character_root)

    animation_root = character_root / "animations/walk"
    animation_root.mkdir(parents=True)

    with Image.open(source) as opened:
        expected_size = (SOURCE_CELL_WIDTH * FRAME_COUNT, SOURCE_HEIGHT)
        if opened.size != expected_size:
            raise ValueError(
                f"Green dinosaur sheet must be {expected_size[0]}x{expected_size[1]}; "
                f"got {opened.width}x{opened.height}."
            )

        sheet = opened.convert("RGBA")
        for index in range(FRAME_COUNT):
            left = index * SOURCE_CELL_WIDTH
            frame = sheet.crop(
                (
                    left,
                    FRAME_CROP_TOP,
                    left + OUTPUT_SIZE,
                    FRAME_CROP_TOP + OUTPUT_SIZE,
                )
            )
            frame.save(animation_root / f"frame-{index + 1:04}.png", optimize=True)

    manifest = {
        "id": "green-dinosaur",
        "animations": {
            "walk": {
                "framesPerSecond": 8,
                "loop": True,
            }
        },
    }
    (character_root / "character.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )
    print("Imported green dinosaur walk cycle.")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--content-root", type=Path)
    arguments = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    source = repository / "Assets/Sources/user/green-dinosaur/walk-cycle.png"
    content_root = arguments.content_root or repository / "Assets/Runtime"
    import_walk_cycle(source, content_root)


if __name__ == "__main__":
    main()
