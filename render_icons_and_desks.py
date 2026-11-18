import bpy
import math
import os
from mathutils import Vector

OUT_MODELS = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"
OUT_ICONS  = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\icons"

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def make_mat(name, rgba, metallic=0.1, roughness=0.42):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat

def rl_pos(p):
    return (p[0], -p[2], p[1])

def rl_scale(s):
    return (s[0], s[2], s[1])

def rl_rot(r):
    return (r[0], -r[2], r[1])

def add_box(name, pos, size, mat, rot=(0,0,0), bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return obj

def add_vcyl(name, pos, radius, height, mat, rot=(0,0,0), vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return obj

def finalize_and_export(filename):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    for poly in obj.data.polygons:
        poly.use_smooth = False
    out_path = os.path.join(OUT_MODELS, filename)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_materials='EXPORT'
    )
    print("Exported:", out_path, os.path.getsize(out_path))

def build_macbook_trap_desk():
    clear_scene()
    m_dark = make_mat("PedestalDark", (0.14, 0.17, 0.23, 1), metallic=0.4, roughness=0.35)
    m_top = make_mat("DeskTop", (0.26, 0.31, 0.40, 1), metallic=0.5, roughness=0.28)
    m_cyan = make_mat("NeonCyan", (0.05, 0.88, 0.98, 1), roughness=0.15)
    m_alu = make_mat("MacAlu", (0.78, 0.82, 0.88, 1), metallic=0.85, roughness=0.20)
    m_keys = make_mat("MacKeys", (0.10, 0.12, 0.16, 1), roughness=0.4)

    add_box("PedestalBase", (0, 0.42, 0), (2.25, 0.84, 1.25), m_dark, bevel=0.03)
    add_box("NeonStripFront", (0, 0.52, 0.63), (2.05, 0.04, 0.02), m_cyan)
    add_box("NeonStripBack", (0, 0.52, -0.63), (2.05, 0.04, 0.02), m_cyan)
    add_box("TableTop", (0, 0.86, 0), (2.48, 0.06, 1.44), m_top, bevel=0.015)
    add_vcyl("HoloEmitterRing", (0, 0.895, 0.05), 0.38, 0.015, m_cyan, vertices=20)
    add_box("MacBody", (0, 0.905, 0.02), (0.60, 0.022, 0.42), m_alu, bevel=0.005)
    add_box("MacKeyboard", (0, 0.918, -0.02), (0.50, 0.006, 0.18), m_keys)
    add_box("MacTrackpad", (0, 0.917, 0.13), (0.18, 0.005, 0.10), m_top)
    add_box("MacLid", (0, 1.12, 0.24), (0.60, 0.40, 0.018), m_alu, bevel=0.005)
    finalize_and_export("prop_macbook_trap_desk.glb")

def build_arsenal_counter():
    clear_scene()
    m_cab = make_mat("ShopCab", (0.16, 0.12, 0.20, 1), roughness=0.4)
    m_crimson = make_mat("ShopCrimson", (0.76, 0.14, 0.28, 1), roughness=0.3)
    m_gold = make_mat("ShopGold", (0.94, 0.74, 0.22, 1), metallic=0.8, roughness=0.22)
    m_peg = make_mat("PegBoard", (0.12, 0.15, 0.22, 1), metallic=0.4, roughness=0.35)
    m_crate = make_mat("AmmoCrate", (0.24, 0.32, 0.22, 1), roughness=0.5)

    add_box("CounterBody", (0, 0.54, 0), (4.10, 1.08, 1.18), m_cab, bevel=0.03)
    add_box("CounterTop", (0, 1.11, 0), (4.30, 0.07, 1.34), m_crimson, bevel=0.015)
    add_box("GoldTrim", (0, 1.05, -0.60), (4.16, 0.04, 0.03), m_gold)
    add_box("BackRack", (0, 2.15, 0.56), (4.10, 2.15, 0.12), m_peg, bevel=0.02)
    add_box("RackHeader", (0, 3.16, 0.52), (4.18, 0.16, 0.18), m_crimson, bevel=0.01)
    for sx in [-1.4, 0.0, 1.4]:
        add_box(f"WeaponCrate_{sx}", (sx, 0.26, -0.68), (0.76, 0.42, 0.46), m_crate, bevel=0.015)
        add_box(f"CrateLatch_{sx}", (sx, 0.34, -0.915), (0.16, 0.06, 0.02), m_gold)
    finalize_and_export("prop_arsenal_counter.glb")

def render_side_profile_icon(glb_name, png_name, tilt_deg=34.0):
    clear_scene()
    glb_path = os.path.join(OUT_MODELS, glb_name)
    bpy.ops.import_scene.gltf(filepath=glb_path)

    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object

    bbox = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    min_v = Vector((min(v.x for v in bbox), min(v.y for v in bbox), min(v.z for v in bbox)))
    max_v = Vector((max(v.x for v in bbox), max(v.y for v in bbox), max(v.z for v in bbox)))
    obj.location -= (min_v + max_v) * 0.5
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # Rotate model 90 deg around Z so its side profile faces -Y camera, and tilt 42 deg diagonally bottom-left to top-right!
    obj.rotation_mode = 'XYZ'
    obj.rotation_euler = (math.radians(14.0), math.radians(42.0), math.radians(-90.0))
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)

    bbox2 = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    min_v2 = Vector((min(v.x for v in bbox2), min(v.y for v in bbox2), min(v.z for v in bbox2)))
    max_v2 = Vector((max(v.x for v in bbox2), max(v.y for v in bbox2), max(v.z for v in bbox2)))
    obj.location -= (min_v2 + max_v2) * 0.5
    span = max(max_v2.x - min_v2.x, max_v2.z - min_v2.z)

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = span * 0.94
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam_obj)

    target = bpy.data.objects.new("Target", None)
    scene.collection.objects.link(target)
    target.location = (0, 0, 0)

    cam_obj.location = (0.0, -2.5, 0.20)
    track = cam_obj.constraints.new(type='TRACK_TO')
    track.target = target
    track.track_axis = 'TRACK_NEGATIVE_Z'
    track.up_axis = 'UP_Y'
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("SunKey",  (1.8, -2.5, 2.0), 5.2, (1.0, 0.96, 0.88)),
        ("SunFill", (-2.0, -2.0, 1.0), 2.8, (0.78, 0.88, 1.0)),
        ("SunRim",  (0.0, 2.0, 2.0),  3.6, (1.0, 0.88, 0.65)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type='SUN')
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        t = lobj.constraints.new(type='TRACK_TO')
        t.target = target
        t.track_axis = 'TRACK_NEGATIVE_Z'
        t.up_axis = 'UP_Y'

    out_png = os.path.join(OUT_ICONS, png_name)
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)
    print("Rendered side-profile icon:", out_png, os.path.getsize(out_png))

build_macbook_trap_desk()
build_arsenal_counter()

render_side_profile_icon("weapon_belt_90s.glb",    "icon_slot_1_belt.png",    28.0)
render_side_profile_icon("weapon_solder_90s.glb",  "icon_slot_2_solder.png",  36.0)
render_side_profile_icon("weapon_uzi_90s.glb",     "icon_slot_3_uzi.png",     28.0)
render_side_profile_icon("weapon_shotgun_90s.glb", "icon_slot_4_shotgun.png", 34.0)
