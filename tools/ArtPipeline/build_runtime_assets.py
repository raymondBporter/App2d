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


def write_manifest(content_root: Path) -> None:
    required = (
        "audio/sfx/player-jump.wav",
        "characters/player-geometry.json",
        "characters/player-sword/character.json",
        "characters/player-gun/character.json",
        "characters/boiler-brute/character.json",
        "characters/shieldback/character.json",
        "effects/bullet/orange.png",
        "effects/fireball/ember-energy.png",
        "environments/tilesets/rust-cyberpunk/tileset.json",
        "environments/tilesets/dark-cave/tileset.json",
        "environments/tilesets/mossy-cavern/tileset.json",
        "ui/hud/weapons/sword.png",
        "ui/hud/weapons/gun.png",
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

    if staging_root.exists():
        shutil.rmtree(staging_root)
    staging_root.parent.mkdir(parents=True, exist_ok=True)

    try:
        shutil.copytree(static_root, staging_root)
        run(
            repository,
            "Importing baked stick-figure sword and gun sprites",
            str(pipeline / "import_stick_figure.py"),
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
