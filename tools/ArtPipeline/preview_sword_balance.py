"""Review the two compact balance exports, validate pixels, and compare originals.

Run after rendering; PNG normalization uses the runtime importer's transform.
Optionally pass an inline fragment containing the __BALANCE_DATA__ placeholder.
"""
from pathlib import Path
import base64
import io
import json
import sys
from PIL import Image, ImageChops, ImageDraw, ImageStat, ImageFont
from import_stick_figure import transform_frame, SCALE, SOURCE_ROOT, TARGET_ROOT

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'Assets/Sources/characters/player-sword'
OUT=ROOT/'Assets/Work/previews/player-sword/balance-compact'
OUT.mkdir(parents=True,exist_ok=True)
render=json.loads((SOURCE/'rendered/render-report.json').read_text())
config=json.loads((SOURCE/'render.json').read_text())
kinds=list(config['clips'])
frames={k:[transform_frame(p) for p in sorted((SOURCE/'rendered'/k).glob('frame-*.png'))] for k in kinds}
assert all(len(seq)==8 for seq in frames.values())
bounds=[im.getbbox() for seq in frames.values() for im in seq]
crop=(min(b[0] for b in bounds)-20,min(b[1] for b in bounds)-20,
      max(b[2] for b in bounds)+20,max(b[3] for b in bounds)+45)
width,height=crop[2]-crop[0],crop[3]-crop[1]
data={'width':width,'height':height,'clips':[]}
checks={'clips':{},'sourceComparisons':{}}
previews={}
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',17)
for index,kind in enumerate(kinds):
    seq=frames[kind]
    x,y=render['clips'][kind]['supportPixel']
    x=round(x*SCALE+round(TARGET_ROOT[0]-SOURCE_ROOT[0]*SCALE))
    y=round(y*SCALE+round(TARGET_ROOT[1]-SOURCE_ROOT[1]*SCALE))
    box=(x-16,y-16,x+17,y+17)
    contacts=[]
    for im in seq:
        mask=im.getchannel('A').point(lambda a:255 if a>=128 else 0)
        b=mask.crop(box).getbbox()
        bottom=box[1]+b[3]-1
        contacts.append((bottom,tuple(xx for xx in range(box[0],box[2]) if mask.getpixel((xx,bottom)))))
        assert im.getbbox()[0]>0 and im.getbbox()[1]>0 and im.getbbox()[2]<512 and im.getbbox()[3]<512
    assert all(c==contacts[0] for c in contacts),f'{kind}: support contact changed'
    ground=contacts[0][0]+1-crop[1]
    edge=(min(contacts[0][1])+1 if index==0 else max(contacts[0][1]))-crop[0]
    atlas=Image.new('RGBA',(width*8,height))
    previews[kind]=[]
    for i,im in enumerate(seq):
        cropped=im.crop(crop)
        atlas.paste(cropped,(i*width,0))
        p=Image.new('RGBA',(width,height),'#9ca8b2')
        d=ImageDraw.Draw(p)
        left,right=(0,edge) if index==0 else (edge,width)
        d.rectangle((left,ground,right,height),fill='#536572')
        d.line((left,ground,right,ground),fill='#e0e8eb',width=1)
        d.line((edge,ground,edge,height),fill='#e0e8eb',width=1)
        p.alpha_composite(cropped)
        previews[kind].append(p.convert('RGB'))
    buf=io.BytesIO();atlas.save(buf,format='WEBP',lossless=True)
    data['clips'].append({'atlas':'data:image/webp;base64,'+base64.b64encode(buf.getvalue()).decode(),'edge':edge,'ground':ground})
    previews[kind][0].save(OUT/f'{kind}.gif',save_all=True,append_images=previews[kind][1:],duration=250,loop=0)
    checks['clips'][kind]={'frameCount':8,'durationSeconds':2,'contactRow':contacts[0][0],
                          'contactColumns':contacts[0][1],'allContactPixelsStable':True,'allFramesWithinCanvas':True}
comparison=[]
for i in range(8):
    p=Image.new('RGB',(width*2,height+36),'#9ca8b2')
    d=ImageDraw.Draw(p)
    for n,k in enumerate(kinds):
        p.paste(previews[k][i],(n*width,36))
        d.text((n*width+12,8),('Left' if n==0 else 'Right')+' foot planted / 8 frames',font=font,fill='#18242d')
    comparison.append(p)
comparison[0].save(OUT/'comparison.gif',save_all=True,append_images=comparison[1:],duration=250,loop=0)
sheet=Image.new('RGB',(width*8,height*2),'#9ca8b2')
for row,k in enumerate(kinds):
    for i,p in enumerate(previews[k]):sheet.paste(p,(i*width,row*height))
sheet.save(OUT/'contact-sheet.png')
pack=ROOT/'Assets/Sources/third-party/rgs-stick-figure/Stick Figure Character Sprites 2D'
for name,spec in render['references'].items():
    a=Image.open(SOURCE/'rendered'/spec['file']).convert('RGBA')
    b=Image.open(pack/spec['packFile']).convert('RGBA')
    ma=a.getchannel('A').point(lambda x:255 if x>=128 else 0)
    mb=b.getchannel('A').point(lambda x:255 if x>=128 else 0)
    overlap=ImageChops.darker(ma,mb).histogram()[255]
    union=ImageChops.lighter(ma,mb).histogram()[255]
    ca=Image.new('RGBA',a.size,'#9ca8b2');ca.alpha_composite(a)
    cb=Image.new('RGBA',b.size,'#9ca8b2');cb.alpha_composite(b)
    difference=ImageChops.difference(ca.convert('RGB'),cb.convert('RGB'))
    checks['sourceComparisons'][name]={'renderBounds':a.getbbox(),'packBounds':b.getbbox(),
        'opaqueMaskIntersectionOverUnion':round(overlap/union,6),
        'meanRgbError0To255':round(sum(ImageStat.Stat(difference).mean)/3,6)}
(OUT/'checks.json').write_text(json.dumps(checks,indent=2)+'\n')
if len(sys.argv)>1:
    path=Path(sys.argv[1]);text=path.read_text(encoding='utf-8')
    assert '__BALANCE_DATA__' in text
    path.write_text(text.replace('__BALANCE_DATA__',json.dumps(data)),encoding='utf-8')
    assert path.stat().st_size<1024*1024
print(json.dumps(checks,indent=2))
