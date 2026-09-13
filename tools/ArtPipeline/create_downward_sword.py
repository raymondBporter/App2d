"""Author an immediate six-pose downward thrust using the original drawn FX.

Run in Blender. Re-render saved edits with render_blender_character.py instead.
"""
import sys,math,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
import bpy
from mathutils import Matrix,Vector
from blender_character import REPOSITORY,action_curves,apply_render_profile

parent=REPOSITORY/'Assets/Sources/characters/player-sword'
out=parent/'downward-attack'
out.mkdir(parents=True,exist_ok=True)
target=out/'sword-downward.blend'
if target.exists() and '--replace' not in sys.argv:
    raise FileExistsError('Source already exists. Re-render it, or pass --replace to regenerate the initial poses.')
parent_config=json.loads((parent/'render.json').read_text())
bpy.ops.wm.open_mainfile(filepath=str(parent/parent_config['blend']),use_scripts=False)
apply_render_profile(parent_config['profile'])
rig=bpy.data.objects['Armature'];s=bpy.context.scene
for t in rig.animation_data.nla_tracks:t.mute=True;t.is_solo=False
rig.animation_data.action=bpy.data.actions['airAttackSword']
s.frame_set(2)
original_fx=rig.pose.bones['slashEffect'].matrix.copy()
details=next(o for o in bpy.data.objects if o.type=='GREASEPENCIL' and o.data.layers.get('punchFX'))
fx_layer=details.data.layers['punchFX']
fx_points=fx_layer.frames[3].drawing.strokes[0].points
# In the authored fourth sweep drawing, these are the two corners of its
# straight leading edge. Its pointed trailing tip is vertex 64.
fx_inner=original_fx@fx_layer.matrix_parent_inverse@Vector(fx_points[110].position)
fx_outer=original_fx@fx_layer.matrix_parent_inverse@Vector(fx_points[0].position)
sword_layer=details.data.layers['sword1']
s.frame_set(3)
base={b.name:b.matrix.copy() for b in rig.pose.bones}
basis={b.name:b.matrix_basis.copy() for b in rig.pose.bones}
rig.animation_data.action=None


def rotate(matrix,degrees,pivot):
    return Matrix.Translation(pivot)@Matrix.Rotation(math.radians(degrees),4,'Y')@Matrix.Translation(-pivot)@matrix


def put(name,point):
    matrix=base[name].copy();matrix.translation=Vector(point)
    rig.pose.bones[name].matrix=matrix


# Hand positions are in armature space (screen X / depth / vertical Z).
# Blade angle is measured counterclockwise from screen-right.
poses=[
    dict(label='Immediate stab',lean=8,drop=-.10,hand=(.20,0,-.10),blade=-91,tuck=.33,fx=(.28,.30)),
    dict(label='Impact',lean=10,drop=-.12,hand=(.20,0,-.10),blade=-90,tuck=.34,fx=(.48,.18)),
    dict(label='Hold down',lean=6,drop=-.07,hand=(.20,0,-.07),blade=-90,tuck=.30,fx=None),
    dict(label='Release',lean=3,drop=-.02,hand=(.20,0,.18),blade=-90,tuck=.26,fx=None),
    dict(label='Retract',lean=1,drop=.02,hand=(.20,0,.54),blade=-89,tuck=.22,fx=None),
    dict(label='Airborne guard',lean=-3,drop=.04,hand=(-.55,0,.66),blade=128,tuck=.12,fx=None),
]
action=bpy.data.actions.new('sword-down-attack')
action.use_fake_user=True
rig.animation_data.action=action
trace=[]
for frame,p in enumerate(poses,1):
    s.frame_set(frame)
    for bone in rig.pose.bones:bone.matrix_basis=basis[bone.name].copy()
    bpy.context.view_layer.update()
    hip=base['b_bony'].translation.copy()
    body=rotate(base['b_bony'],p['lean'],hip)
    body.translation+=Vector((.025 if frame<=3 else 0,0,p['drop']))
    rig.pose.bones['b_bony'].matrix=body
    bpy.context.view_layer.update()
    head=rig.pose.bones['head']
    head.matrix=rotate(head.matrix,-p['lean']*.35,head.head)
    bpy.context.view_layer.update()
    chest=body@base['b_bony'].inverted()
    for side in ['L','R']:
        point=chest@base['shoulder'+side].translation
        if side=='R':point.x+=.10 if frame<=3 else 0
        put('shoulder'+side,point)
    shoulder=rig.pose.bones['shoulderR'].head.copy()
    hand=Vector(p['hand'])
    elbow=shoulder.lerp(hand,.54)
    elbow.z-=.12
    put('armR',elbow)
    put('handR',hand)
    bpy.context.view_layer.update()
    # The source's air-attack end pose points about 8 degrees below horizontal.
    # Rotate its rigid, bone-parented sword around the hand, keeping the grip.
    weapon=rig.pose.bones['handRWeapon']
    weapon.matrix=rotate(weapon.matrix,-(p['blade']+8),hand)
    weapon['HandRWeapon']=1
    shoulder=rig.pose.bones['shoulderL'].head.copy()
    if frame<len(poses):
        brace=hand+Vector((.10,.025,.10))
        elbow=shoulder.lerp(brace,.48)+Vector((.13,0,.03))
        put('armL',elbow)
        put('handL',brace)
    else:
        put('armL',shoulder+Vector((.27,0,-.10)))
        put('handL',shoulder+Vector((.50,0,-.22)))
    # Both feet fold back while the hands drive the blade below the body.
    put('kneeL',Vector((.42,0,.14+p['tuck']*.40+p['drop'])))
    put('footL',Vector((.12,0,-.10+p['tuck']+p['drop'])))
    put('kneeR',Vector((-.18,0,.10+p['tuck']*.35+p['drop'])))
    put('footR',Vector((-.40,0,-.10+p['tuck']+p['drop'])))
    # Bone.matrix is evaluated lazily; update before reading the rotated blade
    # to fit the sweep to its tip rather than the previous evaluated orientation.
    bpy.context.view_layer.update()
    fx=rig.pose.bones['slashEffect']
    fx['slashFX']=4 if p['fx'] else 0
    if p['fx']:
        inner_fraction,trail_width=p['fx']
        # Align the drawn leading edge to the actual transformed sword tip.
        blade_direction=Vector((math.cos(math.radians(p['blade'])),0,math.sin(math.radians(p['blade']))))
        sword_matrix=weapon.matrix@sword_layer.matrix_parent_inverse
        blade_points=[sword_matrix@Vector(pt.position) for stroke in sword_layer.frames[0].drawing.strokes for pt in stroke.points]
        tip=max(blade_points,key=lambda point:(point-hand).dot(blade_direction))
        tip.y=hand.y
        target_inner=hand.lerp(tip,inner_fraction)
        target_outer=hand.lerp(tip,.98)
        target_inner.y=.16
        source_axis=(fx_outer-fx_inner);source_axis.y=0
        target_axis=(target_outer-target_inner);target_axis.y=0
        scale=target_axis.length/source_axis.length
        u=source_axis.normalized();v=target_axis.normalized();depth=Vector((0,1,0))
        source_frame=Matrix((u,depth,Vector((-u.z,0,u.x)))).transposed().to_4x4()
        target_frame=Matrix((v,depth,Vector((-v.z,0,v.x)))).transposed().to_4x4()
        stretch=Matrix.Diagonal((scale,1,scale*trail_width,1))
        fx.matrix=Matrix.Translation(target_inner)@target_frame@stretch@source_frame.inverted()@Matrix.Translation(-fx_inner)@original_fx
    else:fx.matrix=original_fx
    bpy.context.view_layer.update()
    for bone in rig.pose.bones:
        for field in ['location','rotation_quaternion','scale']:
            bone.keyframe_insert(field,frame=frame,group=bone.name)
    fx.keyframe_insert('["slashFX"]',frame=frame,group=fx.name)
    weapon.keyframe_insert('["HandRWeapon"]',frame=frame,group=weapon.name)
    trace.append({'frame':frame,'label':p['label'],'bladeAngleDegrees':p['blade'],'sweepVisible':bool(p['fx'])})
for curve in action_curves(action):
    for key in curve.keyframe_points:key.interpolation='CONSTANT'
s.render.fps=24;s.render.fps_base=1;s.frame_start=1;s.frame_end=len(poses);s.frame_set(1)
s.render.filepath='//rendered/sword-down-attack/frame-'
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(target))
config={'schemaVersion':1,'characterId':'player-sword','blend':target.name,'rig':rig.name,
        'profile':parent_config['profile'],'clips':{'sword-down-attack':{'action':action.name,
            'frames':list(range(1,len(poses)+1)),'loop':False,'durationSeconds':len(poses)/24}},'references':{}}
(out/'render.json').write_text(json.dumps(config,indent=2)+'\n')
(out/'poses.json').write_text(json.dumps(trace,indent=2)+'\n')
print('AUTHORED',target)
