import bpy,json,struct,zlib,hashlib
from pathlib import Path
R=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots');s=bpy.context.scene
assert Path(bpy.data.filepath).name=='Navy_Cube_Robot_v02.blend',bpy.data.filepath
model=bpy.data.collections['ROBOT | Model'];o=next(x for x in model.objects if x.type=='MESH');me=o.data
assert not R.exists()
R.mkdir();(R/'textures').mkdir();(R/'previews').mkdir()
if bpy.data.is_dirty:bpy.ops.wm.save_as_mainfile(filepath=r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Navy-Cube-Robot\Navy_Cube_Robot_before_goalkeepers.blend',copy=True)
def fingerprint():return hashlib.sha256(b''.join(struct.pack('<3f',*v.co) for v in me.vertices)).hexdigest()
before=fingerprint();source_mat=me.materials[0];eye=me.materials[1]
def readpng(path):
    raw=Path(path).read_bytes();pos=8;comp=b''
    while pos<len(raw):
        size=struct.unpack('>I',raw[pos:pos+4])[0];tag=raw[pos+4:pos+8];data=raw[pos+8:pos+8+size];pos+=size+12
        if tag==b'IHDR':w,h=struct.unpack('>II',data[:8])
        if tag==b'IDAT':comp+=data
    buf=zlib.decompress(comp);assert buf[0]==0
    return [list(buf[1+i*16*4:5+i*16*4]) for i in range(8)]
nodes=[n for n in source_mat.node_tree.nodes if n.type=='TEX_IMAGE']
color_node=next(n for n in nodes if 'palette' in n.image.name.lower());surface_node=next(n for n in nodes if n!=color_node)
colors=readpng(bpy.path.abspath(color_node.image.filepath));props=readpng(bpy.path.abspath(surface_node.image.filepath))
def png(path,swatches):
    w,h=256,16;rows=b''.join(b'\x00'+b''.join(bytes(swatches[x//16]) for x in range(w)) for y in range(h))
    def chunk(t,d):return struct.pack('>I',len(d))+t+d+struct.pack('>I',zlib.crc32(t+d)&0xffffffff)
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',w,h,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(rows))+chunk(b'IEND',b''))
def linear(c):return tuple(v/255/12.92 if v/255<=.04045 else ((v/255+.055)/1.055)**2.4 for v in c[:3])+(1,)
bodygroups={g.index for g in o.vertex_groups if g.name.startswith('Body |') or g.name=='Rear | Red service panel'}
plate=o.vertex_groups['Bumper front | Crossbar'].index
bodyverts={v.index for v in me.vertices if any(g.group in bodygroups for g in v.groups)}
plateverts={v.index for v in me.vertices if any(g.group==plate for g in v.groups)}
assert bodyverts and plateverts
uv=me.uv_layers.active;col=me.color_attributes['Preview Color'];indices=[]
for p in me.polygons:
    idx=int(uv.data[p.loop_start].uv.x*8)
    if p.material_index==0:
        if all(v in bodyverts for v in p.vertices):idx=8
        elif all(v in plateverts for v in p.vertices):idx=9
    indices.append(idx)
    for li in p.loop_indices:uv.data[li].uv=((idx+.5)/16,.5)
packedprops=props+[props[0],props[1]]+[props[0]]*6
propfile=R/'textures'/'Goalkeeper_surface.png';png(propfile,packedprops)
propim=bpy.data.images.load(str(propfile));propim.colorspace_settings.name='Non-Color';propim.pack()
results=[]
for label,bodycolor in [('Pink',(255,165,198,255)),('Sky',(143,211,255,255))]:
    palette=colors+[list(bodycolor),[220,255,0,255]]+[[0,0,0,255]]*6
    file=R/'textures'/('Goalkeeper_'+label+'_palette.png');png(file,palette)
    im=bpy.data.images.load(str(file));im.colorspace_settings.name='sRGB';im.pack()
    mat=source_mat.copy();mat.name='Goalkeeper '+label+' | Packed palette';me.materials[0]=mat
    mat.node_tree.nodes[color_node.name].image=im;mat.node_tree.nodes[surface_node.name].image=propim;mat.diffuse_color=linear(bodycolor)
    for p,idx in zip(me.polygons,indices):
        if p.material_index==0:
            for li in p.loop_indices:col.data[li].color=linear(palette[idx])
    assert fingerprint()==before and me.materials[1]==eye
    o.name='GOALKEEPER '+label.upper()+' | Optimized static';s.name='GOALKEEPER '+label+' | Studio'
    o['palette']='Pastel '+label+' body; front crossbar #DCFF00; source navy wheel caps and sky eyes preserved.'
    s.camera=bpy.data.objects['Camera | 01 Concept'];s.render.filepath=str(R/'previews'/('Goalkeeper_'+label+'.png'))
    me.calc_loop_triangles()
    filepath=R/('Goalkeeper_'+label+'_v01.blend');bpy.ops.wm.save_as_mainfile(filepath=str(filepath));bpy.ops.render.render(write_still=True)
    results.append({'file':str(filepath),'body':list(bodycolor),'front_kickplate':'#DCFF00','wheel_caps':'#1B4994','eye_unchanged':True,'geometry_unchanged':True,'triangles':len(me.loop_triangles),'materials':len(me.materials)})
(R/'validation.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
result=results
