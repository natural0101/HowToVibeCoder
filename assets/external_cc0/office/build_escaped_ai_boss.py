import bpy
import bmesh
import math
import os
from pathlib import Path
from mathutils import Vector, Matrix, Euler, Quaternion

ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
PREVIEW_PNG = ROOT / "assets/external_cc0/office/boss_preview_r7.png"

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

# Game coordinates: X right, Y up (height 0..2.26m), Z forward (+Z faces player)
# Blender glTF exporter maps (+X -> +X, +Z -> +Y, -Y -> +Z)
def xyz(p):
    return Vector((p[0], -p[2], p[1]))

def rl_scale(s):
    return Vector((s[0], s[2], s[1]))

def rl_rot(r):
    # r = (pitch_X, yaw_Y, roll_Z) in game space
    return Euler((r[0], -r[2], r[1]), 'XYZ')

MATS = {}

def make_mat(key, rgba, metallic=0.6, roughness=0.35, emission=None, emission_strength=0.0):
    if key in MATS:
        return MATS[key]
    m = bpy.data.materials.new(name=key)
    m.use_nodes = True
    m.diffuse_color = rgba
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = emission
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    MATS[key] = m
    return m

def init_palette():
    MATS.clear()
    # Tuned for VisualEngine3D Finish.Imported (finish==17, pow(base, 0.78))
    # Strict military cyberpunk palette inspired by Pinterest reference (top-left tactical android + Adam Smasher):
    # 1. Matte Carbon / Anthracite Subframe (#15191F)
    make_mat("CarbonSubframe", (0.048, 0.058, 0.075, 1.0), metallic=0.45, roughness=0.60)
    # 2. Dark Gunmetal Primary Armor (#222831)
    make_mat("DarkArmor", (0.105, 0.125, 0.155, 1.0), metallic=0.75, roughness=0.36)
    # 3. Satin Titanium Secondary Armor (#38424F)
    make_mat("SatinTitanium", (0.20, 0.235, 0.285, 1.0), metallic=0.82, roughness=0.30)
    # 4. Machined Chrome / Hydraulic Piston Steel (#7A8696)
    make_mat("PistonChrome", (0.50, 0.55, 0.62, 1.0), metallic=0.95, roughness=0.16)
    # 5. Matte White Ceramic Faceplate & Pauldron Accents (#DCE0E6)
    make_mat("WhiteCeramic", (0.82, 0.85, 0.89, 1.0), metallic=0.10, roughness=0.26)
    # 6. Predatory Crimson AI Core & Optic Sensors (#E62822)
    make_mat("CrimsonCore", (0.96, 0.09, 0.08, 1.0), metallic=0.15, roughness=0.15,
             emission=(0.98, 0.12, 0.08, 1.0), emission_strength=3.6)
    # 7. Deep Obsidian Visor Glass (#080A0F)
    make_mat("VisorObsidian", (0.018, 0.022, 0.032, 1.0), metallic=0.88, roughness=0.10)

def add_beveled_box(name, pos, size, mat_key, rot=(0, 0, 0), bevel=0.010, segments=2, smooth=False):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz(pos))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size)
    obj.rotation_euler = rl_rot(rot)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(MATS[mat_key])
    for poly in obj.data.polygons:
        poly.use_smooth = smooth
    return obj

def add_hull(name, pts_game, mat_key, bevel=0.008, smooth=False):
    bm = bmesh.new()
    for p in pts_game:
        bm.verts.new(xyz(p))
    bm.verts.ensure_lookup_table()
    bmesh.ops.convex_hull(bm, input=list(bm.verts))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        obj.select_set(True)
        mod = obj.modifiers.new("Bevel", 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)
    obj.data.materials.append(MATS[mat_key])
    for poly in obj.data.polygons:
        poly.use_smooth = smooth
    return obj

def add_oriented_hull(name, origin, forward_dir, up_dir, local_pts, mat_key, bevel=0.008, smooth=False):
    f = Vector(forward_dir).normalized()
    u = Vector(up_dir).normalized()
    r = u.cross(f).normalized()
    u = f.cross(r).normalized()
    org = Vector(origin)
    world_pts = [org + r * lx + u * ly + f * lz for lx, ly, lz in local_pts]
    return add_hull(name, world_pts, mat_key, bevel=bevel, smooth=smooth)

def add_rod(name, p0, p1, r0, r1, mat_key, sides=16, smooth=True):
    v0 = xyz(p0)
    v1 = xyz(p1)
    d = v1 - v0
    length = d.length
    if length < 1e-5:
        return None
    mid = (v0 + v1) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=sides, radius1=r0, radius2=r1, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    quat = Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(MATS[mat_key])
    for poly in obj.data.polygons:
        poly.use_smooth = smooth
    return obj

def add_piston(name, p0, p1, r_outer, r_inner, mat_housing="SatinTitanium", mat_rod="PistonChrome"):
    v0 = Vector(p0)
    v1 = Vector(p1)
    mid = v0 * 0.44 + v1 * 0.56
    collar = v0 * 0.38 + v1 * 0.62
    add_rod(name + "_Housing", v0, mid, r_outer, r_outer, mat_housing, sides=14)
    add_rod(name + "_Collar", mid, collar, r_outer * 1.20, r_outer * 1.12, "DarkArmor", sides=14)
    add_rod(name + "_Shaft", mid, v1, r_inner, r_inner, mat_rod, sides=12)

def add_cable_curve(name, pts_game, radius, mat_key="CarbonSubframe", sides=10):
    for i in range(len(pts_game) - 1):
        add_rod(f"{name}_{i}", pts_game[i], pts_game[i + 1], radius, radius, mat_key, sides=sides, smooth=True)

def build_escaped_ai_boss():
    clear_scene()
    init_palette()

    # ==========================================================================
    # 1. HEAD & PREDATORY CERAMIC MASK (Y = 1.90 .. 2.24 m)
    # ==========================================================================
    add_hull("CraniumRearCore", [
        (-0.105, 1.95, -0.09), (0.105, 1.95, -0.09),
        (-0.118, 2.08, -0.10), (0.118, 2.08, -0.10),
        (-0.110, 2.19, -0.05), (0.110, 2.19, -0.05),
        (-0.075, 2.23,  0.04), (0.075, 2.23,  0.04),
        (-0.115, 2.04,  0.08), (0.115, 2.04,  0.08),
        (-0.085, 1.91,  0.06), (0.085, 1.91,  0.06),
    ], "DarkArmor", bevel=0.010, smooth=False)

    add_hull("OpticVisorRecess", [
        (-0.102, 2.02, 0.07), (0.102, 2.02, 0.07),
        (-0.096, 2.11, 0.09), (0.096, 2.11, 0.09),
        (-0.070, 2.10, 0.175), (0.070, 2.10, 0.175),
        (-0.068, 2.02, 0.170), (0.068, 2.02, 0.170),
    ], "VisorObsidian", bevel=0.005, smooth=False)

    # Matte White Ceramic Upper Brow & Forehead Shield
    add_hull("CeramicForeheadMask", [
        (-0.104, 2.10, 0.06), (0.104, 2.10, 0.06),
        (-0.092, 2.21, 0.02), (0.092, 2.21, 0.02),
        ( 0.000, 2.24, 0.07),
        (-0.082, 2.17, 0.155), (0.082, 2.17, 0.155),
        (-0.055, 2.085, 0.188), (0.055, 2.085, 0.188),
        ( 0.000, 2.068, 0.202),
    ], "WhiteCeramic", bevel=0.007, smooth=False)

    # Matte White Ceramic Lower Faceplate / Cheek & Mandible Mask
    for side in (-1, 1):
        s = side
        add_hull(f"CeramicCheekMandible_{s}", [
            (s * 0.004, 2.052, 0.198),
            (s * 0.062, 2.060, 0.182),
            (s * 0.102, 2.040, 0.095),
            (s * 0.092, 1.945, 0.085),
            (s * 0.052, 1.905, 0.165),
            (s * 0.004, 1.898, 0.188),
        ], "WhiteCeramic", bevel=0.005, smooth=False)

    add_beveled_box("ChinVentBlock", (0, 1.895, 0.165), (0.058, 0.032, 0.055), "CarbonSubframe", bevel=0.004)

    # Crimson Tactical Forehead Triangle Insignia
    add_hull("ForeheadCrimsonTriangle", [
        (-0.024, 2.138, 0.180),
        ( 0.024, 2.138, 0.180),
        ( 0.000, 2.102, 0.194),
        (-0.020, 2.135, 0.168),
        ( 0.020, 2.135, 0.168),
        ( 0.000, 2.104, 0.180),
    ], "CrimsonCore", bevel=0.0015, smooth=False)

    for side in (-1, 1):
        add_beveled_box(f"CrimsonOpticEye_{side}", (side * 0.036, 2.068, 0.186),
                        (0.032, 0.010, 0.014), "CrimsonCore", rot=(0, -side * 0.24, -side * 0.18), bevel=0.002)
        add_beveled_box(f"CrimsonSubOptic_{side}", (side * 0.068, 2.074, 0.168),
                        (0.012, 0.008, 0.012), "CrimsonCore", rot=(0, -side * 0.42, 0), bevel=0.0015)
        add_rod(f"EarTurret_{side}", (side * 0.105, 2.065, 0.02), (side * 0.132, 2.065, 0.02),
                0.038, 0.034, "SatinTitanium", sides=18)
        add_rod(f"EarTurretCap_{side}", (side * 0.132, 2.065, 0.02), (side * 0.138, 2.065, 0.02),
                0.024, 0.022, "CarbonSubframe", sides=18)

    # Left temple externally-mounted rangefinder / target-designator pod with glowing red lens
    add_beveled_box("TempleTargetPod", (0.134, 2.045, 0.085), (0.032, 0.034, 0.088), "DarkArmor", bevel=0.005)
    add_rod("TempleTargetLens", (0.134, 2.045, 0.125), (0.134, 2.045, 0.136), 0.011, 0.011, "CrimsonCore", sides=14)

    # ==========================================================================
    # 2. HYDRAULIC NECK COLUMN, BRAIDED CABLES & HIGH ARMORED GORGET COLLAR
    # ==========================================================================
    add_rod("NeckSpineColumn", (0, 1.72, -0.01), (0, 1.94, 0.03), 0.058, 0.048, "CarbonSubframe", sides=16)
    for ring_y in (1.76, 1.81, 1.86):
        add_rod(f"NeckVertebra_{ring_y}", (0, ring_y - 0.012, 0.005), (0, ring_y + 0.012, 0.008),
                0.066, 0.064, "DarkArmor", sides=16)

    for side in (-1, 1):
        add_piston(f"NeckPistonFront_{side}", (side * 0.095, 1.73, 0.08), (side * 0.062, 1.91, 0.10), 0.016, 0.010)
        add_piston(f"NeckPistonRear_{side}", (side * 0.115, 1.74, -0.08), (side * 0.075, 1.93, -0.05), 0.018, 0.011)
        add_cable_curve(f"NeckCableOuter_{side}", [
            (side * 0.115, 2.02, -0.02),
            (side * 0.155, 1.91, -0.05),
            (side * 0.185, 1.78, -0.04),
            (side * 0.170, 1.66,  0.02),
        ], 0.013, "CarbonSubframe")
        add_cable_curve(f"NeckCableInner_{side}", [
            (side * 0.088, 1.98, -0.06),
            (side * 0.125, 1.86, -0.09),
            (side * 0.140, 1.72, -0.08),
        ], 0.011, "DarkArmor")

        add_hull(f"CollarGorget_{side}", [
            (side * 0.085, 1.72,  0.14),
            (side * 0.235, 1.73,  0.08),
            (side * 0.245, 1.84, -0.06),
            (side * 0.110, 1.87, -0.10),
            (side * 0.095, 1.81,  0.11),
        ], "DarkArmor", bevel=0.008)

    # ==========================================================================
    # 3. V-TAPERED ARMORED TORSO, PECTORAL PLATES & CRIMSON TRIANGULAR AI CORE
    # ==========================================================================
    add_hull("ThoraxSubframe", [
        (-0.22, 1.32, -0.14), (0.22, 1.32, -0.14),
        (-0.22, 1.32,  0.15), (0.22, 1.32,  0.15),
        (-0.31, 1.72, -0.17), (0.31, 1.72, -0.17),
        (-0.30, 1.72,  0.19), (0.30, 1.72,  0.19),
    ], "CarbonSubframe", bevel=0.014)

    for side in (-1, 1):
        s = side
        add_hull(f"PectoralArmorMain_{s}", [
            (s * 0.055, 1.38, 0.225),
            (s * 0.255, 1.40, 0.175),
            (s * 0.315, 1.68, 0.165),
            (s * 0.135, 1.75, 0.195),
            (s * 0.065, 1.65, 0.245),
            (s * 0.215, 1.54, 0.235),
        ], "DarkArmor", bevel=0.012)

        add_hull(f"ClaviclePlate_{s}", [
            (s * 0.110, 1.68, 0.215),
            (s * 0.295, 1.66, 0.175),
            (s * 0.320, 1.76, 0.045),
            (s * 0.135, 1.78, 0.095),
        ], "SatinTitanium", bevel=0.008)

        # Diagonal tactical harness strap with chrome latch across upper chest
        add_beveled_box(f"ChestLatch_{s}", (s * 0.195, 1.64, 0.228),
                        (0.038, 0.055, 0.022), "PistonChrome", rot=(0.15, -s * 0.25, -s * 0.30), bevel=0.003)

        for rib_idx, ry in enumerate((1.36, 1.45, 1.54)):
            add_beveled_box(f"RibArmorGill_{s}_{rib_idx}",
                            (s * (0.235 + rib_idx * 0.015), ry, 0.06),
                            (0.065, 0.068, 0.255),
                            "SatinTitanium" if rib_idx == 1 else "DarkArmor",
                            rot=(0.12, s * 0.18, -s * 0.15), bevel=0.007)

    # Central Sternum Frame & Recessed Subdivided Crimson Triangular AI Core
    add_hull("SternumCoreFrame", [
        (-0.105, 1.67, 0.235), (0.105, 1.67, 0.235),
        (-0.042, 1.41, 0.220), (0.042, 1.41, 0.220),
        (-0.115, 1.67, 0.185), (0.115, 1.67, 0.185),
        (-0.055, 1.41, 0.180), (0.055, 1.41, 0.180),
    ], "CarbonSubframe", bevel=0.008)

    add_hull("CrimsonChestCoreTriangle", [
        (-0.076, 1.635, 0.240),
        ( 0.076, 1.635, 0.240),
        ( 0.000, 1.465, 0.232),
        (-0.070, 1.630, 0.210),
        ( 0.070, 1.630, 0.210),
        ( 0.000, 1.470, 0.210),
    ], "CrimsonCore", bevel=0.0025)

    # Inverted dark delta divider inside the crimson triangle (creates the 3-triangle Triforce/Apex cyber-insignia from Pinterest!)
    add_hull("CoreInnerDarkDelta", [
        ( 0.000, 1.636, 0.244),
        (-0.038, 1.552, 0.240),
        ( 0.038, 1.552, 0.240),
        ( 0.000, 1.636, 0.225),
        (-0.038, 1.552, 0.225),
        ( 0.038, 1.552, 0.225),
    ], "CarbonSubframe", bevel=0.0015)

    # Dorsal Back Armor & Exposed Mechanical Spine
    add_hull("UpperBackCarapace", [
        (-0.28, 1.40, -0.15), (0.28, 1.40, -0.15),
        (-0.32, 1.74, -0.16), (0.32, 1.74, -0.16),
        (-0.18, 1.78, -0.25), (0.18, 1.78, -0.25),
        (-0.15, 1.38, -0.24), (0.15, 1.38, -0.24),
    ], "DarkArmor", bevel=0.012)

    for v_idx in range(7):
        vy = 1.12 + v_idx * 0.095
        vz = -0.185 if v_idx < 3 else -0.235
        add_beveled_box(f"SpineVertebraBlock_{v_idx}", (0, vy, vz),
                        (0.095, 0.065, 0.075), "SatinTitanium" if v_idx % 2 == 0 else "DarkArmor",
                        rot=(0.15, 0, 0), bevel=0.007)

    # ==========================================================================
    # 4. V-CHEVRON ARTICULATED ABDOMEN, CENTRAL SPINE PISTON & TACTICAL PELVIS
    # ==========================================================================
    add_rod("AbdomenCoreColumn", (0, 1.00, 0.0), (0, 1.38, 0.01), 0.135, 0.155, "CarbonSubframe", sides=18)

    # Central vertical hydraulic sternum-to-pelvis actuator column
    add_piston("CentralLineaAlbaActuator", (0, 1.05, 0.165), (0, 1.39, 0.185), 0.028, 0.018)

    # Angled Left & Right V-Chevron Abdominal Oblique Armor Plates (replaces flat horizontal bricks!)
    for side in (-1, 1):
        s = side
        for ab_idx, ay in enumerate((1.10, 1.18, 1.26, 1.34)):
            w_out = 0.155 + ab_idx * 0.016
            add_hull(f"AbdominalChevron_{s}_{ab_idx}", [
                (s * 0.032, ay - 0.032, 0.182),
                (s * 0.032, ay + 0.022, 0.186),
                (s * w_out, ay + 0.048, 0.135),
                (s * w_out, ay - 0.008, 0.130),
                (s * 0.032, ay - 0.032, 0.110),
                (s * w_out, ay + 0.048, 0.065),
            ], "DarkArmor" if ab_idx % 2 == 0 else "SatinTitanium", bevel=0.006)

        # Twin exposed diagonal chrome hydraulic waist pistons on left & right flanks
        add_piston(f"WaistHydraulicOuter_{s}",
                   (s * 0.185, 1.02, 0.06), (s * 0.235, 1.36, 0.09), 0.024, 0.015)
        add_piston(f"WaistHydraulicRear_{s}",
                   (s * 0.165, 1.02, -0.09), (s * 0.215, 1.36, -0.12), 0.024, 0.015)

    # Armored Pelvis, Hip Girdle & Codpiece with small crimson triangle accent
    add_hull("PelvisCenterBlock", [
        (-0.16, 0.96, -0.14), (0.16, 0.96, -0.14),
        (-0.14, 1.06,  0.175), (0.14, 1.06,  0.175),
        (-0.075, 0.83, 0.195), (0.075, 0.83, 0.195),
        (-0.095, 0.83,-0.13), (0.095, 0.83,-0.13),
    ], "DarkArmor", bevel=0.012)

    add_hull("PelvisCrimsonAccent", [
        (-0.026, 0.965, 0.196), (0.026, 0.965, 0.196), (0.0, 0.915, 0.199),
        (-0.022, 0.965, 0.178), (0.022, 0.965, 0.178), (0.0, 0.915, 0.178),
    ], "CrimsonCore", bevel=0.0015)

    for side in (-1, 1):
        s = side
        add_hull(f"HipBeltArmorPod_{s}", [
            (s * 0.14, 1.04,  0.14), (s * 0.28, 1.01,  0.11),
            (s * 0.29, 0.89,  0.09), (s * 0.15, 0.88,  0.13),
            (s * 0.14, 1.04, -0.12), (s * 0.28, 1.01, -0.11),
            (s * 0.29, 0.89, -0.10), (s * 0.15, 0.88, -0.12),
        ], "SatinTitanium", bevel=0.009)

    # ==========================================================================
    # 5. MASSIVE ANGULAR PAULDRONS, SCULPTED HYDRAULIC ARMS & CLAWED GAUNTLETS
    # ==========================================================================
    for side in (-1, 1):
        s = side
        sh_p = Vector((s * 0.375, 1.66, 0.00))
        el_p = Vector((s * 0.445, 1.28, 0.05))
        wr_p = Vector((s * 0.390, 0.96, 0.26))

        # 5A. Multi-Layered Angular Pauldron (Shoulder Armor)
        add_hull(f"PauldronUpperShell_{s}", [
            (s * 0.26, 1.74, -0.15), (s * 0.26, 1.74,  0.15),
            (s * 0.35, 1.83, -0.12), (s * 0.35, 1.83,  0.12),
            (s * 0.52, 1.71, -0.16), (s * 0.52, 1.71,  0.16),
            (s * 0.54, 1.56, -0.15), (s * 0.54, 1.56,  0.15),
            (s * 0.36, 1.58, -0.17), (s * 0.36, 1.58,  0.17),
        ], "DarkArmor", bevel=0.012)

        add_hull(f"PauldronLowerSkirt_{s}", [
            (s * 0.38, 1.58, -0.14), (s * 0.38, 1.58,  0.14),
            (s * 0.55, 1.57, -0.13), (s * 0.55, 1.57,  0.13),
            (s * 0.52, 1.43, -0.11), (s * 0.52, 1.43,  0.11),
            (s * 0.38, 1.45, -0.13), (s * 0.38, 1.45,  0.13),
        ], "SatinTitanium", bevel=0.009)

        add_beveled_box(f"PauldronCeramicStripe_{s}", (s * 0.515, 1.65, 0.04),
                        (0.045, 0.085, 0.145), "WhiteCeramic", rot=(0, 0, -s * 0.22), bevel=0.004)
        add_beveled_box(f"PauldronCrimsonPip_{s}", (s * 0.536, 1.68, 0.08),
                        (0.015, 0.022, 0.028), "CrimsonCore", rot=(0, 0, -s * 0.22), bevel=0.002)

        # 5B. Sculpted Bicep Hull & Elbow Hydraulics
        add_rod(f"ShoulderBallActuator_{s}", sh_p - Vector((s * 0.06, 0, 0)), sh_p + Vector((s * 0.05, 0, 0)),
                0.082, 0.082, "CarbonSubframe", sides=18)
        add_rod(f"UpperArmCore_{s}", sh_p, el_p, 0.066, 0.056, "CarbonSubframe", sides=16)

        u_axis = (el_p - sh_p).normalized()
        add_oriented_hull(f"BicepArmorHull_{s}", (sh_p + el_p) * 0.5, u_axis, (0, 0, 1), [
            (-0.065, -0.070, -0.11), ( 0.065, -0.070, -0.11),
            (-0.080,  0.075, -0.09), ( 0.080,  0.075, -0.09),
            (-0.072,  0.082,  0.10), ( 0.072,  0.082,  0.10),
            (-0.055, -0.065,  0.12), ( 0.055, -0.065,  0.12),
        ], "DarkArmor", bevel=0.010)

        add_piston(f"BicepHydraulic_{s}",
                   sh_p + Vector((-s * 0.03, -0.06, 0.068)),
                   el_p + Vector((-s * 0.02,  0.04, 0.068)), 0.018, 0.011)

        add_rod(f"ElbowHingeAxle_{s}", el_p - Vector((0.082, 0, 0)), el_p + Vector((0.082, 0, 0)),
                0.052, 0.052, "SatinTitanium", sides=18)
        add_beveled_box(f"ElbowGuardCowl_{s}", el_p + Vector((s * 0.02, 0, -0.065)),
                        (0.110, 0.115, 0.085), "SatinTitanium", bevel=0.009)

        # 5C. Sculpted Tapered Forearm Gauntlet Hull (replaces loose box-on-cylinder!)
        f_axis = (wr_p - el_p).normalized()
        f_mid = (el_p + wr_p) * 0.5
        add_rod(f"ForearmCore_{s}", el_p, wr_p, 0.058, 0.046, "CarbonSubframe", sides=16)

        add_oriented_hull(f"ForearmGauntletHull_{s}", f_mid, f_axis, (0, 1, 0), [
            # Near elbow (z = -0.14): wider
            (-0.075, -0.070, -0.14), ( 0.075, -0.070, -0.14),
            (-0.082,  0.075, -0.13), ( 0.082,  0.075, -0.13),
            # Near wrist (z = +0.15): sleekly tapered to wrist cuff
            (-0.052, -0.052,  0.15), ( 0.052, -0.052,  0.15),
            (-0.058,  0.056,  0.15), ( 0.058,  0.056,  0.15),
        ], "DarkArmor", bevel=0.010)

        # Outer forearm kinetic shield blade & chrome wrist piston
        add_oriented_hull(f"ForearmKineticBlade_{s}", f_mid + Vector((s * 0.058, 0.01, 0.0)), f_axis, (0, 1, 0), [
            (-0.018, -0.055, -0.12), ( 0.018, -0.045, -0.12),
            (-0.018,  0.060, -0.11), ( 0.022,  0.055, -0.11),
            (-0.016, -0.042,  0.13), ( 0.016, -0.035,  0.13),
            (-0.016,  0.045,  0.13), ( 0.018,  0.042,  0.13),
        ], "SatinTitanium", bevel=0.006)

        add_piston(f"ForearmPiston_{s}",
                   el_p + Vector((s * 0.04, -0.03, -0.05)),
                   wr_p + Vector((s * 0.03,  0.03, -0.04)), 0.016, 0.010)

        # 5D. Articulated 5-Fingered Mechanical Combat Hand aligned with Forearm Axis
        palm_c = wr_p + f_axis * 0.055
        add_oriented_hull(f"HandPalmHull_{s}", palm_c, f_axis, (0, 1, 0), [
            (-0.046, -0.028, -0.045), ( 0.046, -0.028, -0.045),
            (-0.048,  0.032, -0.045), ( 0.048,  0.032, -0.045),
            (-0.044, -0.024,  0.048), ( 0.044, -0.024,  0.048),
            (-0.046,  0.028,  0.048), ( 0.046,  0.028,  0.048),
        ], "DarkArmor", bevel=0.006)

        add_oriented_hull(f"HandDorsalCeramic_{s}", palm_c + Vector((s * 0.022, 0.012, 0)), f_axis, (0, 1, 0), [
            (-0.018, -0.026, -0.036), ( 0.018, -0.026, -0.036),
            (-0.018,  0.026, -0.036), ( 0.018,  0.026, -0.036),
            (-0.016, -0.024,  0.036), ( 0.016, -0.024,  0.036),
            (-0.016,  0.024,  0.036), ( 0.016,  0.024,  0.036),
        ], "WhiteCeramic", bevel=0.004)

        # 4 Armored Claw Fingers + Opposed Thumb
        for f_idx in range(4):
            u_off = (f_idx - 1.5) * 0.022
            k0 = palm_c + f_axis * 0.046 + Vector((s * 0.018, u_off, 0.0))
            k1 = k0 + f_axis * 0.044 + Vector((-s * 0.016, 0.0, 0.010))
            k2 = k1 + f_axis * 0.018 + Vector((-s * 0.036, 0.0, -0.012))
            add_rod(f"FingerProx_{s}_{f_idx}", k0, k1, 0.013, 0.011, "SatinTitanium", sides=10)
            add_rod(f"FingerDist_{s}_{f_idx}", k1, k2, 0.011, 0.007, "DarkArmor", sides=10)

        t0 = palm_c + Vector((-s * 0.018, 0.034, 0.015))
        t1 = t0 + f_axis * 0.042 + Vector((-s * 0.022, -0.010, 0.015))
        t2 = t1 + f_axis * 0.025 + Vector(( s * 0.012, -0.018, 0.0))
        add_rod(f"ThumbProx_{s}", t0, t1, 0.015, 0.012, "SatinTitanium", sides=10)
        add_rod(f"ThumbDist_{s}", t1, t2, 0.012, 0.008, "DarkArmor", sides=10)

    # ==========================================================================
    # 6. SCULPTED HEXAGONAL CYBER-LEGS, KNEE SHIELDS, CALF PISTONS & CLAWED BOOTS
    # ==========================================================================
    for side in (-1, 1):
        s = side
        hip_p   = Vector((s * 0.215, 0.92,  0.00))
        knee_p  = Vector((s * 0.250, 0.50,  0.04))
        ankle_p = Vector((s * 0.265, 0.13, -0.03))

        # 6A. Sculpted Hexagonal Tapered Thigh Armor Hull (cuisse)
        add_rod(f"ThighCore_{s}", hip_p, knee_p, 0.095, 0.080, "CarbonSubframe", sides=18)

        add_hull(f"ThighMainHull_{s}", [
            # Upper thigh near hip (Y = 0.89): wide & swept up on the outer hip
            (s * 0.125, 0.87,  0.125), (s * 0.335, 0.91,  0.110),
            (s * 0.235, 0.90,  0.168),  # front thigh ridge
            (s * 0.135, 0.87, -0.115), (s * 0.345, 0.91, -0.105),
            # Lower thigh near knee (Y = 0.54): tapered cleanly into knee joint
            (s * 0.155, 0.54,  0.115), (s * 0.335, 0.54,  0.105),
            (s * 0.245, 0.53,  0.152),  # lower front ridge
            (s * 0.165, 0.54, -0.095), (s * 0.335, 0.54, -0.095),
        ], "DarkArmor", bevel=0.010)

        # Outer thigh reactive armor plate + White Ceramic tactical stripe
        thigh_mid = (hip_p + knee_p) * 0.5
        add_hull(f"ThighOuterReactive_{s}", [
            (s * 0.315, 0.86,  0.085), (s * 0.368, 0.84,  0.065),
            (s * 0.315, 0.86, -0.085), (s * 0.368, 0.84, -0.065),
            (s * 0.315, 0.58,  0.075), (s * 0.358, 0.59,  0.055),
            (s * 0.315, 0.58, -0.075), (s * 0.358, 0.59, -0.055),
        ], "SatinTitanium", bevel=0.007)

        add_beveled_box(f"ThighCeramicBadge_{s}", thigh_mid + Vector((s * 0.132, 0.02, 0.02)),
                        (0.024, 0.095, 0.085), "WhiteCeramic", rot=(0.08, 0, -s * 0.08), bevel=0.004)

        # Inner thigh hydraulic stabilizer piston
        add_piston(f"ThighInnerPiston_{s}",
                   hip_p + Vector((-s * 0.075, -0.04, 0.07)),
                   knee_p + Vector((-s * 0.070, 0.05, 0.06)), 0.020, 0.012)

        # 6B. Articulated Knee Joint & Angular Patella Shield
        add_rod(f"KneeAxle_{s}", knee_p - Vector((0.105, 0, 0)), knee_p + Vector((0.105, 0, 0)),
                0.068, 0.068, "SatinTitanium", sides=18)
        add_hull(f"KneeShield_{s}", [
            (knee_p.x - 0.072, knee_p.y - 0.085, knee_p.z + 0.055),
            (knee_p.x + 0.072, knee_p.y - 0.085, knee_p.z + 0.055),
            (knee_p.x - 0.082, knee_p.y + 0.085, knee_p.z + 0.065),
            (knee_p.x + 0.082, knee_p.y + 0.085, knee_p.z + 0.065),
            (knee_p.x,         knee_p.y + 0.015, knee_p.z + 0.148),
        ], "DarkArmor", bevel=0.008)

        # 6C. Lower Leg Shin Greave & Twin Rear Calf Hydraulic Shock Absorbers
        add_rod(f"ShinCore_{s}", knee_p, ankle_p, 0.078, 0.062, "CarbonSubframe", sides=18)

        add_hull(f"ShinFrontGreave_{s}", [
            (ankle_p.x - 0.062, 0.15,  0.075), (ankle_p.x + 0.062, 0.15,  0.075),
            (ankle_p.x,         0.14,  0.115),
            (knee_p.x  - 0.078, 0.45,  0.115), (knee_p.x  + 0.078, 0.45,  0.115),
            (knee_p.x,          0.46,  0.155),
            (knee_p.x  - 0.082, 0.45, -0.020), (knee_p.x  + 0.082, 0.45, -0.020),
            (ankle_p.x - 0.066, 0.15, -0.045), (ankle_p.x + 0.066, 0.15, -0.045),
        ], "DarkArmor", bevel=0.009)

        add_hull(f"ShinCrestPlate_{s}", [
            (ankle_p.x - 0.028, 0.17, 0.112), (ankle_p.x + 0.028, 0.17, 0.112),
            (knee_p.x  - 0.035, 0.42, 0.148), (knee_p.x  + 0.035, 0.42, 0.148),
            (ankle_p.x - 0.032, 0.17, 0.075), (ankle_p.x + 0.032, 0.17, 0.075),
            (knee_p.x  - 0.040, 0.42, 0.105), (knee_p.x  + 0.040, 0.42, 0.105),
        ], "SatinTitanium", bevel=0.005)

        for px_off in (-0.036, 0.036):
            add_piston(f"CalfPiston_{s}_{px_off}",
                       knee_p + Vector((px_off, -0.03, -0.070)),
                       ankle_p + Vector((px_off,  0.03, -0.090)), 0.022, 0.014)

        # 6D. Heavy Split-Toe Cybernetic Combat Boot planted solidly at Y = 0.00 m
        fx = ankle_p.x
        add_beveled_box(f"AnkleCollar_{s}", (fx, 0.135, -0.02), (0.140, 0.090, 0.160), "SatinTitanium", bevel=0.009)
        add_hull(f"BootHeelSpur_{s}", [
            (fx - 0.072, 0.00, -0.155), (fx + 0.072, 0.00, -0.155),
            (fx - 0.080, 0.00, -0.020), (fx + 0.080, 0.00, -0.020),
            (fx - 0.068, 0.10, -0.020), (fx + 0.068, 0.10, -0.020),
            (fx - 0.060, 0.08, -0.140), (fx + 0.060, 0.08, -0.140),
        ], "DarkArmor", bevel=0.008)

        for toe_side in (-1, 1):
            tx = fx + toe_side * 0.045
            add_hull(f"BootToeClaw_{s}_{toe_side}", [
                (tx - 0.036, 0.00, -0.02), (tx + 0.036, 0.00, -0.02),
                (tx - 0.030, 0.00,  0.185), (tx + 0.030, 0.00,  0.185),
                (tx - 0.026, 0.048, 0.195), (tx + 0.026, 0.048, 0.195),
                (tx - 0.034, 0.095, 0.01), (tx + 0.034, 0.095, 0.01),
            ], "DarkArmor", bevel=0.007)

        add_beveled_box(f"BootInstepGuard_{s}", (fx, 0.088, 0.065),
                        (0.130, 0.055, 0.140), "SatinTitanium", rot=(0.32, 0, 0), bevel=0.008)

    # Join all parts into one clean mesh `escaped_ai_boss`
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    bpy.ops.object.join()
    boss = bpy.context.active_object
    boss.name = boss.data.name = "escaped_ai_boss"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    bm = bmesh.new()
    bm.from_mesh(boss.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(boss.data)
    bm.free()
    boss.data.update()

    os.makedirs(OUT_MODELS, exist_ok=True)
    out_glb = OUT_MODELS / "escaped_ai_boss.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_glb),
        export_format="GLB",
        export_normals=True,
        export_texcoords=False,
        export_animations=False,
        export_cameras=False,
        export_lights=False
    )
    pts = [boss.matrix_world @ v.co for v in boss.data.vertices]
    print("Exported escaped_ai_boss.glb:", out_glb, "Size:", os.path.getsize(out_glb), "Polys:", len(boss.data.polygons))
    print("BBOX X:", min(p.x for p in pts), max(p.x for p in pts))
    print("BBOX Y:", min(p.y for p in pts), max(p.y for p in pts))
    print("BBOX Z:", min(p.z for p in pts), max(p.z for p in pts))

    # Render a 3/4 front inspection preview PNG
    scene = bpy.context.scene
    scene.render.engine = 'BLENDER_EEVEE'
    scene.render.resolution_x = 768
    scene.render.resolution_y = 960
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.08, 0.10, 0.13, 1.0)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 52
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam_obj)
    cam_obj.location = (1.35, -3.15, 1.32)
    target = bpy.data.objects.new("Target", None)
    scene.collection.objects.link(target)
    target.location = (0.0, 0.0, 1.18)
    track = cam_obj.constraints.new(type='TRACK_TO')
    track.target = target
    track.track_axis = 'TRACK_NEGATIVE_Z'
    track.up_axis = 'UP_Y'
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("Key", (2.5, -3.0, 3.2), 4.5, (1.0, 0.97, 0.92)),
        ("Fill", (-2.8, -2.2, 2.0), 2.6, (0.75, 0.88, 1.0)),
        ("Rim", (0.0, 3.0, 2.8), 3.8, (0.95, 0.45, 0.40)),
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

    scene.render.filepath = str(PREVIEW_PNG)
    bpy.ops.render.render(write_still=True)
    print("Rendered preview:", PREVIEW_PNG)

if __name__ == "__main__":
    build_escaped_ai_boss()
