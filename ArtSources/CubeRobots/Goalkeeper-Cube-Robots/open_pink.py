import bpy
if bpy.data.is_dirty:bpy.ops.wm.save_as_mainfile(filepath=r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots\Goalkeeper_Sky_before_pink_edit.blend',copy=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\GitHub\Machine-Learning\ArtSources\CubeRobots\Goalkeeper-Cube-Robots\Goalkeeper_Pink_v01.blend')
result={'file':bpy.data.filepath}
