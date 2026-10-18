import bpy,json
from pathlib import Path
from mathutils.kdtree import KDTree
R=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');s=bpy.context.scene
o=bpy.data.objects['RED CUBE ROBOT | Optimized static v02'];me=o.data
assert 'v02' in bpy.data.filepath
wheelgroups={g.index for g in o.vertex_groups if g.name.startswith('Wheel ')}
wheelverts=[v for v in me.vertices if any(g.group in wheelgroups for g in v.groups)]
before={'min_abs_x':min(abs(v.co.x) for v in wheelverts),'max_abs_x':max(abs(v.co.x) for v in wheelverts),'min_z':min(v.co.z for v in wheelverts)}
# Shift both wheels inwards, and seat their inner shoulders inside the body chamfer.
# Retain diameter, ground contact, outer profile and the wider-than-kickplate constraint.
for v in wheelverts:
    x=abs(v.co.x);sign=1 if v.co.x>0 else -1
    v.co.x=sign*(.89 if x<1.036 else x-.045)
me.update()
after={'min_abs_x':min(abs(v.co.x) for v in wheelverts),'max_abs_x':max(abs(v.co.x) for v in wheelverts),'min_z':min(v.co.z for v in wheelverts)}
assert abs(before['min_z']-after['min_z'])<1e-7
assert after['max_abs_x']>1.32
assert after['min_abs_x']<.895
exhaust={g.index for g in o.vertex_groups if g.name.startswith('Exhaust')}
pts=[v.co for v in me.vertices if not any(g.group in exhaust for g in v.groups)]
kd=KDTree(len(pts))
for i,p in enumerate(pts):kd.insert(p,i)
kd.balance();err=max(kd.find((-p.x,p.y,p.z))[2] for p in pts)
assert err<1e-5
o.name='RED CUBE ROBOT | Optimized static v03'
o['wheel_attachment']='Inner shoulders embedded through side chamfer; no body-to-wheel clearance.'
me.calc_loop_triangles()
report={'before':before,'after':after,'symmetry_error_except_exhaust':err,'triangles':len(me.loop_triangles),'material_slots':len(me.materials),'mesh_objects':sum(x.type=='MESH' for x in bpy.data.collections['ROBOT | Model'].objects)}
(R/'validation-v03.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
out=R/'previews-v03';out.mkdir(exist_ok=True)
s.camera=bpy.data.objects['Camera | 01 Concept'];s.render.filepath=str(out/'01-concept.png')
bpy.ops.wm.save_as_mainfile(filepath=str(R/'Red_Cube_Robot_v03.blend'))
bpy.ops.render.render(write_still=True)
result=report
