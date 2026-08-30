#!/usr/bin/env python3
"""Adapt Kenney's CC0 Pixel Platformer pack to App2d terrain assets."""

from __future__ import annotations

import argparse
import io
import json
import shutil
import zipfile
from pathlib import Path

from PIL import Image, ImageDraw


TILE_SIZE = 32
SOURCE_TILE_SIZE = 18
SURFACE_THICKNESS = 8
OUTER_CORNER_SIZE = 8
INNER_CORNER_SIZE = 6
ONE_WAY_HEIGHT = 12
SPIKE_HEIGHT = 16
OUTLINE = (66, 69, 86, 255)


def read_tile(archive: Path, index: int) -> Image.Image:
    if not archive.is_file():
        raise FileNotFoundError(
            f"Missing {archive}. Download the pack named in "
            "Assets/Sources/third-party/kenney/provenance.md."
        )
    member = f"Tiles/tile_{index:04}.png"
    with zipfile.ZipFile(archive) as source:
        return Image.open(io.BytesIO(source.read(member))).convert("RGBA")


def resize_tile(image: Image.Image) -> Image.Image:
    return image.resize((TILE_SIZE, TILE_SIZE), Image.Resampling.NEAREST)


def save(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)


def bordered_surface(fill: Image.Image, side: str) -> Image.Image:
    if side in {"top", "bottom"}:
        y = 0 if side == "top" else TILE_SIZE - SURFACE_THICKNESS
        surface = fill.crop((0, y, TILE_SIZE, y + SURFACE_THICKNESS))
        draw = ImageDraw.Draw(surface)
        line_y = 0 if side == "top" else SURFACE_THICKNESS - 2
        draw.rectangle((0, line_y, TILE_SIZE - 1, line_y + 1), fill=OUTLINE)
        return surface

    x = 0 if side == "left" else TILE_SIZE - SURFACE_THICKNESS
    surface = fill.crop((x, 0, x + SURFACE_THICKNESS, TILE_SIZE))
    draw = ImageDraw.Draw(surface)
    line_x = 0 if side == "left" else SURFACE_THICKNESS - 2
    draw.rectangle((line_x, 0, line_x + 1, TILE_SIZE - 1), fill=OUTLINE)
    return surface


def crop_alpha_strip(image: Image.Image, height: int) -> Image.Image:
    alpha = image.getchannel("A")
    bounds = alpha.getbbox()
    if bounds is None:
        raise ValueError("Expected a visible strip tile.")
    visible = image.crop((0, bounds[1], SOURCE_TILE_SIZE, bounds[3]))
    return visible.resize((TILE_SIZE, height), Image.Resampling.NEAREST)


def standalone_strip(left: Image.Image, right: Image.Image) -> Image.Image:
    half = TILE_SIZE // 2
    result = Image.new("RGBA", (TILE_SIZE, ONE_WAY_HEIGHT))
    result.alpha_composite(left.crop((0, 0, half, ONE_WAY_HEIGHT)), (0, 0))
    result.alpha_composite(
        right.crop((TILE_SIZE - half, 0, TILE_SIZE, ONE_WAY_HEIGHT)),
        (half, 0),
    )
    return result


def corner_marker(tile: Image.Image, size: int) -> Image.Image:
    bounds = tile.getchannel("A").getbbox()
    if bounds is None:
        raise ValueError("Expected a visible corner marker tile.")
    return tile.crop(bounds).resize((size, size), Image.Resampling.NEAREST)


def grass_surface(tile: Image.Image) -> Image.Image:
    # Kenney's tile begins with a two-pixel charcoal outline. App2d already
    # supplies the terrain silhouette, so retain the grass/dirt band instead
    # of turning the exposed top into a dark bar.
    return tile.crop((0, 2, SOURCE_TILE_SIZE, 10)).resize(
        (TILE_SIZE, SURFACE_THICKNESS),
        Image.Resampling.NEAREST,
    )


def import_grassland(archive: Path, output_root: Path) -> None:
    tileset_id = "kenney-grassland"
    output = output_root / tileset_id
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)

    fill = resize_tile(read_tile(archive, 104))
    grass_top = grass_surface(read_tile(archive, 22))
    save(fill, output / "fill.png")
    grippable = fill.copy()
    grippable.alpha_composite(resize_tile(read_tile(archive, 71)))
    save(grippable, output / "grippable.png")
    save(grass_top, output / "surfaces/top.png")
    save(bordered_surface(fill, "bottom"), output / "surfaces/bottom.png")
    save(bordered_surface(fill, "left"), output / "surfaces/left.png")
    save(bordered_surface(fill, "right"), output / "surfaces/right.png")

    save(
        corner_marker(read_tile(archive, 7), OUTER_CORNER_SIZE),
        output / "corners/outer.png",
    )
    save(
        corner_marker(read_tile(archive, 8), INNER_CORNER_SIZE),
        output / "corners/inner.png",
    )

    platform_left = crop_alpha_strip(read_tile(archive, 48), ONE_WAY_HEIGHT)
    platform_middle = crop_alpha_strip(read_tile(archive, 49), ONE_WAY_HEIGHT)
    platform_right = crop_alpha_strip(read_tile(archive, 50), ONE_WAY_HEIGHT)
    save(
        standalone_strip(platform_left, platform_right),
        output / "one-way/standalone.png",
    )
    save(platform_left, output / "one-way/left.png")
    save(platform_middle, output / "one-way/middle.png")
    save(platform_right, output / "one-way/right.png")

    spikes = crop_alpha_strip(read_tile(archive, 68), SPIKE_HEIGHT)
    for part in ("standalone", "left", "middle", "right"):
        save(spikes, output / f"hazards/spikes/{part}.png")

    manifest = {
        "id": tileset_id,
        "tileSize": TILE_SIZE,
        "surfaceThickness": SURFACE_THICKNESS,
        "outerCornerSize": OUTER_CORNER_SIZE,
        "innerCornerSize": INNER_CORNER_SIZE,
        "oneWayVisualHeight": ONE_WAY_HEIGHT,
        "spikeVisualHeight": SPIKE_HEIGHT,
    }
    (output / "tileset.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--content-root", type=Path)
    arguments = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    archive = (
        repository
        / "Assets"
        / "Sources"
        / "third-party"
        / "kenney"
        / "pixel-platformer.zip"
    )
    content_root = arguments.content_root or repository / "Assets" / "Runtime"
    output_root = content_root / "environments" / "tilesets"
    import_grassland(archive, output_root)
    print("Imported Kenney Pixel Platformer grassland environment.")


if __name__ == "__main__":
    main()
