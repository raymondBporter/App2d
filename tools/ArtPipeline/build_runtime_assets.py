#!/usr/bin/env python3
"""Build App2d's disposable runtime asset tree from durable inputs."""

from __future__ import annotations

import hashlib
import json
import shutil
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


MAAOT_PACKS = {
    "dark-cave.zip": "https://maaot.itch.io/2d-browncave-assets",
    "mossy-cavern.zip": "https://maaot.itch.io/mossy-cavern",
}


def check_downloaded_sources(assets: Path) -> None:
    """Fail early, with download instructions, when non-redistributable packs are absent."""
    maaot = assets / "Sources/third-party/maaot"
    missing = [name for name in MAAOT_PACKS if not (maaot / name).is_file()]
    if missing:
        lines = [f"  {name:<18} {MAAOT_PACKS[name]}" for name in missing]
        raise SystemExit(
            "Missing Maaot cave packs. Their license forbids redistribution, so download them once:\n"
            + "\n".join(lines)
            + f"\nSave them under {maaot} and run the build again."
        )


def write_manifest(content_root: Path) -> None:
    required = (
        "audio/sfx/player-jump.wav",
        "characters/player-geometry.json",
        "characters/player-sword/character.json",
        "characters/player-gun/character.json",
        "characters/player-unarmed/character.json",
        "characters/boiler-brute/character.json",
        "characters/shieldback/character.json",
        "characters/green-dinosaur/character.json",
        "effects/bullet/orange.png",
        "effects/gun/bolt.png",
        "effects/gun/charge.png",
        "effects/gun/ready/frame-0000.png",
        "effects/gun/ready/frame-0023.png",
        "effects/gun/flash.png",
        "effects/gun/trail-00.png",
        "effects/gun/trail-07.png",
        "ui/hud/gun-charge/frame-0060.png",
        "audio/sfx/gun-charge.wav",
        "audio/sfx/gun-fire.wav",
        "audio/sfx/gun-cancel.wav",
        "audio/sfx/gun-impact.wav",
        "effects/fireball/ember-energy.png",
        "environments/tilesets/rust-cyberpunk/tileset.json",
        "environments/tilesets/dark-cave/tileset.json",
        "environments/tilesets/mossy-cavern/tileset.json",
        "environments/tilesets/kenney-grassland/tileset.json",
        "environments/tilesets/kenney-grassland/ladder/top.png",
        "environments/tilesets/kenney-grassland/ladder/middle.png",
        "ui/hud/weapons/sword.png",
        "ui/hud/weapons/gun.png",
        "ui/hud/weapons/unarmed.png",
        "ui/hud/weapons/fireball.png",
    )
    missing = [path for path in required if not (content_root / path).is_file()]
    if missing:
        raise FileNotFoundError(
            "Runtime asset build is incomplete:\n  " + "\n  ".join(missing)
        )

    files = []
    for path in sorted(content_root.rglob("*")):
        if not path.is_file() or path.name == "content-manifest.json":
            continue
        relative_path = path.relative_to(content_root).as_posix()
        files.append(
            {
                "path": relative_path,
                "bytes": path.stat().st_size,
                "sha256": hashlib.sha256(path.read_bytes()).hexdigest(),
            }
        )

    manifest = {"version": 1, "files": files}
    (content_root / "content-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )


def replace_runtime_tree(runtime_root: Path, staging_root: Path, work_root: Path) -> None:
    backup_root = work_root / "runtime-assets-backup"
    if backup_root.exists():
        shutil.rmtree(backup_root)
    if runtime_root.exists():
        runtime_root.rename(backup_root)

    try:
        staging_root.rename(runtime_root)
    except BaseException:
        if backup_root.exists():
            backup_root.rename(runtime_root)
        raise
    else:
        if backup_root.exists():
            shutil.rmtree(backup_root)


def main() -> None:
    repository = Path(__file__).resolve().parents[2]
    pipeline = repository / "tools/ArtPipeline"
    assets = repository / "Assets"
    static_root = assets / "Static"
    runtime_root = assets / "Runtime"
    work_root = assets / "Work"
    staging_root = work_root / "runtime-assets-staging"

    check_downloaded_sources(assets)
    if staging_root.exists():
        shutil.rmtree(staging_root)
    staging_root.parent.mkdir(parents=True, exist_ok=True)

    try:
        shutil.copytree(static_root, staging_root)
        run(
            repository,
            "Importing baked stick-figure sword, gun, and unarmed sprites",
            str(pipeline / "import_stick_figure.py"),
            "--content-root",
            str(staging_root),
        )
        run(
            repository,
            "Importing authored Blender sword animations",
            str(pipeline / "import_blender_character.py"),
            "--content-root",
            str(staging_root),
        )
        run(
            repository,
            "Importing Maaot DarkCave and Mossy Cavern environments",
            str(pipeline / "import_maaot_caves.py"),
            "--content-root",
            str(staging_root),
        )
        run(
            repository,
            "Importing Kenney Pixel Platformer grassland environment",
            str(pipeline / "import_kenney_pixel_platformer.py"),
            "--content-root",
            str(staging_root),
        )
        run(repository, "Baking blue charged-gun effects and audio",
            str(pipeline / "build_gun_effects.py"), "--content-root", str(staging_root))
        run(
            repository,
            "Importing the green dinosaur walk cycle",
            str(pipeline / "import_green_dinosaur.py"),
            "--content-root",
            str(staging_root),
        )
        write_manifest(staging_root)
        replace_runtime_tree(runtime_root, staging_root, work_root)
    finally:
        if staging_root.exists():
            shutil.rmtree(staging_root)

    file_count = sum(path.is_file() for path in runtime_root.rglob("*"))
    print(
        f"\nRuntime assets are ready ({file_count} files). "
        "Run: dotnet run --project App2d",
        flush=True,
    )


if __name__ == "__main__":
    main()
