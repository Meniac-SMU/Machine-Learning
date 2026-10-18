import bpy, math, json, struct, zlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot')
s=bpy.context.scene; model=bpy.data.collections['ROBOT | Model']
oldmats=[bpy.data.materials.get(n) for n in ['01 | Vermilion red enamel','02 | Wine red rubber bumper','03 | Charcoal tire','04 | Dark wheel inset','05 | Black gasket and cavities','06 | Graphite display bezel','07 | Near black display glass','09 | Graphite exhaust lips']]
eye=bpy.data.materials['08 | Pink luminous pixels']

mat=bpy.data.materials['Robot v02 | Packed color palette']
linearcolors=[list(m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value) for m in oldmats]
# Bake evaluated meshes and normals into one static object, keeping named editing groups.
bpy.context.view_layer.update();dg=bpy.context.evaluated_depsgraph_get()
verts=[];faces=[];normals=[];smooth=[];materialids=[];uvs=[];vcolors=[];groups={};bounds={}
sources=[o for o in model.objects if o.type=='MESH']
for o in sources:
    ev=o.evaluated_get(dg);me=ev.to_mesh();offset=len(verts);world=o.matrix_world
    points=[world@v.co for v in me.vertices];verts.extend(tuple(v) for v in points)
    groups[o.name]=list(range(offset,len(verts)))
    bounds[o.name]={'min':[min(v[i] for v in points) for i in range(3)],'max':[max(v[i] for v in points) for i in range(3)]}
    normalmat=world.to_3x3().inverted().transposed()
    for p in me.polygons:
        material=me.materials[p.material_index];is_eye=material.name==eye.name;idx=[m.name for m in oldmats].index(material.name) if not is_eye else 0
        faces.append(tuple(offset+v for v in p.vertices));smooth.append(p.use_smooth);materialids.append(1 if is_eye else 0)
        for li in p.loop_indices:
            normals.append(tuple((normalmat@me.corner_normals[li].vector).normalized()));uvs.append(((idx+.5)/8,.5));vcolors.append((1,.20,.4,1) if is_eye else linearcolors[idx])
    ev.to_mesh_clear()
me=bpy.data.meshes.new('Red Cube Robot | Static optimized mesh');me.from_pydata(verts,[],faces);me.update();me.materials.append(mat);me.materials.append(eye)
for p,sm,mi in zip(me.polygons,smooth,materialids):p.use_smooth=sm;p.material_index=mi
uv=me.uv_layers.new(name='Palette UV');col=me.color_attributes.new(name='Preview Color',type='FLOAT_COLOR',domain='CORNER')
for i in range(len(uvs)):uv.data[i].uv=uvs[i];col.data[i].color=vcolors[i]
me.normals_split_custom_set(normals)
robot=bpy.data.objects.new('RED CUBE ROBOT | Optimized static v02',me);model.objects.link(robot)
for name,indices in groups.items():robot.vertex_groups.new(name=name).add(indices,1,'REPLACE')
for o in sources:bpy.data.objects.remove(o,do_unlink=True)
for o in list(model.objects):
    if o.type=='EMPTY':bpy.data.objects.remove(o,do_unlink=True)
robot['parts']='All parts static; one mesh, two material slots; editable part vertex groups.'
robot['ground']='Wheel and front pushing plate lowest vertex Z=0.'
robot['eye_lighting']='Camera-only emission; no light spill.'
robot['body_edge']=2.0
ground=bpy.data.objects['Studio | Ground'];ground.location.z=-ground.dimensions.z/2;ground.hide_render=False
s.camera=bpy.data.objects['Camera | 01 Concept']
for o in s.objects:o.select_set(False)
robot.select_set(True);bpy.context.view_layer.objects.active=robot
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            sp=a.spaces.active;sp.shading.type='SOLID';sp.shading.color_type='VERTEX';sp.shading.use_compositor='DISABLED';sp.overlay.show_overlays=False
me.calc_loop_triangles()
report={'file':str(ROOT/'Red_Cube_Robot_v02.blend'),'mesh_objects':1,'material_slots':len(me.materials),'vertices':len(me.vertices),'triangles':len(me.loop_triangles),'triangles_before':9680,'wheel_segments':32,'bounds_by_part':bounds,'packed_images':[im.name for im in bpy.data.images if im.packed_file]}
(ROOT/'validation-v02.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
bpy.ops.wm.save_as_mainfile(filepath=report['file'])
result={k:v for k,v in report.items() if k!='bounds_by_part'}


