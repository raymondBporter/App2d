#!/usr/bin/env python3
"""Render the animated KayKit mannequin as fixed-camera transparent PNG frames."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from normalize_generated_sheet import resize_rgba_premultiplied
from render_walk_guides import AnimationSampler, Glb


@dataclass(frozen=True)
class SkinnedPrimitive:
    name: str
    node_index: int
    positions: np.ndarray
    joints: np.ndarray
    weights: np.ndarray
    triangles: np.ndarray
    color: tuple[int, int, int]


PART_COLORS = {
    "ArmLeft": (61, 151, 191),
    "ArmRight": (84, 184, 215),
    "Body": (219, 119, 66),
    "Head": (64, 166, 202),
    "LegLeft": (97, 116, 170),
    "LegRight": (126, 139, 196),
}


def part_color(name: str) -> tuple[int, int, int]:
    for suffix, color in PART_COLORS.items():
        if name.endswith(suffix):
            return color
    return (150, 165, 180)


def inverse_bind_matrices(glb: Glb, skin_index: int) -> np.ndarray:
    skin = glb.document["skins"][skin_index]
    values = glb.accessor(skin["inverseBindMatrices"])
    # glTF matrices are stored column-major.
    return values.reshape((-1, 4, 4)).transpose((0, 2, 1)).astype(np.float64)


def load_primitives(glb: Glb) -> list[SkinnedPrimitive]:
    result: list[SkinnedPrimitive] = []
    for node_index, node in enumerate(glb.document["nodes"]):
        if "mesh" not in node or "skin" not in node:
            continue
        mesh = glb.document["meshes"][node["mesh"]]
        for primitive_index, primitive in enumerate(mesh["primitives"]):
            if primitive.get("mode", 4) != 4:
                raise ValueError(f"Only triangle primitives are supported: {mesh.get('name')}")
            attributes = primitive["attributes"]
            positions = glb.accessor(attributes["POSITION"]).astype(np.float64)
            joints = glb.accessor(attributes["JOINTS_0"]).astype(np.int32)
            weights = glb.accessor(attributes["WEIGHTS_0"]).astype(np.float64)
            weight_sums = weights.sum(axis=1, keepdims=True)
            weights = np.divide(weights, weight_sums, out=np.zeros_like(weights), where=weight_sums > 0)
            triangles = glb.accessor(primitive["indices"]).reshape(-1).astype(np.int32).reshape((-1, 3))
            name = node.get("name") or mesh.get("name") or f"mesh-{node['mesh']}-{primitive_index}"
            result.append(
                SkinnedPrimitive(
                    name=name,
                    node_index=node_index,
                    positions=positions,
                    joints=joints,
                    weights=weights,
                    triangles=triangles,
                    color=part_color(name),
                )
            )
    if not result:
        raise ValueError("The GLB contains no skinned triangle meshes")
    return result


def skin_vertices(
    primitive: SkinnedPrimitive,
    world: list[np.ndarray],
    joint_nodes: list[int],
    inverse_bind: np.ndarray,
) -> np.ndarray:
    mesh_world = world[primitive.node_index]
    mesh_inverse = np.linalg.inv(mesh_world)
    joint_matrices = np.stack(
        [mesh_inverse @ world[node_index] @ inverse_bind[index] for index, node_index in enumerate(joint_nodes)]
    )
    positions = np.concatenate(
        (primitive.positions, np.ones((len(primitive.positions), 1), dtype=np.float64)),
        axis=1,
    )
    skinned = np.zeros_like(positions)
    vertex_indices = np.arange(len(positions))
    for influence in range(primitive.joints.shape[1]):
        matrices = joint_matrices[primitive.joints[:, influence]]
        transformed = np.einsum("vij,vj->vi", matrices, positions)
        skinned += transformed * primitive.weights[:, influence : influence + 1]
    world_vertices = (mesh_world @ skinned.T).T
    world_vertices[:, :3] /= np.maximum(world_vertices[:, 3:4], 1e-12)
    return world_vertices[:, :3]


def multiply_color(color: tuple[int, int, int], amount: float) -> tuple[int, int, int, int]:
    return (
        max(0, min(255, round(color[0] * amount))),
        max(0, min(255, round(color[1] * amount))),
        max(0, min(255, round(color[2] * amount))),
        255,
    )


def render_frame(
    posed: list[tuple[SkinnedPrimitive, np.ndarray]],
    hips: np.ndarray,
    scale: float,
    canvas: tuple[int, int],
    root_x: int,
    ground_y: int,
    supersample: int,
    ground: float | None = None,
) -> Image.Image:
    high_canvas = (canvas[0] * supersample, canvas[1] * supersample)
    image = Image.new("RGBA", high_canvas, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    if ground is None:
        ground = min(float(vertices[:, 1].min()) for _, vertices in posed)
    light = np.array((0.75, 0.58, -0.32), dtype=np.float64)
    light /= np.linalg.norm(light)
    triangles_to_draw: list[tuple[float, list[tuple[float, float]], tuple[int, int, int, int]]] = []

    for primitive, vertices in posed:
        projected = np.empty((len(vertices), 2), dtype=np.float64)
        # The game's source sprites face right and PlayerPresentation2D flips
        # them only when facing is negative. Project +Z toward screen-right so
        # this rendered source follows the same convention.
        projected[:, 0] = (root_x + (vertices[:, 2] - hips[2]) * scale) * supersample
        projected[:, 1] = (ground_y - (vertices[:, 1] - ground) * scale) * supersample
        for triangle in primitive.triangles:
            points_3d = vertices[triangle]
            edge_a = points_3d[1] - points_3d[0]
            edge_b = points_3d[2] - points_3d[0]
            normal = np.cross(edge_a, edge_b)
            normal_length = float(np.linalg.norm(normal))
            if normal_length < 1e-10:
                continue
            normal /= normal_length
            diffuse = abs(float(np.dot(normal, light)))
            shade = 0.57 + 0.43 * diffuse
            polygon = [(float(x), float(y)) for x, y in projected[triangle]]
            depth = float(points_3d[:, 0].mean())
            triangles_to_draw.append((depth, polygon, multiply_color(primitive.color, shade)))

    # The side camera sits on +X, so larger X is closer and is painted last.
    triangles_to_draw.sort(key=lambda item: item[0])
    for _, polygon, color in triangles_to_draw:
        draw.polygon(polygon, fill=color)

    resized = resize_rgba_premultiplied(image, canvas)
    # Lanczos can ring a few low-alpha pixels below an exactly aligned sole.
    # Keep the declared ground line authoritative after downsampling.
    pixels = np.asarray(resized, dtype=np.uint8).copy()
    pixels[ground_y + 1 :, :, :] = 0
    return Image.fromarray(pixels, "RGBA")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--glb", type=Path)
    parser.add_argument("--animation")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--supersample", type=int, default=2)
    parser.add_argument("--frame-prefix", default="walk")
    parser.add_argument("--scale", type=float)
    args = parser.parse_args()

    if args.fps <= 0:
        raise ValueError("--fps must be positive")
    if args.supersample <= 0:
        raise ValueError("--supersample must be positive")

    profile = json.loads(args.profile.read_text(encoding="utf-8"))
    source = profile["source"]
    registration = profile["registration"]
    profile_root = args.profile.parent
    glb_path = args.glb or profile_root / source["glb"]
    animation_name = args.animation or source["animation"]
    canvas = (registration["canvasWidth"], registration["canvasHeight"])
    root_x = registration["rootX"]
    ground_y = registration["groundY"]

    glb = Glb(glb_path)
    sampler = AnimationSampler(glb, animation_name)
    primitives = load_primitives(glb)
    skin_index = next(node["skin"] for node in glb.document["nodes"] if "mesh" in node and "skin" in node)
    skin = glb.document["skins"][skin_index]
    joint_nodes = skin["joints"]
    inverse_bind = inverse_bind_matrices(glb, skin_index)
    hips_index = sampler.index_by_name["hips"]
    # glTF loops commonly repeat the starting pose at the exact duration.
    # Sampling [0, duration) avoids a duplicate endpoint frame.
    frame_count = max(1, round(sampler.duration * args.fps))
    samples = []
    maximum_world_height = 0.0
    for index in range(frame_count):
        time = index / args.fps
        world = sampler.world_matrices(time)
        posed = [
            (primitive, skin_vertices(primitive, world, joint_nodes, inverse_bind))
            for primitive in primitives
        ]
        all_vertices = np.concatenate([vertices for _, vertices in posed], axis=0)
        world_height = float(all_vertices[:, 1].max() - all_vertices[:, 1].min())
        maximum_world_height = max(maximum_world_height, world_height)
        samples.append((time, world, posed))
    scale = args.scale or registration["targetForegroundHeight"] / max(maximum_world_height, 1e-8)
    if scale <= 0:
        raise ValueError("--scale must be positive")
    args.output.mkdir(parents=True, exist_ok=True)

    frames: list[Image.Image] = []
    metadata = {
        "source": str(glb_path),
        "animation": animation_name,
        "durationSeconds": sampler.duration,
        "fps": args.fps,
        "frameCount": frame_count,
        "canvas": list(canvas),
        "rootX": root_x,
        "groundY": ground_y,
        "supersample": args.supersample,
        "scale": scale,
        "scaleBasis": (
            "explicit override"
            if args.scale is not None
            else "maximum posed mesh height across the cycle"
        ),
        "maximumWorldHeight": maximum_world_height,
        "framePrefix": args.frame_prefix,
        "frames": [],
    }

    for index, (time, world, posed) in enumerate(samples):
        hips = world[hips_index][:3, 3]
        frame = render_frame(
            posed,
            hips,
            scale,
            canvas,
            root_x,
            ground_y,
            args.supersample,
        )
        path = args.output / f"{args.frame_prefix}-{index + 1:03d}.png"
        frame.save(path)
        frames.append(frame)
        metadata["frames"].append(
            {
                "file": path.name,
                "timeSeconds": time,
                "alphaBounds": list(frame.getchannel("A").getbbox() or ()),
            }
        )

    preview_frames = []
    preview_background = (32, 34, 40, 255)
    for frame in frames:
        preview = Image.new("RGBA", canvas, preview_background)
        preview.alpha_composite(frame)
        preview_frames.append(preview.convert("RGB"))
    preview_frames[0].save(
        args.output / f"{args.frame_prefix}-preview.gif",
        save_all=True,
        append_images=preview_frames[1:],
        duration=round(1000 / args.fps),
        loop=0,
        disposal=2,
    )
    (args.output / "render-metadata.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
