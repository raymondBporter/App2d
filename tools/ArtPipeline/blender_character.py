"""Shared Blender-side utilities for the RGS Grease Pencil character."""
from pathlib import Path
import bpy

REPOSITORY = Path(__file__).resolve().parents[2]
ORIGINAL_BLEND = REPOSITORY / 'Assets/Sources/third-party/rgs-stick-figure/sticker character.blend'


def open_original():
    bpy.ops.wm.open_mainfile(filepath=str(ORIGINAL_BLEND), use_scripts=False)
    rig = bpy.data.objects['Armature']
    gp = bpy.data.objects['Stickman']
    for track in rig.animation_data.nla_tracks:
        track.mute = True
        track.is_solo = False
    rig.animation_data.action = bpy.data.actions['idleSword']
    rig.animation_data.action_blend_type = 'REPLACE'
    rig.animation_data.action_influence = 1
    for driver in list(gp.animation_data.drivers):
        if 'grease_pencil_modifiers' in driver.data_path:
            gp.animation_data.drivers.remove(driver)
    bpy.context.scene.frame_set(1)
    # The 4.1 -> 5.2 conversion applies the armature modifier to details that
    # already follow bones via layer parenting. Isolate those original drawings.
    details = gp.copy()
    details.data = gp.data.copy()
    details.name = 'Face and weapons - bone parented'
    bpy.context.collection.objects.link(details)
    details.modifiers.remove(details.modifiers['Armature'])
    for layer in list(details.data.layers):
        if not layer.parent:
            details.data.layers.remove(layer)
    for layer in list(gp.data.layers):
        if layer.parent:
            gp.data.layers.remove(layer)
    bpy.context.view_layer.update()
    return rig


def render_profile():
    s = bpy.context.scene
    camera = s.camera
    return {
        'resolution': [512, 512], 'engine': 'BLENDER_EEVEE',
        'camera': {'name': camera.name, 'type': camera.data.type,
                   'lens': camera.data.lens, 'sensorWidth': camera.data.sensor_width,
                   'sensorFit': camera.data.sensor_fit, 'location': list(camera.location),
                   'rotation': list(camera.rotation_euler)},
        'viewTransform': s.view_settings.view_transform, 'look': s.view_settings.look,
        'exposure': s.view_settings.exposure, 'gamma': s.view_settings.gamma,
        'displayDevice': s.display_settings.display_device,
    }


def apply_render_profile(profile):
    s = bpy.context.scene
    s.render.resolution_x, s.render.resolution_y = profile['resolution']
    s.render.resolution_percentage = 100
    s.render.engine = profile['engine']
    s.render.film_transparent = True
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.render.image_settings.color_depth = '8'
    spec = profile['camera']
    camera = bpy.data.objects[spec['name']]
    s.camera = camera
    camera.data.type = spec['type']
    camera.data.lens = spec['lens']
    camera.data.sensor_width = spec['sensorWidth']
    camera.data.sensor_fit = spec['sensorFit']
    camera.location = spec['location']
    camera.rotation_euler = spec['rotation']
    s.view_settings.view_transform = profile['viewTransform']
    s.view_settings.look = profile['look']
    s.view_settings.exposure = profile['exposure']
    s.view_settings.gamma = profile['gamma']
    s.display_settings.display_device = profile['displayDevice']


def action_curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                yield from bag.fcurves
