"""Author the initial balance source. Ordinary re-renders do NOT run this file.

blender --background --factory-startup --python create_sword_balance.py
An existing editable source is protected unless -- --replace is supplied.
"""
import sys
import math
import json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parent))
import bpy
from mathutils import Matrix, Vector, Quaternion
from blender_character import REPOSITORY, open_original, render_profile, action_curves

folder = REPOSITORY / 'Assets/Sources/characters/player-sword'
folder.mkdir(parents=True, exist_ok=True)
target = folder / 'sword-balance.blend'
if target.exists() and '--replace' not in sys.argv:
    raise FileExistsError('Editable source exists. Re-render it with render_blender_character.py. Use --replace only to regenerate its poses.')
rig = open_original()
scene = bpy.context.scene
profile = render_profile()
base = {b.name: b.matrix.copy() for b in rig.pose.bones}
basis = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
rig.animation_data.action = None


def position(name, point):
    matrix = base[name].copy()
    matrix.translation = point
    rig.pose.bones[name].matrix = matrix


def pose(support, t):
    for bone in rig.pose.bones:
        bone.matrix_basis = basis[bone.name].copy()
    bpy.context.view_layer.update()
    phase = math.tau*t
    # Labels describe screen position in the right-facing source drawing.
    # Rig footR is screen-left, and rig footL is screen-right.
    direction = 1 if support == 'left' else -1
    lean = direction*math.radians(4 + 3.5*math.sin(phase) + .9*math.sin(2*phase+.4))
    lift = .17+.035*math.sin(phase-.7)
    hip = base['b_bony'].translation.copy()
    shift = Vector((-.11 if support == 'left' else .13, 0, -.015+.015*math.cos(phase)))
    rotation = Matrix.Rotation(lean, 4, 'Y')
    matrix = Matrix.Translation(hip) @ rotation @ Matrix.Translation(-hip) @ base['b_bony']
    matrix.translation += shift
    rig.pose.bones['b_bony'].matrix = matrix
    bpy.context.view_layer.update()
    head = rig.pose.bones['head']
    head.rotation_quaternion = head.rotation_quaternion @ Quaternion((0,0,1), lean*.5)
    bpy.context.view_layer.update()
    shoulders = {}
    for side in ['L','R']:
        point = rotation.to_3x3() @ (base['shoulder'+side].translation-hip) + hip + shift
        shoulders[side] = point
        position('shoulder'+side, point)
    arm_angle = math.radians(-20 + direction*13*math.sin(phase-.9))
    elbow = shoulders['L'] + Vector((.35*math.cos(arm_angle-.23),0,.35*math.sin(arm_angle-.23)))
    wrist = elbow + Vector((.30*math.cos(arm_angle+.22),0,.30*math.sin(arm_angle+.22)))
    position('armL', elbow)
    position('handL', wrist)
    sword_angle = math.radians(-8-direction*5*math.sin(phase-.5))
    a = -.36+sword_angle
    elbow = shoulders['R'] + Vector((-.33*math.cos(a),0,-.33*math.sin(-a)))
    position('armR', elbow)
    position('handR', elbow+Vector((-.14,0,-.24)))
    bpy.context.view_layer.update()
    weapon = rig.pose.bones['handRWeapon']
    weapon.rotation_quaternion = weapon.rotation_quaternion @ Quaternion((0,0,1), -sword_angle*.65)
    planted, hanging = ('R','L') if support == 'left' else ('L','R')
    point = base['knee'+planted].translation.copy()
    point.x += direction*(-.04+.03*math.sin(phase))
    point.z += .01
    position('knee'+planted, point)
    position('foot'+planted, base['foot'+planted].translation)
    point = base['knee'+hanging].translation.copy()
    point.x -= direction*.11
    point.z += lift*.7
    position('knee'+hanging, point)
    point = base['foot'+hanging].translation.copy()
    point.x -= direction*(.10+.035*math.sin(phase-.3))
    point.z += lift
    position('foot'+hanging, point)
    bpy.context.view_layer.update()


config = {'schemaVersion': 1, 'characterId': 'player-sword', 'blend':target.name,
          'rig':rig.name, 'profile':profile, 'clips':{}}
for support in ['left','right']:
    name = f'balance-{support}-foot'
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    rig.animation_data.action = action
    for i in range(9):
        frame = 1+i*3
        scene.frame_set(frame)
        pose(support, i/8)
        for bone in rig.pose.bones:
            bone.keyframe_insert('location', frame=frame, group=bone.name)
            bone.keyframe_insert('rotation_quaternion', frame=frame, group=bone.name)
            bone.keyframe_insert('scale', frame=frame, group=bone.name)
    # The Blender preview has the same holds as the eight exported frames.
    for curve in action_curves(action):
        for key in curve.keyframe_points:
            key.interpolation = 'CONSTANT'
    config['clips'][name] = {'action':name, 'frames':list(range(1,25,3)),
        'loop':True,'durationSeconds':2.0,'loopClosingFrame':25,'supportBone':'footR' if support=='left' else 'footL',
        'supportSideInRightFacingImage':support,'edgeSide':'right' if support=='left' else 'left'}
config['references'] = {
    'source-idle': {'action':'idleSword','frame':1,'packFile':'Sword sprites/sword_Idle_0001.png'},
    'source-run': {'action':'runSword','frame':3,'packFile':'Sword sprites/sword_run_0019.png'},
    'source-jump': {'action':'jumpSword','frame':2,'packFile':'Sword sprites/sword_jump_0044.png'},
}
rig.animation_data.action = bpy.data.actions['balance-left-foot']
scene.render.fps = 12
scene.render.fps_base = 1
scene.frame_start = 1
scene.frame_end = 24
scene.frame_set(1)
scene.render.filepath = '//rendered/balance-left-foot/frame-'
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(target))
(folder/'render.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
print('AUTHORED',target)
