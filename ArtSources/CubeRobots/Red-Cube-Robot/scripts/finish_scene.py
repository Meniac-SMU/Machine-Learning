import bpy
from pathlib import Path
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot')
scene=bpy.context.scene
nt=scene.compositing_node_group;glow=nt.nodes.get('Glare')
glow.inputs['Type'].default_value='Fog Glow';glow.inputs['Quality'].default_value='High'
glow.inputs['Threshold'].default_value=1.2;glow.inputs['Size'].default_value=.28;glow.inputs['Strength'].default_value=.35
nt.links.new(nt.nodes.get('Render Layers').outputs['Image'],glow.inputs['Image'])
nt.links.new(glow.outputs['Image'],nt.nodes.get('Group Output').inputs['Image'])
body=bpy.data.objects['Body | Exact 2 x 2 x 2 cube']
bpy.context.view_layer.objects.active=body
for mod in list(body.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
bpy.context.view_layer.update();body.dimensions=(2,2,2)
for o in scene.objects:o.select_set(False)
body.select_set(True);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
hero=bpy.data.objects['Camera | 01 Concept']
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_rotation=hero.rotation_euler.to_quaternion()
            ar.spaces.active.region_3d.view_distance=5.3;ar.spaces.active.region_3d.view_location=(0,0,1.05)
            ar.spaces.active.region_3d.view_perspective='ORTHO'
            ar.spaces.active.shading.type='MATERIAL';ar.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Red_Cube_Robot_v01.blend'))
prefs=bpy.context.preferences.addons['cycles'].preferences
try:
    prefs.compute_device_type='OPTIX';prefs.get_devices()
    for d in prefs.devices:d.use=(d.type=='OPTIX')
    if any(d.type=='OPTIX' for d in prefs.devices):scene.cycles.device='GPU'
except Exception:pass
scene.render.resolution_percentage=80
bpy.ops.render.render(write_still=True)
result={'file':bpy.data.filepath,'preview':scene.render.filepath,'body_dimensions':list(body.dimensions),'render_device':scene.cycles.device}
