import bpy
bpy.ops.wm.read_factory_settings(use_empty=True)
obj_path = r"C:\Users\se-20\AppData\Roaming\Blender Foundation\Blender\5.1\extensions\blender_org\mpfb\data\3dobjs\base.obj"
bpy.ops.wm.obj_import(filepath=obj_path)
obj = bpy.context.selected_objects[0]
print("Imported base.obj:", obj.name, "Verts:", len(obj.data.vertices), "Polys:", len(obj.data.polygons))
print("Dimensions:", obj.dimensions[:], "BBox:", [v[:] for v in obj.bound_box])
vg = [g.name for g in obj.vertex_groups]
print("Vertex groups:", vg[:20])