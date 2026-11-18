import bpy
import bmesh
import math
import os
from mathutils import Vector, Matrix

from pathlib import Path

ASSETS = Path(__file__).resolve().parent / "assets"
CHAIR_SOURCE = ASSETS / "external_cc0/SheenChair"
OUT_MODELS = str(ASSETS / "models")
OUT_ICONS = str(ASSETS / "icons")
os.makedirs(OUT_MODELS, exist_ok=True)
os.makedirs(OUT_ICONS, exist_ok=True)

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

# Convert Raylib coordinates (X right, Y up, Z forward) -> Blender coordinates (X right, Y=-Z, Z=Y)
# Because Blender glTF exporter maps (+X -> +X, +Z -> +Y, -Y -> +Z)!
def rl_pos(p):
    return (p[0], -p[2], p[1])

def rl_scale(s):
    return (s[0], s[2], s[1])

def rl_rot(r):
    # r is (pitch_around_X, yaw_around_Y, roll_around_Z) in Raylib
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

# Vertical cylinder in Raylib (along Y up) -> in Blender default cylinder is ALREADY along Z up!
def add_vcyl(name, pos, radius, height, mat, rot=(0,0,0), vertices=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=height, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return obj

# Rod between two Raylib 3D points p0 -> p1 with radius r0, r1
def add_rod(name, p0, p1, r0, r1, mat, vertices=14):
    v0 = Vector(rl_pos(p0))
    v1 = Vector(rl_pos(p1))
    d = v1 - v0
    length = d.length
    if length < 1e-5:
        return None
    mid = (v0 + v1) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r0, radius2=r1, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    z_axis = Vector((0, 0, 1))
    quat = z_axis.rotation_difference(d.normalized())
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return obj

def add_sphere(name, pos, radius, mat, scale=(1,1,1), segments=14, ring_count=10):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=ring_count, radius=radius, location=rl_pos(pos))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj

def finalize_and_export(filename, preserve_smoothing=False):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    bm=bmesh.new();bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    if not preserve_smoothing:
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

# ==============================================================================
# 1. PROP: OFFICE WATER COOLER (prop_water_cooler.glb)
# ==============================================================================
def build_water_cooler():
    clear_scene()
    m_body = make_mat("CoolerBody", (0.91, 0.94, 0.97, 1), roughness=0.32)
    m_side = make_mat("CoolerSide", (0.74, 0.79, 0.86, 1), roughness=0.38)
    m_recess = make_mat("CoolerRecess", (0.14, 0.18, 0.24, 1), roughness=0.28)
    m_grill = make_mat("CoolerGrill", (0.28, 0.34, 0.42, 1), metallic=0.6, roughness=0.3)
    m_bottle = make_mat("WaterBottle", (0.16, 0.64, 0.98, 1), roughness=0.14)
    m_water = make_mat("WaterInner", (0.06, 0.46, 0.88, 1), roughness=0.1)
    m_tap_hot = make_mat("TapHot", (0.95, 0.18, 0.22, 1), roughness=0.25)
    m_tap_cold = make_mat("TapCold", (0.12, 0.56, 0.98, 1), roughness=0.25)
    m_cup = make_mat("CupWhite", (0.96, 0.97, 0.99, 1), roughness=0.35)

    add_box("Base", (0, 0.52, 0), (0.46, 1.04, 0.46), m_body, bevel=0.025)
    add_box("SideTrimL", (-0.232, 0.52, 0), (0.016, 0.96, 0.36), m_side)
    add_box("SideTrimR", (0.232, 0.52, 0), (0.016, 0.96, 0.36), m_side)
    add_box("LowerDoor", (0, 0.28, 0.232), (0.38, 0.44, 0.02), m_side, bevel=0.008)
    add_box("Recess", (0, 0.76, 0.21), (0.30, 0.28, 0.06), m_recess)
    add_box("DripTray", (0, 0.62, 0.24), (0.32, 0.04, 0.09), m_grill, bevel=0.006)

    # Hot & cold spigots on front (+Z)
    add_rod("TapHot", (-0.065, 0.84, 0.19), (-0.065, 0.84, 0.25), 0.016, 0.014, m_tap_hot, 10)
    add_box("LeverHot", (-0.065, 0.86, 0.255), (0.035, 0.014, 0.028), m_tap_hot)
    add_rod("TapCold", (0.065, 0.84, 0.19), (0.065, 0.84, 0.25), 0.016, 0.014, m_tap_cold, 10)
    add_box("LeverCold", (0.065, 0.86, 0.255), (0.035, 0.014, 0.028), m_tap_cold)

    # Top collar & 19L polycarbonate water jug with molded ribs
    add_vcyl("Collar", (0, 1.06, 0), 0.15, 0.05, m_side, vertices=18)
    add_vcyl("Neck", (0, 1.12, 0), 0.068, 0.10, m_bottle, vertices=16)
    add_vcyl("BottleBody", (0, 1.38, 0), 0.19, 0.44, m_bottle, vertices=20)
    add_vcyl("WaterCore", (0, 1.35, 0), 0.172, 0.36, m_water, vertices=18)
    for ry in [1.22, 1.34, 1.46]:
        add_vcyl(f"Rib_{ry}", (0, ry, 0), 0.20, 0.028, m_bottle, vertices=20)
    add_rod("BottleCrown", (0, 1.60, 0), (0, 1.68, 0), 0.19, 0.11, m_bottle, 20)
    add_vcyl("BottleCap", (0, 1.69, 0), 0.09, 0.025, m_water, vertices=14)

    # Side cup dispenser tube
    add_vcyl("CupTube", (0.265, 0.78, 0.10), 0.044, 0.34, m_cup, vertices=14)
    add_box("CupBracket", (0.235, 0.78, 0.10), (0.05, 0.04, 0.04), m_grill)

    finalize_and_export("prop_water_cooler.glb")

# ==============================================================================
# 2. PROP: VENDING MACHINE (prop_vending_machine.glb)
# ==============================================================================
def build_vending_machine():
    clear_scene()
    m_cab = make_mat("VendCab", (0.12, 0.15, 0.22, 1), metallic=0.35, roughness=0.38)
    m_trim = make_mat("VendTrim", (0.24, 0.29, 0.38, 1), metallic=0.55, roughness=0.3)
    m_interior = make_mat("VendIn", (0.86, 0.91, 0.97, 1), roughness=0.22)
    m_shelf = make_mat("VendShelf", (0.30, 0.35, 0.42, 1), metallic=0.6, roughness=0.3)
    m_can_red = make_mat("CanRed", (0.94, 0.18, 0.24, 1), metallic=0.45, roughness=0.25)
    m_can_cyan = make_mat("CanCyan", (0.10, 0.80, 0.96, 1), metallic=0.45, roughness=0.25)
    m_can_gold = make_mat("CanGold", (0.96, 0.74, 0.16, 1), metallic=0.45, roughness=0.25)
    m_screen = make_mat("VendScreen", (0.15, 0.88, 0.98, 1), roughness=0.15)
    m_flap = make_mat("VendFlap", (0.08, 0.10, 0.14, 1), roughness=0.4)

    add_box("Cab", (0, 1.08, 0), (1.18, 2.16, 0.86), m_cab, bevel=0.03)
    add_box("HeaderGlow", (0, 2.02, 0.435), (1.06, 0.16, 0.03), m_screen)
    add_box("WindowBack", (-0.14, 1.24, 0.415), (0.74, 1.26, 0.04), m_interior)
    add_box("WindowFrame", (-0.14, 1.24, 0.435), (0.78, 1.30, 0.02), m_trim)

    mats = [m_can_red, m_can_cyan, m_can_gold]
    for s_idx, sy in enumerate([0.74, 1.02, 1.30, 1.58]):
        add_box(f"Shelf_{s_idx}", (-0.14, sy, 0.425), (0.70, 0.025, 0.10), m_shelf)
        for c_idx in range(5):
            cx = -0.40 + c_idx * 0.13
            mat = mats[(s_idx + c_idx) % 3]
            add_vcyl(f"Can_{s_idx}_{c_idx}", (cx, sy + 0.075, 0.425), 0.036, 0.115, mat, vertices=12)

    add_box("ControlPanel", (0.40, 1.24, 0.435), (0.24, 1.26, 0.03), m_trim, bevel=0.01)
    add_box("PayDisplay", (0.40, 1.62, 0.452), (0.16, 0.12, 0.015), m_screen)
    add_box("Keypad", (0.40, 1.38, 0.452), (0.14, 0.20, 0.015), m_flap)
    add_box("BillSlot", (0.40, 1.14, 0.452), (0.14, 0.08, 0.02), m_can_gold)
    add_box("PickupFlap", (-0.14, 0.34, 0.435), (0.74, 0.28, 0.04), m_flap, bevel=0.01)
    add_box("PickupTrim", (-0.14, 0.34, 0.452), (0.66, 0.04, 0.02), m_trim)

    finalize_and_export("prop_vending_machine.glb")

# ==============================================================================
# 3. PROP: METAL FILING CABINET (prop_filing_cabinet.glb)
# ==============================================================================
def build_filing_cabinet():
    clear_scene()
    m_steel = make_mat("CabSteel", (0.48, 0.53, 0.60, 1), metallic=0.45, roughness=0.38)
    m_drawer = make_mat("DrawerFront", (0.58, 0.63, 0.70, 1), metallic=0.4, roughness=0.35)
    m_chrome = make_mat("ChromeHandle", (0.88, 0.92, 0.96, 1), metallic=0.85, roughness=0.2)
    m_label = make_mat("LabelCard", (0.96, 0.95, 0.88, 1), roughness=0.5)

    add_box("CabBody", (0, 0.925, 0), (0.88, 1.85, 1.12), m_steel, bevel=0.02)
    for d in range(4):
        dy = 0.26 + d * 0.43
        add_box(f"Drawer_{d}", (-0.445, dy, 0), (0.04, 0.38, 1.02), m_drawer, bevel=0.008)
        add_box(f"LabelFrame_{d}", (-0.47, dy + 0.07, 0), (0.015, 0.06, 0.22), m_chrome)
        add_box(f"LabelCard_{d}", (-0.475, dy + 0.07, 0), (0.012, 0.042, 0.19), m_label)
        add_box(f"Handle_{d}", (-0.48, dy - 0.04, 0), (0.025, 0.028, 0.28), m_chrome)

    finalize_and_export("prop_filing_cabinet.glb")

# ==============================================================================
# 4. PROP: ROLLING OFFICE WHITEBOARD (prop_whiteboard.glb)
# ==============================================================================
def build_whiteboard():
    clear_scene()
    m_alu = make_mat("AluFrame", (0.76, 0.80, 0.85, 1), metallic=0.7, roughness=0.25)
    m_dark = make_mat("StandSteel", (0.22, 0.25, 0.30, 1), metallic=0.5, roughness=0.35)
    m_board = make_mat("BoardWhite", (0.96, 0.97, 0.99, 1), roughness=0.2)
    m_red = make_mat("DiagramRed", (0.92, 0.22, 0.35, 1), roughness=0.3)
    m_blue = make_mat("DiagramBlue", (0.16, 0.56, 0.96, 1), roughness=0.3)
    m_green = make_mat("DiagramGreen", (0.18, 0.78, 0.48, 1), roughness=0.3)
    m_note = make_mat("StickyNote", (0.98, 0.86, 0.24, 1), roughness=0.4)

    for sx in [-0.95, 0.95]:
        add_box(f"Post_{sx}", (sx, 0.95, 0), (0.045, 1.85, 0.045), m_dark)
        add_box(f"BaseBar_{sx}", (sx, 0.06, 0), (0.05, 0.045, 0.62), m_dark)
        for sz in [-0.28, 0.28]:
            add_rod(f"Wheel_{sx}_{sz}", (sx-0.02, 0.035, sz), (sx+0.02, 0.035, sz), 0.035, 0.035, m_alu, 10)
    add_box("CrossBar", (0, 0.38, 0), (1.90, 0.04, 0.04), m_dark)
    add_box("FrameTop", (0, 1.88, 0), (1.96, 0.045, 0.055), m_alu)
    add_box("FrameBot", (0, 0.82, 0), (1.96, 0.045, 0.055), m_alu)
    add_box("BoardCore", (0, 1.35, 0), (1.88, 1.02, 0.03), m_board)
    add_box("MarkerTray", (0, 0.80, 0.055), (1.50, 0.025, 0.09), m_alu)

    for side in [-1, 1]:
        z = side * 0.02
        add_box(f"Flow1_{side}", (-0.52, 1.52, z), (0.42, 0.22, 0.01), m_blue)
        add_box(f"Flow2_{side}", (0.08, 1.52, z), (0.42, 0.22, 0.01), m_red)
        add_box(f"Flow3_{side}", (0.58, 1.22, z), (0.44, 0.24, 0.01), m_green)
        add_box(f"Arrow1_{side}", (-0.22, 1.52, z), (0.18, 0.025, 0.012), m_dark)
        add_box(f"Arrow2_{side}", (0.32, 1.36, z), (0.24, 0.025, 0.012), m_dark)
        for n in range(3):
            add_box(f"Sticky_{side}_{n}", (-0.62 + n*0.18, 1.14, z), (0.13, 0.13, 0.012), m_note)

    finalize_and_export("prop_whiteboard.glb")

# ==============================================================================
# 5. PROP: POTTED FICUS TREE (prop_ficus_tree.glb)
# ==============================================================================
def build_ficus_tree():
    import runpy, shutil
    ficus_script = ASSETS / "external_cc0/office/build_cc0_ficus.py"
    runpy.run_path(str(ficus_script), run_name="__main__")
    src = ASSETS / "external_cc0/office/FicusPlant_CC0.glb"
    dst = Path(OUT_MODELS) / "prop_ficus_tree.glb"
    shutil.copy2(src, dst)
    print("Exported:", dst, os.path.getsize(dst))

# ==============================================================================
# 6. PROP: OFFICE TRASH BIN (prop_trash_bin.glb)
# ==============================================================================
def build_trash_bin():
    clear_scene()
    m_mesh = make_mat("BinSteel", (0.26, 0.30, 0.36, 1), metallic=0.6, roughness=0.35)
    m_rim = make_mat("BinRim", (0.42, 0.47, 0.54, 1), metallic=0.7, roughness=0.3)
    m_paper1 = make_mat("PaperWhite", (0.94, 0.93, 0.88, 1), roughness=0.6)
    m_paper2 = make_mat("PaperBlue", (0.72, 0.86, 0.96, 1), roughness=0.6)

    add_rod("BinWall", (0, 0.0, 0), (0, 0.46, 0), 0.14, 0.19, m_mesh, 14)
    add_vcyl("BinRim", (0, 0.46, 0), 0.198, 0.025, m_rim, vertices=14)
    for i, (px, py, pz, r) in enumerate([
        (-0.05, 0.38, 0.03, 0.065),
        (0.06, 0.41, -0.04, 0.062),
        (0.0, 0.44, 0.05, 0.058),
        (-0.04, 0.45, -0.05, 0.055),
    ]):
        add_sphere(f"Paper_{i}", (px, py, pz), r, m_paper1 if i%2==0 else m_paper2, segments=6, ring_count=5)

    finalize_and_export("prop_trash_bin.glb")

# ==============================================================================
# 7. PROP: SERVER RACK 42U (prop_server_rack.glb)
# ==============================================================================
def build_server_rack():
    clear_scene()
    m_frame = make_mat("RackFrame", (0.12, 0.14, 0.18, 1), metallic=0.55, roughness=0.35)
    m_blade = make_mat("RackBlade", (0.20, 0.24, 0.30, 1), metallic=0.6, roughness=0.3)
    m_led_g = make_mat("LedGreen", (0.15, 0.96, 0.45, 1), roughness=0.1)
    m_led_c = make_mat("LedCyan", (0.12, 0.82, 0.98, 1), roughness=0.1)
    m_led_a = make_mat("LedAmber", (0.98, 0.68, 0.15, 1), roughness=0.1)

    add_box("Cabinet", (0, 1.15, 0), (0.94, 2.30, 0.86), m_frame, bevel=0.02)
    for face in [-1, 1]:
        fz = face * 0.425
        for u in range(8):
            uy = 0.28 + u * 0.24
            add_box(f"Blade_{face}_{u}", (0, uy, fz), (0.82, 0.18, 0.03), m_blade, bevel=0.005)
            led_mat = m_led_g if u % 3 == 0 else (m_led_c if u % 3 == 1 else m_led_a)
            add_box(f"LedStrip_{face}_{u}", (-0.24, uy, face * 0.442), (0.24, 0.03, 0.012), led_mat)
            add_box(f"HandleL_{face}_{u}", (-0.39, uy, face * 0.445), (0.02, 0.12, 0.025), m_led_c)
            add_box(f"HandleR_{face}_{u}", (0.39, uy, face * 0.445), (0.02, 0.12, 0.025), m_led_c)

    finalize_and_export("prop_server_rack.glb")

# ==============================================================================
# 8. PROP: WHITE SHARK CENTRAL HR RECEPTION DESK (prop_hr_reception_desk.glb)
# ==============================================================================
def build_hr_reception_desk():
    clear_scene()
    marble = make_mat("Reception_ObsidianMarble", (.055,.065,.085,1), metallic=.18, roughness=.23)
    plum = make_mat("Reception_VelvetPlum", (.19,.055,.23,1), roughness=.88)
    brass = make_mat("Reception_BrushedBrass", (.65,.43,.17,1), metallic=.85, roughness=.24)
    black = make_mat("Terminal_Graphite", (.018,.023,.031,1), metallic=.55, roughness=.3)
    screen = make_mat("Terminal_Display", (.035,.20,.24,1), roughness=.32)
    vein = make_mat("Marble_MineralVein", (.22,.24,.27,1), metallic=.2, roughness=.3)

    # Continuous elliptical shell, open at the rear for staff access.
    # Four closed surfaces per segment, shared vertices; no separate box wings.
    def arc(name, rx, rz, thickness, low, high, material):
        vertices=[]; faces=[]; n=64
        for i in range(n+1):
            t=math.pi*i/n
            for rxx,rzz,y in [(rx,rz,low),(rx,rz,high),(rx-thickness,rz-thickness,low),(rx-thickness,rz-thickness,high)]:
                vertices.append(rl_pos((rxx*math.cos(t),y,.3-rzz*math.sin(t))))
        for i in range(n):
            k=i*4; j=k+4
            faces.extend([(k,j,j+1,k+1),(k+2,k+3,j+3,j+2),(k+1,j+1,j+3,k+3),(k,k+2,j+2,j)])
        faces.extend([(0,1,3,2),(n*4,n*4+2,n*4+3,n*4+1)])
        mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces); mesh.update()
        obj=bpy.data.objects.new(name,mesh); bpy.context.collection.objects.link(obj); mesh.materials.append(material)
        for f in mesh.polygons: f.use_smooth=True
        return obj
    arc("Continuous_Marble_U_Shell",2.48,1.72,.28,.12,.84,marble)
    arc("Continuous_Velvet_Inlay",2.491,1.731,.012,.24,.70,plum)
    arc("Floating_Marble_Counter",2.57,1.81,.53,.84,.92,marble)
    for y in [.18,.23,.71,.825]: arc("Continuous_Brass_Reveal",2.50,1.74,.018,y,y+.018,brass)
    arc("Recessed_Dark_Plinth",2.36,1.60,.22,.035,.13,black)
    # Fine radial brass seams follow the real curved front.
    for i in range(1,24):
        t=math.pi*i/24
        add_rod("Inlay_Brass_Seam",(2.50*math.cos(t),.25,.3-1.74*math.sin(t)),(2.50*math.cos(t),.69,.3-1.74*math.sin(t)),.006,.006,brass,vertices=6)
    for side in [-1,1]:
        x=side*2.12
        add_box("Terminal_Foot",(x,.94,-.22),(.32,.025,.22),black,bevel=.012)
        add_rod("Inclined_Metal_Stand",(x,.95,-.22),(x,1.14,-.30),.025,.021,brass)
        add_box("Thin_Terminal",(x,1.29,-.31),(.52,.32,.028),black,rot=(-.15,0,0),bevel=.012)
        add_box("Terminal_Screen",(x,1.29,-.328),(.47,.27,.006),screen,rot=(-.15,0,0),bevel=.004)
        for row in range(4):
            add_box("Terminal_UI_Row",(x,1.22+row*.038,-.351),(.31-row*.04,.007,.003),brass)
    finalize_and_export("prop_hr_reception_desk.glb")

# ==============================================================================
# 9. PROP: ESCAPED AI AGENT BOSS (escaped_ai_boss.glb)
# ==============================================================================
# escaped_ai_boss.glb is owned exclusively by generate_anatomy.py (MPFB).

# ==============================================================================
# 10. WEAPON 1: FATHER'S LEATHER BELT (weapon_belt_90s.glb)
# ==============================================================================
def build_weapon_belt():
    clear_scene()
    m_leather      = make_mat("RichLeather", (0.28, 0.13, 0.055, 1), roughness=0.38)
    m_leather_dark = make_mat("DarkLeatherEdge", (0.15, 0.065, 0.025, 1), roughness=0.46)
    m_stitch       = make_mat("SaddleStitch", (0.86, 0.68, 0.38, 1), roughness=0.40)
    m_gold         = make_mat("HeavyBrass", (0.98, 0.78, 0.22, 1), metallic=0.90, roughness=0.16)
    m_brass_recess = make_mat("AgedBrassRecess", (0.52, 0.36, 0.10, 1), metallic=0.82, roughness=0.32)
    m_prong        = make_mat("SteelProng", (0.88, 0.91, 0.96, 1), metallic=0.92, roughness=0.14)

    # Helper to build a thick beveled leather strap along a 3D path with variable normal/width frames
    def build_strap_mesh(name, path_pts, width_vecs, norm_vecs, half_w=0.021, half_t=0.0032, mat_main=m_leather):
        verts = []
        faces = []
        profile = [
            (-half_w,        -half_t * 0.75),
            (-half_w * 0.88, -half_t),
            ( half_w * 0.88, -half_t),
            ( half_w,        -half_t * 0.75),
            ( half_w,         half_t * 0.75),
            ( half_w * 0.88,  half_t),
            (-half_w * 0.88,  half_t),
            (-half_w,         half_t * 0.75),
        ]
        n_prof = len(profile)
        for p, wv, nv in zip(path_pts, width_vecs, norm_vecs):
            for pw, pt in profile:
                verts.append(rl_pos(p + wv * pw + nv * pt))
        for i in range(len(path_pts) - 1):
            base0 = i * n_prof
            base1 = (i + 1) * n_prof
            for j in range(n_prof):
                jn = (j + 1) % n_prof
                faces.append((base0 + j, base0 + jn, base1 + jn, base1 + j))
        # End caps
        faces.append(tuple(reversed(range(n_prof))))
        last = (len(path_pts) - 1) * n_prof
        faces.append(tuple(range(last, last + n_prof)))
        data = bpy.data.meshes.new(name)
        data.from_pydata(verts, [], faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(mat_main)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        return obj

    # --------------------------------------------------------------------------
    # 1) EXACT FINGER-FITTED UPPER STRAP FRAME (0.0mm FINGER CLIPPING!)
    # Matches the finger flexion envelope of fps_hands_belt:
    # Right hand fingers occupy x in [-0.035, +0.038]; Left hand fingers occupy x in [-0.438, -0.365].
    # Outside x > +0.044 (right) and x < -0.446 (left), and below -width*0.022, is 100% finger-free!
    # --------------------------------------------------------------------------
    width_dir  = Vector((0,  math.cos(math.radians(40)), -math.sin(math.radians(40)))).normalized()
    normal_dir = Vector((0, -math.sin(math.radians(40)), -math.cos(math.radians(40)))).normalized()
    along      = Vector((-1, 0, 0))  # From right hand (+x) toward left hand (-x)

    def strap_curve(t):
        return Vector((0.09 - 0.58 * t, -0.032 * 4 * t * (1 - t), 0.0))

    start_t = (0.09 - 0.046) / 0.58
    end_t   = (0.09 + 0.448) / 0.58

    def upper_strap_pos(u):
        t = start_t + (end_t - start_t) * u
        return strap_curve(t)

    def lower_strap_pos(u):
        t = start_t + (end_t - start_t) * u
        sag = math.sin(u * math.pi)
        # Arch below (-width_dir) and slightly behind (-normal_dir) the upper strap,
        # staying completely clear of both hands' fingers!
        return strap_curve(t) - width_dir * (0.026 + 0.030 * sag) - normal_dir * (0.015 + 0.010 * sag)

    n_seg = 64
    u_pts = [upper_strap_pos(i / n_seg) for i in range(n_seg + 1)]
    l_pts = [lower_strap_pos(i / n_seg) for i in range(n_seg + 1)]
    u_w = [width_dir for _ in u_pts]
    u_n = [normal_dir for _ in u_pts]
    l_w = [width_dir for _ in l_pts]
    l_n = [-normal_dir for _ in l_pts]

    build_strap_mesh("UpperCombatStrap", u_pts, u_w, u_n, half_w=0.020, half_t=0.0028, mat_main=m_leather)
    build_strap_mesh("LowerReturnStrap", l_pts, l_w, l_n, half_w=0.0195, half_t=0.0028, mat_main=m_leather_dark)

    # --------------------------------------------------------------------------
    # 2) TWO TIGHT LEATHER COILS WRAPPED AROUND THE RIGHT FIST / WRIST CUFF
    # Positioned at x = +0.052..+0.080 (outside the right index/middle/ring/pinky fingers!)
    # and looping down around the lower right fist into the lower return strap.
    # --------------------------------------------------------------------------
    r_anchor = strap_curve(start_t) - width_dir * 0.014 - normal_dir * 0.012 + Vector((0.018, 0, 0))
    c_rad1 = width_dir
    c_rad2 = normal_dir
    c_axis = Vector((1, -0.18, 0.12)).normalized()

    for coil_idx, ax_off in enumerate([0.000, 0.018]):
        c_pts, c_wvecs, c_nvecs = [], [], []
        steps = 40
        for i in range(steps + 1):
            ang = -math.pi * 0.35 + (math.tau * 0.92) * (i / steps)
            hel = ax_off + (i / steps - 0.5) * 0.008
            radial = c_rad1 * (0.022 * math.cos(ang)) + c_rad2 * (0.018 * math.sin(ang))
            pt = r_anchor + c_axis * hel + radial
            c_pts.append(pt)
            c_wvecs.append(c_axis)
            c_nvecs.append(radial.normalized())
        build_strap_mesh(f"RightFistWrap_{coil_idx}", c_pts, c_wvecs, c_nvecs, half_w=0.014, half_t=0.0030, mat_main=m_leather)
        for i in range(0, steps, 2):
            p0, p1 = c_pts[i], c_pts[min(steps, i + 1)]
            n0 = c_nvecs[i]
            for s_edge in (-1, 1):
                off = c_axis * (s_edge * 0.0105) + n0 * 0.0033
                add_rod(f"WrapStitch_{coil_idx}_{i}_{s_edge}", p0 + off, p1 + off, 0.00065, 0.00065, m_stitch, 6)

    # Smooth U-fold connecting UpperCombatStrap and LowerReturnStrap at the right end (outside fingers)
    r_fold_pts, r_fold_w, r_fold_n = [], [], []
    for i in range(17):
        theta = (i / 16.0) * math.pi
        c_mid = 0.5 * (u_pts[0] + l_pts[0])
        rad_v = 0.5 * (u_pts[0] - l_pts[0])
        pt = c_mid + rad_v * math.cos(theta) - along * (0.014 * math.sin(theta))
        n_v = (rad_v.normalized() * math.cos(theta) - along * math.sin(theta)).normalized()
        r_fold_pts.append(pt)
        r_fold_w.append(width_dir)
        r_fold_n.append(n_v)
    build_strap_mesh("RightEndFold", r_fold_pts, r_fold_w, r_fold_n, half_w=0.0195, half_t=0.0028, mat_main=m_leather)

    # Continuous U-fold bight looping around the outside of the left hand at u_pts[-1] -> l_pts[-1]
    bight_pts, bight_w, bight_n = [], [], []
    for i in range(21):
        theta = (i / 20.0) * math.pi
        c_mid = 0.5 * (u_pts[-1] + l_pts[-1])
        rad_v = 0.5 * (u_pts[-1] - l_pts[-1])
        pt = c_mid + rad_v * math.cos(theta) + along * (0.022 * math.sin(theta))
        n_v = (rad_v.normalized() * math.cos(theta) + along * math.sin(theta)).normalized()
        bight_pts.append(pt)
        bight_w.append(width_dir)
        bight_n.append(n_v)
    build_strap_mesh("LeftHandLoopBight", bight_pts, bight_w, bight_n, half_w=0.0198, half_t=0.0028, mat_main=m_leather)

    # Tapered leather tip emerging cleanly past the left fold (x < -0.450, 100% outside left fingers!)
    tail_pts, tail_w, tail_n = [], [], []
    for i in range(15):
        kt = i / 14.0
        pt = u_pts[-1] + along * (0.004 + kt * 0.036) - width_dir * (0.008 * kt * kt) - normal_dir * (0.003 * kt)
        tail_pts.append(pt)
        taper = 1.0 - 0.55 * (kt * kt)
        tail_w.append(width_dir * taper)
        tail_n.append(normal_dir)
    build_strap_mesh("WhipTailEnd", tail_pts, tail_w, tail_n, half_w=0.0195, half_t=0.0027, mat_main=m_leather)

    # Contrasting saddle stitching along the upper strap & burnished edge rails
    for i in range(0, n_seg, 2):
        p0 = u_pts[i]
        p1 = u_pts[min(n_seg, i + 1)]
        for edge in (-1, 1):
            edge_pos0 = p0 + width_dir * (edge * 0.0202)
            edge_pos1 = p1 + width_dir * (edge * 0.0202)
            add_rod(f"BurnishedEdge_{i}_{edge}", edge_pos0, edge_pos1, 0.0012, 0.0012, m_leather_dark, 6)
            for face in (-1, 1):
                st_off = width_dir * (edge * 0.0162) + normal_dir * (face * 0.0031)
                add_rod(f"SaddleStitch_{i}_{edge}_{face}", p0 + st_off, p1 + st_off, 0.0007, 0.0007, m_stitch, 6)

    # Hollow brass eyelets along the upper strap
    for idx, t_hole in enumerate([0.20, 0.32, 0.44, 0.56, 0.68, 0.80]):
        center = upper_strap_pos(t_hole)
        for face in (-1, 1):
            ring_pts = [
                center + normal_dir * (face * 0.0032)
                + along * (0.0038 * math.cos(k * math.tau / 18))
                + width_dir * (0.0038 * math.sin(k * math.tau / 18))
                for k in range(18)
            ]
            for k in range(18):
                add_rod(f"Eyelet_{idx}_{face}_{k}", ring_pts[k], ring_pts[(k + 1) % 18], 0.0010, 0.0010, m_gold, 8)
        add_rod(f"EyeletHole_{idx}", center - normal_dir * 0.0030, center + normal_dir * 0.0030, 0.0026, 0.0026, m_leather_dark, 12)

    # --------------------------------------------------------------------------
    # 3) MASSIVE SCULPTED 90s BRASS BUCKLE WITH CHAMFERS, ENGRAVING & STEEL PRONG
    # --------------------------------------------------------------------------
    b_center = upper_strap_pos(0.56) + normal_dir * 0.0072
    bx_half = 0.030
    by_half = 0.024

    # Outer forged chamfered brass frame
    frame_corners = []
    for cx, cy, start_deg in ((bx_half, by_half, 0), (-bx_half, by_half, 90), (-bx_half, -by_half, 180), (bx_half, -by_half, 270)):
        for k in range(6):
            a = math.radians(start_deg + k * 18)
            frame_corners.append(b_center + along * (cx + 0.0065 * math.cos(a)) + width_dir * (cy + 0.0065 * math.sin(a)))
    for k in range(len(frame_corners)):
        p_a = frame_corners[k]
        p_b = frame_corners[(k + 1) % len(frame_corners)]
        add_rod(f"BuckleOuterFrame_{k}", p_a, p_b, 0.0048, 0.0048, m_gold, 14)
        p_ia = b_center + (p_a - b_center) * 0.84 - normal_dir * 0.0010
        p_ib = b_center + (p_b - b_center) * 0.84 - normal_dir * 0.0010
        add_rod(f"BuckleInnerBevel_{k}", p_ia, p_ib, 0.0022, 0.0022, m_brass_recess, 10)

    # Sculpted 90s decorative crest plate on the rear half of the buckle + engraved relief bars
    plate_center = b_center - along * 0.011 + normal_dir * 0.0018
    for rib_k, wy in enumerate([-0.015, -0.008, 0.0, 0.008, 0.015]):
        rib_p0 = plate_center - along * 0.015 + width_dir * wy
        rib_p1 = plate_center + along * 0.011 + width_dir * wy
        mat_rib = m_gold if rib_k % 2 == 0 else m_brass_recess
        rad_rib = 0.0024 if rib_k % 2 == 0 else 0.0018
        add_rod(f"BuckleReliefRib_{rib_k}", rib_p0, rib_p1, rad_rib, rad_rib, mat_rib, 10)

    # Raised 5-pointed star / emblem relief in the center of the brass buckle plate
    star_c = plate_center + normal_dir * 0.0022
    for ray in range(5):
        ang = ray * math.tau / 5.0
        tip_p = star_c + along * (0.010 * math.cos(ang)) + width_dir * (0.010 * math.sin(ang))
        add_rod(f"BuckleStarRay_{ray}", star_c, tip_p, 0.0026, 0.0009, m_gold, 8)

    # Corner bolster studs on the brass frame
    for sx in (-1, 1):
        for sy in (-1, 1):
            stud_p = b_center + along * (sx * 0.029) + width_dir * (sy * 0.022) + normal_dir * 0.0030
            add_rod(f"BuckleCornerStud_{sx}_{sy}", stud_p - normal_dir * 0.0025, stud_p + normal_dir * 0.0025, 0.0035, 0.0028, m_gold, 10)

    # Front knurled brass roller bar & rear hinge bar
    roller_c = b_center + along * (bx_half + 0.0055)
    add_rod("BuckleFrontRoller", roller_c - width_dir * 0.022, roller_c + width_dir * 0.022, 0.0058, 0.0058, m_gold, 16)
    hinge_c = b_center - along * (bx_half - 0.004)
    add_rod("BuckleHingeAxle", hinge_c - width_dir * 0.024, hinge_c + width_dir * 0.024, 0.0034, 0.0034, m_brass_recess, 14)

    # Heavy tapered steel buckle prong (стальной язычок)
    prong_base = hinge_c + normal_dir * 0.0028
    prong_mid  = b_center + normal_dir * 0.0064
    prong_tip  = roller_c + along * 0.004 + normal_dir * 0.0050
    add_rod("SteelProngRear", prong_base, prong_mid, 0.0032, 0.0026, m_prong, 12)
    add_rod("SteelProngTip",  prong_mid,  prong_tip, 0.0026, 0.0013, m_prong, 12)

    # Leather keeper loop (тренчик) wrapping both upper & lower straps at t=0.42 + flush brass rivets on the curved strap
    ku_c = upper_strap_pos(0.42)
    kl_c = lower_strap_pos(0.42)
    k_pts = [
        ku_c + width_dir * 0.0225 + normal_dir * 0.0042,
        ku_c - width_dir * 0.0225 + normal_dir * 0.0042,
        kl_c - width_dir * 0.0215 - normal_dir * 0.0042,
        kl_c + width_dir * 0.0215 - normal_dir * 0.0042,
    ]
    for k in range(4):
        add_rod(f"LeatherKeeper_{k}", k_pts[k], k_pts[(k + 1) % 4], 0.0036, 0.0036, m_leather_dark, 10)
    for t_riv in (0.37, 0.46):
        rv_p = upper_strap_pos(t_riv)
        add_rod(f"BrassRivet_{t_riv}", rv_p - normal_dir * 0.0030, rv_p + normal_dir * 0.0038, 0.0032, 0.0032, m_gold, 12)

    finalize_and_export("weapon_belt_90s.glb", preserve_smoothing=True)

# ==============================================================================
# 11. WEAPON 2: 90s SOLDERING IRON WITH BLUE TAPE (weapon_solder_90s.glb)
# ==============================================================================
def build_reception_chair():
    clear_scene()
    bpy.ops.import_scene.gltf(filepath=str(CHAIR_SOURCE / "SheenChair.glb"))
    velvet = make_mat("Reception petrol velvet", (.025,.16,.16,1), roughness=.83)
    for obj in list(bpy.context.scene.objects):
        if obj.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = obj
        obj.data.gltf2_variant_default_materials.clear()
        obj.data.gltf2_variant_mesh_data.clear()
        for slot in obj.material_slots:
            if slot.material and slot.material.name.startswith("fabric "):
                slot.material = velvet
        # Keep the imported silhouette and upholstery UVs at the lobby viewing distance.
        if len(obj.data.polygons) > 3000:
            modifier = obj.modifiers.new("Lobby detail", "DECIMATE")
            modifier.ratio = .42
            bpy.ops.object.modifier_apply(modifier=modifier.name)
    finalize_and_export("prop_reception_chair.glb", preserve_smoothing=True)


def build_weapon_solder():
    clear_scene()
    wood = make_mat("Turned hardwood", (.47,.21,.075,1), roughness=.49)
    nodes, links = wood.node_tree.nodes, wood.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    grain = nodes.new("ShaderNodeTexImage")
    grain.image = bpy.data.images.load(str(CHAIR_SOURCE / "chair_wood_albedo.png"))
    # Stain the source grayscale grain in linear light, then embed the RGB albedo.
    # The runtime samples the texture directly, so bake the stain into its pixels.
    import numpy as np
    source = grain.image
    pixels = np.empty(len(source.pixels), dtype=np.float32)
    source.pixels.foreach_get(pixels)
    pixels.reshape(-1,4)[:,:3] *= (.39,.16,.048)
    stained = bpy.data.images.new("Walnut stained grain", *source.size)
    stained.pixels.foreach_set(pixels)
    stained.pack()
    grain.image = stained
    links.new(grain.outputs["Color"], bsdf.inputs["Base Color"])
    packed = nodes.new("ShaderNodeTexImage")
    packed.image = bpy.data.images.load(str(CHAIR_SOURCE / "chair_woodbrown_roughnessmetallic.png"))
    packed.image.colorspace_settings.name = "Non-Color"
    channels = nodes.new("ShaderNodeSeparateColor")
    links.new(packed.outputs["Color"], channels.inputs[0])
    links.new(channels.outputs["Green"], bsdf.inputs["Roughness"])
    tape = make_mat("Cobalt PVC tape", (.022,.12,.43,1), roughness=.38)
    edge = make_mat("Tape lap", (.015,.077,.29,1), roughness=.40)
    black = make_mat("Black phenolic", (.035,.027,.029,1), roughness=.40)
    steel = make_mat("Brushed nickel steel", (.38,.43,.48,1), metallic=.76, roughness=.35)
    shadow = make_mat("Heat-darkened steel", (.074,.086,.105,1), metallic=.72, roughness=.46)
    brass = make_mat("Clamp brass", (.54,.32,.105,1), metallic=.75, roughness=.36)
    hot = make_mat("Incandescent nichrome", (.98,.36,.08,1), metallic=.12, roughness=.45)
    heater_bsdf = hot.node_tree.nodes.get("Principled BSDF")
    heater_bsdf.inputs["Emission Color"].default_value = (.98,.19,.025,1)
    heater_bsdf.inputs["Emission Strength"].default_value = .7
    copper_root = make_mat("Oxidised copper", (.27,.076,.039,1), metallic=.68, roughness=.48)
    oxide = make_mat("Violet temper oxide", (.20,.14,.29,1), metallic=.70, roughness=.38)
    copper = make_mat("Forged copper", (.82,.28,.105,1), metallic=.73, roughness=.36)
    tin = make_mat("POS-61", (.84,.88,.90,1), metallic=.83, roughness=.22)

    # A continuous turned profile. Each row carries the finish of its forward span.
    rows = [(-.099,.004,black),(-.089,.006,black),(-.084,.009,wood),
            (-.080,.0128,wood),(-.074,.0132,wood),(-.068,.0125,wood),(-.051,.0126,tape)]
    for i in range(8):
        z = -.050 + i*.011
        rows.extend([(z,.0131,tape),(z+.0093,.0132,edge),(z+.0105,.0129,tape)])
    rows += [(.039,.0126,wood),(.042,.0126,black),(.0445,.0135,black)]
    for z, radius in ((.0455,.0163),(.0515,.0182),(.0575,.0195)):
        rows.extend([(z,radius-.0009,black),(z+.0007,radius,black),
                     (z+.0023,radius,black),(z+.0034,.0134,black)])
    rows.extend([(.0613,.0132,steel),(.0622,.0164,steel),
                 (.0634,.0164,steel),(.064,.0100,shadow)])
    sides = 40
    vertices, faces = [], []
    for z,r,material in rows:
        for i in range(sides):
            angle = math.tau*i/sides
            lap = .0018*math.sin(angle) if -.051 <= z <= .038 else 0
            vertices.append(rl_pos((r*math.cos(angle), r*math.sin(angle), z+lap)))
    for j in range(len(rows)-1):
        for i in range(sides):
            n=(i+1)%sides
            faces.append((j*sides+i,j*sides+n,(j+1)*sides+n,(j+1)*sides+i))
    faces += [tuple(reversed(range(sides))),tuple(range(len(vertices)-sides,len(vertices)))]
    mesh = bpy.data.meshes.new("Turned EPSN profile")
    mesh.from_pydata(vertices,[],faces)
    obj = bpy.data.objects.new("EPSN-40",mesh)
    bpy.context.collection.objects.link(obj)
    materials = [wood,tape,edge,black,steel,shadow]
    for material in materials:
        mesh.materials.append(material)
    uv = mesh.uv_layers.new(name="Turned grain")
    for face in mesh.polygons:
        row, side = divmod(face.index,sides)
        if row >= len(rows)-1:
            face.material_index = materials.index(black if face.index==len(faces)-2 else shadow)
            continue
        face.material_index = materials.index(rows[row][2])
        face.use_smooth = True
        for loop,u,v in zip(face.loop_indices,(side,side+1,side+1,side),(row,row,row+1,row+1)):
            uv.data[loop].uv = ((rows[v][0]+.10)*4, u/sides)
    def axial(name, z0, z1, r0, r1, material):
        return add_rod(name, (0,0,z0), (0,0,z1), r0, r1, material, 48)

    # The punched jacket is an actual hollow wall. Each cell connects a curved
    # patch boundary to a rounded aperture, then down to the dark inner lip.
    axial("Insulated heater mandrel", .064, .168, .0063, .0063, hot)
    for index in range(24):
        z = .066 + .00415 * index
        axial("Recessed coil groove", z, z+.00125, .00645, .00645, copper_root)
    jacket_vertices, jacket_faces, jacket_materials = [], [], []
    def jacket_band(z0, z1, angular_opening, half_length):
        cells, samples = 6, 24
        axial_center = (z0+z1)*.5
        for cell in range(cells):
            base = len(jacket_vertices)
            angle = math.tau*cell/cells
            for boundary, radius in ((True,.0091),(False,.0091),(False,.0081),(True,.0081)):
                for index in range(samples):
                    phase = math.tau*index/samples
                    x, y = math.cos(phase), math.sin(phase)
                    if boundary:
                        scale = max(abs(x),abs(y))
                        azimuth = angle + (math.pi/cells)*x/scale
                        z = axial_center + (z1-z0)*.5*y/scale
                    else:
                        # A short circular punch or a long press-formed slot.
                        azimuth = angle + angular_opening*x
                        cap = min(half_length, angular_opening*radius)
                        z = axial_center + cap*y
                        if abs(y) > 1e-6:
                            z += math.copysign(half_length-cap,y)
                    jacket_vertices.append(rl_pos((radius*math.cos(azimuth), radius*math.sin(azimuth), z)))
            for layer in range(3):
                for index in range(samples):
                    a = base + layer*samples + index
                    b = base + layer*samples + (index+1)%samples
                    jacket_faces.append((a,b,b+samples,a+samples))
                    jacket_materials.append(0 if layer==0 else 1)

    for z0,z1,opening,length in ((.064,.079,.24,.0023),(.079,.107,.34,.0115),
                                  (.107,.123,.24,.0023),(.123,.151,.34,.0115),
                                  (.151,.168,.24,.0023)):
        jacket_band(z0,z1,opening,length)
    jacket = bpy.data.meshes.new("Pierced heater jacket")
    jacket.from_pydata(jacket_vertices,[],jacket_faces)
    jacket.materials.append(steel)
    jacket.materials.append(shadow)
    for face,material_index in zip(jacket.polygons,jacket_materials):
        face.material_index = material_index
    bpy.context.collection.objects.link(bpy.data.objects.new("Punch-formed vents",jacket))
    for z in (.065,.078,.106,.122,.150,.164):
        axial("Rolled stiffener",z,z+.0008,.0089,.0096,steel)
        axial("Rolled stiffener crown",z+.0008,z+.0020,.0096,.0096,steel)
        axial("Rolled stiffener return",z+.0020,z+.0028,.0096,.0089,shadow)

    axial("Chuck shoulder",.165,.169,.0091,.0108,shadow)
    axial("Tool steel chuck",.169,.1815,.0108,.0108,steel)
    axial("Chuck nose",.1815,.184,.0108,.0078,shadow)
    for sign in (-1,1):
        add_rod("Screw shank", (sign*.0095,0,.175),(sign*.013,0,.175),.0026,.0026,shadow,16)
        add_rod("Brass thumb nut", (sign*.0125,0,.175),(sign*.015,0,.175),.004,.0036,brass,8)
        add_box("Thumb screw wings", (sign*.015,0,.175),(.0023,.012,.0030),
                brass, rot=(sign*.24,0,0),bevel=.0007)

    # The eight-sided forging fans out to a 14 mm chisel, with a thin tinned lip.
    stations = [(.182,.0059,.005,0,copper_root),(.192,.0056,.0048,0,oxide),
                (.196,.0055,.0047,0,copper),(.211,.0061,.0040,0,copper),
                (.225,.0069,.0023,.0010,tin),(.233,.007,.0013,.0014,tin),
                (.238,.0068,.00042,.0016,tin)]
    section = ((-.72,-1),(.72,-1),(1,-.57),(1,.57),(.72,1),(-.72,1),(-1,.57),(-1,-.57))
    tip_vertices, tip_faces = [], []
    for z,width,thickness,lift,material in stations:
        for x,y in section:
            tip_vertices.append(rl_pos((width*x, lift+thickness*y, z)))
    for row in range(len(stations)-1):
        for side in range(8):
            a = row*8+side
            b = row*8+(side+1)%8
            tip_faces.append((a,b,b+8,a+8))
    tip_faces.extend((tuple(reversed(range(8))),tuple(range(len(tip_vertices)-8,len(tip_vertices)))))
    tip_mesh = bpy.data.meshes.new("Forged screwdriver chisel")
    tip_mesh.from_pydata(tip_vertices,[],tip_faces)
    finishes = [copper_root,oxide,copper,tin]
    for material in finishes:
        tip_mesh.materials.append(material)
    for face in tip_mesh.polygons:
        finish = stations[min(face.index//8,len(stations)-1)][4]
        face.material_index = finishes.index(finish)
    bpy.context.collection.objects.link(bpy.data.objects.new("Tinned copper chisel",tip_mesh))
    finalize_and_export("weapon_solder_90s.glb", preserve_smoothing=True)

# ==============================================================================
# 12. WEAPON 4: SAWED-OFF DOUBLE-BARREL SHOTGUN (weapon_shotgun_90s.glb)
# ==============================================================================
def build_weapon_shotgun():
    clear_scene()
    m_walnut       = make_mat("DarkWalnut", (0.16, 0.072, 0.034, 1), roughness=0.36)
    m_walnut_check = make_mat("CheckeredWalnut", (0.095, 0.042, 0.018, 1), roughness=0.58)
    m_receiver     = make_mat("ReceiverSteel", (.065, .082, .103, 1), metallic=.89, roughness=.34)
    m_steel_dark   = make_mat("DarkSteelTrim", (.043, .055, .072, 1), metallic=.85, roughness=.39)
    m_barrel       = make_mat("BluedSteel", (.032, .045, .061, 1), metallic=.90, roughness=.29)
    m_bore         = make_mat("BoreBlack", (0.02, 0.02, 0.03, 1), roughness=0.85)
    m_brass        = make_mat("BrassBead", (0.96, 0.78, 0.22, 1), metallic=0.90, roughness=0.16)

    walnut = ASSETS / "external_cc0/walnut_veneer"
    # Bake the oil stain into the photo for the game's diffuse-only shader.
    import numpy as np
    original = bpy.data.images.load(str(walnut / "walnut_veneer_diff_1k.jpg"))
    pixels = np.empty(len(original.pixels), dtype=np.float32)
    original.pixels.foreach_get(pixels)
    pixels.reshape(-1, 4)[:, :3] *= (.35, .24, .17)
    stained = bpy.data.images.new("Oiled walnut veneer", *original.size)
    stained.pixels.foreach_set(pixels)
    stained.pack()
    rough = bpy.data.images.load(str(walnut / "walnut_veneer_rough_1k.jpg"))
    rough.colorspace_settings.name = "Non-Color"
    for material in (m_walnut, m_walnut_check):
        tree = material.node_tree
        shader = tree.nodes.get("Principled BSDF")
        for image, socket in ((stained, "Base Color"), (rough, "Roughness")):
            tex = tree.nodes.new("ShaderNodeTexImage")
            tex.image = image
            tree.links.new(tex.outputs["Color"], shader.inputs[socket])

    def smooth(obj):
        for face in obj.data.polygons:
            face.use_smooth = len(face.vertices) <= 4
        return obj

    def curve(name, points, radius, material):
        data = bpy.data.curves.new(name, 'CURVE')
        data.dimensions = '3D'
        data.bevel_depth = radius
        data.bevel_resolution = 4
        spline = data.splines.new('BEZIER')
        spline.bezier_points.add(len(points) - 1)
        for p, co in zip(spline.bezier_points, points):
            p.co = rl_pos(co)
            p.handle_left_type = p.handle_right_type = 'AUTO'
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        data.materials.append(material)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.convert(target='MESH')
        return obj

    # 1) ERGONOMIC DARK WALNUT PISTOL GRIP WITH FINGER GROOVES & METAL MEDALLION CAP
    # Rich dark walnut (never orange!), contoured oval cross-section with 3 front finger grooves
    rings = [
        # (y,     z,     rx,    rz,    front_groove_indent)
        (-0.104, 0.098, 0.025, 0.022,  0.002),  # flared pommel swell
        (-0.092, 0.090, 0.023, 0.021, -0.003),  # finger groove 3 (pinky)
        (-0.079, 0.080, 0.024, 0.023,  0.002),  # ridge 2
        (-0.064, 0.068, 0.022, 0.021, -0.0035), # finger groove 2 (ring)
        (-0.050, 0.057, 0.023, 0.023,  0.002),  # ridge 1
        (-0.036, 0.044, 0.021, 0.021, -0.0035), # finger groove 1 (middle)
        (-0.020, 0.027, 0.021, 0.024,  0.000),  # upper wrist waist
        (-0.004, 0.005, 0.024, 0.027,  0.000),  # receiver tang transition
        ( 0.008,-0.020, 0.026, 0.029,  0.000),  # action socket join
    ]
    n_circ = 28
    verts = []
    for y, z, rx, rz, fg in rings:
        for i in range(n_circ):
            ang = i * math.tau / n_circ
            # Flatten sides slightly for authentic shotgun wrist feel + front finger groove modulation
            sx = rx * math.cos(ang) * (0.92 + 0.08 * abs(math.sin(ang)))
            sz = rz * math.sin(ang)
            if math.sin(ang) < -0.35:
                sz -= fg * ((-math.sin(ang) - 0.35) / 0.65)
            verts.append(rl_pos((sx, y, z + sz)))
    faces = [
        (k * n_circ + i, k * n_circ + (i + 1) % n_circ, (k + 1) * n_circ + (i + 1) % n_circ, (k + 1) * n_circ + i)
        for k in range(len(rings) - 1) for i in range(n_circ)
    ]
    faces += [tuple(reversed(range(n_circ))), tuple(range(len(verts) - n_circ, len(verts)))]
    mesh = bpy.data.meshes.new('ErgonomicWalnutGrip')
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    grip = bpy.data.objects.new('ErgonomicWalnutGrip', mesh)
    bpy.context.collection.objects.link(grip)
    mesh.materials.append(m_walnut)
    mesh.materials.append(m_walnut_check)
    uv = mesh.uv_layers.new(name="UVMap")
    for face in mesh.polygons:
        for index in face.loop_indices:
            row, column = divmod(mesh.loops[index].vertex_index, n_circ)
            if face.index < (len(rings)-1)*n_circ and face.index % n_circ == n_circ-1 and column == 0:
                column = n_circ
            uv.data[index].uv = (.20 + .60*column/n_circ, .12 + .72*row/(len(rings)-1))
    # Assign dark checkered walnut material directly to the flush side-panel polygons of the grip
    # so the silhouette stays 100% sleek and anatomical without protruding pegs!
    for k in range(1, len(rings) - 2):
        for i in range(n_circ):
            ang = i * math.tau / n_circ
            if abs(math.cos(ang)) > 0.72 and abs(math.sin(ang)) < 0.62:
                poly_idx = k * n_circ + i
                if poly_idx < len(mesh.polygons):
                    mesh.polygons[poly_idx].material_index = 1
    smooth(grip)

    # Subtle flush brass grip escutcheon pin on the side of the dark walnut wrist
    add_rod("GripEscutcheonPin", (-0.0205, -0.056, 0.062), (0.0205, -0.056, 0.062), 0.0028, 0.0028, m_brass, 12)

    # Polished steel pommel buttcap (стальной тыльник) + golden brass medallion (латунный медальон)
    smooth(add_rod("SteelPommelCap", (0, -0.102, 0.096), (0, -0.111, 0.104), 0.0255, 0.0225, m_receiver, 24))
    smooth(add_rod("PommelBezelRing", (0, -0.103, 0.097), (0, -0.106, 0.100), 0.0265, 0.0262, m_steel_dark, 24))
    smooth(add_rod("BrassGripMedallion", (0, -0.110, 0.103), (0, -0.1135, 0.1065), 0.0125, 0.0105, m_brass, 18))

    # 2) MACHINED STEEL BOXLOCK RECEIVER WITH SIDE CHEEKS, SCREWS & EXTERNAL HAMMERS
    # Main sculpted steel receiver block
    add_box("ReceiverCoreBlock", (0, 0.011, -0.056), (0.048, 0.044, 0.082), m_receiver, bevel=0.008)
    for side in (-1, 1):
        # Hemispherical breech balls (яблоки колодки)
        smooth(add_sphere(f"BreechFence_{side}", (side * 0.0175, 0.023, -0.048), 1.0, m_receiver,
                          scale=(0.019, 0.026, 0.036), segments=24, ring_count=14))
        # Teardrop sidelock cheek plates (боковые фигурные щеки колодки)
        add_box(f"SideCheekPlate_{side}", (side * 0.0245, 0.008, -0.042), (0.004, 0.026, 0.074), m_receiver, bevel=0.003)
        add_box(f"SideCheekBorder_{side}", (side * 0.0258, 0.008, -0.042), (0.002, 0.020, 0.064), m_steel_dark, bevel=0.0015)

        # Visible slotted gunsmith screws & brass hinge pin on the side cheeks
        for s_idx, (sy, sz, s_rad, s_mat) in enumerate([
            (0.012, -0.022, 0.0038, m_steel_dark),
            (0.004, -0.052, 0.0034, m_steel_dark),
            (0.006, -0.078, 0.0052, m_brass),  # Main hinge pin rosette
        ]):
            sx_in  = side * 0.024
            sx_out = side * 0.0285
            add_rod(f"ReceiverScrew_{side}_{s_idx}", (sx_in, sy, sz), (sx_out, sy, sz), s_rad, s_rad, s_mat, 14)
            add_box(f"ScrewSlot_{side}_{s_idx}", (sx_out, sy, sz), (0.0015, s_rad * 1.6, 0.0012), m_bore)

    # Upper & lower steel tangs gripping the dark walnut wrist
    curve("UpperSteelTang", [(0, 0.034, -0.040), (0, 0.025, -0.010), (0, 0.010, 0.020), (0, -0.005, 0.044)], 0.0055, m_receiver)
    add_rod("TangScrew", (0, 0.008, 0.024), (0, 0.013, 0.027), 0.0032, 0.0032, m_steel_dark, 12)

    # Top Westley Richards locking lever
    curve("TopLockingLever", [(0, 0.042, -0.058), (0.004, 0.049, -0.040), (0.009, 0.048, -0.014), (0.014, 0.039, 0.010)], 0.0038, m_steel_dark)
    smooth(add_sphere("LeverThumbPaddle", (0.014, 0.039, 0.012), 1.0, m_receiver, scale=(0.008, 0.004, 0.012), segments=18))

    # Twin cocked external hammers (взведенные курки с насечкой)
    for side in (-1, 1):
        hx = side * 0.0275
        smooth(add_rod(f"HammerBoss_{side}", (hx - side * 0.003, 0.014, -0.036), (hx + side * 0.003, 0.014, -0.036), 0.0072, 0.0072, m_receiver, 20))
        add_rod(f"HammerPivotScrew_{side}", (hx - side * 0.004, 0.014, -0.036), (hx + side * 0.004, 0.014, -0.036), 0.0035, 0.0035, m_brass, 12)
        curve(f"ExternalHammer_{side}", [(hx, 0.014, -0.036), (hx, 0.034, -0.022), (hx, 0.056, -0.016), (hx, 0.064, -0.034)], 0.0042, m_steel_dark)
        smooth(add_sphere(f"HammerSpur_{side}", (hx, 0.060, -0.010), 1.0, m_receiver, scale=(0.0055, 0.0038, 0.011), segments=18))

    # Sculpted trigger guard & twin curved triggers
    curve("TriggerGuardBow", [(0, -0.014, -0.078), (0, -0.044, -0.072), (0, -0.052, -0.042),
                              (0, -0.043, -0.010), (0, -0.018,  0.006)], 0.0034, m_steel_dark)
    for t_idx, (tx, tz) in enumerate([(-0.0045, -0.056), (0.0045, -0.034)]):
        curve(f"ShotgunTrigger_{t_idx}", [(tx, -0.014, tz), (tx, -0.032, tz + 0.005), (tx, -0.040, tz - 0.002)], 0.0024, m_brass if t_idx == 0 else m_receiver)

    # 3) SCULPTED DARK WALNUT SPLINTER FOREND WITH CHECKERING & STEEL DEELEY LATCH
    forend = smooth(add_sphere("DarkWalnutForend", (0, -0.003, -0.205), 1.0, m_walnut,
                      scale=(0.028, 0.019, 0.110), segments=28, ring_count=16))
    for loop in forend.data.loops:
        x, y, z = forend.data.vertices[loop.vertex_index].co
        forend.data.uv_layers.active.data[loop.index].uv = (.5 + x*3.7 + z*5.1, .5 + y*4)
    for side in (-1, 1):
        # Finger grooves along the upper side of the walnut forend
        add_rod(f"ForendFingerGroove_{side}", (side * 0.023, 0.007, -0.140), (side * 0.021, 0.007, -0.270), 0.0032, 0.0028, m_walnut_check, 10)
        for f_row in range(5):
            fz = -0.155 - f_row * 0.022
            add_rod(f"ForendCheck_{side}_{f_row}", (side * 0.0255, -0.004, fz - 0.006), (side * 0.0255, 0.003, fz + 0.006), 0.0020, 0.0020, m_walnut_check, 8)
    # Bright steel Deeley forend release latch & escutcheon on the bottom of the forend
    add_box("ForendSteelLatch", (0, -0.021, -0.205), (0.010, 0.004, 0.068), m_receiver, bevel=0.0015)
    add_rod("ForendIronTip", (0, 0.002, -0.095), (0, 0.002, -0.112), 0.025, 0.026, m_receiver, 20)

    # 4) TWIN BLUED BARRELS WITH VENTILATED SIGHT RIB, HOLLOW BORES & BRASS FRONT BEAD
    for side in (-1, 1):
        bx = side * 0.0175
        smooth(add_rod(f"BluedBarrel_{side}", (bx, 0.024, -0.092), (bx, 0.024, -0.456), 0.0168, 0.0158, m_barrel, 32))
        smooth(add_rod(f"MuzzleCrown_{side}", (bx, 0.024, -0.456), (bx, 0.024, -0.466), 0.0166, 0.0164, m_receiver, 32))
        smooth(add_rod(f"HollowBore_{side}",  (bx, 0.024, -0.452), (bx, 0.024, -0.467), 0.0132, 0.0132, m_bore, 24))
        # Breech decorative reinforcing ring
        smooth(add_rod(f"BreechBand_{side}",  (bx, 0.024, -0.092), (bx, 0.024, -0.115), 0.0176, 0.0172, m_receiver, 32))

    # Central joining rib & raised ventilated top sighting rib (вентилируемая прицельная планка)
    add_box("MidBarrelFiller", (0, 0.024, -0.275), (0.012, 0.012, 0.365), m_barrel)
    add_box("VentilatedTopRib", (0, 0.0425, -0.276), (0.0095, 0.0035, 0.368), m_barrel, bevel=0.001)
    for post_i in range(9):
        pz = -0.110 - post_i * 0.042
        add_box(f"RibBridgePost_{post_i}", (0, 0.0375, pz), (0.0085, 0.007, 0.014), m_receiver if post_i in (0, 8) else m_barrel)

    # Muzzle rib block + bright golden-brass front bead sight (латунная мушка)
    add_box("FrontSightBase", (0, 0.044, -0.448), (0.0075, 0.004, 0.014), m_receiver, bevel=0.001)
    add_sphere("BrassFrontBead", (0, 0.049, -0.448), 0.0055, m_brass, segments=14, ring_count=10)

    finalize_and_export("weapon_shotgun_90s.glb", preserve_smoothing=True)

# ==============================================================================
# 13. RENDER CRISP CENTERED WEAPON ICONS FOR HOTBAR SLOTS 1..4
# ==============================================================================
def render_centered_icon(glb_name, png_name, roll_deg=32.0):
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
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Camera sees the 3/4 leather loop & buckle face, and the rich 3/4 side of each weapon.
    if "belt" in glb_name:
        direction = Vector((0.08, 0.72, -0.68))
    elif "solder" in glb_name:
        direction = Vector((0.88, -0.34, 0.42))
    else:
        direction = Vector((1.0, -0.35, 0.52))
    rotation = (-direction).to_track_quat('-Z', 'Y')
    from mathutils import Quaternion
    rotation = rotation @ Quaternion(Vector((0,0,1)), math.radians(roll_deg))
    # Fit actual projected vertices rather than a world-axis bounding box.
    projected = [rotation.inverted() @ (obj.matrix_world @ v.co) for v in obj.data.vertices]
    low = Vector((min(v.x for v in projected), min(v.y for v in projected), 0))
    high = Vector((max(v.x for v in projected), max(v.y for v in projected), 0))
    obj.location -= rotation @ ((low+high)*.5)
    span = max(high.x-low.x, high.y-low.y)

    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'

    cam_data = bpy.data.cameras.new("IconCam")
    cam_data.type = 'ORTHO'
    cam_data.ortho_scale = span * 1.07
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam_obj)

    target = bpy.data.objects.new("Target", None)
    scene.collection.objects.link(target)
    target.location = (0, 0, 0)

    cam_obj.location = direction.normalized() * 2.5
    cam_obj.rotation_mode = 'QUATERNION'
    cam_obj.rotation_quaternion = rotation
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("SunKey",  (1.8, -2.5, 2.0), 6.2, (1.0, 0.96, 0.88)),
        ("SunFill", (-2.0, -2.0, 1.2), 3.8, (0.80, 0.90, 1.0)),
        ("SunRim",  (0.0, 2.2, 2.2),  4.5, (1.0, 0.88, 0.65)),
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
    print("Rendered icon:", out_png, os.path.getsize(out_png))

def build_weapon_uzi():
    # The partner and first-person SMG use the same swatches and mesh builder.
    import sys
    sys.path.insert(0, str(ASSETS.parent))
    from generate_anatomy import game_prop
    game_prop()


def render_hotbar_icons(selected=None):
    for slot, (name, angle) in enumerate((("belt", 18), ("solder", -36), ("uzi", -26), ("shotgun", 28)), 1):
        if selected is None or name == selected:
            render_centered_icon(f"weapon_{name}_90s.glb", f"icon_slot_{slot}_{name}.png", angle)


if __name__ == "__main__":
    if "--solder-only" in __import__("sys").argv:
        build_weapon_solder()
        render_centered_icon("weapon_solder_90s.glb", "icon_slot_2_solder.png", -36)
    elif "--ficus-only" in __import__("sys").argv:
        build_ficus_tree()
    elif "--chair-only" in __import__("sys").argv:
        build_reception_chair()
    elif "--weapons-only" in __import__("sys").argv:
        build_weapon_belt()
        build_weapon_solder()
        build_weapon_uzi()
        build_weapon_shotgun()
        render_hotbar_icons()
    elif "--belt-only" in __import__("sys").argv:
        build_weapon_belt()
        render_centered_icon("weapon_belt_90s.glb", "icon_slot_1_belt.png", 18.0)
    elif "--audit-fix" in __import__("sys").argv:
        build_hr_reception_desk()
        build_weapon_belt()
        render_centered_icon("weapon_belt_90s.glb", "icon_slot_1_belt.png", 18.0)
    elif "--workshop6-icons" in __import__("sys").argv:
        render_centered_icon("weapon_uzi_90s.glb", "icon_slot_3_uzi.png", -26.0)
    elif "--workshop5" in __import__("sys").argv:
        build_weapon_shotgun()
    elif "--workshop5-icons" in __import__("sys").argv:
        render_centered_icon("weapon_uzi_90s.glb", "icon_slot_3_uzi.png", -26.0)
        render_centered_icon("weapon_shotgun_90s.glb", "icon_slot_4_shotgun.png", 28.0)
    elif "--reception-only" in __import__("sys").argv:
        build_hr_reception_desk()
    else:
        if "--icons-only" not in __import__("sys").argv:
            build_reception_chair()
            build_water_cooler()
            build_vending_machine()
            build_filing_cabinet()
            build_whiteboard()
            build_ficus_tree()
            build_trash_bin()
            build_server_rack()
            build_hr_reception_desk()
            build_weapon_belt()
            build_weapon_solder()
            build_weapon_uzi()
            build_weapon_shotgun()

        render_hotbar_icons()
