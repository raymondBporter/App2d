"""Author editable caveman meshes and timed rig curves. Run explicitly; never part of automatic asset builds."""
import copy
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / 'Assets/Characters/authored'

def load(folder, name):
    return json.loads((ROOT / folder / (name + '.json')).read_text())

def save(folder, asset):
    (ROOT / folder / (asset['id'] + '.json')).write_text(json.dumps(asset, indent=2) + '\n')

def extrude(points, fill, depth=.09):
    n = len(points)
    vertices = [dict(x=x, y=y, z=z) for z in [-depth/2, depth/2] for x, y in points]
    indices = []
    for i in range(1, n-1):
        indices += [0, i+1, i, n, n+i, n+i+1]
    for i in range(n):
        j = (i+1) % n
        indices += [i, j, n+j, i, n+j, n+i]
    return dict(vertices=vertices, triangles=indices, fill=fill,
                outline=[dict(x=x, y=y, z=0) for x,y in points], thickness=depth)

def main():
    variant = load('variants', 'cinder')
    variant.update(id='club-caveman-build', name='Club caveman')
    variant['parts']['body']['fill'] = '#d7a348'
    save('variants', variant)
    for source, target in [('cinder-hide-wrap', 'club-caveman-hide-wrap'), ('brute-hair', 'club-caveman-hair'), ('brute-beard', 'club-caveman-beard')]:
        art = load('props', source)
        art.update(id=target, name=target.replace('-', ' ').title())
        for solid in art['solids']:
            if solid['fill'] == '#b97549': solid['fill'] = '#d7a348'
        save('props', art)
    club = dict(format='app2d-prop', version=1, id='wooden-club', name='Heavy wooden club', ink='#392b23', lineWidth=.025,
                tip=dict(x=.98, y=0, z=0), shapes=[], solids=[extrude([
                    (-.14,-.04), (.35,-.055), (.68,-.16), (.94,-.14), (1.06,-.07),
                    (1.08,.05), (.99,.13), (.72,.17), (.35,.055), (-.14,.04)], '#895a32')])
    save('props', club)
    slam = load('animations', 'person-hammer-slam')
    slam.update(id='club-caveman-slam', name='Club caveman / overhead slam', duration=1.8)
    # Preserve solved rig geometry. Piecewise timing gives a slow lift, a held tell,
    # an 80 ms strike, and a half-second planted recovery before standing again.
    def time(t):
        if t <= .7: return t
        if t <= .95: return t
        return .95 + (t-.95) * (.85/.55)
    for track in slam['tracks']:
        keys = track['keys']
        for key in keys: key['time'] = round(time(key.get('time', 0)), 5)
        held = next((k for k in keys if k['time'] == .95), None)
        if held:
            hold = copy.deepcopy(held); hold['time'] = 1.35
            keys.insert(len(keys)-1, hold)
    for contact in slam['contacts']: contact['finish'] = 1.8
    save('animations', slam)
    entity = load('entities', 'maul-brute')
    entity.update(id='club-caveman', name='Club caveman', model=variant['id'], motionSet='standard', health=9, mass=1)
    entity['roles'] = {'walk': 'person-run', 'hit': 'person-hit', 'death': 'person-death'}
    entity['controller'] = dict(walkSpeed=2.5, range=1.15, cooldown=.35, respectTerrain=True, verticalRange=.75)
    entity['movement'] = load('entities', 'cinder-gunner')['movement']
    entity['equipment'] = [dict(prop='wooden-club', socket='sword-hand')] + [dict(prop='club-caveman-'+p, socket=s) for p,s in [('hide-wrap','body-art'),('hair','head-art'),('beard','head-art')]]
    action = entity['actions'][0]
    action['clip'] = slam['id']
    hit = action['hits'][0]
    hit.update(id='club-head', prop='wooden-club', width=.7, height=.65, damage=3, sound='heavy')
    action['events'].insert(0, dict(id='club-lift', at=dict(marker='windup'), sound='club-windup'))
    save('entities', entity)

if __name__ == '__main__': main()
