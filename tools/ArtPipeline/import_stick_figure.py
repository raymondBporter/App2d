#!/usr/bin/env python3
"""Build the minimal baked sword/gun player art used by the prototype."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


CANVAS_SIZE = (512, 512)
SOURCE_ROOT = (256, 380)
TARGET_ROOT = (256, 325)
SCALE = 1.35
VISUAL_WORLD_SIZE = 138.0
COLLIDER_HEIGHT_INCREMENT = 4.0
HEAD_INSET_PIXELS = 11
MEASUREMENT_HALF_WIDTH = 85
FOOT_SAMPLE_DEPTH = 18


@dataclass(frozen=True)
class AnimationSpec:
    source_name: str
    frames_per_second: float | None = None
    duration_seconds: float | None = None
    loop: bool = False
    frame_indices: tuple[int, ...] | None = None


COMMON_ANIMATIONS = {
    "idle": AnimationSpec("Idle", frames_per_second=12, loop=True),
    "walk": AnimationSpec("run", frames_per_second=14, loop=True),
    "jump-start": AnimationSpec("jump", frames_per_second=14),
    "fall": AnimationSpec("jump", frames_per_second=1, loop=True, frame_indices=(2,)),
    "wall-grip": AnimationSpec("wallslide", frames_per_second=4, loop=True),
    "dash": AnimationSpec("dash", duration_seconds=0.16),
    "land": AnimationSpec("jump", frames_per_second=12, frame_indices=(0,)),
    "hit-a": AnimationSpec("hit", duration_seconds=0.28),
    "shield-block": AnimationSpec("Idle", frames_per_second=1, loop=True, frame_indices=(0,)),
}


def animation_specs(kind: str) -> dict[str, AnimationSpec]:
    specs = dict(COMMON_ANIMATIONS)
    if kind == "sword":
        attack = AnimationSpec("combo", duration_seconds=0.35)
        specs.update(
            {
                "sword-attack": attack,
                "magic-shot": AnimationSpec(
                    "Idle", duration_seconds=0.18, frame_indices=(0,)
                ),
            }
        )
    else:
        placeholder = AnimationSpec("shot", duration_seconds=0.35)
        specs.update(
            {
                "sword-attack": placeholder,
                "magic-shot": AnimationSpec("shot", duration_seconds=0.18),
            }
        )
    return specs


def transform_frame(source_path: Path) -> Image.Image:
    with Image.open(source_path) as opened:
        source = opened.convert("RGBA")

    scaled_size = tuple(round(value * SCALE) for value in source.size)
    scaled = source.resize(scaled_size, Image.Resampling.LANCZOS)
    destination = Image.new("RGBA", CANVAS_SIZE, (0, 0, 0, 0))
    offset = (
        round(TARGET_ROOT[0] - SOURCE_ROOT[0] * SCALE),
        round(TARGET_ROOT[1] - SOURCE_ROOT[1] * SCALE),
    )
    destination.alpha_composite(scaled, offset)
    return destination


def source_frames(source_directory: Path, prefix: str, spec: AnimationSpec) -> list[Path]:
    frames = sorted(source_directory.glob(f"{prefix}_{spec.source_name}_*.png"))
    if not frames:
        # The pack's wall-slide frames omit the separator before their number.
        frames = sorted(source_directory.glob(f"{prefix}_{spec.source_name}*.png"))
    if not frames:
        raise FileNotFoundError(
            f"No source frames matched {prefix}_{spec.source_name}_*.png in {source_directory}"
        )
    if spec.frame_indices is not None:
        frames = [frames[index] for index in spec.frame_indices]
    return frames


def build_character(
    content_root: Path,
    source_directory: Path,
    character_id: str,
    prefix: str,
) -> None:
    character_root = content_root / "characters" / character_id
    specifications = animation_specs(prefix)
    manifest: dict[str, object] = {"id": character_id, "animations": {}}

    for animation_id, spec in specifications.items():
        output_directory = character_root / "animations" / animation_id
        output_directory.mkdir(parents=True, exist_ok=True)
        for stale in output_directory.glob("frame-*.png"):
            stale.unlink()

        frames = source_frames(source_directory, prefix, spec)
        for frame_number, source_path in enumerate(frames, start=1):
            transform_frame(source_path).save(
                output_directory / f"frame-{frame_number:04d}.png",
                optimize=True,
            )

        definition: dict[str, object] = {"loop": spec.loop}
        if spec.duration_seconds is not None:
            definition["durationSeconds"] = spec.duration_seconds
        else:
            definition["framesPerSecond"] = spec.frames_per_second
        manifest["animations"][animation_id] = definition

    character_root.mkdir(parents=True, exist_ok=True)
    (character_root / "character.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )


def measured_bounds(
    frame_paths: list[Path],
    crop: tuple[int, int, int, int],
) -> tuple[int, int, int, int]:
    bounds: list[tuple[int, int, int, int]] = []
    for frame_path in frame_paths:
        with Image.open(frame_path) as opened:
            alpha = opened.convert("RGBA").getchannel("A").crop(crop)
        frame_bounds = alpha.getbbox()
        if frame_bounds is None:
            raise ValueError(f"Measurement crop contains no artwork: {frame_path}")
        bounds.append(
            (
                crop[0] + frame_bounds[0],
                crop[1] + frame_bounds[1],
                crop[0] + frame_bounds[2],
                crop[1] + frame_bounds[3],
            )
        )

    return (
        min(bound[0] for bound in bounds),
        min(bound[1] for bound in bounds),
        max(bound[2] for bound in bounds),
        max(bound[3] for bound in bounds),
    )


def lowest_head_top(frame_paths: list[Path]) -> int:
    crop = (
        TARGET_ROOT[0] - MEASUREMENT_HALF_WIDTH,
        0,
        TARGET_ROOT[0] + MEASUREMENT_HALF_WIDTH + 1,
        TARGET_ROOT[1],
    )
    tops: list[int] = []
    for frame_path in frame_paths:
        with Image.open(frame_path) as opened:
            alpha = opened.convert("RGBA").getchannel("A").crop(crop)
        frame_bounds = alpha.getbbox()
        if frame_bounds is None:
            raise ValueError(f"Head measurement crop contains no artwork: {frame_path}")
        tops.append(crop[1] + frame_bounds[1])
    return max(tops)


def snap(value: float, increment: float) -> float:
    return round(value / increment) * increment


def write_player_geometry(content_root: Path, character_ids: tuple[str, ...]) -> None:
    world_units_per_pixel = VISUAL_WORLD_SIZE / CANVAS_SIZE[1]
    idle_frames = [
        frame
        for character_id in character_ids
        for frame in sorted(
            (content_root / "characters" / character_id / "animations/idle").glob(
                "frame-*.png"
            )
        )
    ]
    if not idle_frames:
        raise FileNotFoundError("Player idle frames are required for geometry measurement.")

    foot_crop = (
        TARGET_ROOT[0] - MEASUREMENT_HALF_WIDTH,
        TARGET_ROOT[1] - FOOT_SAMPLE_DEPTH,
        TARGET_ROOT[0] + MEASUREMENT_HALF_WIDTH + 1,
        TARGET_ROOT[1] + 4,
    )
    feet = measured_bounds(idle_frames, foot_crop)
    foot_width_pixels = feet[2] - feet[0]
    foot_center_x = (feet[0] + feet[2] - 1) * 0.5
    standing_top = lowest_head_top(idle_frames) + HEAD_INSET_PIXELS

    collider_width = foot_width_pixels * world_units_per_pixel
    collider_center_offset_x = (
        foot_center_x - TARGET_ROOT[0]
    ) * world_units_per_pixel
    standing_height = snap(
        (TARGET_ROOT[1] - standing_top) * world_units_per_pixel,
        COLLIDER_HEIGHT_INCREMENT,
    )
    geometry = {
        "schemaVersion": 1,
        "canvasSizePixels": {"width": CANVAS_SIZE[0], "height": CANVAS_SIZE[1]},
        "visualSize": {"width": VISUAL_WORLD_SIZE, "height": VISUAL_WORLD_SIZE},
        "footAnchorYFraction": TARGET_ROOT[1] / CANVAS_SIZE[1],
        "standingCollider": {
            "size": {
                "width": round(collider_width, 6),
                "height": standing_height,
            },
            "centerOffsetX": round(collider_center_offset_x, 6),
        },
    }
    output_path = content_root / "characters/player-geometry.json"
    output_path.write_text(json.dumps(geometry, indent=2) + "\n", encoding="utf-8")
    print(
        "Measured player geometry: "
        f"visual {VISUAL_WORLD_SIZE:g}x{VISUAL_WORLD_SIZE:g}, "
        f"standing collider {collider_width:.2f}x{standing_height:g}, "
        f"center offset {collider_center_offset_x:.2f}."
    )


def build_icon(source_path: Path, crop: tuple[int, int, int, int], output_path: Path) -> None:
    with Image.open(source_path) as opened:
        icon = opened.convert("RGBA").crop(crop)
    icon.thumbnail((112, 52), Image.Resampling.LANCZOS)
    canvas = Image.new("RGBA", (128, 64), (0, 0, 0, 0))
    canvas.alpha_composite(
        icon,
        ((canvas.width - icon.width) // 2, (canvas.height - icon.height) // 2),
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    canvas.save(output_path, optimize=True)


def main() -> None:
    repository = Path(__file__).resolve().parents[2]
    pack_root = (
        repository
        / "Assets/Sources/third-party/rgs-stick-figure"
        / "Stick Figure Character Sprites 2D"
    )
    content_root = repository / "Assets/Content"

    sword_source = pack_root / "Sword sprites"
    pistol_source = pack_root / "Pistol sprites"
    if not sword_source.is_dir() or not pistol_source.is_dir():
        raise FileNotFoundError(
            "Extract the RGS stick-figure pack below "
            "Assets/Sources/third-party/rgs-stick-figure before building runtime art."
        )

    build_character(content_root, sword_source, "player-sword", "sword")
    build_character(content_root, pistol_source, "player-gun", "pistol")
    write_player_geometry(content_root, ("player-sword", "player-gun"))

    build_icon(
        sword_source / "sword_Idle_0001.png",
        (130, 240, 225, 350),
        content_root / "ui/hud/weapons/sword.png",
    )
    build_icon(
        pistol_source / "pistol_Idle_0001.png",
        (300, 260, 390, 330),
        content_root / "ui/hud/weapons/gun.png",
    )

    bullet_output = content_root / "effects/bullet/orange.png"
    bullet_output.parent.mkdir(parents=True, exist_ok=True)
    with Image.open(pack_root / "Extras/bullet.png") as bullet:
        bullet.convert("RGBA").save(bullet_output, optimize=True)

    print("Built baked player-sword and player-gun runtime art.")


if __name__ == "__main__":
    main()
