"""Seed editable baby triceratops assets from the existing quadruped rig."""
import copy
import math
from build_club_caveman import load, save

ID = 'baby-triceratops'
SCALE = .7

def main():
    model = load('models', 'triceratops')
    model.update(id=ID+'-rig', name='Baby triceratops')
    for control in model['controls']:
        for axis in ('x','y','z'):
            if axis in control['rest']: control['rest'][axis] *= SCALE
    for part in model['parts']:
        for field in ('width','height','offsetX','offsetY','depth'):
            if field in part: part[field] *= SCALE
        if 'horn' in part['id']:
            part['width'] *= .8; part['height'] *= .8
        if part['id']=='eye': part['width'] *= 1.2; part['height'] *= 1.2
    for layout in model['hurtLayouts']:
        for region in layout['regions']: region['pad'] *= SCALE
    for motion in model['motionSets']:
        motion['roles']={role: clip.replace('triceratops-',ID+'-') for role,clip in motion['roles'].items()}
        motion['roles'].update(hit=ID+'-hit',death=ID+'-death')
    save('models', model)
    for role in ('idle','walk','run','scrape','head-down','rush','brake','recover'):
        clip=load('animations','triceratops-'+role)
        clip.update(id=ID+'-'+role,name='Baby triceratops / '+role,model=ID+'-rig')
        for track in clip['tracks']:
            if track.get('kind','translate')!='rotate':
                for k in track['keys']:
                    for axis in ('x','y','z'):
                        if axis in k:k[axis] *= SCALE
        for k in clip['travel']['keys']:
            if 'x' in k:k['x'] *= SCALE
        for contact in clip.get('contacts',[]):
            for axis in ('x','y','z'):
                if axis in contact.get('target',{}):contact['target'][axis] *= SCALE
        save('animations',clip)
    def clip(name,duration):
        return dict(format='app2d-clip',version=1,id=ID+'-'+name,name='Baby triceratops / '+name,model=ID+'-rig',
                    structureRevision=1,duration=duration,reference={},travel=dict(keys=[]),tracks=[],contacts=[],markers=[])
    charge=clip('charge',2.7)
    def track(kind,target,keys): charge['tracks'].append(dict(kind=kind,target=target,keys=keys))
    def key(t,**values):return dict(time=round(t,5),ease='smooth',**values)
    track('rotate','head',[key(0,angle=-.1),key(.65,angle=-.2),key(1,angle=-.55),key(1.85,angle=-.55),key(2.15,angle=.1),key(2.7,angle=0)])
    track('translate','head',[key(0),key(1,y=-.09),key(1.85,y=-.09),key(2.15,y=-.04),key(2.7)])
    track('translate','body',[key(0),key(.65,y=.02),key(1,y=-.04),key(1.85,y=-.04),key(2.15,x=.04,y=-.06),key(2.7)])
    track('rotate','tail',[key(0),key(.3,angle=.15),key(.65,angle=-.1),key(1.85,angle=-.1),key(2.7)])
    for leg in ('far-hind','far-front','near-hind','near-front'):
        keys=[key(0)]
        if leg=='near-front':keys += [key(.2,x=.12,y=.1),key(.4,x=.16),key(.65,x=-.12)]
        keys.append(key(1))
        phase=0 if leg in ('near-front','far-hind') else .5
        for i in range(1,43):
            t=1+.85*i/42
            cycle=((t-1)/.28+phase)%1
            keys.append(key(t,x=.15*math.cos(cycle*2*math.pi),y=.1*max(0,math.sin(cycle*2*math.pi))))
        keys += [key(2.05,x=.14),key(2.15,x=.1),key(2.7)]
        track('target',leg,keys)
        if leg!='near-front':charge['contacts'].append(dict(chain=leg,finish=1))
        charge['contacts'].append(dict(chain=leg,start=2.15,finish=2.7))
    charge['markers']=[dict(id='scrape',time=.4),dict(id='rush',time=1),dict(id='brake',time=1.85),dict(id='recover',time=2.15)]
    save('animations',charge)
    for role,duration in [('hit',.3),('death',.8)]:
        reaction=clip(role,duration)
        reaction['tracks']=[dict(kind='rotate',target='body',keys=[key(0),key(duration*.4,angle=.18 if role=='hit' else .12),key(duration,angle=0 if role=='hit' else .12)]),
                            dict(kind='translate',target='body',keys=[key(0),key(duration*.4,x=-.08,y=0 if role=='hit' else -.35),key(duration,x=0 if role=='hit' else -.08,y=0 if role=='hit' else -.35)])]
        save('animations',reaction)
    entity=dict(format='app2d-entity',version=1,id=ID,name='Baby triceratops',model=ID+'-rig',motionSet='standard',roles={},
                health=8,mass=1.5,movement=dict(width=1.6,height=.95,offsetX=-.1),hurt=dict(layout='body',regions={}),equipment=[],
                controller=dict(walkSpeed=1.2,range=4.5,cooldown=.7,respectTerrain=True,verticalRange=.8,
                                chargeSpeed=5,chargeStartSeconds=1,chargeEndSeconds=1.85,brakeSeconds=.3),
                actions=[dict(id='attack',clip=charge['id'],hits=[dict(id='horns',socket='head-art',width=.75,height=.65,
                                  offsetX=0,start=dict(marker='rush'),finish=dict(marker='brake'),damage=3,sound='hit')],
                              events=[dict(id='scrape',at=dict(marker='scrape'),sound='charge-scrape'),
                                      dict(id='rush',at=dict(marker='rush'),sound='charge-rush'),
                                      dict(id='brake',at=dict(marker='brake'),sound='charge-stop')])])
    save('entities',entity)

if __name__=='__main__':main()
