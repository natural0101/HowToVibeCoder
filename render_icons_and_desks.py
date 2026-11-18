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
    walnut=make_mat("Smoked_Walnut",(.16,.075,.035,1),roughness=.38)
    grain=make_mat("Walnut_Grain",(.085,.037,.017,1),roughness=.44)
    titanium=make_mat("Matte_Titanium",(.22,.25,.28,1),metallic=.78,roughness=.44)
    alu=make_mat("MacBook_Aluminum",(.60,.65,.70,1),metallic=.83,roughness=.27)
    black=make_mat("MacBook_Keyboard",(.012,.016,.022,1),roughness=.48)
    display=make_mat("MacBook_Display",(.018,.065,.09,1),roughness=.25)
    white=make_mat("Logo_Glow",(.78,.87,.92,1),roughness=.3)
    bsdf=white.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs['Emission Color'].default_value=(.5,.65,.8,1)
    bsdf.inputs['Emission Strength'].default_value=.45
    add_box("Thin_Walnut_Slab",(0,.88,0),(2.48,.12,1.44),walnut,bevel=.035)
    add_box("Titanium_Underframe",(0,.795,0),(2.20,.045,1.20),titanium,bevel=.018)
    for side in [-1,1]:
        add_box("Splayed_Titanium_Blade",(side*.87,.40,0),(.115,.78,.97),titanium,rot=(0,0,side*-.24),bevel=.025)
        add_box("Recessed_Foot",(side*.96,.035,0),(.34,.055,1.04),black,bevel=.02)
    for i in range(23):
        add_box("Walnut_Fine_Grain",(math.sin(i*4)*.13,.941,-.66+i*.057),(2.10+(i%3)*.08,.001,.003+(i%2)*.002),grain)
    add_box("MacBook_Unibody",(0,.968,.02),(.68,.035,.46),alu,bevel=.012)
    add_box("Keyboard_Recess",(0,.988,-.045),(.57,.005,.205),black,bevel=.007)
    for row in range(5):
        for col in range(12):
            add_box("Individual_Key",(-.254+col*.046, .994,-.124+row*.036),(.037,.005,.027),titanium,bevel=.003)
    add_box("Spacebar",(0,.996,.046),(.22,.005,.02),black,bevel=.003)
    add_box("Trackpad_Reveal",(0,.988,.155),(.25,.002,.12),titanium,bevel=.008)
    add_box("Trackpad",(0,.990,.155),(.238,.002,.108),alu,bevel=.007)
    for side in [-1,1]:
        for i in range(18):
            add_box("Speaker_Slot",(side*.31,.989,-.13+i*.011),(.019,.002,.003),black)
        for i in range(2):
            add_box("USB_C_Port",(side*.341,.969,-.12+i*.07),(.002,.01,.026),black,bevel=.003)
    # Lid tilted away from keyboard. Local coordinates keep display and logo on opposite faces.
    angle=.15
    def lid_part(name,x,y,depth,w,h,d,mat,bevel=0):
        pos=(x,.995+y*math.cos(angle)-depth*math.sin(angle),.245+y*math.sin(angle)+depth*math.cos(angle))
        return add_box(name,pos,(w,h,d),mat,rot=(angle,0,0),bevel=bevel)
    lid_part("Slim_Aluminum_Lid",0,.215,0,.68,.43,.016,alu,.009)
    lid_part("Black_Display_Bezel",0,.215,-.010,.652,.40,.003,black,.006)
    lid_part("Inset_Dark_Display",0,.215,-.013,.62,.366,.002,display,.004)
    for row in range(7): lid_part("Editor_Line",-.06,.10+row*.031,-.015,.33-row%3*.06,.004,.001,titanium)
    lid_part("Camera_Notch",0,.398,-.015,.065,.019,.002,black)
    # Small bitten-apple silhouette on rear, not a luminous rectangle.
    points=[(-.002,.037),(-.018,.046),(-.038,.038),(-.047,.018),(-.045,-.008),(-.030,-.036),(-.014,-.041),(0,-.034),(.015,-.041),(.031,-.026),(.040,-.008),(.026,.002),(.023,.016),(.034,.028),(.022,.043),(.008,.045)]
    verts=[rl_pos((x, .995+(.235+y)*math.cos(angle)-.011*math.sin(angle),.245+(.235+y)*math.sin(angle)+.011*math.cos(angle))) for x,y in points]
    mesh=bpy.data.meshes.new("Apple_Logo"); mesh.from_pydata(verts,[],[tuple(range(len(verts)))]); mesh.materials.append(white)
    obj=bpy.data.objects.new("Small_Rear_Apple_Logo",mesh); bpy.context.collection.objects.link(obj)
    lid_part("Logo_Leaf",.010,.291,.011,.012,.021,.001,white,.005)
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

if "--macbook-only" in __import__("sys").argv:
    build_macbook_trap_desk()
else:
    build_macbook_trap_desk()
    build_arsenal_counter()

    render_side_profile_icon("weapon_belt_90s.glb",    "icon_slot_1_belt.png",    28.0)
    render_side_profile_icon("weapon_solder_90s.glb",  "icon_slot_2_solder.png",  36.0)
    render_side_profile_icon("weapon_uzi_90s.glb",     "icon_slot_3_uzi.png",     28.0)
    render_side_profile_icon("weapon_shotgun_90s.glb", "icon_slot_4_shotgun.png", 34.0)
