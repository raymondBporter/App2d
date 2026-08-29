#!/usr/bin/env python3
"""Rebuild the generated character and equipment assets required by App2d."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path


def run(repository: Path, description: str, *arguments: str) -> None:
    print(f"\n==> {description}", flush=True)
    subprocess.run(
        [sys.executable, *arguments],
        cwd=repository,
        check=True,
    )


def require_inputs(repository: Path) -> None:
    required = [
        repository
        / "Assets/Sources/third-party/kaykit-character-animations-1.1"
        / "Animations/gltf/Rig_Medium/Rig_Medium_General.glb",
        repository
        / "Assets/Sources/third-party/KayKit_FantasyWeaponsBits_1.0_FREE"
        / "Assets/gltf/sword_A.gltf",
        repository / "Assets/Content/characters/player/character.json",
    ]
    missing = [path.relative_to(repository) for path in required if not path.is_file()]
    if missing:
        formatted = "\n".join(f"  - {path}" for path in missing)
        raise FileNotFoundError(
            "The runtime asset build is missing committed source inputs:\n" + formatted
        )


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Build all generated art needed to run App2d from a clean clone."
    )
    parser.add_argument(
        "--skip-sparse",
        action="store_true",
        help=(
            "Build the full-canvas fallback assets only. The game remains playable, "
            "but it will not use the smaller independent sparse layer library."
        ),
    )
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    pipeline = repository / "tools/ArtPipeline"
    require_inputs(repository)

    run(
        repository,
        "Rendering directional character layers",
        str(pipeline / "render_directional_character_layers.py"),
        "--plan",
        str(pipeline / "character-direction-plan.json"),
    )
    run(
        repository,
        "Rendering right-hand equipment layers",
        str(pipeline / "render_loadout_equipment_layers.py"),
        "--plan",
        str(pipeline / "right-hand-weapon-batch-plan.json"),
    )
    run(
        repository,
        "Validating right-hand equipment layers",
        str(pipeline / "validate_equipment_batch.py"),
        "--plan",
        str(pipeline / "right-hand-weapon-batch-plan.json"),
    )
    run(
        repository,
        "Rendering the sword-and-shield fallback loadout",
        str(pipeline / "render_loadout_equipment_layers.py"),
        "--plan",
        str(pipeline / "loadout-render-plan.json"),
    )

    if not args.skip_sparse:
        sparse_plan = pipeline / "sparse-one-handed-production-plan.json"
        sparse_configuration = json.loads(sparse_plan.read_text(encoding="utf-8"))
        selected_profile = sparse_configuration["planning"]["selectedProfile"]
        planning_root = repository / sparse_configuration["planning"]["outputRoot"]
        run(
            repository,
            "Planning independent sparse character and equipment timelines",
            str(pipeline / "plan_sparse_timelines.py"),
            "--plan",
            str(sparse_plan),
        )
        run(
            repository,
            "Packing the shared sparse character/equipment library",
            str(pipeline / "build_sparse_layer_library.py"),
            "--plan",
            str(sparse_plan),
            "--timeline-set",
            str(
                planning_root
                / f"profiles/{selected_profile}/independent/timeline-set.json"
            ),
            "--replace",
        )

    print("\nRuntime assets are ready. Run: dotnet run --project App2d", flush=True)


if __name__ == "__main__":
    main()
