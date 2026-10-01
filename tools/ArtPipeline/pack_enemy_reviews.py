"""Package native Character Studio review frames as looping GIFs; no art regeneration."""
import argparse
from pathlib import Path
from PIL import Image

parser = argparse.ArgumentParser()
parser.add_argument('directory', type=Path)
args = parser.parse_args()
for folder in sorted(args.directory.glob('*-frames')):
    frames = []
    for file in sorted(folder.glob('frame-*.png')):
        with Image.open(file) as source:
            frames.append(source.convert('RGB').copy())
    if not frames: continue
    output = args.directory / (folder.name.removesuffix('-frames') + '.gif')
    frames[0].save(output, save_all=True, append_images=frames[1:], duration=42,
                   loop=0, disposal=2)
    print(output)
