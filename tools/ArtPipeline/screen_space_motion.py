#!/usr/bin/env python3
"""Measure and sample KayKit animation motion in final-canvas pixels."""

from __future__ import annotations

import json
import math
from dataclasses import dataclass
from pathlib import Path

import numpy as np

from render_attached_weapon_layers import (
    facing_transform,
    load_weapon,
    transform_points,
)
from render_loadout_equipment_layers import (
    equipment_grip_matrix,
    local_z_rotation_matrix,
)
from render_mannequin_walk import inverse_bind_matrices, load_primitives, skin_vertices
from render_walk_guides import AnimationSampler, Glb


@dataclass(frozen=True)
class MotionMeasurement:
    source_duration_seconds: float
    source_frames_per_second: float
    segment_pixels: tuple[float, ...]

    @property
    def frame_count(self) -> int:
        return len(self.segment_pixels)


def playback_sample_times(frame_count: int, duration_seconds: float) -> list[float]:
    if frame_count <= 0:
        raise ValueError("frame_count must be positive")
    if not math.isfinite(duration_seconds) or duration_seconds <= 0:
        raise ValueError("duration_seconds must be positive and finite")
    return [index * duration_seconds / frame_count for index in range(frame_count)]


def frame_durations(indices: list[int], frame_count: int, duration_seconds: float) -> list[float]:
    if not indices or indices[0] != 0:
        raise ValueError("selected indices must begin with source frame zero")
    if any(left >= right for left, right in zip(indices, indices[1:])):
        raise ValueError("selected indices must be strictly increasing")
    if indices[-1] >= frame_count:
        raise ValueError("selected index is outside the source animation")
    times = playback_sample_times(frame_count, duration_seconds)
    selected_times = [times[index] for index in indices]
    return [
        (selected_times[index + 1] if index + 1 < len(indices) else duration_seconds)
        - time
        for index, time in enumerate(selected_times)
    ]


def select_motion_indices(
    segment_pixels: list[float] | tuple[float, ...],
    duration_seconds: float,
    max_pixels_per_sample: float,
    minimum_frames_per_second: float,
    loop: bool,
) -> list[int]:
    """Select source poses by accumulated curve length and maximum hold time.

    ``segment_pixels[i]`` is the measured path length from source pose ``i`` to
    pose ``i + 1``.  The final entry spans the last source pose to the authored
    clip endpoint and is used to close loops.
    """

    frame_count = len(segment_pixels)
    if frame_count <= 0:
        raise ValueError("segment_pixels must not be empty")
    if any(not math.isfinite(value) or value < 0 for value in segment_pixels):
        raise ValueError("segment motion must be finite and nonnegative")
    if not math.isfinite(max_pixels_per_sample) or max_pixels_per_sample <= 0:
        raise ValueError("max_pixels_per_sample must be positive and finite")
    if not math.isfinite(minimum_frames_per_second) or minimum_frames_per_second <= 0:
        raise ValueError("minimum_frames_per_second must be positive and finite")

    times = playback_sample_times(frame_count, duration_seconds)
    maximum_hold = 1.0 / minimum_frames_per_second
    selected = [0]
    accumulated_motion = 0.0
    epsilon = 1e-9

    for source_index in range(1, frame_count):
        accumulated_motion += segment_pixels[source_index - 1]
        held_seconds = times[source_index] - times[selected[-1]]
        if (
            accumulated_motion + epsilon >= max_pixels_per_sample
            or held_seconds + epsilon >= maximum_hold
        ):
            selected.append(source_index)
            accumulated_motion = 0.0

    if loop:
        accumulated_motion += segment_pixels[-1]
        held_seconds = duration_seconds - times[selected[-1]]
        if (
            selected[-1] != frame_count - 1
            and (
                accumulated_motion + epsilon >= max_pixels_per_sample
                or held_seconds > maximum_hold + epsilon
            )
        ):
            selected.append(frame_count - 1)
    elif selected[-1] != frame_count - 1:
        selected.append(frame_count - 1)

    return selected


def _project(vertices: np.ndarray, hips: np.ndarray, ground: float, scale: float) -> np.ndarray:
    projected = np.empty((len(vertices), 2), dtype=np.float64)
    projected[:, 0] = (vertices[:, 2] - hips[2]) * scale
    projected[:, 1] = -(vertices[:, 1] - ground) * scale
    return projected


def _maximum_displacement(
    previous: dict[str, np.ndarray],
    current: dict[str, np.ndarray],
) -> float:
    if previous.keys() != current.keys():
        raise ValueError("motion point sets changed between animation samples")
    maximum = 0.0
    for key, points in current.items():
        prior = previous[key]
        if points.shape != prior.shape:
            raise ValueError(f"motion point count changed for {key}")
        if len(points):
            maximum = max(
                maximum,
                float(np.linalg.norm(points - prior, axis=1).max()),
            )
    return maximum


class ScreenSpaceMotionAnalyzer:
    """Re-evaluates render-plan geometry without rasterizing any images."""

    def __init__(self, repository: Path, render_plan_path: Path) -> None:
        self.repository = repository.resolve()
        self.render_plan_path = render_plan_path.resolve()
        self.plan = json.loads(self.render_plan_path.read_text(encoding="utf-8"))
        self.fps = float(self.plan["fps"])
        self.scale = float(self.plan["scale"])
        if not math.isfinite(self.fps) or self.fps <= 0:
            raise ValueError("motion render-plan fps must be positive and finite")
        if not math.isfinite(self.scale) or self.scale <= 0:
            raise ValueError("motion render-plan scale must be positive and finite")
        self.clips = {clip["id"]: clip for clip in self.plan["clips"]}
        self.facings = self.plan["facings"]
        self.equipment = [
            {
                **entry,
                "mesh": load_weapon(self.repository / entry["gltf"]),
                "gripMatrix": equipment_grip_matrix(entry.get("grip")),
            }
            for entry in self.plan.get("equipment", [])
        ]
        self._cache: dict[str, MotionMeasurement] = {}

    def measure(self, clip_id: str, expected_frame_count: int) -> MotionMeasurement:
        if clip_id in self._cache:
            measurement = self._cache[clip_id]
            if measurement.frame_count != expected_frame_count:
                raise ValueError(
                    f"Motion/source frame mismatch for {clip_id}: "
                    f"{measurement.frame_count} and {expected_frame_count}"
                )
            return measurement
        if clip_id not in self.clips:
            raise ValueError(f"Motion render plan does not define {clip_id}")

        clip = self.clips[clip_id]
        glb = Glb(self.repository / clip["glb"])
        sampler = AnimationSampler(glb, clip["animation"])
        primitives = load_primitives(glb)
        skin_index = next(
            node["skin"]
            for node in glb.document["nodes"]
            if "mesh" in node and "skin" in node
        )
        skin = glb.document["skins"][skin_index]
        joint_nodes = skin["joints"]
        inverse_bind = inverse_bind_matrices(glb, skin_index)
        hips_index = sampler.index_by_name["hips"]
        hand_indices = {
            entry["id"]: sampler.index_by_name[entry["hand"]]
            for entry in self.equipment
        }
        frame_count = max(1, round(sampler.duration * self.fps))
        if frame_count != expected_frame_count:
            raise ValueError(
                f"Motion/source frame mismatch for {clip_id}: "
                f"render plan has {frame_count}, source has {expected_frame_count}"
            )

        samples: list[dict[str, np.ndarray]] = []
        # Include the exact authored endpoint so a loop's final segment closes
        # against KayKit's own pose rather than an invented duplicate frame.
        source_times = [index / self.fps for index in range(frame_count)]
        source_times.append(sampler.duration)
        for source_time in source_times:
            world = sampler.world_matrices(
                source_time,
                mirror_sides=clip.get("mirrorPose", False),
            )
            posed = [
                (primitive, skin_vertices(primitive, world, joint_nodes, inverse_bind))
                for primitive in primitives
            ]
            all_vertices = np.concatenate([vertices for _, vertices in posed], axis=0)
            ground = float(all_vertices[:, 1].min())
            hips = world[hips_index][:3, 3]
            points: dict[str, np.ndarray] = {}

            for facing in self.facings:
                facing_id = facing["id"]
                turn = facing_transform(hips, facing["yawDegrees"])
                facing_hips = transform_points(turn, hips.reshape(1, 3))[0]
                for primitive, vertices in posed:
                    facing_vertices = transform_points(turn, vertices)
                    points[f"{facing_id}/character/{primitive.name}"] = _project(
                        facing_vertices,
                        facing_hips,
                        ground,
                        self.scale,
                    )

                for entry in self.equipment:
                    forearm_roll = entry.get(
                        "forearmRollDegreesByClip",
                        {},
                    ).get(clip_id, 0)
                    attachment = (
                        turn
                        @ world[hand_indices[entry["id"]]]
                        @ entry["gripMatrix"]
                        @ local_z_rotation_matrix(forearm_roll)
                    )
                    weapon_vertices = transform_points(
                        attachment @ entry["mesh"].local_matrix,
                        entry["mesh"].positions,
                    )
                    points[f"{facing_id}/equipment/{entry['id']}"] = _project(
                        weapon_vertices,
                        facing_hips,
                        ground,
                        self.scale,
                    )
            samples.append(points)

        segments = tuple(
            _maximum_displacement(samples[index], samples[index + 1])
            for index in range(frame_count)
        )
        measurement = MotionMeasurement(sampler.duration, self.fps, segments)
        self._cache[clip_id] = measurement
        return measurement
