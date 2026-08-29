#!/usr/bin/env python3
"""Run deterministic registration checks on normalized walk frames."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from normalize_generated_sheet import helmet_anchor


def failed_motion_checks(review: dict) -> list[str]:
    failed = []
    for frame in review.get("frames", []):
        frame_number = frame.get("frame", "?")
        for name, value in frame.items():
            if name != "frame" and isinstance(value, bool) and not value:
                failed.append(f"motion frame {frame_number}: {name}")
    for name, value in review.get("cycle", {}).items():
        if isinstance(value, bool) and not value:
            failed.append(f"motion cycle: {name}")
    return failed


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--frames", type=Path, required=True)
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--report", type=Path, required=True)
    parser.add_argument("--motion-review", type=Path)
    args = parser.parse_args()

    profile = json.loads(args.profile.read_text(encoding="utf-8"))
    registration = profile["registration"]
    validation = profile["validation"]
    expected_size = (registration["canvasWidth"], registration["canvasHeight"])
    expected_ground = registration["groundY"]
    expected_anchor = registration["rootX"]
    expected_height = registration["targetForegroundHeight"]
    padding = validation["minimumCanvasPaddingPixels"]

    failures: list[str] = []
    results = []
    frame_paths = sorted(args.frames.glob("walk-??.png"))
    if len(frame_paths) != profile["frames"]["count"]:
        failures.append(f"expected {profile['frames']['count']} frames, found {len(frame_paths)}")

    anchors = []
    previous = None
    for path in frame_paths:
        image = Image.open(path).convert("RGBA")
        alpha = image.getchannel("A")
        bbox = alpha.getbbox()
        frame_failures = []
        if image.size != expected_size:
            frame_failures.append(f"canvas {image.size} != {expected_size}")
        if bbox is None:
            frame_failures.append("empty alpha")
            results.append({"file": path.name, "failures": frame_failures})
            failures.extend(f"{path.name}: {item}" for item in frame_failures)
            continue
        height = bbox[3] - bbox[1]
        anchor_x, anchor_y = helmet_anchor(np.asarray(image, dtype=np.uint8))
        anchors.append(anchor_x)
        if abs(bbox[3] - expected_ground) > validation["groundTolerancePixels"]:
            frame_failures.append(f"ground {bbox[3]} != {expected_ground}")
        if abs(height - expected_height) > validation["heightTolerancePixels"]:
            frame_failures.append(f"height {height} != {expected_height}")
        if bbox[0] < padding or bbox[2] > image.width - padding or bbox[1] < padding:
            frame_failures.append(f"foreground violates {padding}px canvas padding: {bbox}")
        rgba = np.asarray(image, dtype=np.int16)
        delta = None if previous is None else float(np.abs(rgba - previous).mean())
        previous = rgba
        results.append(
            {
                "file": path.name,
                "bounds": list(bbox),
                "helmetAnchor": [anchor_x, anchor_y],
                "meanAbsoluteDeltaFromPrevious": delta,
                "failures": frame_failures,
            }
        )
        failures.extend(f"{path.name}: {item}" for item in frame_failures)

    if anchors and max(anchors) - min(anchors) > validation["anchorTolerancePixels"]:
        failures.append(
            f"helmet anchor drift {max(anchors) - min(anchors):.2f}px exceeds "
            f"{validation['anchorTolerancePixels']}px"
        )

    motion_review = None
    if validation.get("requireMotionReview", False):
        if args.motion_review is None or not args.motion_review.exists():
            failures.append("required semantic motion review is missing")
        else:
            motion_review = json.loads(args.motion_review.read_text(encoding="utf-8"))
            if not motion_review.get("passed", False):
                failures.append("semantic motion review did not pass")
            failures.extend(failed_motion_checks(motion_review))

    report = {
        "passed": not failures,
        "failures": failures,
        "frames": results,
        "motionReview": motion_review,
    }
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    raise SystemExit(0 if not failures else 1)


if __name__ == "__main__":
    main()
