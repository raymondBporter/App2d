#!/usr/bin/env python3
"""Split an AI sprite sheet, remove connected neutral background, and register frames."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image


FOREGROUND_ALPHA_THRESHOLD = 24


def visible_alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    """Ignore near-invisible alpha haze when measuring generated foreground."""
    alpha = image.convert("RGBA").getchannel("A")
    visible = alpha.point(lambda value: 255 if value > FOREGROUND_ALPHA_THRESHOLD else 0)
    return visible.getbbox()


def resize_rgba_premultiplied(image: Image.Image, size: tuple[int, int]) -> Image.Image:
    """Resize RGBA without allowing hidden transparent RGB to contaminate edges."""
    rgba = np.asarray(image.convert("RGBA"), dtype=np.float32)
    alpha = rgba[:, :, 3:4] / 255.0
    premultiplied = np.concatenate((rgba[:, :, :3] * alpha, rgba[:, :, 3:4]), axis=2)
    premultiplied_image = Image.fromarray(
        np.clip(np.rint(premultiplied), 0, 255).astype(np.uint8),
        "RGBA",
    )
    resized = np.asarray(
        premultiplied_image.resize(size, Image.Resampling.LANCZOS),
        dtype=np.float32,
    )
    resized_alpha = resized[:, :, 3:4]
    straight_rgb = np.zeros_like(resized[:, :, :3])
    np.divide(
        resized[:, :, :3] * 255.0,
        resized_alpha,
        out=straight_rgb,
        where=resized_alpha > 0,
    )
    straight = np.concatenate((straight_rgb, resized_alpha), axis=2)
    return Image.fromarray(np.clip(np.rint(straight), 0, 255).astype(np.uint8), "RGBA")


def helmet_anchor(rgba: np.ndarray) -> tuple[float, float]:
    alpha = rgba[:, :, 3] > 24
    ys, xs = np.nonzero(alpha)
    if len(xs) == 0:
        raise ValueError("Frame contains no foreground")
    top = int(ys.min())
    bottom = int(ys.max())
    upper_limit = top + int((bottom - top + 1) * 0.48)
    red = rgba[:, :, 0].astype(np.int16)
    green = rgba[:, :, 1].astype(np.int16)
    blue = rgba[:, :, 2].astype(np.int16)
    blue_armor = alpha & (blue >= red + 18) & (green >= red + 8)
    blue_armor[upper_limit + 1 :, :] = False
    helmet_y, helmet_x = np.nonzero(blue_armor)
    if len(helmet_x) < 50:
        return float((xs.min() + xs.max()) / 2), float(top)
    return float(np.median(helmet_x)), float(np.median(helmet_y))


def split_boundaries(length: int, parts: int) -> list[int]:
    return [round(index * length / parts) for index in range(parts + 1)]


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--sheet", type=Path, required=True)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    profile = json.loads(args.profile.read_text(encoding="utf-8"))
    frame_config = profile["frames"]
    registration = profile["registration"]
    columns = frame_config["sheetColumns"]
    rows = frame_config["sheetRows"]
    frame_count = frame_config["count"]
    canvas = (registration["canvasWidth"], registration["canvasHeight"])
    target_height = registration["targetForegroundHeight"]
    target_x = registration["rootX"]
    ground_y = registration["groundY"]

    sheet = Image.open(args.sheet)
    if sheet.mode not in ("RGBA", "LA") or "A" not in sheet.getbands():
        raise ValueError(
            "Painted sheet has no native alpha channel. Rejecting it: an opaque or "
            "checkerboard background cannot be removed without contaminating antialiased edges."
        )
    sheet = sheet.convert("RGBA")
    alpha_extrema = sheet.getchannel("A").getextrema()
    if alpha_extrema[0] == 255:
        raise ValueError(
            "Painted sheet is fully opaque. Rejecting it rather than synthesizing transparency."
        )
    x_edges = split_boundaries(sheet.width, columns)
    y_edges = split_boundaries(sheet.height, rows)
    args.output.mkdir(parents=True, exist_ok=True)
    report = {"sourceSheet": str(args.sheet), "frames": []}
    normalized_frames: list[Image.Image] = []

    for index in range(frame_count):
        column = index % columns
        row = index // columns
        cell = sheet.crop((x_edges[column], y_edges[row], x_edges[column + 1], y_edges[row + 1]))
        extracted = cell
        bbox = visible_alpha_bbox(extracted)
        if bbox is None:
            raise ValueError(f"No foreground found in cell {index + 1}")
        cropped = extracted.crop(bbox)
        scale = target_height / cropped.height
        size = (max(1, round(cropped.width * scale)), target_height)
        resized = resize_rgba_premultiplied(cropped, size)

        resized_array = np.asarray(resized, dtype=np.uint8)
        anchor_x, _ = helmet_anchor(resized_array)
        paste_x = round(target_x - anchor_x)
        paste_y = ground_y - resized.height
        frame = Image.new("RGBA", canvas, (0, 0, 0, 0))
        frame.alpha_composite(resized, (paste_x, paste_y))
        output_path = args.output / f"walk-{index + 1:02d}.png"
        frame.save(output_path)
        normalized_frames.append(frame)
        final_bbox = frame.getchannel("A").getbbox()
        report["frames"].append(
            {
                "file": output_path.name,
                "sourceCell": [x_edges[column], y_edges[row], x_edges[column + 1], y_edges[row + 1]],
                "sourceBounds": list(bbox),
                "scale": scale,
                "paste": [paste_x, paste_y],
                "finalBounds": list(final_bbox) if final_bbox else None,
            }
        )

    preview_sheet = Image.new(
        "RGBA",
        (canvas[0] * columns, canvas[1] * rows),
        (32, 34, 40, 255),
    )
    for index, frame in enumerate(normalized_frames):
        preview_sheet.alpha_composite(frame, ((index % columns) * canvas[0], (index // columns) * canvas[1]))
    preview_sheet.save(args.output / "walk-normalized-sheet.png")
    normalized_frames[0].save(
        args.output / "walk-preview.gif",
        save_all=True,
        append_images=normalized_frames[1:],
        duration=round(1000 / frame_config["framesPerSecond"]),
        loop=0,
        disposal=2,
    )
    (args.output / "normalization-report.json").write_text(json.dumps(report, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
