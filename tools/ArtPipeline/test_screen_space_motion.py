#!/usr/bin/env python3

import unittest

from screen_space_motion import (
    frame_durations,
    playback_sample_times,
    select_motion_indices,
)


class ScreenSpaceMotionTests(unittest.TestCase):
    def assert_float_lists_equal(
        self,
        expected: list[float],
        actual: list[float],
    ) -> None:
        self.assertEqual(len(expected), len(actual))
        for expected_value, actual_value in zip(expected, actual, strict=True):
            self.assertAlmostEqual(expected_value, actual_value, places=12)

    def test_stationary_loop_uses_maximum_hold_without_duplicate_endpoint(self) -> None:
        indices = select_motion_indices(
            [0.0] * 30,
            duration_seconds=1.0,
            max_pixels_per_sample=2.0,
            minimum_frames_per_second=6.0,
            loop=True,
        )

        self.assertEqual([0, 5, 10, 15, 20, 25], indices)
        self.assert_float_lists_equal(
            [1 / 6] * 6,
            frame_durations(indices, 30, 1.0),
        )

    def test_motion_accumulates_along_curve_instead_of_cancelling(self) -> None:
        indices = select_motion_indices(
            [0.4] * 10,
            duration_seconds=1.0,
            max_pixels_per_sample=1.0,
            minimum_frames_per_second=1.0,
            loop=False,
        )

        self.assertEqual([0, 3, 6, 9], indices)
        self.assert_float_lists_equal(
            [0.3, 0.3, 0.3, 0.1],
            frame_durations(indices, 10, 1.0),
        )

    def test_selected_times_remain_source_grid_points_after_retiming(self) -> None:
        times = playback_sample_times(30, 0.24)
        indices = [0, 4, 12, 29]

        self.assertEqual([0.0, 0.032, 0.096, 0.232], [times[index] for index in indices])
        self.assert_float_lists_equal(
            [0.032, 0.064, 0.136, 0.008],
            frame_durations(indices, 30, 0.24),
        )


if __name__ == "__main__":
    unittest.main()
