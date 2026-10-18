import bpy,json
s=bpy.context.scene;dg=bpy.context.evaluated_depsgraph_get();rows=[]
for ob in bpy.data.collections['ROBOT | Model'].objects:
    if ob.type!='MESH':continue
    ev=ob.evaluated_get(dg);me=ev.to_mesh();me.calc_loop_triangles()
    rows.append({'name':ob.name,'triangles':len(me.loop_triangles),'vertices':len(me.vertices),'materials':[m.name for m in me.materials if m],'modifiers':[m.type for m in ob.modifiers]})
    ev.to_mesh_clear()
result={'render_engine':s.render.engine,'cycles_device':s.cycles.device,'preview_samples':s.cycles.preview_samples,'viewport':[{'shading':a.spaces.active.shading.type,'compositor':getattr(a.spaces.active.shading,'use_compositor',None)} for a in bpy.context.screen.areas if a.type=='VIEW_3D'],'model_mesh_count':len(rows),'unique_material_count':len({m for r in rows for m in r['materials']}),'triangles':sum(r['triangles'] for r in rows),'vertices':sum(r['vertices'] for r in rows),'mesh_rows':sorted(rows,key=lambda r:r['triangles'],reverse=True)}
