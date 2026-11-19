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
# 3. `prop_workstation_full.glb` & 4. `prop_leather_sofa.glb`
#    Built by assets/external_cc0/office/build_hero_office_props.py
# ==============================================================================
_hero_props = runpy.run_path(str(Path(__file__).parent / "assets/external_cc0/office/build_hero_office_props.py"))
_hero_props["build_prop_workstation_full"]()
_hero_props["build_prop_leather_sofa"]()

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
