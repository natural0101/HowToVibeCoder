import bpy
import bmesh
import os
import math

out_dir = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"
os.makedirs(out_dir, exist_ok=True)

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def make_mat(name, rgba, metallic=0.15, roughness=0.45):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat

def add_box(name, loc, scale, mat, bevel=0.02, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new(name="Bevel", type='BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return obj

def add_cyl(name, loc, radius, depth, mat, rot=(0, 0, 0), verts=16):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.data.materials.append(mat)
    return obj

def add_sphere(name, loc, radius, mat, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=radius, location=loc)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    return obj

def export_glb(filename):
    path = os.path.join(out_dir, filename)
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB')
    print("Exported:", path)

# ==============================================================================
# 1. `char_vibe_coder.glb` — Detailed 3D Vibe Coder (Hoodie, Hood, Kangaroo Pocket,
#    Headphones, Glowing AR Visor, Jeans, Sneakers with White Soles, Laptop in hands)
#    Coordinate convention: Blender +Z is Up -> glTF exports +Y as Up automatically!
# ==============================================================================
clear_scene()
m_hoodie = make_mat("Hoodie", (0.22, 0.56, 0.96, 1.0), roughness=0.65)
m_hoodie_dark = make_mat("HoodieAccent", (0.14, 0.38, 0.72, 1.0), roughness=0.7)
m_jeans = make_mat("Jeans", (0.14, 0.17, 0.25, 1.0), roughness=0.75)
m_sneaker = make_mat("SneakerTop", (0.88, 0.22, 0.32, 1.0), roughness=0.5)
m_sole = make_mat("WhiteSole", (0.95, 0.95, 0.97, 1.0), roughness=0.4)
m_skin = make_mat("Skin", (0.95, 0.78, 0.66, 1.0), roughness=0.55)
m_hair = make_mat("Hair", (0.18, 0.12, 0.08, 1.0), roughness=0.7)
m_visor = make_mat("CyberVisor", (0.10, 0.95, 0.88, 1.0), metallic=0.8, roughness=0.1)
m_phones = make_mat("Headphones", (0.12, 0.13, 0.16, 1.0), metallic=0.6, roughness=0.3)
m_laptop = make_mat("SilverMac", (0.82, 0.85, 0.90, 1.0), metallic=0.85, roughness=0.2)

# Sneakers & Soles
for sx in (-0.16, 0.16):
    add_box(f"Sole_{sx}", (sx, 0.04, 0.04), (0.16, 0.30, 0.06), m_sole, bevel=0.01)
    add_box(f"Shoe_{sx}", (sx, 0.02, 0.10), (0.15, 0.26, 0.09), m_sneaker, bevel=0.02)
    add_cyl(f"Leg_{sx}", (sx, 0.0, 0.44), 0.095, 0.62, m_jeans, verts=12)

# Oversized Hoodie Torso + Kangaroo Pocket + Back Hood
add_box("Torso", (0, 0, 1.05), (0.58, 0.36, 0.68), m_hoodie, bevel=0.06)
add_box("KangarooPocket", (0, 0.19, 0.88), (0.38, 0.06, 0.22), m_hoodie_dark, bevel=0.02)
add_sphere("BackHood", (0, -0.16, 1.38), 0.22, m_hoodie_dark, scale=(1.1, 0.8, 0.7))

# Arms & Hands holding open MacBook
for side in (-1, 1):
    add_box(f"UpperArm_{side}", (side * 0.35, 0.04, 1.12), (0.15, 0.16, 0.42), m_hoodie, bevel=0.03, rot=(0.45, 0, -side * 0.15))
    add_box(f"ForeArm_{side}", (side * 0.28, 0.24, 0.94), (0.13, 0.32, 0.13), m_hoodie, bevel=0.02, rot=(0.2, 0, side * 0.25))
    add_sphere(f"Hand_{side}", (side * 0.18, 0.38, 0.92), 0.065, m_skin)

# Open Laptop in Hands
add_box("LaptopBase", (0, 0.42, 0.91), (0.34, 0.24, 0.025), m_laptop, bevel=0.005)
add_box("LaptopLid", (0, 0.53, 1.03), (0.34, 0.02, 0.24), m_laptop, bevel=0.005, rot=(-0.25, 0, 0))
add_box("LaptopScreenGlow", (0, 0.515, 1.03), (0.30, 0.01, 0.20), m_visor, bevel=0.002, rot=(-0.25, 0, 0))

# Neck, Head, Messy Zoomer Hair, AR Visor Glasses & Over-Ear Headphones
add_cyl("Neck", (0, 0, 1.44), 0.08, 0.14, m_skin, verts=10)
add_sphere("Head", (0, 0.02, 1.64), 0.21, m_skin, scale=(0.95, 1.0, 1.08))
add_sphere("HairTop", (0, 0.04, 1.79), 0.19, m_hair, scale=(1.08, 1.12, 0.65))
add_box("ARVisor", (0, 0.20, 1.66), (0.34, 0.08, 0.10), m_visor, bevel=0.015)

# Headphones earcups + headband
for side in (-1, 1):
    add_cyl(f"EarCup_{side}", (side * 0.22, 0.02, 1.65), 0.08, 0.06, m_phones, rot=(0, math.pi/2, 0), verts=12)
add_box("Headband", (0, 0.02, 1.87), (0.44, 0.06, 0.05), m_phones, bevel=0.015)

export_glb("char_vibe_coder.glb")

# ==============================================================================
# 2. `char_hr_veronika.glb` — Detailed 3D HR-Tarot Veronika + Tarot Table + Candles + Crystal Ball
# ==============================================================================
clear_scene()
m_skin = make_mat("Skin", (0.95, 0.78, 0.66, 1.0), roughness=0.55)
m_hair = make_mat("Hair", (0.18, 0.12, 0.08, 1.0), roughness=0.7)
m_velvet = make_mat("PurpleVelvet", (0.42, 0.12, 0.64, 1.0), roughness=0.7)
m_gold = make_mat("GoldTrim", (0.96, 0.78, 0.20, 1.0), metallic=0.9, roughness=0.22)
m_dress = make_mat("MysticDress", (0.56, 0.14, 0.78, 1.0), roughness=0.55)
m_orb = make_mat("CrystalOrb", (0.78, 0.36, 1.0, 1.0), metallic=0.2, roughness=0.1)
m_wax = make_mat("CandleWax", (0.96, 0.93, 0.84, 1.0), roughness=0.5)
m_flame = make_mat("CandleFlame", (1.0, 0.72, 0.15, 1.0), roughness=0.1)
m_card1 = make_mat("TarotCard", (0.95, 0.82, 0.35, 1.0), roughness=0.3)

# Velvet Tarot Table
add_box("TarotTableBody", (0, 0, 0.52), (2.75, 1.45, 1.04), m_velvet, bevel=0.04)
add_box("TarotTableTopGold", (0, 0, 1.06), (2.86, 1.56, 0.08), m_gold, bevel=0.02)

# Crystal Ball on golden pedestal
add_cyl("OrbBase", (0, -0.10, 1.14), 0.22, 0.10, m_gold, verts=16)
add_sphere("CrystalBall", (0, -0.10, 1.36), 0.24, m_orb)

# 4 Tarot Cards on table
for i, cx in enumerate((-0.82, -0.32, 0.32, 0.82)):
    add_box(f"Card_{i}", (cx, -0.38, 1.11), (0.26, 0.40, 0.02), m_card1, bevel=0.004, rot=(0, 0, (i - 1.5) * 0.08))

# 4 Wax Candles with flames
for cx, cy in ((-1.15, -0.45), (-1.02, 0.25), (1.02, 0.25), (1.15, -0.45)):
    add_cyl(f"Candle_{cx}", (cx, cy, 1.24), 0.055, 0.28, m_wax, verts=12)
    add_sphere(f"Flame_{cx}", (cx, cy, 1.43), 0.06, m_flame, scale=(0.7, 0.7, 1.5))

# HR Veronika standing behind table
add_cyl("VeronikaSkirt", (0, 0.82, 0.55), 0.46, 1.10, m_dress, verts=16)
add_sphere("VeronikaTorso", (0, 0.80, 1.28), 0.38, m_dress, scale=(1.15, 0.92, 1.15))
add_cyl("VeronikaNecklace", (0, 0.78, 1.58), 0.22, 0.06, m_gold, verts=16)
add_sphere("VeronikaHead", (0, 0.78, 1.82), 0.23, m_skin)
add_sphere("VeronikaHairBun", (0, 0.88, 2.04), 0.18, m_hair)
add_box("VeronikaHeadband", (0, 0.60, 1.90), (0.36, 0.12, 0.06), m_gold, bevel=0.01)

# Arms raised mystically over the Crystal Ball
for side in (-1, 1):
    add_box(f"VArm_{side}", (side * 0.42, 0.52, 1.44), (0.16, 0.52, 0.16), m_dress, bevel=0.03, rot=(0.35, 0, side * 0.3))
    add_sphere(f"VHand_{side}", (side * 0.28, 0.22, 1.48), 0.075, m_skin)

export_glb("char_hr_veronika.glb")

# ==============================================================================
# 3. `prop_workstation_full.glb` — Rich Office Cubicle (Frosted Partition, Desk,
#    Dual Curved Monitors with Code, PC Tower with RGB, Desk Lamp, Chair, Mug, Folders)
# ==============================================================================
clear_scene()
m_part = make_mat("PartitionFabric", (0.22, 0.25, 0.34, 1.0), roughness=0.8)
m_glass = make_mat("FrostedGlass", (0.55, 0.78, 0.95, 1.0), metallic=0.1, roughness=0.15)
m_desk = make_mat("DeskTop", (0.30, 0.33, 0.40, 1.0), roughness=0.45)
m_legs = make_mat("DarkMetal", (0.12, 0.14, 0.18, 1.0), metallic=0.8, roughness=0.35)
m_pc = make_mat("PcCase", (0.09, 0.10, 0.13, 1.0), metallic=0.7, roughness=0.3)
m_code1 = make_mat("MonitorCode1", (0.10, 0.88, 0.96, 1.0), roughness=0.15)
m_code2 = make_mat("MonitorCode2", (0.20, 0.95, 0.55, 1.0), roughness=0.15)
m_chair = make_mat("ChairMesh", (0.16, 0.18, 0.24, 1.0), roughness=0.6)
m_folder_r = make_mat("RedFolder", (0.88, 0.22, 0.28, 1.0), roughness=0.6)

# L-Shaped Cubicle Partition Wall with Frosted Glass Top
add_box("BackPartition", (0, -0.58, 0.55), (2.25, 0.10, 1.10), m_part, bevel=0.015)
add_box("BackGlass", (0, -0.58, 1.28), (2.22, 0.05, 0.36), m_glass, bevel=0.005)
add_box("SidePartition", (-1.08, -0.05, 0.55), (0.10, 1.15, 1.10), m_part, bevel=0.015)

# Desk Surface + 4 Metal Legs
add_box("DeskTop", (0, 0, 0.76), (2.10, 1.02, 0.07), m_desk, bevel=0.015)
for lx in (-0.95, 0.95):
    for ly in (-0.42, 0.42):
        add_box(f"Leg_{lx}_{ly}", (lx, ly, 0.38), (0.07, 0.07, 0.74), m_legs, bevel=0.005)

# Dual Monitors + Stands + Code Screens
for idx, (mx, rot_z, m_scr) in enumerate(((-0.42, -0.18, m_code1), (0.34, 0.18, m_code2))):
    add_box(f"MonStand_{idx}", (mx, -0.28, 0.88), (0.08, 0.08, 0.22), m_legs, bevel=0.005)
    add_box(f"MonFrame_{idx}", (mx, -0.24, 1.12), (0.72, 0.05, 0.44), m_pc, bevel=0.01, rot=(0, 0, rot_z))
    add_box(f"MonScreen_{idx}", (mx, -0.21, 1.12), (0.66, 0.02, 0.38), m_scr, bevel=0.004, rot=(0, 0, rot_z))

# Keyboard, Mouse, PC Tower, Desk Lamp & Folders
add_box("Keyboard", (-0.05, 0.12, 0.81), (0.44, 0.16, 0.025), m_pc, bevel=0.004)
add_box("Mouse", (0.28, 0.12, 0.81), (0.08, 0.12, 0.025), m_pc, bevel=0.005)
add_box("PcTower", (0.82, -0.12, 1.02), (0.24, 0.46, 0.46), m_pc, bevel=0.015)
add_box("PcRgbStrip", (0.82, 0.115, 1.02), (0.04, 0.02, 0.40), m_code1, bevel=0.002)
add_box("Folder1", (-0.82, 0.15, 0.82), (0.26, 0.34, 0.05), m_folder_r, bevel=0.005, rot=(0, 0, 0.15))

# Swivel Ergonomic Office Chair in front of Desk
add_cyl("ChairPost", (0, 0.72, 0.26), 0.04, 0.42, m_legs, verts=10)
add_box("ChairBase1", (0, 0.72, 0.06), (0.56, 0.07, 0.05), m_legs, bevel=0.005)
add_box("ChairBase2", (0, 0.72, 0.06), (0.07, 0.56, 0.05), m_legs, bevel=0.005)
add_box("ChairSeat", (0, 0.72, 0.48), (0.52, 0.50, 0.08), m_chair, bevel=0.02)
add_box("ChairBack", (0, 0.94, 0.82), (0.48, 0.08, 0.62), m_chair, bevel=0.02, rot=(-0.12, 0, 0))

export_glb("prop_workstation_full.glb")

# ==============================================================================
# 4. `prop_leather_sofa.glb` — 90s Boss Tufted Crimson/Brown Leather Sofa
# ==============================================================================
clear_scene()
m_sofa = make_mat("Leather90s", (0.44, 0.16, 0.14, 1.0), roughness=0.42)
m_sofa_cush = make_mat("LeatherCushion", (0.52, 0.19, 0.16, 1.0), roughness=0.48)

add_box("SofaBase", (0, 0, 0.22), (2.40, 1.02, 0.32), m_sofa, bevel=0.04)
add_box("SofaBack", (0, -0.38, 0.68), (2.40, 0.28, 0.68), m_sofa, bevel=0.06)
add_box("ArmLeft", (-1.12, 0.02, 0.48), (0.24, 1.04, 0.52), m_sofa, bevel=0.05)
add_box("ArmRight", (1.12, 0.02, 0.48), (0.24, 1.04, 0.52), m_sofa, bevel=0.05)
for cx in (-0.64, 0.0, 0.64):
    add_box(f"Cushion_{cx}", (cx, 0.06, 0.40), (0.62, 0.78, 0.16), m_sofa_cush, bevel=0.04)

export_glb("prop_leather_sofa.glb")

# ==============================================================================
# 5. `prop_xerox_printer.glb` — Heavy Office Xerox Photocopier with Scanner Glow
# ==============================================================================
clear_scene()
m_xerox = make_mat("XeroxBody", (0.86, 0.88, 0.92, 1.0), roughness=0.4)
m_xdark = make_mat("XeroxDark", (0.22, 0.25, 0.30, 1.0), roughness=0.5)
m_scan = make_mat("ScannerGreen", (0.15, 0.96, 0.48, 1.0), roughness=0.1)

add_box("XeroxMain", (0, 0, 0.54), (1.28, 0.84, 1.06), m_xerox, bevel=0.03)
add_box("XeroxTray1", (0, 0.43, 0.35), (0.86, 0.04, 0.18), m_xdark, bevel=0.005)
add_box("XeroxTray2", (0, 0.43, 0.62), (0.86, 0.04, 0.18), m_xdark, bevel=0.005)
add_box("ScanGlass", (0, 0, 1.08), (1.05, 0.68, 0.03), m_scan, bevel=0.003)
add_box("TopFeeder", (0, -0.06, 1.18), (1.18, 0.74, 0.14), m_xerox, bevel=0.02, rot=(0.18, 0, 0))
add_box("SidePaperTray", (-0.74, 0, 0.74), (0.28, 0.48, 0.05), m_xdark, bevel=0.005, rot=(0, 0.25, 0))

export_glb("prop_xerox_printer.glb")

print("ALL_DETAILED_GLB_MODELS_EXPORTED")
