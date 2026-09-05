"""Select key poses and remove a green screen without rerunning the video model."""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


def key_green(image):
    """Approximate chroma key for artwork with no intentional green areas."""
    rgb = np.asarray(image.convert("RGB"), dtype=np.float32) / 255
    other = np.maximum(rgb[..., 0], rgb[..., 2])
    dominance = rgb[..., 1] - other
    alpha = 1 - np.clip((dominance - 0.08) / 0.35, 0, 1)
    # Gurgle has white, red, and black artwork. Remove green spill from edge pixels.
    rgb[..., 1] = np.minimum(rgb[..., 1], other)
    rgba = np.dstack((rgb, alpha))
    rgba[alpha == 0, :3] = 0
    return Image.fromarray(np.rint(rgba * 255).astype(np.uint8))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("run", type=Path)
    parser.add_argument("--frames", required=True, help="Comma-separated zero-based source frame indices")
    parser.add_argument("--durations", default="", help="Comma-separated milliseconds, one per selected pose")
    parser.add_argument("--name", default="five-pose-study")
    args = parser.parse_args()
    indices = [int(value) for value in args.frames.split(",")]
    durations = [int(value) for value in args.durations.split(",")] if args.durations else [100] * len(indices)
    if len(indices) != len(durations) or any(value < 10 for value in durations):
        parser.error("Specify one duration >= 10 ms per pose")
    if not args.name or Path(args.name).name != args.name or args.name in (".", ".."):
        parser.error("name must be a single directory name")
    sources = sorted((args.run / "raw").glob("frame_*.png"))
    if any(index < 0 or index >= len(sources) for index in indices):
        parser.error(f"Frame indices must be between 0 and {len(sources)-1}")
    output = args.run / args.name
    output.mkdir(exist_ok=False)
    images = [key_green(Image.open(sources[index])) for index in indices]
    width, height = images[0].size
    sheet = Image.new("RGBA", (width * len(images), height))
    dark, light = [], []
    metrics = []
    for ordinal, (source_index, image) in enumerate(zip(indices, images)):
        image.save(output / f"frame-{ordinal+1:04d}.png")
        sheet.paste(image, (ordinal*width, 0))
        alpha = np.asarray(image.getchannel("A"))
        metrics.append({"source_frame": source_index, "duration_ms": durations[ordinal],
                        "transparent_fraction": float((alpha == 0).mean()), "bounds": image.getbbox()})
        for color, collection in [((40, 43, 52, 255), dark), ((238, 231, 210, 255), light)]:
            composite = Image.new("RGBA", image.size, color)
            composite.alpha_composite(image)
            collection.append(composite.convert("RGB"))
    sheet.save(output / "spritesheet.png")
    # GIF is a convenient preview. The PNG frames retain full alpha precision.
    for name, collection in [("preview-dark.gif", dark), ("preview-light.gif", light)]:
        collection[0].save(output / name, save_all=True, append_images=collection[1:],
                           duration=durations, loop=0, disposal=2)
    tile = 192
    review = Image.new("RGB", (tile * len(images), tile + 38), "#282b34")
    draw = ImageDraw.Draw(review)
    for ordinal, image in enumerate(dark):
        image = image.copy()
        image.thumbnail((tile, tile))
        review.paste(image, (ordinal*tile, 38))
        draw.text((ordinal*tile+6, 6), f"Pose {ordinal+1} / source {indices[ordinal]}", fill="white")
        draw.text((ordinal*tile+6, 22), f"{durations[ordinal]} ms", fill="#acb2bc")
    review.save(output / "poses.png")
    manifest = {"source_run": str(args.run.resolve()), "width": width, "height": height,
                "duration_ms": sum(durations), "frames": metrics,
                "background_removal": "Approximate green-screen key; original RGB frames preserved",
                "registration": "Original fixed canvas; no per-frame scaling or recentering",
                "note": "Preview metadata, not an App2d runtime character manifest"}
    (output / "animation.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(output.resolve())


if __name__ == "__main__":
    main()
