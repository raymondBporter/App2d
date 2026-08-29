#!/usr/bin/env python3
"""Render true left/right character color and depth for selected animations."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageChops

from render_attached_weapon_layers import (
    facing_transform,
    render_character_depth,
    transform_points,
)
from render_mannequin_walk import (
    inverse_bind_matrices,
    load_primitives,
    render_frame,
    skin_vertices,
)
from render_walk_guides import AnimationSampler, Glb


def save_preview(frames: list[Image.Image], path: Path, fps: int) -> None:
    background_frames = []
    for frame in frames:
        preview = Image.new("RGBA", frame.size, (32, 34, 40, 255))
        preview.alpha_composite(frame)
        background_frames.append(preview.convert("RGB"))
    background_frames[0].save(
        path,
        save_all=True,
        append_images=background_frames[1:],
        duration=round(1000 / fps),
        loop=0,
        disposal=2,
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--only-clip")
    args = parser.parse_args()
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    repository = Path(__file__).resolve().parents[2]
    content_root = repository / plan["contentRoot"]
    preview_root = repository / plan["previewRoot"]
    preview_root.mkdir(parents=True, exist_ok=True)
    canvas = tuple(plan["canvas"])
    fps = plan["fps"]
    supersample = plan["supersample"]
    root_x = plan["rootX"]
    root_x_by_facing = plan.get("rootXByFacing", {})
    replace_canonical = plan.get("replaceCanonical", False)
    ground_y = plan["groundY"]
    scale = plan["scale"]
    metadata = {"plan": str(plan_path), "clips": []}

    if args.only_clip and not any(
        clip["id"] == args.only_clip for clip in plan["clips"]
    ):
        raise ValueError(f"Unknown --only-clip value: {args.only_clip}")

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
        frame_count = max(1, round(sampler.duration * fps))
        animation_root = content_root / clip["id"]
        animation_root.mkdir(parents=True, exist_ok=True)
        outputs = {}
        for facing in plan["facings"]:
            color_root = animation_root / "color" / facing["id"]
            depth_root = animation_root / "depth" / facing["id"]
            color_root.mkdir(parents=True, exist_ok=True)
            depth_root.mkdir(parents=True, exist_ok=True)
            outputs[facing["id"]] = {
                "color": color_root,
                "depth": depth_root,
                "preview": [],
            }

        for index in range(frame_count):
            world = sampler.world_matrices(
                index / fps,
                mirror_sides=clip.get("mirrorPose", False),
            )
            posed = [
                (primitive, skin_vertices(primitive, world, joint_nodes, inverse_bind))
                for primitive in primitives
            ]
            all_vertices = np.concatenate([vertices for _, vertices in posed], axis=0)
            ground = float(all_vertices[:, 1].min())
            hips = world[hips_index][:3, 3]
            frame_name = f"frame-{index + 1:04d}.png"
            for facing in plan["facings"]:
                facing_id = facing["id"]
                facing_root_x = clip_root_x_by_facing.get(facing_id, root_x)
                turn = facing_transform(hips, facing["yawDegrees"])
                facing_posed = [
                    (primitive, transform_points(turn, vertices))
                    for primitive, vertices in posed
                ]
                facing_hips = transform_points(turn, hips.reshape(1, 3))[0]
                color = render_frame(
                    facing_posed,
                    facing_hips,
                    scale,
                    canvas,
                    facing_root_x,
                    ground_y,
                    supersample,
                )
                depth = render_character_depth(
                    facing_posed,
                    facing_hips,
                    ground,
                    scale,
                    canvas,
                    facing_root_x,
                    ground_y,
                    supersample,
                )
                color.save(outputs[facing_id]["color"] / frame_name)
                depth.save(outputs[facing_id]["depth"] / frame_name)
                outputs[facing_id]["preview"].append(color)

                if facing_id == "right":
                    canonical_path = animation_root / frame_name
                    if canonical_path.exists():
                        existing = Image.open(canonical_path).convert("RGBA")
                        if (
                            not replace_canonical
                            and ImageChops.difference(existing, color).getbbox() is not None
                        ):
                            raise ValueError(
                                f"Canonical right render changed for {clip['id']}/{frame_name}"
                            )
                    if replace_canonical or not canonical_path.exists():
                        color.save(canonical_path)

        facing_metadata = []
        for facing in plan["facings"]:
            facing_id = facing["id"]
            preview_path = preview_root / f"{clip['id']}-{facing_id}.gif"
            save_preview(outputs[facing_id]["preview"], preview_path, fps)
            facing_metadata.append({"id": facing_id, "preview": str(preview_path)})
        metadata["clips"].append(
            {
                "id": clip["id"],
                "source": str(glb_path),
                "animation": clip["animation"],
                "frameCount": frame_count,
                "facings": facing_metadata,
            }
        )
        print(f"rendered {clip['id']}: {frame_count} frames x 2 facings", flush=True)

    (preview_root / "render-metadata.json").write_text(
        json.dumps(metadata, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
