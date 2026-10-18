import bpy, math
from mathutils import Matrix
from pathlib import Path
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');scene=bpy.context.scene
model=bpy.data.collections['ROBOT | Model'];root=bpy.data.objects['RED CUBE ROBOT | Root']
# Lathed wheel meshes contain their coordinates in model space. Preserve that
# space explicitly while assigning independent wheel rotation pivots.
bpy.context.view_layer.update()
for ob in model.objects:
    if ob.type=='MESH' and ob.name.startswith('Wheel '):
        ob.matrix_basis=Matrix.Identity(4)
        ob.matrix_parent_inverse=ob.parent.matrix_world.inverted()
bpy.context.view_layer.update()

# Apply the broad body chamfer before cutting the tiny grille recesses so the
# bevel's overlap clamp cannot reduce the silhouette chamfer to zero.
old=bpy.data.objects['Body | Exact 2 x 2 x 2 cube'];bpy.data.objects.remove(old,do_unlink=True)
bpy.ops.mesh.primitive_cube_add(size=2,location=(0,0,1.08));body=bpy.context.object
for c in list(body.users_collection):c.objects.unlink(body)
model.objects.link(body);body.name='Body | Exact 2 x 2 x 2 cube';body.parent=root
body.data.materials.append(bpy.data.materials['01 | Vermilion red enamel'])
be=body.modifiers.new('Broad single segment chamfer','BEVEL');be.width=.105;be.segments=1
bpy.ops.object.modifier_apply(modifier=be.name)
for i,z in enumerate((.51,.63,.75),1):
    src=bpy.data.objects['Rear grille | Recess '+str(i)]
    cutter=src.copy();cutter.data=src.data.copy();model.objects.link(cutter);cutter.parent=None
    # The source mesh is in world coordinates. Stretch its depth around the recess.
    for v in cutter.data.vertices:v.co.y=.997+(v.co.y-.927)*30
    mod=body.modifiers.new('Vent cut '+str(i),'BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
    bpy.context.view_layer.objects.active=body;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
wn=body.modifiers.new('Weighted body normals','WEIGHTED_NORMAL');wn.keep_sharp=True
body['design_constraint']='Exact equal 2 x 2 x 2 dimensions; chamfered cube.'
red=bpy.data.materials['01 | Vermilion red enamel'];p=red.node_tree.nodes.get('Principled BSDF')
p.inputs['Base Color'].default_value=(.70,.0018,.016,1);p.inputs['Roughness'].default_value=.38;p.inputs['Metallic'].default_value=0;p.inputs['Specular IOR Level'].default_value=.25
red.diffuse_color=(.70,.0018,.016,1)
p=bpy.data.materials['07 | Near black display glass'].node_tree.nodes.get('Principled BSDF');p.inputs['Specular IOR Level'].default_value=.22;p.inputs['Roughness'].default_value=.3
p=bpy.data.materials['08 | Pink luminous pixels'].node_tree.nodes.get('Principled BSDF')
p.inputs['Base Color'].default_value=(1,.17,.40,1);p.inputs['Emission Color'].default_value=(1,.17,.40,1);p.inputs['Emission Strength'].default_value=1.8
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.35,.35,.35,1)
scene.render.filepath=str(ROOT/'previews'/'01-concept.png')
bpy.context.view_layer.update()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Red_Cube_Robot_v01.blend'))
bpy.ops.render.render(write_still=True)
result={'body_dimensions':list(body.dimensions),'preview':scene.render.filepath}
