"""Flatten Gurgle to three base colors while retaining antialiased boundaries."""
import argparse
import json
import time
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy.ndimage import gaussian_filter

PALETTE = np.array([[0, 0, 0], [255, 255, 255], [255, 0, 0]], dtype=np.float32)


def flatten(image):
    rgba = np.asarray(image.convert('RGBA'), dtype=np.float32) / 255
    rgb, alpha = rgba[..., :3], rgba[..., 3]
    # Fixed assignments across frames avoid independently learned palette flicker.
    labels = ((rgb[..., None, :] - PALETTE / 255) ** 2).sum(-1).argmin(-1)
    # Treat sufficiently saturated dark reds as red rather than losing them to black.
    red = (rgb[..., 0] > .28) & (rgb[..., 0] - rgb[..., 1:].max(-1) > .16)
    labels[red] = 2
    # Smooth color coverage, not RGB against the transparent canvas's black values.
    coverage = np.stack([gaussian_filter(alpha * (labels == i), .55, truncate=2)
                         for i in range(3)], axis=-1)
    out_alpha = np.clip(coverage.sum(-1), 0, 1)
    premultiplied = coverage @ (PALETTE / 255)
    out_rgb = np.divide(premultiplied, out_alpha[..., None],
                        out=np.zeros_like(premultiplied), where=out_alpha[..., None] > 1e-6)
    return Image.fromarray(np.rint(np.dstack([out_rgb, out_alpha]) * 255).astype('uint8'))


def resize(image, size):
    # Pillow's RGBa mode stores premultiplied RGB and avoids dark resize fringes.
    return image.convert('RGBa').resize((size, size), Image.Resampling.LANCZOS).convert('RGBA')


def composite(image, color):
    canvas = Image.new('RGBA', image.size, color)
    canvas.alpha_composite(image)
    return canvas.convert('RGB')


def save_gif(images, path, durations):
    # A single shared palette prevents GIF-specific color changes between frames.
    atlas = Image.new('RGB', (images[0].width * len(images), images[0].height))
    for i, image in enumerate(images):
        atlas.paste(image, (i * image.width, 0))
    palette = atlas.quantize(colors=256, dither=Image.Dither.NONE)
    indexed = [im.quantize(palette=palette, dither=Image.Dither.NONE) for im in images]
    indexed[0].save(path, save_all=True, append_images=indexed[1:], duration=durations,
                    loop=0, disposal=2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('study', type=Path)
    parser.add_argument('--name', default='palette-cleanup')
    args = parser.parse_args()
    if Path(args.name).name != args.name or args.name in ('', '.', '..'):
        parser.error('name must be a single directory name')
    start = time.perf_counter()
    meta = json.loads((args.study / 'animation.json').read_text())
    originals = [Image.open(args.study / f'frame-{i+1:04d}.png').convert('RGBA')
                 for i in range(len(meta['frames']))]
    if any(im.size != (512, 512) for im in originals):
        parser.error('This Gurgle experiment expects 512x512 source frames')
    durations = [frame['duration_ms'] for frame in meta['frames']]
    output = args.study / args.name
    output.mkdir(exist_ok=False)
    cleaned = [flatten(im) for im in originals]
    for size in (512, 256, 128):
        folder = output / str(size)
        folder.mkdir()
        frames = [resize(im, size) for im in cleaned]
        sheet = Image.new('RGBA', (size * len(frames), size))
        for i, frame in enumerate(frames):
            frame.save(folder / f'frame-{i+1:04d}.png')
            sheet.paste(frame, (size*i, 0))
        sheet.save(folder / 'spritesheet.png')
        for name, color in [('dark', '#282b34'), ('light', '#eee7d2')]:
            save_gif([composite(im, color) for im in frames], folder / f'preview-{name}.gif', durations)
        (folder / 'animation.json').write_text(json.dumps({**meta, 'width': size, 'height': size,
            'source_study': str(args.study.resolve()), 'base_palette': PALETTE.astype(int).tolist(),
            'frames': [{'filename': f'frame-{i+1:04d}.png', 'source_frame': frame['source_frame'],
                        'duration_ms': frame['duration_ms'], 'bounds': frames[i].getbbox()}
                       for i, frame in enumerate(meta['frames'])],
            'note': 'Three flat base colors plus blended boundary pixels; experimental cleanup, not runtime manifest'
        }, indent=2))
    # Compare at the same final size; include two backgrounds to expose matte artifacts.
    previews = []
    for original, clean in zip(originals, cleaned):
        canvas = Image.new('RGB', (512, 552), '#282b34')
        draw = ImageDraw.Draw(canvas)
        draw.text((12, 8), 'Before / 256 px', fill='white')
        draw.text((268, 8), 'Palette + smooth edges / 256 px', fill='white')
        for row, color in enumerate(('#282b34', '#eee7d2')):
            canvas.paste(composite(resize(original, 256), color), (0, 32 + row*260))
            canvas.paste(composite(resize(clean, 256), color), (256, 32 + row*260))
        previews.append(canvas)
    save_gif(previews, output / 'comparison.gif', durations)
    previews[2].save(output / 'comparison.png')
    (output / 'report.json').write_text(json.dumps({'elapsed_seconds': time.perf_counter()-start,
        'source': str(args.study.resolve()), 'base_palette': PALETTE.astype(int).tolist(),
        'sizes': [512, 256, 128], 'duration_ms': sum(durations)}, indent=2))
    print(output.resolve())
    print(f'Cleanup and exports: {time.perf_counter()-start:.2f}s')


if __name__ == '__main__':
    main()
