import bpy, math, json, struct, zlib
from pathlib import Path
from mathutils import Vector
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot')
s=bpy.context.scene; model=bpy.data.collections['ROBOT | Model']
assert 'v01' in bpy.data.filepath
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Red_Cube_Robot_before_v02.blend'),copy=True)
oldmats=[bpy.data.materials.get(n) for n in ['01 | Vermilion red enamel','02 | Wine red rubber bumper','03 | Charcoal tire','04 | Dark wheel inset','05 | Black gasket and cavities','06 | Graphite display bezel','07 | Near black display glass','09 | Graphite exhaust lips']]
eye=bpy.data.materials['08 | Pink luminous pixels']
# Put the full-width pushing surface in front of its mounting caps, down to Z=0.
bar=bpy.data.objects['Bumper front | Crossbar']
bar.location=(0,-1.50,.2575);bar.dimensions=(2.64,.24,.515)
bpy.context.view_layer.objects.active=bar
bpy.ops.object.select_all(action='DESELECT');bar.select_set(True)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
for o in model.objects:
    if o.name.startswith('Bumper rear'):
        o.location.y=1.006-o.dimensions.y/2
        if 'Corner' in o.name:o.location.x=math.copysign(.90,o.location.x)
# Replace the layered wheels with a continuous profile, without hidden duplicate caps.
for o in list(model.objects):
    if o.type=='MESH' and o.name.startswith('Wheel '):bpy.data.objects.remove(o,do_unlink=True)
profile=[(1.035,.26),(1.035,.645),(1.075,.691),(1.11,.71),(1.26,.71),(1.30,.685),(1.325,.632),(1.325,.505),(1.335,.492),(1.335,.326),(1.35,.326),(1.35,.309),(1.37,.316),(1.397,.281)]
# Face material for each radial section: tire, inset, seal, red hub.
bands=[2,2,2,2,2,2,2,3,3,4,4,0,0]
for sign,label in [(1,'L'),(-1,'R')]:
    N=32;verts=[(sign*x,.12+r*math.sin(2*math.pi*j/N),.71+r*math.cos(2*math.pi*j/N)) for x,r in profile for j in range(N)]
    faces=[];mis=[]
    for k in range(len(profile)-1):
        for j in range(N):
            f=(k*N+j,k*N+(j+1)%N,(k+1)*N+(j+1)%N,(k+1)*N+j)
            faces.append(f if sign==1 else f[::-1]);mis.append(bands[k])
    # Inner back and outer hub disks; no duplicated zero-radius rings.
    for ring,reverse,mi in [(0,True,3),(len(profile)-1,False,0)]:
        f=tuple(ring*N+j for j in range(N))
        if reverse:f=f[::-1]
        if sign<0:f=f[::-1]
        faces.append(f);mis.append(mi)
    me=bpy.data.meshes.new('Wheel '+label+' optimized');me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new('Wheel '+label+' | 32 segment unified profile',me);model.objects.link(o)
    for m in oldmats:me.materials.append(m)
    for p,mi in zip(me.polygons,mis):p.material_index=mi;p.use_smooth=len(p.vertices)==4
    mod=o.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True
# A packed palette keeps the original color/roughness/metal values with one body material.
texdir=ROOT/'textures';texdir.mkdir(exist_ok=True)
def png(path,swatches):
    w,h=128,16
    rows=b''.join(b'\x00'+b''.join(bytes(swatches[x//16]) for x in range(w)) for y in range(h))
    def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(rows))+chunk(b'IEND',b''))
def srgb(v):return round(255*(12.92*v if v<=.0031308 else 1.055*v**(1/2.4)-.055))
colors=[];props=[];linearcolors=[]
for m in oldmats:
    p=m.node_tree.nodes.get('Principled BSDF');c=list(p.inputs['Base Color'].default_value);linearcolors.append(c)
    colors.append([srgb(v) for v in c[:3]]+[255])
    props.append([round(255*p.inputs[k].default_value) for k in ['Roughness','Metallic','Specular IOR Level']]+[255])
png(texdir/'Robot_v02_palette.png',colors);png(texdir/'Robot_v02_surface.png',props)
mat=bpy.data.materials.new('Robot v02 | Packed color palette');mat.use_nodes=True
nt=mat.node_tree;p=nt.nodes.get('Principled BSDF')
for name,cs in [('Robot_v02_palette.png','sRGB'),('Robot_v02_surface.png','Non-Color')]:
    im=bpy.data.images.load(str(texdir/name));im.colorspace_settings.name=cs;im.pack()
    node=nt.nodes.new('ShaderNodeTexImage');node.image=im;node.interpolation='Closest'
    if cs=='sRGB':nt.links.new(node.outputs['Color'],p.inputs['Base Color'])
    else:
        sep=nt.nodes.new('ShaderNodeSeparateColor');nt.links.new(node.outputs['Color'],sep.inputs['Color'])
        for output,input in [('Red','Roughness'),('Green','Metallic'),('Blue','Specular IOR Level')]:nt.links.new(sep.outputs[output],p.inputs[input])
# Camera-only emission: no diffuse/glossy light transport from the eyes.
nt=eye.node_tree;em=next(n for n in nt.nodes if n.type=='EMISSION');out=next(n for n in nt.nodes if n.type=='OUTPUT_MATERIAL')
black=nt.nodes.new('ShaderNodeEmission');black.inputs['Color'].default_value=(0,0,0,1);black.inputs['Strength'].default_value=0
lp=nt.nodes.new('ShaderNodeLightPath');mix=nt.nodes.new('ShaderNodeMixShader')
nt.links.new(lp.outputs['Is Camera Ray'],mix.inputs[0]);nt.links.new(black.outputs[0],mix.inputs[1]);nt.links.new(em.outputs[0],mix.inputs[2]);nt.links.new(mix.outputs[0],out.inputs['Surface'])
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

