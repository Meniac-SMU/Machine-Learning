import bpy
from mathutils import Vector,kdtree
obs=[o for o in bpy.data.collections['ROBOT | Model'].objects if o.type=='MESH' and not o.name.startswith('Exhaust ')]
verts=[(o.name,o.matrix_world@v.co) for o in obs for v in o.data.vertices]
t=kdtree.KDTree(len(verts))
for i,(_,v) in enumerate(verts):t.insert(v,i)
t.balance()
result={'unmatched':[(name,list(v),t.find(Vector((-v.x,v.y,v.z)))[2]) for name,v in verts if t.find(Vector((-v.x,v.y,v.z)))[2]>1e-5]}
