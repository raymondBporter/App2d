#!/usr/bin/env python3
"""Render every unique KayKit Rig_Medium animation into a transparent sprite library."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from concurrent.futures import ThreadPoolExecutor, as_completed
from dataclasses import dataclass
from pathlib import Path

import numpy as np

from render_mannequin_walk import inverse_bind_matrices, load_primitives, skin_vertices
from render_walk_guides import AnimationSampler, Glb


@dataclass(frozen=True)
class RenderJob:
    source: Path
    pack: str
    animation: str
    duration: float
    expected_frames: int
    output: Path


def safe_component(value: str) -> str:
    cleaned = re.sub(r'[<>:"/\\|?*]+', "-", value).strip(" .")
    return cleaned or "unnamed"


def animation_duration(glb: Glb, animation: dict) -> float:
    duration = 0.0
    for sampler in animation["samplers"]:
        times = glb.accessor(sampler["input"])
        duration = max(duration, float(times[-1, 0]))
    return duration


def canonical_scale(source: Path, profile: Path) -> float:
    glb = Glb(source)
    sampler = AnimationSampler(glb, "T-Pose")
    primitives = load_primitives(glb)
    skin_index = next(
        node["skin"] for node in glb.document["nodes"] if "mesh" in node and "skin" in node
    )
    skin = glb.document["skins"][skin_index]
    inverse_bind = inverse_bind_matrices(glb, skin_index)
    world = sampler.world_matrices(0.0)
    posed = [
        skin_vertices(primitive, world, skin["joints"], inverse_bind)
        for primitive in primitives
    ]
    vertices = np.concatenate(posed, axis=0)
    height = float(vertices[:, 1].max() - vertices[:, 1].min())
    settings = json.loads(profile.read_text(encoding="utf-8"))
    return settings["registration"]["targetForegroundHeight"] / max(height, 1e-8)


def completed(job: RenderJob, fps: int, supersample: int, scale: float) -> bool:
    metadata_path = job.output / "render-metadata.json"
    if not metadata_path.exists():
        return False
    try:
        metadata = json.loads(metadata_path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return False
    frames = list(job.output.glob("frame-*.png"))
    return (
        metadata.get("animation") == job.animation
        and metadata.get("fps") == fps
        and metadata.get("supersample") == supersample
        and abs(float(metadata.get("scale", 0)) - scale) < 1e-9
        and metadata.get("scaleBasis") == "explicit override"
        and metadata.get("frameCount") == job.expected_frames
        and len(frames) == job.expected_frames
    )


def render(
    job: RenderJob,
    profile: Path,
    fps: int,
    supersample: int,
    scale: float,
) -> tuple[RenderJob, str, str]:
    renderer = Path(__file__).with_name("render_mannequin_walk.py")
    command = [
        sys.executable,
        str(renderer),
        "--profile",
        str(profile),
        "--glb",
        str(job.source),
        "--animation",
        job.animation,
        "--output",
        str(job.output),
        "--fps",
        str(fps),
        "--supersample",
        str(supersample),
        "--frame-prefix",
        "frame",
        "--scale",
        repr(scale),
    ]
    result = subprocess.run(command, capture_output=True, text=True, check=False)
    if result.returncode != 0:
        details = (result.stderr or result.stdout).strip()
        return job, "failed", details
    return job, "completed", ""


def write_manifest(path: Path, manifest: dict) -> None:
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    temporary.replace(path)


def write_index(path: Path, manifest: dict) -> None:
    lines = [
        "# KayKit Rig_Medium animation library",
        "",
        f"{manifest['animationCount']} unique animations rendered at {manifest['fps']} FPS "
        f"with {manifest['supersample']}x supersampling.",
        "",
        "Each preview uses a dark background for visibility. The numbered PNG frames have native transparency.",
    ]
    current_pack = None
    for entry in manifest["animations"]:
        if entry["pack"] != current_pack:
            current_pack = entry["pack"]
            lines.extend(("", f"## {current_pack}", ""))
        preview = (
            Path(safe_component(entry["pack"]))
            / safe_component(entry["animation"])
            / "frame-preview.gif"
        ).as_posix()
        lines.append(
            f"- [{entry['animation']}]({preview}) — {entry['expectedFrames']} frames, "
            f"{entry['durationSeconds']:.2f}s"
        )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--fps", type=int, default=30)
    parser.add_argument("--supersample", type=int, default=2)
    parser.add_argument("--workers", type=int, default=4)
    parser.add_argument("--resume", action="store_true")
    args = parser.parse_args()

    if args.fps <= 0 or args.supersample <= 0 or args.workers <= 0:
        raise ValueError("fps, supersample, and workers must all be positive")
    sources = sorted(args.source.glob("Rig_Medium_*.glb"))
    if not sources:
        raise ValueError(f"No Rig_Medium GLBs found under {args.source}")
    if args.output.exists() and any(args.output.iterdir()) and not args.resume:
        raise ValueError(f"Output directory is not empty; pass --resume to continue: {args.output}")
    args.output.mkdir(parents=True, exist_ok=True)
    scale = canonical_scale(sources[0], args.profile)

    jobs: list[RenderJob] = []
    duplicate_animations = []
    seen: dict[str, Path] = {}
    for source in sources:
        glb = Glb(source)
        pack = source.stem.removeprefix("Rig_Medium_")
        for animation in glb.document.get("animations", []):
            name = animation.get("name") or "unnamed"
            if name in seen:
                duplicate_animations.append(
                    {
                        "animation": name,
                        "keptSource": str(seen[name]),
                        "skippedSource": str(source),
                    }
                )
                continue
            seen[name] = source
            duration = animation_duration(glb, animation)
            expected_frames = max(1, round(duration * args.fps))
            output = args.output / safe_component(pack) / safe_component(name)
            jobs.append(RenderJob(source, pack, name, duration, expected_frames, output))

    manifest = {
        "sourceDirectory": str(args.source),
        "fps": args.fps,
        "supersample": args.supersample,
        "workers": args.workers,
        "canonicalScale": scale,
        "canonicalScaleSource": f"{sources[0]} / T-Pose",
        "animationCount": len(jobs),
        "expectedFrameCount": sum(job.expected_frames for job in jobs),
        "duplicateAnimations": duplicate_animations,
        "animations": [
            {
                "pack": job.pack,
                "animation": job.animation,
                "source": str(job.source),
                "durationSeconds": job.duration,
                "expectedFrames": job.expected_frames,
                "output": str(job.output),
                "status": "pending",
            }
            for job in jobs
        ],
    }
    manifest_path = args.output / "library-manifest.json"
    entry_by_key = {
        (entry["pack"], entry["animation"]): entry for entry in manifest["animations"]
    }

    pending = []
    for job in jobs:
        entry = entry_by_key[(job.pack, job.animation)]
        if args.resume and completed(job, args.fps, args.supersample, scale):
            entry["status"] = "skipped-complete"
            print(f"SKIP {job.pack}/{job.animation}", flush=True)
        else:
            pending.append(job)
    write_manifest(manifest_path, manifest)

    completed_count = len(jobs) - len(pending)
    failed_count = 0
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        futures = {
            executor.submit(
                render,
                job,
                args.profile.resolve(),
                args.fps,
                args.supersample,
                scale,
            ): job
            for job in pending
        }
        for future in as_completed(futures):
            job, status, details = future.result()
            entry = entry_by_key[(job.pack, job.animation)]
            entry["status"] = status
            if details:
                entry["error"] = details
            if status == "completed":
                completed_count += 1
            else:
                failed_count += 1
            print(
                f"[{completed_count + failed_count}/{len(jobs)}] {status.upper()} "
                f"{job.pack}/{job.animation} ({job.expected_frames} frames)",
                flush=True,
            )
            write_manifest(manifest_path, manifest)

    manifest["completedCount"] = completed_count
    manifest["failedCount"] = failed_count
    write_manifest(manifest_path, manifest)
    write_index(args.output / "README.md", manifest)
    print(
        f"DONE animations={len(jobs)} completed={completed_count} failed={failed_count} "
        f"frames={manifest['expectedFrameCount']}",
        flush=True,
    )
    if failed_count:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
