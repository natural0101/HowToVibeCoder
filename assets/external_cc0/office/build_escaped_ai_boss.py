"""High-poly smooth-shaded Titanium & Ceramic Android Boss builder for assets/models/escaped_ai_boss.glb.
Preserves exact bounding box, origin (Y=0 at soles), and forward orientation (+Z in glTF / -Y in Blender):
  LO ≈ [-0.5486, -0.3638, 0.0000]
  HI ≈ [ 0.5486,  0.2759, 2.2365]
Features:
  - Cold, featureless/blind sculpted matte-white porcelain face mask with subtle recessed obsidian seam lines (zero cartoon eyes)
  - Deeply recessed glowing triangular crimson chest core with internal delta lattice & chrome corner lugs (FP16 HDR Bloom ready)
  - Sleek dark-titanium & matte gunmetal corporate security exo-frame with broad angular pauldrons, dorsal spine vertebrae,
    articulated hydraulic elbows/knees, and 5-fingered mechanical gauntlets
  - Embedded 512x512 BossDetailAtlas texture + 24-ray hemisphere BVHTree vertex AO (COLOR_0) + weighted smooth normals
"""
import json
import math
import os
import struct
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
PREVIEW_PNG = ROOT / "assets/external_cc0/office/boss_preview_r7.png"
ARTIFACT_PROOF_1 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\escaped_ai_boss_v2_proof.png")
ARTIFACT_PROOF_2 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\34a5dbd6-7dba-4b8d-8112-53de935b5112\scratch\escaped_ai_boss_v2_proof.png")
SCREENSHOT_PROOF = ROOT / "screenshots/escaped_ai_boss_v2_proof.png"

MAT_ORDER = [
    "DarkArmor",
    "VisorObsidian",
    "WhiteCeramic",
    "CarbonSubframe",
    "CrimsonCore",
    "SatinTitanium",
    "PistonChrome",
]

MAT_SPECS = {
    # key: (linear_rgba, metallic, roughness, emissive_rgb)
    "DarkArmor":      ((0.036, 0.044, 0.058, 1.0), 0.78, 0.36, None),
    "VisorObsidian":  ((0.008, 0.010, 0.015, 1.0), 0.88, 0.14, None),
    "WhiteCeramic":   ((0.840, 0.855, 0.885, 1.0), 0.05, 0.22, None),
    "CarbonSubframe": ((0.015, 0.018, 0.024, 1.0), 0.45, 0.58, None),
    "CrimsonCore":    ((0.920, 0.015, 0.015, 1.0), 0.00, 0.55, (1.00, 0.05, 0.03)),
    "SatinTitanium":  ((0.095, 0.114, 0.142, 1.0), 0.84, 0.28, None),
    "PistonChrome":   ((0.460, 0.510, 0.580, 1.0), 0.95, 0.16, None),
}

MATS = {}


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# Game coordinates: X right, Y up (height 0..2.2365m), Z forward (+Z faces player)
# Blender glTF exporter maps (+X -> +X, +Z -> +Y, -Y -> +Z)
def xyz(p):
    return Vector((p[0], -p[2], p[1]))


def rl_scale(s):
    return Vector((s[0], s[2], s[1]))


def rl_rot(r):
    return Euler((r[0], -r[2], r[1]), "XYZ")


def build_boss_detail_atlas():
    img = bpy.data.images.get("BossDetailAtlas")
    if img is not None:
        return img
    W, H = 512, 512
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(20261120)
    n1 = rng.uniform(-0.5, 0.5, size=(H, W)).astype(np.float32)
    n2_s = rng.uniform(-0.5, 0.5, size=(H // 2, W // 2)).astype(np.float32)
    n2 = np.repeat(np.repeat(n2_s, 2, axis=0), 2, axis=1)
    y_idx, x_idx = np.mgrid[0:H, 0:W].astype(np.float32)

    # Quadrant 0 (x<256, y<256): Brushed Dark Titanium & Satin Titanium micro-grain
    q0 = (x_idx < 256) & (y_idx < 256)
    streak = 0.5 * (n1 + np.roll(n1, 1, axis=1))
    val0 = 0.988 + 0.008 * streak[q0] + 0.004 * n2[q0]
    for c in range(3):
        arr[q0, c] = np.clip(val0, 0.975, 1.0)

    # Quadrant 1 (x>=256, y<256): Matte Carbon Subframe & Obsidian micro-weave
    q1 = (x_idx >= 256) & (y_idx < 256)
    val1 = 0.986 + 0.009 * n1[q1] + 0.005 * n2[q1]
    for c in range(3):
        arr[q1, c] = np.clip(val1, 0.972, 1.0)

    # Quadrant 2 (x<256, y>=256): Smooth White Porcelain Ceramic & Machined Chrome
    q2 = (x_idx < 256) & (y_idx >= 256)
    val2 = 0.994 + 0.004 * n1[q2] + 0.002 * n2[q2]
    for c in range(3):
        arr[q2, c] = np.clip(val2, 0.986, 1.0)

    # Quadrant 3 (x>=256, y>=256): High-intensity Crimson Core Plasma Cell
    q3 = (x_idx >= 256) & (y_idx >= 256)
    val3 = 0.995 + 0.004 * n1[q3]
    for c in range(3):
        arr[q3, c] = np.clip(val3, 0.988, 1.0)

    img = bpy.data.images.new("BossDetailAtlas", width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


def init_palette():
    MATS.clear()
    atlas = build_boss_detail_atlas()
    for key in MAT_ORDER:
        rgba, met, rough, em = MAT_SPECS[key]
        m = bpy.data.materials.new(name=key)
        m.use_nodes = True
        m.diffuse_color = rgba
        tree = m.node_tree
        bsdf = tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = met
        bsdf.inputs["Roughness"].default_value = rough
        if em is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*em, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 4.2
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = atlas
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        MATS[key] = m


def smooth_obj(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_loft_rings(name, rings_game, mat_key, closed_u=True, cap_ends=True):
    """Create a smooth lofted mesh from a list of rings in game coordinates (x, y, z)."""
    n_rings = len(rings_game)
    n_seg = len(rings_game[0])
    verts = [xyz(p) for ring in rings_game for p in ring]
    faces = []
    u_lim = n_seg if closed_u else (n_seg - 1)
    for r in range(n_rings - 1):
        for s in range(u_lim):
            ns = (s + 1) % n_seg
            a = r * n_seg + s
            b = r * n_seg + ns
            c = (r + 1) * n_seg + ns
            d = (r + 1) * n_seg + s
            faces.append((a, b, c, d))
    if closed_u and cap_ends:
        faces.append(tuple(reversed(range(n_seg))))
        base = (n_rings - 1) * n_seg
        faces.append(tuple(range(base, base + n_seg)))
    m = bpy.data.meshes.new(name)
    m.from_pydata(verts, [], faces)
    m.update()
    obj = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATS[mat_key])
    return smooth_obj(obj)


def add_oriented_loft(name, p0, p1, up_hint, station_specs, mat_key, n_seg=28):
    """Loft a smooth contoured limb armor shell along the axis p0 -> p1.
    station_specs: list of (t, rx, ry, ox, oy, superellipse_p) where x is lateral, y is along up_hint."""
    v0 = Vector(p0)
    v1 = Vector(p1)
    axis = (v1 - v0).normalized()
    up = Vector(up_hint).normalized()
    right = up.cross(axis).normalized()
    up = axis.cross(right).normalized()
    length = (v1 - v0).length

    rings = []
    for row in station_specs:
        t, rx, ry = row[0], row[1], row[2]
        ox = row[3] if len(row) > 3 else 0.0
        oy = row[4] if len(row) > 4 else 0.0
        power = row[5] if len(row) > 5 else 2.0
        center = v0 + axis * (t * length) + right * ox + up * oy
        ring = []
        for i in range(n_seg):
            a = i * math.tau / n_seg
            ca, sa = math.cos(a), math.sin(a)
            if power != 2.0:
                denom = (abs(ca) ** power + abs(sa) ** power) ** (1.0 / power)
                ca, sa = ca / max(1e-5, denom), sa / max(1e-5, denom)
            pt = center + right * (rx * ca) + up * (ry * sa)
            ring.append((pt.x, pt.y, pt.z))
        rings.append(ring)
    return add_loft_rings(name, rings, mat_key, closed_u=True, cap_ends=True)


def add_beveled_box(name, pos, size, mat_key, rot=(0, 0, 0), bevel=0.012, segments=3):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz(pos))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size)
    obj.rotation_euler = rl_rot(rot)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(MATS[mat_key])
    return smooth_obj(obj)


def add_hull(name, pts_game, mat_key, bevel=0.010, segments=3):
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
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)
    obj.data.materials.append(MATS[mat_key])
    return smooth_obj(obj)


def add_oriented_hull(name, origin, forward_dir, up_dir, local_pts, mat_key, bevel=0.010, segments=3):
    f = Vector(forward_dir).normalized()
    u = Vector(up_dir).normalized()
    r = u.cross(f).normalized()
    u = f.cross(r).normalized()
    org = Vector(origin)
    world_pts = [org + r * lx + u * ly + f * lz for lx, ly, lz in local_pts]
    return add_hull(name, world_pts, mat_key, bevel=bevel, segments=segments)


def add_rod(name, p0, p1, r0, r1, mat_key, sides=24):
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
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(MATS[mat_key])
    return smooth_obj(obj)


def add_piston(name, p0, p1, r_outer, r_inner, mat_housing="SatinTitanium", mat_rod="PistonChrome"):
    v0 = Vector(p0)
    v1 = Vector(p1)
    c0 = v0 + (v1 - v0) * 0.08
    mid = v0 * 0.44 + v1 * 0.56
    collar = v0 * 0.37 + v1 * 0.63
    c1 = v0 + (v1 - v0) * 0.92
    add_rod(name + "_MountA", v0, c0, r_outer * 1.15, r_outer * 1.15, "DarkArmor", sides=20)
    add_rod(name + "_Housing", c0, mid, r_outer, r_outer, mat_housing, sides=24)
    add_rod(name + "_Collar", mid, collar, r_outer * 1.22, r_outer * 1.14, "DarkArmor", sides=24)
    add_rod(name + "_Shaft", mid, c1, r_inner, r_inner, mat_rod, sides=20)
    add_rod(name + "_MountB", c1, v1, r_outer * 1.05, r_outer * 1.05, "DarkArmor", sides=20)


def add_poly_seam(name, pts_game, radius, mat_key="VisorObsidian", bevel_res=5):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_res
    spline = curve.splines.new("POLY")
    spline.points.add(len(pts_game) - 1)
    for sp, pt in zip(spline.points, pts_game):
        v_bl = xyz(pt)
        sp.co = (v_bl.x, v_bl.y, v_bl.z, 1.0)
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATS[mat_key])
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    return smooth_obj(obj)


def add_smooth_cable(name, pts_game, radius, mat_key="CarbonSubframe", res=12, bevel_res=6):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_res
    curve.resolution_u = res
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(pts_game) - 1)
    for bp, pt in zip(spline.bezier_points, pts_game):
        bp.co = xyz(pt)
        bp.handle_left_type = bp.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(MATS[mat_key])
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.convert(target="MESH")
    return smooth_obj(obj)


def build_porcelain_face_mask_and_head():
    """1. High-poly sculpted Titanium cranium + Smooth Matte White Porcelain Face Mask with subtle recessed seam lines.
    NO cheesy glowing cartoon eyes — unsettling cold porcelain stillness."""
    # 1A. Sculpted Dark Titanium Cranium Shell (Y = 1.89 .. 2.236 m)
    cranium_specs = [
        # (y, rx, rz, cz)
        (1.895, 0.072, 0.074, 0.020),
        (1.940, 0.094, 0.098, 0.015),
        (2.010, 0.110, 0.118, 0.012),
        (2.075, 0.114, 0.124, 0.015),
        (2.135, 0.108, 0.118, 0.018),
        (2.185, 0.092, 0.102, 0.022),
        (2.218, 0.064, 0.072, 0.028),
        (2.235, 0.025, 0.028, 0.032),
    ]
    n_c = 36
    c_rings = []
    for y, rx, rz, cz in cranium_specs:
        ring = []
        for i in range(n_c):
            a = i * math.tau / n_c
            # Subtle temporal ridge & occipital plate contour
            ridge = 1.0 + 0.03 * math.cos(a * 4.0) * (1.0 if math.cos(a) < 0 else 0.3)
            x = rx * math.sin(a) * ridge
            z = cz + rz * math.cos(a) * ridge
            ring.append((x, y, z))
        c_rings.append(ring)
    add_loft_rings("TitaniumCraniumShell", c_rings, "DarkArmor", closed_u=True, cap_ends=True)

    # Dark obsidian recessed sub-mask liner (sits 26mm behind the porcelain mask so it never pokes through orbitals or cheeks!)
    liner_specs = [
        (1.895, 0.042, 0.055, 0.080),
        (1.940, 0.068, 0.070, 0.072),
        (2.010, 0.084, 0.078, 0.070),
        (2.075, 0.086, 0.080, 0.072),
        (2.145, 0.078, 0.075, 0.068),
        (2.200, 0.058, 0.058, 0.060),
    ]
    l_rings = []
    for y, rx, rz, cz in liner_specs:
        l_rings.append([(rx * math.sin(i * math.tau / 32), y, cz + rz * math.cos(i * math.tau / 32)) for i in range(32)])
    add_loft_rings("ObsidianMaskLiner", l_rings, "VisorObsidian", closed_u=True, cap_ends=True)

    # 1B. High-Poly Sculpted Matte White Porcelain Face Mask (65 cols x 54 rows, smooth anatomical stillness + razor-sharp recessed seam lines)
    def porcelain_surface(u, v):
        """u in [-1..1] left-to-right across face, v in [0..1] chin (0) to forehead crown (1)."""
        y = 1.890 + v * 0.332  # 1.890 .. 2.222
        chin_taper = 0.46 + 0.54 * (math.sin(min(1.0, v / 0.52) * math.pi * 0.5) ** 0.78)
        crown_taper = 1.0 - 0.34 * (max(0.0, (v - 0.65) / 0.35) ** 1.6)
        half_w = 0.104 * chin_taper * crown_taper

        ang = u * math.pi * 0.46
        x = half_w * math.sin(ang)
        base_z = 0.084 + 0.102 * math.cos(ang) * (0.86 + 0.14 * math.sin(v * math.pi))

        # 1) Nose bridge & subtle nasal ridge (v in 0.28..0.64, u near 0)
        nose_v = math.exp(-((v - 0.44) / 0.14) ** 2) * (1.0 - 0.45 * max(0.0, (0.30 - v) / 0.10))
        nose_u = math.exp(-((u / 0.18) ** 2))
        nose_tip = 0.0085 * math.exp(-((v - 0.34) / 0.055) ** 2) * math.exp(-((u / 0.13) ** 2))
        dz_nose = (0.0175 * nose_v * nose_u) + nose_tip

        # 2) Calm closed-eye orbital depressions (cold blind porcelain android stillness — ZERO glowing cartoon eyes!)
        eye_u = math.exp(-(((abs(u) - 0.38) / 0.21) ** 2))
        eye_v = math.exp(-(((v - 0.545) / 0.072) ** 2))
        lid_swell = 0.0040 * math.exp(-(((abs(u) - 0.37) / 0.14) ** 2)) * math.exp(-(((v - 0.540) / 0.028) ** 2))
        dz_orbit = -0.0085 * eye_u * eye_v + lid_swell

        # 3) Supraorbital brow ridge (v around 0.63)
        brow_v = math.exp(-(((v - 0.635) / 0.055) ** 2))
        brow_u = math.exp(-((u / 0.72) ** 4))
        dz_brow = 0.0085 * brow_v * brow_u

        # 4) Sculpted cheekbones & subtle lower cheek contour
        cheek_arch = 0.0075 * math.exp(-(((abs(u) - 0.56) / 0.24) ** 2)) * math.exp(-(((v - 0.44) / 0.09) ** 2))
        cheek_hollow = -0.0035 * math.exp(-(((abs(u) - 0.46) / 0.20) ** 2)) * math.exp(-(((v - 0.26) / 0.09) ** 2))

        # 5) Calm, sealed porcelain lips & chin contour
        lips = 0.0055 * math.exp(-((u / 0.26) ** 2)) * math.exp(-(((v - 0.21) / 0.045) ** 2))
        lip_part = -0.0022 * math.exp(-((u / 0.24) ** 2)) * math.exp(-(((v - 0.21) / 0.009) ** 2))
        chin_pad = 0.0065 * math.exp(-((u / 0.28) ** 2)) * math.exp(-(((v - 0.08) / 0.055) ** 2))

        # 6) Subtle recessed architectural seam lines in the porcelain mask
        seam_ocular = math.exp(-(((v - (0.540 - 0.015 * (u ** 2))) / 0.0085) ** 2)) * (1.0 - math.exp(-((abs(u) / 0.14) ** 4))) * math.exp(-((u / 0.88) ** 6))
        u_cheek_seam = 0.35 + 0.27 * v
        seam_cheek = math.exp(-(((abs(u) - u_cheek_seam) / 0.014) ** 2)) * (1.0 - 0.6 * max(0.0, (v - 0.70) / 0.30))
        seam_crown = math.exp(-((u / 0.013) ** 2)) * max(0.0, min(1.0, (v - 0.64) / 0.08))
        seam_val = max(seam_ocular, 0.88 * seam_cheek, 0.80 * seam_crown)
        seam_depth = -0.0036 * seam_val

        z = base_z + dz_nose + dz_orbit + dz_brow + cheek_arch + cheek_hollow + lips + lip_part + chin_pad + seam_depth
        return Vector((x, y, z)), seam_val > 0.58

    n_u, n_v = 65, 54
    mask_verts = []
    mask_seams = []
    # Front porcelain surface + inner return rim for thickness
    for j in range(n_v):
        v = j / (n_v - 1)
        for i in range(n_u):
            u = -1.0 + 2.0 * i / (n_u - 1)
            pt, is_seam = porcelain_surface(u, v)
            mask_verts.append(xyz(pt))
            mask_seams.append(is_seam)

    # Back rim vertices inset by 12mm in -Z so the mask has thick sculpted porcelain edges
    for j in range(n_v):
        v = j / (n_v - 1)
        for i in range(n_u):
            u = -1.0 + 2.0 * i / (n_u - 1)
            pt, _ = porcelain_surface(u, v)
            pt_back = Vector((pt.x * 0.96, pt.y, pt.z - 0.014))
            mask_verts.append(xyz(pt_back))

    mask_faces = []
    for j in range(n_v - 1):
        for i in range(n_u - 1):
            a = j * n_u + i
            b = j * n_u + (i + 1)
            c = (j + 1) * n_u + (i + 1)
            d = (j + 1) * n_u + i
            mask_faces.append((a, b, c, d))

    # Stitch outer perimeter border between front surface and back inset surface
    back_offset = n_u * n_v
    for i in range(n_u - 1):
        a, b = i, i + 1
        mask_faces.append((b, a, back_offset + a, back_offset + b))
    top_row = (n_v - 1) * n_u
    for i in range(n_u - 1):
        a, b = top_row + i, top_row + i + 1
        mask_faces.append((a, b, back_offset + b, back_offset + a))
    for j in range(n_v - 1):
        a, b = j * n_u, (j + 1) * n_u
        mask_faces.append((a, b, back_offset + b, back_offset + a))
    for j in range(n_v - 1):
        a, b = j * n_u + (n_u - 1), (j + 1) * n_u + (n_u - 1)
        mask_faces.append((b, a, back_offset + a, back_offset + b))

    m_mask = bpy.data.meshes.new("PorcelainFaceMask")
    m_mask.from_pydata(mask_verts, [], mask_faces)
    m_mask.update()
    o_mask = bpy.data.objects.new("PorcelainFaceMask", m_mask)
    bpy.context.collection.objects.link(o_mask)
    o_mask.data.materials.append(MATS["WhiteCeramic"])
    for poly in o_mask.data.polygons:
        poly.use_smooth = True

    # Continuous high-density recessed obsidian seam inlays nestled flush inside the sculpted porcelain seam grooves (100% unbroken!)
    for side in (-1, 1):
        # 1) Recessed horizontal ocular seam line (blind porcelain slit where eyes would be — zero glowing cartoon eyes!)
        ocular_pts = []
        for k in range(36):
            u_s = side * (0.14 + 0.70 * (k / 35.0))
            v_s = 0.540 - 0.015 * (u_s ** 2)
            pt, _ = porcelain_surface(u_s, v_s)
            n_out = Vector((pt.x, 0.0, pt.z - 0.075)).normalized()
            p_seam = pt + n_out * 0.0030
            ocular_pts.append((p_seam.x, p_seam.y, p_seam.z))
        add_poly_seam(f"PorcelainOcularSeam_{side}", ocular_pts, 0.0011, "VisorObsidian", bevel_res=5)

        # 2) Recessed vertical cheek/mandible panel seam line
        cheek_pts = []
        for k in range(40):
            v_s = 0.06 + 0.70 * (k / 39.0)
            u_s = side * (0.35 + 0.27 * v_s)
            pt, _ = porcelain_surface(u_s, v_s)
            n_out = Vector((pt.x, 0.0, pt.z - 0.075)).normalized()
            p_seam = pt + n_out * 0.0032
            cheek_pts.append((p_seam.x, p_seam.y, p_seam.z))
        add_poly_seam(f"PorcelainCheekSeam_{side}", cheek_pts, 0.00095, "VisorObsidian", bevel_res=5)

    # 3) Recessed central forehead crown seam line
    crown_pts = []
    for k in range(24):
        v_s = 0.62 + 0.34 * (k / 23.0)
        pt, _ = porcelain_surface(0.0, v_s)
        n_out = Vector((pt.x, 0.0, pt.z - 0.075)).normalized()
        p_seam = pt + n_out * 0.0024
        crown_pts.append((p_seam.x, p_seam.y, p_seam.z))
    add_poly_seam("PorcelainCrownSeam", crown_pts, 0.0009, "VisorObsidian", bevel_res=5)

    # 1C. Precision Machined Ear-Turret Assemblies & Cranial Crown Ribs (zero glowing cartoon head lights!)
    for side in (-1, 1):
        add_rod(f"EarTurretBase_{side}", (side * 0.102, 2.060, 0.032), (side * 0.128, 2.060, 0.032),
                0.038, 0.035, "SatinTitanium", sides=28)
        add_rod(f"EarTurretRing_{side}", (side * 0.128, 2.060, 0.032), (side * 0.135, 2.060, 0.032),
                0.028, 0.026, "PistonChrome", sides=28)
        add_rod(f"EarTurretCore_{side}", (side * 0.135, 2.060, 0.032), (side * 0.139, 2.060, 0.032),
                0.018, 0.016, "CarbonSubframe", sides=24)
        # Sleek titanium temple locking clasp anchoring the porcelain mask
        add_beveled_box(f"TempleMaskClasp_{side}", (side * 0.104, 2.092, 0.095),
                        (0.024, 0.028, 0.068), "SatinTitanium", rot=(0.12, -side * 0.18, 0), bevel=0.004, segments=3)


def build_hydraulic_neck_and_collar():
    """2. Multi-vertebra hydraulic neck column, smooth braided hoses, and high armored gorget collar."""
    add_rod("NeckSpineColumn", (0, 1.72, -0.008), (0, 1.93, 0.025), 0.056, 0.046, "CarbonSubframe", sides=24)
    for ring_y in (1.755, 1.800, 1.845, 1.885):
        add_rod(f"NeckVertebra_{ring_y}", (0, ring_y - 0.011, 0.006), (0, ring_y + 0.011, 0.009),
                0.065, 0.062, "DarkArmor", sides=24)

    for side in (-1, 1):
        add_piston(f"NeckPistonFront_{side}", (side * 0.092, 1.73, 0.082), (side * 0.058, 1.905, 0.098), 0.016, 0.010)
        add_piston(f"NeckPistonRear_{side}", (side * 0.112, 1.74, -0.075), (side * 0.072, 1.925, -0.045), 0.018, 0.011)
        add_smooth_cable(f"NeckCableOuter_{side}", [
            (side * 0.112, 2.02, 0.00),
            (side * 0.152, 1.91, -0.04),
            (side * 0.182, 1.78, -0.03),
            (side * 0.165, 1.66, 0.02),
        ], 0.0125, "CarbonSubframe")
        add_smooth_cable(f"NeckCableInner_{side}", [
            (side * 0.086, 1.98, -0.05),
            (side * 0.122, 1.86, -0.08),
            (side * 0.138, 1.72, -0.07),
        ], 0.0105, "SatinTitanium")

        add_hull(f"CollarGorget_{side}", [
            (side * 0.082, 1.72,  0.145),
            (side * 0.238, 1.73,  0.085),
            (side * 0.248, 1.84, -0.055),
            (side * 0.108, 1.87, -0.095),
            (side * 0.092, 1.81,  0.115),
        ], "DarkArmor", bevel=0.012, segments=3)


def build_torso_and_recessed_crimson_core():
    """3. V-tapered dark-titanium corporate security torso with deeply recessed glowing triangular crimson chest core."""
    # Sculpted Thorax Subframe Loft
    thorax_specs = [
        (1.30, 0.215, 0.142, 0.005),
        (1.42, 0.255, 0.165, 0.012),
        (1.56, 0.295, 0.182, 0.018),
        (1.70, 0.312, 0.184, 0.012),
        (1.76, 0.245, 0.145, 0.000),
    ]
    t_rings = []
    for y, rx, rz, cz in thorax_specs:
        t_rings.append([(rx * math.sin(i * math.tau / 32), y, cz + rz * math.cos(i * math.tau / 32)) for i in range(32)])
    add_loft_rings("ThoraxSubframe", t_rings, "CarbonSubframe", closed_u=True, cap_ends=True)

    # Left & Right Sculpted Pectoral Armor Plates framing the central recessed triangular reactor cavity
    for side in (-1, 1):
        s = side
        add_hull(f"PectoralArmorMain_{s}", [
            (s * 0.092, 1.675, 0.236),  # upper inner corner next to triangle top vertex
            (s * 0.022, 1.435, 0.228),  # lower inner corner next to triangle bottom apex
            (s * 0.235, 1.390, 0.182),
            (s * 0.318, 1.675, 0.168),
            (s * 0.145, 1.755, 0.195),
            (s * 0.215, 1.545, 0.242),
            (s * 0.085, 1.675, 0.185),
            (s * 0.022, 1.435, 0.180),
        ], "DarkArmor", bevel=0.014, segments=3)

        add_hull(f"ClaviclePlate_{s}", [
            (s * 0.105, 1.685, 0.218),
            (s * 0.298, 1.665, 0.175),
            (s * 0.322, 1.765, 0.045),
            (s * 0.132, 1.785, 0.095),
        ], "SatinTitanium", bevel=0.010, segments=3)

        for rib_idx, ry in enumerate((1.355, 1.445, 1.535)):
            add_beveled_box(f"RibArmorGill_{s}_{rib_idx}",
                            (s * (0.238 + rib_idx * 0.016), ry, 0.062),
                            (0.068, 0.066, 0.258),
                            "SatinTitanium" if rib_idx == 1 else "DarkArmor",
                            rot=(0.12, s * 0.18, -s * 0.15), bevel=0.009, segments=3)

    # ==========================================================================
    # RECESSED GLOWING TRIANGULAR CRIMSON CHEST CORE (Y = 1.43 .. 1.67 m)
    # ==========================================================================
    # 1) Outer beveled triangular bezel frame (3 thick sculpted SatinTitanium walls forming the recessed cavity)
    tri_outer = [(-0.096, 1.662), (0.096, 1.662), (0.000, 1.442)]
    tri_inner = [(-0.068, 1.640), (0.068, 1.640), (0.000, 1.482)]
    for e_i in range(3):
        ne = (e_i + 1) % 3
        ox0, oy0 = tri_outer[e_i]
        ox1, oy1 = tri_outer[ne]
        ix0, iy0 = tri_inner[e_i]
        ix1, iy1 = tri_inner[ne]
        add_hull(f"CoreBezelWall_{e_i}", [
            (ox0, oy0, 0.244), (ox1, oy1, 0.244),
            (ix0, iy0, 0.236), (ix1, iy1, 0.236),
            (ox0, oy0, 0.195), (ox1, oy1, 0.195),
            (ix0, iy0, 0.195), (ix1, iy1, 0.195),
        ], "SatinTitanium", bevel=0.004, segments=3)

        # Stepped inner carbon recess wall leading down to the glowing crimson core
        add_hull(f"CoreRecessStep_{e_i}", [
            (ix0, iy0, 0.234), (ix1, iy1, 0.234),
            (ix0 * 0.86, iy0 * 0.86 + 1.587 * 0.14, 0.214),
            (ix1 * 0.86, iy1 * 0.86 + 1.587 * 0.14, 0.214),
            (ix0, iy0, 0.198), (ix1, iy1, 0.198),
        ], "CarbonSubframe", bevel=0.002, segments=2)

    # 3 Machined Chrome Corner Clamps on the triangular core vertices
    for c_idx, (cx, cy) in enumerate(tri_outer):
        add_rod(f"CoreCornerLug_{c_idx}", (cx * 0.92, cy * 0.98 + 1.587 * 0.02, 0.228),
                (cx * 0.92, cy * 0.98 + 1.587 * 0.02, 0.247), 0.011, 0.009, "PistonChrome", sides=18)

    # 2) Deeply Recessed Glowing Triangular Crimson Core Plate + faceted crimson crystal prism inside the cavity
    add_hull("RecessedCrimsonCorePrism", [
        (-0.066, 1.638, 0.224),
        ( 0.066, 1.638, 0.224),
        ( 0.000, 1.484, 0.220),
        ( 0.000, 1.585, 0.231),  # slightly domed triangular crystal apex inside the recess
        (-0.066, 1.638, 0.205),
        ( 0.066, 1.638, 0.205),
        ( 0.000, 1.484, 0.205),
    ], "CrimsonCore", bevel=0.002, segments=2)

    # Precision dark-titanium Y-lattice retention ribs recessed across the glowing crimson core
    core_center = (0.0, 1.582, 0.229)
    for r_idx, (vx, vy) in enumerate(tri_inner):
        add_rod(f"CoreRetentionStrut_{r_idx}", core_center, (vx * 0.92, vy * 0.98 + 1.582 * 0.02, 0.227),
                0.0042, 0.0038, "VisorObsidian", sides=12)
    add_rod("CoreCenterNode", (0.0, 1.582, 0.224), (0.0, 1.582, 0.233), 0.011, 0.009, "VisorObsidian", sides=18)

    # Dorsal Back Carapace, Dual Heat-Sink Vents & Articulated Mechanical Spine
    add_hull("UpperBackCarapace", [
        (-0.28, 1.40, -0.15), (0.28, 1.40, -0.15),
        (-0.32, 1.74, -0.16), (0.32, 1.74, -0.16),
        (-0.18, 1.78, -0.25), (0.18, 1.78, -0.25),
        (-0.15, 1.38, -0.24), (0.15, 1.38, -0.24),
    ], "DarkArmor", bevel=0.015, segments=3)

    for side in (-1, 1):
        add_beveled_box(f"DorsalCoolingPod_{side}", (side * 0.165, 1.60, -0.215),
                        (0.088, 0.24, 0.075), "SatinTitanium", rot=(-0.08, side * 0.14, 0), bevel=0.008, segments=3)
        add_piston(f"DorsalSpineActuator_{side}", (side * 0.135, 1.16, -0.155), (side * 0.160, 1.48, -0.205), 0.020, 0.012)

    for v_idx in range(8):
        vy = 1.08 + v_idx * 0.088
        vz = -0.185 if v_idx < 3 else (-0.215 if v_idx < 5 else -0.242)
        add_beveled_box(f"SpineVertebraBlock_{v_idx}", (0, vy, vz),
                        (0.096, 0.062, 0.078), "SatinTitanium" if v_idx % 2 == 0 else "DarkArmor",
                        rot=(0.15, 0, 0), bevel=0.009, segments=3)


def build_abdomen_and_pelvis():
    """4. V-Chevron articulated abdomen, central hydraulic actuator & armored pelvis (no cartoon glowing codpiece!)."""
    add_rod("AbdomenCoreColumn", (0, 0.98, 0.0), (0, 1.38, 0.01), 0.138, 0.158, "CarbonSubframe", sides=28)
    add_piston("CentralLineaAlbaActuator", (0, 1.05, 0.165), (0, 1.39, 0.185), 0.028, 0.018)

    for side in (-1, 1):
        s = side
        for ab_idx, ay in enumerate((1.10, 1.18, 1.26, 1.34)):
            w_out = 0.158 + ab_idx * 0.016
            add_hull(f"AbdominalChevron_{s}_{ab_idx}", [
                (s * 0.032, ay - 0.030, 0.182),
                (s * 0.032, ay + 0.022, 0.186),
                (s * w_out, ay + 0.048, 0.135),
                (s * w_out, ay - 0.008, 0.130),
                (s * 0.032, ay - 0.030, 0.110),
                (s * w_out, ay + 0.048, 0.065),
            ], "DarkArmor" if ab_idx % 2 == 0 else "SatinTitanium", bevel=0.008, segments=3)

        add_piston(f"WaistHydraulicOuter_{s}",
                   (s * 0.185, 1.02, 0.06), (s * 0.235, 1.36, 0.09), 0.024, 0.015)
        add_piston(f"WaistHydraulicRear_{s}",
                   (s * 0.165, 1.02, -0.09), (s * 0.215, 1.36, -0.12), 0.024, 0.015)

    add_hull("PelvisCenterBlock", [
        (-0.16, 0.96, -0.14), (0.16, 0.96, -0.14),
        (-0.14, 1.06,  0.175), (0.14, 1.06,  0.175),
        (-0.075, 0.83, 0.195), (0.075, 0.83, 0.195),
        (-0.095, 0.83, -0.13), (0.095, 0.83, -0.13),
    ], "DarkArmor", bevel=0.014, segments=3)

    # Subtle SatinTitanium & WhiteCeramic pelvic Serial Plate (replaces glowing red triangle on crotch)
    add_hull("PelvisTitaniumEscutcheon", [
        (-0.042, 0.985, 0.192), (0.042, 0.985, 0.192),
        (-0.028, 0.895, 0.196), (0.028, 0.895, 0.196),
        (-0.042, 0.985, 0.175), (0.042, 0.985, 0.175),
        (-0.028, 0.895, 0.175), (0.028, 0.895, 0.175),
    ], "SatinTitanium", bevel=0.004, segments=3)

    for side in (-1, 1):
        s = side
        add_hull(f"HipBeltArmorPod_{s}", [
            (s * 0.14, 1.04,  0.14), (s * 0.28, 1.01,  0.11),
            (s * 0.29, 0.89,  0.09), (s * 0.15, 0.88,  0.13),
            (s * 0.14, 1.04, -0.12), (s * 0.28, 1.01, -0.11),
            (s * 0.29, 0.89, -0.10), (s * 0.15, 0.88, -0.12),
        ], "SatinTitanium", bevel=0.011, segments=3)


def build_pauldrons_and_hydraulic_arms():
    """5. Broad angular pauldrons, smooth lofted bicep/forearm armor shells, double-clevis hydraulic elbows & 5-fingered gauntlets."""
    for side in (-1, 1):
        s = side
        sh_p = Vector((s * 0.375, 1.66, 0.00))
        el_p = Vector((s * 0.445, 1.28, 0.05))
        wr_p = Vector((s * 0.390, 0.96, 0.26))

        # 5A. Broad Angular Multi-Layered Pauldrons (exact outer X = ±0.5486 preserved!)
        add_hull(f"PauldronUpperShell_{s}", [
            (s * 0.26, 1.74, -0.15), (s * 0.26, 1.74,  0.15),
            (s * 0.35, 1.83, -0.12), (s * 0.35, 1.83,  0.12),
            (s * 0.52, 1.71, -0.16), (s * 0.52, 1.71,  0.16),
            (s * 0.54, 1.56, -0.15), (s * 0.54, 1.56,  0.15),
            (s * 0.36, 1.58, -0.17), (s * 0.36, 1.58,  0.17),
        ], "DarkArmor", bevel=0.015, segments=3)

        add_hull(f"PauldronLowerSkirt_{s}", [
            (s * 0.38, 1.58, -0.14), (s * 0.38, 1.58,  0.14),
            (s * 0.548, 1.57, -0.13), (s * 0.548, 1.57, 0.13),
            (s * 0.52, 1.43, -0.11), (s * 0.52, 1.43,  0.11),
            (s * 0.38, 1.45, -0.13), (s * 0.38, 1.45,  0.13),
        ], "SatinTitanium", bevel=0.011, segments=3)

        # 5B. Rotary Shoulder Actuator & Smooth Lofted Bicep Shell
        add_rod(f"ShoulderBallActuator_{s}", sh_p - Vector((s * 0.065, 0, 0)), sh_p + Vector((s * 0.055, 0, 0)),
                0.084, 0.084, "CarbonSubframe", sides=28)
        add_rod(f"UpperArmCore_{s}", sh_p, el_p, 0.066, 0.056, "CarbonSubframe", sides=24)

        add_oriented_loft(f"BicepSculptedShell_{s}", sh_p, el_p, (0, 0, 1), [
            (0.08, 0.066, 0.068, 0.0, 0.0, 2.4),
            (0.22, 0.082, 0.086, s * 0.006, 0.004, 2.6),
            (0.55, 0.084, 0.088, s * 0.008, 0.006, 2.6),
            (0.82, 0.072, 0.076, s * 0.004, 0.002, 2.4),
            (0.92, 0.058, 0.062, 0.0, 0.0, 2.2),
        ], "DarkArmor", n_seg=28)

        add_piston(f"BicepHydraulic_{s}",
                   sh_p + Vector((-s * 0.032, -0.05, 0.072)),
                   el_p + Vector((-s * 0.022,  0.04, 0.072)), 0.019, 0.012)

        # 5C. Articulated Double-Clevis Elbow Joint
        add_rod(f"ElbowHingeAxle_{s}", el_p - Vector((0.086, 0, 0)), el_p + Vector((0.086, 0, 0)),
                0.054, 0.054, "SatinTitanium", sides=28)
        add_rod(f"ElbowChromeCaps_{s}", el_p - Vector((0.092, 0, 0)), el_p + Vector((0.092, 0, 0)),
                0.034, 0.034, "PistonChrome", sides=24)
        add_beveled_box(f"ElbowGuardCowl_{s}", el_p + Vector((s * 0.018, 0, -0.062)),
                        (0.112, 0.118, 0.088), "SatinTitanium", bevel=0.012, segments=3)

        # 5D. Smooth Lofted Tapered Forearm Gauntlet Shell
        f_axis = (wr_p - el_p).normalized()
        f_mid = (el_p + wr_p) * 0.5
        add_rod(f"ForearmCore_{s}", el_p, wr_p, 0.058, 0.046, "CarbonSubframe", sides=24)

        add_oriented_loft(f"ForearmSculptedGauntlet_{s}", el_p, wr_p, (0, 1, 0), [
            (0.08, 0.062, 0.062, 0.0, 0.0, 2.2),
            (0.22, 0.082, 0.078, s * 0.006, 0.002, 2.6),
            (0.58, 0.074, 0.070, s * 0.004, 0.002, 2.6),
            (0.88, 0.058, 0.056, 0.0, 0.0, 2.4),
            (0.98, 0.048, 0.046, 0.0, 0.0, 2.2),
        ], "DarkArmor", n_seg=28)

        add_oriented_hull(f"ForearmKineticBlade_{s}", f_mid + Vector((s * 0.058, 0.01, 0.0)), f_axis, (0, 1, 0), [
            (-0.018, -0.055, -0.12), ( 0.018, -0.045, -0.12),
            (-0.018,  0.060, -0.11), ( 0.022,  0.055, -0.11),
            (-0.016, -0.042,  0.13), ( 0.016, -0.035,  0.13),
            (-0.016,  0.045,  0.13), ( 0.018,  0.042,  0.13),
        ], "SatinTitanium", bevel=0.008, segments=3)

        add_piston(f"ForearmPiston_{s}",
                   el_p + Vector((s * 0.042, -0.03, -0.052)),
                   wr_p + Vector((s * 0.032,  0.03, -0.042)), 0.017, 0.011)

        # 5E. Articulated 5-Fingered Mechanical Hand (with knuckle spheres so zero gaps!)
        add_rod(f"WristRotaryCuff_{s}", wr_p - f_axis * 0.015, wr_p + f_axis * 0.020, 0.048, 0.046, "SatinTitanium", sides=24)
        palm_c = wr_p + f_axis * 0.055
        add_oriented_hull(f"HandPalmHull_{s}", palm_c, f_axis, (0, 1, 0), [
            (-0.046, -0.028, -0.045), ( 0.046, -0.028, -0.045),
            (-0.048,  0.032, -0.045), ( 0.048,  0.032, -0.045),
            (-0.044, -0.024,  0.048), ( 0.044, -0.024,  0.048),
            (-0.046,  0.028,  0.048), ( 0.046,  0.028,  0.048),
        ], "DarkArmor", bevel=0.008, segments=3)

        add_oriented_hull(f"HandDorsalCeramic_{s}", palm_c + Vector((s * 0.022, 0.012, 0)), f_axis, (0, 1, 0), [
            (-0.018, -0.026, -0.036), ( 0.018, -0.026, -0.036),
            (-0.018,  0.026, -0.036), ( 0.018,  0.026, -0.036),
            (-0.016, -0.024,  0.036), ( 0.016, -0.024,  0.036),
            (-0.016,  0.024,  0.036), ( 0.016,  0.024,  0.036),
        ], "WhiteCeramic", bevel=0.005, segments=3)

        for f_idx in range(4):
            u_off = (f_idx - 1.5) * 0.022
            k0 = palm_c + f_axis * 0.044 + Vector((s * 0.018, u_off, 0.0))
            k1 = k0 + f_axis * 0.044 + Vector((-s * 0.016, 0.0, 0.010))
            k2 = k1 + f_axis * 0.020 + Vector((-s * 0.036, 0.0, -0.012))
            add_rod(f"FingerProx_{s}_{f_idx}", k0, k1, 0.0135, 0.0115, "SatinTitanium", sides=16)
            add_rod(f"FingerMid_{s}_{f_idx}", k1 - (k1 - k0).normalized() * 0.004, k2, 0.0115, 0.0075, "DarkArmor", sides=16)

        t0 = palm_c + Vector((-s * 0.018, 0.034, 0.015))
        t1 = t0 + f_axis * 0.042 + Vector((-s * 0.022, -0.010, 0.015))
        t2 = t1 + f_axis * 0.026 + Vector(( s * 0.012, -0.018, 0.0))
        add_rod(f"ThumbProx_{s}", t0, t1, 0.0155, 0.0125, "SatinTitanium", sides=16)
        add_rod(f"ThumbDist_{s}", t1 - (t1 - t0).normalized() * 0.004, t2, 0.0125, 0.0085, "DarkArmor", sides=16)


def build_sculpted_hydraulic_legs_and_boots():
    """6. Smooth lofted thigh & shin armor shells, articulated hydraulic knees, twin calf shock absorbers & split-toe boots at Y=0.0."""
    for side in (-1, 1):
        s = side
        hip_p   = Vector((s * 0.215, 0.92,  0.00))
        knee_p  = Vector((s * 0.250, 0.50,  0.04))
        ankle_p = Vector((s * 0.265, 0.13, -0.03))

        # 6A. Smooth Lofted Thigh Armor Shell (Cuisse)
        add_rod(f"HipBallJoint_{s}", hip_p - Vector((s * 0.06, 0, 0)), hip_p + Vector((s * 0.05, 0, 0)),
                0.096, 0.096, "CarbonSubframe", sides=28)
        add_rod(f"ThighCore_{s}", hip_p, knee_p, 0.095, 0.080, "CarbonSubframe", sides=24)

        add_oriented_loft(f"ThighSculptedShell_{s}", hip_p, knee_p, (0, 0, 1), [
            (0.06, 0.092, 0.105, s * 0.015, 0.010, 2.4),
            (0.20, 0.114, 0.132, s * 0.022, 0.018, 2.6),
            (0.55, 0.108, 0.126, s * 0.018, 0.016, 2.6),
            (0.84, 0.092, 0.108, s * 0.010, 0.010, 2.4),
            (0.95, 0.078, 0.088, 0.0, 0.0, 2.2),
        ], "DarkArmor", n_seg=32)

        thigh_mid = (hip_p + knee_p) * 0.5
        add_hull(f"ThighOuterReactive_{s}", [
            (s * 0.315, 0.86,  0.085), (s * 0.368, 0.84,  0.065),
            (s * 0.315, 0.86, -0.085), (s * 0.368, 0.84, -0.065),
            (s * 0.315, 0.58,  0.075), (s * 0.358, 0.59,  0.055),
            (s * 0.315, 0.58, -0.075), (s * 0.358, 0.59, -0.055),
        ], "SatinTitanium", bevel=0.010, segments=3)

        add_piston(f"ThighInnerPiston_{s}",
                   hip_p + Vector((-s * 0.075, -0.04, 0.07)),
                   knee_p + Vector((-s * 0.070, 0.05, 0.06)), 0.021, 0.013)

        # 6B. Articulated Hydraulic Knee Joint & Sculpted Patella Shield
        add_rod(f"KneeAxle_{s}", knee_p - Vector((0.105, 0, 0)), knee_p + Vector((0.105, 0, 0)),
                0.068, 0.068, "SatinTitanium", sides=28)
        add_rod(f"KneeChromeCaps_{s}", knee_p - Vector((0.112, 0, 0)), knee_p + Vector((0.112, 0, 0)),
                0.042, 0.042, "PistonChrome", sides=24)
        add_hull(f"KneeShield_{s}", [
            (knee_p.x - 0.072, knee_p.y - 0.085, knee_p.z + 0.055),
            (knee_p.x + 0.072, knee_p.y - 0.085, knee_p.z + 0.055),
            (knee_p.x - 0.082, knee_p.y + 0.085, knee_p.z + 0.065),
            (knee_p.x + 0.082, knee_p.y + 0.085, knee_p.z + 0.065),
            (knee_p.x,         knee_p.y + 0.015, knee_p.z + 0.148),
        ], "DarkArmor", bevel=0.011, segments=3)

        # 6C. Smooth Lofted Shin Greave & Twin Rear Calf Hydraulic Shock Absorbers
        add_rod(f"ShinCore_{s}", knee_p, ankle_p, 0.078, 0.062, "CarbonSubframe", sides=24)

        add_oriented_loft(f"ShinSculptedGreave_{s}", knee_p, ankle_p, (0, 0, 1), [
            (0.08, 0.076, 0.088, 0.0, 0.018, 2.3),
            (0.22, 0.088, 0.105, 0.0, 0.025, 2.6),
            (0.62, 0.078, 0.095, 0.0, 0.022, 2.6),
            (0.90, 0.066, 0.082, 0.0, 0.014, 2.3),
        ], "DarkArmor", n_seg=32)

        add_hull(f"ShinCrestPlate_{s}", [
            (ankle_p.x - 0.028, 0.17, 0.112), (ankle_p.x + 0.028, 0.17, 0.112),
            (knee_p.x  - 0.035, 0.42, 0.148), (knee_p.x  + 0.035, 0.42, 0.148),
            (ankle_p.x - 0.032, 0.17, 0.075), (ankle_p.x + 0.032, 0.17, 0.075),
            (knee_p.x  - 0.040, 0.42, 0.105), (knee_p.x  + 0.040, 0.42, 0.105),
        ], "SatinTitanium", bevel=0.007, segments=3)

        for px_off in (-0.036, 0.036):
            add_piston(f"CalfPiston_{s}_{px_off}",
                       knee_p + Vector((px_off, -0.03, -0.070)),
                       ankle_p + Vector((px_off,  0.03, -0.090)), 0.023, 0.015)

        # 6D. Heavy Split-Toe Cybernetic Combat Boot planted solidly at Y = 0.0000 m
        fx = ankle_p.x
        add_beveled_box(f"AnkleCollar_{s}", (fx, 0.135, -0.02), (0.142, 0.092, 0.162), "SatinTitanium", bevel=0.012, segments=3)
        add_hull(f"BootHeelSpur_{s}", [
            (fx - 0.072, 0.00, -0.155), (fx + 0.072, 0.00, -0.155),
            (fx - 0.080, 0.00, -0.020), (fx + 0.080, 0.00, -0.020),
            (fx - 0.068, 0.10, -0.020), (fx + 0.068, 0.10, -0.020),
            (fx - 0.060, 0.08, -0.140), (fx + 0.060, 0.08, -0.140),
        ], "DarkArmor", bevel=0.010, segments=3)

        for toe_side in (-1, 1):
            tx = fx + toe_side * 0.045
            add_hull(f"BootToeClaw_{s}_{toe_side}", [
                (tx - 0.036, 0.00, -0.02), (tx + 0.036, 0.00, -0.02),
                (tx - 0.030, 0.00,  0.185), (tx + 0.030, 0.00,  0.185),
                (tx - 0.026, 0.048, 0.195), (tx + 0.026, 0.048, 0.195),
                (tx - 0.034, 0.095, 0.01), (tx + 0.034, 0.095, 0.01),
            ], "DarkArmor", bevel=0.009, segments=3)

        add_beveled_box(f"BootInstepGuard_{s}", (fx, 0.088, 0.065),
                        (0.132, 0.056, 0.142), "SatinTitanium", rot=(0.32, 0, 0), bevel=0.010, segments=3)


def assign_uvs_and_bake_ao_24(boss):
    mesh = boss.data

    old_names = [m.name.split(".")[0] if m else "DarkArmor" for m in mesh.materials]
    name_to_idx = {name: idx for idx, name in enumerate(MAT_ORDER)}
    poly_targets = [name_to_idx.get(old_names[p.material_index], 0) for p in mesh.polygons]
    mesh.materials.clear()
    for name in MAT_ORDER:
        mesh.materials.append(MATS[name])
    for poly, ni in zip(mesh.polygons, poly_targets):
        poly.material_index = ni

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    # Clamp bottom-most boot sole vertices to exact Z_bl = 0.0000 (Y_gltf = 0.0000) so origin is 100% flush with floor
    for v in mesh.vertices:
        if v.co.z < 0.002:
            v.co.z = 0.0
        elif v.co.z > 2.2365:
            v.co.z = 2.2365
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.update()

    # Apply Weighted Normals modifier so beveled armor plates have razor-crisp planar reflections & smooth edges
    bpy.context.view_layer.objects.active = boss
    boss.select_set(True)
    wn = boss.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.keep_sharp = False
    wn.weight = 50
    try:
        bpy.ops.object.modifier_apply(modifier=wn.name)
    except Exception:
        boss.modifiers.remove(wn)

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    quad_offsets = {
        "DarkArmor":      (0.04, 0.04),
        "SatinTitanium":  (0.04, 0.04),
        "CarbonSubframe": (0.54, 0.04),
        "VisorObsidian":  (0.54, 0.04),
        "WhiteCeramic":   (0.04, 0.54),
        "PistonChrome":   (0.04, 0.54),
        "CrimsonCore":    (0.54, 0.54),
    }

    verts = mesh.vertices
    for poly in mesh.polygons:
        m_name = MAT_ORDER[poly.material_index]
        u_base, v_base = quad_offsets.get(m_name, (0.04, 0.04))
        for li in poly.loop_indices:
            v_co = verts[mesh.loops[li].vertex_index].co
            tu = min(1.0, max(0.0, (v_co.x + 0.56 + 0.15 * v_co.y) / 1.12))
            tv = min(1.0, max(0.0, (v_co.z + 0.08 * v_co.y) / 2.25))
            uv_layer.data[li].uv = (tu * 0.42 + u_base, tv * 0.42 + v_base)

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    vcol = mesh.color_attributes.new(name="Color", type="BYTE_COLOR", domain="POINT")

    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(p.vertices) for p in mesh.polygons])
    v_mat = [0] * len(verts)
    v_norms = [Vector((0.0, 0.0, 0.0)) for _ in range(len(verts))]
    v_neighbors = [set() for _ in range(len(verts))]
    for poly in mesh.polygons:
        mi = poly.material_index
        pn = poly.normal * max(1e-6, poly.area)
        pverts = poly.vertices
        for vi in pverts:
            v_norms[vi] += pn
            if mi > v_mat[vi]:
                v_mat[vi] = mi
        for idx_a in range(len(pverts)):
            va = pverts[idx_a]
            vb = pverts[(idx_a + 1) % len(pverts)]
            v_neighbors[va].add(vb)
            v_neighbors[vb].add(va)

    golden_angle = 2.39996323
    world_rays = []
    n_sphere = 24
    for j in range(n_sphere):
        z_r = 1.0 - 2.0 * (j + 0.5) / n_sphere
        rad_r = math.sqrt(max(0.0, 1.0 - z_r * z_r))
        ang_r = j * golden_angle
        world_rays.append(Vector((rad_r * math.cos(ang_r), rad_r * math.sin(ang_r), z_r)).normalized())

    shades = [1.0] * len(verts)
    for v in verts:
        vi = v.index
        m_name = MAT_ORDER[v_mat[vi]]
        if m_name == "CrimsonCore":
            shades[vi] = 1.0
            continue
        n = v_norms[vi].normalized()
        if n.length_squared < 0.01:
            n = Vector((0.0, -1.0, 0.0))
        origin = v.co + n * 0.005
        ao = 0.0
        valid = 0
        max_dist = 0.085
        for ray in world_rays:
            nd = ray.dot(n)
            if nd <= 0.45:
                continue
            valid += 1
            hit_dist = tree.ray_cast(origin, ray, max_dist)[3]
            if hit_dist is not None and hit_dist > 0.007:
                ao += (1.0 - hit_dist / max_dist) * nd
        if valid > 0:
            ao /= valid
        v_grad = 0.94 + 0.06 * min(1.0, max(0.0, v.co.z / 2.20))
        ao_strength = 0.22 if m_name == "WhiteCeramic" else 0.36
        shades[vi] = max(0.65, min(1.0, v_grad * (1.0 - ao_strength * ao)))

    for v in verts:
        vi = v.index
        m_name = MAT_ORDER[v_mat[vi]]
        if m_name == "CrimsonCore":
            vcol.data[vi].color = (1.0, 1.0, 1.0, 1.0)
            continue
        nbrs = v_neighbors[vi]
        shade = (0.55 * shades[vi] + 0.45 * (sum(shades[nb] for nb in nbrs) / len(nbrs))) if nbrs else shades[vi]
        vcol.data[vi].color = (shade, shade, shade, 1.0)


def patch_boss_glb_materials(glb_path):
    raw = glb_path.read_bytes()
    magic, version, _ = struct.unpack("<III", raw[:12])
    json_len, json_type = struct.unpack("<II", raw[12:20])
    gltf = json.loads(raw[20 : 20 + json_len].decode("utf-8"))
    bin_chunk = raw[20 + json_len :]

    has_tex = bool(gltf.get("textures"))
    materials = gltf.get("materials", [])
    for idx, key in enumerate(MAT_ORDER):
        if idx >= len(materials):
            break
        rgba, met, rough, em = MAT_SPECS[key]
        m_dict = materials[idx]
        m_dict["name"] = key
        pbr = m_dict.setdefault("pbrMetallicRoughness", {})
        pbr["baseColorFactor"] = [round(c, 5) for c in rgba]
        pbr["metallicFactor"] = met
        pbr["roughnessFactor"] = rough
        if has_tex:
            pbr["baseColorTexture"] = {"index": 0}
        if em is not None:
            m_dict["emissiveFactor"] = [round(c, 5) for c in em]

    new_json = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    pad = (4 - (len(new_json) % 4)) % 4
    new_json += b" " * pad
    total = 12 + 8 + len(new_json) + len(bin_chunk)
    out = struct.pack("<III", magic, version, total) + struct.pack("<II", len(new_json), json_type) + new_json + bin_chunk
    glb_path.write_bytes(out)


def render_boss_v2_proof(out_glb):
    """Render 3-panel inspection proof sheet (Front 3/4, Back 3/4, Close-Up Porcelain Mask & Recessed Triangular Crimson Core)."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1800
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("ProofWorld")
    sc.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.07, 0.085, 0.11, 1.0)
        bg.inputs[1].default_value = 0.7

    # 1. Left Panel (x = -1.35): Full-body Front 3/4 view (head to boots)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(out_glb))
    front_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in front_objs:
        o.location = Vector((-1.35, 0.0, 0.0))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(-20.0))

    # 2. Center Panel (x = -0.12): Full-body Back 3/4 view (dorsal carapace, cooling pods, hydraulic spine vertebrae & calf shock absorbers)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(out_glb))
    back_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in back_objs:
        o.location = Vector((-0.12, 0.0, 0.0))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(156.0))

    # 3. Right Panel (x = +1.22): Close-up Bust of Smooth White Porcelain Face Mask + Recessed Glowing Triangular Crimson Chest Core
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(out_glb))
    bust_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in bust_objs:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
        bmesh.ops.bisect_plane(bm, geom=geom, plane_co=Vector((0.0, 0.0, 1.34)), plane_no=Vector((0.0, 0.0, 1.0)), clear_inner=True)
        bm.normal_update()
        bm.to_mesh(o.data)
        bm.free()
        o.scale = (1.72, 1.72, 1.72)
        o.location = Vector((1.22, 0.0, -1.84))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(12.0))

    # Boost CrimsonCore emission in Blender's imported shader so EEVEE proof shows the glowing crimson reactor core clearly
    for mat in bpy.data.materials:
        if mat.name.startswith("CrimsonCore") and mat.node_tree:
            bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
            if bsdf and "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (1.0, 0.08, 0.05, 1.0)
                bsdf.inputs["Emission Strength"].default_value = 6.5

    target = bpy.data.objects.new("Target", None)
    sc.collection.objects.link(target)
    target.location = (0.0, 0.0, 1.12)

    for name, loc, energy, col in [
        ("Key",  (2.4, -3.5, 3.0), 3.6, (1.0, 0.96, 0.90)),
        ("Fill", (-2.6, -2.5, 1.8), 1.9, (0.76, 0.86, 1.0)),
        ("Rim",  (0.0, 3.2, 2.8),   2.8, (1.0, 0.55, 0.48)),
    ]:
        ld = bpy.data.lights.new(name, type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        d = Vector((0.0, 0.0, 1.12)) - Vector(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("ProofCam")
    cd.type = "ORTHO"
    cd.ortho_scale = 4.25
    co = bpy.data.objects.new("ProofCam", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -4.5, 1.12)
    co.rotation_euler = (math.radians(90.0), 0.0, 0.0)

    for target_path in (ARTIFACT_PROOF_1, ARTIFACT_PROOF_2, SCREENSHOT_PROOF, PREVIEW_PNG):
        try:
            target_path.parent.mkdir(parents=True, exist_ok=True)
            sc.render.filepath = str(target_path)
            bpy.ops.render.render(write_still=True)
            print("Saved proof:", target_path)
        except Exception as exc:
            print("Warning saving proof to", target_path, ":", exc)


def build_escaped_ai_boss():
    clear_scene()
    init_palette()

    build_porcelain_face_mask_and_head()
    build_hydraulic_neck_and_collar()
    build_torso_and_recessed_crimson_core()
    build_abdomen_and_pelvis()
    build_pauldrons_and_hydraulic_arms()
    build_sculpted_hydraulic_legs_and_boots()

    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    bpy.ops.object.join()
    boss = bpy.context.active_object
    boss.name = boss.data.name = "escaped_ai_boss"
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    assign_uvs_and_bake_ao_24(boss)

    os.makedirs(OUT_MODELS, exist_ok=True)
    out_glb = OUT_MODELS / "escaped_ai_boss.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_glb),
        export_format="GLB",
        export_vertex_color="ACTIVE",
        export_normals=True,
        export_texcoords=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
    )
    patch_boss_glb_materials(out_glb)

    pts = [boss.matrix_world @ v.co for v in boss.data.vertices]
    tris = sum(len(p.vertices) - 2 for p in boss.data.polygons)
    print(f"Exported escaped_ai_boss.glb: {out_glb} Size: {os.path.getsize(out_glb)} Verts: {len(boss.data.vertices)} Tris: {tris}")
    print("BBOX X:", round(min(p.x for p in pts), 4), round(max(p.x for p in pts), 4))
    print("BBOX Y:", round(min(p.y for p in pts), 4), round(max(p.y for p in pts), 4))
    print("BBOX Z:", round(min(p.z for p in pts), 4), round(max(p.z for p in pts), 4))

    render_boss_v2_proof(out_glb)


if __name__ == "__main__":
    build_escaped_ai_boss()
