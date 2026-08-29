#!/usr/bin/env python3
"""Build a shared character timeline and independent sparse layer packages."""

from __future__ import annotations

import argparse
import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image

from build_sparse_layer_package import (
    SparseFrame,
    alpha_bounds,
    create_sparse_frame,
    extract_frame,
    frame_manifest,
    image_equal,
    pack_frames,
    reconstruct_layer,
    write_atlases,
    load_atlas_pages,
)
from render_attached_weapon_layers import decode_depth
from render_loadout_equipment_layers import composite_depth_layers
from screen_space_motion import (
    ScreenSpaceMotionAnalyzer,
    frame_durations,
    playback_sample_times,
    select_motion_indices,
)


@dataclass
class BuiltPackage:
    package_id: str
    output: Path
    frames: dict[str, SparseFrame]
    pages: list[tuple[Image.Image, np.ndarray]]
    metrics: dict


def frame_paths(
    source_root: Path,
    layer_kind: str,
    clip_id: str,
    facing_id: str,
) -> tuple[list[Path], list[Path]]:
    if layer_kind == "character":
        clip_root = source_root / clip_id
        color_root = clip_root / "color" / facing_id
        depth_root = clip_root / "depth" / facing_id
    elif layer_kind == "equipment":
        clip_root = source_root / clip_id / facing_id
        color_root = clip_root / "color"
        depth_root = clip_root / "depth"
    else:
        raise ValueError(f"Unknown layer kind: {layer_kind}")
    return (
        sorted(color_root.glob("frame-*.png")),
        sorted(depth_root.glob("frame-*.png")),
    )


def selected_indices(frame_count: int, sample_count: int, loop: bool) -> list[int]:
    if sample_count >= frame_count:
        return list(range(frame_count))
    if sample_count == 1:
        return [0]
    if loop:
        return [int(math.floor(index * frame_count / sample_count)) for index in range(sample_count)]
    return [
        int(round(index * (frame_count - 1) / (sample_count - 1)))
        for index in range(sample_count)
    ]


def build_timeline(repository: Path, plan: dict) -> dict:
    character_root = repository / plan["character"]["contentRoot"]
    character_manifest = json.loads(
        (repository / plan["character"]["manifest"]).read_text(encoding="utf-8")
    )
    canvas = tuple(int(value) for value in plan["canvas"])
    target_fps = float(plan["targetFramesPerSecond"])
    sampling = plan.get("sampling", {"mode": "uniform"})
    sampling_mode = sampling.get("mode", "uniform")
    motion_analyzer = None
    if sampling_mode == "screen-space-motion":
        motion_analyzer = ScreenSpaceMotionAnalyzer(
            repository,
            repository / sampling["analysisPlan"],
        )
    elif sampling_mode != "uniform":
        raise ValueError(f"Unknown sampling mode: {sampling_mode}")
    animations = {}

    for clip_id in plan["clips"]:
        definition = character_manifest["animations"].get(clip_id)
        if definition is None:
            raise ValueError(f"Character manifest does not define {clip_id}")
        counts = set()
        for facing in plan["facings"]:
            colors, depths = frame_paths(character_root, "character", clip_id, facing["id"])
            if len(colors) != len(depths) or not colors:
                raise ValueError(f"Invalid character source: {clip_id}/{facing['id']}")
            counts.add(len(colors))
        if len(counts) != 1:
            raise ValueError(f"Directional frame counts differ for {clip_id}: {counts}")
        frame_count = counts.pop()
        if "durationSeconds" in definition:
            duration = float(definition["durationSeconds"])
        else:
            duration = frame_count / float(definition["framesPerSecond"])
        loop = bool(definition["loop"])
        if motion_analyzer is None:
            sample_count = min(
                frame_count,
                max(1, math.ceil(duration * target_fps - 1e-9)),
            )
            indices = selected_indices(frame_count, sample_count, loop)
            sampling_manifest = {
                "mode": "uniform",
                "targetFramesPerSecond": target_fps,
            }
        else:
            measurement = motion_analyzer.measure(clip_id, frame_count)
            max_pixels = float(sampling["maxPixelsPerSample"])
            minimum_fps = float(sampling["minimumFramesPerSecond"])
            indices = select_motion_indices(
                measurement.segment_pixels,
                duration,
                max_pixels,
                minimum_fps,
                loop,
            )
            measured_segments = (
                measurement.segment_pixels
                if loop
                else measurement.segment_pixels[:-1]
            )
            sampling_manifest = {
                "mode": "screen-space-motion",
                "maxPixelsPerSample": max_pixels,
                "minimumFramesPerSecond": minimum_fps,
                "measurementFramesPerSecond": measurement.source_frames_per_second,
                "sourceDurationSeconds": measurement.source_duration_seconds,
                "maximumSourceStepPixels": max(measured_segments, default=0.0),
                "sourceStepThresholdViolations": sum(
                    value > max_pixels for value in measured_segments
                ),
            }
        source_times = playback_sample_times(frame_count, duration)
        durations = frame_durations(indices, frame_count, duration)
        facings = {}
        for facing in plan["facings"]:
            facing_id = facing["id"]
            root_x = int(
                definition.get("rootXByFacing", {}).get(facing_id, plan["rootX"])
            )
            facings[facing_id] = {"root": [root_x, int(plan["groundY"])]}
        animations[clip_id] = {
            "loop": loop,
            "durationSeconds": duration,
            "sourceFrameCount": frame_count,
            "sampling": sampling_manifest,
            "samples": [
                {
                    "sourceFrame": source_index + 1,
                    "timeSeconds": source_times[source_index],
                    "durationSeconds": sample_duration,
                }
                for source_index, sample_duration in zip(indices, durations, strict=True)
            ],
            "facings": facings,
        }

    return {
        "format": "sparse-rooted-timeline-v1",
        "id": plan["id"],
        "canvas": list(canvas),
        "targetFramesPerSecond": target_fps,
        "sampling": sampling,
        "animations": animations,
    }


def build_package(
    repository: Path,
    plan: dict,
    timeline: dict,
    package_id: str,
    source_root: Path,
    layer_kind: str,
    output: Path,
) -> BuiltPackage:
    canvas = tuple(timeline["canvas"])
    crop_padding = int(plan["cropPadding"])
    atlas_size = int(plan["atlasSize"])
    atlas_gutter = int(plan["atlasGutter"])
    frames = []
    frames_by_key = {}
    selected_source_paths: set[Path] = set()
    full_source_paths: set[Path] = set()
    package_animations = {}

    for clip_id, animation in timeline["animations"].items():
        facing_manifests = {}
        for facing_id, facing in animation["facings"].items():
            colors, depths = frame_paths(source_root, layer_kind, clip_id, facing_id)
            if len(colors) != animation["sourceFrameCount"] or len(depths) != len(colors):
                raise ValueError(
                    f"Source frame mismatch for {package_id}/{clip_id}/{facing_id}: "
                    f"{len(colors)} color, {len(depths)} depth, "
                    f"expected {animation['sourceFrameCount']}"
                )
            full_source_paths.update(colors)
            full_source_paths.update(depths)
            sample_manifests = []
            for sample_index, sample in enumerate(animation["samples"]):
                source_index = int(sample["sourceFrame"]) - 1
                key = f"{clip_id}/{facing_id}/{sample_index:04d}"
                frame = create_sparse_frame(
                    key,
                    colors[source_index],
                    depths[source_index],
                    tuple(facing["root"]),
                    crop_padding,
                )
                frames.append(frame)
                frames_by_key[key] = frame
                selected_source_paths.update((colors[source_index], depths[source_index]))
                sample_manifests.append(
                    {
                        "sourceFrame": sample["sourceFrame"],
                        "frameKey": key,
                    }
                )
            facing_manifests[facing_id] = {"samples": sample_manifests}
        package_animations[clip_id] = {"facings": facing_manifests}

    page_count = pack_frames(frames, atlas_size, atlas_gutter)
    atlases = write_atlases(frames, page_count, atlas_size, output)
    pages = load_atlas_pages(output, atlases)

    for frame in frames:
        clip_id, facing_id, _ = frame.key.split("/")
        root = tuple(timeline["animations"][clip_id]["facings"][facing_id]["root"])
        reconstructed_color, reconstructed_depth = reconstruct_layer(
            frame, pages, canvas, root
        )
        with Image.open(frame.source_color) as source:
            expected_color = source.convert("RGBA")
        with Image.open(frame.source_depth) as source:
            expected_depth = decode_depth(source)
        if not image_equal(reconstructed_color, expected_color):
            raise ValueError(f"Color reconstruction mismatch: {package_id}/{frame.key}")
        if not np.array_equal(reconstructed_depth, expected_depth):
            raise ValueError(f"Depth reconstruction mismatch: {package_id}/{frame.key}")

    for animation in package_animations.values():
        for facing in animation["facings"].values():
            for sample in facing["samples"]:
                frame = frames_by_key[sample.pop("frameKey")]
                sample["frame"] = frame_manifest(frame)

    manifest = {
        "format": "sparse-rooted-layer-package-v1",
        "id": package_id,
        "kind": layer_kind,
        "timeline": "../../timeline.json" if layer_kind == "equipment" else "../timeline.json",
        "depthFormat": "r16-unorm",
        "cropPadding": crop_padding,
        "atlasGutter": atlas_gutter,
        "atlases": atlases,
        "animations": package_animations,
    }
    manifest_path = output / "package.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    package_paths = [manifest_path]
    for atlas in atlases:
        package_paths.extend((output / atlas["color"], output / atlas["depth"]))
    original_decoded = len(full_source_paths) // 2 * canvas[0] * canvas[1] * 8
    selected_decoded = len(frames) * canvas[0] * canvas[1] * 8
    tight_decoded = sum(frame.color.width * frame.color.height * 6 for frame in frames)
    atlas_decoded = sum(atlas["width"] * atlas["height"] * 6 for atlas in atlases)
    metrics = {
        "id": package_id,
        "kind": layer_kind,
        "status": "pass",
        "selectedFrames": len(frames),
        "sourceFrames": len(full_source_paths) // 2,
        "atlasPages": len(atlases),
        "packageFiles": len(package_paths),
        "fullSourceDiskBytes": sum(path.stat().st_size for path in full_source_paths),
        "selectedSourceDiskBytes": sum(path.stat().st_size for path in selected_source_paths),
        "packageDiskBytes": sum(path.stat().st_size for path in package_paths),
        "fullSourceDecodedBytes": original_decoded,
        "selectedSourceDecodedBytes": selected_decoded,
        "tightLayerDecodedBytes": tight_decoded,
        "atlasDecodedBytes": atlas_decoded,
        "pixelExactReconstructions": len(frames),
    }
    (output / "validation-report.json").write_text(
        json.dumps(metrics, indent=2), encoding="utf-8"
    )
    return BuiltPackage(package_id, output, frames_by_key, pages, metrics)


def compose_extracted_layers(
    layers: list[tuple[Image.Image, np.ndarray, tuple[int, int]]],
    padding: int,
) -> tuple[Image.Image, tuple[int, int]]:
    left = min(origin[0] for _, _, origin in layers)
    top = min(origin[1] for _, _, origin in layers)
    right = max(origin[0] + color.width for color, _, origin in layers)
    bottom = max(origin[1] + color.height for color, _, origin in layers)
    union_size = (right - left, bottom - top)
    positioned = []
    for color, depth, origin in layers:
        layer_color = Image.new("RGBA", union_size, (0, 0, 0, 0))
        layer_depth = np.zeros((union_size[1], union_size[0]), dtype=np.uint16)
        x = origin[0] - left
        y = origin[1] - top
        layer_color.paste(color, (x, y))
        layer_depth[y : y + color.height, x : x + color.width] = depth
        depth_rgb = np.zeros((*layer_depth.shape, 3), dtype=np.uint8)
        depth_rgb[:, :, 0] = layer_depth >> 8
        depth_rgb[:, :, 1] = layer_depth & 255
        positioned.append((layer_color, Image.fromarray(depth_rgb, "RGB")))
    composite = composite_depth_layers(positioned)
    crop = alpha_bounds(composite, padding)
    return composite.crop(crop), (left + crop[0], top + crop[1])


def validate_representative_composites(
    timeline: dict,
    character: BuiltPackage,
    equipment: BuiltPackage,
    padding: int,
) -> int:
    canvas = tuple(timeline["canvas"])
    validated = 0
    for clip_id, animation in timeline["animations"].items():
        sample_index = len(animation["samples"]) // 2
        for facing_id, facing in animation["facings"].items():
            key = f"{clip_id}/{facing_id}/{sample_index:04d}"
            character_frame = character.frames[key]
            equipment_frame = equipment.frames[key]
            character_color, character_depth = extract_frame(character_frame, character.pages)
            equipment_color, equipment_depth = extract_frame(equipment_frame, equipment.pages)
            composite, origin = compose_extracted_layers(
                [
                    (character_color, character_depth, character_frame.origin),
                    (equipment_color, equipment_depth, equipment_frame.origin),
                ],
                padding,
            )
            root = tuple(facing["root"])
            reconstructed = Image.new("RGBA", canvas, (0, 0, 0, 0))
            reconstructed.paste(composite, (root[0] + origin[0], root[1] + origin[1]))
            with Image.open(character_frame.source_color) as source:
                source_character_color = source.convert("RGBA")
            with Image.open(character_frame.source_depth) as source:
                source_character_depth = source.convert("RGB")
            with Image.open(equipment_frame.source_color) as source:
                source_equipment_color = source.convert("RGBA")
            with Image.open(equipment_frame.source_depth) as source:
                source_equipment_depth = source.convert("RGB")
            expected = composite_depth_layers(
                [
                    (source_character_color, source_character_depth),
                    (source_equipment_color, source_equipment_depth),
                ]
            )
            if not image_equal(reconstructed, expected):
                raise ValueError(
                    f"Composite mismatch: {equipment.package_id}/{clip_id}/{facing_id}"
                )
            validated += 1
    return validated


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    output = (repository / plan["outputRoot"]).resolve()
    allowed_root = (repository / "Assets" / "Content" / "sparse").resolve()
    if output != allowed_root and allowed_root not in output.parents:
        raise ValueError(f"Output must be at or below {allowed_root}: {output}")
    building = output.with_name(output.name + ".building")
    if output.exists() and not args.replace:
        raise FileExistsError(f"Output exists; pass --replace to rebuild: {output}")
    if building.exists():
        shutil.rmtree(building)
    building.mkdir(parents=True)

    timeline = build_timeline(repository, plan)
    (building / "timeline.json").write_text(
        json.dumps(timeline, indent=2), encoding="utf-8"
    )
    character = build_package(
        repository,
        plan,
        timeline,
        plan["character"]["id"],
        repository / plan["character"]["contentRoot"],
        "character",
        building / "character",
    )
    print(
        f"built {character.package_id}: {character.metrics['selectedFrames']} frames, "
        f"{character.metrics['atlasPages']} pages",
        flush=True,
    )

    equipment_results = []
    representative_composites = 0
    for entry in plan["equipment"]:
        result = build_package(
            repository,
            plan,
            timeline,
            entry["id"],
            repository / entry["contentRoot"],
            "equipment",
            building / "equipment" / entry["id"],
        )
        representative_composites += validate_representative_composites(
            timeline,
            character,
            result,
            int(plan["cropPadding"]),
        )
        equipment_results.append(result)
        print(
            f"built {result.package_id}: {result.metrics['selectedFrames']} frames, "
            f"{result.metrics['atlasPages']} pages",
            flush=True,
        )

    packages = [character, *equipment_results]
    totals = {
        key: sum(package.metrics[key] for package in packages)
        for key in (
            "selectedFrames",
            "sourceFrames",
            "atlasPages",
            "packageFiles",
            "fullSourceDiskBytes",
            "selectedSourceDiskBytes",
            "packageDiskBytes",
            "fullSourceDecodedBytes",
            "selectedSourceDecodedBytes",
            "tightLayerDecodedBytes",
            "atlasDecodedBytes",
            "pixelExactReconstructions",
        )
    }
    report = {
        "status": "pass",
        "id": plan["id"],
        "timeline": {
            "animationCount": len(timeline["animations"]),
            "facingCount": len(plan["facings"]),
            "targetFramesPerSecond": plan["targetFramesPerSecond"],
        },
        "packages": [package.metrics for package in packages],
        "totals": totals,
        "validation": {
            "pixelExactLayerReconstructions": totals["pixelExactReconstructions"],
            "pixelExactRepresentativeComposites": representative_composites,
        },
        "ratios": {
            "packageDiskVsFullSource": totals["packageDiskBytes"]
            / totals["fullSourceDiskBytes"],
            "atlasDecodedVsFullSource": totals["atlasDecodedBytes"]
            / totals["fullSourceDecodedBytes"],
            "atlasDecodedVsSelectedFullCanvas": totals["atlasDecodedBytes"]
            / totals["selectedSourceDecodedBytes"],
        },
    }
    (building / "build-report.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    library = {
        "format": "sparse-rooted-layer-library-v1",
        "id": plan["id"],
        "timeline": "timeline.json",
        "character": "character/package.json",
        "equipment": {
            result.package_id: f"equipment/{result.package_id}/package.json"
            for result in equipment_results
        },
        "buildReport": "build-report.json",
    }
    (building / "library.json").write_text(
        json.dumps(library, indent=2), encoding="utf-8"
    )

    if output.exists():
        shutil.rmtree(output)
    building.rename(output)
    print(json.dumps(report["totals"], indent=2), flush=True)
    print(json.dumps(report["ratios"], indent=2), flush=True)


if __name__ == "__main__":
    main()
