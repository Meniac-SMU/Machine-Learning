import bpy,json,struct,zlib,hashlib
from pathlib import Path
R=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Navy-Cube-Robot');s=bpy.context.scene
assert Path(bpy.data.filepath).name=='Navy_Cube_Robot_v01.blend',bpy.data.filepath
assert not (R/'Navy_Cube_Robot_v02.blend').exists()
model=bpy.data.collections['ROBOT | Model'];o=next(x for x in model.objects if x.type=='MESH');me=o.data
def geomhash():return hashlib.sha256(b''.join(struct.pack('<3f',*v.co) for v in me.vertices)+b''.join(struct.pack('<I',i) for p in me.polygons for i in p.vertices)).hexdigest()
before=geomhash()
if bpy.data.is_dirty:bpy.ops.wm.save_as_mainfile(filepath=str(R/'Navy_Cube_Robot_before_v02.blend'),copy=True)
navy=(27,73,148,255);dark=(18,52,110,255)
def linear(c):return tuple(v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in c[:3])+(1,)
body=me.materials[0].copy();body.name='Navy Robot v02 | Brighter saturated palette';me.materials[0]=body
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
path=R/'textures'/'Navy_Robot_palette_v02.png'
path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(pixels))+chunk(b'IEND',b''))
im=bpy.data.images.load(str(path));im.colorspace_settings.name='sRGB';im.pack();tex.image=im;body.diffuse_color=linear(navy)
col=me.color_attributes['Preview Color'];uv=me.uv_layers.active
for p in me.polygons:
    if p.material_index==0:
        for li in p.loop_indices:
            index=int(uv.data[li].uv.x*8)
            if index in (0,1):col.data[li].color=linear(navy if index==0 else dark)
o.name='NAVY CUBE ROBOT | Optimized static v02';o['palette']='Body and wheel caps #1B4994; kickplate and rear trim #12346E; sky blue eyes unchanged.'
assert geomhash()==before
me.calc_loop_triangles()
out=R/'previews-v02';out.mkdir(exist_ok=True)
s.camera=bpy.data.objects['Camera | 01 Concept'];s.render.filepath=str(out/'01-concept.png')
bpy.ops.wm.save_as_mainfile(filepath=str(R/'Navy_Cube_Robot_v02.blend'))
bpy.ops.render.render(write_still=True)
result={'file':bpy.data.filepath,'body':'#1B4994','kickplate':'#12346E','geometry_unchanged':True,'triangles':len(me.loop_triangles),'materials':len(me.materials)}
(R/'validation-v02.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
