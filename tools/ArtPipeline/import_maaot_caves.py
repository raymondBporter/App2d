#!/usr/bin/env python3
"""Adapt the locally downloaded Maaot cave packs to App2d terrain assets."""

from __future__ import annotations

import argparse
import io
import json
import shutil
import zipfile
from pathlib import Path

from PIL import Image


TILE_SIZE = 32


def read_image(archive: Path, member: str) -> Image.Image:
    if not archive.is_file():
        raise FileNotFoundError(
            f"Missing {archive}. Download the pack named in "
            "Assets/Sources/third-party/maaot/provenance.md."
        )
    with zipfile.ZipFile(archive) as source:
        return Image.open(io.BytesIO(source.read(member))).convert("RGBA")


def crop(image: Image.Image, box: tuple[int, int, int, int]) -> Image.Image:
    return image.crop(box)


def save(image: Image.Image, path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)


def save_manifest(
    output: Path,
    tileset_id: str,
    surface_thickness: int,
    outer_corner_size: int,
    inner_corner_size: int,
    one_way_height: int,
    spike_height: int,
) -> None:
    manifest = {
        "id": tileset_id,
        "tileSize": TILE_SIZE,
        "surfaceThickness": surface_thickness,
        "outerCornerSize": outer_corner_size,
        "innerCornerSize": inner_corner_size,
        "oneWayVisualHeight": one_way_height,
        "spikeVisualHeight": spike_height,
    }
    (output / "tileset.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )


def prepare_output(root: Path, tileset_id: str) -> Path:
    output = root / tileset_id
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)
    return output


def import_mossy(archive: Path, output_root: Path) -> None:
    output = prepare_output(output_root, "mossy-cavern")
    sheet = read_image(archive, "Mossy Tileset/Mossy - TileSet.png")
    platforms = read_image(
        archive,
        "Mossy Tileset/Mossy - FloatingPlatforms.png",
    )
    hazards = read_image(
        archive,
        "Mossy Tileset/Mossy - Decorations&Hazards.png",
    )

    # The first 3x3 cells are the pack's 512px terrain neighborhood. Preserve
    # the high-resolution brushwork while exposing the narrow edge pieces that
    # App2d's topology renderer expects.
    save(crop(sheet, (512, 512, 1024, 1024)), output / "fill.png")
    save(crop(sheet, (512, 0, 1024, 192)), output / "surfaces/top.png")
    save(crop(sheet, (512, 1344, 1024, 1536)), output / "surfaces/bottom.png")
    save(crop(sheet, (0, 512, 192, 1024)), output / "surfaces/left.png")
    save(crop(sheet, (1344, 512, 1536, 1024)), output / "surfaces/right.png")
    save(crop(sheet, (0, 0, 256, 256)), output / "corners/outer.png")
    save(crop(sheet, (1280, 1280, 1536, 1536)), output / "corners/inner.png")

    standalone = crop(platforms, (47, 36, 428, 367))
    save(standalone, output / "one-way/standalone.png")
    # The lowest platform is a clean continuous strip. Its end and center
    # samples join naturally when a procedural one-way platform spans tiles.
    save(crop(platforms, (20, 1566, 532, 2016)), output / "one-way/left.png")
    save(crop(platforms, (768, 1566, 1280, 2016)), output / "one-way/middle.png")
    save(crop(platforms, (1521, 1566, 2033, 2016)), output / "one-way/right.png")
    spikes = crop(hazards, (3100, 1900, 3980, 2320))
    spike_third = spikes.width // 3
    save(spikes, output / "hazards/spikes/standalone.png")
    save(crop(spikes, (0, 0, spike_third, spikes.height)), output / "hazards/spikes/left.png")
    save(crop(spikes, (spike_third, 0, spike_third * 2, spikes.height)), output / "hazards/spikes/middle.png")
    save(crop(spikes, (spike_third * 2, 0, spikes.width, spikes.height)), output / "hazards/spikes/right.png")
    save_manifest(output, "mossy-cavern", 12, 16, 14, 32, 30)


def import_dark(archive: Path, output_root: Path) -> None:
    output = prepare_output(output_root, "dark-cave")
    floor = read_image(archive, "Assets 1024 Cave/Cave - Floor.png")
    platforms = read_image(archive, "Assets 1024 Cave/Cave - Platforms.png")
    black_fill = read_image(archive, "Assets 1024 Cave/Square - Black.jpg")
    combinations = read_image(
        archive,
        "Assets 1024 Cave/Cave - RockCombinations1.png",
    )

    # DarkCave is a sprite atlas rather than a grid tileset. Its included black
    # square is the intended backing layer; cropping across the platform atlas
    # would repeat several sprites and their transparent gutters as one tile.
    save(black_fill, output / "fill.png")
    top = crop(floor, (68, 54, 438, 146))
    save(top, output / "surfaces/top.png")
    save(top.transpose(Image.Transpose.FLIP_TOP_BOTTOM), output / "surfaces/bottom.png")
    save(top.rotate(90, expand=True), output / "surfaces/left.png")
    save(top.rotate(-90, expand=True), output / "surfaces/right.png")
    save(crop(floor, (1345, 54, 1559, 268)), output / "corners/outer.png")
    save(crop(floor, (1721, 54, 1935, 268)), output / "corners/inner.png")

    platform = crop(platforms, (269, 903, 656, 1006))
    third = platform.width // 3
    save(platform, output / "one-way/standalone.png")
    save(crop(platform, (0, 0, third, platform.height)), output / "one-way/left.png")
    save(crop(platform, (third, 0, third * 2, platform.height)), output / "one-way/middle.png")
    save(crop(platform, (third * 2, 0, platform.width, platform.height)), output / "one-way/right.png")
    spikes = crop(combinations, (44, 1234, 619, 1955))
    spike_third = spikes.width // 3
    save(crop(combinations, (1571, 1234, 1969, 1955)), output / "hazards/spikes/standalone.png")
    save(crop(spikes, (0, 0, spike_third, spikes.height)), output / "hazards/spikes/left.png")
    save(crop(spikes, (spike_third, 0, spike_third * 2, spikes.height)), output / "hazards/spikes/middle.png")
    save(crop(spikes, (spike_third * 2, 0, spikes.width, spikes.height)), output / "hazards/spikes/right.png")
    save_manifest(output, "dark-cave", 8, 12, 10, 24, 32)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--content-root", type=Path)
    arguments = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    sources = repository / "Assets/Sources/third-party/maaot"
    content_root = arguments.content_root or repository / "Assets/Runtime"
    output = content_root / "environments/tilesets"
    import_dark(sources / "dark-cave.zip", output)
    import_mossy(sources / "mossy-cavern.zip", output)
    print("Imported Maaot cave environments.")


if __name__ == "__main__":
    main()
