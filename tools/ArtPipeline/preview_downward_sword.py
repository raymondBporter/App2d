"""Package and inspect the downward sword attack using its export timing."""
from pathlib import Path
import io,base64,json,sys
from PIL import Image,ImageDraw,ImageFont
from import_stick_figure import transform_frame

ROOT=Path(__file__).resolve().parents[2]
SOURCE=ROOT/'Assets/Sources/characters/player-sword/downward-attack'
OUT=ROOT/'Assets/Work/previews/player-sword/downward-attack'
OUT.mkdir(parents=True,exist_ok=True)
paths=sorted((SOURCE/'rendered/sword-down-attack').glob('frame-*.png'))
spec=json.loads((SOURCE/'render.json').read_text())['clips']['sword-down-attack']
count=len(spec['frames']);duration=spec['durationSeconds'];fps=count/duration
assert len(paths)==count
frames=[transform_frame(path) for path in paths]
bounds=[im.getbbox() for im in frames]
assert all(b and b[0]>0 and b[1]>0 and b[2]<512 and b[3]<512 for b in bounds),bounds
crop=(min(b[0] for b in bounds)-18,min(b[1] for b in bounds)-18,
      max(b[2] for b in bounds)+18,max(b[3] for b in bounds)+22)
w,h=crop[2]-crop[0],crop[3]-crop[1]
atlas=Image.new('RGBA',(w*count,h))
preview=[]
for i,frame in enumerate(frames):
    cropped=frame.crop(crop);atlas.paste(cropped,(i*w,0))
    background=Image.new('RGBA',(w,h),'#9ca8b2');background.alpha_composite(cropped)
    preview.append(background.convert('RGB'))
# GIF rounds to 10ms; frame holds sum to the recipe duration, then add a
# 600ms review pause on the final guard pose before the next playback.
durations=[round((i+1)*100/fps)*10-round(i*100/fps)*10 for i in range(count)]
durations[-1]+=600
preview[0].save(OUT/'downward-sword.gif',save_all=True,append_images=preview[1:],duration=durations,loop=0)
slow=[round((i+1)*400/fps)*10-round(i*400/fps)*10 for i in range(count)]
slow[-1]+=600
preview[0].save(OUT/'downward-sword-slow.gif',save_all=True,append_images=preview[1:],duration=slow,loop=0)
poses=json.loads((SOURCE/'poses.json').read_text())
font=ImageFont.truetype('C:/Windows/Fonts/segoeui.ttf',16)
columns=3 if count==6 else 4
sheet=Image.new('RGB',(w*columns,(h+34)*((count+columns-1)//columns)),'#9ca8b2')
d=ImageDraw.Draw(sheet)
for i,p in enumerate(preview):
    x,y=i%columns*w,i//columns*(h+34)
    sheet.paste(p,(x,y+34));d.text((x+8,y+8),f'{i+1}. '+poses[i]['label'],font=font,fill='#18242d')
sheet.save(OUT/'contact-sheet.png')
preview[0].save(OUT/'downward-strike.png')
checks={'frameCount':count,'durationSeconds':duration,'runtimeLoop':False,
        'sweepFrames':[p['frame'] for p in poses if p['sweepVisible']],
        'normalizedBounds':bounds,'allFramesInside512Canvas':True,
        'previewPauseSeconds':.6}
(OUT/'checks.json').write_text(json.dumps(checks,indent=2)+'\n')
if len(sys.argv)>1:
    buffer=io.BytesIO();atlas.save(buffer,format='WEBP',lossless=True)
    data={'width':w,'height':h,'count':count,'duration':duration,'fps':fps,'labels':[p['label'] for p in poses],
          'atlas':'data:image/webp;base64,'+base64.b64encode(buffer.getvalue()).decode()}
    path=Path(sys.argv[1]);fragment=path.read_text(encoding='utf-8')
    assert '__ATTACK_DATA__' in fragment
    path.write_text(fragment.replace('__ATTACK_DATA__',json.dumps(data)),encoding='utf-8')
    assert path.stat().st_size<1024*1024
print(json.dumps(checks,indent=2))
