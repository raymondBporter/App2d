"""Package the redraw with rough, border-connected background removal."""
from pathlib import Path
import json
from PIL import Image, ImageDraw

folder = Path(__file__).resolve().parent
raw = Image.open(folder / 'redraw-raw.png').convert('RGBA')
frames_dir = folder / 'frames'
frames_dir.mkdir(exist_ok=True)
sheet = Image.new('RGBA', (2048, 1024))
preview = []
for index in range(8):
    col, row = index % 4, index // 4
    box = (round(col * raw.width / 4), round(row * raw.height / 2),
           round((col+1) * raw.width / 4), round((row+1) * raw.height / 2))
    frame = raw.crop(box)
    mask = Image.new('L', frame.size)
    mask.putdata([255 if min(r,g,b) >= 210 and max(r,g,b)-min(r,g,b) <= 35 else 0
                  for r,g,b,a in frame.get_flattened_data()])
    w, h = frame.size
    border = ([(x,0) for x in range(w)] + [(x,h-1) for x in range(w)]
              + [(0,y) for y in range(h)] + [(w-1,y) for y in range(h)])
    for point in border:
        if mask.getpixel(point) == 255:
            ImageDraw.floodfill(mask, point, 128)
    frame.putalpha(mask.point(lambda value: 0 if value == 128 else 255))
    bounds = frame.getbbox()
    assert bounds is not None
    # Common grid center, original art scale, and shared bottom anchor.
    canvas = Image.new('RGBA', (512, 512))
    canvas.alpha_composite(frame, (round((512-w)/2), 460-bounds[3]))
    canvas.save(frames_dir / f'frame-{index+1:04d}.png')
    sheet.alpha_composite(canvas, (col*512, row*512))
    bg = Image.new('RGBA', canvas.size, '#292933')
    bg.alpha_composite(canvas)
    preview.append(bg.convert('RGB'))
    alpha = canvas.getchannel('A')
    assert alpha.getextrema() == (0,255)
    assert canvas.getbbox()[3] == 460
sheet.save(folder / 'crawl-transparent.png')
preview[0].save(folder / 'crawl-preview.gif', save_all=True,
                append_images=preview[1:], duration=125, loop=0, disposal=2)
(folder / 'animation.json').write_text(json.dumps({
    'name': 'crawl', 'status': 'rough animation study', 'frameCount': 8,
    'frameSize': [512,512], 'fps': 8, 'loop': True, 'anchorPixels': [256,460],
    'sheet': 'crawl-transparent.png', 'columns': 4, 'rows': 2,
    'frames': [f'frames/frame-{i+1:04d}.png' for i in range(8)]
}, indent=2) + '\n')
print('Verified 8 transparent 512x512 frames, shared baseline, 2048x1024 sheet, and looping GIF.')
