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
        "audio/music/soundtrack.json",
        "audio/music/crown-of-embers/manifest.json",
        "audio/music/crown-of-embers/world.ogg",
        "audio/music/crown-of-embers/theme.ogg",
        "audio/music/crown-of-embers/motion.ogg",
        "audio/music/crown-of-embers/battle.ogg",
        "audio/music/copper-circuit/manifest.json",
        "audio/music/copper-circuit/world.ogg",
        "audio/music/copper-circuit/theme.ogg",
        "audio/music/copper-circuit/rock.ogg",
        "audio/music/copper-circuit/overdrive.ogg",
        "levels/cavern/zones.json",
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
        "gameplay/player-spells.json",
        "audio/sfx/heal-charge.wav",
        "audio/sfx/heal-complete.wav",
        "audio/sfx/heal-cancel.wav",
        "effects/fireball/ember-energy.png",
        "environments/tilesets/rust-cyberpunk/tileset.json",
        "environments/tilesets/ink-medieval-ground/tileset.json",
        "environments/tilesets/ink-medieval-ground/ladder/top.png",
        "environments/tilesets/ink-medieval-ground/ladder/middle.png",
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
        run(repository, "Baking blue charged-gun effects and audio",
            str(pipeline / "build_gun_effects.py"), "--content-root", str(staging_root))
        run(repository, "Baking player spell audio",
            str(pipeline / "build_spell_audio.py"), "--content-root", str(staging_root))
        run(
            repository,
            "Importing the green dinosaur walk cycle",
            str(pipeline / "import_green_dinosaur.py"),
            "--content-root",
            str(staging_root),
        )
        write_manifest(staging_root)
        # Bake once on the authoring machine; contributors receive the results in Git.
        for relative in (
            "characters/player-sword", "characters/player-gun", "characters/player-unarmed",
            "characters/player-geometry.json", "characters/green-dinosaur",
            "effects/bullet", "effects/gun", "ui/hud/weapons", "ui/hud/gun-charge",
        ):
            source = staging_root / relative
            destination = static_root / relative
            if source.is_dir():
                if destination.exists():
                    shutil.rmtree(destination)
                shutil.copytree(source, destination)
            else:
                destination.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(source, destination)
        for pattern in ("gun-*.wav", "heal-*.wav"):
            for source in (staging_root / "audio/sfx").glob(pattern):
                shutil.copy2(source, static_root / "audio/sfx" / source.name)
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
