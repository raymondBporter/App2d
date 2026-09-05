"""Remove only border-connected near-white background; preserve enclosed whites."""
from pathlib import Path
from PIL import Image, ImageDraw

folder = Path(__file__).resolve().parent
source = folder.parents[3] / '4gh0secedcag1.webp'
image = Image.open(source).convert('RGBA')
mask = Image.new('L', image.size)
mask.putdata([255 if min(r, g, b) >= 210 and max(r, g, b)-min(r, g, b) <= 35 else 0
              for r, g, b, a in image.get_flattened_data()])
w, h = image.size
for point in ([(x, 0) for x in range(w)] + [(x, h-1) for x in range(w)]
              + [(0, y) for y in range(h)] + [(w-1, y) for y in range(h)]):
    if mask.getpixel(point) == 255:
        ImageDraw.floodfill(mask, point, 128)
image.putalpha(mask.point(lambda value: 0 if value == 128 else 255))
output = folder / 'reference-transparent.png'
image.save(output)
alpha = image.getchannel('A')
print(f'{output}: {image.size}, {image.mode}, transparent pixels={alpha.histogram()[0]}')
