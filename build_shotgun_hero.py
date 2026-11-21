"""v4 AAA HARD-SURFACE 1990s SAWED-OFF COACH GUN (КОРОТКИЙ ОБРЕЗ ТОЗ-66 / БМ-16)
-> assets/models/weapon_shotgun_90s.glb + assets/icons/icon_slot_4_shotgun.png

Uses the exact studio-grade hard-surface pipeline from `build_smooth_uzi.py`:
  - ZERO Catmull-Clark SUBSURF melting! Crisp `BEVEL` + `WEIGHTED_NORMAL` (`FACE_AREA`, weight=50)
  - ZERO noisy photographic JPGs! Embedded 256x256 2D BVHTree 24-ray Baked Ambient Occlusion +
    Curvature Edge-Wear + Fine Brushed-Steel / Oiled-Walnut-Grain LUTs calibrated for Raylib's
    `pow(base, 0.78)` & showcase gamma curves!
  - True short brutal sawed-off ОБРЕЗ proportions (`z_muzzle = -0.262`, hand-sawed raw steel muzzle lip,
    crisp twin 48-seg cold-blued barrels, low concave bridge rib, sculpted standing-breech receiver with
    recessed geometric sidelock panels & countersunk pins, razor-crisp external S-curved hammers,
    top break-action lever, twin triggers, contoured oiled-walnut forend & sawed-off pistol grip!)
  - Preserves exact `MovingIslands` break-action hinge split (`minZ < -0.100` hinges around `(0, -0.013, -0.127)`)
    and 100% `--grip-test` hand alignment!
"""
import math
import os
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Quaternion, Vector as V
from mathutils.bvhtree import BVHTree

ROOT = Path(__file__).resolve().parent
OUT_MODELS = ROOT / "assets/models"
OUT_ICONS = ROOT / "assets/icons"


def xyz(p):
    """Game local coords (X right, Y up, +Z toward butt, -Z muzzle) -> Blender Z-up (X, -Z, Y)."""
    return (p[0], -p[2], p[1])


def build_shotgun_lut_texture(key):
    """256x256 2D AO+Curvature (u) x Fine Longitudinal Grain (v) LUT calibrated to match `weapon_uzi_90s.glb`."""
    img_name = f"ShotgunLUT_{key}"
    existing = bpy.data.images.get(img_name)
    if existing is not None:
        return existing

    W, H = 256, 256
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(1994 + sum(ord(c) for c in key))
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u = x / float(W - 1)
    v = y / float(H - 1)

    # Smooth AO + Convex Edge-Wear curve along u (exact Uzi calibration)
    ao_part = np.clip(u / 0.58, 0.0, 1.0)
    edge_part = np.clip((u - 0.58) / 0.42, 0.0, 1.0)
    shade = (0.16 + 0.84 * (ao_part ** 1.32)) * (1.0 + 0.78 * (edge_part ** 1.60))

    noise_fine = rng.normal(0.0, 1.0, (H, W)).astype(np.float32)

    if key == "BluedSteel":
        # Deep cold-blued Soviet barrel gunmetal (dark sleek satin blue-black with subtle brushed streaks)
        grain = 1.0 + 0.055 * np.sin(y * 1.05 + np.sin(x * 0.07) * 1.1) + 0.032 * noise_fine
        base = np.clip(0.0138 * shade * grain, 0.0016, 0.036)
        arr[:, :, 0] = base * 0.88
        arr[:, :, 1] = base * 0.96
        arr[:, :, 2] = base * 1.14
    elif key == "ReceiverSteel":
        # Refined satin case-hardened steel (lighter steel tone contrasting cleanly against dark barrels & walnut)
        grain = 1.0 + 0.065 * np.sin(y * 1.35) + 0.038 * np.cos(y * 2.6 + x * 0.06) + 0.035 * noise_fine
        base = np.clip(0.0265 * shade * grain, 0.0032, 0.068)
        arr[:, :, 0] = base * 0.96
        arr[:, :, 1] = base * 0.99
        arr[:, :, 2] = base * 1.07
    elif key == "RawCutSteel":
        # Bright freshly hacksaw-cut steel muzzle lip (iconic sawed-off glint!)
        grain = 1.0 + 0.09 * np.sin(y * 2.8) + 0.04 * noise_fine
        base = np.clip(0.072 * (0.45 + 0.65 * shade) * grain, 0.012, 0.165)
        arr[:, :, 0] = base * 0.97
        arr[:, :, 1] = base * 1.00
        arr[:, :, 2] = base * 1.05
    elif key == "DarkSteelTrim":
        # Deep black-oxide machined steel for hammers, triggers, guard, opening lever & pins
        grain = 1.0 + 0.05 * np.sin(y * 1.2) + 0.03 * noise_fine
        base = np.clip(0.0095 * shade * grain, 0.0012, 0.025)
        arr[:, :, 0] = base * 0.92
        arr[:, :, 1] = base * 0.97
        arr[:, :, 2] = base * 1.08
    elif key == "DarkWalnut":
        # Rich, clean dark oiled Russian walnut with flowing longitudinal grain (ZERO stone mottling!)
        # Smooth organic wood rings running along v (barrel length)
        wave = np.sin(v * 26.0 + 1.8 * np.sin(u * 5.2) + 0.7 * np.cos(v * 7.5))
        fine_fiber = np.sin(v * 95.0 + 0.6 * np.sin(u * 12.0))
        grain = 1.0 + 0.18 * wave + 0.06 * fine_fiber + 0.025 * noise_fine
        w_shade = np.clip(0.24 + 0.76 * (0.18 + 0.82 * (ao_part ** 1.25)) * (1.0 + 0.38 * (edge_part ** 1.5)), 0.12, 1.38)
        arr[:, :, 0] = np.clip(0.046 * w_shade * grain, 0.005, 0.105)
        arr[:, :, 1] = np.clip(0.0185 * w_shade * grain, 0.002, 0.046)
        arr[:, :, 2] = np.clip(0.0068 * w_shade * grain, 0.0008, 0.019)
    elif key == "CheckeredWalnut":
        # Deeper oil-darkened walnut with crisp clean gunsmith diamond checkering
        d1 = np.abs(np.sin((x + y) * 0.52))
        d2 = np.abs(np.sin((x - y) * 0.52))
        diamond = 0.62 + 0.38 * np.minimum(d1, d2)
        w_shade = np.clip(0.22 + 0.78 * (0.18 + 0.82 * (ao_part ** 1.25)), 0.10, 1.15)
        arr[:, :, 0] = np.clip(0.028 * w_shade * diamond, 0.003, 0.062)
        arr[:, :, 1] = np.clip(0.0110 * w_shade * diamond, 0.0012, 0.026)
        arr[:, :, 2] = np.clip(0.0040 * w_shade * diamond, 0.0005, 0.011)
    elif key == "BrassBead":
        # Warm antique oiled brass for 12-gauge shell rims, front bead & screws
        grain = 1.0 + 0.055 * np.sin(y * 0.95) + 0.03 * noise_fine
        b_shade = np.clip(0.26 + 0.74 * shade, 0.22, 1.45)
        arr[:, :, 0] = np.clip(0.132 * b_shade * grain, 0.022, 0.26)
        arr[:, :, 1] = np.clip(0.086 * b_shade * grain, 0.014, 0.17)
        arr[:, :, 2] = np.clip(0.024 * b_shade * grain, 0.004, 0.055)
    else:  # BoreBlack
        arr[:, :, 0] = 0.0018
        arr[:, :, 1] = 0.0018
        arr[:, :, 2] = 0.0022

    img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


MAT_SPECS = {
    "BluedSteel":      (0.84, 0.30),
    "ReceiverSteel":   (0.86, 0.28),
    "RawCutSteel":     (0.92, 0.22),
    "DarkSteelTrim":   (0.78, 0.36),
    "DarkWalnut":      (0.04, 0.44),
    "CheckeredWalnut": (0.02, 0.64),
    "BrassBead":       (0.90, 0.24),
    "BoreBlack":       (0.00, 0.95),
}


def make_lut_mat(key):
    met, rough = MAT_SPECS[key]
    m = bpy.data.materials.new(key)
    m.diffuse_color = (1.0, 1.0, 1.0, 1.0)
    m.use_nodes = True
    tree = m.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    bsdf.inputs["Metallic"].default_value = met
    bsdf.inputs["Roughness"].default_value = rough
    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = build_shotgun_lut_texture(key)
    tex.extension = "EXTEND"
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def apply_weighted_smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.mode = "FACE_AREA"
    wn.weight = 50
    wn.keep_sharp = False
    bpy.ops.object.modifier_apply(modifier=wn.name)
    obj.select_set(False)
    return obj


class HardSurfaceShotgun:
    def __init__(self):
        self.mats = {k: make_lut_mat(k) for k in MAT_SPECS}
        self.objs = []

    def add_bmesh_obj(self, name, bm, mat_key, bevel=0.0, bevel_segs=3, bevel_angle_deg=32.0):
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(self.mats[mat_key])
        if bevel > 0.0:
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            mod = obj.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = bevel_segs
            mod.profile = 0.55
            mod.limit_method = "ANGLE"
            mod.angle_limit = math.radians(bevel_angle_deg)
            mod.use_clamp_overlap = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
            obj.select_set(False)
        apply_weighted_smooth(obj)
        self.objs.append(obj)
        return obj

    def add_box(self, name, cx, cy, cz, w, h, d, mat_key, bevel=0.0018, segs=3, rot=(0.0, 0.0, 0.0)):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz((cx, cy, cz)))
        obj = bpy.context.active_object
        obj.name = name
        obj.dimensions = (w, d, h)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if any(abs(a) > 1e-6 for a in rot):
            obj.rotation_euler = Euler((rot[0], -rot[2], rot[1]), "XYZ")
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
        obj.data.materials.append(self.mats[mat_key])
        if bevel > 0.0:
            mod = obj.modifiers.new("Bevel", "BEVEL")
            mod.width = bevel
            mod.segments = segs
            mod.profile = 0.55
            mod.limit_method = "ANGLE"
            mod.use_clamp_overlap = True
            bpy.ops.object.modifier_apply(modifier=mod.name)
        apply_weighted_smooth(obj)
        self.objs.append(obj)
        return obj

    @staticmethod
    def bm_tube_z(bm, cx, cy, z0, z1, r_out0, r_out1=None, r_in=0.0, segs=48, cap0=True, cap1=True, chamfer=0.0005):
        if r_out1 is None:
            r_out1 = r_out0
        # Add tiny chamfer rings near ends so weighted normals stay laser-crisp on cylindrical barrels!
        if r_in > 0.0 and chamfer > 0.0 and abs(z1 - z0) > chamfer * 4.0:
            zs = [z0, z0 + chamfer, z1 - chamfer, z1]
            r_outs = [r_out0 - chamfer * 0.65, r_out0, r_out1, r_out1 - chamfer * 0.65]
            r_ins = [r_in + chamfer * 0.5, r_in, r_in, r_in + chamfer * 0.5]
            orings, irings = [], []
            for z, ro, ri in zip(zs, r_outs, r_ins):
                oring, iring = [], []
                for i in range(segs):
                    a = i * math.tau / segs
                    ca, sa = math.cos(a), math.sin(a)
                    oring.append(bm.verts.new(xyz((cx + ro * ca, cy + ro * sa, z))))
                    iring.append(bm.verts.new(xyz((cx + ri * ca, cy + ri * sa, z))))
                orings.append(oring)
                irings.append(iring)
            for s in range(len(zs) - 1):
                for i in range(segs):
                    ni = (i + 1) % segs
                    bm.faces.new((orings[s][i], orings[s][ni], orings[s + 1][ni], orings[s + 1][i]))
                    bm.faces.new((irings[s][ni], irings[s][i], irings[s + 1][i], irings[s + 1][ni]))
            for i in range(segs):
                ni = (i + 1) % segs
                bm.faces.new((orings[0][ni], orings[0][i], irings[0][i], irings[0][ni]))
                bm.faces.new((orings[-1][i], orings[-1][ni], irings[-1][ni], irings[-1][i]))
            return

        o0, o1 = [], []
        for i in range(segs):
            a = i * math.tau / segs
            ca, sa = math.cos(a), math.sin(a)
            o0.append(bm.verts.new(xyz((cx + r_out0 * ca, cy + r_out0 * sa, z0))))
            o1.append(bm.verts.new(xyz((cx + r_out1 * ca, cy + r_out1 * sa, z1))))
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((o0[i], o0[ni], o1[ni], o1[i]))
        if r_in > 0.0:
            i0, i1 = [], []
            for i in range(segs):
                a = i * math.tau / segs
                ca, sa = math.cos(a), math.sin(a)
                i0.append(bm.verts.new(xyz((cx + r_in * ca, cy + r_in * sa, z0))))
                i1.append(bm.verts.new(xyz((cx + r_in * ca, cy + r_in * sa, z1))))
            for i in range(segs):
                ni = (i + 1) % segs
                bm.faces.new((i0[ni], i0[i], i1[i], i1[ni]))
                bm.faces.new((o0[ni], o0[i], i0[i], i0[ni]))
                bm.faces.new((o1[i], o1[ni], i1[ni], i1[i]))
        else:
            if cap0:
                bm.faces.new(list(reversed(o0)))
            if cap1:
                bm.faces.new(o1)

    @staticmethod
    def bm_cyl_x(bm, x0, x1, cy, cz, r, segs=20):
        r0, r1 = [], []
        for i in range(segs):
            a = i * math.tau / segs
            ca, sa = math.cos(a), math.sin(a)
            r0.append(bm.verts.new(xyz((x0, cy + r * ca, cz + r * sa))))
            r1.append(bm.verts.new(xyz((x1, cy + r * ca, cz + r * sa))))
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))
        bm.faces.new(list(reversed(r0)))
        bm.faces.new(r1)

    @staticmethod
    def bm_cyl_y(bm, cx, y0, y1, cz, r0_rad, r1_rad=None, segs=22):
        if r1_rad is None:
            r1_rad = r0_rad
        a0, a1 = [], []
        for i in range(segs):
            a = i * math.tau / segs
            ca, sa = math.cos(a), math.sin(a)
            a0.append(bm.verts.new(xyz((cx + r0_rad * ca, y0, cz + r0_rad * sa))))
            a1.append(bm.verts.new(xyz((cx + r1_rad * ca, y1, cz + r1_rad * sa))))
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((a0[i], a0[ni], a1[ni], a1[i]))
        bm.faces.new(list(reversed(a0)))
        bm.faces.new(a1)

    @staticmethod
    def bm_loft(bm, rings_xyz, cap_start=True, cap_end=True):
        vrings = [[bm.verts.new(xyz(p)) for p in ring] for ring in rings_xyz]
        n = len(vrings[0])
        for s in range(len(vrings) - 1):
            a, b = vrings[s], vrings[s + 1]
            for i in range(n):
                ni = (i + 1) % n
                bm.faces.new((a[i], a[ni], b[ni], b[i]))
        if cap_start:
            bm.faces.new(list(reversed(vrings[0])))
        if cap_end:
            bm.faces.new(vrings[-1])

    @staticmethod
    def bm_sweep_yz(bm, pts_yz, half_w, half_t, x_center=0.0):
        rings = []
        m = len(pts_yz)
        for i, (y, z) in enumerate(pts_yz):
            if i == 0:
                dy, dz = pts_yz[1][0] - y, pts_yz[1][1] - z
            elif i == m - 1:
                dy, dz = y - pts_yz[-2][0], z - pts_yz[-2][1]
            else:
                dy, dz = pts_yz[i + 1][0] - pts_yz[i - 1][0], pts_yz[i + 1][1] - pts_yz[i - 1][1]
            L = math.hypot(dy, dz) + 1e-6
            ny, nz = -dz / L, dy / L
            hw = half_w[i] if isinstance(half_w, (list, tuple)) else half_w
            ht = half_t[i] if isinstance(half_t, (list, tuple)) else half_t
            # Crisp 8-point chamfered octagon cross-section (never blobby!)
            prof = [
                (x_center - hw,        y + ny * ht * 0.62, z + nz * ht * 0.62),
                (x_center - hw * 0.62, y + ny * ht,        z + nz * ht),
                (x_center + hw * 0.62, y + ny * ht,        z + nz * ht),
                (x_center + hw,        y + ny * ht * 0.62, z + nz * ht * 0.62),
                (x_center + hw,        y - ny * ht * 0.62, z - nz * ht * 0.62),
                (x_center + hw * 0.62, y - ny * ht,        z - nz * ht),
                (x_center - hw * 0.62, y - ny * ht,        z - nz * ht),
                (x_center - hw,        y - ny * ht * 0.62, z - nz * ht * 0.62),
            ]
            rings.append(prof)
        HardSurfaceShotgun.bm_loft(bm, rings, cap_start=True, cap_end=True)


def build_sawed_off_v4():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    b = HardSurfaceShotgun()

    # =========================================================================
    # PART A: HINGING SHORT SAWED-OFF TWIN BARRELS & WALNUT FOREND (minZ < -0.100!)
    # Because MovingIslands checks island minZ < -0.100, barrels/forend can meet the
    # receiver at a crisp 0.7mm machined hairline seam (-0.0982 vs -0.0975) with ZERO air gap!
    # =========================================================================
    z_muzzle = -0.264   # Brutal short genuine ОБРЕЗ (~4.6cm past forend so twin tubes read clearly!)
    z_breech = -0.0982  # Hairline 0.7mm break-action seam! Island minZ=-0.264 < -0.100 hinges cleanly!
    by = 0.0052
    bx_off = 0.0114

    # 1) Twin forged 48-seg cold-blued barrels with stepped reinforced breech chambers
    bm_barrels = bmesh.new()
    bm_muzzle_raw = bmesh.new()
    bm_bores = bmesh.new()
    bm_brass = bmesh.new()
    bm_primers = bmesh.new()
    for s in (-1, 1):
        bx = s * bx_off
        # Main cut barrel tube
        b.bm_tube_z(bm_barrels, bx, by, z_muzzle + 0.0030, -0.156, r_out0=0.0108, r_out1=0.0115, r_in=0.0084, segs=48, chamfer=0.00045)
        # Reinforced forged breech chamber step (classic Soviet TOZ-66 shoulder!)
        b.bm_tube_z(bm_barrels, bx, by, -0.156, z_breech, r_out0=0.0117, r_out1=0.0124, r_in=0.0086, segs=48, chamfer=0.00045)
        # Raw hacksaw-cut steel muzzle ring (razor-crisp metallic highlight at muzzle!)
        b.bm_tube_z(bm_muzzle_raw, bx, by, z_muzzle, z_muzzle + 0.0032, r_out0=0.0108, r_out1=0.0108, r_in=0.0084, segs=48, chamfer=0.00035)
        # Deep dark interior bore
        b.bm_tube_z(bm_bores, bx, by, z_muzzle + 0.005, z_breech - 0.010, r_out0=0.0083, r_out1=0.0083, segs=24)
        # 12-gauge brass shell rims & steel primers seated inside the twin breeches (visible on break-open!)
        b.bm_tube_z(bm_brass,   bx, by, z_breech - 0.0045, z_breech - 0.0002, r_out0=0.0094, r_out1=0.0095, segs=28, chamfer=0.0)
        b.bm_tube_z(bm_primers, bx, by, z_breech - 0.0004, z_breech + 0.0002, r_out0=0.0028, r_out1=0.0028, segs=16, chamfer=0.0)

    # Tiny classic brass front bead sight on the center rib near the sawed muzzle
    b.bm_cyl_y(bm_brass, 0.0, by + 0.0092, by + 0.0124, z_muzzle + 0.010, 0.0013, 0.0015, segs=16)

    b.add_bmesh_obj("TwinBarrels", bm_barrels, "BluedSteel")
    b.add_bmesh_obj("SawedMuzzleRings", bm_muzzle_raw, "RawCutSteel")
    b.add_bmesh_obj("InnerBores", bm_bores, "BoreBlack")
    b.add_bmesh_obj("BreechBrass", bm_brass, "BrassBead")
    b.add_bmesh_obj("ShellPrimers", bm_primers, "ReceiverSteel")

    # 2) Sleek low-profile concave sighting rib nestled flush between the twin barrels (NO tall ladder!)
    bm_rib = bmesh.new()
    rib_rings = [
        [(-0.0050, by + 0.0094, z_muzzle + 0.003), (-0.0018, by + 0.0088, z_muzzle + 0.003),
         ( 0.0018, by + 0.0088, z_muzzle + 0.003), ( 0.0050, by + 0.0094, z_muzzle + 0.003),
         ( 0.0066, by + 0.0048, z_muzzle + 0.003), (-0.0066, by + 0.0048, z_muzzle + 0.003)],
        [(-0.0062, by + 0.0108, z_breech - 0.001), (-0.0022, by + 0.0101, z_breech - 0.001),
         ( 0.0022, by + 0.0101, z_breech - 0.001), ( 0.0062, by + 0.0108, z_breech - 0.001),
         ( 0.0076, by + 0.0055, z_breech - 0.001), (-0.0076, by + 0.0055, z_breech - 0.001)],
    ]
    b.bm_loft(bm_rib, rib_rings, cap_start=True, cap_end=True)
    b.add_bmesh_obj("ConcaveTopRib", bm_rib, "DarkSteelTrim", bevel=0.0005, bevel_segs=2)

    # Lower barrel joining rib & underlug hinge block
    b.add_box("LowerBarrelRib", 0.0, by - 0.0096, -0.184, 0.0088, 0.0055, 0.152, "DarkSteelTrim", bevel=0.0008, segs=2)
    b.add_box("UnderlugHinge",  0.0, -0.013,      -0.127, 0.0165, 0.0155, 0.022, "DarkSteelTrim", bevel=0.0016, segs=3)

    # 3) Crisp Contoured Oiled-Walnut Forend (`цевьё`, Z = -0.214 .. -0.0986, flush to receiver!)
    bm_forend = bmesh.new()
    forend_stations = [
        (-0.214, 0.0125, -0.0110, -0.0025),
        (-0.208, 0.0185, -0.0185, -0.0005),
        (-0.194, 0.0222, -0.0238,  0.0012),
        (-0.172, 0.0234, -0.0252,  0.0015),
        (-0.146, 0.0228, -0.0248,  0.0010),
        (-0.118, 0.0224, -0.0242,  0.0000),
        (-0.0986,0.0228, -0.0238, -0.0005),
    ]
    f_rings = []
    for z, hw, yb, yt in forend_stations:
        ym = (yb + yt) * 0.5
        f_rings.append([
            (-hw * 0.92, yt, z), ( hw * 0.92, yt, z),
            ( hw, ym + 0.003, z), ( hw * 0.96, ym - 0.003, z),
            ( hw * 0.78, yb + 0.0032, z), ( hw * 0.34, yb, z),
            (-hw * 0.34, yb, z), (-hw * 0.78, yb + 0.0032, z),
            (-hw * 0.96, ym - 0.003, z), (-hw, ym + 0.003, z),
        ])
    b.bm_loft(bm_forend, f_rings, cap_start=True, cap_end=True)
    b.add_bmesh_obj("WalnutForend", bm_forend, "DarkWalnut", bevel=0.0014, bevel_segs=3, bevel_angle_deg=26.0)

    # Machined steel forend nose cap & recessed Deeley latch (NO glued sticker slabs!)
    b.add_box("ForendNoseCap",   0.0, -0.0085, -0.216, 0.023, 0.011, 0.0055, "DarkSteelTrim", bevel=0.0014, segs=3)
    b.add_box("ForendDeeleyBar", 0.0, -0.0246, -0.162, 0.0092, 0.0028, 0.032, "ReceiverSteel", bevel=0.0006, segs=2)

    # =========================================================================
    # PART B: NON-HINGING HARD-SURFACE FORGED RECEIVER, HAMMERS & WALNUT GRIP
    # EVERY VERTEX MUST HAVE Z >= -0.098 (STRICTLY NON-HINGING!)
    # =========================================================================

    # 1) Sculpted Hard-Surface Forged Steel Receiver (Satin Case-Hardened Steel!)
    bm_rec = bmesh.new()
    rec_stations = [
        (-0.0975, 0.0242, -0.0245,  0.0165),
        (-0.0880, 0.0246, -0.0265,  0.0168),
        (-0.0720, 0.0244, -0.0278,  0.0150),
        (-0.0520, 0.0234, -0.0274,  0.0108),
        (-0.0320, 0.0212, -0.0256,  0.0046),
        (-0.0160, 0.0190, -0.0235, -0.0018),
        (-0.0060, 0.0174, -0.0220, -0.0052),
    ]
    r_rings = []
    for z, hw, yb, yt in rec_stations:
        r_rings.append([
            (-hw * 0.72, yt, z), ( hw * 0.72, yt, z),
            ( hw, yt - 0.0042, z), ( hw, yb + 0.0050, z),
            ( hw * 0.72, yb, z), (-hw * 0.72, yb, z),
            (-hw, yb + 0.0050, z), (-hw, yt - 0.0042, z),
        ])
    b.bm_loft(bm_rec, r_rings, cap_start=True, cap_end=True)
    b.add_bmesh_obj("ForgedReceiver", bm_rec, "ReceiverSteel", bevel=0.0020, bevel_segs=3, bevel_angle_deg=24.0)

    # Crisp Standing Breech Cylindrical Bolsters (iconic double-barrel receiver shoulders!)
    bm_bolsters = bmesh.new()
    bm_firing_pins = bmesh.new()
    for s in (-1, 1):
        b.bm_tube_z(bm_bolsters,    s * bx_off, by, -0.0975, -0.072, r_out0=0.0125, r_out1=0.0116, segs=36, chamfer=0.0)
        b.bm_tube_z(bm_firing_pins, s * bx_off, by, -0.0980, -0.094, r_out0=0.0030, r_out1=0.0030, segs=16, chamfer=0.0)
    b.add_bmesh_obj("BreechBolsters", bm_bolsters, "ReceiverSteel", bevel=0.0012, bevel_segs=3)
    b.add_bmesh_obj("FiringPinBushings", bm_firing_pins, "BrassBead")

    # Sculpted Beveled Side-Lock Plates (`замочные доски ТОЗ-66`) with crisp stepped edges & pivot pins!
    bm_plates = bmesh.new()
    bm_pins = bmesh.new()
    for s in (-1, 1):
        # Tapered gunsmith sideplate silhouette hugging receiver walls
        p_stations = [
            (-0.089, -0.0205,  0.0092, 0.0244),
            (-0.074, -0.0220,  0.0096, 0.0244),
            (-0.052, -0.0218,  0.0058, 0.0234),
            (-0.032, -0.0200,  0.0005, 0.0212),
            (-0.017, -0.0180, -0.0048, 0.0190),
        ]
        p_rings = []
        for z, yb, yt, hw in p_stations:
            x0, x1 = s * (hw - 0.0006), s * (hw + 0.0014)
            ring = [(x0, yt, z), (x1, yt - 0.0012, z), (x1, yb + 0.0012, z), (x0, yb, z)]
            p_rings.append(ring if s > 0 else list(reversed(ring)))
        b.bm_loft(bm_plates, p_rings, cap_start=True, cap_end=True)
        # Countersunk dark-steel lockplate pivot pins & screw heads
        for pz, py, pr in ((-0.081, -0.008, 0.0022), (-0.064, -0.014, 0.0018), (-0.044, -0.010, 0.0020), (-0.026, -0.012, 0.0016)):
            hw_at = 0.0244 if pz < -0.068 else (0.0234 if pz < -0.040 else 0.0210)
            b.bm_cyl_x(bm_pins, s * (hw_at + 0.0004), s * (hw_at + 0.0022), py, pz, pr, segs=18)
    b.add_bmesh_obj("SideLockPlates", bm_plates, "DarkSteelTrim", bevel=0.0006, bevel_segs=2)
    b.add_bmesh_obj("LockplatePins",  bm_pins,   "ReceiverSteel")

    # Upper & Lower Steel Tangs flowing cleanly into the walnut pistol grip wrist
    bm_tangs = bmesh.new()
    b.bm_sweep_yz(bm_tangs, [
        (0.0135, -0.050), (0.0072, -0.026), (-0.0026, -0.002), (-0.0155, 0.020), (-0.0268, 0.039)
    ], half_w=0.0074, half_t=0.0022)
    b.bm_sweep_yz(bm_tangs, [
        (-0.0240, -0.024), (-0.0320, -0.002), (-0.0455, 0.020), (-0.0575, 0.038)
    ], half_w=0.0068, half_t=0.0020)
    b.add_bmesh_obj("SteelTangs", bm_tangs, "ReceiverSteel", bevel=0.0007, bevel_segs=2)

    # Top Break-Action Opening Lever (`ключ запирания`) — tucked low & flush along the tang!
    bm_lever = bmesh.new()
    b.bm_cyl_y(bm_lever, 0.0, 0.0095, 0.0168, -0.052, 0.0048, 0.0042, segs=22)
    b.bm_sweep_yz(bm_lever, [
        (0.0154, -0.052), (0.0118, -0.036), (0.0058, -0.020), (0.0004, -0.005), (-0.0028, 0.005)
    ], half_w=[0.0034, 0.0030, 0.0034, 0.0048, 0.0054], half_t=[0.0020, 0.0016, 0.0016, 0.0018, 0.0020])
    b.add_bmesh_obj("TopOpeningLever", bm_lever, "DarkSteelTrim", bevel=0.0007, bevel_segs=2)

    # Crisp Tang Safety Slider (low machined steel thumb button)
    b.add_box("TangSafety", 0.0, -0.0095, 0.016, 0.0052, 0.0022, 0.0085, "DarkSteelTrim", bevel=0.0006, segs=2, rot=(0.48, 0.0, 0.0))

    # RAZOR-CRISP EXTERNAL S-CURVED HAMMERS (`курки ТОЗ-66`) — 100% hard-surface machined steel!
    bm_hammers = bmesh.new()
    bm_hammer_boss = bmesh.new()
    for s in (-1, 1):
        hx = s * 0.0262
        # Circular machined hammer axle boss & retaining screw
        b.bm_cyl_x(bm_hammer_boss, s * 0.0230, s * 0.0290, -0.0055, -0.056, 0.0050, segs=24)
        b.bm_cyl_x(bm_hammer_boss, s * 0.0286, s * 0.0298, -0.0055, -0.056, 0.0022, segs=16)
        # Sleek 8-station S-curved gunsmith hammer arm ending in an angled thumb spur
        b.bm_sweep_yz(bm_hammers, [
            (-0.0055, -0.056),
            ( 0.0025, -0.058),
            ( 0.0098, -0.055),
            ( 0.0155, -0.050),
            ( 0.0180, -0.043),
            ( 0.0172, -0.036),
            ( 0.0185, -0.030),
            ( 0.0208, -0.024),
        ], half_w=[0.0026, 0.0023, 0.0020, 0.0019, 0.0020, 0.0022, 0.0026, 0.0030],
           half_t=[0.0026, 0.0023, 0.0020, 0.0019, 0.0018, 0.0018, 0.0018, 0.0015], x_center=hx)
    b.add_bmesh_obj("ExternalHammers", bm_hammers, "DarkSteelTrim", bevel=0.0005, bevel_segs=2)
    b.add_bmesh_obj("HammerBosses",    bm_hammer_boss, "ReceiverSteel")

    # Crisp Steel Trigger Guard Bow + Twin Blued Triggers!
    bm_guard = bmesh.new()
    b.bm_sweep_yz(bm_guard, [
        (-0.024, -0.088),
        (-0.038, -0.084),
        (-0.049, -0.072),
        (-0.054, -0.054),
        (-0.053, -0.036),
        (-0.044, -0.020),
        (-0.033, -0.006),
        (-0.046,  0.016),
    ], half_w=0.0064, half_t=0.0018)
    for tx, tz in ((0.0032, -0.064), (-0.0032, -0.045)):
        b.bm_sweep_yz(bm_guard, [
            (-0.025, tz), (-0.034, tz + 0.001), (-0.042, tz + 0.005), (-0.047, tz + 0.010)
        ], half_w=0.0017, half_t=0.0016, x_center=tx)
    b.add_bmesh_obj("TriggerGuardAndTriggers", bm_guard, "DarkSteelTrim", bevel=0.0005, bevel_segs=2)

    # 2) SMOOTH CONTOURED OILED-WALNUT SAWED-OFF PISTOL GRIP (Graceful curved bird's-head sawed-off arch!)
    bm_grip = bmesh.new()
    grip_ctrl = [
        (-0.018, 0.0184, -0.0245,  0.0015),
        (-0.004, 0.0174, -0.0315, -0.0025),
        ( 0.010, 0.0172, -0.0400, -0.0078),
        ( 0.024, 0.0178, -0.0495, -0.0148),
        ( 0.040, 0.0188, -0.0608, -0.0235),
        ( 0.056, 0.0198, -0.0725, -0.0332),
        ( 0.072, 0.0208, -0.0842, -0.0435),
        ( 0.086, 0.0214, -0.0945, -0.0535),
        ( 0.100, 0.0216, -0.1025, -0.0630),
        ( 0.110, 0.0210, -0.1065, -0.0695),
    ]

    def make_grip_ring(z, hw, yb, yt, pad_x=0.0, pad_y=0.0, n_pts=18):
        ym = (yb + yt) * 0.5
        rx = hw + pad_x
        ry = (yt - yb) * 0.5 + pad_y
        ring = []
        for i in range(n_pts):
            a = i * math.tau / n_pts
            ca, sa = math.cos(a), math.sin(a)
            p = 2.55
            d = (abs(ca) ** p + abs(sa) ** p) ** (1.0 / p)
            ring.append((rx * ca / d, ym + ry * sa / d, z))
        return ring

    b.bm_loft(bm_grip, [make_grip_ring(*st) for st in grip_ctrl], cap_start=True, cap_end=True)
    b.add_bmesh_obj("WalnutPistolGrip", bm_grip, "DarkWalnut", bevel=0.0012, bevel_segs=3, bevel_angle_deg=28.0)

    # Crisp Machined Steel Sawed-Off Butt Cap (ZERO glued rectangular side stickers!)
    bm_butt = bmesh.new()
    b.bm_loft(bm_butt, [
        make_grip_ring(0.108, 0.0214, -0.1055, -0.0685, pad_x=0.0005, pad_y=0.0005),
        make_grip_ring(0.114, 0.0206, -0.1078, -0.0720, pad_x=0.0004, pad_y=0.0004),
        make_grip_ring(0.116, 0.0182, -0.1088, -0.0740, pad_x=0.0,    pad_y=0.0),
    ], cap_start=True, cap_end=True)
    b.add_bmesh_obj("SteelButtCap", bm_butt, "DarkSteelTrim", bevel=0.0008, bevel_segs=2)

    # =========================================================================
    # JOIN ALL PARTS + BAKE 24-RAY BVHTREE AMBIENT OCCLUSION & CURVATURE UVs!
    # (EXACT `build_smooth_uzi.py` Studio Pipeline!)
    # =========================================================================
    bpy.ops.object.select_all(action="DESELECT")
    for o in b.objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = b.objs[0]
    bpy.ops.object.join()
    gun = bpy.context.active_object
    gun.name = "Shotgun_SawedOff_90s"
    mesh = gun.data

    # Build BVHTree in local space for 24-ray hemisphere AO + local convexity curvature
    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bmesh.ops.triangulate(bm_ao, faces=bm_ao.faces)
    bvh = BVHTree.FromBMesh(bm_ao)
    bm_ao.free()

    n_rays = 24
    golden = math.pi * (3.0 - math.sqrt(5.0))
    local_dirs = []
    for k in range(n_rays):
        zc = (k + 0.5) / n_rays
        r_h = math.sqrt(max(0.0, 1.0 - zc * zc))
        theta = golden * k
        local_dirs.append(V((math.cos(theta) * r_h, math.sin(theta) * r_h, zc)).normalized())

    verts = mesh.vertices
    v_coords = [v.co.copy() for v in verts]
    neighbors = [[] for _ in range(len(verts))]
    for edge in mesh.edges:
        a, c = edge.vertices[0], edge.vertices[1]
        neighbors[a].append(c)
        neighbors[c].append(a)

    max_dist = 0.085
    vert_u = [0.58] * len(verts)
    vert_col = [1.0] * len(verts)

    for idx, v in enumerate(verts):
        p = v.co
        n = v.normal.normalized() if v.normal.length > 1e-6 else V((0.0, 0.0, 1.0))
        up = V((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else V((1.0, 0.0, 0.0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()

        origin = p + n * 0.0004
        occ = 0.0
        for ld in local_dirs:
            d = (t1 * ld.x + t2 * ld.y + n * ld.z).normalized()
            hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, max_dist)
            if hit_loc is not None and hit_dist is not None:
                occ += (1.0 - min(1.0, hit_dist / max_dist)) ** 1.35
        ao = max(0.0, 1.0 - (occ / n_rays) * 1.12)

        curv = 0.0
        if neighbors[idx]:
            acc = 0.0
            for nb in neighbors[idx]:
                dv = v_coords[nb] - p
                if dv.length > 1e-6:
                    acc += dv.normalized().dot(n)
            mean_dot = acc / len(neighbors[idx])
            curv = max(-0.22, min(0.28, -mean_dot * 0.65))

        vert_u[idx] = min(0.97, max(0.03, 0.08 + 0.56 * (ao ** 1.28) + curv))
        vert_col[idx] = min(1.0, max(0.18, 0.18 + 0.82 * (ao ** 1.22)))

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    col_attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")

    # Map UV.u = baked AO+curvature, UV.v = smooth coordinate along shotgun length (y in Blender)
    for poly in mesh.polygons:
        poly.use_smooth = True
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            v_co = verts[vi].co
            tu = vert_u[vi]
            tv = min(0.98, max(0.02, (v_co.y + 0.14) / 0.44))
            uv_layer.data[li].uv = (tu, tv)
            c = vert_col[vi]
            col_attr.data[li].color = (c, c, c, 1.0)

    mesh.update()

    # Verify break-action MovingIslands split before exporting!
    min_z = min(-v.co.y for v in verts)
    max_z = max(-v.co.y for v in verts)
    print(f"Shotgun v4 bounds: Z=[{min_z:.4f}, {max_z:.4f}] Polys={len(mesh.polygons)} Mats={len(mesh.materials)}")

    out_path = OUT_MODELS / "weapon_shotgun_90s.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_normals=True,
        export_texcoords=True,
        export_attributes=True,
        export_active_vertex_color_when_no_material=True,
        export_vertex_color="ACTIVE",
        export_animations=False,
    )
    print("Exported AAA Hard-Surface weapon_shotgun_90s.glb:", out_path, os.path.getsize(out_path))


def render_shotgun_icon():
    """Render crisp 512x512 transparent hotbar/catalog icon `assets/icons/icon_slot_4_shotgun.png`."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "weapon_shotgun_90s.glb"))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    obj = meshes[0]
    # Apply Raylib showcase gamma preview so icon matches in-game brightness
    for m in obj.data.materials:
        if not m or not m.use_nodes:
            continue
        tree = m.node_tree
        bsdf = tree.nodes.get("Principled BSDF")
        if not bsdf:
            continue
        for link in list(tree.links):
            if link.to_node == bsdf and link.to_socket.name == "Base Color":
                src = link.from_socket
                tree.links.remove(link)
                gn = tree.nodes.new("ShaderNodeGamma")
                gn.inputs["Gamma"].default_value = 0.52
                tree.links.new(src, gn.inputs["Color"])
                tree.links.new(gn.outputs["Color"], bsdf.inputs["Base Color"])
                break

    bbox = [obj.matrix_world @ V(c) for c in obj.bound_box]
    min_v = V((min(v.x for v in bbox), min(v.y for v in bbox), min(v.z for v in bbox)))
    max_v = V((max(v.x for v in bbox), max(v.y for v in bbox), max(v.z for v in bbox)))
    obj.location -= (min_v + max_v) * 0.5
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    direction = V((-1.0, -0.08, 0.12))
    rotation = (-direction).to_track_quat("-Z", "Y")
    rotation = rotation @ Quaternion(V((0, 0, 1)), math.radians(14.0))
    projected = [rotation.inverted() @ (obj.matrix_world @ v.co) for v in obj.data.vertices]
    low = V((min(v.x for v in projected), min(v.y for v in projected), 0))
    high = V((max(v.x for v in projected), max(v.y for v in projected), 0))
    obj.location -= rotation @ ((low + high) * 0.5)
    span = max(high.x - low.x, high.y - low.y)

    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 512
    sc.render.resolution_y = 512
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = "PNG"
    sc.render.image_settings.color_mode = "RGBA"

    cd = bpy.data.cameras.new("IconCam")
    cd.type = "ORTHO"
    cd.ortho_scale = span * 1.08
    co = bpy.data.objects.new("IconCam", cd)
    sc.collection.objects.link(co)
    co.location = direction.normalized() * 2.5
    co.rotation_mode = "QUATERNION"
    co.rotation_quaternion = rotation
    sc.camera = co

    for lname, lpos, energy, col in [
        ("SunKey",  (-2.2, -1.8, 2.2), 4.6, (1.0, 0.94, 0.85)),
        ("SunFill", (-1.8,  2.2, 1.4), 2.8, (0.82, 0.90, 1.0)),
        ("SunRim",  ( 2.0,  0.5, 2.4), 3.6, (1.0, 0.86, 0.65)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        sc.collection.objects.link(lobj)
        lobj.location = lpos
        lobj.rotation_euler = (V((0, 0, 0)) - V(lpos)).to_track_quat("-Z", "Y").to_euler()

    OUT_ICONS.mkdir(parents=True, exist_ok=True)
    icon_path = OUT_ICONS / "icon_slot_4_shotgun.png"
    sc.render.filepath = str(icon_path)
    bpy.ops.render.render(write_still=True)
    print("Rendered 512x512 icon_slot_4_shotgun.png:", icon_path, os.path.getsize(icon_path))


if __name__ == "__main__":
    build_sawed_off_v4()
    render_shotgun_icon()
