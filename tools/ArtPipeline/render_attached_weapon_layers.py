#!/usr/bin/env python3
"""Render independent character-depth and hand-attached weapon color/depth layers."""

from __future__ import annotations

import argparse
import json
from dataclasses import dataclass
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from normalize_generated_sheet import resize_rgba_premultiplied
from render_mannequin_walk import (
    inverse_bind_matrices,
    load_primitives,
    render_frame,
    skin_vertices,
)
from render_walk_guides import (
    AnimationSampler,
    COMPONENT_DTYPES,
    Glb,
    TYPE_WIDTHS,
    transform_matrix,
)


DEPTH_MIN = -4.0
DEPTH_MAX = 4.0


class Gltf:
    """Small external-buffer GLTF reader for the rigid weapon files."""

    def __init__(self, path: Path) -> None:
        self.path = path
        self.document = json.loads(path.read_text(encoding="utf-8"))
        buffers = self.document.get("buffers", [])
        if len(buffers) != 1 or "uri" not in buffers[0]:
            raise ValueError(f"Expected one external GLTF buffer: {path}")
        self.binary = (path.parent / buffers[0]["uri"]).read_bytes()

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
            values = np.frombuffer(
                self.binary,
                dtype=dtype,
                count=count * width,
                offset=start,
            )
            return values.reshape(count, width).copy()
        return np.ndarray(
            shape=(count, width),
            dtype=dtype,
            buffer=self.binary,
            offset=start,
            strides=(stride, dtype.itemsize),
        ).copy()


@dataclass(frozen=True)
class WeaponMesh:
    positions: np.ndarray
    triangles: np.ndarray
    triangle_colors: np.ndarray
    local_matrix: np.ndarray


def load_weapon(path: Path) -> WeaponMesh:
    gltf = Gltf(path)
    scene = gltf.document["scenes"][gltf.document.get("scene", 0)]
    if len(scene["nodes"]) != 1:
        raise ValueError(f"Expected one weapon scene root: {path}")
    node = gltf.document["nodes"][scene["nodes"][0]]
    mesh = gltf.document["meshes"][node["mesh"]]
    if len(mesh["primitives"]) != 1:
        raise ValueError(f"Expected one weapon primitive: {path}")
    primitive = mesh["primitives"][0]
    attributes = primitive["attributes"]
    positions = gltf.accessor(attributes["POSITION"]).astype(np.float64)
    texcoords = gltf.accessor(attributes["TEXCOORD_0"]).astype(np.float64)
    triangles = gltf.accessor(primitive["indices"]).reshape(-1).astype(np.int32)
    triangles = triangles.reshape((-1, 3))

    material = gltf.document["materials"][primitive["material"]]
    texture_index = material["pbrMetallicRoughness"]["baseColorTexture"]["index"]
    image_index = gltf.document["textures"][texture_index]["source"]
    texture_path = path.parent / gltf.document["images"][image_index]["uri"]
    texture = np.asarray(Image.open(texture_path).convert("RGB"), dtype=np.uint8)
    height, width = texture.shape[:2]
    triangle_uv = texcoords[triangles].mean(axis=1)
    sample_x = np.clip(np.rint(triangle_uv[:, 0] * (width - 1)), 0, width - 1).astype(int)
    sample_y = np.clip(np.rint(triangle_uv[:, 1] * (height - 1)), 0, height - 1).astype(int)
    triangle_colors = texture[sample_y, sample_x]

    if "matrix" in node:
        local_matrix = np.asarray(node["matrix"], dtype=np.float64).reshape(4, 4).T
    else:
        local_matrix = transform_matrix(
            np.asarray(node.get("translation", [0, 0, 0]), dtype=np.float64),
            np.asarray(node.get("rotation", [0, 0, 0, 1]), dtype=np.float64),
            np.asarray(node.get("scale", [1, 1, 1]), dtype=np.float64),
        )
    return WeaponMesh(positions, triangles, triangle_colors, local_matrix)


def transform_points(matrix: np.ndarray, positions: np.ndarray) -> np.ndarray:
    homogeneous = np.concatenate(
        (positions, np.ones((len(positions), 1), dtype=np.float64)),
        axis=1,
    )
    transformed = (matrix @ homogeneous.T).T
    transformed[:, :3] /= np.maximum(transformed[:, 3:4], 1e-12)
    return transformed[:, :3]


def facing_transform(hips: np.ndarray, yaw_degrees: float) -> np.ndarray:
    radians = np.deg2rad(yaw_degrees)
    cosine = float(np.cos(radians))
    sine = float(np.sin(radians))
    rotation = np.asarray(
        (
            (cosine, 0.0, sine, 0.0),
            (0.0, 1.0, 0.0, 0.0),
            (-sine, 0.0, cosine, 0.0),
            (0.0, 0.0, 0.0, 1.0),
        ),
        dtype=np.float64,
    )
    to_origin = np.identity(4, dtype=np.float64)
    to_origin[0, 3] = -hips[0]
    to_origin[2, 3] = -hips[2]
    from_origin = np.identity(4, dtype=np.float64)
    from_origin[0, 3] = hips[0]
    from_origin[2, 3] = hips[2]
    return from_origin @ rotation @ to_origin


def project(
    vertices: np.ndarray,
    hips: np.ndarray,
    ground: float,
    scale: float,
    root_x: int,
    ground_y: int,
    supersample: int,
) -> np.ndarray:
    result = np.empty((len(vertices), 2), dtype=np.float64)
    result[:, 0] = (
        root_x + (vertices[:, 2] - hips[2]) * scale
    ) * supersample
    result[:, 1] = (
        ground_y - (vertices[:, 1] - ground) * scale
    ) * supersample
    return result


def encode_depth(depth: float) -> tuple[int, int, int]:
    normalized = np.clip((depth - DEPTH_MIN) / (DEPTH_MAX - DEPTH_MIN), 0.0, 1.0)
    value = int(round(float(normalized) * 65535.0))
    return value >> 8, value & 255, 0


def decode_depth(image: Image.Image) -> np.ndarray:
    pixels = np.asarray(image.convert("RGB"), dtype=np.uint16)
    return (pixels[:, :, 0] << 8) | pixels[:, :, 1]


def downsample_depth(image: Image.Image, canvas: tuple[int, int], supersample: int) -> Image.Image:
    if supersample == 1:
        return image
    depth = decode_depth(image)
    depth = depth.reshape(
        canvas[1], supersample, canvas[0], supersample
    ).max(axis=(1, 3))
    pixels = np.zeros((canvas[1], canvas[0], 3), dtype=np.uint8)
    pixels[:, :, 0] = depth >> 8
    pixels[:, :, 1] = depth & 255
    return Image.fromarray(pixels, "RGB")


def render_character_depth(
    posed,
    hips: np.ndarray,
    ground: float,
    scale: float,
    canvas: tuple[int, int],
    root_x: int,
    ground_y: int,
    supersample: int,
) -> Image.Image:
    high_size = (canvas[0] * supersample, canvas[1] * supersample)
    image = Image.new("RGB", high_size, (0, 0, 0))
    draw = ImageDraw.Draw(image)
    triangles = []
    for primitive, vertices in posed:
        projected = project(
            vertices,
            hips,
            ground,
            scale,
            root_x,
            ground_y,
            supersample,
        )
        for triangle in primitive.triangles:
            points = vertices[triangle]
            depth = float(points[:, 0].mean())
            polygon = [(float(x), float(y)) for x, y in projected[triangle]]
            triangles.append((depth, polygon))
    triangles.sort(key=lambda item: item[0])
    for depth, polygon in triangles:
        draw.polygon(polygon, fill=encode_depth(depth))
    return downsample_depth(image, canvas, supersample)


def render_weapon(
    mesh: WeaponMesh,
    weapon_world: np.ndarray,
    hips: np.ndarray,
    ground: float,
    scale: float,
    canvas: tuple[int, int],
    root_x: int,
    ground_y: int,
    supersample: int,
) -> tuple[Image.Image, Image.Image, tuple[float, float]]:
    vertices = transform_points(weapon_world @ mesh.local_matrix, mesh.positions)
    projected = project(
        vertices,
        hips,
        ground,
        scale,
        root_x,
        ground_y,
        supersample,
    )
    high_size = (canvas[0] * supersample, canvas[1] * supersample)
    color_image = Image.new("RGBA", high_size, (0, 0, 0, 0))
    depth_image = Image.new("RGB", high_size, (0, 0, 0))
    color_draw = ImageDraw.Draw(color_image)
    depth_draw = ImageDraw.Draw(depth_image)
    light = np.asarray((0.75, 0.58, -0.32), dtype=np.float64)
    light /= np.linalg.norm(light)
    triangles = []
    for index, triangle in enumerate(mesh.triangles):
        points = vertices[triangle]
        normal = np.cross(points[1] - points[0], points[2] - points[0])
        length = float(np.linalg.norm(normal))
        if length < 1e-10:
            continue
        normal /= length
        shade = 0.68 + 0.32 * abs(float(np.dot(normal, light)))
        base = mesh.triangle_colors[index]
        color = tuple(int(np.clip(round(channel * shade), 0, 255)) for channel in base)
        depth = float(points[:, 0].mean())
        polygon = [(float(x), float(y)) for x, y in projected[triangle]]
        triangles.append((depth, polygon, color))
    triangles.sort(key=lambda item: item[0])
    for depth, polygon, color in triangles:
        color_draw.polygon(polygon, fill=(*color, 255))
        depth_draw.polygon(polygon, fill=encode_depth(depth))
    color = resize_rgba_premultiplied(color_image, canvas)
    depth = downsample_depth(depth_image, canvas, supersample)
    return color, depth, (float(vertices[:, 0].min()), float(vertices[:, 0].max()))


def composite_layers(
    character_color: Image.Image,
    character_depth: Image.Image,
    weapon_color: Image.Image,
    weapon_depth: Image.Image,
) -> Image.Image:
    character = np.asarray(character_color.convert("RGBA"), dtype=np.float64) / 255.0
    weapon = np.asarray(weapon_color.convert("RGBA"), dtype=np.float64) / 255.0
    character_z = decode_depth(character_depth)
    weapon_z = decode_depth(weapon_depth)
    character_present = character[:, :, 3] > 0
    weapon_present = weapon[:, :, 3] > 0
    weapon_front = weapon_present & (
        ~character_present | (weapon_z > character_z)
    )
    front = np.where(weapon_front[:, :, None], weapon, character)
    back = np.where(weapon_front[:, :, None], character, weapon)
    front_alpha = front[:, :, 3:4]
    back_alpha = back[:, :, 3:4]
    output_alpha = front_alpha + back_alpha * (1.0 - front_alpha)
    premultiplied = (
        front[:, :, :3] * front_alpha +
        back[:, :, :3] * back_alpha * (1.0 - front_alpha)
    )
    output_rgb = np.divide(
        premultiplied,
        output_alpha,
        out=np.zeros_like(premultiplied),
        where=output_alpha > 1e-8,
    )
    output = np.concatenate((output_rgb, output_alpha), axis=2)
    return Image.fromarray(np.rint(np.clip(output, 0, 1) * 255).astype(np.uint8), "RGBA")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    args = parser.parse_args()
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    repository = Path(__file__).resolve().parents[2]
    canvas = tuple(plan["canvas"])
    root_x = plan["rootX"]
    ground_y = plan["groundY"]
    scale = plan["scale"]
    fps = plan["fps"]
    supersample = plan["supersample"]
    hand = plan["hand"]
    facings = plan["facings"]
    weapon_id = plan["weaponId"]
    weapon_path = repository / plan["weaponGltf"]
    weapon = load_weapon(weapon_path)
    character_root = repository / plan["characterContentRoot"]
    weapon_root = repository / plan["weaponContentRoot"]
    preview_root = repository / plan["previewRoot"]
    preview_root.mkdir(parents=True, exist_ok=True)

    summary = {
        "plan": str(plan_path),
        "weapon": str(weapon_path),
        "hand": hand,
        "facingPolicy": plan["facingPolicy"],
        "depthRange": [DEPTH_MIN, DEPTH_MAX],
        "clips": [],
    }
    for clip in plan["clips"]:
        animation_id = clip["id"]
        glb_path = repository / clip["glb"]
        glb = Glb(glb_path)
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
        hand_index = sampler.index_by_name[hand]
        frame_count = max(1, round(sampler.duration * fps))
        color_directory = character_root / animation_id
        render_outputs = {}
        for facing in facings:
            facing_id = facing["id"]
            character_color_directory = color_directory / "color" / facing_id
            character_depth_directory = color_directory / "depth" / facing_id
            weapon_facing_root = weapon_root / animation_id / facing_id
            weapon_color_directory = weapon_facing_root / "color"
            weapon_depth_directory = weapon_facing_root / "depth"
            for directory in (
                character_color_directory,
                character_depth_directory,
                weapon_color_directory,
                weapon_depth_directory,
            ):
                directory.mkdir(parents=True, exist_ok=True)
            render_outputs[facing_id] = {
                "characterColor": character_color_directory,
                "characterDepth": character_depth_directory,
                "weaponColor": weapon_color_directory,
                "weaponDepth": weapon_depth_directory,
                "previewFrames": [],
                "weaponDepthBounds": [float("inf"), float("-inf")],
            }

        for index in range(frame_count):
            time = index / fps
            world = sampler.world_matrices(time)
            posed = [
                (primitive, skin_vertices(primitive, world, joint_nodes, inverse_bind))
                for primitive in primitives
            ]
            all_character_vertices = np.concatenate(
                [vertices for _, vertices in posed],
                axis=0,
            )
            ground = float(all_character_vertices[:, 1].min())
            hips = world[hips_index][:3, 3]
            frame_name = f"frame-{index + 1:04d}.png"
            for facing in facings:
                facing_id = facing["id"]
                output = render_outputs[facing_id]
                turn = facing_transform(hips, facing["yawDegrees"])
                facing_posed = [
                    (primitive, transform_points(turn, vertices))
                    for primitive, vertices in posed
                ]
                facing_hips = transform_points(turn, hips.reshape(1, 3))[0]
                character_color = render_frame(
                    facing_posed,
                    facing_hips,
                    scale,
                    canvas,
                    root_x,
                    ground_y,
                    supersample,
                )
                character_depth = render_character_depth(
                    facing_posed,
                    facing_hips,
                    ground,
                    scale,
                    canvas,
                    root_x,
                    ground_y,
                    supersample,
                )
                weapon_color, weapon_depth, weapon_bounds = render_weapon(
                    weapon,
                    turn @ world[hand_index],
                    facing_hips,
                    ground,
                    scale,
                    canvas,
                    root_x,
                    ground_y,
                    supersample,
                )
                output["weaponDepthBounds"][0] = min(
                    output["weaponDepthBounds"][0], weapon_bounds[0]
                )
                output["weaponDepthBounds"][1] = max(
                    output["weaponDepthBounds"][1], weapon_bounds[1]
                )
                character_color.save(output["characterColor"] / frame_name)
                character_depth.save(output["characterDepth"] / frame_name)
                weapon_color.save(output["weaponColor"] / frame_name)
                weapon_depth.save(output["weaponDepth"] / frame_name)
                composite = composite_layers(
                    character_color,
                    character_depth,
                    weapon_color,
                    weapon_depth,
                )
                preview = Image.new("RGBA", canvas, (32, 34, 40, 255))
                preview.alpha_composite(composite)
                output["previewFrames"].append(preview.convert("RGB"))

        facing_metadata = []
        for facing in facings:
            facing_id = facing["id"]
            output = render_outputs[facing_id]
            preview_frames = output["previewFrames"]
            preview_path = preview_root / f"{weapon_id}-{animation_id}-{facing_id}.gif"
            preview_frames[0].save(
                preview_path,
                save_all=True,
                append_images=preview_frames[1:],
                duration=round(1000 / fps),
                loop=0,
                disposal=2,
            )
            facing_metadata.append(
                {
                    "id": facing_id,
                    "yawDegrees": facing["yawDegrees"],
                    "weaponWorldDepthBounds": output["weaponDepthBounds"],
                    "preview": str(preview_path),
                }
            )
        summary["clips"].append(
            {
                "id": animation_id,
                "sourceAnimation": clip["animation"],
                "frameCount": frame_count,
                "facings": facing_metadata,
            }
        )
        print(f"rendered {animation_id}: {frame_count} frames x {len(facings)} facings")
    (preview_root / "render-metadata.json").write_text(
        json.dumps(summary, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
