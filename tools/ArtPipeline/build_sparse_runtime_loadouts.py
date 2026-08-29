#!/usr/bin/env python3
"""Build complete shipping sparse packages for every right-hand equipment set."""

from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from concurrent.futures import ThreadPoolExecutor, as_completed
from pathlib import Path


def build(repository: Path, equipment_id: str) -> tuple[str, str]:
    plan = {
        "id": f"{equipment_id}-runtime",
        "outputRoot": f"Assets/Content/sparse-loadouts/{equipment_id}",
        "canvas": [512, 384],
        "rootX": 256,
        "groundY": 324,
        "sourceFps": 30,
        "targetFps": 30,
        "sampling": {
            "mode": "screen-space-motion",
            "analysisPlan": "tools/ArtPipeline/right-hand-weapon-batch-plan.json",
            "maxPixelsPerSample": 2.0,
            "minimumFramesPerSecond": 6.0,
        },
        "cropPadding": 2,
        "atlasSize": 1024,
        "atlasGutter": 2,
        "writeProofArtifacts": False,
        "characterContentRoot": "Assets/Content/characters/player/animations",
        "equipment": {
            "id": equipment_id,
            "contentRoot": f"Assets/Content/weapons/{equipment_id}/animations",
        },
        "facings": [{"id": "right"}, {"id": "left"}],
        "clips": [
            {"id": "idle", "loop": True, "targetFramesPerSecond": 30},
            {"id": "walk", "loop": True, "targetFramesPerSecond": 30},
            {"id": "crouch", "loop": True, "targetFramesPerSecond": 30},
            {"id": "jump-start", "loop": False, "targetFramesPerSecond": 30},
            {"id": "fall", "loop": True, "targetFramesPerSecond": 30},
            {"id": "land", "loop": False, "targetFramesPerSecond": 30},
            {"id": "hit-a", "loop": False, "targetFramesPerSecond": 30},
            {"id": "melee-chop", "loop": False, "targetFramesPerSecond": 30},
            {
                "id": "sword-attack",
                "loop": False,
                "durationSeconds": 0.24,
                "targetFramesPerSecond": 30,
            },
            {
                "id": "melee-stab",
                "loop": False,
                "targetFramesPerSecond": 30,
                "rootXByFacing": {"right": 210, "left": 302},
            },
            {
                "id": "magic-shot",
                "loop": False,
                "durationSeconds": 0.4,
                "targetFramesPerSecond": 30,
            },
            {"id": "shield-block", "loop": True, "targetFramesPerSecond": 30},
        ],
    }
    with tempfile.NamedTemporaryFile(
        mode="w",
        suffix=".json",
        encoding="utf-8",
        delete=False,
    ) as temporary:
        json.dump(plan, temporary)
        plan_path = Path(temporary.name)
    try:
        process = subprocess.run(
            [
                sys.executable,
                str(repository / "tools" / "ArtPipeline" / "build_sparse_layer_package.py"),
                "--plan",
                str(plan_path),
                "--replace",
            ],
            cwd=repository,
            check=True,
            capture_output=True,
            text=True,
        )
        return equipment_id, process.stdout
    finally:
        plan_path.unlink(missing_ok=True)


def main() -> None:
    repository = Path(__file__).resolve().parents[2]
    weapons = repository / "Assets" / "Content" / "weapons"
    equipment_ids = sorted(
        path.name for path in weapons.iterdir()
        if path.is_dir() and path.name.startswith("right-hand-")
    )
    if not equipment_ids:
        raise ValueError(f"No right-hand equipment directories found below {weapons}")

    reports = {}
    # The package builder validates and atomically replaces one output directory at
    # a time. Keep production writes serial so interrupted or overlapping native
    # image encoders cannot leave a mixed atlas set on Windows.
    with ThreadPoolExecutor(max_workers=1) as executor:
        pending = {
            executor.submit(build, repository, equipment_id): equipment_id
            for equipment_id in equipment_ids
        }
        for future in as_completed(pending):
            equipment_id, output = future.result()
            report = json.loads(output)
            reports[equipment_id] = report
            print(
                f"{equipment_id}: {report['counts']['atlasPages']} pages, "
                f"{report['bytes']['packageDisk']} bytes",
                flush=True,
            )

    library_root = repository / "Assets" / "Content" / "sparse-loadouts"
    library = {
        "format": "sparse-loadout-library-v1",
        "sampling": {
            "mode": "screen-space-motion",
            "maxPixelsPerSample": 2.0,
            "minimumFramesPerSecond": 6.0,
            "measurementFramesPerSecond": 30,
        },
        "defaultTargetFramesPerSecond": 30,
        "animationTargetFramesPerSecond": {
            animation_id: 30
            for animation_id in [
                "idle",
                "walk",
                "crouch",
                "jump-start",
                "fall",
                "land",
                "hit-a",
                "melee-chop",
                "sword-attack",
                "melee-stab",
                "magic-shot",
                "shield-block",
            ]
        },
        "animations": [
            "idle",
            "walk",
            "crouch",
            "jump-start",
            "fall",
            "land",
            "hit-a",
            "melee-chop",
            "sword-attack",
            "melee-stab",
            "magic-shot",
            "shield-block",
        ],
        "equipment": {
            equipment_id: f"{equipment_id}/package.json"
            for equipment_id in sorted(reports)
        },
        "totals": {
            "packages": len(reports),
            "layerReconstructions": sum(
                report["validation"]["layerReconstructions"]
                for report in reports.values()
            ),
            "composites": sum(
                report["validation"]["composites"] for report in reports.values()
            ),
            "atlasPages": sum(
                report["counts"]["atlasPages"] for report in reports.values()
            ),
            "packageDiskBytes": sum(
                report["bytes"]["packageDisk"] for report in reports.values()
            ),
            "atlasDecodedBytes": sum(
                report["bytes"]["atlasDecoded"] for report in reports.values()
            ),
            "fullSourceDiskBytes": sum(
                report["bytes"]["fullSourceDisk"] for report in reports.values()
            ),
            "fullSourceDecodedBytes": sum(
                report["bytes"]["fullSourceDecodedEstimate"]
                for report in reports.values()
            ),
        },
    }
    (library_root / "library.json").write_text(
        json.dumps(library, indent=2), encoding="utf-8"
    )


if __name__ == "__main__":
    main()
