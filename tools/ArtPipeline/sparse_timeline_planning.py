#!/usr/bin/env python3
"""Reusable numerical analysis and policy planning for sparse timelines."""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path

from PIL import Image

from screen_space_motion import (
    ScreenSpaceMotionAnalyzer,
    frame_durations,
    playback_sample_times,
    select_motion_indices,
)


ANALYSIS_FORMAT = "screen-space-motion-analysis-v1"
TIMELINE_FORMAT = "sparse-rooted-timeline-v1"
TIMELINE_SET_FORMAT = "sparse-independent-layer-timeline-set-v1"


def _frame_paths(
    source_root: Path,
    layer_kind: str,
    clip_id: str,
    facing_id: str,
) -> tuple[list[Path], list[Path]]:
    if layer_kind == "character":
        color_root = source_root / clip_id / "color" / facing_id
        depth_root = source_root / clip_id / "depth" / facing_id
    elif layer_kind == "equipment":
        color_root = source_root / clip_id / facing_id / "color"
        depth_root = source_root / clip_id / facing_id / "depth"
    else:
        raise ValueError(f"Unknown layer kind: {layer_kind}")
    return sorted(color_root.glob("frame-*.png")), sorted(depth_root.glob("frame-*.png"))


def _source_inventory(repository: Path, plan: dict) -> dict[str, dict]:
    character_root = repository / plan["character"]["contentRoot"]
    equipment_roots = {
        entry["id"]: repository / entry["contentRoot"]
        for entry in plan["equipment"]
    }
    inventory: dict[str, dict] = {}
    for clip_id in plan["clips"]:
        character: dict[str, tuple[list[Path], list[Path]]] = {}
        equipment: dict[str, dict[str, tuple[list[Path], list[Path]]]] = {
            equipment_id: {} for equipment_id in equipment_roots
        }
        counts = set()
        for facing in plan["facings"]:
            facing_id = facing["id"]
            paths = _frame_paths(character_root, "character", clip_id, facing_id)
            if not paths[0] or len(paths[0]) != len(paths[1]):
                raise ValueError(f"Invalid character source: {clip_id}/{facing_id}")
            character[facing_id] = paths
            counts.add(len(paths[0]))
            for equipment_id, equipment_root in equipment_roots.items():
                equipment_paths = _frame_paths(
                    equipment_root,
                    "equipment",
                    clip_id,
                    facing_id,
                )
                if len(equipment_paths[0]) != len(paths[0]) or len(equipment_paths[1]) != len(paths[0]):
                    raise ValueError(
                        f"Layer frame mismatch: {equipment_id}/{clip_id}/{facing_id}"
                    )
                equipment[equipment_id][facing_id] = equipment_paths
        if len(counts) != 1:
            raise ValueError(f"Directional frame counts differ for {clip_id}: {counts}")
        inventory[clip_id] = {
            "frameCount": counts.pop(),
            "character": character,
            "equipment": equipment,
        }
    return inventory


def analysis_input_fingerprint(repository: Path, plan: dict) -> str:
    inventory = _source_inventory(repository, plan)
    digest = hashlib.sha256()
    for path in (
        repository / plan["character"]["manifest"],
        repository / plan["planning"]["analysisPlan"],
    ):
        digest.update(path.read_bytes())
    analysis_plan = json.loads(
        (repository / plan["planning"]["analysisPlan"]).read_text(encoding="utf-8")
    )
    for source in [
        *(repository / clip["glb"] for clip in analysis_plan["clips"]),
        *(repository / equipment["gltf"] for equipment in analysis_plan.get("equipment", [])),
    ]:
        stat = source.stat()
        digest.update(str(source.relative_to(repository)).encode("utf-8"))
        digest.update(f"{stat.st_size}:{stat.st_mtime_ns}".encode("ascii"))
    relevant_plan = {
        key: plan[key]
        for key in (
            "id",
            "canvas",
            "rootX",
            "groundY",
            "cropPadding",
            "character",
            "facings",
            "clips",
            "equipment",
        )
    }
    digest.update(json.dumps(relevant_plan, sort_keys=True).encode("utf-8"))
    for clip in inventory.values():
        path_groups = list(clip["character"].values())
        path_groups.extend(
            paths
            for equipment in clip["equipment"].values()
            for paths in equipment.values()
        )
        for colors, depths in path_groups:
            for path in (*colors, *depths):
                stat = path.stat()
                digest.update(str(path.relative_to(repository)).encode("utf-8"))
                digest.update(f"{stat.st_size}:{stat.st_mtime_ns}".encode("ascii"))
    return digest.hexdigest()


def _alpha_rect(path: Path) -> tuple[int, int, int, int]:
    with Image.open(path) as source:
        rgba = source.convert("RGBA")
        bounds = rgba.getchannel("A").getbbox()
        if bounds is None:
            return 0, 0, 1, 1
        return tuple(int(value) for value in bounds)


def _expand_rect(
    rect: tuple[int, int, int, int],
    padding: int,
    canvas: tuple[int, int],
) -> tuple[int, int, int, int]:
    return (
        max(0, rect[0] - padding),
        max(0, rect[1] - padding),
        min(canvas[0], rect[2] + padding),
        min(canvas[1], rect[3] + padding),
    )


def _union_rect(
    first: tuple[int, int, int, int],
    second: tuple[int, int, int, int],
) -> tuple[int, int, int, int]:
    return (
        min(first[0], second[0]),
        min(first[1], second[1]),
        max(first[2], second[2]),
        max(first[3], second[3]),
    )


def _area(rect: tuple[int, int, int, int]) -> int:
    return max(1, rect[2] - rect[0]) * max(1, rect[3] - rect[1])


def _estimate_costs(
    clip_inventory: dict,
    facings: list[dict],
    equipment_ids: list[str],
    frame_count: int,
    padding: int,
    canvas: tuple[int, int],
) -> dict:
    facing_ids = [facing["id"] for facing in facings]
    character_rects = {
        facing_id: [
            _alpha_rect(path)
            for path in clip_inventory["character"][facing_id][0]
        ]
        for facing_id in facing_ids
    }
    equipment_rects = {
        equipment_id: {
            facing_id: [
                _alpha_rect(path)
                for path in clip_inventory["equipment"][equipment_id][facing_id][0]
            ]
            for facing_id in facing_ids
        }
        for equipment_id in equipment_ids
    }
    character_bytes = []
    equipment_bytes = {equipment_id: [] for equipment_id in equipment_ids}
    composite_bytes = {equipment_id: [] for equipment_id in equipment_ids}
    for frame_index in range(frame_count):
        character_bytes.append(
            sum(
                _area(_expand_rect(character_rects[facing_id][frame_index], padding, canvas)) * 6
                for facing_id in facing_ids
            )
        )
        for equipment_id in equipment_ids:
            equipment_bytes[equipment_id].append(
                sum(
                    _area(
                        _expand_rect(
                            equipment_rects[equipment_id][facing_id][frame_index],
                            padding,
                            canvas,
                        )
                    ) * 6
                    for facing_id in facing_ids
                )
            )
            composite_bytes[equipment_id].append(
                sum(
                    _area(
                        _expand_rect(
                            _union_rect(
                                character_rects[facing_id][frame_index],
                                equipment_rects[equipment_id][facing_id][frame_index],
                            ),
                            padding,
                            canvas,
                        )
                    ) * 4
                    for facing_id in facing_ids
                )
            )
    return {
        "characterTightDecodedBytes": character_bytes,
        "equipmentTightDecodedBytes": equipment_bytes,
        "compositeCacheDecodedBytes": composite_bytes,
    }


def build_motion_analysis(repository: Path, plan: dict) -> dict:
    planning = plan["planning"]
    inventory = _source_inventory(repository, plan)
    character_manifest = json.loads(
        (repository / plan["character"]["manifest"]).read_text(encoding="utf-8")
    )
    analyzer = ScreenSpaceMotionAnalyzer(
        repository,
        repository / planning["analysisPlan"],
    )
    canvas = tuple(int(value) for value in plan["canvas"])
    padding = int(plan["cropPadding"])
    equipment_ids = [entry["id"] for entry in plan["equipment"]]
    animations = {}
    for clip_id in plan["clips"]:
        definition = character_manifest["animations"][clip_id]
        frame_count = int(inventory[clip_id]["frameCount"])
        playback_duration = (
            float(definition["durationSeconds"])
            if "durationSeconds" in definition
            else frame_count / float(definition["framesPerSecond"])
        )
        measurement = analyzer.measure(clip_id, frame_count)
        roots = {
            facing["id"]: [
                int(
                    definition.get("rootXByFacing", {}).get(
                        facing["id"],
                        plan["rootX"],
                    )
                ),
                int(plan["groundY"]),
            ]
            for facing in plan["facings"]
        }
        animations[clip_id] = {
            "sourceFrameCount": frame_count,
            "sourceDurationSeconds": measurement.source_duration_seconds,
            "playbackDurationSeconds": playback_duration,
            "loop": bool(definition["loop"]),
            "roots": roots,
            "segments": {
                "combinedPixels": list(measurement.segment_pixels),
                "characterPixels": list(measurement.character_segment_pixels),
                "equipmentPixels": {
                    equipment_id: list(values)
                    for equipment_id, values in (
                        measurement.equipment_segment_pixels or {}
                    ).items()
                },
            },
            "costs": _estimate_costs(
                inventory[clip_id],
                plan["facings"],
                equipment_ids,
                frame_count,
                padding,
                canvas,
            ),
        }
    return {
        "format": ANALYSIS_FORMAT,
        "id": plan["id"],
        "inputFingerprint": analysis_input_fingerprint(repository, plan),
        "canvas": list(canvas),
        "measurementFramesPerSecond": analyzer.fps,
        "facings": [facing["id"] for facing in plan["facings"]],
        "equipment": equipment_ids,
        "animations": animations,
    }


def validate_analysis(repository: Path, plan: dict, analysis: dict) -> None:
    if analysis.get("format") != ANALYSIS_FORMAT:
        raise ValueError(f"Unsupported motion analysis format: {analysis.get('format')}")
    if analysis.get("id") != plan["id"]:
        raise ValueError("Motion analysis and sparse plan IDs differ")
    if analysis.get("canvas") != plan["canvas"]:
        raise ValueError("Motion analysis and sparse plan canvases differ")
    current_fingerprint = analysis_input_fingerprint(repository, plan)
    if analysis.get("inputFingerprint") != current_fingerprint:
        raise ValueError("Motion analysis is stale; rerun with --reanalyze")


def _profile_segments(animation: dict, profile: dict) -> list[float]:
    scope = profile.get("motionScope", "combined")
    segments = animation["segments"]
    if scope == "combined":
        return list(segments["combinedPixels"])
    if scope == "character":
        return list(segments["characterPixels"])
    if scope.startswith("equipment:"):
        equipment_id = scope.split(":", 1)[1]
        return list(segments["equipmentPixels"][equipment_id])
    if scope.startswith("loadout:"):
        equipment_id = scope.split(":", 1)[1]
        return [
            max(character, equipment)
            for character, equipment in zip(
                segments["characterPixels"],
                segments["equipmentPixels"][equipment_id],
                strict=True,
            )
        ]
    raise ValueError(f"Unknown motion scope: {scope}")


def _selected_indices(animation_id: str, animation: dict, profile: dict) -> list[int]:
    indices = select_motion_indices(
        _profile_segments(animation, profile),
        float(animation["playbackDurationSeconds"]),
        float(profile["maxPixelsPerSample"]),
        float(profile["minimumFramesPerSecond"]),
        bool(animation["loop"]),
    )
    required = profile.get("requiredSourceFrames", {}).get(animation_id, [])
    required_indices = [int(source_frame) - 1 for source_frame in required]
    frame_count = int(animation["sourceFrameCount"])
    if any(index < 0 or index >= frame_count for index in required_indices):
        raise ValueError(f"Required source frame is outside {animation_id}")
    return sorted(set(indices).union(required_indices))


def _selection_costs(analysis: dict, selections: dict[str, list[int]]) -> dict:
    equipment_ids = list(analysis["equipment"])
    character_tight = 0
    equipment_tight = {equipment_id: 0 for equipment_id in equipment_ids}
    composite_cache = {equipment_id: 0 for equipment_id in equipment_ids}
    for animation_id, indices in selections.items():
        costs = analysis["animations"][animation_id]["costs"]
        character_tight += sum(costs["characterTightDecodedBytes"][index] for index in indices)
        for equipment_id in equipment_ids:
            equipment_tight[equipment_id] += sum(
                costs["equipmentTightDecodedBytes"][equipment_id][index]
                for index in indices
            )
            composite_cache[equipment_id] += sum(
                costs["compositeCacheDecodedBytes"][equipment_id][index]
                for index in indices
            )
    shared_tight = character_tight + sum(equipment_tight.values())
    return {
        "characterTightDecodedBytes": character_tight,
        "equipmentTightDecodedBytes": equipment_tight,
        "sharedLibraryTightDecodedBytes": shared_tight,
        "loadoutCompositeCacheDecodedBytes": composite_cache,
        "maximumLoadoutCompositeCacheDecodedBytes": max(
            composite_cache.values(),
            default=0,
        ),
    }


def build_timeline_from_analysis(
    plan: dict,
    analysis: dict,
    profile_name: str,
    profile: dict,
    analysis_reference: str,
) -> tuple[dict, dict]:
    selections = {
        animation_id: _selected_indices(animation_id, animation, profile)
        for animation_id, animation in analysis["animations"].items()
    }
    animations = {}
    for animation_id, animation in analysis["animations"].items():
        frame_count = int(animation["sourceFrameCount"])
        duration = float(animation["playbackDurationSeconds"])
        indices = selections[animation_id]
        times = playback_sample_times(frame_count, duration)
        durations = frame_durations(indices, frame_count, duration)
        animations[animation_id] = {
            "loop": bool(animation["loop"]),
            "durationSeconds": duration,
            "sourceFrameCount": frame_count,
            "sampling": {
                "mode": "planned-screen-space-motion",
                "profile": profile_name,
                "motionScope": profile.get("motionScope", "combined"),
                "maxPixelsPerSample": float(profile["maxPixelsPerSample"]),
                "minimumFramesPerSecond": float(profile["minimumFramesPerSecond"]),
            },
            "samples": [
                {
                    "sourceFrame": source_index + 1,
                    "timeSeconds": times[source_index],
                    "durationSeconds": sample_duration,
                }
                for source_index, sample_duration in zip(indices, durations, strict=True)
            ],
            "facings": {
                facing_id: {"root": root}
                for facing_id, root in animation["roots"].items()
            },
        }
    source_pose_count = sum(
        int(animation["sourceFrameCount"])
        for animation in analysis["animations"].values()
    )
    selected_pose_count = sum(len(indices) for indices in selections.values())
    metrics = {
        "profile": profile_name,
        "sourcePoses": source_pose_count,
        "selectedPoses": selected_pose_count,
        "reduction": 1.0 - selected_pose_count / source_pose_count,
        "perAnimation": {
            animation_id: {
                "sourcePoses": int(analysis["animations"][animation_id]["sourceFrameCount"]),
                "selectedPoses": len(indices),
            }
            for animation_id, indices in selections.items()
        },
        "estimatedBytes": _selection_costs(analysis, selections),
    }
    timeline = {
        "format": TIMELINE_FORMAT,
        "id": plan["id"],
        "canvas": list(plan["canvas"]),
        "targetFramesPerSecond": float(plan["targetFramesPerSecond"]),
        "sampling": {
            "mode": "planned-screen-space-motion",
            "profile": profile_name,
            "analysis": analysis_reference,
            **profile,
        },
        "metrics": metrics,
        "animations": animations,
    }
    return timeline, metrics


def build_independent_layer_timelines(
    plan: dict,
    analysis: dict,
    profile_name: str,
    profile: dict,
    analysis_reference: str,
) -> tuple[dict, dict, dict[str, dict], dict]:
    """Build one character timeline and one independently sampled timeline per item.

    All timelines retain the same clip durations and loop policy.  Only their
    source-pose grids differ, allowing runtime layers to advance on one clock
    without duplicating character pixels in every equipment package.
    """
    character_profile = {**profile, "motionScope": "character"}
    character_timeline, character_metrics = build_timeline_from_analysis(
        plan,
        analysis,
        f"{profile_name}-character",
        character_profile,
        analysis_reference,
    )

    equipment_timelines: dict[str, dict] = {}
    equipment_metrics: dict[str, dict] = {}
    for equipment_id in analysis["equipment"]:
        equipment_profile = {
            **profile,
            "motionScope": f"equipment:{equipment_id}",
        }
        timeline, metrics = build_timeline_from_analysis(
            plan,
            analysis,
            f"{profile_name}-{equipment_id}",
            equipment_profile,
            analysis_reference,
        )
        equipment_timelines[equipment_id] = timeline
        equipment_metrics[equipment_id] = metrics

    character_bytes = int(
        character_metrics["estimatedBytes"]["characterTightDecodedBytes"]
    )
    equipment_bytes = {
        equipment_id: int(
            metrics["estimatedBytes"]["equipmentTightDecodedBytes"][equipment_id]
        )
        for equipment_id, metrics in equipment_metrics.items()
    }
    metrics = {
        "profile": profile_name,
        "characterSelectedPoses": character_metrics["selectedPoses"],
        "equipmentSelectedPoses": {
            equipment_id: value["selectedPoses"]
            for equipment_id, value in equipment_metrics.items()
        },
        "totalStoredPoses": character_metrics["selectedPoses"] + sum(
            value["selectedPoses"] for value in equipment_metrics.values()
        ),
        "estimatedBytes": {
            "characterTightDecodedBytes": character_bytes,
            "equipmentTightDecodedBytes": equipment_bytes,
            "sharedLibraryTightDecodedBytes": character_bytes
            + sum(equipment_bytes.values()),
        },
    }
    timeline_set = {
        "format": TIMELINE_SET_FORMAT,
        "id": plan["id"],
        "canvas": list(plan["canvas"]),
        "profile": profile_name,
        "character": "character-timeline.json",
        "equipment": {
            equipment_id: f"equipment/{equipment_id}-timeline.json"
            for equipment_id in equipment_timelines
        },
        "metrics": metrics,
    }
    return timeline_set, character_timeline, equipment_timelines, metrics


def build_curve_report(analysis: dict, planning: dict) -> list[dict]:
    minimum_fps = float(planning.get("curveMinimumFramesPerSecond", 6.0))
    motion_scope = planning.get("curveMotionScope", "combined")
    curve = []
    for threshold in planning.get(
        "curvePixels",
        [1, 2, 3, 4, 5, 6, 8, 10, 12, 16, 24, 32],
    ):
        profile = {
            "maxPixelsPerSample": float(threshold),
            "minimumFramesPerSecond": minimum_fps,
            "motionScope": motion_scope,
        }
        selections = {
            animation_id: _selected_indices(animation_id, animation, profile)
            for animation_id, animation in analysis["animations"].items()
        }
        source_count = sum(
            int(animation["sourceFrameCount"])
            for animation in analysis["animations"].values()
        )
        selected_count = sum(len(indices) for indices in selections.values())
        curve.append(
            {
                "maxPixelsPerSample": float(threshold),
                "selectedPoses": selected_count,
                "reduction": 1.0 - selected_count / source_count,
                "perAnimation": {
                    animation_id: len(indices)
                    for animation_id, indices in selections.items()
                },
                "estimatedBytes": _selection_costs(analysis, selections),
            }
        )
    return curve


def recommend_for_budgets(curve: list[dict], planning: dict) -> list[dict]:
    recommendations = []
    for budget in planning.get("budgets", []):
        metric = budget["metric"]
        byte_limit = int(budget["bytes"])
        qualifying = [
            point
            for point in curve
            if int(point["estimatedBytes"][metric]) <= byte_limit
        ]
        selected = min(
            qualifying,
            key=lambda point: float(point["maxPixelsPerSample"]),
            default=None,
        )
        recommendations.append(
            {
                "name": budget["name"],
                "metric": metric,
                "budgetBytes": byte_limit,
                "recommendedMaxPixelsPerSample": (
                    selected["maxPixelsPerSample"] if selected is not None else None
                ),
                "selectedPoses": (
                    selected["selectedPoses"] if selected is not None else None
                ),
                "estimatedBytes": (
                    selected["estimatedBytes"][metric]
                    if selected is not None
                    else None
                ),
            }
        )
    return recommendations
