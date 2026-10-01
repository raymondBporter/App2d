"""Explicitly seed editable shell-shield defender assets; not an automatic build step."""
import copy
from build_club_caveman import load, save, extrude

def main():
    variant=load('variants','club-caveman-build')
    variant.update(id='shield-defender-build',name='Shell-shield defender')
    save('variants',variant)
    for suffix in ('hair','beard','hide-wrap'):
        prop=load('props','club-caveman-'+suffix)
        prop.update(id='shield-defender-'+suffix,name='Shell defender / '+suffix)
        save('props',prop)
    outline=[(-.12,-.7),(.22,-.65),(.43,-.4),(.48,-.05),(.38,.37),(.17,.65),(-.12,.7),(-.38,.45),(-.46,.08),(-.38,-.4)]
    shell=dict(format='app2d-prop',version=1,id='scavenged-shell',name='Scavenged shell shield',ink='#303b32',lineWidth=.03,
               tip=dict(x=0,y=0,z=0),shapes=[],solids=[extrude(outline,'#4b6653',.10)])
    inner=extrude([(x*.82,y*.85) for x,y in outline],'#91a276',.025)
    for p in inner['vertices']+inner['outline']:p['z']-=.075
    shell['solids'].append(inner)
    for points in [[(-.28,-.32),(-.12,-.05),(.1,-.15),(.16,-.46),(-.08,-.52)],
                   [(-.1,.02),(-.18,.32),(.05,.48),(.27,.26),(.12,-.08)]]:
        plate=extrude(points,'#738e65',.01)
        for p in plate['vertices']+plate['outline']:p['z']-=.095
        shell['solids'].append(plate)
    save('props',shell)
    def key(t,x=0,y=0):return dict(time=t,x=x,y=y,ease='smooth')
    for role in ('idle','walk'):
        clip=load('animations','person-'+role)
        clip.update(id='shield-defender-'+role,name='Shell defender / '+role)
        clip['tracks']=[t for t in clip['tracks'] if t['target'] not in ('left-arm','left-shoulder')]
        clip['tracks'].append(dict(kind='target',target='left-arm',keys=[key(0,.5,.12),key(clip['duration'],.5,.12)]))
        save('animations',clip)
    bash=load('animations','person-idle')
    bash.update(id='shield-defender-bash',name='Shell defender / pullback bash recovery',duration=1.4,loop=False)
    bash['travel']['keys']=[]
    bash['tracks']=[dict(kind='target',target='left-arm',keys=[key(0,.5,.12),key(.25,-.35,.22),key(.45,-.35,.22),key(.55,.78,.3),key(.7,.78,.3),key(.95,-.25,.12),key(1.2,-.25,.12),key(1.4,.5,.12)]),
                    dict(kind='target',target='right-arm',keys=[key(0),key(.45,.1,.15),key(.55,.15,.2),key(1.4)]),
                    dict(kind='translate',target='hips',keys=[key(0),key(.45,-.035,-.04),key(.55,.06,-.06),key(.7,.06,-.06),key(1.4)]),
                    dict(kind='rotate',target='chest',keys=[dict(time=t,angle=a,ease='smooth') for t,a in [(0,0),(.45,.1),(.55,-.16),(.7,-.16),(1.4,0)]])]
    bash['contacts']=[dict(chain=leg,finish=1.4) for leg in ('left-leg','right-leg')]
    bash['markers']=[dict(id='pullback',time=.2),dict(id='strike',time=.55),dict(id='recover',time=.7)]
    bash['faces']=[dict(part='head',keys=[dict(expression='focused'),dict(time=.45,expression='determined'),dict(time=.55,expression='angry')])]
    save('animations',bash)
    entity=load('entities','club-caveman')
    entity.update(id='shield-defender',name='Shell-shield defender',model=variant['id'],health=8,mass=1.5,
                  guard=dict(prop='scavenged-shell',width=.9,height=1.4))
    entity['roles'].update(idle='shield-defender-idle',walk='shield-defender-walk')
    entity['controller']=dict(walkSpeed=.8,range=1.1,cooldown=.5,respectTerrain=True,verticalRange=.75)
    entity['equipment']=[dict(prop='scavenged-shell',socket='left-grip')] + [dict(prop='shield-defender-'+p,socket=s) for p,s in
                          [('hide-wrap','body-art'),('hair','head-art'),('beard','head-art')]]
    entity['actions']=[dict(id='attack',clip=bash['id'],hits=[dict(id='shell-bash',prop='scavenged-shell',width=.9,height=1.4,damage=2,sound='hit',
                                start=dict(marker='strike'),finish=dict(marker='recover'))],
                           events=[dict(id='guard-open',at=dict(marker='pullback'),sound='shield-pullback'),
                                   dict(id='bash',at=dict(marker='strike'),sound='shield-bash')])]
    save('entities',entity)

if __name__=='__main__':main()
