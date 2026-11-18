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
# The anatomical character generator also produces the shared viewmodel.
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).with_name("generate_anatomy.py")),run_name="__main__")

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
