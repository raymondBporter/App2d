#!/usr/bin/env python3
"""Build ordinary sprite frames from already rendered independent loadout layers."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from render_loadout_equipment_layers import composite_depth_layers


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    args = parser.parse_args()
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    repository = Path(__file__).resolve().parents[2]
    character_root = repository / plan["characterContentRoot"]
    composite_root = repository / plan["compositeContentRoot"]
    equipment = [
        {
            "id": entry["id"],
            "root": repository / entry["contentRoot"],
        }
        for entry in plan["equipment"]
    ]
    manifest = {
        "id": plan["loadoutId"],
        "sourcePlan": str(plan_path),
        "equipment": [entry["id"] for entry in equipment],
        "animations": {},
    }

    for clip in plan["clips"]:
        animation_id = clip["id"]
        animation_manifest = {"facings": {}}
        for facing in plan["facings"]:
            facing_id = facing["id"]
            character_color_root = character_root / animation_id / "color" / facing_id
            character_depth_root = character_root / animation_id / "depth" / facing_id
            character_colors = sorted(character_color_root.glob("frame-*.png"))
            character_depths = sorted(character_depth_root.glob("frame-*.png"))
            if not character_colors or len(character_colors) != len(character_depths):
                raise ValueError(f"Invalid character layers: {animation_id}/{facing_id}")
            equipment_frames = []
            for entry in equipment:
                root = entry["root"] / animation_id / facing_id
                colors = sorted((root / "color").glob("frame-*.png"))
                depths = sorted((root / "depth").glob("frame-*.png"))
                if len(colors) != len(character_colors) or len(depths) != len(colors):
                    raise ValueError(
                        f"Invalid {entry['id']} layers: {animation_id}/{facing_id}"
                    )
                equipment_frames.append((colors, depths))

            output_root = composite_root / animation_id / facing_id
            output_root.mkdir(parents=True, exist_ok=True)
            for index, (character_color, character_depth) in enumerate(
                zip(character_colors, character_depths, strict=True)
            ):
                layers = [
                    (
                        Image.open(character_color).convert("RGBA"),
                        Image.open(character_depth).convert("RGB"),
                    )
                ]
                for colors, depths in equipment_frames:
                    layers.append(
                        (
                            Image.open(colors[index]).convert("RGBA"),
                            Image.open(depths[index]).convert("RGB"),
                        )
                    )
                composite_depth_layers(layers).save(output_root / character_color.name)
            animation_manifest["facings"][facing_id] = {
                "frameCount": len(character_colors),
                "directory": str(output_root),
            }
        manifest["animations"][animation_id] = animation_manifest
        print(f"composited {animation_id}", flush=True)

    manifest_path = composite_root.parent / "loadout.json"
    manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")


if __name__ == "__main__":
    main()
