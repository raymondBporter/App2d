#!/usr/bin/env python3
"""Render fixed-canvas 2D guides from the CC0 KayKit Rig_Medium walk."""

from __future__ import annotations

import argparse
import json
import math
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw


COMPONENT_DTYPES = {
    5120: np.int8,
    5121: np.uint8,
    5122: np.int16,
    5123: np.uint16,
    5125: np.uint32,
    5126: np.float32,
}
TYPE_WIDTHS = {
    "SCALAR": 1,
    "VEC2": 2,
    "VEC3": 3,
    "VEC4": 4,
    "MAT2": 4,
    "MAT3": 9,
    "MAT4": 16,
}


class Glb:
    def __init__(self, path: Path) -> None:
        data = path.read_bytes()
        magic, version, total_length = struct.unpack_from("<4sII", data, 0)
        if magic != b"glTF" or version != 2 or total_length != len(data):
            raise ValueError(f"Unsupported GLB: {path}")

        offset = 12
        json_chunk = None
        binary_chunk = None
        while offset < len(data):
            chunk_length, chunk_type = struct.unpack_from("<II", data, offset)
            offset += 8
            chunk = data[offset : offset + chunk_length]
            offset += chunk_length
            if chunk_type == 0x4E4F534A:
                json_chunk = chunk
            elif chunk_type == 0x004E4942:
                binary_chunk = chunk

        if json_chunk is None or binary_chunk is None:
            raise ValueError(f"GLB is missing JSON or binary data: {path}")
        self.document = json.loads(json_chunk.decode("utf-8").rstrip("\x00 \t\r\n"))
        self.binary = binary_chunk

    def accessor(self, index: int) -> np.ndarray:
        accessor = self.document["accessors"][index]
        if "sparse" in accessor:
            raise ValueError("Sparse GLTF accessors are not supported")
        view = self.document["bufferViews"][accessor["bufferView"]]
        dtype = np.dtype(COMPONENT_DTYPES[accessor["componentType"]]).newbyteorder("<")
        width = TYPE_WIDTHS[accessor["type"]]
        count = accessor["count"]
        start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
        packed_stride = dtype.itemsize * width
        stride = view.get("byteStride", packed_stride)
        if stride == packed_stride:
            values = np.frombuffer(self.binary, dtype=dtype, count=count * width, offset=start)
            return values.reshape(count, width).copy()
        values = np.ndarray(
            shape=(count, width),
            dtype=dtype,
            buffer=self.binary,
            offset=start,
            strides=(stride, dtype.itemsize),
        )
        return values.copy()


def quaternion_matrix(value: np.ndarray) -> np.ndarray:
    x, y, z, w = value / max(np.linalg.norm(value), 1e-12)
    return np.array(
        [
            [1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w), 0],
            [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w), 0],
            [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y), 0],
            [0, 0, 0, 1],
        ],
        dtype=np.float64,
    )


def transform_matrix(translation: np.ndarray, rotation: np.ndarray, scale: np.ndarray) -> np.ndarray:
    matrix = quaternion_matrix(rotation)
    matrix[:3, :3] *= scale[np.newaxis, :]
    matrix[:3, 3] = translation
    return matrix


def slerp(a: np.ndarray, b: np.ndarray, amount: float) -> np.ndarray:
    a = a / max(np.linalg.norm(a), 1e-12)
    b = b / max(np.linalg.norm(b), 1e-12)
    dot = float(np.dot(a, b))
    if dot < 0:
        b = -b
        dot = -dot
    if dot > 0.9995:
        value = a + amount * (b - a)
        return value / max(np.linalg.norm(value), 1e-12)
    theta = math.acos(max(-1.0, min(1.0, dot)))
    sin_theta = math.sin(theta)
    return (
        math.sin((1 - amount) * theta) / sin_theta * a
        + math.sin(amount * theta) / sin_theta * b
    )


def sample_values(times: np.ndarray, values: np.ndarray, time: float, path: str, interpolation: str) -> np.ndarray:
    scalar_times = times[:, 0]
    if time <= scalar_times[0]:
        return values[0]
    if time >= scalar_times[-1]:
        return values[-1]
    right = int(np.searchsorted(scalar_times, time, side="right"))
    left = right - 1
    if interpolation == "STEP":
        return values[left]
    span = scalar_times[right] - scalar_times[left]
    amount = float((time - scalar_times[left]) / span)
    if path == "rotation":
        return slerp(values[left], values[right], amount)
    return values[left] * (1 - amount) + values[right] * amount


class AnimationSampler:
    def __init__(self, glb: Glb, animation_name: str) -> None:
        self.glb = glb
        self.nodes = glb.document["nodes"]
        matches = [item for item in glb.document.get("animations", []) if item.get("name") == animation_name]
        if len(matches) != 1:
            names = [item.get("name") for item in glb.document.get("animations", [])]
            raise ValueError(f"Animation {animation_name!r} not found; available: {names}")
        self.animation = matches[0]
        self.channels = []
        self.duration = 0.0
        for channel in self.animation["channels"]:
            sampler = self.animation["samplers"][channel["sampler"]]
            times = glb.accessor(sampler["input"])
            values = glb.accessor(sampler["output"])
            self.duration = max(self.duration, float(times[-1, 0]))
            self.channels.append(
                (
                    channel["target"]["node"],
                    channel["target"]["path"],
                    times,
                    values,
                    sampler.get("interpolation", "LINEAR"),
                )
            )

        self.parents = [-1] * len(self.nodes)
        for parent, node in enumerate(self.nodes):
            for child in node.get("children", []):
                self.parents[child] = parent
        self.index_by_name = {node.get("name"): index for index, node in enumerate(self.nodes)}

    def world_matrices(
        self,
        time: float,
        mirror_sides: bool = False,
    ) -> list[np.ndarray]:
        translations = []
        rotations = []
        scales = []
        for node in self.nodes:
            translations.append(np.array(node.get("translation", [0, 0, 0]), dtype=np.float64))
            rotations.append(np.array(node.get("rotation", [0, 0, 0, 1]), dtype=np.float64))
            scales.append(np.array(node.get("scale", [1, 1, 1]), dtype=np.float64))
        for node_index, path, times, values, interpolation in self.channels:
            value = sample_values(times, values, time, path, interpolation)
            if path == "translation":
                translations[node_index] = value.astype(np.float64)
            elif path == "rotation":
                rotations[node_index] = value.astype(np.float64)
            elif path == "scale":
                scales[node_index] = value.astype(np.float64)

        local = [
            transform_matrix(translations[i], rotations[i], scales[i])
            for i in range(len(self.nodes))
        ]
        if mirror_sides:
            reflection = np.diag([-1.0, 1.0, 1.0, 1.0])
            mirrored = list(local)
            for target_index, target_node in enumerate(self.nodes):
                if "mesh" in target_node:
                    continue
                target_name = target_node.get("name")
                source_index = target_index
                if target_name and target_name.endswith(".l"):
                    source_index = self.index_by_name.get(
                        target_name[:-2] + ".r",
                        target_index,
                    )
                elif target_name and target_name.endswith(".r"):
                    source_index = self.index_by_name.get(
                        target_name[:-2] + ".l",
                        target_index,
                    )
                mirrored[target_index] = reflection @ local[source_index] @ reflection
            local = mirrored
        world: list[np.ndarray | None] = [None] * len(self.nodes)

        def resolve(index: int) -> np.ndarray:
            if world[index] is not None:
                return world[index]  # type: ignore[return-value]
            parent = self.parents[index]
            world[index] = local[index] if parent < 0 else resolve(parent) @ local[index]
            return world[index]  # type: ignore[return-value]

        for index in range(len(self.nodes)):
            resolve(index)
        return [matrix for matrix in world if matrix is not None]

    def world_positions(self, time: float) -> dict[str, np.ndarray]:
        world = self.world_matrices(time)
        return {
            name: world[index][:3, 3].copy()
            for name, index in self.index_by_name.items()
            if name is not None
        }


CANVAS = (512, 384)
ROOT_X = 256
GROUND_Y = 324
TARGET_BONE_HEIGHT = 190


def projected_pose(world: dict[str, np.ndarray]) -> dict[str, tuple[float, float, float]]:
    # KayKit uses Y-up and faces along Z. Looking along X gives a side view.
    foot_names = ("foot.l", "toes.l", "foot.r", "toes.r")
    ground = min(float(world[name][1]) for name in foot_names)
    head = float(world["head"][1])
    scale = TARGET_BONE_HEIGHT / max(head - ground, 1e-6)
    hips_z = float(world["hips"][2])
    return {
        name: (
            ROOT_X - (float(value[2]) - hips_z) * scale,
            GROUND_Y - (float(value[1]) - ground) * scale,
            float(value[0]),
        )
        for name, value in world.items()
    }


def line(draw: ImageDraw.ImageDraw, pose, names, fill, width) -> None:
    points = [(pose[name][0], pose[name][1]) for name in names]
    draw.line(points, fill=fill, width=width, joint="curve")
    radius = max(2, width // 3)
    for x, y in points:
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=fill)


def render_pose(pose: dict[str, tuple[float, float, float]]) -> Image.Image:
    image = Image.new("RGBA", CANVAS, (255, 255, 255, 0))
    draw = ImageDraw.Draw(image)
    draw.line((88, GROUND_Y, 424, GROUND_Y), fill=(90, 90, 100, 175), width=2)
    draw.line((ROOT_X, 54, ROOT_X, 344), fill=(120, 120, 130, 70), width=1)

    # Sort the paired limbs by source-space X so the far limbs are drawn first.
    left_is_far = pose["upperarm.l"][2] < pose["upperarm.r"][2]
    far_suffix, near_suffix = ("l", "r") if left_is_far else ("r", "l")
    far_color = (88, 114, 146, 185)
    near_color = (27, 158, 220, 235)
    leg_far = (105, 90, 145, 190)
    leg_near = (186, 84, 210, 235)

    line(draw, pose, [f"upperleg.{far_suffix}", f"lowerleg.{far_suffix}", f"foot.{far_suffix}", f"toes.{far_suffix}"], leg_far, 18)
    line(draw, pose, [f"upperarm.{far_suffix}", f"lowerarm.{far_suffix}", f"wrist.{far_suffix}", f"hand.{far_suffix}"], far_color, 15)
    line(draw, pose, ["hips", "spine", "chest", "head"], (44, 185, 111, 235), 28)

    head_x, head_y, _ = pose["head"]
    draw.ellipse((head_x - 43, head_y - 52, head_x + 43, head_y + 34), fill=(55, 193, 126, 235), outline=(20, 80, 55, 255), width=3)
    hips_x, hips_y, _ = pose["hips"]
    draw.ellipse((hips_x - 25, hips_y - 18, hips_x + 25, hips_y + 20), fill=(44, 185, 111, 235))

    line(draw, pose, [f"upperleg.{near_suffix}", f"lowerleg.{near_suffix}", f"foot.{near_suffix}", f"toes.{near_suffix}"], leg_near, 18)
    line(draw, pose, [f"upperarm.{near_suffix}", f"lowerarm.{near_suffix}", f"wrist.{near_suffix}", f"hand.{near_suffix}"], near_color, 15)

    # The finished knight always carries the shield in front and the sword forward/down.
    sword_hand = pose[f"hand.{near_suffix}"]
    grip = (sword_hand[0], sword_hand[1])
    tip = (grip[0] + 104, grip[1] + 34)
    draw.line((grip, tip), fill=(244, 196, 48, 255), width=10)
    draw.polygon(
        [(tip[0], tip[1]), (tip[0] - 18, tip[1] - 12), (tip[0] - 12, tip[1] + 10)],
        fill=(244, 196, 48, 255),
    )
    shield_x = pose[f"hand.{far_suffix}"][0] + 26
    shield_y = pose[f"hand.{far_suffix}"][1] - 12
    draw.rounded_rectangle(
        (shield_x - 27, shield_y - 42, shield_x + 27, shield_y + 42),
        radius=11,
        fill=(237, 103, 61, 220),
        outline=(116, 43, 28, 255),
        width=4,
    )

    draw.ellipse((ROOT_X - 5, pose["hips"][1] - 5, ROOT_X + 5, pose["hips"][1] + 5), fill=(255, 255, 255, 255))
    return image


def alpha_bbox(image: Image.Image) -> tuple[int, int, int, int] | None:
    return image.getchannel("A").getbbox()


def main() -> None:
    global CANVAS, ROOT_X, GROUND_Y, TARGET_BONE_HEIGHT

    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--glb", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--animation")
    parser.add_argument("--frames", type=int)
    args = parser.parse_args()

    profile = json.loads(args.profile.read_text(encoding="utf-8"))
    source = profile["source"]
    registration = profile["registration"]
    profile_root = args.profile.parent
    args.glb = args.glb or profile_root / source["glb"]
    args.animation = args.animation or source["animation"]
    args.frames = args.frames or profile["frames"]["count"]
    CANVAS = (registration["canvasWidth"], registration["canvasHeight"])
    ROOT_X = registration["rootX"]
    GROUND_Y = registration["groundY"]
    TARGET_BONE_HEIGHT = registration["targetBoneHeight"]

    args.output.mkdir(parents=True, exist_ok=True)
    sampler = AnimationSampler(Glb(args.glb), args.animation)
    frames = []
    metadata = {
        "source": str(args.glb),
        "animation": args.animation,
        "sourceDurationSeconds": sampler.duration,
        "canvas": list(CANVAS),
        "rootX": ROOT_X,
        "groundY": GROUND_Y,
        "frames": [],
    }
    for index in range(args.frames):
        time = sampler.duration * index / args.frames
        pose = projected_pose(sampler.world_positions(time))
        frame = render_pose(pose)
        path = args.output / f"guide-{index + 1:02d}.png"
        frame.save(path)
        frames.append(frame)
        metadata["frames"].append(
            {
                "file": path.name,
                "sourceTimeSeconds": time,
                "alphaBounds": alpha_bbox(frame),
                "hips": [pose["hips"][0], pose["hips"][1]],
                "leftFoot": [pose["foot.l"][0], pose["foot.l"][1]],
                "rightFoot": [pose["foot.r"][0], pose["foot.r"][1]],
            }
        )

    sheet = Image.new("RGBA", (CANVAS[0] * 3, CANVAS[1] * 2), (238, 238, 234, 255))
    sheet_draw = ImageDraw.Draw(sheet)
    for index, frame in enumerate(frames):
        x = index % 3 * CANVAS[0]
        y = index // 3 * CANVAS[1]
        sheet.alpha_composite(frame, (x, y))
        sheet_draw.rectangle((x, y, x + CANVAS[0] - 1, y + CANVAS[1] - 1), outline=(80, 80, 88, 255), width=2)
        sheet_draw.text((x + 12, y + 10), f"FRAME {index + 1}", fill=(40, 40, 48, 255))
    sheet.save(args.output / "walk-guide-sheet.png")
    (args.output / "walk-guide-metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
