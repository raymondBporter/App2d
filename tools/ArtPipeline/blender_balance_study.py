"""Run with Blender --background --factory-startup --python this_file.

An isolated animation experiment. Opens the original source read-only, repairs
Grease Pencil double deformation, creates three editable actions, and renders.
"""
import bpy
import math
import json
import sys
from pathlib import Path
from mathutils import Matrix, Vector, Quaternion

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Assets/Library/characters/player-sword/balance-study'
OUT.mkdir(parents=True, exist_ok=True)
SOURCE = ROOT / 'Assets/Sources/third-party/rgs-stick-figure/sticker character.blend'
bpy.ops.wm.open_mainfile(filepath=str(SOURCE), use_scripts=False)
s = bpy.context.scene
rig = bpy.data.objects['Armature']
gp = bpy.data.objects['Stickman']
for track in rig.animation_data.nla_tracks:
    track.mute = True
rig.animation_data.action = bpy.data.actions['idleSword']
rig.animation_data.action_blend_type = 'REPLACE'
rig.animation_data.action_influence = 1
for driver in list(gp.animation_data.drivers):
    if 'grease_pencil_modifiers' in driver.data_path:
        gp.animation_data.drivers.remove(driver)
s.frame_set(1)
# In 5.2, the armature modifier also deforms already bone-parented details.
# Preserve the original drawings and parenting; isolate them from that modifier.
details = gp.copy()
details.data = gp.data.copy()
details.name = 'Face and sword - bone parented'
bpy.context.collection.objects.link(details)
details.modifiers.remove(details.modifiers['Armature'])
for layer in list(details.data.layers):
    if not layer.parent:
        details.data.layers.remove(layer)
for layer in list(gp.data.layers):
    if layer.parent:
        gp.data.layers.remove(layer)

bpy.context.view_layer.update()
base = {b.name: b.matrix.copy() for b in rig.pose.bones}
basis = {b.name: b.matrix_basis.copy() for b in rig.pose.bones}
rig.animation_data.action = None

def reset():
    for b in rig.pose.bones:
        b.matrix_basis = basis[b.name].copy()
    bpy.context.view_layer.update()

def position(name, p):
    m = base[name].copy()
    m.translation = Vector(p)
    rig.pose.bones[name].matrix = m

def rotate_at(m, angle, pivot):
    return Matrix.Translation(pivot) @ Matrix.Rotation(angle, 4, 'Y') @ Matrix.Translation(-pivot) @ m

def smooth_keys(t, keys):
    for (a, va), (b, vb) in zip(keys, keys[1:]):
        if a <= t <= b:
            u = (t-a)/(b-a)
            u = u*u*(3-2*u)
            return va+(vb-va)*u
    return keys[-1][1]

def pose(kind, t):
    reset()
    if kind == 'standing':
        return
    # Balance on the rear foot at a right-hand edge; the forward foot hangs free.
    phase = 2*math.pi*t
    if kind == 'balance-subtle':
        lean = math.radians(4 + 3.5*math.sin(phase) + .9*math.sin(2*phase+.4))
        lift = .17 + .035*math.sin(phase-.7)
        arm_angle = math.radians(-20 + 13*math.sin(phase-.9))
        sword_angle = math.radians(-8 - 5*math.sin(phase-.5))
    else:
        lean = math.radians(smooth_keys(t,[(0,3),(.16,13),(.32,16),(.49,-5),(.65,9),(.82,-2),(1,3)]))
        lift = smooth_keys(t,[(0,.17),(.22,.34),(.42,.22),(.63,.30),(.84,.18),(1,.17)])
        arm_angle = math.radians(smooth_keys(t,[(0,-25),(.13,15),(.26,125),(.38,235),(.49,335),(.60,420),(.71,530),(.84,695),(1,695)]))
        sword_angle = math.radians(smooth_keys(t,[(0,-8),(.2,-28),(.38,8),(.53,14),(.71,-18),(1,-8)]))
    hip = base['b_bony'].translation.copy()
    shift = Vector((-.11, 0, -.015 + .015*math.cos(phase)))
    body = rotate_at(base['b_bony'], lean, hip)
    body.translation += shift
    rig.pose.bones['b_bony'].matrix = body
    bpy.context.view_layer.update()
    # Head follows chest but counter-rotates slightly, with delayed correction.
    head = rig.pose.bones['head']
    head.rotation_quaternion = head.rotation_quaternion @ Quaternion((0,0,1), lean*.5)
    bpy.context.view_layer.update()
    rot = Matrix.Rotation(lean, 3, 'Y')
    shoulders = {}
    for side in ['L','R']:
        p = rot @ (base['shoulder'+side].translation-hip) + hip + shift
        shoulders[side] = p
        position('shoulder'+side, p)
    # Free arm traces an arc around the shoulder; elbow leads the wrist.
    sh = shoulders['L']
    if kind == 'balance-subtle':
        elbow = sh + Vector((.35*math.cos(arm_angle-.23),0,.35*math.sin(arm_angle-.23)))
        wrist = elbow + Vector((.30*math.cos(arm_angle+.22),0,.30*math.sin(arm_angle+.22)))
    else:
        # Circle the forearm beside the silhouette; an overhead swing disappears
        # completely behind this character's oversized head.
        effort = smooth_keys(t,[(0,0),(.15,1),(.74,1),(1,0)])
        elbow = sh + Vector((.34 + .10*effort,0,-.17+.05*math.sin(arm_angle)))
        wrist = elbow + Vector((.31*math.cos(arm_angle),0,.31*math.sin(arm_angle)))
    position('armL', elbow)
    position('handL', wrist)
    # Sword arm is a counterweight, with a smaller lagging wrist correction.
    sh = shoulders['R']
    a = -.36 + sword_angle
    elbow = sh + Vector((-.33*math.cos(a),0,-.33*math.sin(-a)))
    wrist = elbow + Vector((-.14,0,-.24))
    position('armR', elbow)
    position('handR', wrist)
    bpy.context.view_layer.update()
    weapon = rig.pose.bones['handRWeapon']
    weapon.rotation_quaternion = weapon.rotation_quaternion @ Quaternion((0,0,1), -sword_angle*.65)
    # The support target stays exact. The other leg searches for purchase.
    p = base['kneeR'].translation.copy(); p.x += -.04 + .03*math.sin(phase); p.z += .01
    position('kneeR', p)
    position('footR', base['footR'].translation)
    p = base['kneeL'].translation.copy(); p.x -= .11; p.z += lift*.7
    position('kneeL', p)
    p = base['footL'].translation.copy(); p.x -= .10 + .035*math.sin(phase-.3); p.z += lift
    position('footL', p)
    bpy.context.view_layer.update()

s.render.fps = 24
s.render.resolution_x = 512
s.render.resolution_y = 512
s.render.resolution_percentage = 100
s.render.film_transparent = True
s.render.image_settings.file_format = 'PNG'
s.render.image_settings.color_mode = 'RGBA'
s.render.engine = 'BLENDER_EEVEE'
# Reframe for judging the artwork; this is deliberately not runtime sizing.
s.camera.data.type = 'ORTHO'
s.camera.data.ortho_scale = 4.2
s.camera.location.x = -.02
s.camera.location.z = -.47
s.view_settings.view_transform = 'Standard'
specs = [('standing',1),('balance-subtle',72),('balance-windmill',72)]
manifest = {'fps':24,'source':str(SOURCE.relative_to(ROOT)),'animations':{}}
for kind, count in specs:
    action = bpy.data.actions.new('Study - '+kind)
    action.use_fake_user = True
    rig.animation_data.action = action
    for i in range(count+1):
        frame=i+1
        s.frame_set(frame)
        pose(kind,i/count)
        for b in rig.pose.bones:
            b.keyframe_insert('location',frame=frame,group=b.name)
            b.keyframe_insert('rotation_quaternion',frame=frame,group=b.name)
            b.keyframe_insert('scale',frame=frame,group=b.name)
    folder=OUT/kind
    folder.mkdir(exist_ok=True)
    # Preview flag renders only selected poses for a fast inspection pass.
    frames = [1] if kind=='standing' else ([1,13,25,37,49,61] if '--poses-only' in sys.argv else range(1,count+1))
    for frame in frames:
        s.frame_set(frame)
        s.render.filepath=str(folder/f'frame-{frame:04d}.png')
        bpy.ops.render.render(write_still=True)
    manifest['animations'][kind]={'action':action.name,'frames':count,'loop':kind!='standing'}
rig.animation_data.action=bpy.data.actions['Study - balance-subtle']
s.frame_start=1; s.frame_end=72; s.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'sword-balance-study.blend'))
(OUT/'manifest.json').write_text(json.dumps(manifest,indent=2))
print('STUDY_COMPLETE',OUT)
