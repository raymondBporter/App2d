import unittest

from PIL import Image

from render_loadout_equipment_layers import bake_afterimages


class BakeAfterimagesTests(unittest.TestCase):
    def test_bakes_older_colors_and_their_matching_depth(self) -> None:
        size = (3, 1)
        far_color = Image.new("RGBA", size)
        far_color.putpixel((0, 0), (0, 0, 255, 255))
        far_depth = Image.new("RGB", size)
        far_depth.putpixel((0, 0), (10, 10, 10))

        near_color = Image.new("RGBA", size)
        near_color.putpixel((1, 0), (0, 255, 0, 255))
        near_depth = Image.new("RGB", size)
        near_depth.putpixel((1, 0), (20, 20, 20))

        current_color = Image.new("RGBA", size)
        current_color.putpixel((2, 0), (255, 0, 0, 255))
        current_depth = Image.new("RGB", size)
        current_depth.putpixel((2, 0), (30, 30, 30))

        color, depth = bake_afterimages(
            current_color,
            current_depth,
            [(far_color, far_depth), (near_color, near_depth)],
            [
                {"framesBack": 1, "opacity": 0.5},
                {"framesBack": 2, "opacity": 0.25},
            ],
        )

        self.assertEqual((0, 0, 255, 64), color.getpixel((0, 0)))
        self.assertEqual((0, 255, 0, 128), color.getpixel((1, 0)))
        self.assertEqual((255, 0, 0, 255), color.getpixel((2, 0)))
        self.assertEqual((10, 10, 10), depth.getpixel((0, 0)))
        self.assertEqual((20, 20, 20), depth.getpixel((1, 0)))
        self.assertEqual((30, 30, 30), depth.getpixel((2, 0)))


if __name__ == "__main__":
    unittest.main()
