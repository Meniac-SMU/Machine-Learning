import bpy,json,struct,zlib,shutil,hashlib
from pathlib import Path
from mathutils import Vector
R=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Cube-Robots-Unity-Release');A=R/'Assets'/'CubeRobots'
assert not R.exists()
for sub in ['Models','Textures','Editor','Materials','Prefabs']:(A/sub).mkdir(parents=True,exist_ok=True)
(R/'Previews').mkdir()
items=[('RedRobot',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot\Red_Cube_Robot_v03.blend',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot\previews-v03\01-concept.png'),('NavyRobot',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Navy-Cube-Robot\Navy_Cube_Robot_v02.blend',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Navy-Cube-Robot\previews-v02\01-concept.png'),('PinkGoalkeeper',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots\Goalkeeper_Pink_v02.blend',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots\previews\Goalkeeper_Pink_v02.png'),('SkyGoalkeeper',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots\Goalkeeper_Sky_v01.blend',r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots\previews\Goalkeeper_Sky.png')]
def pngread(path):
    raw=Path(path).read_bytes();pos=8;comp=b''
    while pos<len(raw):
        n=struct.unpack('>I',raw[pos:pos+4])[0];tag=raw[pos+4:pos+8];d=raw[pos+8:pos+8+n];pos+=n+12
        if tag==b'IHDR':w,h=struct.unpack('>II',d[:8])
        if tag==b'IDAT':comp+=d
    rows=bytearray(zlib.decompress(comp));assert all(rows[y*(1+w*4)]==0 for y in range(h));return w,h,rows
def pngwrite(path,w,h,rows):
    def c(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+c(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+c(b'IDAT',zlib.compress(rows))+c(b'IEND',b''))
def bounds(ob):
    v=[ob.matrix_world@x.co for x in ob.data.vertices]
    return [[min(p[i] for p in v) for i in range(3)],[max(p[i] for p in v) for i in range(3)]]
results=[]
for name,source,preview in items:
    sourcehash=hashlib.sha256(Path(source).read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=source)
    collection=bpy.data.collections['ROBOT | Model'];objects=[o for o in collection.objects if o.type=='MESH'];assert len(objects)==1
    ob=objects[0];me=ob.data;assert len(me.materials)==2
    body,eye=me.materials
    color=next(n for n in body.node_tree.nodes if n.type=='TEX_IMAGE' and 'palette' in n.image.name.lower())
    surface=next(n for n in body.node_tree.nodes if n.type=='TEX_IMAGE' and n!=color)
    colorpath=A/'Textures'/(name+'_BaseColor.png');shutil.copyfile(bpy.path.abspath(color.image.filepath),colorpath)
    w,h,rows=pngread(bpy.path.abspath(surface.image.filepath))
    for y in range(h):
        for x in range(w):
            i=y*(1+w*4)+1+x*4;rough,metal=rows[i:i+2];rows[i:i+4]=bytes([metal,0,0,255-rough])
    pngwrite(A/'Textures'/(name+'_MetallicSmoothness.png'),w,h,rows)
    emission=next(n for n in eye.node_tree.nodes if n.type=='EMISSION' and n.inputs['Strength'].default_value>0)
    hdr=[c*emission.inputs['Strength'].default_value for c in emission.inputs['Color'].default_value[:3]]
    # Use meters explicitly in the exported mesh. Original body edge = 2 * 0.5 = 1 m.
    scale=bpy.context.scene.unit_settings.scale_length
    for v in me.vertices:v.co*=scale
    bpy.context.scene.unit_settings.scale_length=1
    ob.name=name;me.name=name+'_Mesh'
    me.materials[0].name=name+'_Body';me.materials[1].name=name+'_Eyes'
    expected=bounds(ob)
    # Export only this static mesh. Studio objects, reference images, rigs and animation are excluded.
    bpy.ops.object.select_all(action='DESELECT');ob.hide_set(False);ob.select_set(True);bpy.context.view_layer.objects.active=ob
    tri=ob.modifiers.new('Export triangulation','TRIANGULATE')
    if hasattr(tri,'keep_custom_normals'):tri.keep_custom_normals=True
    bpy.ops.object.modifier_apply(modifier=tri.name)
    me.calc_loop_triangles();expectedtris=len(me.loop_triangles)
    fbx=A/'Models'/(name+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='OFF',use_tspace=False,use_triangles=True,add_leaf_bones=False,bake_anim=False,path_mode='STRIP',embed_textures=False)
    assert hashlib.sha256(Path(source).read_bytes()).hexdigest()==sourcehash
    # Reimport exported FBX into a clean scene to check topology, units and material slots.
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(fbx),use_custom_normals=True)
    meshes=[x for x in bpy.context.scene.objects if x.type=='MESH'];assert len(meshes)==1
    check=meshes[0];check.data.calc_loop_triangles();actual=bounds(check)
    error=max(abs(a-b) for rowa,rowb in zip(expected,actual) for a,b in zip(rowa,rowb))
    assert error<1e-4,(name,expected,actual)
    assert len(check.data.loop_triangles)==expectedtris==5768
    assert len(check.data.materials)==2 and len(check.data.uv_layers)>0
    assert not any(x.type in {'LIGHT','CAMERA','ARMATURE'} for x in bpy.context.scene.objects)
    shutil.copyfile(preview,R/'Previews'/(name+'.png'))
    results.append({'name':name,'source':source,'source_sha256':sourcehash,'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'triangles':expectedtris,'mesh_objects':1,'material_slots':2,'eye_hdr_linear':hdr,'body_edge_meters':1,'roundtrip_bounds_error':error,'blender_bounds_meters':actual})
(R/'export-validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
print('EXPORT_VALIDATED '+json.dumps(results))
