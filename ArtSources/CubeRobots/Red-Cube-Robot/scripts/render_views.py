import bpy,json
from pathlib import Path
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');s=bpy.context.scene
for cam,filename in [('Camera | 02 Front','02-front.png'),('Camera | 03 Left','03-left.png'),('Camera | 04 Rear','04-rear.png'),('Camera | 05 Rear three quarter','05-rear-angle.png')]:
    s.camera=bpy.data.objects[cam];s.render.filepath=str(ROOT/'previews'/filename);bpy.ops.render.render(write_still=True)
s.camera=bpy.data.objects['Camera | 01 Concept']
p=bpy.data.materials['08 | Pink luminous pixels'].node_tree.nodes.get('Principled BSDF')
result={'previews':str(ROOT/'previews'),'eyes':{k:list(p.inputs[k].default_value) for k in ['Base Color','Emission Color']},'strength':p.inputs['Emission Strength'].default_value,'view':s.view_settings.view_transform}
