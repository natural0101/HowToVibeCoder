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

def finalize_and_export(filename, preserve_smoothing=False):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
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
    # Subtle pale mineral veins across the dark stone counter.
    for i in range(7):
        t=.22+i*.44
        add_rod("Stone_Vein",(2.13*math.cos(t),.922,.3-1.37*math.sin(t)),(2.52*math.cos(t+.018),.922,.3-1.76*math.sin(t+.018)),.003,.002,vein,vertices=5)
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
    m_leather = make_mat("RichLeather", (0.38, 0.19, 0.09, 1), roughness=0.40)
    m_stitch = make_mat("LeatherEdge", (0.54, 0.29, 0.14, 1), roughness=0.45)
    m_gold = make_mat("HeavyBrass", (0.96, 0.78, 0.22, 1), metallic=0.88, roughness=0.18)
    m_prong = make_mat("SteelProng", (0.90, 0.92, 0.96, 1), metallic=0.9, roughness=0.15)

    # One swept rectangular leather ribbon, 44 mm wide and 5.5 mm thick.
    def centre(t):
        return Vector((.045-.405*t, -.045*math.sin(math.pi*t), .10*t))
    across = Vector((.10, 0, .36)).normalized()
    verts, faces = [], []
    # Return fold at the right end is connected to the long ribbon, not rods.
    rings=[]
    for i in range(9):
        rings.append((Vector((-.025+.070*i/8,.018,0)),Vector((0,0,1)),Vector((0,1,0))))
    for i in range(1,17):
        a=math.pi*i/16
        rings.append((Vector((.045+.009*math.sin(a),.009+.009*math.cos(a),0)),
                      Vector((0,0,1)),Vector((math.sin(a),math.cos(a),0))))
    for i in range(1,65):
        rings.append((centre(i/64),across,Vector((0,-1,0))))
    for c,width,normal in rings:
        for w,h in ((-1,-1),(1,-1),(1,1),(-1,1)):
            verts.append(rl_pos(c+width*(w*.022)+normal*(h*.00275)))
    for i in range(len(rings)-1):
        for j in range(4):
            faces.append((4*i+j,4*i+(j+1)%4,4*(i+1)+(j+1)%4,4*(i+1)+j))
    end=4*(len(rings)-1)
    faces += [(3,2,1,0),(end,end+1,end+2,end+3)]
    data=bpy.data.meshes.new("Continuous leather ribbon")
    data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new("Continuous leather ribbon",data)
    bpy.context.collection.objects.link(obj); obj.data.materials.append(m_leather)
    bpy.context.view_layer.objects.active=obj; obj.select_set(True)
    bevel=obj.modifiers.new("Rounded leather edges","BEVEL")
    bevel.width=.001; bevel.segments=3
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    # Stitches are thread, never the leather body.
    for side in (-1,1):
        for i in range(48):
            a=centre((i+.15)/48)+across*(side*.0185)+Vector((0,.0031,0))
            b=centre((i+.65)/48)+across*(side*.0185)+Vector((0,.0031,0))
            add_rod("Saddle stitch",a,b,.00055,.00055,m_stitch,6)
    for i in range(5):
        c=centre(.23+i*.10)+Vector((0,.0032,0))
        bpy.ops.mesh.primitive_torus_add(major_segments=20,minor_segments=8,
            location=rl_pos(c),major_radius=.0032,minor_radius=.0008)
        bpy.context.object.name="Brass eyelet"
        bpy.context.object.data.materials.append(m_gold)
    # Open frame extends beyond the leather end, away from the sleeve.
    c=centre(1); bx,by,bz=c
    for z in (-.028,.028):
        add_box("Buckle long rail",(bx-.022,by,bz+z),(.056,.007,.007),m_gold,bevel=.002)
    for x in (.006,-.050):
        add_box("Buckle cross rail",(bx+x,by,bz),(.007,.007,.063),m_gold,bevel=.002)
    add_rod("Steel buckle tongue",(bx+.006,by+.004,bz),(bx-.049,by+.005,bz),.002,.0016,m_prong,10)

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
    add_sphere("Pommel", (0, 0, -0.12), 0.017, m_wood, segments=14, ring_count=10)
    add_rod("WoodHandle", (0, 0, -0.12), (0, 0, 0.12), 0.016, 0.016, m_wood, 16)
    add_rod("BlueTapeWrap", (0, 0, -0.06), (0, 0, 0.06), 0.0175, 0.017, m_tape, 16)
    for z in (-.105,-.09,.085,.10):
        add_rod("Turned wood bead",(0,0,z),(0,0,z+.005),.018,.018,m_wood,24)
    for i in range(6):
        z=-.058+i*.019
        add_rod("Tape overlap",(0,0,z),(0,0,z+.0015),.0177,.0177,m_tape,24)
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

    def smooth(obj):
        for face in obj.data.polygons: face.use_smooth = len(face.vertices)<=4
        return obj

    def curve(name, points, radius, material):
        data=bpy.data.curves.new(name,'CURVE')
        data.dimensions='3D'; data.bevel_depth=radius; data.bevel_resolution=3
        spline=data.splines.new('BEZIER'); spline.bezier_points.add(len(points)-1)
        for p,co in zip(spline.bezier_points,points):
            p.co=rl_pos(co); p.handle_left_type=p.handle_right_type='AUTO'
        obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
        data.materials.append(material)
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True)
        bpy.context.view_layer.objects.active=obj; bpy.ops.object.convert(target='MESH')
        return obj

    # One continuous oval walnut wrist, with 24 vertices around every section.
    rings=[(-.106,.099,.027,.021),(-.095,.091,.027,.023),(-.065,.071,.024,.024),
           (-.035,.048,.022,.023),(-.010,.018,.023,.027),(.004,-.018,.026,.029)]
    verts=[rl_pos((rx*math.cos(i*math.tau/24),y,z+rz*math.sin(i*math.tau/24)))
           for y,z,rx,rz in rings for i in range(24)]
    faces=[(k*24+i,k*24+(i+1)%24,(k+1)*24+(i+1)%24,(k+1)*24+i)
           for k in range(len(rings)-1) for i in range(24)]
    faces += [tuple(reversed(range(24))),tuple(range(len(verts)-24,len(verts)))]
    mesh=bpy.data.meshes.new('Walnut wrist 24 segments'); mesh.from_pydata(verts,[],faces); mesh.update()
    grip=bpy.data.objects.new('Continuous walnut grip',mesh); bpy.context.collection.objects.link(grip)
    mesh.materials.append(m_walnut); smooth(grip)
    m_check=make_mat('Cut walnut checkering',(.23,.095,.035,1),roughness=.7)
    for side in (-1,1):
        for j in range(8):
            y=-.09+j*.006
            z=.091-(y+.095)*.74
            for slope in (-1,1):
                curve('Diamond grip checkering',[(side*.024,y-.004,z-.010),
                    (side*.025,y,z),(side*.024,y+.004,z+slope*.010)],.00065,m_check)
    smooth(add_sphere('Steel grip heel',(0,-.105,.099),1,m_receiver,scale=(.028,.004,.022),segments=24))
    # Twin rounded fences blend into a tapered action and polished upper tang.
    for side in (-1,1):
        smooth(add_sphere('Rounded breech fence',(side*.017,.020,-.064),1,m_receiver,
                          scale=(.021,.029,.050),segments=32,ring_count=16))
    smooth(add_sphere('Action belly',(0,-.004,-.055),1,m_receiver,scale=(.031,.023,.055),segments=24))
    curve('Swept top tang',[(0,.040,-.060),(0,.033,-.020),(0,.017,.021),(0,-.002,.045)],.006,m_receiver)
    curve('Westley Richards locking lever',[(0,.047,-.075),(.005,.054,-.055),(.008,.054,-.023),(.012,.043,.006)],.0035,m_barrel)
    smooth(add_sphere('Lever thumb head',(.012,.043,.008),1,m_receiver,scale=(.009,.004,.012),segments=24))
    for side in (-1,1):
        x=side*.029
        smooth(add_rod('Hammer pivot',(x-side*.003,.014,-.047),(x+side*.003,.014,-.047),.007,.007,m_barrel,24))
        curve('Cocked external hammer',[(x,.014,-.047),(x,.032,-.032),(x,.058,-.024),(x,.065,-.041)],.0045,m_barrel)
        smooth(add_sphere('Hammer spur',(x,.059,-.017),1,m_receiver,scale=(.006,.004,.011),segments=24))
    curve('Oval trigger guard',[(0,-.020,-.085),(0,-.047,-.081),(0,-.056,-.053),
                               (0,-.048,-.019),(0,-.021,-.006)],.003,m_barrel)
    for x,z in ((-.005,-.065),(.005,-.041)):
        curve('Separate curved trigger',[(x,-.019,z),(x,-.035,z+.006),(x,-.042,z-.001)],.0023,m_receiver)
    smooth(add_sphere('Rounded walnut forearm',(0,-.004,-.205),1,m_walnut,
                      scale=(.027,.019,.105),segments=24,ring_count=16))

    for side in [-1, 1]:
        bx = side * 0.017
        smooth(add_rod(f"Barrel_{side}", (bx, 0.024, -0.095), (bx, 0.024, -0.455), 0.0165, 0.0160, m_barrel, 32))
        smooth(add_rod(f"MuzzleCrown_{side}", (bx, 0.024, -0.455), (bx, 0.024, -0.468), 0.0175, 0.0175, m_receiver, 32))
    add_box("CenterRib", (0, 0.038, -0.275), (0.008, 0.004, 0.36), m_barrel, bevel=.001)
    add_sphere("FrontBead", (0, 0.045, -0.450), 0.006, m_brass, segments=8, ring_count=6)

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

    # Camera sees the leather face and the long side of the iron, never their ends.
    direction = Vector((0, -.32, 1)) if "belt" in glb_name else Vector((1, -.35, .55))
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
    cam_data.ortho_scale = span * 1.08
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

if "--workshop6-icons" in __import__("sys").argv:
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
        build_weapon_shotgun()

    render_centered_icon("weapon_belt_90s.glb", "icon_slot_1_belt.png", 35.0)
    render_centered_icon("weapon_solder_90s.glb", "icon_slot_2_solder.png", -38.0)
    render_centered_icon("weapon_uzi_90s.glb", "icon_slot_3_uzi.png", -26.0)
    if "--icons-only" not in __import__("sys").argv:
        render_centered_icon("weapon_shotgun_90s.glb", "icon_slot_4_shotgun.png", 28.0)
