import bpy,json,math
from pathlib import Path
from mathutils import Vector,kdtree
ROOT=Path(r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Red-Cube-Robot');s=bpy.context.scene;model=bpy.data.collections['ROBOT | Model']
bpy.context.view_layer.update()
dg=bpy.context.evaluated_depsgraph_get()
verts=[];triangles=0
for ob in model.objects:
    if ob.type!='MESH':continue
    ev=ob.evaluated_get(dg);me=ev.to_mesh();me.calc_loop_triangles();triangles+=len(me.loop_triangles)
    if not ob.name.startswith('Exhaust '):verts.extend(ev.matrix_world@v.co for v in me.vertices)
    ev.to_mesh_clear()
tree=kdtree.KDTree(len(verts))
for i,v in enumerate(verts):tree.insert(v,i)
tree.balance();distances=[tree.find(Vector((-v.x,v.y,v.z)))[2] for v in verts]
body=bpy.data.objects['Body | Exact 2 x 2 x 2 cube']
report={'body_dimensions':list(body.dimensions),'body_equal_axes':max(body.dimensions)-min(body.dimensions)<1e-6,'model_meshes':sum(o.type=='MESH' for o in model.objects),'evaluated_triangles':triangles,'symmetry_excluding_exhaust_max_vertex_distance':max(distances),'symmetry_unmatched_vertex_count':sum(d>1e-5 for d in distances),'symmetry_vertex_count':len(verts),'rear_exhaust_side':'Rear view right (world -X)','export_performed':False}
(ROOT/'model-validation.json').write_text(json.dumps(report,indent=2),encoding='utf-8')
glow=s.compositing_node_group.nodes['Glare'];glow.inputs['Threshold'].default_value=.45;glow.inputs['Size'].default_value=.10;glow.inputs['Strength'].default_value=.8
s.camera=bpy.data.objects['Camera | 01 Concept'];s.render.resolution_percentage=100;s.render.filepath=str(ROOT/'previews'/'01-concept.png')
for ob in s.objects:ob.select_set(False)
root=bpy.data.objects['RED CUBE ROBOT | Root'];root.select_set(True);bpy.context.view_layer.objects.active=root
# Overlays hide camera/light gizmos while retaining the real studio lighting.
for ob in bpy.data.collections['STUDIO | Preview only'].objects:
    if ob.type in {'LIGHT','CAMERA'}:ob.hide_set(False)
s.cycles.preview_samples=32;s.cycles.use_preview_denoising=True
for screen in bpy.data.screens:
    for ar in screen.areas:
        if ar.type=='VIEW_3D':
            sp=ar.spaces.active;sp.shading.type='RENDERED';sp.shading.use_scene_world=True;sp.shading.use_scene_lights=True
            if hasattr(sp.shading,'use_compositor'):sp.shading.use_compositor='ALWAYS'
            sp.overlay.show_overlays=False
            sp.region_3d.view_rotation=s.camera.rotation_euler.to_quaternion();sp.region_3d.view_distance=4.8;sp.region_3d.view_location=(0,-.05,1.04);sp.region_3d.view_perspective='ORTHO'
        if ar.type=='PROPERTIES':ar.spaces.active.context='MATERIAL'
s['review_views']='Cameras 01 Concept / 02 Front / 03 Left / 04 Rear / 05 Rear three quarter'
s['model_note']='Only exhaust is asymmetric. Eye material emits pink. No export. Review cameras and studio are separate from model collection.'
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Red_Cube_Robot_v01.blend'))
result=report
