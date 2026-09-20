"""Bake tomek's evaluated wolf rig into the existing Hound point contract.

Run with Blender --background --factory-startup --disable-autoexec
--python-exit-code 1 --python tools/CharacterPipeline/export_wolf.py.
The source scene is opened read-only; only the generated cache is written.
"""
import hashlib
import json
import math
from pathlib import Path
import struct

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
MAPPING = Path(__file__).with_name('wolf-mapping.json')
OUT = ROOT / 'Assets/Work/tomek-wolf'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def main():
    cfg = json.loads(MAPPING.read_text())
    source = ROOT / cfg['source']
    if digest(source.read_bytes()) != cfg['sourceSha256']:
        raise ValueError('Wolf source hash changed; inspect the rig before updating the mapping.')
    library = json.loads((ROOT / 'Assets/Characters/quadruped/library.json').read_text())
    names = library['pointNames']
    bpy.ops.wm.open_mainfile(filepath=str(source))
    rig = bpy.data.objects[cfg['rig']]
    scene = bpy.context.scene
    fps = scene.render.fps / scene.render.fps_base
    rig.animation_data_create()
    rig.animation_data.use_nla = False
    for track in rig.animation_data.nla_tracks:
        track.mute = True

    # Same XYZ axes as HoundPose: +X left, -Y forward, +Z up.
    # A single rest calibration is shared by every action (including jumps/death).
    foot_bones = ['Bone.014', 'Bone.015', 'Bone.020', 'Bone.021']
    floor = min((rig.matrix_world @ rig.data.bones[b].tail_local).z for b in foot_bones)
    back = rig.matrix_world @ rig.data.bones['Bone'].head_local
    scale = cfg['standingBackHeight'] / (back.z - floor)
    origin = Vector((back.x, back.y, floor))
    segments = dict(cfg['segments'])
    # Split the five donor tail bones at fixed rest-length distances. This keeps
    # all nine target tail landmarks attached as the source bends and wags.
    chain = cfg['tailChain']
    lengths = [rig.data.bones[b].length for b in chain]
    total = sum(lengths)

    def tail_point(evaluated, fraction):
        distance = fraction * total
        for name, length in zip(chain, lengths):
            if distance <= length + 1e-8:
                bone = evaluated.pose.bones[name]
                return bone.head.lerp(bone.tail, min(1, distance / length))
            distance -= length
        return evaluated.pose.bones[chain[-1]].tail.copy()

    ear_local = {name: [rig.data.bones['head'].matrix_local.inverted() @ Vector(p) for p in ends]
                 for name, ends in cfg['ears'].items()}
    mapped = set(segments) | set(ear_local) | {f'Tail{i}' for i in range(1, cfg['tailSegments'] + 1)}
    if {f'{bone}:{end}' for bone in mapped for end in ('head', 'tail')} != set(names):
        raise ValueError('Wolf mapping does not cover the Hound point contract exactly.')

    def sample(frame):
        scene.frame_set(math.floor(frame), subframe=frame - math.floor(frame))
        evaluated = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
        points = {}
        for name, (donor, start, end) in segments.items():
            bone = evaluated.pose.bones[donor]
            points[name] = [bone.head.lerp(bone.tail, start), bone.head.lerp(bone.tail, end)]
        for i in range(cfg['tailSegments']):
            points[f'Tail{i + 1}'] = [tail_point(evaluated, i / cfg['tailSegments']),
                                    tail_point(evaluated, (i + 1) / cfg['tailSegments'])]
        for name, ends in ear_local.items():
            points[name] = [evaluated.pose.bones['head'].matrix @ p for p in ends]
        result = []
        for name in names:
            bone, end = name.split(':')
            p = (evaluated.matrix_world @ points[bone][end == 'tail'] - origin) * scale
            result.extend(float(v) for v in p)
        if not all(math.isfinite(v) for v in result):
            raise ValueError('Nonfinite wolf pose')
        return result

    clips, data = {}, bytearray()
    for key, entry in cfg['clips'].items():
        # Clear transforms before changing actions so unkeyed channels cannot leak.
        for bone in rig.pose.bones:
            bone.location = (0, 0, 0)
            bone.rotation_quaternion = (1, 0, 0, 0)
            bone.rotation_euler = (0, 0, 0)
            bone.scale = (1, 1, 1)
        action = bpy.data.actions[entry['action']]
        rig.animation_data.action = action
        if action.slots:
            rig.animation_data.action_slot = action.slots[0]
        start, end = action.frame_range
        duration = (end - start) / fps
        count = math.ceil(duration * cfg['sampleHz'])
        frames = [sample(start + (end - start) * i / count) for i in range(count + 1)]
        error = 0
        for i in range(count):
            actual = sample(start + (end - start) * (i + .5) / count)
            midpoint = [(a + b) / 2 for a, b in zip(frames[i], frames[i + 1])]
            error = max(error, max(math.dist(actual[j:j + 3], midpoint[j:j + 3])
                                   for j in range(0, len(actual), 3)))
        seam = max(math.dist(frames[0][j:j + 3], frames[-1][j:j + 3])
                   for j in range(0, len(names) * 3, 3))
        packed = b''.join(struct.pack('<' + 'f' * len(frame), *frame) for frame in frames)
        warnings = []
        if entry['loop'] and seam > .02:
            warnings.append(f'Source loop seam: {seam:.6f} units; no loop repair applied.')
        if error > .001:
            warnings.append(f'Source midpoint error: {error:.6f} units.')
        if key == 'fly':
            warnings.append('Source Fly is an airborne cycle; map to Fall only where appropriate.')
        clips['tomek_wolf_' + key] = dict(
            label='Wolf / ' + key.capitalize(), category='Tomek wolf', source='tomek_wolf',
            sourceAction=entry['action'], suggestedRole=entry['role'], loop=entry['loop'],
            duration=duration, sourceFrames=[start, end], sourceFps=fps,
            sampleCount=len(frames), times=[i * duration / count for i in range(count + 1)],
            byteOffset=len(data), byteLength=len(packed),
            encoding=dict(type='float32-le', layout='sample,point,xyz', origin=[0, 0, 0], step=1),
            loopSeam=seam, midpointError=error, warnings=warnings)
        data.extend(packed)
        print(f'{key}: {duration:.3f}s, {len(frames)} samples, seam {seam:.6f}, midpoint {error:.6f}', flush=True)

    provenance = {k: cfg[k] for k in ('sourceUrl', 'downloadUrl', 'author', 'license', 'sourceSha256')}
    provenance.update(mappingSha256=digest(MAPPING.read_bytes()), exporterSha256=digest(Path(__file__).read_bytes()),
                      blenderVersion=bpy.app.version_string, sourceFps=fps, sampleHz=cfg['sampleHz'],
                      scale=scale, origin=list(origin), rootMotion='retained',
                      notes=['Donor landmarks uniformly scaled to the Hound standing back height.',
                             'Spine, neck, paws and tail subdivided to fit the Hound point contract.',
                             'Donor has no ear bones; ear guides follow head-local rest offsets.',
                             'Jaw/nose animation is not represented by the Hound drawing.'])
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / 'points.bin').write_bytes(data)
    (OUT / 'library.json').write_text(json.dumps(dict(version=1, format='app2d-point-library',
        id='quadruped', label='Quadruped', anatomy='hound', pointNames=names, drawing={},
        clips=clips, dataSha256=digest(data), provenance=provenance), separators=(',', ':')))


if __name__ == '__main__':
    main()
