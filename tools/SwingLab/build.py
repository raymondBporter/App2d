"""Swing lab page: render the sword swing experiments with and without the live swoosh, and assemble a publishable site.

Usage: python tools/SwingLab/build.py <output-directory>
Writes <output>/site (index.html, sheets/) for publishing as an Artifact. Needs Pillow and a built
App2d.CharacterStudio (Debug). The page stores picks in its artifact db collection "picks".
"""
import json, math, os, shutil, subprocess, sys
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.abspath(sys.argv[1])
EXE = os.path.join(ROOT, 'App2d.CharacterStudio', 'bin', 'Debug', 'net10.0-windows10.0.19041.0', 'App2d.CharacterStudio.exe')
subprocess.run(['dotnet', 'build', os.path.join(ROOT, 'App2d.CharacterStudio'), '-v', 'q', '-nologo'], check=True)
R = os.path.join(OUT, 'review'); shutil.rmtree(R, ignore_errors=True)
subprocess.run([EXE, '--swing-lab', R], check=True)
checks = {l.split(':')[0]: l.split(': ', 1)[1].strip() for l in open(os.path.join(R, 'checks.txt')) if ':' in l}
print('\n'.join(f'{k}: {v}' for k, v in checks.items()))

O = os.path.join(OUT, 'site'); shutil.rmtree(O, ignore_errors=True); os.makedirs(os.path.join(O, 'sheets'))
W, H = 330, 345
variants = []
for e in json.load(open(os.path.join(R, 'manifest.json'))):
    n = e['frames']; cols = min(n, 12); rows = math.ceil(n / cols)
    for mode in ('plain', 'swoosh'):
        sheet = Image.new('RGB', (cols * W, rows * H), (241, 240, 232))
        for i in range(n):
            im = Image.open(os.path.join(R, 'frames', e['id'], mode, f'{i:03d}.png')).convert('RGB').resize((W, H), Image.LANCZOS)
            sheet.paste(im, ((i % cols) * W, (i // cols) * H))
        sheet.quantize(colors=64, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).save(os.path.join(O, 'sheets', f"{e['id']}-{mode}.png"), optimize=True)
    variants.append({**e, 'cols': cols, 'w': W, 'h': H, 'checks': checks.get(e['id'], '')})

template = open(os.path.join(ROOT, 'tools', 'SwingLab', 'template.html'), encoding='utf-8').read()
open(os.path.join(O, 'index.html'), 'w', encoding='utf-8').write(template.replace('/*VARIANTS*/[]', json.dumps(variants, separators=(',', ':'))))
print('total bytes', sum(os.path.getsize(os.path.join(dp, f)) for dp, _, fs in os.walk(O) for f in fs))
