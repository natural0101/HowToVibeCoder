"""PASS 2 — Museum-grade 1990s Soviet-Russian Sawed-Off Coach Gun (ТОЗ-66 / БМ-16 Обрез) ->
assets/models/weapon_shotgun_90s.glb.
Features:
  - Stepped cold-blued twin side-by-side barrels with crowned steel muzzles, deep dark bores & brass chamber rims
  - Machined ventilated top sight rib (11 stanchions) + silver ramp + turned solid-brass pedestal bead sight
  - Conformal lofted 1990s Soviet-blue electrical tape field-wrap hugging exact barrel+forend silhouette (zero blocky cube!)
  - Sculpted scalloped teardrop side-lock plates with curved brass filigree scrollwork & countersunk slotted screws
  - Sleek curved S-profile rabbit-ear external hammers with knurled spurs, top opening lever & subtle brass tang safety
  - Contoured walnut forend with Deeley steel latch & lofted organic walnut pistol grip with flush checkered side panels
  - Flush conformal beveled brass pommel end-cap + steel lanyard ring (zero misaligned boxes!)
  - Compact low-profile dark-leather & brass 2-shell side-saddle nestled cleanly beside the near breech flank (never blocking sights!)
  - Preserves exact MovingIslands break-action hinge split (minZ < -0.100 hinges around (0, -0.013, -0.127)) & --grip-test!
"""
import math
import shutil
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector as V, Matrix

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "assets/models"


def xyz(p):
    """Game local coords (X right, Y up, +Z toward butt, -Z muzzle) -> Blender Z-up (X, -Z, Y)."""
    return (p[0], -p[2], p[1])


def make_mat(name, hex_rgb, roughness=0.45, metallic=0.0):
    m = bpy.data.materials.new(name)
    rgb = bytes.fromhex(hex_rgb)
    lin = tuple(((c / 255.0 + 0.055) / 1.055) ** 2.4 for c in rgb) + (1.0,)
    m.diffuse_color = lin
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = lin
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    return m


class GunBuilder:
    def __init__(self):
        self.mats = {
            "walnut":       make_mat("DarkWalnut",       "482815", roughness=0.52, metallic=0.0),
            "walnut_chk":   make_mat("CheckeredWalnut",  "2b160b", roughness=0.76, metallic=0.0),
            "walnut_hi":    make_mat("OiledWalnut",      "663a1f", roughness=0.42, metallic=0.0),
            "blued":        make_mat("BluedSteel",       "1b2028", roughness=0.24, metallic=0.90),
            "steel_worn":   make_mat("ReceiverSteel",    "3b424d", roughness=0.28, metallic=0.88),
            "steel_dark":   make_mat("DarkSteelTrim",    "21262e", roughness=0.34, metallic=0.84),
            "engraving":    make_mat("SilverEngraving",  "7f8894", roughness=0.22, metallic=0.92),
            "brass":        make_mat("BrassBead",        "d4a34a", roughness=0.20, metallic=0.95),
            "brass_aged":   make_mat("AgedBrass",        "946c2e", roughness=0.34, metallic=0.88),
            "bore":         make_mat("BoreBlack",        "060608", roughness=0.94, metallic=0.0),
            "tape_blue":    make_mat("SovietBlueTape",   "1c3e6e", roughness=0.40, metallic=0.0),
            "shell_red":    make_mat("ShellHullRed",     "9a1b20", roughness=0.36, metallic=0.0),
            "leather":      make_mat("LeatherSaddle",    "2a160e", roughness=0.66, metallic=0.0),
        }
        self.bms = {k: bmesh.new() for k in self.mats}

    def box(self, key, cx, cy, cz, w, h, d, bevel=0.0, rot_x=0.0, rot_y=0.0, rot_z=0.0):
        bm = self.bms[key]
        res = bmesh.ops.create_cube(bm, size=1.0)
        verts = res["verts"]
        R = Matrix.Rotation(rot_z, 3, 'Z') @ Matrix.Rotation(rot_y, 3, 'Y') @ Matrix.Rotation(rot_x, 3, 'X')
        for v in verts:
            local = V((v.co.x * w, v.co.y * h, v.co.z * d))
            world = V((cx, cy, cz)) + R @ local
            v.co = V(xyz(world))
        if bevel > 0.0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=2, affect="EDGES", clamp_overlap=True)

    def tube_z(self, key, cx, cy, z0, z1, r_out0, r_out1=None, r_in=0.0, segs=26, cap0=True, cap1=True):
        if r_out1 is None:
            r_out1 = r_out0
        bm = self.bms[key]
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

    def cyl_x(self, key, x0, x1, cy, cz, r, segs=18):
        bm = self.bms[key]
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

    def cyl_y(self, key, cx, y0, y1, cz, r0_rad, r1_rad=None, segs=16):
        if r1_rad is None:
            r1_rad = r0_rad
        bm = self.bms[key]
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

    def loft_profile(self, key, rings_xyz, cap_start=True, cap_end=True):
        bm = self.bms[key]
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

    def sweep_strip_yz(self, key, pts_yz, half_w, half_t, x_center=0.0):
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
            prof = [
                (x_center - hw,        y + ny * ht * 0.72, z + nz * ht * 0.72),
                (x_center - hw * 0.65, y + ny * ht,        z + nz * ht),
                (x_center + hw * 0.65, y + ny * ht,        z + nz * ht),
                (x_center + hw,        y + ny * ht * 0.72, z + nz * ht * 0.72),
                (x_center + hw,        y - ny * ht * 0.72, z - nz * ht * 0.72),
                (x_center + hw * 0.65, y - ny * ht,        z - nz * ht),
                (x_center - hw * 0.65, y - ny * ht,        z - nz * ht),
                (x_center - hw,        y - ny * ht * 0.72, z - nz * ht * 0.72),
            ]
            rings.append(prof)
        self.loft_profile(key, rings, cap_start=True, cap_end=True)

    def plate_yz(self, key, x_in, x_out, poly_yz):
        """Extrudes a custom 2D curved polygon in the YZ plane between x_in and x_out (for scalloped lockplates!)."""
        bm = self.bms[key]
        v_in = [bm.verts.new(xyz((x_in, y, z))) for (y, z) in poly_yz]
        v_out = [bm.verts.new(xyz((x_out, y, z))) for (y, z) in poly_yz]
        n = len(poly_yz)
        if x_out > x_in:
            bm.faces.new(v_out)
            bm.faces.new(list(reversed(v_in)))
            for i in range(n):
                ni = (i + 1) % n
                bm.faces.new((v_in[i], v_in[ni], v_out[ni], v_out[i]))
        else:
            bm.faces.new(list(reversed(v_out)))
            bm.faces.new(v_in)
            for i in range(n):
                ni = (i + 1) % n
                bm.faces.new((v_out[i], v_out[ni], v_in[ni], v_in[i]))


def conformal_tape_ring(z_center, half_dz, tilt_z, pad=0.0):
    """Computes a smooth 16-point conformal cross-section tightly hugging BOTH barrels on top
    and the curved walnut forend underneath — ZERO square plastic box!"""
    pts = [
        (-0.011,  0.0138),
        ( 0.000,  0.0156),
        ( 0.011,  0.0138),
        ( 0.0218, 0.0085),
        ( 0.0235, 0.0010),
        ( 0.0222, -0.0075),
        ( 0.0190, -0.0165),
        ( 0.0125, -0.0228),
        ( 0.000,  -0.0245),
        (-0.0125, -0.0228),
        (-0.0190, -0.0165),
        (-0.0222, -0.0075),
        (-0.0235, 0.0010),
        (-0.0218, 0.0085),
    ]
    cx, cy = 0.0, -0.004
    ring0, ring1 = [], []
    for (x, y) in pts:
        dx, dy = x - cx, y - cy
        L = math.hypot(dx, dy) + 1e-6
        px, py = x + dx / L * pad, y + dy / L * pad
        skew = tilt_z * (px / 0.024)
        ring0.append((px, py, z_center - half_dz + skew))
        ring1.append((px, py, z_center + half_dz + skew))
    return [ring0, ring1]


def build_shotgun(g: GunBuilder):
    # =========================================================================
    # PART A: HINGED BARREL & FOREND ASSEMBLY (ALL ISLANDS HAVE minZ < -0.100!)
    # =========================================================================
    z_muzzle = -0.315
    z_breech = -0.102
    by = 0.0025
    bx_off = 0.0112

    for s in (-1, 1):
        bx = s * bx_off
        # Stepped cold-blued twin barrels (heavy breech chambers -> graceful tapered muzzle)
        g.tube_z("blued", bx, by, z_muzzle, -0.168, r_out0=0.0102, r_out1=0.0112, r_in=0.0082, segs=28)
        g.tube_z("blued", bx, by, -0.168, z_breech, r_out0=0.0114, r_out1=0.0124, r_in=0.0086, segs=28)
        # Crowned brushed-steel muzzle lip
        g.tube_z("steel_worn", bx, by, z_muzzle - 0.001, z_muzzle + 0.004, r_out0=0.0103, r_out1=0.0105, r_in=0.0083, segs=28)
        # Deep dark bore core
        g.tube_z("bore", bx, by, z_muzzle + 0.008, z_breech - 0.012, r_out0=0.0081, r_out1=0.0081, segs=18)
        # Exposed 12-gauge brass shell rims & primers visible inside the breech on break-action reload!
        g.tube_z("brass", bx, by, z_breech - 0.004, z_breech + 0.001, r_out0=0.0094, r_out1=0.0096, segs=24)
        g.tube_z("steel_dark", bx, by, z_breech + 0.0005, z_breech + 0.0015, r_out0=0.0026, r_out1=0.0026, segs=14)
        # Engraved aged-brass decorative breech collar band
        g.tube_z("brass_aged", bx, by, z_breech - 0.013, z_breech - 0.0085, r_out0=0.0126, r_out1=0.0126, segs=24)

    # Machined Ventilated Top Sight Rib (11 recessed vents) + Silver Ramp + Solid Brass Front Bead!
    g.box("steel_dark", 0.0, by + 0.0126, -0.208, 0.0080, 0.0030, 0.208, bevel=0.0006)
    g.box("steel_worn", 0.0, by + 0.0143, -0.208, 0.0058, 0.0014, 0.204, bevel=0.0003)
    for i in range(11):
        pz = -0.304 + i * 0.0195
        g.box("blued", 0.0, by + 0.0096, pz, 0.0066, 0.0046, 0.0070, bevel=0.0005)
    g.box("steel_dark", 0.0, by + 0.0154, -0.305, 0.0050, 0.0025, 0.012, bevel=0.0005)
    g.cyl_y("brass", 0.0, by + 0.0164, by + 0.0205, -0.306, 0.0018, 0.0022, segs=16)

    # Lower barrel joining under-rib + forward steel barrel lug + monoblock hinge lug
    g.box("steel_dark", 0.0, by - 0.0108, -0.210, 0.009, 0.0055, 0.204, bevel=0.0008)
    g.box("steel_worn", 0.0, -0.013, -0.127, 0.016, 0.016, 0.024, bevel=0.002)

    # Sculpted Ergonomic Walnut Splinter/Beavertail Forend (Z = -0.228 .. -0.110)
    forend_stations = [
        (-0.228, 0.015, -0.014, -0.004),
        (-0.212, 0.021, -0.022, -0.001),
        (-0.180, 0.023, -0.025,  0.000),
        (-0.140, 0.022, -0.024, -0.001),
        (-0.112, 0.019, -0.021, -0.003),
    ]
    f_rings = []
    for z, hw, y_bot, y_top in forend_stations:
        ym = (y_bot + y_top) * 0.5
        f_rings.append([
            (-hw * 0.92, y_top, z),
            ( hw * 0.92, y_top, z),
            ( hw,        ym + 0.002, z),
            ( hw * 0.84, y_bot + 0.004, z),
            ( hw * 0.38, y_bot, z),
            (-hw * 0.38, y_bot, z),
            (-hw * 0.84, y_bot + 0.004, z),
            (-hw,        ym + 0.002, z),
        ])
    g.loft_profile("walnut", f_rings, cap_start=True, cap_end=True)
    # Recessed dark checkered forend grip panels + steel forend nose cap + Deeley latch underneath
    for s in (-1, 1):
        g.box("walnut_chk", s * 0.0225, -0.013, -0.172, 0.0020, 0.010, 0.054, bevel=0.0005)
    g.box("steel_dark", 0.0, -0.011, -0.229, 0.026, 0.011, 0.006, bevel=0.0014)
    g.box("steel_worn", 0.0, -0.025, -0.168, 0.009, 0.0030, 0.030, bevel=0.0005)

    # CONFORMAL LOFTED 1990s SOVIET-BLUE ELECTRICAL TAPE FIELD-WRAP (tightly hugging round barrels + forend!)
    for k, (zc, tilt, pad) in enumerate([
        (-0.224,  0.0022, 0.0006),
        (-0.217, -0.0018, 0.0011),
        (-0.210,  0.0020, 0.0008),
        (-0.203, -0.0015, 0.0005),
    ]):
        g.loft_profile("tape_blue", conformal_tape_ring(zc, 0.0034, tilt, pad), cap_start=True, cap_end=True)

    # SLEEK LOW-PROFILE 2-SHELL LEATHER & BRASS SIDE-SADDLE ON NEAR FLANK (+X in local -> LEFT visible side in FPS!)
    # Oriented ALONG the breech flank (Z = -0.166 .. -0.116, Y = +0.001 -> sits BELOW the sight rib, ZERO view block!)
    g.box("leather",    0.0242, 0.000, -0.141, 0.0036, 0.019, 0.048, bevel=0.0010)
    g.box("brass_aged", 0.0258, 0.000, -0.141, 0.0014, 0.011, 0.044, bevel=0.0004)
    for sz in (-0.153, -0.129):
        # Compact angled spare shells sitting low & tight along the breech side-saddle (top rim BELOW sight line!)
        g.cyl_y("leather",   0.0300, -0.006, 0.007, sz, 0.0088, 0.0088, segs=16)
        g.cyl_y("shell_red", 0.0300, -0.012, 0.010, sz, 0.0078, 0.0078, segs=18)
        g.cyl_y("brass",     0.0300,  0.008, 0.0155, sz, 0.0082, 0.0084, segs=18)
        g.cyl_y("brass",     0.0300,  0.0155, 0.0168, sz, 0.0090, 0.0090, segs=18)

    # =========================================================================
    # PART B: STATIC ENGRAVED RECEIVER, SLEEK HAMMERS & CONFORMAL WALNUT GRIP
    # EVERY VERTEX MUST HAVE Z >= -0.098 (STRICTLY NON-HINGING!)
    # =========================================================================

    # 1) Sculpted Case-Hardened Steel Receiver Body (Z = -0.097 .. -0.014)
    rec_stations = [
        (-0.097, 0.0244, -0.026,  0.0142),
        (-0.082, 0.0246, -0.028,  0.0138),
        (-0.056, 0.0236, -0.029,  0.0112),
        (-0.032, 0.0212, -0.027,  0.0040),
        (-0.014, 0.0184, -0.024, -0.0032),
    ]
    r_rings = []
    for z, hw, yb, yt in rec_stations:
        r_rings.append([
            (-hw * 0.78, yt, z),
            ( hw * 0.78, yt, z),
            ( hw,        yt - 0.004, z),
            ( hw,        yb + 0.005, z),
            ( hw * 0.76, yb, z),
            (-hw * 0.76, yb, z),
            (-hw,        yb + 0.005, z),
            (-hw,        yt - 0.004, z),
        ])
    g.loft_profile("steel_worn", r_rings, cap_start=True, cap_end=True)

    # Twin scalloped breech standing-wall bolsters + Authentic Curved Teardrop Side-Lock Plates (`ТОЗ-66`)!
    teardrop_outer = [
        ( 0.006, -0.093),
        ( 0.006, -0.056),
        ( 0.001, -0.034),
        (-0.006, -0.018),
        (-0.016, -0.022),
        (-0.023, -0.044),
        (-0.023, -0.088),
        (-0.014, -0.095),
    ]
    teardrop_inner = [
        ( 0.003, -0.088),
        ( 0.003, -0.058),
        (-0.001, -0.038),
        (-0.007, -0.024),
        (-0.014, -0.027),
        (-0.019, -0.046),
        (-0.019, -0.084),
        (-0.012, -0.090),
    ]
    for s in (-1, 1):
        g.tube_z("steel_worn", s * bx_off, by, -0.097, -0.072, r_out0=0.0128, r_out1=0.0122, segs=22)
        g.tube_z("brass",      s * bx_off, by, -0.098, -0.094, r_out0=0.0034, r_out1=0.0034, segs=14)
        # Curved teardrop blued-steel lockplate + subtle recessed silver-engraved inlay panel
        g.plate_yz("steel_dark", s * 0.0225, s * 0.0252, teardrop_outer)
        g.plate_yz("engraving",  s * 0.0248, s * 0.0260, teardrop_inner)
        # Sculpted curved gold filigree scrollwork arcs along the lockplate!
        g.sweep_strip_yz("brass_aged", [
            (-0.003, -0.084), (0.000, -0.068), (-0.006, -0.052), (-0.013, -0.038), (-0.010, -0.028)
        ], half_w=0.0009, half_t=0.0012, x_center=s * 0.0262)
        g.sweep_strip_yz("brass_aged", [
            (-0.015, -0.082), (-0.018, -0.066), (-0.014, -0.050), (-0.006, -0.038)
        ], half_w=0.0008, half_t=0.0010, x_center=s * 0.0262)
        # 3 machined countersunk slotted lockplate pins
        for pz, py, pr in ((-0.082, -0.011, 0.0024), (-0.060, -0.007, 0.0030), (-0.036, -0.011, 0.0021)):
            g.cyl_x("steel_dark", s * 0.0245, s * 0.0268, py, pz, pr, segs=14)
            g.box("bore", s * 0.0269, py, pz, 0.0005, 0.0006, pr * 1.55)

    # Upper Steel Tang sweeping smoothly into the walnut grip wrist
    g.sweep_strip_yz("steel_worn", [
        (0.0112, -0.042), (0.0058, -0.022), (-0.0040, -0.002), (-0.0160, 0.020), (-0.0265, 0.038)
    ], half_w=0.0076, half_t=0.0025)

    # Sculpted Top Break-Action Opening Lever (`ключ запирания`) + Sleek Flush Brass Tang Safety
    g.cyl_y("steel_dark", 0.0, 0.008, 0.0185, -0.048, 0.0050, 0.0046, segs=16)
    g.sweep_strip_yz("steel_dark", [
        (0.0168, -0.048), (0.0148, -0.032), (0.0098, -0.014), (0.0042, 0.003)
    ], half_w=[0.0035, 0.0034, 0.0042, 0.0058], half_t=0.0020)
    # Sleek recessed brass safety slider aligned with tang slope (+0.48 rad!)
    g.box("brass_aged", 0.0, -0.008, 0.015, 0.0052, 0.0022, 0.009, bevel=0.0005, rot_x=0.48)

    # SLEEK CURVED TWIN EXTERNAL RABBIT-EAR HAMMERS (`курки ТОЗ-66`)
    for s in (-1, 1):
        hx = s * 0.0252
        g.cyl_x("steel_dark", s * 0.022, s * 0.0290, -0.005, -0.056, 0.0055, segs=16)
        g.cyl_x("brass_aged", s * 0.0278, s * 0.0298, -0.005, -0.056, 0.0022, segs=12)
        g.sweep_strip_yz("steel_dark", [
            (-0.004, -0.056),
            ( 0.008, -0.058),
            ( 0.019, -0.052),
            ( 0.024, -0.041),
            ( 0.022, -0.029),
            ( 0.025, -0.022),
        ], half_w=[0.0028, 0.0026, 0.0024, 0.0026, 0.0028, 0.0034], half_t=0.0022, x_center=hx)
        g.box("engraving", hx, 0.0252, -0.0215, 0.0068, 0.0022, 0.0058, bevel=0.0005, rot_x=0.32)

    # Sculpted Trigger Guard Bow + DUAL Staggered Curved Triggers!
    g.sweep_strip_yz("steel_dark", [
        (-0.026, -0.088),
        (-0.044, -0.082),
        (-0.055, -0.062),
        (-0.055, -0.036),
        (-0.044, -0.018),
        (-0.032, -0.004),
        (-0.048,  0.022),
    ], half_w=0.0066, half_t=0.0019)
    for tx, tz in ((0.0032, -0.062), (-0.0032, -0.044)):
        g.sweep_strip_yz("brass_aged", [
            (-0.027, tz), (-0.038, tz + 0.002), (-0.046, tz + 0.007)
        ], half_w=0.0017, half_t=0.0017, x_center=tx)

    # 2) MASTERCRAFTED SAWED-OFF WALNUT PISTOL GRIP + CONFORMAL CHECKERED PANELS + FLUSH BRASS POMMEL!
    grip_stations = [
        (-0.018, 0.0184, -0.025,  0.003),
        ( 0.004, 0.0174, -0.038, -0.005),
        ( 0.028, 0.0179, -0.054, -0.018),
        ( 0.054, 0.0194, -0.072, -0.034),
        ( 0.082, 0.0212, -0.090, -0.052),
        ( 0.104, 0.0224, -0.103, -0.066),
        ( 0.111, 0.0216, -0.106, -0.070),
    ]
    def make_grip_ring(z, hw, yb, yt, pad_x=0.0, pad_y=0.0):
        ym = (yb + yt) * 0.5
        hw2 = hw + pad_x
        yt2, yb2 = yt + pad_y, yb - pad_y
        return [
            (-hw2 * 0.65, yt2, z),
            ( hw2 * 0.65, yt2, z),
            ( hw2,        ym + 0.006, z),
            ( hw2 * 0.96, ym - 0.006, z),
            ( hw2 * 0.62, yb2, z),
            (-hw2 * 0.62, yb2, z),
            (-hw2 * 0.96, ym - 0.006, z),
            (-hw2,        ym + 0.006, z),
        ]
    g_rings = [make_grip_ring(z, hw, yb, yt) for (z, hw, yb, yt) in grip_stations]
    g.loft_profile("walnut", g_rings, cap_start=True, cap_end=True)

    # CONFORMAL LOFTED CHECKERED WALNUT SIDE PANELS (100% flush to grip curve — ZERO floating paddle!)
    for s in (-1, 1):
        chk_rings = []
        for (z, hw, yb, yt) in grip_stations[2:6]:
            ym = (yb + yt) * 0.5
            h_half = (yt - yb) * 0.30
            x0, x1 = s * (hw - 0.0012), s * (hw + 0.0014)
            ring = [
                (x0, ym + h_half, z),
                (x1, ym + h_half * 0.85, z),
                (x1, ym - h_half * 0.85, z),
                (x0, ym - h_half, z),
            ]
            chk_rings.append(ring if s > 0 else list(reversed(ring)))
        g.loft_profile("walnut_chk", chk_rings, cap_start=True, cap_end=True)
        # Flush brass grip escutcheon medallion rivet
        g.cyl_x("brass", s * 0.0195, s * 0.0214, -0.056, 0.056, 0.0028, segs=14)

    # CONFORMAL LOFTED SOLID-BRASS SAWED-OFF POMMEL CAP (100% aligned with grip end — ZERO poking corner!)
    pommel_rings = [
        make_grip_ring(0.106, 0.0226, -0.104, -0.067, pad_x=0.0008, pad_y=0.0008),
        make_grip_ring(0.114, 0.0222, -0.108, -0.072, pad_x=0.0010, pad_y=0.0010),
        make_grip_ring(0.118, 0.0195, -0.109, -0.075, pad_x=0.0000, pad_y=0.0000),
    ]
    g.loft_profile("brass", pommel_rings, cap_start=True, cap_end=True)
    # Turned steel lanyard ring tucked neatly at the butt pommel
    g.cyl_x("steel_dark", -0.006, 0.006, -0.094, 0.120, 0.0034, segs=12)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    g = GunBuilder()
    build_shotgun(g)

    objs = []
    for key, bm in g.bms.items():
        if len(bm.verts) == 0:
            bm.free()
            continue
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        m = bpy.data.meshes.new(f"shotgun_{key}")
        bm.to_mesh(m)
        bm.free()
        smooth = key in ("blued", "steel_worn", "brass", "brass_aged", "walnut", "shell_red", "tape_blue", "leather")
        for poly in m.polygons:
            poly.use_smooth = smooth
        obj = bpy.data.objects.new(f"shotgun_{key}", m)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(g.mats[key])
        objs.append(obj)

    bpy.ops.object.select_all(action="DESELECT")
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    final_obj = bpy.context.object
    final_obj.name = final_obj.data.name = "ErgonomicWalnutGrip"

    out_path = OUT / "weapon_shotgun_90s.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_normals=True,
        export_texcoords=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest_dir = ROOT / sub
        if dest_dir.exists():
            shutil.copy2(out_path, dest_dir / "weapon_shotgun_90s.glb")
    print(f"EXPORTED PASS-2 HERO SHOTGUN {out_path} ({len(final_obj.data.polygons)} polys, {len(final_obj.data.materials)} mats)")


if __name__ == "__main__":
    main()
