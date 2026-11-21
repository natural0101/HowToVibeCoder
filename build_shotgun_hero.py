"""v3 TRUE 1990s SAWED-OFF COACH GUN (КОРОТКИЙ ОБРЕЗ БМ-16 / ТОЗ-66) -> assets/models/weapon_shotgun_90s.glb
ZERO toy clutter, ZERO flat Lego blocks:
  - Authentic short brutal sawed-off proportions (barrels cut short at Z=-0.244 right past the forend,
    exposing a raw hand-sawed steel muzzle ring — genuine ОБРЕЗ, not a long sporting shotgun!)
  - Real high-res photographic PBR textures embedded in GLB with clean box/cylindrical UV unwraps:
      * Dark oiled Russian walnut burl grain (`tex_shotgun_walnut`) + darkened diamond checkering
      * Weathered Soviet cold-blued brushed gunmetal steel (`tex_shotgun_blued_steel`)
      * Monochrome hand-etched Russian scroll filigree case-hardened receiver (`tex_shotgun_engraved_steel`)
  - Smooth Subdivision-Surface sculpted receiver, walnut grip, forend, and graceful slim curved S-hammers
  - Preserves exact MovingIslands break-action hinge split (minZ < -0.100 hinges around (0, -0.013, -0.127))
    and 100% --grip-test hand alignment!
"""
import math
import shutil
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector as V, Matrix

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "assets/models"
TEX_DIR = ROOT / "assets/textures"
ART_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")

WALNUT_SRC   = ART_DIR / "tex_shotgun_walnut_1795276839223.jpg"
BLUED_SRC    = ART_DIR / "tex_shotgun_blued_steel_1795276864172.jpg"
ENGRAVED_SRC = ART_DIR / "tex_shotgun_engraved_steel_1795276871963.jpg"


def xyz(p):
    """Game local coords (X right, Y up, +Z toward butt, -Z muzzle) -> Blender Z-up (X, -Z, Y)."""
    return (p[0], -p[2], p[1])


def prepare_textures():
    """Returns paths to prepared 1024x1024 PBR textures in assets/textures/ (generating via PIL if missing)."""
    TEX_DIR.mkdir(parents=True, exist_ok=True)
    paths = {
        "walnut":     TEX_DIR / "shotgun_walnut.png",
        "blued":      TEX_DIR / "shotgun_blued.png",
        "engraved":   TEX_DIR / "shotgun_engraved.png",
        "walnut_chk": TEX_DIR / "shotgun_walnut_checkered.png",
    }
    if all(p.exists() for p in paths.values()):
        return paths
    from PIL import Image, ImageDraw, ImageEnhance
    for key, src, bright, sat in [
        ("walnut",   WALNUT_SRC,   0.92, 1.05),
        ("blued",    BLUED_SRC,    0.82, 0.95),
        ("engraved", ENGRAVED_SRC, 0.88, 0.85),
    ]:
        im = Image.open(src).convert("RGB").resize((1024, 1024), Image.LANCZOS)
        im = ImageEnhance.Brightness(im).enhance(bright)
        im = ImageEnhance.Color(im).enhance(sat)
        im.save(paths[key])

    chk = Image.open(paths["walnut"]).convert("RGB")
    chk = ImageEnhance.Brightness(chk).enhance(0.68)
    overlay = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    for offset in range(-1024, 2048, 18):
        draw.line([(offset, 0), (offset + 1024, 1024)], fill=(10, 5, 2, 175), width=2)
        draw.line([(offset, 1024), (offset + 1024, 0)], fill=(10, 5, 2, 175), width=2)
        draw.line([(offset + 2, 0), (offset + 1026, 1024)], fill=(95, 62, 34, 65), width=1)
    chk = Image.alpha_composite(chk.convert("RGBA"), overlay).convert("RGB")
    chk.save(paths["walnut_chk"])
    return paths


def make_tex_mat(name, img_path=None, hex_rgb="FFFFFF", roughness=0.42, metallic=0.0):
    m = bpy.data.materials.new(name)
    rgb = bytes.fromhex(hex_rgb)
    lin = tuple(((c / 255.0 + 0.055) / 1.055) ** 2.4 for c in rgb) + (1.0,)
    m.diffuse_color = lin
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = lin
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if img_path is not None:
        tex_node = m.node_tree.nodes.new("ShaderNodeTexImage")
        tex_node.image = bpy.data.images.load(str(img_path), check_existing=True)
        m.node_tree.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
    return m


def apply_triplanar_uv(mesh_data, scale=4.2):
    """Assigns clean, distortion-free triplanar UVs to every face so wood grain & brushed metal
    flow naturally along the shotgun length without stretching!"""
    if not mesh_data.uv_layers:
        mesh_data.uv_layers.new(name="UVMap")
    uv_layer = mesh_data.uv_layers.active.data
    verts = mesh_data.vertices
    for poly in mesh_data.polygons:
        n = poly.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for li in poly.loop_indices:
            co = verts[mesh_data.loops[li].vertex_index].co
            # In Blender Z-up: co.y is along barrel length (-Z game), co.x is width, co.z is height
            if ax >= ay and ax >= az:
                u, v = co.y * scale, co.z * scale
            elif az >= ax and az >= ay:
                u, v = co.y * scale, co.x * scale
            else:
                u, v = co.x * scale, co.z * scale
            uv_layer[li].uv = (u + 0.5, v + 0.5)


class SawedOffBuilder:
    def __init__(self, tex_paths):
        self.mats = {
            "walnut":     make_tex_mat("DarkWalnut",      tex_paths["walnut"],     roughness=0.48, metallic=0.0),
            "walnut_chk": make_tex_mat("CheckeredWalnut", tex_paths["walnut_chk"], roughness=0.68, metallic=0.0),
            "blued":      make_tex_mat("BluedSteel",      tex_paths["blued"],      roughness=0.28, metallic=0.88),
            "receiver":   make_tex_mat("ReceiverSteel",   tex_paths["engraved"],   roughness=0.30, metallic=0.86),
            "steel_raw":  make_tex_mat("RawCutSteel",     tex_paths["blued"],      hex_rgb="9AA2AC", roughness=0.24, metallic=0.92),
            "steel_dark": make_tex_mat("DarkSteelTrim",   tex_paths["blued"],      hex_rgb="5A606A", roughness=0.34, metallic=0.84),
            "brass":      make_tex_mat("BrassBead",       None,                    hex_rgb="B88B42", roughness=0.24, metallic=0.92),
            "bore":       make_tex_mat("BoreBlack",       None,                    hex_rgb="050507", roughness=0.95, metallic=0.0),
        }
        self.parts = []  # list of (mat_key, bmesh, subdiv_level, uv_scale)

    def new_part(self, mat_key, subdiv=0, uv_scale=4.2):
        bm = bmesh.new()
        self.parts.append((mat_key, bm, subdiv, uv_scale))
        return bm

    @staticmethod
    def tube_z(bm, cx, cy, z0, z1, r_out0, r_out1=None, r_in=0.0, segs=36, cap0=True, cap1=True):
        if r_out1 is None:
            r_out1 = r_out0
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
    def cyl_x(bm, x0, x1, cy, cz, r, segs=18):
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
    def cyl_y(bm, cx, y0, y1, cz, r0_rad, r1_rad=None, segs=18):
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
    def loft(bm, rings_xyz, cap_start=True, cap_end=True):
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
    def sweep_yz(bm, pts_yz, half_w, half_t, x_center=0.0):
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
                (x_center - hw,        y + ny * ht * 0.65, z + nz * ht * 0.65),
                (x_center - hw * 0.60, y + ny * ht,        z + nz * ht),
                (x_center + hw * 0.60, y + ny * ht,        z + nz * ht),
                (x_center + hw,        y + ny * ht * 0.65, z + nz * ht * 0.65),
                (x_center + hw,        y - ny * ht * 0.65, z - nz * ht * 0.65),
                (x_center + hw * 0.60, y - ny * ht,        z - nz * ht),
                (x_center - hw * 0.60, y - ny * ht,        z - nz * ht),
                (x_center - hw,        y - ny * ht * 0.65, z - nz * ht * 0.65),
            ]
            rings.append(prof)
        SawedOffBuilder.loft(bm, rings, cap_start=True, cap_end=True)

    @staticmethod
    def box(bm, cx, cy, cz, w, h, d, bevel=0.0, rot_x=0.0):
        res = bmesh.ops.create_cube(bm, size=1.0)
        verts = res["verts"]
        R = Matrix.Rotation(rot_x, 3, 'X')
        for v in verts:
            local = V((v.co.x * w, v.co.y * h, v.co.z * d))
            v.co = V(xyz(V((cx, cy, cz)) + R @ local))
        if bevel > 0.0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=2, affect="EDGES", clamp_overlap=True)


def build_sawed_off(b: SawedOffBuilder):
    # =========================================================================
    # PART A: SHORT BRUTAL SAWED-OFF TWIN BARRELS & WALNUT FOREND (minZ < -0.100)
    # Cut brutal & short right past the walnut forend (z_muzzle = -0.244)!
    # =========================================================================
    z_muzzle = -0.244   # Genuine short ОБРЕЗ length! (Was -0.315 sporting shotgun!)
    z_breech = -0.102   # Strictly < -0.100 so MovingIslands hinges cleanly on reload
    by = 0.0025
    bx_off = 0.0112

    bm_blued = b.new_part("blued", subdiv=0, uv_scale=4.8)
    bm_raw   = b.new_part("steel_raw", subdiv=0, uv_scale=6.0)
    bm_bore  = b.new_part("bore", subdiv=0, uv_scale=2.0)
    bm_brass = b.new_part("brass", subdiv=0, uv_scale=4.0)
    bm_dark  = b.new_part("steel_dark", subdiv=0, uv_scale=5.2)

    for s in (-1, 1):
        bx = s * bx_off
        # Heavy forged twin barrels: stepped reinforced breech chamber -> tapered short sawed-off tube
        b.tube_z(bm_blued, bx, by, z_muzzle + 0.0025, -0.156, r_out0=0.0108, r_out1=0.0115, r_in=0.0084, segs=36)
        b.tube_z(bm_blued, bx, by, -0.156, z_breech, r_out0=0.0116, r_out1=0.0124, r_in=0.0086, segs=36)
        # Raw hand-sawed cold-steel cut face at the muzzle tip (iconic hacksaw sheen of a real обрез!)
        b.tube_z(bm_raw, bx, by, z_muzzle, z_muzzle + 0.0028, r_out0=0.0107, r_out1=0.0108, r_in=0.0084, segs=36)
        # Deep dark interior bore
        b.tube_z(bm_bore, bx, by, z_muzzle + 0.006, z_breech - 0.012, r_out0=0.0083, r_out1=0.0083, segs=20)
        # Authentic 12-gauge brass shell rims & primers recessed inside breech (visible during break-action reload!)
        b.tube_z(bm_brass, bx, by, z_breech - 0.004, z_breech + 0.0008, r_out0=0.0094, r_out1=0.0096, segs=24)
        b.tube_z(bm_dark,  bx, by, z_breech + 0.0004, z_breech + 0.0014, r_out0=0.0026, r_out1=0.0026, segs=14)

    # Low, sleek solid concave bridge-rib tucked FLUSH between the two barrels (NO tall sporting ladder!)
    rib_rings = [
        [(-0.0052, by + 0.0096, z_muzzle + 0.003), (0.0052, by + 0.0096, z_muzzle + 0.003),
         (0.0065, by + 0.0055, z_muzzle + 0.003), (-0.0065, by + 0.0055, z_muzzle + 0.003)],
        [(-0.0064, by + 0.0108, z_breech - 0.002), (0.0064, by + 0.0108, z_breech - 0.002),
         (0.0076, by + 0.0060, z_breech - 0.002), (-0.0076, by + 0.0060, z_breech - 0.002)],
    ]
    b.loft(bm_dark, rib_rings, cap_start=True, cap_end=True)
    # Subtle lower joining rib & underlug hinge block
    b.box(bm_dark, 0.0, by - 0.0095, -0.175, 0.0085, 0.005, 0.136, bevel=0.0008)
    b.box(bm_dark, 0.0, -0.013, -0.127, 0.016, 0.015, 0.022, bevel=0.0018)

    # Smooth Subdivision-Surface Contoured Oiled Walnut Forend (Z = -0.214 .. -0.108)
    bm_forend = b.new_part("walnut", subdiv=1, uv_scale=4.5)
    forend_stations = [
        (-0.214, 0.013, -0.012, -0.004),
        (-0.204, 0.020, -0.021, -0.001),
        (-0.176, 0.0228,-0.025,  0.000),
        (-0.140, 0.0220,-0.024, -0.001),
        (-0.110, 0.0195,-0.021, -0.003),
    ]
    f_rings = []
    for z, hw, yb, yt in forend_stations:
        ym = (yb + yt) * 0.5
        f_rings.append([
            (-hw * 0.90, yt, z), ( hw * 0.90, yt, z),
            ( hw, ym + 0.002, z), ( hw * 0.85, yb + 0.004, z),
            ( hw * 0.36, yb, z), (-hw * 0.36, yb, z),
            (-hw * 0.85, yb + 0.004, z), (-hw, ym + 0.002, z),
        ])
    b.loft(bm_forend, f_rings, cap_start=True, cap_end=True)
    # Subtle blued-steel forend tip cap & recessed Deeley forend release latch
    b.box(bm_dark, 0.0, -0.010, -0.216, 0.024, 0.010, 0.005, bevel=0.0014)
    b.box(bm_dark, 0.0, -0.0245, -0.165, 0.0085, 0.0026, 0.028, bevel=0.0005)

    # =========================================================================
    # PART B: SMOOTH SUBDIVIDED ENGRAVED RECEIVER, SLEEK HAMMERS & SAWED-OFF GRIP
    # EVERY VERTEX MUST HAVE Z >= -0.098 (STRICTLY NON-HINGING!)
    # =========================================================================

    # 1) Smooth Subdivided Forged Engraved Receiver (real monochrome gunsmith scroll texture!)
    bm_rec = b.new_part("receiver", subdiv=1, uv_scale=6.8)
    rec_stations = [
        (-0.097, 0.0240, -0.025,  0.0138),
        (-0.088, 0.0244, -0.027,  0.0140),
        (-0.068, 0.0240, -0.028,  0.0124),
        (-0.044, 0.0224, -0.027,  0.0076),
        (-0.022, 0.0196, -0.025, -0.0006),
        (-0.010, 0.0176, -0.023, -0.0045),
    ]
    r_rings = []
    for z, hw, yb, yt in rec_stations:
        ym = (yb + yt) * 0.5
        r_rings.append([
            (-hw * 0.72, yt, z), ( hw * 0.72, yt, z),
            ( hw, yt - 0.0045, z), ( hw, ym, z), ( hw, yb + 0.0055, z),
            ( hw * 0.70, yb, z), (-hw * 0.70, yb, z),
            (-hw, yb + 0.0055, z), (-hw, ym, z), (-hw, yt - 0.0045, z),
        ])
    b.loft(bm_rec, r_rings, cap_start=True, cap_end=True)

    # Rounded standing breech bolsters & countersunk machined lockplate pins (ZERO flat grey stickers!)
    bm_rec_crisp = b.new_part("receiver", subdiv=0, uv_scale=6.8)
    for s in (-1, 1):
        b.tube_z(bm_rec_crisp, s * bx_off, by, -0.097, -0.072, r_out0=0.0124, r_out1=0.0118, segs=26)
        b.tube_z(bm_brass,     s * bx_off, by, -0.098, -0.094, r_out0=0.0032, r_out1=0.0032, segs=14)
        # Subtle recessed dark-blued pivot pins (realistic Soviet TOZ-66 pin layout!)
        for pz, py, pr in ((-0.082, -0.011, 0.0020), (-0.060, -0.008, 0.0025), (-0.036, -0.012, 0.0018)):
            b.cyl_x(bm_dark, s * 0.0225, s * 0.0246, py, pz, pr, segs=16)

    # Upper Steel Tang flowing smoothly into the walnut grip wrist
    b.sweep_yz(bm_rec_crisp, [
        (0.0108, -0.044), (0.0056, -0.024), (-0.0042, -0.002), (-0.0165, 0.020), (-0.0270, 0.038)
    ], half_w=0.0070, half_t=0.0022)

    # Sleek Top Break-Action Opening Lever (`ключ запирания`) tucked low & tight along the tang!
    b.cyl_y(bm_dark, 0.0, 0.008, 0.0165, -0.048, 0.0046, 0.0042, segs=18)
    b.sweep_yz(bm_dark, [
        (0.0148, -0.048), (0.0132, -0.032), (0.0086, -0.015), (0.0036, 0.001)
    ], half_w=[0.0032, 0.0030, 0.0036, 0.0050], half_t=0.0018)
    # Subtle dark-steel tang safety slider (NO bright yellow block!)
    b.box(bm_dark, 0.0, -0.0075, 0.014, 0.0048, 0.0018, 0.008, bevel=0.0005, rot_x=0.48)

    # GRACEFUL, SLIM, SUBDIVIDED S-CURVED EXTERNAL HAMMERS (`курки ТОЗ-66`) — NO Lego blocks!
    bm_hammers = b.new_part("steel_dark", subdiv=1, uv_scale=6.0)
    for s in (-1, 1):
        hx = s * 0.0246
        b.cyl_x(bm_dark, s * 0.022, s * 0.0272, -0.006, -0.056, 0.0046, segs=18)
        b.sweep_yz(bm_hammers, [
            (-0.005, -0.056),
            ( 0.005, -0.058),
            ( 0.014, -0.054),
            ( 0.019, -0.046),
            ( 0.018, -0.036),
            ( 0.021, -0.029),
        ], half_w=[0.0022, 0.0020, 0.0018, 0.0019, 0.0021, 0.0026], half_t=0.0018, x_center=hx)

    # Sleek Dark-Steel Trigger Guard Bow + Twin Blued Triggers!
    b.sweep_yz(bm_dark, [
        (-0.025, -0.086),
        (-0.042, -0.080),
        (-0.053, -0.062),
        (-0.053, -0.038),
        (-0.042, -0.020),
        (-0.031, -0.005),
        (-0.048,  0.022),
    ], half_w=0.0060, half_t=0.0016)
    for tx, tz in ((0.0030, -0.062), (-0.0030, -0.044)):
        b.sweep_yz(bm_dark, [
            (-0.026, tz), (-0.037, tz + 0.002), (-0.045, tz + 0.007)
        ], half_w=0.0015, half_t=0.0015, x_center=tx)

    # 2) SMOOTH SUBDIVIDED OILED WALNUT SAWED-OFF PISTOL GRIP + FLUSH CHECKERED PANELS
    bm_grip = b.new_part("walnut", subdiv=2, uv_scale=4.5)
    grip_stations = [
        (-0.018, 0.0180, -0.025,  0.002),
        ( 0.004, 0.0172, -0.038, -0.006),
        ( 0.028, 0.0178, -0.054, -0.018),
        ( 0.054, 0.0192, -0.072, -0.034),
        ( 0.082, 0.0210, -0.090, -0.052),
        ( 0.104, 0.0218, -0.103, -0.066),
        ( 0.112, 0.0205, -0.106, -0.071),
    ]
    def make_grip_ring(z, hw, yb, yt, pad_x=0.0, pad_y=0.0):
        ym = (yb + yt) * 0.5
        hw2 = hw + pad_x
        yt2, yb2 = yt + pad_y, yb - pad_y
        return [
            (-hw2 * 0.65, yt2, z), ( hw2 * 0.65, yt2, z),
            ( hw2, ym + 0.006, z), ( hw2 * 0.96, ym - 0.006, z),
            ( hw2 * 0.62, yb2, z), (-hw2 * 0.62, yb2, z),
            (-hw2 * 0.96, ym - 0.006, z), (-hw2, ym + 0.006, z),
        ]
    b.loft(bm_grip, [make_grip_ring(z, hw, yb, yt) for (z, hw, yb, yt) in grip_stations], cap_start=True, cap_end=True)

    # Subtle flush laser-cut checkered walnut side panels hugging the exact organic grip curve!
    bm_chk = b.new_part("walnut_chk", subdiv=1, uv_scale=6.5)
    for s in (-1, 1):
        chk_rings = []
        for (z, hw, yb, yt) in grip_stations[2:6]:
            ym = (yb + yt) * 0.5
            hh = (yt - yb) * 0.28
            x0, x1 = s * (hw - 0.0014), s * (hw + 0.0008)
            ring = [(x0, ym + hh, z), (x1, ym + hh * 0.85, z), (x1, ym - hh * 0.85, z), (x0, ym - hh, z)]
            chk_rings.append(ring if s > 0 else list(reversed(ring)))
        b.loft(bm_chk, chk_rings, cap_start=True, cap_end=True)

    # Subtle dark cold-blued steel sawed-off butt cap (NO bright yellow gold pommel!)
    bm_cap = b.new_part("steel_dark", subdiv=1, uv_scale=5.0)
    b.loft(bm_cap, [
        make_grip_ring(0.108, 0.0214, -0.105, -0.068, pad_x=0.0005, pad_y=0.0005),
        make_grip_ring(0.114, 0.0206, -0.107, -0.072, pad_x=0.0005, pad_y=0.0005),
        make_grip_ring(0.116, 0.0180, -0.108, -0.074, pad_x=0.0, pad_y=0.0),
    ], cap_start=True, cap_end=True)


def main():
    tex_paths = prepare_textures()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    builder = SawedOffBuilder(tex_paths)
    build_sawed_off(builder)

    objs = []
    for idx, (mat_key, bm, subdiv, uv_scale) in enumerate(builder.parts):
        if len(bm.verts) == 0:
            bm.free()
            continue
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        m = bpy.data.meshes.new(f"part_{idx}_{mat_key}")
        bm.to_mesh(m)
        bm.free()
        obj = bpy.data.objects.new(f"part_{idx}_{mat_key}", m)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(builder.mats[mat_key])
        if subdiv > 0:
            mod = obj.modifiers.new("Subsurf", type="SUBSURF")
            mod.levels = subdiv
            mod.render_levels = subdiv
            bpy.context.view_layer.objects.active = obj
            obj.select_set(True)
            bpy.ops.object.modifier_apply(modifier=mod.name)
            obj.select_set(False)
        for poly in obj.data.polygons:
            poly.use_smooth = True
        apply_triplanar_uv(obj.data, scale=uv_scale)
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
        export_image_format="AUTO",
        export_animations=False,
        export_cameras=False,
        export_lights=False,
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest_dir = ROOT / sub
        if dest_dir.exists():
            shutil.copy2(out_path, dest_dir / "weapon_shotgun_90s.glb")
    print(f"EXPORTED V3 PBR SAWED-OFF {out_path} ({len(final_obj.data.polygons)} polys, {len(final_obj.data.materials)} mats)")


if __name__ == "__main__":
    main()
