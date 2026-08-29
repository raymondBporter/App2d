#!/usr/bin/env python3
"""Normalize separately generated native-alpha animation frames."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from normalize_generated_sheet import helmet_anchor, resize_rgba_premultiplied, visible_alpha_bbox


def require_native_alpha(path: Path) -> Image.Image:
    image = Image.open(path)
    if image.mode not in ("RGBA", "LA") or "A" not in image.getbands():
        raise ValueError(
            f"{path.name} has no native alpha channel. Rejecting it; background removal is not permitted."
        )
    image = image.convert("RGBA")
    if image.getchannel("A").getextrema()[0] == 255:
        raise ValueError(f"{path.name} is fully opaque. Rejecting it; native transparent pixels are required.")
    return image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", type=Path, required=True)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument(
        "--allow-incomplete",
        action="store_true",
        help="Normalize the generated frames that exist so each candidate can be reviewed before the full cycle is ready.",
    )
    args = parser.parse_args()

    profile = json.loads(args.profile.read_text(encoding="utf-8"))
    frame_config = profile["frames"]
    registration = profile["registration"]
    count = frame_config["count"]
    canvas = (registration["canvasWidth"], registration["canvasHeight"])
    target_height = registration["targetForegroundHeight"]
    target_x = registration["rootX"]
    ground_y = registration["groundY"]
    args.output.mkdir(parents=True, exist_ok=True)

    normalized_frames: list[tuple[int, Image.Image]] = []
    report = {
        "sourceDirectory": str(args.input),
        "expectedFrameCount": count,
        "allowIncomplete": args.allow_incomplete,
        "frames": [],
    }
    for index in range(count):
        source = args.input / f"painted-{index + 1:02d}.png"
        if not source.exists():
            if args.allow_incomplete:
                continue
            raise FileNotFoundError(f"Missing required generated frame: {source}")
        image = require_native_alpha(source)
        bbox = visible_alpha_bbox(image)
        if bbox is None:
            raise ValueError(f"{source.name} has no visible foreground")
        cropped = image.crop(bbox)
        scale = target_height / cropped.height
        resized = resize_rgba_premultiplied(
            cropped,
            (max(1, round(cropped.width * scale)), target_height),
        )
        anchor_x, _ = helmet_anchor(np.asarray(resized, dtype=np.uint8))
        paste = (round(target_x - anchor_x), ground_y - resized.height)
        frame = Image.new("RGBA", canvas, (0, 0, 0, 0))
        frame.alpha_composite(resized, paste)
        target = args.output / f"walk-{index + 1:02d}.png"
        frame.save(target)
        normalized_frames.append((index, frame))
        report["frames"].append(
            {
                "file": target.name,
                "source": source.name,
                "sourceBounds": list(bbox),
                "scale": scale,
                "paste": list(paste),
                "finalBounds": list(frame.getchannel("A").getbbox()),
            }
        )

    if not normalized_frames:
        raise ValueError("No generated frames were found to normalize")

    report["normalizedFrameCount"] = len(normalized_frames)
    columns = frame_config["sheetColumns"]
    rows = frame_config["sheetRows"]
    preview = Image.new("RGBA", (canvas[0] * columns, canvas[1] * rows), (32, 34, 40, 255))
    for source_index, frame in normalized_frames:
        preview.alpha_composite(
            frame,
            ((source_index % columns) * canvas[0], (source_index // columns) * canvas[1]),
        )
    preview.save(args.output / "walk-normalized-sheet.png")
    animation_frames = [frame for _, frame in normalized_frames]
    animation_frames[0].save(
        args.output / "walk-preview.gif",
        save_all=True,
        append_images=animation_frames[1:],
        duration=round(1000 / frame_config["framesPerSecond"]),
        loop=0,
        disposal=2,
    )
    (args.output / "normalization-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
