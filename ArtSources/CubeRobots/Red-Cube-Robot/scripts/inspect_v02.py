import bpy
s=bpy.context.scene
result={'file':bpy.data.filepath,'dirty':bpy.data.is_dirty,'scene':s.name,'objects':[{'name':o.name,'location':list(o.location),'dimensions':list(o.dimensions),'parent':o.parent.name if o.parent else None} for o in bpy.data.collections['ROBOT | Model'].objects if o.type=='MESH'],'materials':[m.name for m in bpy.data.materials],'screens':[a.spaces.active.shading.type for a in bpy.context.screen.areas if a.type=='VIEW_3D']}
