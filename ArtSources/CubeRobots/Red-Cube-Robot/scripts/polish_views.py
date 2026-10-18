import bpy, json
from pathlib import Path
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');s=bpy.context.scene
for ob in bpy.data.collections['ROBOT | Model'].objects:
    if ob.name.startswith('Rear grille | Recess'):
        ob.location.y=.099
    if ob.name.startswith('Exhaust '):ob.location.z=.06
# Correct the labeled left camera to show the front on the image's left.
from mathutils import Vector
cam=bpy.data.objects['Camera | 03 Left'];cam.location=(7,0,1.08);cam.rotation_euler=(Vector((0,0,1.08))-cam.location).to_track_quat('-Z','Y').to_euler()
# Emission-only pixels keep a clean luminous pink independent of studio reflections.
m=bpy.data.materials['08 | Pink luminous pixels'];m.node_tree.nodes.clear()
em=m.node_tree.nodes.new('ShaderNodeEmission');em.inputs['Color'].default_value=(1.0,.20,.34,1);em.inputs['Strength'].default_value=1.7
out=m.node_tree.nodes.new('ShaderNodeOutputMaterial');m.node_tree.links.new(em.outputs[0],out.inputs['Surface'])
s.compositing_node_group.nodes['Glare'].inputs['Threshold'].default_value=.8
s.compositing_node_group.nodes['Glare'].inputs['Strength'].default_value=.16
# Give orthographic checks a white background with the ground hidden.
nt=s.compositing_node_group
alpha=nt.nodes.get('Alpha Over') or nt.nodes.new('CompositorNodeAlphaOver');alpha.inputs['Factor'].default_value=1;alpha.inputs['Background'].default_value=(1,1,1,1)
nt.links.new(nt.nodes['Glare'].outputs['Image'],alpha.inputs['Foreground']);nt.links.new(alpha.outputs['Image'],nt.nodes['Group Output'].inputs['Image'])
s.render.film_transparent=True
ground=bpy.data.objects['Studio | Ground']
for camera,filename,ground_on in [('Camera | 01 Concept','01-concept.png',True),('Camera | 02 Front','02-front.png',False),('Camera | 03 Left','03-left.png',False),('Camera | 04 Rear','04-rear.png',False),('Camera | 05 Rear three quarter','05-rear-angle.png',True)]:
    ground.hide_render=not ground_on;s.camera=bpy.data.objects[camera];s.render.filepath=str(ROOT/'previews'/filename);bpy.ops.render.render(write_still=True)
ground.hide_render=False;s.camera=bpy.data.objects['Camera | 01 Concept']
result={'status':'Five updated views rendered'}
