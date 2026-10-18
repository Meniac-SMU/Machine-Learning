import bpy
from pathlib import Path
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');s=bpy.context.scene
body=bpy.data.objects['Body | Exact 2 x 2 x 2 cube']
bpy.context.view_layer.objects.active=body
mod=body.modifiers.new('Exact X symmetry','MIRROR');mod.use_axis[0]=True;mod.use_bisect_axis[0]=True;mod.use_clip=True;mod.merge_threshold=.000001
bpy.ops.object.modifier_apply(modifier=mod.name)
em=bpy.data.materials['08 | Pink luminous pixels'].node_tree.nodes.get('Emission')
em.inputs['Color'].default_value=(1,.06,.12,1);em.inputs['Strength'].default_value=5
# Keep anatomical L/R names consistent with the front direction -Y.
objs=[o for o in bpy.data.collections['ROBOT | Model'].objects if ('Wheel ' in o.name or 'Bumper ' in o.name) and (' L ' in o.name or ' R ' in o.name)]
renames={o:o.name.replace(' L ',' __TMP__ ').replace(' R ',' L ').replace(' __TMP__ ',' R ') for o in objs}
for o in objs:o.name='SWAP '+o.name
for o,name in renames.items():o.name=name
s.render.filepath=str(ROOT/'previews'/'01-concept.png');bpy.ops.render.render(write_still=True)
result={'preview':s.render.filepath,'body_dimensions':list(body.dimensions)}
