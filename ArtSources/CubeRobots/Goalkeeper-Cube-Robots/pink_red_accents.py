import bpy,struct,zlib,json,hashlib
from pathlib import Path
R=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots');s=bpy.context.scene
assert Path(bpy.data.filepath).name=='Goalkeeper_Pink_v01.blend'
dest=R/'Goalkeeper_Pink_v02.blend';assert not dest.exists()
o=next(o for o in bpy.data.collections['ROBOT | Model'].objects if o.type=='MESH');me=o.data
def fingerprint():return hashlib.sha256(b''.join(struct.pack('<3f',*v.co) for v in me.vertices)).hexdigest()
before=fingerprint()
if bpy.data.is_dirty:bpy.ops.wm.save_as_mainfile(filepath=str(R/'Goalkeeper_Pink_before_v02.blend'),copy=True)
def readpng(path):
    data=Path(path).read_bytes();pos=8;comp=b''
    while pos<len(data):
        n=struct.unpack('>I',data[pos:pos+4])[0];tag=data[pos+4:pos+8];payload=data[pos+8:pos+8+n];pos+=n+12
        if tag==b'IHDR':w,h=struct.unpack('>II',payload[:8])
        if tag==b'IDAT':comp+=payload
    pixels=bytearray(zlib.decompress(comp));assert all(pixels[y*(1+w*4)]==0 for y in range(h));return w,h,pixels
mat=me.materials[0].copy();mat.name='Pink Goalkeeper | Red robot accents';me.materials[0]=mat
tex=next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and 'palette' in n.image.name.lower())
w,h,pixels=readpng(bpy.path.abspath(tex.image.filepath))
rw,rh,rpixels=readpng(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot\textures\Robot_v02_palette.png')
red=rpixels[1:5];wine=rpixels[65:69]
for y in range(h):
    for x in range(32):pixels[y*(1+w*4)+1+x*4:y*(1+w*4)+5+x*4]=red if x<16 else wine
def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
path=R/'textures'/'Goalkeeper_Pink_palette_v02.png'
path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(pixels))+chunk(b'IEND',b''))
im=bpy.data.images.load(str(path));im.colorspace_settings.name='sRGB';im.pack();tex.image=im
# Append the exact eye shader from the original red robot, including its no-spill setup.
with bpy.data.libraries.load(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot\Red_Cube_Robot_v03.blend',link=False) as (source,target):
    assert '08 | Pink luminous pixels' in source.materials
    target.materials=['08 | Pink luminous pixels']
eye=target.materials[0];me.materials[1]=eye
def linear(c):return tuple(v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in c[:3])+(1,)
col=me.color_attributes['Preview Color'];uv=me.uv_layers.active;counts={0:0,1:0,8:0,9:0}
for p in me.polygons:
    for li in p.loop_indices:
        if p.material_index==1:col.data[li].color=eye.diffuse_color
        else:
            idx=int(uv.data[li].uv.x*16)
            if idx in counts:counts[idx]+=1
            if idx in (0,1):col.data[li].color=linear(red if idx==0 else wine)
assert all(counts.values());assert fingerprint()==before
o.name='GOALKEEPER PINK | Red accents v02';o['palette']='Pink body and fluorescent yellow front bar preserved; eye shader, red wheel caps and wine trim from Red_Cube_Robot_v03.'
s.camera=bpy.data.objects['Camera | 01 Concept'];s.render.filepath=str(R/'previews'/'Goalkeeper_Pink_v02.png')
me.calc_loop_triangles()
bpy.ops.wm.save_as_mainfile(filepath=str(dest));bpy.ops.render.render(write_still=True)
result={'file':str(dest),'wheel_cap_rgba':list(red),'accent_rgba':list(wine),'eye_material':eye.name,'geometry_unchanged':True,'triangles':len(me.loop_triangles),'material_slots':len(me.materials),'preserved_body_and_front_bar':True}
(R/'validation-pink-v02.json').write_text(json.dumps(result,indent=2),encoding='utf-8')
