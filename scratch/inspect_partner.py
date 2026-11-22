import bpy, bmesh, mathutils, json, os

bpy.ops.wm.read_factory_settings(use_empty=True)
glb_path = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models\char_partner_90s.glb"
bpy.ops.import_scene.gltf(filepath=glb_path)

print("=== OBJECTS IN char_partner_90s.glb ===")
for obj in bpy.data.objects:
    print(f"OBJ: {obj.name} type={obj.type} loc={tuple(round(v,4) for v in obj.location)} rot={tuple(round(v,4) for v in obj.rotation_euler)} scale={tuple(round(v,4) for v in obj.scale)}")

# Remember: Blender imports glTF Y-up into Blender Z-up:
# glTF (X, Y, Z) -> Blender (X, -Z, Y)
# Conversely: Blender (bx, by, bz) -> glTF (bx, bz, -by)
def b_to_gltf(v):
    return (round(v.x, 5), round(v.z, 5), round(-v.y, 5))

print("\n=== PER-MATERIAL VERTEX BOUNDS (in glTF Y-up coords!) ===")
all_verts_gltf = []
mat_verts = {}

deps = bpy.context.evaluated_depsgraph_get()
for obj in bpy.data.objects:
    if obj.type != 'MESH':
        continue
    eval_obj = obj.evaluated_get(deps)
    mesh = eval_obj.to_mesh()
    mw = obj.matrix_world
    mats = obj.data.materials
    for poly in mesh.polygons:
        mname = mats[poly.material_index].name if (poly.material_index < len(mats) and mats[poly.material_index]) else "NONE"
        if mname not in mat_verts:
            mat_verts[mname] = []
        for vi in poly.vertices:
            w = mw @ mesh.vertices[vi].co
            g = (w.x, w.z, -w.y)
            mat_verts[mname].append(g)
            all_verts_gltf.append(g)
    eval_obj.to_mesh_clear()

def summarize(pts):
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    zs = [p[2] for p in pts]
    return {
        "count": len(pts),
        "min": (round(min(xs),4), round(min(ys),4), round(min(zs),4)),
        "max": (round(max(xs),4), round(max(ys),4), round(max(zs),4)),
        "center": (round((min(xs)+max(xs))/2,4), round((min(ys)+max(ys))/2,4), round((min(zs)+max(zs))/2,4)),
        "size": (round(max(xs)-min(xs),4), round(max(ys)-min(ys),4), round(max(zs)-min(zs),4)),
    }

print("ALL:", json.dumps(summarize(all_verts_gltf)))
for mname, pts in sorted(mat_verts.items()):
    print(f"MAT [{mname}]:", json.dumps(summarize(pts)))

# Analyze upper head region (glTF Y > 1.35)
head_pts = [p for p in all_verts_gltf if p[1] > 1.38]
print("\n=== HEAD SLICE PROFILE (glTF Y-up) ===")
if head_pts:
    ymin, ymax = min(p[1] for p in head_pts), max(p[1] for p in head_pts)
    print(f"Head overall (Y>1.38): Y=[{ymin:.4f}, {ymax:.4f}]")
    step = 0.02
    y = 1.40
    while y <= ymax + 0.005:
        sl = [p for p in head_pts if abs(p[1] - y) <= 0.012]
        if sl:
            xs = [p[0] for p in sl]
            zs = [p[2] for p in sl]
            cx = (min(xs)+max(xs))/2
            cz = (min(zs)+max(zs))/2
            rx = (max(xs)-min(xs))/2
            rz = (max(zs)-min(zs))/2
            print(f"  Y={y:.3f}: X=[{min(xs):.4f},{max(xs):.4f}] (cx={cx:.4f}, rx={rx:.4f})  Z=[{min(zs):.4f},{max(zs):.4f}] (cz={cz:.4f}, rz={rz:.4f}) n={len(sl)}")
        y += step

# Also inspect skin/hair/eye/nose details specifically for Y > 1.35
print("\n=== HEAD PER-MATERIAL (Y > 1.35) ===")
for mname, pts in sorted(mat_verts.items()):
    hpts = [p for p in pts if p[1] > 1.35]
    if hpts:
        print(f"  HEAD MAT [{mname}]:", json.dumps(summarize(hpts)))

# Render orthographic front, right, top views of the head to inspect visually!
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.resolution_x = 900
scene.render.resolution_y = 450
scene.render.film_transparent = False
world = bpy.data.worlds.new("W")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.2, 0.24, 1)

# Add sun + fill light
def add_light(name, loc, rot, energy):
    ld = bpy.data.lights.new(name, 'SUN')
    ld.energy = energy
    lo = bpy.data.objects.new(name, ld)
    scene.collection.objects.link(lo)
    lo.location = loc
    lo.rotation_euler = rot

import math
add_light("Key", (2, -2, 3), (math.radians(55), 0, math.radians(35)), 3.0)
add_light("Fill", (-2, 2, 2), (math.radians(60), 0, math.radians(-140)), 1.5)

# Save front and side head renders
cam_data = bpy.data.cameras.new("Cam")
cam_obj = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam_obj)
scene.camera = cam_obj

# Head center in Blender coords: let's compute from head_pts
if head_pts:
    hy = (min(p[1] for p in head_pts) + max(p[1] for p in head_pts)) / 2
    hx = (min(p[0] for p in head_pts) + max(p[0] for p in head_pts)) / 2
    hz_g = (min(p[2] for p in head_pts) + max(p[2] for p in head_pts)) / 2
    # Blender center: (hx, -hz_g, hy)
    target = mathutils.Vector((hx, -hz_g, hy))
    print(f"Head center glTF=({hx:.4f}, {hy:.4f}, {hz_g:.4f})  Blender=({target.x:.4f}, {target.y:.4f}, {target.z:.4f})")

    out_dir = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\scratch"
    os.makedirs(out_dir, exist_ok=True)
    for label, offset in [
        ("front_posZ", (0, -0.75, 0)),   # looking from glTF +Z (Blender -Y) toward origin
        ("back_negZ",  (0, +0.75, 0)),   # looking from glTF -Z (Blender +Y) toward origin
        ("side_posX",  (0.75, 0, 0)),    # looking from +X
    ]:
        cam_obj.location = target + mathutils.Vector(offset)
        direction = target - cam_obj.location
        cam_obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
        cam_data.lens = 65
        scene.render.resolution_x = 512
        scene.render.resolution_y = 512
        scene.render.filepath = os.path.join(out_dir, f"inspect_partner_{label}.png")
        bpy.ops.render.render(write_still=True)
print("INSPECT_DONE")
