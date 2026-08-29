#!/usr/bin/env python3
"""Build one character+equipment preview per independently rendered item."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image

from render_loadout_equipment_layers import composite_depth_layers, save_preview


def frame_paths(root: Path) -> list[Path]:
    return sorted(root.glob("frame-*.png"))


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--plan", type=Path, required=True)
    args = parser.parse_args()
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    repository = Path(__file__).resolve().parents[2]
    character_root = repository / plan["characterContentRoot"]
    preview_root = repository / plan["previewRoot"]
    preview_root.mkdir(parents=True, exist_ok=True)
    manifest = {
        "plan": str(plan_path),
        "equipment": {},
    }

    for entry in plan["equipment"]:
        equipment_root = repository / entry["contentRoot"]
        equipment_manifest = {"clips": {}}
        for clip in plan["clips"]:
            clip_manifest = {"facings": {}}
            for facing in plan["facings"]:
                facing_id = facing["id"]
                character_colors = frame_paths(
                    character_root / clip["id"] / "color" / facing_id
                )
                character_depths = frame_paths(
                    character_root / clip["id"] / "depth" / facing_id
                )
                equipment_colors = frame_paths(
                    equipment_root / clip["id"] / facing_id / "color"
                )
                equipment_depths = frame_paths(
                    equipment_root / clip["id"] / facing_id / "depth"
                )
                counts = {
                    len(character_colors),
                    len(character_depths),
                    len(equipment_colors),
                    len(equipment_depths),
                }
                if len(counts) != 1 or not character_colors:
                    raise ValueError(
                        f"Mismatched frames for {entry['id']}/{clip['id']}/{facing_id}"
                    )

                frames = []
                for paths in zip(
                    character_colors,
                    character_depths,
                    equipment_colors,
                    equipment_depths,
                    strict=True,
                ):
                    frames.append(
                        composite_depth_layers(
                            [
                                (
                                    Image.open(paths[0]).convert("RGBA"),
                                    Image.open(paths[1]).convert("RGB"),
                                ),
                                (
                                    Image.open(paths[2]).convert("RGBA"),
                                    Image.open(paths[3]).convert("RGB"),
                                ),
                            ]
                        )
                    )
                preview_path = (
                    preview_root
                    / f"{entry['id']}-{clip['id']}-{facing_id}.gif"
                )
                save_preview(frames, preview_path, plan["fps"])
                clip_manifest["facings"][facing_id] = {
                    "frameCount": len(frames),
                    "preview": str(preview_path),
                }
            equipment_manifest["clips"][clip["id"]] = clip_manifest
        manifest["equipment"][entry["id"]] = equipment_manifest
        print(f"previewed {entry['id']}", flush=True)

    (preview_root / "preview-manifest.json").write_text(
        json.dumps(manifest, indent=2),
        encoding="utf-8",
    )


if __name__ == "__main__":
    main()
