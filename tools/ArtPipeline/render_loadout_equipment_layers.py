#!/usr/bin/env python3
"""Render independent equipment layers for a declared multi-item loadout."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from render_attached_weapon_layers import (
    decode_depth,
    facing_transform,
    load_weapon,
    render_weapon,
    transform_points,
)
from render_mannequin_walk import inverse_bind_matrices, load_primitives, skin_vertices
from render_walk_guides import AnimationSampler, Glb


def equipment_grip_matrix(grip: dict | None) -> np.ndarray:
    """Build one fixed equipment-to-hand transform in hand-local coordinates."""
    result = np.identity(4, dtype=np.float64)
    if not grip:
        return result

    result[:3, 3] = np.asarray(grip.get("translation", [0, 0, 0]), dtype=np.float64)
    scale = float(grip.get("scale", 1))
    if scale <= 0:
        raise ValueError("Equipment grip scale must be positive")
    degrees = float(grip.get("rotationDegrees", 0))
    if abs(degrees) < 1e-12:
        result[:3, :3] *= scale
        return result

    axis = np.asarray(grip["rotationAxis"], dtype=np.float64)
    length = float(np.linalg.norm(axis))
    if length < 1e-12:
        raise ValueError("Equipment grip rotationAxis must be non-zero")
    x, y, z = axis / length
    radians = np.deg2rad(degrees)
    cosine = float(np.cos(radians))
    sine = float(np.sin(radians))
    one_minus_cosine = 1.0 - cosine
    result[:3, :3] = np.asarray(
        (
            (
                cosine + x * x * one_minus_cosine,
                x * y * one_minus_cosine - z * sine,
                x * z * one_minus_cosine + y * sine,
            ),
            (
                y * x * one_minus_cosine + z * sine,
                cosine + y * y * one_minus_cosine,
                y * z * one_minus_cosine - x * sine,
            ),
            (
                z * x * one_minus_cosine - y * sine,
                z * y * one_minus_cosine + x * sine,
                cosine + z * z * one_minus_cosine,
            ),
        ),
        dtype=np.float64,
    )
    result[:3, :3] *= scale
    return result


def local_z_rotation_matrix(degrees: float) -> np.ndarray:
    """Roll equipment around its grip/forearm axis without changing front/back."""
    radians = np.deg2rad(degrees)
    cosine = float(np.cos(radians))
    sine = float(np.sin(radians))
    return np.asarray(
        (
            (cosine, -sine, 0.0, 0.0),
            (sine, cosine, 0.0, 0.0),
            (0.0, 0.0, 1.0, 0.0),
            (0.0, 0.0, 0.0, 1.0),
        ),
        dtype=np.float64,
    )


def composite_depth_layers(layers: list[tuple[Image.Image, Image.Image]]) -> Image.Image:
    colors = np.stack(
        [np.asarray(color.convert("RGBA"), dtype=np.float64) / 255.0 for color, _ in layers]
    )
    depths = np.stack([decode_depth(depth) for _, depth in layers])
    present = colors[:, :, :, 3] > 0
    order = np.argsort(np.where(present, depths, -1), axis=0)
    output = np.zeros_like(colors[0])
    for rank in range(len(layers)):
        indices = order[rank]
        selected = np.take_along_axis(
            colors,
            indices[None, :, :, None],
            axis=0,
        )[0]
        alpha = selected[:, :, 3:4]
        output_alpha = alpha + output[:, :, 3:4] * (1.0 - alpha)
        premultiplied = (
            selected[:, :, :3] * alpha
            + output[:, :, :3] * output[:, :, 3:4] * (1.0 - alpha)
        )
        output[:, :, :3] = np.divide(
            premultiplied,
            output_alpha,
            out=np.zeros_like(premultiplied),
            where=output_alpha > 1e-8,
        )
        output[:, :, 3:4] = output_alpha
    return Image.fromarray(np.rint(np.clip(output, 0, 1) * 255).astype(np.uint8), "RGBA")


def save_preview(frames: list[Image.Image], path: Path, fps: int) -> None:
    previews = []
    for frame in frames:
        preview = Image.new("RGBA", frame.size, (32, 34, 40, 255))
        preview.alpha_composite(frame)
        previews.append(preview.convert("RGB"))
    previews[0].save(
        path,
        save_all=True,
        append_images=previews[1:],
        duration=round(1000 / fps),
        loop=0,
        disposal=2,
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--only-equipment")
    parser.add_argument("--only-clip")
    args = parser.parse_args()
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    repository = Path(__file__).resolve().parents[2]
    character_root = repository / plan["characterContentRoot"]
    preview_root = repository / plan["previewRoot"]
    preview_root.mkdir(parents=True, exist_ok=True)
    preview_mode = plan.get("previewMode", "combined")
    if preview_mode not in ("combined", "none"):
        raise ValueError(f"Unknown previewMode: {preview_mode}")
    canvas = tuple(plan["canvas"])
    fps = plan["fps"]
    supersample = plan["supersample"]
    root_x = plan["rootX"]
    root_x_by_facing = plan.get("rootXByFacing", {})
    ground_y = plan["groundY"]
    scale = plan["scale"]
    equipment = [
        {
            **entry,
            "mesh": load_weapon(repository / entry["gltf"]),
            "output": repository / entry["contentRoot"],
            "gripMatrix": equipment_grip_matrix(entry.get("grip")),
        }
        for entry in plan["equipment"]
    ]
    if args.only_equipment and not any(
        entry["id"] == args.only_equipment for entry in equipment
    ):
        raise ValueError(f"Unknown --only-equipment value: {args.only_equipment}")
    if args.only_clip and not any(
        clip["id"] == args.only_clip for clip in plan["clips"]
    ):
        raise ValueError(f"Unknown --only-clip value: {args.only_clip}")
    metadata = {
        "plan": str(plan_path),
        "loadoutId": plan["loadoutId"],
        "equipment": [entry["id"] for entry in equipment],
        "clips": [],
    }

    for clip in plan["clips"]:
        if args.only_clip and clip["id"] != args.only_clip:
            continue
        clip_root_x_by_facing = clip.get("rootXByFacing", root_x_by_facing)
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
        hand_indices = {
            entry["id"]: sampler.index_by_name[entry["hand"]]
            for entry in equipment
        }
        frame_count = max(1, round(sampler.duration * fps))
        previews = {facing["id"]: [] for facing in plan["facings"]}

        for entry in equipment:
            for facing in plan["facings"]:
                root = entry["output"] / clip["id"] / facing["id"]
                (root / "color").mkdir(parents=True, exist_ok=True)
                (root / "depth").mkdir(parents=True, exist_ok=True)

        for frame_index in range(frame_count):
            world = sampler.world_matrices(
                frame_index / fps,
                mirror_sides=clip.get("mirrorPose", False),
            )
            posed = [
                (primitive, skin_vertices(primitive, world, joint_nodes, inverse_bind))
                for primitive in primitives
            ]
            all_vertices = np.concatenate([vertices for _, vertices in posed], axis=0)
            ground = float(all_vertices[:, 1].min())
            hips = world[hips_index][:3, 3]
            frame_name = f"frame-{frame_index + 1:04d}.png"
            for facing in plan["facings"]:
                facing_id = facing["id"]
                facing_root_x = clip_root_x_by_facing.get(facing_id, root_x)
                turn = facing_transform(hips, facing["yawDegrees"])
                facing_hips = transform_points(turn, hips.reshape(1, 3))[0]
                character_color_path = (
                    character_root / clip["id"] / "color" / facing_id / frame_name
                )
                character_depth_path = (
                    character_root / clip["id"] / "depth" / facing_id / frame_name
                )
                if not character_color_path.exists() or not character_depth_path.exists():
                    raise FileNotFoundError(
                        character_color_path
                        if not character_color_path.exists()
                        else character_depth_path
                    )
                layers = [
                    (
                        Image.open(character_color_path).convert("RGBA"),
                        Image.open(character_depth_path).convert("RGB"),
                    )
                ]
                for entry in equipment:
                    output = entry["output"] / clip["id"] / facing_id
                    if args.only_equipment and entry["id"] != args.only_equipment:
                        color = Image.open(output / "color" / frame_name).convert("RGBA")
                        depth = Image.open(output / "depth" / frame_name).convert("RGB")
                    else:
                        forearm_roll = entry.get(
                            "forearmRollDegreesByClip",
                            {},
                        ).get(clip["id"], 0)
                        attachment = (
                            turn
                            @ world[hand_indices[entry["id"]]]
                            @ entry["gripMatrix"]
                            @ local_z_rotation_matrix(forearm_roll)
                        )
                        color, depth, _ = render_weapon(
                            entry["mesh"],
                            attachment,
                            facing_hips,
                            ground,
                            scale,
                            canvas,
                            facing_root_x,
                            ground_y,
                            supersample,
                        )
                        color.save(output / "color" / frame_name)
                        depth.save(output / "depth" / frame_name)
                    layers.append((color, depth))
                if preview_mode == "combined":
                    previews[facing_id].append(composite_depth_layers(layers))

        facing_metadata = []
        for facing in plan["facings"]:
            facing_id = facing["id"]
            facing_entry = {"id": facing_id}
            if preview_mode == "combined":
                preview_path = preview_root / f"{clip['id']}-{facing_id}.gif"
                save_preview(previews[facing_id], preview_path, fps)
                facing_entry["preview"] = str(preview_path)
            facing_metadata.append(facing_entry)
        metadata["clips"].append(
            {
                "id": clip["id"],
                "source": str(glb_path),
                "animation": clip["animation"],
                "frameCount": frame_count,
                "facings": facing_metadata,
            }
        )
        print(
            f"rendered {clip['id']}: {frame_count} frames x "
            f"{len(plan['facings'])} facings x {len(equipment)} items",
            flush=True,
        )

    (preview_root / "render-metadata.json").write_text(
        json.dumps(metadata, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
