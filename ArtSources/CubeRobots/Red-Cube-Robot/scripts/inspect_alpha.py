import bpy
n=bpy.context.scene.compositing_node_group.nodes.get('Alpha Over')
result={'inputs':[(x.name,x.type,str(getattr(x,'default_value',''))) for x in n.inputs]}
