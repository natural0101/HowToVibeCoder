import bpy
import math
import os
from mathutils import Vector, Matrix

OUT_MODELS = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"
OUT_ICONS  = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\icons"
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
    clear_scene()
    m_pot = make_mat("PlanterPot", (0.72, 0.46, 0.30, 1), roughness=0.55)
    m_rim = make_mat("PlanterRim", (0.82, 0.54, 0.36, 1), roughness=0.5)
    m_soil = make_mat("SoilDark", (0.22, 0.16, 0.12, 1), roughness=0.8)
    m_bark = make_mat("TrunkBark", (0.42, 0.29, 0.18, 1), roughness=0.7)
    m_leaf1 = make_mat("LeafDark", (0.14, 0.48, 0.25, 1), roughness=0.45)
    m_leaf2 = make_mat("LeafMid", (0.20, 0.60, 0.32, 1), roughness=0.45)
    m_leaf3 = make_mat("LeafBright", (0.28, 0.70, 0.38, 1), roughness=0.4)

    add_rod("PotBody", (0, 0.0, 0), (0, 0.56, 0), 0.26, 0.36, m_pot, 16)
    add_vcyl("PotRim", (0, 0.55, 0), 0.38, 0.06, m_rim, vertices=16)
    add_vcyl("Soil", (0, 0.53, 0), 0.34, 0.03, m_soil, vertices=16)
    add_rod("MainTrunk", (0, 0.52, 0), (0, 1.45, 0), 0.08, 0.042, m_bark, 10)

    branches = [
        ((-0.24, 1.32, 0.16), m_leaf1, (0.40, 0.34, 0.38)),
        ((0.26, 1.40, -0.18), m_leaf2, (0.42, 0.36, 0.40)),
        ((-0.18, 1.58, -0.22), m_leaf3, (0.38, 0.32, 0.38)),
        ((0.20, 1.66, 0.22), m_leaf1, (0.40, 0.34, 0.40)),
        ((0.0, 1.86, 0.0), m_leaf2, (0.48, 0.40, 0.48)),
        ((-0.30, 1.50, 0.02), m_leaf3, (0.34, 0.28, 0.34)),
        ((0.30, 1.54, 0.04), m_leaf3, (0.34, 0.28, 0.34)),
    ]
    for idx, (pos, lmat, scl) in enumerate(branches):
        add_rod(f"Branch_{idx}", (0, 0.92 + idx*0.06, 0), pos, 0.038, 0.018, m_bark, 8)
        add_sphere(f"Canopy_{idx}", pos, 1.0, lmat, scale=scl, segments=12, ring_count=8)

    finalize_and_export("prop_ficus_tree.glb")

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
    m_marble = make_mat("DarkMarble", (0.16, 0.18, 0.24, 1), roughness=0.25)
    m_front = make_mat("VelvetPlum", (0.38, 0.16, 0.44, 1), roughness=0.38)
    m_brass = make_mat("PolishedBrass", (0.90, 0.70, 0.28, 1), metallic=0.82, roughness=0.20)
    m_top = make_mat("CounterTop", (0.92, 0.90, 0.86, 1), roughness=0.24)
    m_screen = make_mat("HrMonitor", (0.12, 0.82, 0.98, 1), roughness=0.15)

    # White-Shark style curved reception wings framing HR Veronika's central Tarot table (at origin)
    for side in [-1, 1]:
        add_box(f"WingBody_{side}", (side * 1.92, 0.48, 0.18), (1.35, 0.96, 0.82), m_marble, rot=(0, side * -0.36, 0), bevel=0.025)
        add_box(f"WingPanel_{side}", (side * 1.92, 0.48, -0.22), (1.22, 0.76, 0.03), m_front, rot=(0, side * -0.36, 0))
        add_box(f"WingTop_{side}", (side * 1.92, 0.98, 0.18), (1.44, 0.06, 0.92), m_top, rot=(0, side * -0.36, 0), bevel=0.01)
        add_box(f"WingBrass_{side}", (side * 1.92, 0.92, -0.23), (1.38, 0.04, 0.03), m_brass, rot=(0, side * -0.36, 0))
        # HR reception terminal monitor on each wing
        add_box(f"TermStand_{side}", (side * 1.85, 1.08, 0.18), (0.08, 0.16, 0.08), m_brass)
        add_box(f"TermScreen_{side}", (side * 1.85, 1.24, 0.18), (0.48, 0.28, 0.03), m_marble, rot=(0, side * -0.36, 0), bevel=0.006)
        add_box(f"TermGlow_{side}", (side * 1.85, 1.24, 0.16), (0.42, 0.22, 0.015), m_screen, rot=(0, side * -0.36, 0))

    finalize_and_export("prop_hr_reception_desk.glb")

# ==============================================================================
# 9. PROP: ESCAPED AI AGENT BOSS (escaped_ai_boss.glb)
# ==============================================================================
def build_escaped_ai_boss():
    clear_scene()
    m_armor = make_mat("BossArmor", (0.16, 0.19, 0.26, 1), metallic=0.65, roughness=0.3)
    m_chassis = make_mat("BossChassis", (0.28, 0.32, 0.40, 1), metallic=0.7, roughness=0.35)
    m_crimson = make_mat("BossDanger", (0.92, 0.16, 0.22, 1), roughness=0.2)
    m_cyan = make_mat("BossCore", (0.10, 0.88, 0.98, 1), roughness=0.15)
    m_gold = make_mat("BossGold", (0.96, 0.74, 0.18, 1), metallic=0.8, roughness=0.2)
    m_screen = make_mat("BossScreen", (0.06, 0.10, 0.16, 1), roughness=0.15)

    for side in [-1, 1]:
        add_box(f"Track_{side}", (side * 0.78, 0.32, 0), (0.36, 0.56, 1.48), m_chassis, bevel=0.06)
        for w in [-0.48, 0.0, 0.48]:
            add_rod(f"Wheel_{side}_{w}", (side * 0.94, 0.26, w), (side * 1.02, 0.26, w), 0.22, 0.22, m_gold, 14)
    add_box("Pelvis", (0, 0.56, 0), (1.32, 0.34, 1.12), m_armor, bevel=0.04)
    add_box("Torso", (0, 1.42, 0), (1.46, 1.42, 1.04), m_armor, bevel=0.05)
    add_box("ReactorFrame", (0, 1.42, -0.53), (0.86, 0.86, 0.06), m_chassis, bevel=0.02)
    add_rod("ReactorCore", (0, 1.42, -0.52), (0, 1.42, -0.58), 0.30, 0.28, m_crimson, 16)
    for r in range(4):
        add_box(f"Vent_{r}", (0, 0.88 + r*0.12, -0.53), (1.10, 0.05, 0.04), m_cyan)

    for side in [-1, 1]:
        add_sphere(f"Shoulder_{side}", (side * 0.94, 1.88, 0), 0.26, m_crimson, segments=12, ring_count=8)
        add_rod(f"UpperArm_{side}", (side * 0.96, 1.82, 0), (side * 1.16, 1.18, -0.26), 0.16, 0.15, m_chassis, 12)
        add_box(f"ForeArm_{side}", (side * 1.18, 1.12, -0.58), (0.32, 0.32, 0.74), m_armor, bevel=0.02)
        add_box(f"ClawTop_{side}", (side * 1.18, 1.22, -1.02), (0.26, 0.08, 0.32), m_gold)
        add_box(f"ClawBot_{side}", (side * 1.18, 1.02, -1.02), (0.26, 0.08, 0.32), m_gold)

    add_box("HeadMonitor", (0, 2.48, -0.06), (0.96, 0.74, 0.76), m_chassis, bevel=0.04)
    add_box("FaceScreen", (0, 2.48, -0.445), (0.80, 0.56, 0.03), m_screen)
    for side in [-1, 1]:
        add_box(f"Eye_{side}", (side * 0.20, 2.56, -0.465), (0.22, 0.08, 0.02), m_crimson)
        add_vcyl(f"Antenna_{side}", (side * 0.36, 3.02, 0), 0.025, 0.42, m_gold, vertices=8)
        add_sphere(f"AntTip_{side}", (side * 0.36, 3.25, 0), 0.065, m_crimson, segments=10, ring_count=8)
    add_box("MouthWave", (0, 2.34, -0.465), (0.46, 0.06, 0.02), m_cyan)

    finalize_and_export("escaped_ai_boss.glb")

# ==============================================================================
# 10. WEAPON 1: FATHER'S LEATHER BELT (weapon_belt_90s.glb)
# ==============================================================================
def build_weapon_belt():
    clear_scene()
    m_leather = make_mat("RichLeather", (0.38, 0.19, 0.09, 1), roughness=0.40)
    m_stitch = make_mat("LeatherEdge", (0.54, 0.29, 0.14, 1), roughness=0.45)
    m_gold = make_mat("HeavyBrass", (0.96, 0.78, 0.22, 1), metallic=0.88, roughness=0.18)
    m_prong = make_mat("SteelProng", (0.90, 0.92, 0.96, 1), metallic=0.9, roughness=0.15)

    pts = [
        ( 0.00,  0.00,  0.00),
        (-0.05,  0.03,  0.08),
        (-0.11,  0.04,  0.16),
        (-0.17,  0.02,  0.23),
        (-0.23, -0.02,  0.28),
        (-0.28, -0.06,  0.32),
    ]
    for i in range(len(pts)-1):
        p0 = pts[i]
        p1 = pts[i+1]
        add_rod(f"StrapTop_{i}", p0, p1, 0.022, 0.021, m_leather, 12)
        p0_b = (p0[0], p0[1]-0.024, p0[2])
        p1_b = (p1[0], p1[1]-0.024, p1[2])
        add_rod(f"StrapBot_{i}", p0_b, p1_b, 0.019, 0.018, m_stitch, 12)
        mid = ((p0[0]+p1[0])*0.5, (p0[1]+p1[1])*0.5 + 0.016, (p0[2]+p1[2])*0.5)
        add_sphere(f"Stud_{i}", mid, 0.008, m_gold, segments=8, ring_count=6)

    bx, by, bz = pts[-1]
    # Open rectangular brass buckle frame lying along the strap end with a clear hollow center and steel prong
    add_box("BuckleTopBar",  (bx-0.02, by+0.022, bz+0.025), (0.064, 0.012, 0.072), m_gold, bevel=0.002)
    add_box("BuckleBotBar",  (bx-0.02, by-0.022, bz+0.025), (0.064, 0.012, 0.072), m_gold, bevel=0.002)
    add_box("BuckleBackBar", (bx-0.005, by,      bz-0.005), (0.064, 0.054, 0.014), m_gold, bevel=0.002)
    add_box("BuckleTipBar",  (bx-0.035, by,      bz+0.055), (0.064, 0.054, 0.014), m_gold, bevel=0.002)
    add_rod("BuckleProng",   (bx-0.005, by,      bz-0.005), (bx-0.040, by+0.006, bz+0.060), 0.006, 0.004, m_prong, 8)

    finalize_and_export("weapon_belt_90s.glb")

# ==============================================================================
# 11. WEAPON 2: 90s SOLDERING IRON WITH BLUE TAPE (weapon_solder_90s.glb)
# ==============================================================================
def build_weapon_solder():
    clear_scene()
    m_wood = make_mat("LatheWood", (0.50, 0.23, 0.10, 1), roughness=0.42)
    m_tape = make_mat("BlueTape", (0.10, 0.40, 0.96, 1), roughness=0.28)
    m_collar = make_mat("Bakelite", (0.14, 0.12, 0.12, 1), roughness=0.35)
    m_steel = make_mat("HeaterSteel", (0.74, 0.78, 0.84, 1), metallic=0.82, roughness=0.24)
    m_copper = make_mat("HotCopper", (0.98, 0.46, 0.12, 1), metallic=0.55, roughness=0.20)
    m_glow = make_mat("TipGlow", (1.0, 0.80, 0.26, 1), roughness=0.1)

    # Strictly along Raylib +Z axis (from Z=-0.12 to Z=+0.46)!
    add_sphere("Pommel", (0, 0, -0.12), 0.030, m_wood, segments=14, ring_count=10)
    add_rod("WoodHandle", (0, 0, -0.12), (0, 0, 0.12), 0.030, 0.024, m_wood, 16)
    add_rod("BlueTapeWrap", (0, 0, -0.06), (0, 0, 0.06), 0.032, 0.029, m_tape, 16)
    add_rod("GuardFlange", (0, 0, 0.115), (0, 0, 0.135), 0.038, 0.036, m_collar, 16)
    add_rod("HeaterTube", (0, 0, 0.135), (0, 0, 0.365), 0.016, 0.015, m_steel, 14)
    for r_i in range(4):
        rz = 0.165 + r_i * 0.048
        add_rod(f"VentRing_{r_i}", (0, 0, rz), (0, 0, rz+0.012), 0.019, 0.019, m_collar, 14)
    add_rod("CopperTip", (0, 0, 0.365), (0, 0.014, 0.455), 0.014, 0.005, m_copper, 12)
    add_rod("GlowingPoint", (0, 0.014, 0.455), (0, 0.018, 0.485), 0.006, 0.002, m_glow, 10)

    finalize_and_export("weapon_solder_90s.glb")

# ==============================================================================
# 12. WEAPON 4: SAWED-OFF DOUBLE-BARREL SHOTGUN (weapon_shotgun_90s.glb)
# ==============================================================================
def build_weapon_shotgun():
    clear_scene()
    m_walnut = make_mat("WalnutStock", (0.46, 0.22, 0.10, 1), roughness=0.38)
    m_receiver = make_mat("ReceiverSteel", (0.30, 0.33, 0.38, 1), metallic=0.80, roughness=0.26)
    m_barrel = make_mat("BluedSteel", (0.18, 0.21, 0.26, 1), metallic=0.88, roughness=0.20)
    m_brass = make_mat("BrassBead", (0.96, 0.78, 0.24, 1), metallic=0.85, roughness=0.2)

    # Match weapon_uzi_90s convention: grip at +Z, twin barrels extending toward -Z!
    add_rod("SawedOffGrip", (0, -0.095, 0.095), (0, -0.005, 0.025), 0.026, 0.024, m_walnut, 12)
    add_rod("StockWrist", (0, -0.005, 0.025), (0, 0.010, -0.035), 0.025, 0.024, m_walnut, 12)
    add_box("ReceiverBlock", (0, 0.015, -0.065), (0.058, 0.056, 0.095), m_receiver, bevel=0.006)
    add_box("TopLever", (0, 0.046, -0.048), (0.016, 0.012, 0.042), m_brass)
    add_box("ForendWood", (0, -0.004, -0.205), (0.054, 0.038, 0.20), m_walnut, bevel=0.008)

    for side in [-1, 1]:
        bx = side * 0.017
        add_rod(f"Barrel_{side}", (bx, 0.024, -0.095), (bx, 0.024, -0.455), 0.0165, 0.0160, m_barrel, 16)
        add_rod(f"MuzzleCrown_{side}", (bx, 0.024, -0.455), (bx, 0.024, -0.468), 0.0175, 0.0175, m_receiver, 16)
    add_box("CenterRib", (0, 0.038, -0.275), (0.014, 0.008, 0.36), m_receiver)
    add_sphere("FrontBead", (0, 0.045, -0.450), 0.006, m_brass, segments=8, ring_count=6)

    finalize_and_export("weapon_shotgun_90s.glb")

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
    bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)

    # Rotate into side-profile diagonal presentation in X-Z plane (seen from -Y camera)
    obj.rotation_euler = (math.radians(roll_deg), math.radians(14.0), math.radians(-72.0))
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
    cam_data.ortho_scale = span * 1.16
    cam_obj = bpy.data.objects.new("IconCam", cam_data)
    scene.collection.objects.link(cam_obj)

    target = bpy.data.objects.new("Target", None)
    scene.collection.objects.link(target)
    target.location = (0, 0, 0)

    cam_obj.location = (0.0, -2.5, 0.25)
    track = cam_obj.constraints.new(type='TRACK_TO')
    track.target = target
    track.track_axis = 'TRACK_NEGATIVE_Z'
    track.up_axis = 'UP_Y'
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("SunKey",  (1.8, -2.5, 2.0), 5.0, (1.0, 0.96, 0.88)),
        ("SunFill", (-2.0, -2.0, 1.0), 2.8, (0.78, 0.88, 1.0)),
        ("SunRim",  (0.0, 2.0, 2.0),  3.5, (1.0, 0.88, 0.65)),
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

build_water_cooler()
build_vending_machine()
build_filing_cabinet()
build_whiteboard()
build_ficus_tree()
build_trash_bin()
build_server_rack()
build_hr_reception_desk()
build_escaped_ai_boss()
build_weapon_belt()
build_weapon_solder()
build_weapon_shotgun()

render_centered_icon("weapon_belt_90s.glb",    "icon_slot_1_belt.png",    28.0)
render_centered_icon("weapon_solder_90s.glb",  "icon_slot_2_solder.png",  32.0)
render_centered_icon("weapon_uzi_90s.glb",     "icon_slot_3_uzi.png",     26.0)
render_centered_icon("weapon_shotgun_90s.glb", "icon_slot_4_shotgun.png", 28.0)
