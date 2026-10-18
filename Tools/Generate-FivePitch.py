import bpy,json,math
from mathutils import Vector
from mathutils.geometry import tessellate_polygon
from pathlib import Path
root=Path(r'C:\GitHub\Machine-Learning');d=json.load(open(root/'Tools/FivePitchSource.json')); ps=d['polys']
out=root/'Assets/_Soccer/Manager5v5/Art/Pitch';out.mkdir(parents=True,exist_ok=True)
gv=[];gt=[];lv=[];lt=[];checks=[]
def add(poly,vs,ts,lift=0):
 pts=[Vector((-v[0]*.01,v[1]*.01+lift,v[2]*.01)) for v in poly]
 base=len(vs);vs.extend([dict(x=v.x,y=v.y,z=v.z) for v in pts])
 for tri in tessellate_polygon([pts]):
  ids=list(tri)
  if (pts[ids[1]]-pts[ids[0]]).cross(pts[ids[2]]-pts[ids[0]]).y<0:ids.reverse()
  ts.extend(base+i for i in ids)
# Full original surface becomes independently editable grass; original bounds and topology retained.
for p in ps:add(p['v'],gv,gt)
# Preserve original arc strip cross sections exactly, halving their width around their midpoints.
arc=list(range(8,18))+list(range(19,35))+list(range(90,100))+list(range(102,118))
for idx in arc:
 q=[Vector(v) for v in ps[idx]['v']]
 edges=[(q[(i+1)%4]-q[i]).length for i in range(4)]
 k=0 if edges[0]+edges[2]<edges[1]+edges[3] else 1
 pairs=[(k,(k+1)%4),((k+2)%4,(k+3)%4)];n=q.copy()
 for a,b in pairs:
  c=(q[a]+q[b])*.5;n[a]=c+(q[a]-c)*.5;n[b]=c+(q[b]-c)*.5
  checks.append(dict(kind='arc',source=idx,midpointError=((n[a]+n[b])*.5-c).length,widthRatio=(n[a]-n[b]).length/(q[a]-q[b]).length))
 add(n,lv,lt,.012)
# Straight marking centers and widths are measured from opposite edges in the original mesh.
# Different original widths are retained proportionally, not replaced with a uniform invented width.
y=10.582
segments=[]
def strip(axis,c,w,a,b,label):
 nw=w*.5
 if axis=='x':q=[[c-nw/2,y,a],[c+nw/2,y,a],[c+nw/2,y,b],[c-nw/2,y,b]]
 else:q=[[a,y,c-nw/2],[b,y,c-nw/2],[b,y,c+nw/2],[a,y,c+nw/2]]
 add(q,lv,lt,.012);checks.append(dict(kind=label,center=c,oldWidth=w,newWidth=nw,midpointError=0,widthRatio=.5))
# Outer boundary center positions, from original inner and outer edges.
def bounds(idx,axis):
 vals=[v[axis] for v in ps[idx]['v']];return min(vals),max(vals)
xInner,xOuter=bounds(130,0);zOuterLow,zInnerLow=bounds(122,2);zInnerHigh,zOuterHigh=bounds(121,2)
penX=bounds(136,0);penZ0=bounds(132,2);penZ1=bounds(133,2)
smallX=bounds(146,0);smallZ0=bounds(144,2);smallZ1=bounds(145,2)
xc=(xOuter+xInner)/2;wx=xOuter-xInner;zl=(zOuterLow+zInnerLow)/2;zh=(zOuterHigh+zInnerHigh)/2;wl=zInnerLow-zOuterLow;wh=zOuterHigh-zInnerHigh
for side in [-1,1]:strip('x',side*xc,wx,zl-wl/4,zh+wh/4,'touchline-x')
strip('z',zl,wl,-xc-wx/4,xc+wx/4,'touchline-z');strip('z',zh,wh,-xc-wx/4,xc+wx/4,'touchline-z')
for side in [-1,1]:
 for xlo,xhi,zlo,zli,zhi,zho,label in [(*penX,*penZ0,*penZ1,'penalty'),(*smallX,*smallZ0,*smallZ1,'goal-area')]:
  cx=(xlo+xhi)/2;ww=xhi-xlo;za=(zlo+zli)/2;zb=(zhi+zho)/2;wa=zli-zlo;wb=zho-zhi
  strip('x',side*cx,ww,za-wa/4,zb+wb/4,label+'-front')
  for zz,wz in [(za,wa),(zb,wb)]:strip('z',zz,wz,min(side*cx,side*xc),max(side*cx,side*xc),label+'-side')
# Center stripe: use original paired x boundaries on each original section.
for idx in [100,120,165,166,167,222]:
 q=[Vector(v) for v in ps[idx]['v']];n=q.copy()
 # pair vertices sharing their longitudinal endpoint
 order=sorted(range(4),key=lambda i:q[i].z)
 for a,b in [order[:2],order[2:]]:
  c=(q[a]+q[b])*.5;n[a]=c+(q[a]-c)*.5;n[b]=c+(q[b]-c)*.5
  checks.append(dict(kind='center',source=idx,midpointError=((n[a]+n[b])*.5-c).length,widthRatio=(n[a]-n[b]).length/(q[a]-q[b]).length))
 add(n,lv,lt,.012)
strip('x',0,54.4,zl,-1988.84,'center-end');strip('x',0,55.8,1988.848,zh,'center-end')
# Continue the existing penalty arc tangents into the narrowed vertical line at the same intersection.
for idx in [10,11,92,93]:
 q=[Vector(v) for v in ps[idx]['v']];edge=min([(i,(i+1)%4) for i in range(4)],key=lambda ab:abs(q[ab[0]].x-q[ab[1]].x))
 a,b=edge;c=(q[a]+q[b])*.5;pa=c+(q[a]-c)*.5;pb=c+(q[b]-c)*.5
 target=math.copysign(sum(penX)/2,c.x);ea=pa.copy();eb=pb.copy();ea.x=eb.x=target
 add([pa,pb,eb,ea],lv,lt,.012)
# Close the circle/center-stripe junction along its unchanged tangent.
for idx in [19,34,102,117]:
 q=[Vector(v) for v in ps[idx]['v']]
 a,b=min([(i,(i+1)%4) for i in range(4)],key=lambda ab:abs((q[ab[0]].x+q[ab[1]].x)/2))
 c=(q[a]+q[b])*.5;pa=c+(q[a]-c)*.5;pb=c+(q[b]-c)*.5;ea=pa.copy();eb=pb.copy();ea.x=eb.x=0
 add([pa,pb,eb,ea],lv,lt,.012)
json.dump(dict(grassVertices=gv,grassTriangles=gt,lineVertices=lv,lineTriangles=lt),open(out/'PitchGeometry.json','w'))
json.dump(dict(checks=checks,maxMidpointError=max(c['midpointError'] for c in checks),widthRatioMin=min(c['widthRatio'] for c in checks),widthRatioMax=max(c['widthRatio'] for c in checks),grassTriangles=len(gt)//3,lineTriangles=len(lt)//3),open(root/'Logs/Manager5v5/pitch-split-20261018/geometry-validation.json','w'),indent=2)
print('GENERATED',len(gt)//3,len(lt)//3)



