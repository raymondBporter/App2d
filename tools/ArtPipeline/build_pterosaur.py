"""Seed the pterosaur entity and gameplay clips from existing flight studies. Never overwrites the model/studies."""
import copy
from build_club_caveman import load, save

def main():
    phases=[('fold',0),('dive',.48),('brake',1.13),('low-recovery',1.45),('reset',2.45)]
    studies=[(load('animations','pterosaur-'+role),at) for role,at in phases]
    action=copy.deepcopy(studies[0][0])
    action.update(id='pterosaur-swoop',name='Pterosaur / committed dive and low pass',duration=3.65,loop=False,tracks=[],markers=[])
    identities={(t['kind'],t['target']) for clip,_ in studies for t in clip['tracks']}
    for kind,target in sorted(identities):
        keys={}
        for clip,offset in studies:
            source=next((t for t in clip['tracks'] if (t['kind'],t['target'])==(kind,target)),None)
            sample=source['keys'] if source else [dict(time=0),dict(time=clip.get('duration',1))]
            for k in sample:
                key=copy.deepcopy(k);key['time']=round(offset+k.get('time',0),5)
                keys[key['time']]=key
        action['tracks'].append(dict(kind=kind,target=target,keys=[keys[t] for t in sorted(keys)]))
    times=dict(screech=.2,commit=.4,rush=.48,brake=1.13,low=1.45,reset=2.45)
    action['markers']=[dict(id=id,time=t) for id,t in times.items()]
    save('animations',action)
    for role,duration,angle in [('hit',.45,.3),('death',.8,1.8)]:
        clip=copy.deepcopy(studies[0][0]);clip.update(id='pterosaur-'+role,name='Pterosaur / '+role,duration=duration,loop=False,markers=[],tracks=[])
        for target,a in [('body',angle),('near-shoulder',-.8),('far-shoulder',.8),('near-bend',-.5),('far-bend',.5)]:
            clip['tracks'].append(dict(kind='rotate',target=target,keys=[dict(time=0),dict(time=duration*.3,angle=a),dict(time=duration,angle=0 if role=='hit' else a)]))
        save('animations',clip)
    entity=dict(format='app2d-entity',version=1,id='flying-pterosaur',name='Pterosaur',model='pterosaur',motionSet='standard',
        roles=dict(hit='pterosaur-hit',death='pterosaur-death'),health=4,mass=.8,
        movement=dict(width=.85,height=.6,offsetY=.4),hurt=dict(layout='body',regions={}),equipment=[],
        controller=dict(kind="flying",range=3.2,cooldown=.8,flight=dict(hoverHeight=3.2,speed=3,diveSpeed=10,lowSpeed=.8)),
        actions=[dict(id='attack',clip='pterosaur-swoop',hits=[dict(id='beak',socket='head-art',width=.65,height=.5,offsetX=.15,damage=2,sound='hit',start=dict(marker='rush'),finish=dict(marker='brake'))],
            events=[dict(id=id,at=dict(marker=id),**(dict(sound='pterosaur-'+id) if id in ('screech','rush','brake','reset') else {})) for id in times])])
    save('entities',entity)
if __name__=='__main__':main()
