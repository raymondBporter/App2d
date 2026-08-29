#!/usr/bin/env python3
"""Validate independent equipment layers and reject canvas clipping."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    args = parser.parse_args()
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    repository = Path(__file__).resolve().parents[2]
    width, height = plan["canvas"]
    character_root = repository / plan["characterContentRoot"]
    character_bounds_cache = {}
    report = {"plan": str(plan_path), "equipment": {}}
    failed = False

    for entry in plan["equipment"]:
        issues = []
        frame_count = 0
        occupied_by_facing = {}
        equipment_bounds_by_clip_facing = {}
        root = repository / entry["contentRoot"]
        for clip in plan["clips"]:
            for facing in plan["facings"]:
                facing_id = facing["id"]
                color_root = root / clip["id"] / facing_id / "color"
                depth_root = root / clip["id"] / facing_id / "depth"
                colors = sorted(color_root.glob("frame-*.png"))
                depths = sorted(depth_root.glob("frame-*.png"))
                if not colors or len(colors) != len(depths):
                    issues.append(
                        f"{clip['id']}/{facing_id}: "
                        f"{len(colors)} color vs {len(depths)} depth frames"
                    )
                    continue
                frame_count += len(colors)
                for color_path in colors:
                    bounds = Image.open(color_path).convert("RGBA").getbbox()
                    if bounds is None:
                        issues.append(
                            f"{clip['id']}/{facing_id}/{color_path.name}: blank"
                        )
                    elif (
                        bounds[0] <= 0
                        or bounds[1] <= 0
                        or bounds[2] >= width
                        or bounds[3] >= height
                    ):
                        issues.append(
                            f"{clip['id']}/{facing_id}/{color_path.name}: "
                            f"touches canvas {bounds}"
                        )
                    if bounds:
                        key = f"{clip['id']}/{facing_id}"
                        previous_equipment = equipment_bounds_by_clip_facing.get(key)
                        equipment_bounds_by_clip_facing[key] = (
                            bounds
                            if previous_equipment is None
                            else (
                                min(previous_equipment[0], bounds[0]),
                                min(previous_equipment[1], bounds[1]),
                                max(previous_equipment[2], bounds[2]),
                                max(previous_equipment[3], bounds[3]),
                            )
                        )
                    character_path = (
                        character_root
                        / clip["id"]
                        / "color"
                        / facing_id
                        / color_path.name
                    )
                    character_key = (clip["id"], facing_id, color_path.name)
                    if character_key not in character_bounds_cache:
                        character_bounds_cache[character_key] = (
                            Image.open(character_path).convert("RGBA").getbbox()
                            if character_path.exists()
                            else None
                        )
                    character_bounds = character_bounds_cache[character_key]
                    frame_bounds = [item for item in (bounds, character_bounds) if item]
                    if frame_bounds:
                        combined = (
                            min(item[0] for item in frame_bounds),
                            min(item[1] for item in frame_bounds),
                            max(item[2] for item in frame_bounds),
                            max(item[3] for item in frame_bounds),
                        )
                        previous = occupied_by_facing.get(facing_id)
                        occupied_by_facing[facing_id] = combined if previous is None else (
                            min(previous[0], combined[0]),
                            min(previous[1], combined[1]),
                            max(previous[2], combined[2]),
                            max(previous[3], combined[3]),
                        )
        occupied_report = {
            facing_id: {
                "bounds": list(bounds),
                "margins": {
                    "left": bounds[0],
                    "top": bounds[1],
                    "right": width - bounds[2],
                    "bottom": height - bounds[3],
                },
                "width": bounds[2] - bounds[0],
                "height": bounds[3] - bounds[1],
            }
            for facing_id, bounds in occupied_by_facing.items()
        }
        report["equipment"][entry["id"]] = {
            "frameCount": frame_count,
            "status": "pass" if not issues else "fail",
            "issues": issues,
            "occupiedByFacing": occupied_report,
            "equipmentBoundsByClipFacing": {
                key: list(bounds)
                for key, bounds in equipment_bounds_by_clip_facing.items()
            },
        }
        failed |= bool(issues)
        print(
            f"{entry['id']}: "
            f"{'PASS' if not issues else f'FAIL ({len(issues)})'}",
            flush=True,
        )

    output = repository / plan["previewRoot"] / "validation-report.json"
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    if failed:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
