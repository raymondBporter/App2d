"""Explicitly regenerate editable rock-thrower art and mathematical rig curves."""
import copy
from build_club_caveman import load, save, extrude

def main():
    variant = load('variants', 'cinder')
    variant.update(id='rock-thrower-build', name='Rock thrower')
    variant['parts']['body']['fill'] = '#f3dcc6'
    variant['parts']['body']['paint'] = []
    variant['build']['arms'] = 1.05
    save('variants', variant)
    for source, suffix in [('cinder-hide-wrap','hide-wrap'), ('cinder-hair','hair'), ('cinder-beard','beard')]:
        prop = load('props', source)
        prop.update(id='rock-thrower-'+suffix, name='Rock thrower / '+suffix)
        for solid in prop['solids']:
            if solid['fill'] == '#843f27': solid['fill'] = '#69402a'
        save('props', prop)
    rock = dict(format='app2d-prop', version=1, id='throwing-rock', name='Throwing rock',
                ink='#30373a', lineWidth=.025, grip=dict(x=0,y=-.17,z=.5), tip=dict(x=0,y=0,z=0), muzzle=dict(x=0,y=0,z=0), shapes=[],
                solids=[extrude([(-.19,-.10),(-.08,-.18),(.13,-.16),(.22,-.02),(.13,.16),(-.10,.18),(-.22,.04)], '#858b86', .16)])
    save('props', rock)
    bag = dict(format='app2d-prop', version=1, id='rock-thrower-bag', name='Hide stone bag with teal sling',
               ink='#30373a', lineWidth=.025, usage='clothing', attachment='body-art', tip=dict(x=0,y=0,z=0), shapes=[],
               solids=[extrude([(-.39,-.12),(-.58,-.02),(-.56,.25),(-.39,.29),(-.21,.22),(-.23,-.05)], '#987347', .08),
                       extrude([(-.42,.18),(.13,.71),(.17,.67),(-.37,.13)], '#538e89', .03)])
    # Put the bag in front of the hips; the narrow strap reads over the bare torso.
    for solid in bag['solids']:
        for p in solid['vertices']: p['z'] += .12
        for p in solid['outline']: p['z'] += .12
    save('props', bag)
    clip = load('animations', 'person-hammer-slam')
    clip.update(id='rock-thrower-throw', name='Rock thrower / search lift aim release', duration=1.85)
    times={0:0,.55:.8,.7:1.05,.78:1.14,.95:1.3,1.5:1.85}
    for track in clip['tracks']:
        for k in track['keys']: k['time'] = times[k.get('time',0)]
        # Search the hip bag, then hold the stone overhead before a quick forward throw.
        if track['target']=='right-arm' and track.get('kind')=='target':
            first=copy.deepcopy(track['keys'][0]); first.update(time=.35,x=-.32,y=-.18)
            track['keys'].insert(1,first)
            for k in track['keys']:
                if .8 <= k['time'] <= 1.05: k.update(x=-.03,y=1.5)
        if track['target']=='left-arm':
            for k in track['keys']:
                if .8 <= k['time'] <= 1.05: k.update(x=.03,y=1.5)
        if track['target']=='right-shoulder':
            for k in track['keys']: k['angle']=0
        if track.get('kind')=='orient':
            for k in track['keys']: k.update(x=0,y=0,z=0)
        if track['target']=='hips':
            for k in track['keys']:
                if k['time'] in (1.14,1.3) and 'y' in k: k['y']=-.08
        if track['target']=='chest' and track.get('kind')=='rotate':
            for k in track['keys']:
                if k['time'] in (1.14,1.3): k['angle']=-.28
        if track['target'] in ('hips','chest','head'):
            for k in track['keys']:
                if .8 <= k['time'] <= 1.05:
                    if track.get('kind')=='rotate': k['angle']=0
                    else: k.update(x=0,y=0)
    for c in clip['contacts']: c['finish']=1.85
    clip['markers']=[dict(id='search',time=.15),dict(id='lift',time=.55),dict(id='aim',time=.8),dict(id='release',time=1.14),dict(id='recover',time=1.3)]
    for face in clip.get('faces',[]):
        for k in face['keys']: k['time']= {0:0,.5:.8,.78:1.14}[k.get('time',0)]
    save('animations',clip)
    entity=load('entities','club-caveman')
    entity.update(id='rock-thrower',name='Rock thrower',model=variant['id'],health=6)
    entity['controller']=dict(walkSpeed=1.8,range=7,cooldown=.7,respectTerrain=True,verticalRange=3,
                              retreatRange=2,retreatSeconds=.35,retreatPause=.65)
    entity['equipment']=[dict(prop='throwing-rock',socket='sword-hand')] + [dict(prop='rock-thrower-'+s,socket=socket) for s,socket in
                         [('hide-wrap','body-art'),('bag','body-art'),('hair','head-art'),('beard','head-art')]]
    entity['actions']=[dict(id='attack',clip=clip['id'],hits=[],projectile=dict(speed=8,width=.36,height=.36,damage=2,lifetime=3,gravity=15,flightSeconds=.85),
                           events=[dict(id='bag-search',at=dict(marker='search'),sound='rock-search'),
                                   dict(id='fire',at=dict(marker='release'),sound='rock-throw')])]
    save('entities',entity)

if __name__=='__main__': main()
