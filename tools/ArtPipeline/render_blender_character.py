"""Render the saved editable .blend, preserving all artist edits.

blender --background --factory-startup --python-exit-code 1 --python this_file
Optional arguments after --: --config path/to/render.json
"""
import argparse
import hashlib
import json
import shutil
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from bpy_extras.object_utils import world_to_camera_view
from blender_character import REPOSITORY, apply_render_profile

parser = argparse.ArgumentParser()
parser.add_argument('--config',type=Path,default=REPOSITORY/'Assets/Sources/characters/player-sword/render.json')
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
config_path = args.config.resolve()
config = json.loads(config_path.read_text(encoding='utf-8'))
source = config_path.parent
blend = source/config['blend']
bpy.ops.wm.open_mainfile(filepath=str(blend),use_scripts=False)
apply_render_profile(config['profile'])
s = bpy.context.scene
rig = bpy.data.objects[config['rig']]
for track in rig.animation_data.nla_tracks:
    track.mute = True
    track.is_solo = False
rig.animation_data.action_blend_type = 'REPLACE'
rig.animation_data.action_influence = 1
stage = REPOSITORY/'Assets/Work/blender-character-export'/config['characterId']
stage.mkdir(parents=True,exist_ok=True)
pending = []
report = {'schemaVersion':1,'blenderVersion':bpy.app.version_string,
          'blendSha256':hashlib.sha256(blend.read_bytes()).hexdigest(),
          'configSha256':hashlib.sha256(config_path.read_bytes()).hexdigest(),
          'clips':{},'references':{},'files':{}}


def render(relative, frame):
    s.frame_set(frame)
    path = stage/relative
    path.parent.mkdir(parents=True,exist_ok=True)
    s.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    pending.append((path,source/'rendered'/relative))
    report['files'][relative.as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()


for name, spec in config['clips'].items():
    rig.animation_data.action = bpy.data.actions[spec['action']]
    s.frame_set(spec['frames'][0])
    start = {b.name:b.matrix.copy() for b in rig.pose.bones}
    bone = rig.pose.bones[spec['supportBone']] if 'supportBone' in spec else None
    support_start = bone.head.copy() if bone else None
    screen = world_to_camera_view(s,s.camera,rig.matrix_world@bone.head) if bone else None
    max_drift = 0
    for index, frame in enumerate(spec['frames'],1):
        render(Path(name)/f'frame-{index:04d}.png',frame)
        if bone:
            max_drift = max(max_drift,(bone.head-support_start).length)
    loop_error = 0
    if 'loopClosingFrame' in spec:
        s.frame_set(spec['loopClosingFrame'])
        loop_error = max(abs(start[b.name][row][col]-b.matrix[row][col])
                         for b in rig.pose.bones for row in range(4) for col in range(4))
    if max_drift>1e-5 or loop_error>1e-5:
        raise ValueError(f'{name}: planted foot moved ({max_drift}) or loop does not close ({loop_error}). Fix the saved action.')
    report['clips'][name] = {'frameCount':len(spec['frames']),
        'supportBoneMaxDrift':max_drift if bone else None,
        'loopPoseMaxError':loop_error if 'loopClosingFrame' in spec else None,
        'supportPixel':[screen.x*s.render.resolution_x,(1-screen.y)*s.render.resolution_y] if screen else None}
for name,spec in config.get('references',{}).items():
    rig.animation_data.action = bpy.data.actions[spec['action']]
    relative = Path('references')/f'{name}.png'
    render(relative,spec['frame'])
    report['references'][name] = {'file':relative.as_posix(),'packFile':spec['packFile']}
# Publish only after all rendering and pose checks succeed. Re-rendering never
# saves over the editable blend. Old cached frames outside the recipe are removed.
for staged,destination in pending:
    destination.parent.mkdir(parents=True,exist_ok=True)
    shutil.copy2(staged,destination)
for name,spec in config['clips'].items():
    expected = {f'frame-{i:04d}.png' for i in range(1,len(spec['frames'])+1)}
    for stale in (source/'rendered'/name).glob('frame-*.png'):
        if stale.name not in expected:
            stale.unlink()
(source/'rendered/render-report.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('RENDERED',len(pending),'frames from saved source:',blend)
