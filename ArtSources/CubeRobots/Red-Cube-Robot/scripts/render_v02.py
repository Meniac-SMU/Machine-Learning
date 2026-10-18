import bpy,json
from pathlib import Path
from mathutils.kdtree import KDTree
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');s=bpy.context.scene
o=bpy.data.objects['RED CUBE ROBOT | Optimized static v02'];me=o.data
exhaust={g.index for g in o.vertex_groups if g.name.startswith('Exhaust')}
pts=[v.co for v in me.vertices if not any(g.group in exhaust for g in v.groups)]
kd=KDTree(len(pts))
for i,p in enumerate(pts):kd.insert(p,i)
kd.balance();err=max(kd.find((-p.x,p.y,p.z))[2] for p in pts)
report=json.loads((ROOT/'validation-v02.json').read_text())
report['symmetry_max_error_except_exhaust']=err
bar=report['bounds_by_part']['Bumper front | Crossbar'];wheel=report['bounds_by_part']['Wheel L | 32 segment unified profile']
report['ground_contact_error']=abs(bar['min'][2]-wheel['min'][2])
assert report['ground_contact_error']<1e-5
assert err<1e-5
assert bar['min'][1]<-1.5
assert bar['max'][0]<wheel['max'][0]
assert all(b['max'][1]<1.007 for n,b in report['bounds_by_part'].items() if n.startswith('Bumper rear'))
(ROOT/'validation-v02.json').write_text(json.dumps(report,indent=2))
outdir=ROOT/'previews-v02';outdir.mkdir(exist_ok=True)
s.cycles.samples=24;s.render.resolution_x=900;s.render.resolution_y=900
ground=bpy.data.objects['Studio | Ground']
for camera,filename,ground_on in [('Camera | 01 Concept','01-concept.png',True),('Camera | 03 Left','03-left.png',False),('Camera | 05 Rear three quarter','05-rear-angle.png',True)]:
    s.camera=bpy.data.objects[camera];ground.hide_render=not ground_on
    s.render.filepath=str(outdir/filename);bpy.ops.render.render(write_still=True)
ground.hide_render=False;s.camera=bpy.data.objects['Camera | 01 Concept']
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Red_Cube_Robot_v02.blend'))
result={'symmetry_error':err,'ground_contact_error':report['ground_contact_error'],'rendered':str(outdir)}
