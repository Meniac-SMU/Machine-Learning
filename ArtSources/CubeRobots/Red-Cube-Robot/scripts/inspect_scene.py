import bpy
result={'version':bpy.app.version_string,'file':bpy.data.filepath,'dirty':bpy.data.is_dirty,'objects':[(o.name,o.type) for o in bpy.context.scene.objects],'scenes':[s.name for s in bpy.data.scenes]}
