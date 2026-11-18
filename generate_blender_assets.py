import bpy
import os
import math

out_dir = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"
os.makedirs(out_dir, exist_ok=True)

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def make_mat(name, rgba, metallic=0.2, roughness=0.45, emission=None):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = emission
            bsdf.inputs["Emission Strength"].default_value = 2.5
    return mat

def add_bevel(obj, width=0.03, segments=2):
    mod = obj.modifiers.new(name="Bevel", type='BEVEL')
    mod.width = width
    mod.segments = segments
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=mod.name)

# 1. Generate `escaped_ai_boss.glb` (Wheeled Server-Rack Boss with Blue Duct Tape & CRT Head)
clear_scene()
mat_steel = make_mat("DarkSteel", (0.14, 0.17, 0.22, 1.0), metallic=0.75, roughness=0.35)
mat_tape = make_mat("BlueDuctTape", (0.08, 0.42, 0.92, 1.0), metallic=0.1, roughness=0.6)
mat_crt = make_mat("BeigeCRT", (0.76, 0.73, 0.66, 1.0), metallic=0.1, roughness=0.5)
mat_screen = make_mat("GreenPhosphor", (0.18, 0.95, 0.52, 1.0), metallic=0.0, roughness=0.15, emission=(0.18, 0.95, 0.52, 1.0))
mat_wheel = make_mat("RubberWheel", (0.06, 0.06, 0.07, 1.0), metallic=0.1, roughness=0.8)

# Main server rack chassis
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 1.25))
chassis = bpy.context.active_object
chassis.scale = (1.5, 1.2, 1.85)
bpy.ops.object.transform_apply(scale=True)
add_bevel(chassis, 0.04, 2)
chassis.data.materials.append(mat_steel)

# Blue duct tape straps
for z_off in (0.95, 1.55):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, z_off))
    tape = bpy.context.active_object
    tape.scale = (1.56, 1.26, 0.20)
    bpy.ops.object.transform_apply(scale=True)
    add_bevel(tape, 0.015, 1)
    tape.data.materials.append(mat_tape)

# CRT Monitor Head
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 2.55))
head = bpy.context.active_object
head.scale = (1.02, 0.86, 0.80)
bpy.ops.object.transform_apply(scale=True)
add_bevel(head, 0.05, 2)
head.data.materials.append(mat_crt)

# Glowing CRT Screen
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0.43, 2.55))
scr = bpy.context.active_object
scr.scale = (0.80, 0.04, 0.58)
bpy.ops.object.transform_apply(scale=True)
scr.data.materials.append(mat_screen)

# 4 Caster Wheels
for wx in (-0.58, 0.58):
    for wy in (-0.45, 0.45):
        bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=0.22, depth=0.16, location=(wx, wy, 0.22), rotation=(0, math.pi/2, 0))
        wh = bpy.context.active_object
        wh.data.materials.append(mat_wheel)

bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, "escaped_ai_boss.glb"), export_format='GLB')

# The SMG now shares its grip geometry with the anatomical partner model.
# Both assets are authored by generate_anatomy.py.

# 3. Generate `office_workstation.glb` (Beveled L-Desk + Dual Monitors + Swivel Chair)
clear_scene()
mat_wood = make_mat("DeskWood", (0.26, 0.28, 0.34, 1.0), metallic=0.2, roughness=0.4)
mat_mon = make_mat("MonitorFrame", (0.10, 0.11, 0.14, 1.0), metallic=0.4, roughness=0.3)
mat_code = make_mat("CodeGlow", (0.12, 0.82, 0.95, 1.0), metallic=0.0, roughness=0.2, emission=(0.12, 0.82, 0.95, 1.0))

bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0.76))
desk = bpy.context.active_object
desk.scale = (2.1, 1.05, 0.08)
bpy.ops.object.transform_apply(scale=True)
add_bevel(desk, 0.015, 2)
desk.data.materials.append(mat_wood)

for mx in (-0.46, 0.46):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(mx, -0.18, 1.12))
    m = bpy.context.active_object
    m.scale = (0.76, 0.06, 0.46)
    bpy.ops.object.transform_apply(scale=True)
    add_bevel(m, 0.01, 1)
    m.data.materials.append(mat_mon)

bpy.ops.export_scene.gltf(filepath=os.path.join(out_dir, "office_workstation.glb"), export_format='GLB')
print("BLENDER_GLB_EXPORT_OK")