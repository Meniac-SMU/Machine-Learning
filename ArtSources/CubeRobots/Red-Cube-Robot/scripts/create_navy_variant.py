import bpy,json,struct,zlib,hashlib
from pathlib import Path
src=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');R=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Navy-Cube-Robot')
s=bpy.context.scene
assert Path(bpy.data.filepath).name=='Red_Cube_Robot_v03.blend',bpy.data.filepath
model=bpy.data.collections['ROBOT | Model'];o=next(x for x in model.objects if x.type=='MESH')
assert sum(x.type=='MESH' for x in model.objects)==1
assert not (R/'Navy_Cube_Robot_v01.blend').exists()
if bpy.data.is_dirty:bpy.ops.wm.save_as_mainfile(filepath=str(src/'Red_Cube_Robot_before_navy.blend'),copy=True)
R.mkdir(exist_ok=True);(R/'textures').mkdir(exist_ok=True);(R/'previews').mkdir(exist_ok=True)
def geomhash(me):return hashlib.sha256(b''.join(struct.pack('<3f',*v.co) for v in me.vertices)+b''.join(struct.pack('<I',i) for p in me.polygons for i in p.vertices)).hexdigest()
before=geomhash(o.data)
navy=(24,53,106,255);dark=(16,31,59,255)
def linear(c):return tuple(v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in c[:3])+(1,)
# Copy the palette material and change only the red/wine color swatches.
body=o.data.materials[0].copy();body.name='Navy Robot | Packed color palette';o.data.materials[0]=body
tex=next(n for n in body.node_tree.nodes if n.type=='TEX_IMAGE' and 'palette' in n.image.name.lower())
raw=Path(bpy.path.abspath(tex.image.filepath)).read_bytes();pos=8;compressed=b''
while pos<len(raw):
    size=struct.unpack('>I',raw[pos:pos+4])[0];tag=raw[pos+4:pos+8];data=raw[pos+8:pos+8+size];pos+=12+size
    if tag==b'IHDR':w,h=struct.unpack('>II',data[:8])
    if tag==b'IDAT':compressed+=data
pixels=bytearray(zlib.decompress(compressed));stride=1+w*4
for y in range(h):
    assert pixels[y*stride]==0
    for x in range(32):pixels[y*stride+1+x*4:y*stride+5+x*4]=bytes(navy if x<16 else dark)
def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
path=R/'textures'/'Navy_Robot_palette.png'
path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(pixels))+chunk(b'IEND',b''))
im=bpy.data.images.load(str(path));im.colorspace_settings.name='sRGB';im.pack();tex.image=im
body.diffuse_color=linear(navy)
eye=o.data.materials[1].copy();eye.name='Navy Robot | Sky blue luminous eyes';o.data.materials[1]=eye
for n in eye.node_tree.nodes:
    if n.type=='EMISSION' and n.inputs['Strength'].default_value>0:
        n.inputs['Color'].default_value=linear((72,165,245,255));n.inputs['Strength'].default_value=2.5
eye.diffuse_color=linear((116,216,255,255))
me=o.data;col=me.color_attributes['Preview Color'];uv=me.uv_layers.active
for p in me.polygons:
    for li in p.loop_indices:
        if p.material_index==1:col.data[li].color=eye.diffuse_color
        else:
            index=int(uv.data[li].uv.x*8)
            if index in (0,1):col.data[li].color=linear(navy if index==0 else dark)
o.name='NAVY CUBE ROBOT | Optimized static v01';me.name='Navy Cube Robot | Static optimized mesh'
s.name='NAVY CUBE | Studio'
o['palette']='Body and wheel caps #18356A; kickplate and rear trim #101F3B; sky blue emissive eyes.'
assert geomhash(me)==before
me.calc_loop_triangles()
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':a.spaces.active.shading.type='SOLID';a.spaces.active.shading.color_type='VERTEX';a.spaces.active.shading.use_compositor='DISABLED'
report={'file':str(R/'Navy_Cube_Robot_v01.blend'),'source':str(src/'Red_Cube_Robot_v03.blend'),'geometry_identical':True,'geometry_sha256':before,'triangles':len(me.loop_triangles),'mesh_objects':1,'material_slots':len(me.materials),'colors':{'body_and_wheel_caps':'#18356A','kickplate_and_rear_trim':'#101F3B','eye_emission_base':'#48A5F5'},'eye_light_spill':False}
(R/'validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
ground=bpy.data.objects['Studio | Ground'];ground.hide_render=False
for camera,filename in [('Camera | 01 Concept','01-concept.png'),('Camera | 05 Rear three quarter','05-rear-angle.png')]:
    s.camera=bpy.data.objects[camera];s.render.filepath=str(R/'previews'/filename);bpy.ops.render.render(write_still=True)
s.camera=bpy.data.objects['Camera | 01 Concept'];s.render.filepath=str(R/'previews'/'01-concept.png')
bpy.ops.wm.save_as_mainfile(filepath=report['file'])
result=report
