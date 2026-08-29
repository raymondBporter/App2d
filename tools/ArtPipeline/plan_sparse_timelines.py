#!/usr/bin/env python3
"""Analyze master renders once and cheaply produce selectable sparse timelines."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

from sparse_timeline_planning import (
    build_curve_report,
    build_independent_layer_timelines,
    build_motion_analysis,
    build_timeline_from_analysis,
    recommend_for_budgets,
    validate_analysis,
)


def _write_json(path: Path, value: dict | list) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=Path, required=True)
    parser.add_argument(
        "--reanalyze",
        action="store_true",
        help="Discard the reusable numerical analysis and recompute it from master inputs.",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Print the complete machine-readable report instead of a concise summary.",
    )
    args = parser.parse_args()

    repository = Path(__file__).resolve().parents[2]
    plan_path = args.plan.resolve()
    plan = json.loads(plan_path.read_text(encoding="utf-8"))
    planning = plan["planning"]
    output_root = (repository / planning["outputRoot"]).resolve()
    allowed_root = (repository / "Assets" / "Work" / "art-pipeline").resolve()
    if allowed_root not in output_root.parents:
        raise ValueError(f"Planning output must be below {allowed_root}: {output_root}")
    analysis_path = output_root / "motion-analysis.json"

    if analysis_path.exists() and not args.reanalyze:
        analysis = json.loads(analysis_path.read_text(encoding="utf-8"))
        validate_analysis(repository, plan, analysis)
        analysis_reused = True
    else:
        analysis = build_motion_analysis(repository, plan)
        _write_json(analysis_path, analysis)
        analysis_reused = False

    profile_metrics = {}
    for profile_name, profile in planning["profiles"].items():
        timeline_path = output_root / "profiles" / profile_name / "timeline.json"
        analysis_reference = str(
            Path("..") / ".." / "motion-analysis.json"
        ).replace("\\", "/")
        timeline, metrics = build_timeline_from_analysis(
            plan,
            analysis,
            profile_name,
            profile,
            analysis_reference,
        )
        _write_json(timeline_path, timeline)
        profile_metrics[profile_name] = metrics

    selected_profile = planning["selectedProfile"]
    if selected_profile not in profile_metrics:
        raise ValueError(f"Unknown selected profile: {selected_profile}")
    independent_root = output_root / "profiles" / selected_profile / "independent"
    timeline_set, character_timeline, equipment_timelines, independent_metrics = (
        build_independent_layer_timelines(
            plan,
            analysis,
            selected_profile,
            planning["profiles"][selected_profile],
            "../../../motion-analysis.json",
        )
    )
    _write_json(independent_root / "character-timeline.json", character_timeline)
    for equipment_id, timeline in equipment_timelines.items():
        _write_json(
            independent_root / "equipment" / f"{equipment_id}-timeline.json",
            timeline,
        )
    _write_json(independent_root / "timeline-set.json", timeline_set)
    curve = build_curve_report(analysis, planning)
    report = {
        "format": "sparse-timeline-planning-report-v1",
        "id": plan["id"],
        "analysisReused": analysis_reused,
        "analysis": "motion-analysis.json",
        "selectedProfile": selected_profile,
        "selectedTimeline": f"profiles/{selected_profile}/timeline.json",
        "selectedIndependentTimelineSet": (
            f"profiles/{selected_profile}/independent/timeline-set.json"
        ),
        "independentLayers": independent_metrics,
        "profiles": profile_metrics,
        "curve": curve,
        "budgetRecommendations": recommend_for_budgets(curve, planning),
    }
    _write_json(output_root / "planning-report.json", report)
    if args.json:
        print(json.dumps(report, indent=2), flush=True)
    else:
        state = "reused" if analysis_reused else "rebuilt"
        print(f"motion analysis: {state}", flush=True)
        for profile_name, metrics in profile_metrics.items():
            print(
                f"{profile_name}: {metrics['selectedPoses']}/"
                f"{metrics['sourcePoses']} poses, "
                f"{metrics['reduction']:.1%} reduction, "
                f"{metrics['estimatedBytes']['maximumLoadoutCompositeCacheDecodedBytes']} "
                "bytes worst active loadout",
                flush=True,
            )
        print(
            f"selected: {selected_profile} -> {report['selectedTimeline']}",
            flush=True,
        )
        print(
            "independent layers: "
            f"{independent_metrics['totalStoredPoses']} stored poses, "
            f"{independent_metrics['estimatedBytes']['sharedLibraryTightDecodedBytes']} "
            "estimated tight decoded bytes",
            flush=True,
        )
        for recommendation in report["budgetRecommendations"]:
            if recommendation["recommendedMaxPixelsPerSample"] is None:
                print(
                    f"{recommendation['name']}: no configured curve point fits",
                    flush=True,
                )
            else:
                print(
                    f"{recommendation['name']}: "
                    f"{recommendation['recommendedMaxPixelsPerSample']:g}px, "
                    f"{recommendation['selectedPoses']} poses",
                    flush=True,
                )


if __name__ == "__main__":
    main()
