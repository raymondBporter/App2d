#!/usr/bin/env python3

import unittest

from sparse_timeline_planning import (
    build_independent_layer_timelines,
    build_timeline_from_analysis,
    recommend_for_budgets,
)


def synthetic_analysis() -> dict:
    return {
        "format": "screen-space-motion-analysis-v1",
        "id": "test",
        "canvas": [64, 64],
        "equipment": ["weapon"],
        "animations": {
            "attack": {
                "sourceFrameCount": 6,
                "sourceDurationSeconds": 1.0,
                "playbackDurationSeconds": 1.0,
                "loop": False,
                "roots": {"right": [32, 60], "left": [32, 60]},
                "segments": {
                    "combinedPixels": [0.5] * 6,
                    "characterPixels": [0.25] * 6,
                    "equipmentPixels": {"weapon": [0.5] * 6},
                },
                "costs": {
                    "characterTightDecodedBytes": [10] * 6,
                    "equipmentTightDecodedBytes": {"weapon": [5] * 6},
                    "compositeCacheDecodedBytes": {"weapon": [20] * 6},
                },
            }
        },
    }


class SparseTimelinePlanningTests(unittest.TestCase):
    def test_independent_layers_keep_duration_but_choose_separate_pose_grids(self) -> None:
        timeline_set, character, equipment, metrics = build_independent_layer_timelines(
            {"id": "test", "canvas": [64, 64], "targetFramesPerSecond": 30},
            synthetic_analysis(),
            "balanced",
            {"maxPixelsPerSample": 0.4, "minimumFramesPerSecond": 1.0},
            "../../../motion-analysis.json",
        )

        character_samples = character["animations"]["attack"]["samples"]
        equipment_samples = equipment["weapon"]["animations"]["attack"]["samples"]
        self.assertLess(len(character_samples), len(equipment_samples))
        self.assertAlmostEqual(
            sum(sample["durationSeconds"] for sample in character_samples),
            sum(sample["durationSeconds"] for sample in equipment_samples),
        )
        self.assertEqual(
            "sparse-independent-layer-timeline-set-v1", timeline_set["format"]
        )
        self.assertEqual(
            len(character_samples) + len(equipment_samples),
            metrics["totalStoredPoses"],
        )

    def test_profile_produces_exact_source_grid_timeline_and_costs(self) -> None:
        timeline, metrics = build_timeline_from_analysis(
            {
                "id": "test",
                "canvas": [64, 64],
                "targetFramesPerSecond": 30,
            },
            synthetic_analysis(),
            "balanced",
            {
                "maxPixelsPerSample": 1.0,
                "minimumFramesPerSecond": 1.0,
                "motionScope": "combined",
            },
            "../../motion-analysis.json",
        )

        samples = timeline["animations"]["attack"]["samples"]
        self.assertEqual([1, 3, 5, 6], [sample["sourceFrame"] for sample in samples])
        self.assertAlmostEqual(1.0, sum(sample["durationSeconds"] for sample in samples))
        self.assertEqual(4, metrics["selectedPoses"])
        self.assertEqual(40, metrics["estimatedBytes"]["characterTightDecodedBytes"])
        self.assertEqual(60, metrics["estimatedBytes"]["sharedLibraryTightDecodedBytes"])
        self.assertEqual(80, metrics["estimatedBytes"]["maximumLoadoutCompositeCacheDecodedBytes"])

    def test_required_source_frames_are_policy_not_analysis(self) -> None:
        timeline, _ = build_timeline_from_analysis(
            {
                "id": "test",
                "canvas": [64, 64],
                "targetFramesPerSecond": 30,
            },
            synthetic_analysis(),
            "anchored",
            {
                "maxPixelsPerSample": 1.0,
                "minimumFramesPerSecond": 1.0,
                "motionScope": "loadout:weapon",
                "requiredSourceFrames": {"attack": [2]},
            },
            "../../motion-analysis.json",
        )

        samples = timeline["animations"]["attack"]["samples"]
        self.assertEqual([1, 2, 3, 5, 6], [sample["sourceFrame"] for sample in samples])
        self.assertAlmostEqual(1.0, sum(sample["durationSeconds"] for sample in samples))

    def test_budget_recommendation_chooses_highest_quality_that_fits(self) -> None:
        curve = [
            {
                "maxPixelsPerSample": 4.0,
                "selectedPoses": 20,
                "estimatedBytes": {"maximumLoadoutCompositeCacheDecodedBytes": 120},
            },
            {
                "maxPixelsPerSample": 8.0,
                "selectedPoses": 12,
                "estimatedBytes": {"maximumLoadoutCompositeCacheDecodedBytes": 80},
            },
            {
                "maxPixelsPerSample": 16.0,
                "selectedPoses": 8,
                "estimatedBytes": {"maximumLoadoutCompositeCacheDecodedBytes": 50},
            },
        ]
        recommendations = recommend_for_budgets(
            curve,
            {
                "budgets": [
                    {
                        "name": "test",
                        "metric": "maximumLoadoutCompositeCacheDecodedBytes",
                        "bytes": 90,
                    }
                ]
            },
        )

        self.assertEqual(8.0, recommendations[0]["recommendedMaxPixelsPerSample"])
        self.assertEqual(12, recommendations[0]["selectedPoses"])


if __name__ == "__main__":
    unittest.main()
