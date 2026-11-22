import bpy, json

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models\char_partner_90s.glb")

obj = bpy.data.objects["char_partner_90s"]
mesh = obj.data
mats = mesh.materials

# Separate connected components per material in head (Y > 1.45 in glTF)
import bmesh
bm = bmesh.new()
bm.from_mesh(mesh)
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()

# Find connected face islands
visited = set()
islands = []
for f in bm.faces:
    if f.index in visited:
        continue
    mname = mats[f.material_index].name if f.material_index < len(mats) else "NONE"
    stack = [f]
    visited.add(f.index)
    verts = set()
    while stack:
        cf = stack.pop()
        for v in cf.verts:
            verts.add(v.index)
            for lf in v.link_faces:
                if lf.index not in visited and lf.material_index == f.material_index:
                    visited.add(lf.index)
                    stack.append(lf)
    pts = [(obj.matrix_world @ bm.verts[vi].co) for vi in verts]
    gpts = [(p.x, p.z, -p.y) for p in pts]
    ys = [p[1] for p in gpts]
    if max(ys) > 1.44:
        xs = [p[0] for p in gpts]
        zs = [p[2] for p in gpts]
        islands.append({
            "mat": mname,
            "verts": len(verts),
            "min": [round(min(xs),4), round(min(ys),4), round(min(zs),4)],
            "max": [round(max(xs),4), round(max(ys),4), round(max(zs),4)],
            "center": [round((min(xs)+max(xs))/2,4), round((min(ys)+max(ys))/2,4), round((min(zs)+max(zs))/2,4)],
            "size": [round(max(xs)-min(xs),4), round(max(ys)-min(ys),4), round(max(zs)-min(zs),4)],
        })

islands.sort(key=lambda d: (d["mat"], d["center"][0]))
print("=== HEAD ISLANDS (glTF Y-up) ===")
for isl in islands:
    print(f"  {isl['mat']:10s} n={isl['verts']:4d} min={isl['min']} max={isl['max']} ctr={isl['center']} sz={isl['size']}")

# Also inspect prop_bug_frog.glb
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models\prop_bug_frog.glb")
print("\n=== prop_bug_frog.glb ===")
for o in bpy.data.objects:
    if o.type == 'MESH':
        pts = [(o.matrix_world @ v.co) for v in o.data.vertices]
        gpts = [(p.x, p.z, -p.y) for p in pts]
        xs = [p[0] for p in gpts]; ys = [p[1] for p in gpts]; zs = [p[2] for p in gpts]
        print(f"  OBJ {o.name}: mats={[m.name for m in o.data.materials]} min=({min(xs):.3f},{min(ys):.3f},{min(zs):.3f}) max=({max(xs):.3f},{max(ys):.3f},{max(zs):.3f})")
