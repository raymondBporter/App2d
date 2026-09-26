"""Player move review page: regenerate clips, render frames, and assemble a publishable site.

Usage: python tools/MoveReview/build.py <output-directory>
Writes <output>/site (index.html, sheets/, ref/) for publishing as an Artifact. Needs Pillow and a built
App2d.CharacterStudio (Debug). RGS reference strips come from the CC0 pack under Assets/Sources/third-party.
"""
import json, os, shutil, math, subprocess, sys, glob, re
from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.abspath(sys.argv[1])
EXE = os.path.join(ROOT, 'App2d.CharacterStudio', 'bin', 'Debug', 'net10.0-windows10.0.19041.0', 'App2d.CharacterStudio.exe')
def build(): subprocess.run(['dotnet', 'build', os.path.join(ROOT, 'App2d.CharacterStudio'), '-v', 'q', '-nologo'], check=True)
build()
subprocess.run([EXE, '--write-player-moves', os.path.join(ROOT, 'Assets', 'Characters', 'authored')], check=True)
build()
R = os.path.join(OUT, 'review'); shutil.rmtree(R, ignore_errors=True)
subprocess.run([EXE, '--review-moves', R], check=True)
print(open(os.path.join(R, 'checks.txt')).read())

# RGS contact strips, one per move set.
RGS = os.path.join(ROOT, 'Assets', 'Sources', 'third-party', 'rgs-stick-figure', 'Stick Figure Character Sprites 2D')
STRIPS = os.path.join(OUT, 'rgs'); os.makedirs(STRIPS, exist_ok=True)
groups = {}
for f in glob.glob(RGS + '/*sprites/*.png'):
    g = re.match(r'(.*?)_?(\d+)\.png', os.path.basename(f)); groups.setdefault(g.group(1), []).append(f)
for key, files in groups.items():
    ims = [Image.open(f).convert('RGBA') for f in sorted(files)]
    box = None
    for im in ims:
        b = im.getbbox()
        if b: box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    ims = [im.crop(box) for im in ims]; w, h = ims[0].size
    ims = [im.resize((max(1, int(w * 160 / h)), 160)) for im in ims]
    strip = Image.new('RGB', (sum(i.size[0] + 6 for i in ims), 166), 'white'); x = 0
    for im in ims: strip.paste(im, (x, 3), im); x += im.size[0] + 6
    strip.save(os.path.join(STRIPS, key + '.png'))

os.chdir(OUT)
R='review'; O='site'
shutil.rmtree(O, ignore_errors=True); os.makedirs(O+'/sheets'); os.makedirs(O+'/ref')
m=json.load(open(R+'/manifest.json'))
W,H=330,345
out=[]
for e in m:
    n=e['frames']; cols=min(n,12); rows=math.ceil(n/cols)
    sheet=Image.new('RGB',(cols*W,rows*H),(241,240,232))
    for i in range(n):
        im=Image.open(f"{R}/frames/{e['id']}/{i:03d}.png").convert('RGB').resize((W,H),Image.LANCZOS)
        sheet.paste(im,((i%cols)*W,(i//cols)*H))
    sheet.quantize(colors=64,method=Image.Quantize.MEDIANCUT,dither=Image.Dither.NONE).save(f"{O}/sheets/{e['id']}.png",optimize=True)
    e=dict(e); e['cols']=cols; e['w']=W; e['h']=H; out.append(e)
refs={'person-walk':'sword_walk','person-run':'sword_run','player-idle':'sword_Idle','player-jump':'sword_jump','player-dash':'sword_dash','player-climb':'sword_climb',
 'player-wall-grip':'sword_wallslide','player-hit':'sword_hit','player-death':'sword_death','player-sword-draw-slash':'sword_combo','player-sword-slash':'sword_combo',
 'player-sword-down-attack':'sword_air_attack','player-gun-aim':'pistol_Idle','player-gun-shot':'pistol_shot','player-gun-wall-shot':'pistol_wallslide'}
for e in out:
    r=refs.get(e['id'])
    if r:
        dst=f'{O}/ref/{r}.png'
        if not os.path.exists(dst):
            im=Image.open(f'rgs/{r}.png').convert('RGB'); h=110; im=im.resize((int(im.size[0]*h/im.size[1]),h),Image.LANCZOS)
            im.quantize(colors=32,dither=Image.Dither.NONE).save(dst,optimize=True)
        e['ref']=r
checks={l.split(':')[0]:l.split(': ',1)[1] for l in open(R+'/checks.txt') if ':' in l}
for e in out: e['checks']=checks.get(e['id'],'')
json.dump(out,open(O+'/moves.json','w'),indent=1)
tot=sum(os.path.getsize(os.path.join(dp,f)) for dp,_,fs in os.walk(O) for f in fs)
print('total bytes',tot, 'files', sum(len(fs) for _,_,fs in os.walk(O)))
t=open(os.path.join(ROOT,'tools','MoveReview','template.html'),encoding='utf-8').read()
open(O+'/index.html','w',encoding='utf-8').write(t.replace('/*MOVES*/[]',json.dumps(out,separators=(',',':'))))
