#!/usr/bin/env python3
"""Print pixel-threshold/frame-count curves without rendering images."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from screen_space_motion import ScreenSpaceMotionAnalyzer, select_motion_indices


DEFAULT_THRESHOLDS = (1, 2, 3, 4, 5, 6, 8, 10, 12, 16, 24, 32)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--plan",
        default="tools/ArtPipeline/sparse-one-handed-production-plan.json",
        help="Sparse library plan whose clips and playback durations should be analyzed.",
    )
    parser.add_argument(
        "--thresholds",
        nargs="+",
        type=float,
        default=DEFAULT_THRESHOLDS,
        help="Screen-space pixel thresholds to test.",
    )
    parser.add_argument(
        "--minimum-fps",
        type=float,
        default=6.0,
        help="Maximum-hold floor applied at every threshold.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    repository = Path(__file__).resolve().parents[2]
    plan_path = (repository / args.plan).resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    character_manifest = json.loads(
        (repository / plan["character"]["manifest"]).read_text(encoding="utf-8")
    )
    character_root = repository / plan["character"]["contentRoot"]
    analysis_plan = repository / plan["sampling"]["analysisPlan"]
    analyzer = ScreenSpaceMotionAnalyzer(repository, analysis_plan)
    thresholds = tuple(dict.fromkeys(float(value) for value in args.thresholds))

    measurements: dict[str, tuple[int, float, bool, tuple[float, ...]]] = {}
    for clip_id in plan["clips"]:
        definition = character_manifest["animations"][clip_id]
        frame_count = len(
            list((character_root / clip_id / "color" / "right").glob("frame-*.png"))
        )
        if frame_count == 0:
            raise ValueError(f"No source frames found for {clip_id}")
        duration = (
            float(definition["durationSeconds"])
            if "durationSeconds" in definition
            else frame_count / float(definition["framesPerSecond"])
        )
        measurement = analyzer.measure(clip_id, frame_count)
        measurements[clip_id] = (
            frame_count,
            duration,
            bool(definition["loop"]),
            measurement.segment_pixels,
        )

    print(
        f"minimum FPS: {args.minimum_fps:g}; source poses: "
        f"{sum(value[0] for value in measurements.values())}"
    )
    print()
    header = ["animation", "source", *(f"{value:g}px" for value in thresholds)]
    print(" | ".join(header))
    print(" | ".join(["---", "---:", *("---:" for _ in thresholds)]))
    totals = [0] * len(thresholds)
    for clip_id, (frame_count, duration, loop, segments) in measurements.items():
        counts = []
        for index, threshold in enumerate(thresholds):
            count = len(
                select_motion_indices(
                    segments,
                    duration,
                    threshold,
                    args.minimum_fps,
                    loop,
                )
            )
            counts.append(count)
            totals[index] += count
        print(" | ".join([clip_id, str(frame_count), *(str(value) for value in counts)]))

    source_total = sum(value[0] for value in measurements.values())
    print(" | ".join(["TOTAL", str(source_total), *(str(value) for value in totals)]))
    reductions = [100.0 * (source_total - value) / source_total for value in totals]
    print(
        " | ".join(
            ["REDUCTION", "0%", *(f"{value:.1f}%" for value in reductions)]
        )
    )


if __name__ == "__main__":
    main()
