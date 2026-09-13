"""Package Blender renders as GIF previews and an optional inline review atlas."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import base64
import io
import json
import sys

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'Assets/Library/characters/player-sword/balance-study'
kinds=['standing','balance-subtle','balance-windmill']
labels=['Standing reference','Small corrections','Windmill recovery']
frames={k:[Image.open(p).convert('RGBA') for p in sorted((OUT/k).glob('frame-*.png'))] for k in kinds}
assert [len(frames[k]) for k in kinds]==[1,72,72]
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',19)
atlases=[]
for k in kinds:
    seq=frames[k]
    atlas=Image.new('RGBA',(3072,256*((len(seq)+11)//12)))
    for i,im in enumerate(seq):
        atlas.paste(im.resize((256,256),Image.Resampling.LANCZOS),((i%12)*256,(i//12)*256))
    buffer=io.BytesIO();atlas.save(buffer,format='WEBP',lossless=True)
    atlases.append('data:image/webp;base64,'+base64.b64encode(buffer.getvalue()).decode())

def panel(im, kind, label):
    canvas=Image.new('RGBA',(512,466),'#9ca8b2')
    d=ImageDraw.Draw(canvas)
    edge=512 if kind=='standing' else 229
    d.rectangle((0,376,edge-1,466),fill='#536572')
    d.line((0,376,edge,376),fill='#dae4e8',width=2)
    if kind!='standing': d.line((edge,376,edge,466),fill='#dae4e8',width=2)
    canvas.alpha_composite(im,(0,-34))
    d.text((22,15),label,font=font,fill='#18242d')
    return canvas.convert('RGB')

triptych=[]
individual={k:[] for k in kinds[1:]}
for i in range(72):
    combined=Image.new('RGB',(960,292))
    for n,k in enumerate(kinds):
        p=panel(frames[k][0 if k=='standing' else i],k,labels[n])
        combined.paste(p.resize((320,292),Image.Resampling.LANCZOS),(n*320,0))
        if k!='standing':individual[k].append(p)
    triptych.append(combined)
# GIF time resolution is 10 ms: distribute rounding so 72 frames total 3 s.
durations=[round((i+1)*100/24)*10-round(i*100/24)*10 for i in range(72)]
triptych[0].save(OUT/'comparison.gif',save_all=True,append_images=triptych[1:],duration=durations,loop=0)
for k,seq in individual.items():
    seq[0].save(OUT/(k+'.gif'),save_all=True,append_images=seq[1:],duration=durations,loop=0)
panel(frames['standing'][0],'standing',labels[0]).save(OUT/'standing-reference.png')
if len(sys.argv)>1:
    fragment=Path(sys.argv[1]);markup=fragment.read_text(encoding='utf-8')
    assert '__ATLAS_DATA__' in markup
    fragment.write_text(markup.replace('__ATLAS_DATA__',json.dumps(atlases)),encoding='utf-8')
    assert fragment.stat().st_size<1024*1024
    print('INLINE_BYTES',fragment.stat().st_size)
report={}
for k in kinds[1:]:
    # Check rendered pixels, not just the fixed bone target.
    contacts=[]
    for im in frames[k]:
        contacts.append([x for x in range(180,245) if im.getpixel((x,409))[3]>128])
    assert all(c==contacts[0] for c in contacts),f'{k}: support foot drift'
    assert all(im.getchannel('A').getbbox() and im.getchannel('A').getbbox()[0]>0 and im.getchannel('A').getbbox()[2]<512 for im in frames[k]),f'{k}: clipped'
    report[k]={'frames':len(frames[k]),'support_contact_x':contacts[0],'support_contact_y':409,'support_pixels_identical_all_frames':True}
(OUT/'preview-checks.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
