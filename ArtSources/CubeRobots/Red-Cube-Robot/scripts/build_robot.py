import bpy, math, json
from mathutils import Vector
from pathlib import Path

ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot')
scene=bpy.data.scenes.new('RED CUBE | Studio')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=0.5
model=bpy.data.collections.new('ROBOT | Model')
stage=bpy.data.collections.new('STUDIO | Preview only')
refs=bpy.data.collections.new('REFERENCES | Hidden')
scene.collection.children.link(model);scene.collection.children.link(stage);scene.collection.children.link(refs)
refs.hide_viewport=True;refs.hide_render=True

def material(name,hexcolor,roughness=.4,metallic=0,emission=0):
    h=hexcolor.lstrip('#');rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    linear=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    m=bpy.data.materials.new(name);m.diffuse_color=(*linear,1);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*linear,1)
    p.inputs['Roughness'].default_value=roughness;p.inputs['Metallic'].default_value=metallic
    if emission:
        p.inputs['Emission Color'].default_value=(*linear,1);p.inputs['Emission Strength'].default_value=emission
    return m
red=material('01 | Vermilion red enamel','#ED0730',.32,.08)
bumper=material('02 | Wine red rubber bumper','#77333D',.48)
tire=material('03 | Charcoal tire','#272529',.62)
rim=material('04 | Dark wheel inset','#1C1B1E',.43)
gasket=material('05 | Black gasket and cavities','#090A0D',.55)
bezel=material('06 | Graphite display bezel','#504B50',.36,.15)
screenmat=material('07 | Near black display glass','#111216',.24,.05)
eye=material('08 | Pink luminous pixels','#FF68B0',.28,0,2.8)
exhaust=material('09 | Graphite exhaust lips','#59555C',.38,.30)
floor=material('Studio | Warm off white','#E6E5E4',.75)

def link_obj(obj,collection=model):
    for c in list(obj.users_collection):c.objects.unlink(obj)
    collection.objects.link(obj);return obj
def mesh_obj(name,verts,faces,mat,collection=model):
    me=bpy.data.meshes.new(name+' mesh');me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);collection.objects.link(ob)
    if mat:ob.data.materials.append(mat)
    return ob
def normal(ob):
    mod=ob.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
def box(name,loc,size,mat,bevelwidth=0,segments=1,collection=model):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);ob=link_obj(bpy.context.object,collection);ob.name=name
    ob.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    ob.data.materials.append(mat)
    if bevelwidth:
        mod=ob.modifiers.new('Soft machined edges','BEVEL');mod.width=bevelwidth;mod.segments=segments
        normal(ob)
    return ob
def contour(w,h,r,n=10):
    pts=[]
    for cx,cz,start in [(w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)]:
        for j in range(n+1):
            a=math.radians(start+j*90/n);pts.append((cx+r*math.cos(a),cz+r*math.sin(a)))
    return pts
def rr(name,w,h,r,depth,loc,mat,n=10):
    pts=contour(w,h,r,n);N=len(pts);x,y,z=loc
    verts=[(x+a,y+d,z+b) for d in (-depth/2,depth/2) for a,b in pts]
    faces=[tuple(range(N)),tuple(range(2*N-1,N-1,-1))]
    faces +=[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
    return mesh_obj(name,verts,faces,mat)
def ring(name,sections,loc,mat,n=12):
    x,y,z=loc;verts=[];N=4*(n+1)
    for w,h,r,d in sections:verts.extend((x+a,y+d,z+b) for a,b in contour(w,h,r,n))
    faces=[]
    for s in range(len(sections)-1):
        faces.extend((s*N+i,s*N+(i+1)%N,(s+1)*N+(i+1)%N,(s+1)*N+i) for i in range(N))
    ob=mesh_obj(name,verts,faces,mat)
    for f in ob.data.polygons:f.use_smooth=True
    normal(ob);return ob
def polyextrude(name,pts,y0,y1,mat):
    N=len(pts);v=[(x,y,z) for y in (y0,y1) for x,z in pts]
    f=[tuple(range(N-1,-1,-1)),tuple(range(N,2*N))]+[(i,i+N,(i+1)%N+N,(i+1)%N) for i in range(N)]
    return mesh_obj(name,v,f,mat)

body=box('Body | Exact 2 x 2 x 2 cube',(0,0,1.08),(2,2,2),red,.105,1)
body['design_constraint']='Equal width, depth, height. Front = -Y. Symmetry plane X=0.'
rr('Display | Black outer seal',1.66,1.46,.31,.035,(0,-1.012,1.205),gasket)
ring('Display | Rounded graphite frame',[(1.62,1.43,.30,.0),(1.59,1.40,.29,-.035),(1.53,1.34,.27,-.060),(1.405,1.215,.225,-.060),(1.38,1.19,.215,-.038)],(0,-1.025,1.205),bezel)
rr('Display | Glass',1.405,1.215,.225,.018,(0,-1.068,1.205),screenmat)
for s,label in [(-1,'L'),(1,'R')]:
    rr('Eye '+label+' | Pink emission',.36,.325,.075,.006,(s*.265,-1.082,1.205),eye,12)
sensor=polyextrude('Display | Lower sensor chin',[(-.41,.525),(-.33,.64),(-.27,.665),(.27,.665),(.33,.64),(.41,.525)],-1.097,-1.06,bezel)
rr('Display | Sensor slot gasket',.318,.079,.035,.008,(0,-1.102,.591),gasket)
rr('Display | Sensor dark inset',.282,.050,.022,.004,(0,-1.108,.591),rim)

# Wheel profiles rotate around X. The two sides share exactly mirrored geometry.
def lathe(name,profile,sign,y,z,mat,N=64):
    verts=[(sign*x,y+r*math.sin(2*math.pi*j/N),z+r*math.cos(2*math.pi*j/N)) for x,r in profile for j in range(N)]
    faces=[]
    for k in range(len(profile)-1):
        for j in range(N):
            f=(k*N+j,k*N+(j+1)%N,(k+1)*N+(j+1)%N,(k+1)*N+j)
            faces.append(f if sign==1 else tuple(reversed(f)))
    ob=mesh_obj(name,verts,faces,mat)
    for p in ob.data.polygons:p.use_smooth=True
    normal(ob);return ob
for s,label in [(-1,'L'),(1,'R')]:
    lathe('Wheel '+label+' | Axle',[(.97,0),(.97,.26),(1.08,.26),(1.08,0)],s,.12,.71,rim,48)
    lathe('Wheel '+label+' | Tire',[(1.035,.49),(1.035,.645),(1.075,.691),(1.11,.71),(1.26,.71),(1.30,.685),(1.325,.632),(1.325,.505),(1.30,.49),(1.035,.49)],s,.12,.71,tire)
    lathe('Wheel '+label+' | Inset disk',[(1.30,0),(1.30,.49),(1.323,.505),(1.335,.492),(1.335,0)],s,.12,.71,rim)
    lathe('Wheel '+label+' | Hub shadow ring',[(1.332,0),(1.332,.326),(1.35,.326),(1.35,0)],s,.12,.71,gasket)
    lathe('Wheel '+label+' | Red center cap',[(1.345,0),(1.345,.309),(1.37,.316),(1.397,.281),(1.397,0)],s,.12,.71,red)

box('Bumper front | Crossbar',(0,-1.145,.285),(1.91,.29,.46),bumper,.04,1)
# End caps have a sloped upper face as in the side reference.
for s,label in [(-1,'L'),(1,'R')]:
    # mesh cross-section Y/Z extruded across X
    yz=[(-1.46,.08),(-1.46,.49),(-1.20,.68),(-.99,.68),(-.90,.13),(-.97,.03)]
    verts=[(s*.99+dx,y,z) for dx in (-.225,.225) for y,z in yz];N=len(yz)
    faces=[tuple(range(N-1,-1,-1)),tuple(range(N,2*N))]+[(i,(i+1)%N,(i+1)%N+N,i+N) for i in range(N)]
    ob=mesh_obj('Bumper front '+label+' | Chamfered cap',verts,faces,bumper)
    mod=ob.modifiers.new('Single segment chamfer','BEVEL');mod.width=.045;mod.segments=1;normal(ob)

# Rear service panel follows the reference; all details except exhaust are mirrored.
panel_pts=[(-.91,1.95),(.91,1.95),(.91,.55),(.80,.45),(.80,.18),(-.80,.18),(-.80,.45),(-.91,.55)]
seam=polyextrude('Rear | Panel seam',panel_pts,.999,1.008,gasket)
panel_pts2=[(x*.983,(z-1.065)*.983+1.065) for x,z in panel_pts]
panel=polyextrude('Rear | Red service panel',panel_pts2,1.008,1.020,red)
for i,z in enumerate((.51,.63,.75),1):
    cutter=rr('TEMP grill cutter',.82,.065,.018,.18,(0,.997,z),None,6)
    for ob in (body,seam,panel):
        bpy.context.view_layer.objects.active=ob
        mod=ob.modifiers.new('Recessed vent '+str(i),'BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cutter,do_unlink=True)
    rr('Rear grille | Recess '+str(i),.812,.059,.016,.007,(0,.927,z),gasket,6)
for s,label in [(-1,'L'),(1,'R')]:
    for z in (1.79,.28):rr('Rear | Fastener '+label+' '+str(z),.027,.079,.007,.008,(s*.847,1.025,z),bumper,4)
    box('Bumper rear '+label+' | Corner',(s*.947,1.08,.275),(.18,.23,.40),bumper,.035,1)
box('Bumper rear | Crossbar',(0,1.075,.115),(1.71,.20,.17),bumper,.022,1)
# Rear view is from +Y, therefore its image-right is world -X.
for i,x in enumerate((-.686,-.442),1):
    ring('Exhaust '+str(i)+' | Enlarged square lip',[(.232,.245,.042,0),(.232,.245,.042,.095),(.207,.220,.033,.12),(.148,.160,.024,.12),(.140,.152,.022,.085)],(x,1.025,.28),exhaust,2)
    rr('Exhaust '+str(i)+' | Deep dark opening',.143,.156,.023,.006,(x,1.037,.28),gasket,3)

# Model root and separate wheel pivots for later game work (no export performed).
root=bpy.data.objects.new('RED CUBE ROBOT | Root',None);model.objects.link(root);root.empty_display_type='PLAIN_AXES';root.empty_display_size=.25
for ob in list(model.objects):
    if ob!=root:ob.parent=root
for label,s in [('L',-1),('R',1)]:
    pivot=bpy.data.objects.new('Wheel '+label+' | Rotation pivot X',None);model.objects.link(pivot);pivot.location=(s*1.16,.12,.71);pivot.parent=root;pivot.empty_display_size=.14
    for ob in list(model.objects):
        if ob.type=='MESH' and ob.name.startswith('Wheel '+label+' |'):
            world=ob.matrix_world.copy();ob.parent=pivot;ob.matrix_world=world
root['reference']='Concept palette and chamfers; front/left/rear shape references. Rear-view right exhaust is the sole asymmetric feature.'
root['size_note']='Body 2 Blender units, metric unit scale 0.5. Physical body edge 1 metre; can be rescaled for Unity later.'

# Pack the original reference images inside the blend, in a hidden collection.
for i,filename in enumerate(('01-concept.png','02-front.png','03-left.png','04-rear.png')):
    im=bpy.data.images.load(str(ROOT/'references'/filename));im.pack()
    ob=bpy.data.objects.new('Reference '+filename,None);ob.empty_display_type='IMAGE';ob.data=im;refs.objects.link(ob);ob.location=(8+i*3,0,1)

box('Studio | Ground',(0,0,-.055),(200,200,.1),floor,0,collection=stage)
world=bpy.data.worlds.new('Studio | Soft neutral world');world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.55,.55,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.35;scene.world=world
def area(name,loc,power,size,target):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    ob=bpy.data.objects.new(name,data);stage.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
area('Studio | Key softbox',(-3.5,-4.5,6),450,4,(0,0,1))
area('Studio | Fill softbox',(4,-2,3.5),250,3.5,(0,0,1))
area('Studio | Rear rim',(1,4.2,5.5),500,3,(0,0,1))
def camera(name,loc,target,scale):
    data=bpy.data.cameras.new(name);data.type='ORTHO';data.ortho_scale=scale;data.lens=55
    ob=bpy.data.objects.new(name,data);stage.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler();return ob
hero=camera('Camera | 01 Concept',(-4,-6,3.5),(0,-.08,1.02),3.95)
camera('Camera | 02 Front',(0,-7,1.08),(0,0,1.08),3.45)
camera('Camera | 03 Left',(-7,0,1.08),(0,0,1.08),3.45)
camera('Camera | 04 Rear',(0,7,1.08),(0,0,1.08),3.45)
camera('Camera | 05 Rear three quarter',(4,6,3.5),(0,.05,1.02),3.95)
scene.camera=hero;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1000;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.view_settings.exposure=-.35
scene.render.filepath=str(ROOT/'previews'/'01-concept.png')
# Current Blender versions use a compositor node group.
if hasattr(scene,'compositing_node_group'):
    nt=bpy.data.node_groups.new('Studio | Gentle pink bloom','CompositorNodeTree');scene.compositing_node_group=nt
    nt.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
    out=nt.nodes.new('NodeGroupOutput')
else:
    scene.use_nodes=True;nt=scene.node_tree;nt.nodes.clear();out=nt.nodes.new('CompositorNodeComposite')
rl=nt.nodes.new('CompositorNodeRLayers');rl.scene=scene
glow=nt.nodes.new('CompositorNodeGlare');glow.glare_type='FOG_GLOW';glow.quality='HIGH'
if hasattr(glow,'threshold'):glow.threshold=1.2
if hasattr(glow,'size'):glow.size=7
nt.links.new(rl.outputs['Image'],glow.inputs['Image']);nt.links.new(glow.outputs['Image'],out.inputs['Image'])

for ob in scene.objects:ob.select_set(False)
body.select_set(True);bpy.context.view_layer.objects.active=body
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            ar.spaces.active.region_3d.view_rotation=hero.rotation_euler.to_quaternion()
            ar.spaces.active.region_3d.view_distance=5.3;ar.spaces.active.region_3d.view_location=(0,0,1.05)
            ar.spaces.active.region_3d.view_perspective='ORTHO'
            ar.spaces.active.shading.type='MATERIAL';ar.spaces.active.overlay.show_overlays=False
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Red_Cube_Robot_v01.blend'))
result={'file':bpy.data.filepath,'body_dimensions':list(body.dimensions),'model_meshes':sum(o.type=='MESH' for o in model.objects),'compositor':True,'scene':scene.name}
