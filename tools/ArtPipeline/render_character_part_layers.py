#!/usr/bin/env python3
"""Render and sparsely pack the six native KayKit Rig_Medium body meshes."""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

from build_sparse_layer_package import (
    SparseFrame,
    create_sparse_frame,
    extract_frame,
    frame_manifest,
    image_equal,
    load_atlas_pages,
    pack_frames,
    reconstruct_layer,
    write_atlases,
)
from render_attached_weapon_layers import (
    decode_depth,
    facing_transform,
    render_character_depth,
    transform_points,
)
from render_loadout_equipment_layers import composite_depth_layers, save_preview
from render_mannequin_walk import (
    inverse_bind_matrices,
    load_primitives,
    render_frame,
    skin_vertices,
)
from render_walk_guides import AnimationSampler, Glb


NATIVE_PART_SUFFIXES = {
    "arm-left": "ArmLeft",
    "arm-right": "ArmRight",
    "body": "Body",
    "head": "Head",
    "leg-left": "LegLeft",
    "leg-right": "LegRight",
}


def classify_primitives(primitives) -> dict[str, object]:
    classified = {}
    unmatched = []
    for primitive in primitives:
        matches = [
            part_id
            for part_id, suffix in NATIVE_PART_SUFFIXES.items()
            if primitive.name.endswith(suffix)
        ]
        if len(matches) != 1:
            unmatched.append(primitive.name)
            continue
        part_id = matches[0]
        if part_id in classified:
            raise ValueError(f"KayKit part {part_id} has more than one mesh primitive")
        classified[part_id] = primitive
    missing = sorted(set(NATIVE_PART_SUFFIXES) - set(classified))
    if missing or unmatched:
        raise ValueError(
            f"KayKit native part classification failed; missing={missing}, "
            f"unmatched={unmatched}"
        )
    return classified


def difference_metrics(reference: Image.Image, composite: Image.Image) -> dict:
    first = np.asarray(reference.convert("RGBA"), dtype=np.int16)
    second = np.asarray(composite.convert("RGBA"), dtype=np.int16)
    delta = np.abs(first - second)
    changed = np.any(delta != 0, axis=2)
    reference_present = first[:, :, 3] > 0
    composite_present = second[:, :, 3] > 0
    return {
        "changedPixels": int(changed.sum()),
        "maxChannelDelta": int(delta.max()),
        "referenceVisiblePixels": int(reference_present.sum()),
        "compositeVisiblePixels": int(composite_present.sum()),
        "silhouetteDisagreementPixels": int(np.logical_xor(reference_present, composite_present).sum()),
    }


def save_part_sheet(
    output: Path,
    clip_id: str,
    facing_id: str,
    frame_name: str,
) -> None:
    cell_width = 256
    image_height = 192
    label_height = 24
    sheet = Image.new(
        "RGB",
        (cell_width * 3, (image_height + label_height) * 2),
        (24, 26, 31),
    )
    draw = ImageDraw.Draw(sheet)
    for index, part_id in enumerate(NATIVE_PART_SUFFIXES):
        with Image.open(
            output
            / "full-layers"
            / clip_id
            / facing_id
            / part_id
            / "color"
            / frame_name
        ) as source:
            part = source.convert("RGBA")
        preview = Image.new("RGBA", part.size, (32, 34, 40, 255))
        preview.alpha_composite(part)
        preview = preview.convert("RGB").resize(
            (cell_width, image_height), Image.Resampling.LANCZOS
        )
        column = index % 3
        row = index // 3
        x = column * cell_width
        y = row * (image_height + label_height)
        sheet.paste(preview, (x, y))
        draw.text((x + 8, y + image_height + 5), part_id, fill=(235, 237, 242))
    sheet.save(output / "previews" / f"{clip_id}-{facing_id}-native-parts.png")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument("--replace", action="store_true")
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    output = (repository / plan["outputRoot"]).resolve()
    allowed_root = (repository / "Assets" / "Work" / "art-pipeline").resolve()
    if allowed_root not in output.parents:
        raise ValueError(f"Output must be below {allowed_root}: {output}")
    if output.exists():
        if not args.replace:
            raise FileExistsError(f"Output exists; pass --replace to rebuild: {output}")
        shutil.rmtree(output)
    output.mkdir(parents=True)

    canvas = tuple(int(value) for value in plan["canvas"])
    fps = int(plan["fps"])
    supersample = int(plan["supersample"])
    root_x = int(plan["rootX"])
    ground_y = int(plan["groundY"])
    scale = float(plan["scale"])
    crop_padding = int(plan.get("cropPadding", 2))
    atlas_size = int(plan.get("atlasSize", 1024))
    atlas_gutter = int(plan.get("atlasGutter", 2))

    sparse_frames: list[SparseFrame] = []
    sparse_lookup: dict[str, SparseFrame] = {}
    animations = {}
    comparison_totals = {
        "frames": 0,
        "changedPixels": 0,
        "maxChannelDelta": 0,
        "silhouetteDisagreementPixels": 0,
    }

    for clip in plan["clips"]:
        clip_id = clip["id"]
        glb = Glb(repository / clip["glb"])
        sampler = AnimationSampler(glb, clip["animation"])
        primitives = load_primitives(glb)
        parts = classify_primitives(primitives)
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
        clip_manifest = {"loop": bool(clip.get("loop", True)), "facings": {}}

        for facing in plan["facings"]:
            facing_id = facing["id"]
            root = (
                int(clip.get("rootXByFacing", {}).get(facing_id, root_x)),
                ground_y,
            )
            samples = []
            preview_frames = []
            for frame_index in range(frame_count):
                world = sampler.world_matrices(
                    frame_index / fps,
                    mirror_sides=bool(clip.get("mirrorPose", False)),
                )
                posed = {
                    part_id: (
                        primitive,
                        skin_vertices(primitive, world, joint_nodes, inverse_bind),
                    )
                    for part_id, primitive in parts.items()
                }
                all_vertices = np.concatenate(
                    [vertices for _, vertices in posed.values()], axis=0
                )
                ground = float(all_vertices[:, 1].min())
                hips = world[hips_index][:3, 3]
                turn = facing_transform(hips, facing["yawDegrees"])
                facing_hips = transform_points(turn, hips.reshape(1, 3))[0]
                facing_posed = {
                    part_id: (primitive, transform_points(turn, vertices))
                    for part_id, (primitive, vertices) in posed.items()
                }
                reference = render_frame(
                    list(facing_posed.values()),
                    facing_hips,
                    scale,
                    canvas,
                    root[0],
                    root[1],
                    supersample,
                    ground,
                )
                full_layers = []
                sample_layers = {}
                frame_name = f"frame-{frame_index + 1:04d}.png"
                for part_id in NATIVE_PART_SUFFIXES:
                    part_posed = [facing_posed[part_id]]
                    color = render_frame(
                        part_posed,
                        facing_hips,
                        scale,
                        canvas,
                        root[0],
                        root[1],
                        supersample,
                        ground,
                    )
                    depth = render_character_depth(
                        part_posed,
                        facing_hips,
                        ground,
                        scale,
                        canvas,
                        root[0],
                        root[1],
                        supersample,
                    )
                    part_root = output / "full-layers" / clip_id / facing_id / part_id
                    color_path = part_root / "color" / frame_name
                    depth_path = part_root / "depth" / frame_name
                    color_path.parent.mkdir(parents=True, exist_ok=True)
                    depth_path.parent.mkdir(parents=True, exist_ok=True)
                    color.save(color_path)
                    depth.save(depth_path)
                    key = f"{clip_id}/{facing_id}/{frame_index:04d}/{part_id}"
                    sparse = create_sparse_frame(
                        key, color_path, depth_path, root, crop_padding
                    )
                    sparse_frames.append(sparse)
                    sparse_lookup[key] = sparse
                    sample_layers[part_id] = key
                    full_layers.append((color, depth))

                composite = composite_depth_layers(full_layers)
                metrics = difference_metrics(reference, composite)
                comparison_totals["frames"] += 1
                comparison_totals["changedPixels"] += metrics["changedPixels"]
                comparison_totals["maxChannelDelta"] = max(
                    comparison_totals["maxChannelDelta"], metrics["maxChannelDelta"]
                )
                comparison_totals["silhouetteDisagreementPixels"] += metrics[
                    "silhouetteDisagreementPixels"
                ]
                reference_root = output / "references" / clip_id / facing_id
                composite_root = output / "composites" / clip_id / facing_id
                reference_root.mkdir(parents=True, exist_ok=True)
                composite_root.mkdir(parents=True, exist_ok=True)
                reference.save(reference_root / frame_name)
                composite.save(composite_root / frame_name)
                preview_frames.append(composite)
                samples.append(
                    {
                        "sourceFrame": frame_index + 1,
                        "timeSeconds": frame_index / fps,
                        "durationSeconds": 1 / fps,
                        "layers": sample_layers,
                        "referenceDifference": metrics,
                    }
                )

            preview_root = output / "previews"
            preview_root.mkdir(parents=True, exist_ok=True)
            save_preview(
                preview_frames,
                preview_root / f"{clip_id}-{facing_id}.gif",
                fps,
            )
            save_part_sheet(output, clip_id, facing_id, "frame-0001.png")
            clip_manifest["facings"][facing_id] = {
                "root": list(root),
                "samples": samples,
            }
        animations[clip_id] = clip_manifest
        print(f"rendered native parts for {clip_id}: {frame_count} frames", flush=True)

    sparse_root = output / "sparse-package"
    page_count = pack_frames(sparse_frames, atlas_size, atlas_gutter)
    atlases = write_atlases(sparse_frames, page_count, atlas_size, sparse_root)
    pages = load_atlas_pages(sparse_root, atlases)
    for frame in sparse_frames:
        clip_id, facing_id, _, _ = frame.key.split("/")
        root = tuple(animations[clip_id]["facings"][facing_id]["root"])
        reconstructed_color, reconstructed_depth = reconstruct_layer(
            frame, pages, canvas, root
        )
        with Image.open(frame.source_color) as source:
            expected_color = source.convert("RGBA")
        with Image.open(frame.source_depth) as source:
            expected_depth = decode_depth(source)
        if not image_equal(reconstructed_color, expected_color):
            raise ValueError(f"Sparse color reconstruction mismatch: {frame.key}")
        if not np.array_equal(reconstructed_depth, expected_depth):
            raise ValueError(f"Sparse depth reconstruction mismatch: {frame.key}")

    for animation in animations.values():
        for facing in animation["facings"].values():
            for sample in facing["samples"]:
                sample["layers"] = {
                    part_id: frame_manifest(sparse_lookup[key])
                    for part_id, key in sample["layers"].items()
                }

    manifest = {
        "format": "sparse-rooted-layers-v1",
        "id": plan["id"],
        "equipment": "native-character-parts",
        "canvas": list(canvas),
        "sourceFramesPerSecond": fps,
        "targetFramesPerSecond": fps,
        "cropPadding": crop_padding,
        "atlasGutter": atlas_gutter,
        "nativeParts": list(NATIVE_PART_SUFFIXES),
        "atlases": atlases,
        "animations": animations,
    }
    (sparse_root / "package.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8"
    )
    report = {
        "status": "pass",
        "nativeParts": NATIVE_PART_SUFFIXES,
        "sparseLayerReconstructions": len(sparse_frames),
        "atlasPages": page_count,
        "referenceComparison": comparison_totals,
        "referencePixelExact": comparison_totals["changedPixels"] == 0,
        "referenceDifferenceReason": (
            "Native meshes are antialiased independently before depth composition; "
            "differences are expected at mesh and silhouette boundaries."
        ),
    }
    (output / "validation-report.json").write_text(
        json.dumps(report, indent=2), encoding="utf-8"
    )
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    main()
