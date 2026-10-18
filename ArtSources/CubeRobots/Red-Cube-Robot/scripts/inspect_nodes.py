import bpy
s=bpy.context.scene;n=s.compositing_node_group.nodes.get('Glare')
result={'props':[(p.identifier,p.type) for p in n.bl_rna.properties],'inputs':[(x.name,x.type,str(getattr(x,'default_value',''))) for x in n.inputs], 'body_dims':list(bpy.data.objects['Body | Exact 2 x 2 x 2 cube'].dimensions)}
