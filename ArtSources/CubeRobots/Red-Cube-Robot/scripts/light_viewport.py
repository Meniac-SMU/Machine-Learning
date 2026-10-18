import bpy
changed=[]
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active
            sp.shading.type='SOLID'
            sp.shading.light='STUDIO'
            sp.shading.color_type='MATERIAL'
            sp.shading.show_shadows=True
            sp.shading.show_cavity=False
            if hasattr(sp.shading,'use_compositor'):sp.shading.use_compositor='DISABLED'
            changed.append(screen.name)
# Solid mode uses diffuse_color; keep the pink eyes visible without rendering.
bpy.data.materials['08 | Pink luminous pixels'].diffuse_color=(1.0,.20,.40,1)
result={'viewport_mode':'SOLID / material colors','compositor':'DISABLED','changed_screens':changed,'model_geometry_unchanged':True,'file':bpy.data.filepath,'dirty':bpy.data.is_dirty}
