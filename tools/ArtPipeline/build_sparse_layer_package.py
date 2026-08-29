#!/usr/bin/env python3
"""Build and validate a root-anchored sparse animation-layer package."""

from __future__ import annotations

import argparse
import json
import math
import shutil
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image

from render_attached_weapon_layers import decode_depth
from render_loadout_equipment_layers import composite_depth_layers
from screen_space_motion import (
    ScreenSpaceMotionAnalyzer,
    frame_durations as adaptive_frame_durations,
    select_motion_indices,
)


@dataclass
class SparseFrame:
    key: str
    color: Image.Image
    depth: np.ndarray
    origin: tuple[int, int]
    source_color: Path
    source_depth: Path
    page: int = -1
    atlas_rect: tuple[int, int, int, int] = (0, 0, 0, 0)


def alpha_bounds(image: Image.Image, padding: int) -> tuple[int, int, int, int]:
    alpha = np.asarray(image.convert("RGBA"), dtype=np.uint8)[:, :, 3]
    occupied_y, occupied_x = np.nonzero(alpha)
    if len(occupied_x) == 0:
        return 0, 0, 1, 1
    width, height = image.size
    return (
        max(0, int(occupied_x.min()) - padding),
        max(0, int(occupied_y.min()) - padding),
        min(width, int(occupied_x.max()) + padding + 1),
        min(height, int(occupied_y.max()) + padding + 1),
    )


def select_source_indices(
    frame_count: int,
    source_fps: float,
    target_fps: float,
) -> list[int]:
    duration = frame_count / source_fps
    sample_count = max(1, math.ceil(duration * target_fps - 1e-9))
    indices: list[int] = []
    for sample_index in range(sample_count):
        sample_time = sample_index / target_fps
        source_index = min(frame_count - 1, int(math.floor(sample_time * source_fps + 0.5)))
        if not indices or indices[-1] != source_index:
            indices.append(source_index)
    return indices


def frame_durations(
    indices: list[int],
    frame_count: int,
    source_fps: float,
) -> list[float]:
    duration = frame_count / source_fps
    times = [index / source_fps for index in indices]
    return [
        (times[index + 1] if index + 1 < len(times) else duration) - time
        for index, time in enumerate(times)
    ]


def create_sparse_frame(
    key: str,
    color_path: Path,
    depth_path: Path,
    root: tuple[int, int],
    padding: int,
) -> SparseFrame:
    with Image.open(color_path) as source:
        color = source.convert("RGBA")
    with Image.open(depth_path) as source:
        depth = decode_depth(source)
    if depth.shape != (color.height, color.width):
        raise ValueError(f"Mismatched color/depth dimensions: {color_path}")

    left, top, right, bottom = alpha_bounds(color, padding)
    return SparseFrame(
        key=key,
        color=color.crop((left, top, right, bottom)),
        depth=depth[top:bottom, left:right].copy(),
        origin=(left - root[0], top - root[1]),
        source_color=color_path,
        source_depth=depth_path,
    )


def try_place(
    state: tuple[int, int, int],
    size: tuple[int, int],
    page_size: int,
    gutter: int,
) -> tuple[tuple[int, int], tuple[int, int, int]] | None:
    x, y, row_height = state
    width, height = size
    if width + gutter * 2 > page_size or height + gutter * 2 > page_size:
        raise ValueError(f"Sparse frame {size} does not fit a {page_size}px atlas")
    if x + width + gutter > page_size:
        x = gutter
        y += row_height + gutter
        row_height = 0
    if y + height + gutter > page_size:
        return None
    return (x, y), (x + width + gutter, y, max(row_height, height))


def pack_frames(
    frames: list[SparseFrame],
    page_size: int,
    gutter: int,
) -> int:
    page_states: list[tuple[int, int, int]] = []
    for frame in sorted(frames, key=lambda item: (-item.color.height, -item.color.width, item.key)):
        placement = None
        page_index = -1
        for candidate, state in enumerate(page_states):
            placement = try_place(state, frame.color.size, page_size, gutter)
            if placement is not None:
                page_index = candidate
                break
        if placement is None:
            page_states.append((gutter, gutter, 0))
            page_index = len(page_states) - 1
            placement = try_place(page_states[page_index], frame.color.size, page_size, gutter)
            if placement is None:
                raise AssertionError("A new atlas page unexpectedly rejected a frame")
        (x, y), page_states[page_index] = placement
        frame.page = page_index
        frame.atlas_rect = (x, y, frame.color.width, frame.color.height)
    return len(page_states)


def write_atlases(
    frames: list[SparseFrame],
    page_count: int,
    page_size: int,
    output: Path,
) -> list[dict]:
    atlas_root = output / "atlases"
    atlas_root.mkdir(parents=True, exist_ok=True)
    manifests = []
    for page_index in range(page_count):
        page_frames = [frame for frame in frames if frame.page == page_index]
        used_width = max(frame.atlas_rect[0] + frame.atlas_rect[2] for frame in page_frames)
        used_height = max(frame.atlas_rect[1] + frame.atlas_rect[3] for frame in page_frames)
        atlas_width = min(page_size, math.ceil((used_width + 2) / 4) * 4)
        atlas_height = min(page_size, math.ceil((used_height + 2) / 4) * 4)
        color = Image.new("RGBA", (atlas_width, atlas_height), (0, 0, 0, 0))
        depth = np.zeros((atlas_height, atlas_width), dtype=np.uint16)
        for frame in page_frames:
            x, y, width, height = frame.atlas_rect
            color.paste(frame.color, (x, y))
            depth[y : y + height, x : x + width] = frame.depth
        color_name = f"color-{page_index:02d}.png"
        depth_name = f"depth-{page_index:02d}.png"
        color.save(atlas_root / color_name, optimize=True)
        Image.fromarray(depth).save(atlas_root / depth_name, optimize=True)
        manifests.append(
            {
                "color": f"atlases/{color_name}",
                "depth": f"atlases/{depth_name}",
                "width": atlas_width,
                "height": atlas_height,
                "depthFormat": "r16-unorm",
            }
        )
    return manifests


def load_atlas_pages(output: Path, atlases: list[dict]) -> list[tuple[Image.Image, np.ndarray]]:
    pages = []
    for atlas in atlases:
        with Image.open(output / atlas["color"]) as source:
            color = source.convert("RGBA")
        with Image.open(output / atlas["depth"]) as source:
            depth = np.asarray(source, dtype=np.uint16).copy()
        pages.append((color, depth))
    return pages


def extract_frame(
    frame: SparseFrame,
    pages: list[tuple[Image.Image, np.ndarray]],
) -> tuple[Image.Image, np.ndarray]:
    x, y, width, height = frame.atlas_rect
    color_page, depth_page = pages[frame.page]
    return (
        color_page.crop((x, y, x + width, y + height)),
        depth_page[y : y + height, x : x + width].copy(),
    )


def encode_depth_rgb(depth: np.ndarray) -> Image.Image:
    pixels = np.zeros((*depth.shape, 3), dtype=np.uint8)
    pixels[:, :, 0] = depth >> 8
    pixels[:, :, 1] = depth & 255
    return Image.fromarray(pixels, "RGB")


def reconstruct_layer(
    frame: SparseFrame,
    pages: list[tuple[Image.Image, np.ndarray]],
    canvas: tuple[int, int],
    root: tuple[int, int],
) -> tuple[Image.Image, np.ndarray]:
    color, depth = extract_frame(frame, pages)
    left = root[0] + frame.origin[0]
    top = root[1] + frame.origin[1]
    reconstructed_color = Image.new("RGBA", canvas, (0, 0, 0, 0))
    reconstructed_color.paste(color, (left, top))
    reconstructed_depth = np.zeros((canvas[1], canvas[0]), dtype=np.uint16)
    reconstructed_depth[top : top + color.height, left : left + color.width] = depth
    return reconstructed_color, reconstructed_depth


def compose_sparse_layers(
    layers: list[SparseFrame],
    pages: list[tuple[Image.Image, np.ndarray]],
    padding: int,
) -> tuple[Image.Image, tuple[int, int]]:
    left = min(frame.origin[0] for frame in layers)
    top = min(frame.origin[1] for frame in layers)
    right = max(frame.origin[0] + frame.color.width for frame in layers)
    bottom = max(frame.origin[1] + frame.color.height for frame in layers)
    union_size = (right - left, bottom - top)
    positioned = []
    for frame in layers:
        color, depth = extract_frame(frame, pages)
        layer_color = Image.new("RGBA", union_size, (0, 0, 0, 0))
        layer_depth = np.zeros((union_size[1], union_size[0]), dtype=np.uint16)
        x = frame.origin[0] - left
        y = frame.origin[1] - top
        layer_color.paste(color, (x, y))
        layer_depth[y : y + color.height, x : x + color.width] = depth
        positioned.append((layer_color, encode_depth_rgb(layer_depth)))

    composite = composite_depth_layers(positioned)
    crop = alpha_bounds(composite, padding)
    return composite.crop(crop), (left + crop[0], top + crop[1])


def image_equal(first: Image.Image, second: Image.Image) -> bool:
    return first.mode == second.mode and first.size == second.size and np.array_equal(
        np.asarray(first), np.asarray(second)
    )


def frame_manifest(frame: SparseFrame) -> dict:
    return {
        "page": frame.page,
        "rect": list(frame.atlas_rect),
        "origin": list(frame.origin),
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument(
        "--timeline",
        type=Path,
        help="Materialize an already planned timeline without rerunning motion analysis.",
    )
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    output = (repository / plan["outputRoot"]).resolve()
    allowed_roots = (
        (repository / "Assets" / "Work" / "art-pipeline").resolve(),
        (repository / "Assets" / "Content" / "sparse-loadouts").resolve(),
    )
    if not any(root in output.parents for root in allowed_roots):
        raise ValueError(f"Output must be below one of {allowed_roots}: {output}")
    if output.exists():
        if not args.replace:
            raise FileExistsError(f"Output exists; pass --replace to rebuild: {output}")
        shutil.rmtree(output)
    output.mkdir(parents=True)

    canvas = tuple(int(value) for value in plan["canvas"])
    default_root = (int(plan["rootX"]), int(plan["groundY"]))
    source_fps = float(plan["sourceFps"])
    target_fps = float(plan["targetFps"])
    if not math.isfinite(source_fps) or source_fps <= 0:
        raise ValueError("sourceFps must be finite and positive")
    if not math.isfinite(target_fps) or target_fps <= 0:
        raise ValueError("targetFps must be finite and positive")
    crop_padding = int(plan.get("cropPadding", 2))
    atlas_size = int(plan.get("atlasSize", 1024))
    atlas_gutter = int(plan.get("atlasGutter", 2))
    write_proof_artifacts = bool(plan.get("writeProofArtifacts", True))
    character_root = repository / plan["characterContentRoot"]
    equipment_root = repository / plan["equipment"]["contentRoot"]
    sampling = plan.get("sampling", {"mode": "uniform"})
    preselected_timeline = None
    timeline_argument = (
        args.timeline.resolve()
        if args.timeline is not None
        else (repository / plan["timeline"]).resolve()
        if "timeline" in plan
        else None
    )
    if timeline_argument is not None:
        preselected_timeline = json.loads(
            timeline_argument.read_text(encoding="utf-8")
        )
        if preselected_timeline.get("format") != "sparse-rooted-timeline-v1":
            raise ValueError(
                f"Unsupported timeline format: {preselected_timeline.get('format')}"
            )
        if preselected_timeline.get("canvas") != list(canvas):
            raise ValueError("Preselected timeline canvas does not match the package plan")
        try:
            timeline_reference = str(timeline_argument.relative_to(repository))
        except ValueError:
            timeline_reference = timeline_argument.name
        sampling = {
            **preselected_timeline.get("sampling", {}),
            "mode": "preselected-timeline",
            "timeline": timeline_reference.replace("\\", "/"),
        }
    sampling_mode = sampling.get("mode", "uniform")
    motion_analyzer = None
    if sampling_mode == "screen-space-motion":
        motion_analyzer = ScreenSpaceMotionAnalyzer(
            repository,
            repository / sampling["analysisPlan"],
        )
    elif sampling_mode not in ("uniform", "preselected-timeline"):
        raise ValueError(f"Unknown sampling mode: {sampling_mode}")

    sparse_frames: list[SparseFrame] = []
    frame_lookup: dict[str, SparseFrame] = {}
    animations: dict[str, dict] = {}
    source_paths: set[Path] = set()
    all_source_paths: set[Path] = set()
    all_source_layer_frame_count = 0

    for clip in plan["clips"]:
        clip_id = clip["id"]
        planned_animation = (
            preselected_timeline["animations"].get(clip_id)
            if preselected_timeline is not None
            else None
        )
        if preselected_timeline is not None and planned_animation is None:
            raise ValueError(f"Preselected timeline does not define {clip_id}")
        clip_target_fps = float(
            clip.get("targetFramesPerSecond", clip.get("targetFps", target_fps))
        )
        if not math.isfinite(clip_target_fps) or clip_target_fps <= 0:
            raise ValueError(f"Invalid target frame rate: {clip_id}")
        loop = (
            bool(planned_animation["loop"])
            if planned_animation is not None
            else bool(clip.get("loop", True))
        )
        clip_manifest = {
            "loop": loop,
            "targetFramesPerSecond": clip_target_fps,
            "facings": {},
        }
        clip_indices: list[int] | None = None
        clip_durations: list[float] | None = None
        clip_times: list[float] | None = None
        clip_frame_count: int | None = None
        clip_duration: float | None = None
        for facing in plan["facings"]:
            facing_id = facing["id"]
            if planned_animation is not None:
                planned_root = planned_animation["facings"][facing_id]["root"]
                root = (int(planned_root[0]), int(planned_root[1]))
            else:
                root_x = int(
                    clip.get("rootXByFacing", {}).get(facing_id, default_root[0])
                )
                root = (root_x, default_root[1])
            character_color_paths = sorted(
                (character_root / clip_id / "color" / facing_id).glob("frame-*.png")
            )
            character_depth_paths = sorted(
                (character_root / clip_id / "depth" / facing_id).glob("frame-*.png")
            )
            equipment_clip_root = equipment_root / clip_id / facing_id
            equipment_color_paths = sorted((equipment_clip_root / "color").glob("frame-*.png"))
            equipment_depth_paths = sorted((equipment_clip_root / "depth").glob("frame-*.png"))
            counts = {
                len(character_color_paths),
                len(character_depth_paths),
                len(equipment_color_paths),
                len(equipment_depth_paths),
            }
            if len(counts) != 1 or not character_color_paths:
                raise ValueError(f"Layer frame mismatch: {clip_id}/{facing_id}")

            frame_count = len(character_color_paths)
            if clip_frame_count is not None and clip_frame_count != frame_count:
                raise ValueError(
                    f"Directional source frame mismatch: {clip_id} has "
                    f"{clip_frame_count} and {frame_count} frames"
                )
            all_source_paths.update(character_color_paths)
            all_source_paths.update(character_depth_paths)
            all_source_paths.update(equipment_color_paths)
            all_source_paths.update(equipment_depth_paths)
            all_source_layer_frame_count += frame_count * 2
            facing_duration = (
                float(planned_animation["durationSeconds"])
                if planned_animation is not None
                else float(clip.get("durationSeconds", frame_count / source_fps))
            )
            if not math.isfinite(facing_duration) or facing_duration <= 0:
                raise ValueError(f"Invalid clip duration: {clip_id}/{facing_id}")
            if clip_indices is None:
                clip_frame_count = frame_count
                clip_duration = facing_duration
                playback_fps = frame_count / facing_duration
                if planned_animation is not None:
                    if int(planned_animation["sourceFrameCount"]) != frame_count:
                        raise ValueError(
                            f"Preselected timeline/source mismatch: {clip_id}"
                        )
                    planned_samples = planned_animation["samples"]
                    clip_indices = [
                        int(sample["sourceFrame"]) - 1 for sample in planned_samples
                    ]
                    clip_times = [
                        float(sample["timeSeconds"]) for sample in planned_samples
                    ]
                    clip_durations = [
                        float(sample["durationSeconds"]) for sample in planned_samples
                    ]
                    clip_manifest["sampling"] = planned_animation.get(
                        "sampling",
                        preselected_timeline.get("sampling", {}),
                    )
                elif motion_analyzer is None:
                    clip_indices = select_source_indices(
                        frame_count,
                        playback_fps,
                        clip_target_fps,
                    )
                    clip_durations = frame_durations(
                        clip_indices,
                        frame_count,
                        playback_fps,
                    )
                    clip_times = [index / playback_fps for index in clip_indices]
                    clip_manifest["sampling"] = {
                        "mode": "uniform",
                        "targetFramesPerSecond": clip_target_fps,
                    }
                else:
                    measurement = motion_analyzer.measure(clip_id, frame_count)
                    max_pixels = float(sampling["maxPixelsPerSample"])
                    minimum_fps = float(sampling["minimumFramesPerSecond"])
                    clip_indices = select_motion_indices(
                        measurement.segment_pixels,
                        facing_duration,
                        max_pixels,
                        minimum_fps,
                        loop,
                    )
                    measured_segments = (
                        measurement.segment_pixels
                        if loop
                        else measurement.segment_pixels[:-1]
                    )
                    clip_durations = adaptive_frame_durations(
                        clip_indices,
                        frame_count,
                        facing_duration,
                    )
                    clip_times = [index / playback_fps for index in clip_indices]
                    clip_manifest["sampling"] = {
                        "mode": "screen-space-motion",
                        "maxPixelsPerSample": max_pixels,
                        "minimumFramesPerSecond": minimum_fps,
                        "measurementFramesPerSecond": (
                            measurement.source_frames_per_second
                        ),
                        "sourceDurationSeconds": measurement.source_duration_seconds,
                        "maximumSourceStepPixels": max(
                            measured_segments,
                            default=0.0,
                        ),
                        "sourceStepThresholdViolations": sum(
                            value > max_pixels for value in measured_segments
                        ),
                    }
                clip_manifest["durationSeconds"] = facing_duration
            elif abs(facing_duration - clip_duration) > 1e-9:
                raise ValueError(f"Directional clip duration mismatch: {clip_id}")

            indices = clip_indices
            durations = clip_durations
            times = clip_times
            playback_fps = frame_count / facing_duration
            samples = []
            for output_index, (source_index, time_seconds, duration) in enumerate(
                zip(indices, times, durations, strict=True)
            ):
                layers = {}
                paths_by_layer = {
                    "character": (
                        character_color_paths[source_index],
                        character_depth_paths[source_index],
                    ),
                    "equipment": (
                        equipment_color_paths[source_index],
                        equipment_depth_paths[source_index],
                    ),
                }
                for layer_id, (color_path, depth_path) in paths_by_layer.items():
                    key = f"{clip_id}/{facing_id}/{output_index:04d}/{layer_id}"
                    frame = create_sparse_frame(
                        key,
                        color_path,
                        depth_path,
                        root,
                        crop_padding,
                    )
                    sparse_frames.append(frame)
                    frame_lookup[key] = frame
                    source_paths.update((color_path, depth_path))
                    layers[layer_id] = key
                samples.append(
                    {
                        "sourceFrame": source_index + 1,
                        "timeSeconds": time_seconds,
                        "durationSeconds": duration,
                        "layers": layers,
                    }
                )
            clip_manifest["facings"][facing_id] = {
                "root": list(root),
                "sourceFrameCount": frame_count,
                "samples": samples,
            }
        animations[clip_id] = clip_manifest

    page_count = pack_frames(sparse_frames, atlas_size, atlas_gutter)
    atlases = write_atlases(sparse_frames, page_count, atlas_size, output)
    pages = load_atlas_pages(output, atlases)

    validation_errors = []
    composite_root = output / "proof-composites"
    preview_root = output / "previews"
    if write_proof_artifacts:
        preview_root.mkdir(parents=True)
    composed_area = 0
    for clip_id, clip_manifest in animations.items():
        for facing_id, facing_manifest in clip_manifest["facings"].items():
            root = tuple(facing_manifest["root"])
            preview_frames = []
            preview_durations = []
            for output_index, sample in enumerate(facing_manifest["samples"]):
                layer_frames = [frame_lookup[key] for key in sample["layers"].values()]
                for frame in layer_frames:
                    reconstructed_color, reconstructed_depth = reconstruct_layer(
                        frame, pages, canvas, root
                    )
                    with Image.open(frame.source_color) as source:
                        source_color = source.convert("RGBA")
                    with Image.open(frame.source_depth) as source:
                        source_depth = decode_depth(source)
                    if not image_equal(reconstructed_color, source_color):
                        validation_errors.append(f"Color mismatch: {frame.key}")
                    if not np.array_equal(reconstructed_depth, source_depth):
                        validation_errors.append(f"Depth mismatch: {frame.key}")

                composite, composite_origin = compose_sparse_layers(
                    layer_frames, pages, crop_padding
                )
                composed_area += composite.width * composite.height
                sample["composite"] = {
                    "origin": list(composite_origin),
                    "width": composite.width,
                    "height": composite.height,
                }
                if write_proof_artifacts:
                    composite_directory = composite_root / clip_id / facing_id
                    composite_directory.mkdir(parents=True, exist_ok=True)
                    composite_path = composite_directory / f"frame-{output_index + 1:04d}.png"
                    composite.save(composite_path, optimize=True)
                    sample["composite"]["path"] = str(
                        composite_path.relative_to(output)
                    ).replace("\\", "/")

                source_layers = []
                for frame in layer_frames:
                    with Image.open(frame.source_color) as color_source:
                        color = color_source.convert("RGBA")
                    with Image.open(frame.source_depth) as depth_source:
                        depth = depth_source.convert("RGB")
                    source_layers.append((color, depth))
                expected = composite_depth_layers(source_layers)
                reconstructed = Image.new("RGBA", canvas, (0, 0, 0, 0))
                reconstructed.paste(
                    composite,
                    (root[0] + composite_origin[0], root[1] + composite_origin[1]),
                )
                if not image_equal(reconstructed, expected):
                    validation_errors.append(
                        f"Composite mismatch: {clip_id}/{facing_id}/{output_index:04d}"
                    )

                if write_proof_artifacts:
                    preview = Image.new("RGBA", canvas, (32, 34, 40, 255))
                    preview.alpha_composite(reconstructed)
                    preview_frames.append(preview.convert("RGB"))
                    preview_durations.append(max(1, round(sample["durationSeconds"] * 1000)))

            if write_proof_artifacts:
                preview_frames[0].save(
                    preview_root / f"{clip_id}-{facing_id}.gif",
                    save_all=True,
                    append_images=preview_frames[1:],
                    duration=preview_durations,
                    loop=0,
                    disposal=2,
                )

    if validation_errors:
        raise ValueError("Sparse package validation failed:\n" + "\n".join(validation_errors))

    for clip_manifest in animations.values():
        for facing_manifest in clip_manifest["facings"].values():
            for sample in facing_manifest["samples"]:
                sample["layers"] = {
                    layer_id: frame_manifest(frame_lookup[key])
                    for layer_id, key in sample["layers"].items()
                }

    manifest = {
        "format": "sparse-rooted-layers-v1",
        "id": plan["id"],
        "equipment": plan["equipment"]["id"],
        "canvas": list(canvas),
        "sourceFramesPerSecond": source_fps,
        "targetFramesPerSecond": target_fps,
        "sampling": sampling,
        "cropPadding": crop_padding,
        "atlasGutter": atlas_gutter,
        "atlases": atlases,
        "animations": animations,
    }
    manifest_path = output / "package.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")

    original_disk_bytes = sum(path.stat().st_size for path in source_paths)
    full_source_disk_bytes = sum(path.stat().st_size for path in all_source_paths)
    package_paths = [manifest_path]
    for atlas in atlases:
        package_paths.extend((output / atlas["color"], output / atlas["depth"]))
    package_disk_bytes = sum(path.stat().st_size for path in package_paths)
    original_decoded_bytes = len(sparse_frames) * canvas[0] * canvas[1] * 8
    full_source_decoded_bytes = all_source_layer_frame_count * canvas[0] * canvas[1] * 8
    tight_decoded_bytes = sum(frame.color.width * frame.color.height * 6 for frame in sparse_frames)
    atlas_decoded_bytes = sum(atlas["width"] * atlas["height"] * 6 for atlas in atlases)
    report = {
        "status": "pass",
        "validation": {
            "layerReconstructions": len(sparse_frames),
            "composites": sum(
                len(facing["samples"])
                for clip in animations.values()
                for facing in clip["facings"].values()
            ),
            "pixelExact": True,
        },
        "counts": {
            "sourceFilesUsed": len(source_paths),
            "fullSourceFiles": len(all_source_paths),
            "fullSourceLayerFrames": all_source_layer_frame_count,
            "sparseLayerFrames": len(sparse_frames),
            "atlasPages": page_count,
            "packageFiles": len(package_paths),
        },
        "bytes": {
            "selectedSourceDisk": original_disk_bytes,
            "fullSourceDisk": full_source_disk_bytes,
            "packageDisk": package_disk_bytes,
            "originalDecodedEstimate": original_decoded_bytes,
            "fullSourceDecodedEstimate": full_source_decoded_bytes,
            "tightLayerDecoded": tight_decoded_bytes,
            "atlasDecoded": atlas_decoded_bytes,
            "compositeCacheDecoded": composed_area * 4,
        },
        "ratios": {
            "packageDiskVsSelectedSource": package_disk_bytes / original_disk_bytes,
            "packageDiskVsFullSource": package_disk_bytes / full_source_disk_bytes,
            "tightLayerDecodedVsOriginal": tight_decoded_bytes / original_decoded_bytes,
            "atlasDecodedVsOriginal": atlas_decoded_bytes / original_decoded_bytes,
            "atlasDecodedVsFullSource": atlas_decoded_bytes / full_source_decoded_bytes,
        },
    }
    (output / "validation-report.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    main()
