"""Generates 4 museum-grade Moscow-City Art-Deco sculpted 3D GLB assets for the Hero Central Elevator & Lobby Floor:
  1. assets/models/elevator_lobby_facade.glb   — Full 9m x 7.15m architectural lobby portal with fluted columns,
     stepped Art-Deco capitals & plinths, recessed vertical walnut acoustic slats, flush marble call-panel bay,
     sculpted milk-glass & cast-brass wall sconces, semicircular mechanical brass floor-indicator dial with arrow
     needle, monumental upper coffered stone/walnut bays, and stepped brass ceiling crown cornice.
  2. assets/models/elevator_door_leaf.glb      — Sculpted beveled brushed-bronze sliding door leaf with Art-Deco
     brass filigree transom lattice, mitered kickplate, and vertical astragal meeting edge.
  3. assets/models/elevator_cabin_interior.glb — Luxury executive elevator cabin interior with ribbed walnut
     wainscoting, framed burgundy-leather upper wall panels, tubular brass handrail on curved stand-off brackets,
     stepped coffered backlit alabaster ceiling dome, and inlaid marble/brass compass cabin floor.
  4. assets/models/elevator_floor_medallion.glb — Low-profile 2.4m octagonal Art-Deco obsidian, travertine &
     brushed-brass compass medallion inlay embedded cleanly at the foot of the elevator doors.
"""
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector as V, Matrix

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "assets/models"


def xyz(p):
    """Game coords (X right, Y up, Z forward toward lobby) -> Blender Z-up (X, -Z, Y),
    so glTF export outputs exact 1:1 game coords (X, Y, Z)."""
    return (p[0], -p[2], p[1])


def make_mat(name, hex_rgb, roughness=0.42, metallic=0.0, emission_hex=None, emission_strength=0.0):
    m = bpy.data.materials.new(name)
    rgb = bytes.fromhex(hex_rgb)
    lin = tuple(((c / 255.0 + 0.055) / 1.055) ** 2.4 for c in rgb) + (1.0,)
    m.diffuse_color = lin
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = lin
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission_hex and emission_strength > 0:
        er = bytes.fromhex(emission_hex)
        elin = tuple(((c / 255.0 + 0.055) / 1.055) ** 2.4 for c in er) + (1.0,)
        bsdf.inputs["Emission Color"].default_value = elin
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return m


class Builder:
    def __init__(self):
        self.mats = {
            "obsidian":   make_mat("obsidian",   "1c1d22", roughness=0.28, metallic=0.05),
            "nero":       make_mat("nero",       "28272d", roughness=0.34, metallic=0.04),
            "slate":      make_mat("slate",      "36353c", roughness=0.45, metallic=0.02),
            "travertine": make_mat("travertine", "c8b89d", roughness=0.42, metallic=0.0),
            "walnut":     make_mat("walnut",     "4a2e1d", roughness=0.52, metallic=0.0),
            "walnut_dk":  make_mat("walnut_dk",  "311d12", roughness=0.58, metallic=0.0),
            "burgundy":   make_mat("burgundy",   "521a22", roughness=0.62, metallic=0.0),
            "bronze":     make_mat("bronze",     "8c6638", roughness=0.32, metallic=0.82),
            "bronze_dk":  make_mat("bronze_dk",  "4e3a24", roughness=0.38, metallic=0.75),
            "brass":      make_mat("brass",      "d6aa60", roughness=0.22, metallic=0.92),
            "brass_hi":   make_mat("brass_hi",   "f0ca7d", roughness=0.18, metallic=0.95),
            "rubber":     make_mat("rubber",     "18181a", roughness=0.85, metallic=0.0),
            "milk_glass": make_mat("milk_glass", "ffe4b5", roughness=0.25, metallic=0.0, emission_hex="ffd591", emission_strength=1.45),
            "warm_cove":  make_mat("warm_cove",  "ffe0a3", roughness=0.30, metallic=0.0, emission_hex="ffc870", emission_strength=1.20),
            "dial_face":  make_mat("dial_face",  "f3e3c1", roughness=0.35, metallic=0.0, emission_hex="f0d5a0", emission_strength=0.65),
        }
        self.bms = {k: bmesh.new() for k in self.mats}

    def box(self, mat_key, cx, cy, cz, w, h, d, bevel=0.0):
        bm = self.bms[mat_key]
        res = bmesh.ops.create_cube(bm, size=1.0)
        verts = res["verts"]
        for v in verts:
            p = (cx + v.co.x * w, cy + v.co.y * h, cz + v.co.z * d)
            v.co = V(xyz(p))
        if bevel > 0.0:
            edges = list({e for v in verts for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=2, affect="EDGES", clamp_overlap=True)

    def cyl_y(self, mat_key, cx, y0, y1, cz, r_bot, r_top=None, segs=20, fluted=0):
        """Vertical cylinder along game Y axis, optionally fluted for classical/Art-Deco columns!"""
        if r_top is None:
            r_top = r_bot
        bm = self.bms[mat_key]
        rings = []
        n_segs = segs * 2 if fluted > 0 else segs
        for y, r in ((y0, r_bot), (y1, r_top)):
            ring = []
            for i in range(n_segs):
                a = i * math.tau / n_segs
                rad = r
                if fluted > 0:
                    # Crisp concave flutes carved along column shaft
                    flute_wave = math.cos(a * fluted)
                    rad *= 1.0 - 0.085 * max(0.0, flute_wave)
                p = (cx + rad * math.cos(a), y, cz + rad * math.sin(a))
                ring.append(bm.verts.new(xyz(p)))
            rings.append(ring)
        for i in range(n_segs):
            ni = (i + 1) % n_segs
            bm.faces.new((rings[0][i], rings[0][ni], rings[1][ni], rings[1][i]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[1])

    def cyl_z(self, mat_key, cx, cy, z0, z1, r, segs=24):
        """Horizontal cylinder along game Z axis (for arched bezels, knobs, standoffs)."""
        bm = self.bms[mat_key]
        rings = []
        for z in (z0, z1):
            ring = []
            for i in range(segs):
                a = i * math.tau / segs
                p = (cx + r * math.cos(a), cy + r * math.sin(a), z)
                ring.append(bm.verts.new(xyz(p)))
            rings.append(ring)
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((rings[0][i], rings[0][ni], rings[1][ni], rings[1][i]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[1])

    def cyl_x(self, mat_key, x0, x1, cy, cz, r, segs=16):
        """Horizontal cylinder along game X axis (for brass handrails)."""
        bm = self.bms[mat_key]
        rings = []
        for x in (x0, x1):
            ring = []
            for i in range(segs):
                a = i * math.tau / segs
                p = (x, cy + r * math.cos(a), cz + r * math.sin(a))
                ring.append(bm.verts.new(xyz(p)))
            rings.append(ring)
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((rings[0][i], rings[0][ni], rings[1][ni], rings[1][i]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[1])

    def arch_ring(self, mat_key, cx, cy, z0, z1, r_in, r_out, a0_deg=0.0, a1_deg=180.0, segs=28):
        """Extruded semicircular/arched architectural frame in the XY plane (along game Z)."""
        bm = self.bms[mat_key]
        pts_f_in, pts_f_out, pts_b_in, pts_b_out = [], [], [], []
        for i in range(segs + 1):
            t = i / segs
            a = math.radians(a0_deg + (a1_deg - a0_deg) * t)
            ca, sa = math.cos(a), math.sin(a)
            pts_b_in.append(bm.verts.new(xyz((cx + r_in * ca, cy + r_in * sa, z0))))
            pts_b_out.append(bm.verts.new(xyz((cx + r_out * ca, cy + r_out * sa, z0))))
            pts_f_in.append(bm.verts.new(xyz((cx + r_in * ca, cy + r_in * sa, z1))))
            pts_f_out.append(bm.verts.new(xyz((cx + r_out * ca, cy + r_out * sa, z1))))
        for i in range(segs):
            # Front, back, outer, inner quads
            bm.faces.new((pts_f_in[i], pts_f_out[i], pts_f_out[i + 1], pts_f_in[i + 1]))
            bm.faces.new((pts_b_out[i], pts_b_in[i], pts_b_in[i + 1], pts_b_out[i + 1]))
            bm.faces.new((pts_b_out[i], pts_b_out[i + 1], pts_f_out[i + 1], pts_f_out[i]))
            bm.faces.new((pts_f_in[i], pts_f_in[i + 1], pts_b_in[i + 1], pts_b_in[i]))
        # End caps
        bm.faces.new((pts_b_in[0], pts_b_out[0], pts_f_out[0], pts_f_in[0]))
        bm.faces.new((pts_b_out[-1], pts_b_in[-1], pts_f_in[-1], pts_f_out[-1]))

    def quad_bar(self, mat_key, p0, p1, half_w, z0, z1):
        """Angled architectural bar in the XY plane from p0=(x0,y0) to p1=(x1,y1)."""
        bm = self.bms[mat_key]
        dx, dy = p1[0] - p0[0], p1[1] - p0[1]
        L = math.hypot(dx, dy)
        if L < 1e-5:
            return
        nx, ny = -dy / L * half_w, dx / L * half_w
        corners = [
            (p0[0] - nx, p0[1] - ny),
            (p1[0] - nx, p1[1] - ny),
            (p1[0] + nx, p1[1] + ny),
            (p0[0] + nx, p0[1] + ny),
        ]
        b = [bm.verts.new(xyz((x, y, z0))) for (x, y) in corners]
        f = [bm.verts.new(xyz((x, y, z1))) for (x, y) in corners]
        bm.faces.new(f)
        bm.faces.new(list(reversed(b)))
        for i in range(4):
            ni = (i + 1) % 4
            bm.faces.new((b[i], b[ni], f[ni], f[i]))

    def export(self, filename):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = []
        for key, bm in self.bms.items():
            if len(bm.verts) == 0:
                bm.free()
                continue
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            m = bpy.data.meshes.new(f"mesh_{key}")
            bm.to_mesh(m)
            bm.free()
            for poly in m.polygons:
                poly.use_smooth = False
            obj = bpy.data.objects.new(f"obj_{key}", m)
            bpy.context.collection.objects.link(obj)
            # Re-create material inside the fresh scene
            obj.data.materials.append(self.mats[key].copy() if self.mats[key] else None)
            objs.append(obj)

        # Wait: read_factory_settings cleared bpy.data.materials! Build scene cleanly BEFORE exporting.
        return objs


def export_builder_glb(build_fn, stem):
    """Creates a clean Blender scene, runs build_fn(Builder), and exports assets/models/<stem>.glb."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    b = Builder()
    build_fn(b)
    objs = []
    for key, bm in b.bms.items():
        if len(bm.verts) == 0:
            bm.free()
            continue
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        m = bpy.data.meshes.new(f"{stem}_{key}")
        bm.to_mesh(m)
        bm.free()
        # Smooth shade curved cylinders/glasses, crisp flat-bevel shade on stone/brass panels
        smooth = key in ("milk_glass", "warm_cove", "dial_face", "brass", "brass_hi")
        for poly in m.polygons:
            poly.use_smooth = smooth
        obj = bpy.data.objects.new(f"{stem}_{key}", m)
        bpy.context.collection.objects.link(obj)
        obj.data.materials.append(b.mats[key])
        objs.append(obj)

    out_path = OUT / f"{stem}.glb"
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
            shutil.copy2(out_path, dest_dir / f"{stem}.glb")
    print(f"EXPORTED {out_path} ({len(objs)} material groups)")


# =============================================================================
# 1. HERO CENTRAL ELEVATOR LOBBY FACADE (origin at door face center y=0, +Z out)
# =============================================================================
def build_elevator_lobby_facade(b: Builder):
    # Portal opening: X in [-1.18, +1.18], Y in [0.0, 2.52]
    # Existing structural wall face sits at Z = +0.36 (front of marble wall) -> facade builds cleanly from Z = +0.16..+0.54!
    # Call panel mounts at local X = +1.82, Y = 1.38, Z = +0.374 (so X in [1.62..2.06] stays flush at Z = +0.365!)
    # Existing transom sign ("01 CLOUD ENTROPIC TOWER") sits at X=0, Y=3.14, Z=+0.415, W=3.15, H=0.62 -> keep clear!

    # --- A. STEPPED 3-TIER ART-DECO BRONZE & OBSIDIAN PORTAL ARCHITRAVE ---
    for s in (-1, 1):
        # Inner dark-bronze reveal jamb
        b.box("bronze_dk", s * 1.24, 1.30, 0.28, 0.12, 2.60, 0.28, bevel=0.012)
        # Middle stepped brushed-brass molding jamb
        b.box("bronze",    s * 1.35, 1.34, 0.36, 0.12, 2.68, 0.16, bevel=0.012)
        b.box("brass",     s * 1.42, 1.37, 0.41, 0.045, 2.74, 0.10, bevel=0.008)
        # Heavy obsidian plinth boot at floor
        b.box("obsidian",  s * 1.33, 0.14, 0.36, 0.30, 0.28, 0.24, bevel=0.012)
        b.box("brass",     s * 1.33, 0.285, 0.38, 0.31, 0.025, 0.24, bevel=0.005)
    # Stepped portal lintel above doors (y = 2.52 .. 2.78)
    b.box("bronze_dk", 0.0, 2.58, 0.28, 2.68, 0.12, 0.28, bevel=0.012)
    b.box("bronze",    0.0, 2.67, 0.37, 2.92, 0.11, 0.18, bevel=0.012)
    b.box("brass",     0.0, 2.745, 0.42, 3.06, 0.045, 0.14, bevel=0.008)
    # Recessed warm cove uplight under portal lintel!
    b.box("warm_cove", 0.0, 2.525, 0.24, 2.28, 0.022, 0.06)

    # --- B. FLUTED HALF-ROUND ARCHITECTURAL COLUMNS FLANKING THE PORTAL & CORNERS ---
    # Inner pair at X = ±1.56 (leaves X = +1.82 completely open for RFID Call Panel!)
    # Outer pair at X = ±3.88, framing the walnut & sconce bays!
    for cx, col_r in ((-1.56, 0.135), (1.56, 0.135), (-3.88, 0.17), (3.88, 0.17)):
        # Stepped obsidian + brass base plinth
        b.box("obsidian", cx, 0.18, 0.42, col_r * 2.5, 0.36, 0.26, bevel=0.014)
        b.box("bronze",   cx, 0.38, 0.42, col_r * 2.3, 0.06, 0.24, bevel=0.008)
        b.box("brass",    cx, 0.43, 0.42, col_r * 2.1, 0.04, 0.22, bevel=0.006)
        # Fluted dark obsidian marble shaft
        b.cyl_y("nero",   cx, 0.45, 3.38, 0.42, col_r, col_r * 0.96, segs=16, fluted=12)
        # Mid-shaft Art-Deco brass collar rings
        for ry in (1.15, 2.56, 3.34):
            b.cyl_y("brass", cx, ry - 0.022, ry + 0.022, 0.42, col_r * 1.09, col_r * 1.09, segs=24)
        # Stepped Art-Deco geometric capital (y = 3.38 .. 3.68)
        b.box("bronze",   cx, 3.45, 0.42, col_r * 2.2, 0.12, 0.24, bevel=0.010)
        b.box("brass",    cx, 3.55, 0.44, col_r * 2.5, 0.08, 0.28, bevel=0.010)
        b.box("bronze_dk",cx, 3.63, 0.44, col_r * 2.8, 0.08, 0.30, bevel=0.010)

    # --- C. FLANKING LUXURY WALL BAYS (WALNUT VERTICAL SLATS + HONED MARBLE CALL BAY) ---
    for s in (-1, 1):
        # Flush dark honed obsidian base wall backing (completely hides ugly procedural wall!)
        b.box("obsidian", s * 2.95, 1.85, 0.355, 2.98, 3.50, 0.04)
        # Heavy obsidian baseboard & brass kick molding
        b.box("obsidian", s * 2.95, 0.14, 0.39, 2.96, 0.28, 0.08, bevel=0.008)
        b.box("brass",    s * 2.95, 0.285, 0.41, 2.96, 0.024, 0.06, bevel=0.004)
        # Sculpted horizontal chair-rail wainscot trim (breaks right around call panel on s=+1!)
        if s == -1:
            b.box("bronze", -2.72, 1.02, 0.40, 2.02, 0.055, 0.07, bevel=0.006)
        else:
            b.box("bronze",  3.04, 1.02, 0.40, 1.38, 0.055, 0.07, bevel=0.006)
            # Crisp recessed brushed-brass picture-frame surround for the RFID Call Panel at X=+1.82, Y=1.38!
            b.box("nero",   1.86, 1.38, 0.362, 0.44, 0.88, 0.014, bevel=0.004)
            for fx in (1.64, 2.08):
                b.box("brass", fx, 1.38, 0.372, 0.022, 0.90, 0.020, bevel=0.003)
            for fy in (0.94, 1.82):
                b.box("brass", 1.86, fy, 0.372, 0.46, 0.022, 0.020, bevel=0.003)

        # Deep sculpted vertical DARK WALNUT acoustic architectural slats across the outer lobby bays!
        x_start = 2.18 if s == 1 else 1.78
        x_end   = 3.62
        n_slats = 16 if s == 1 else 20
        step = (x_end - x_start) / max(1, n_slats - 1)
        for k in range(n_slats):
            sx = s * (x_start + k * step)
            b.box("walnut_dk", sx, 1.92, 0.385, 0.044, 3.16, 0.052, bevel=0.005)
            if k % 4 == 0:
                b.box("brass", sx, 1.92, 0.412, 0.010, 3.10, 0.012)

        # Sculpted Outer Corner Pier Cladding (X = ±4.28)
        b.box("nero",   s * 4.30, 3.55, 0.39, 0.44, 7.05, 0.14, bevel=0.014)
        b.box("bronze", s * 4.48, 3.55, 0.43, 0.09, 7.05, 0.14, bevel=0.010)
        b.box("brass",  s * 4.10, 3.55, 0.43, 0.032, 7.05, 0.06, bevel=0.005)

        # --- D. SCULPTED ART-DECO RIBBED MILK-GLASS & BRASS WALL SCONCES (X = ±2.88, Y = 2.26) ---
        sx, sy = s * 2.88, 2.26
        # Cast-brass stepped architectural wall backplate
        b.box("bronze_dk", sx, sy, 0.415, 0.24, 0.62, 0.04, bevel=0.008)
        b.box("brass",     sx, sy, 0.435, 0.18, 0.54, 0.03, bevel=0.006)
        # Upper & lower brass mounting brackets
        for by in (sy - 0.19, sy + 0.19):
            b.box("bronze", sx, by, 0.485, 0.14, 0.045, 0.12, bevel=0.006)
            b.cyl_y("brass_hi", sx, by - 0.025, by + 0.025, 0.535, 0.068, 0.068, segs=20)
        # Fluted warm glowing alabaster milk-glass cylinder lantern!
        b.cyl_y("milk_glass", sx, sy - 0.175, sy + 0.175, 0.535, 0.056, 0.056, segs=16, fluted=10)
        # Sculpted brass top & bottom art-deco spire finials
        b.cyl_y("brass", sx, sy + 0.215, sy + 0.30, 0.535, 0.042, 0.008, segs=14)
        b.cyl_y("brass", sx, sy - 0.30, sy - 0.215, 0.535, 0.008, 0.042, segs=14)

    # --- E. GRAND HORIZONTAL ENTABLATURE CORNICE (Y = 3.68) ---
    b.box("obsidian",  0.0, 3.68, 0.42, 9.04, 0.18, 0.22, bevel=0.014)
    b.box("bronze",    0.0, 3.78, 0.46, 9.10, 0.09, 0.26, bevel=0.012)
    b.box("brass",     0.0, 3.84, 0.49, 9.14, 0.04, 0.22, bevel=0.008)
    # Recessed brass frame around the existing transom plaque at Y=3.14 (W=3.15, H=0.62)
    b.box("bronze_dk", 0.0, 3.50, 0.415, 3.38, 0.075, 0.12, bevel=0.008)
    b.box("brass",     0.0, 2.80, 0.415, 3.38, 0.045, 0.11, bevel=0.006)

    # --- F. MONUMENTAL UPPER HALL CLADDING (Y = 3.86 .. 7.15) — REPLACES UGLY WAVY WALL! ---
    # Deep charcoal-obsidian coffered architectural upper facade
    b.box("obsidian", 0.0, 5.50, 0.355, 8.96, 3.30, 0.06)
    # 3 recessed dark-walnut coffered architectural upper panels with brass frames
    for bx, bw in ((-2.82, 2.02), (0.0, 2.76), (2.82, 2.02)):
        b.box("walnut_dk", bx, 5.52, 0.382, bw, 2.66, 0.03)
        # Beveled bronze picture-frame molding around each upper coffer
        b.box("bronze", bx, 6.86, 0.405, bw + 0.08, 0.06, 0.06, bevel=0.006)
        b.box("bronze", bx, 4.18, 0.405, bw + 0.08, 0.06, 0.06, bevel=0.006)
        for edge in (-1, 1):
            b.box("bronze", bx + edge * (bw * 0.5 + 0.02), 5.52, 0.405, 0.06, 2.72, 0.06, bevel=0.006)
        # Vertical slim brass flutes inside left/right coffers
        if abs(bx) > 0.5:
            for k in range(-3, 4):
                b.box("walnut", bx + k * 0.25, 5.52, 0.40, 0.065, 2.54, 0.032, bevel=0.005)
                if k % 2 == 0:
                    b.box("brass", bx + k * 0.25, 5.52, 0.418, 0.012, 2.50, 0.012)

    # 4 upper fluted stone & brass pilasters rising to the ceiling crown cornice!
    for px in (-3.88, -1.56, 1.56, 3.88):
        b.box("nero",   px, 5.50, 0.42, 0.30, 3.24, 0.14, bevel=0.012)
        b.box("bronze", px, 5.50, 0.495, 0.14, 3.16, 0.04, bevel=0.006)
        b.box("brass",  px, 5.50, 0.515, 0.036, 3.12, 0.02, bevel=0.004)

    # --- G. HERO MECHANICAL ART-DECO SEMICIRCULAR BRASS FLOOR-INDICATOR DIAL (Y = 4.02 .. 5.20) ---
    dy = 4.18
    # Radiant Art-Deco bronze sunburst rays behind the dial inside the center coffer!
    for i in range(9):
        ang = math.radians(15.0 + i * (150.0 / 8.0))
        p0 = (0.0 + 0.72 * math.cos(ang), dy + 0.72 * math.sin(ang))
        p1 = (0.0 + 1.24 * math.cos(ang), dy + 1.24 * math.sin(ang))
        b.quad_bar("brass", p0, p1, 0.018, 0.39, 0.425)
    # Semicircular dark-bronze outer housing + gleaming brass arched bezel
    b.arch_ring("bronze_dk", 0.0, dy, 0.39, 0.51, 0.62, 0.78, 0, 180, segs=32)
    b.arch_ring("brass_hi",  0.0, dy, 0.47, 0.54, 0.66, 0.75, 0, 180, segs=32)
    # Backlit warm ivory milk-glass semicircular dial face
    b.arch_ring("dial_face", 0.0, dy, 0.40, 0.46, 0.04, 0.66, 0, 180, segs=32)
    # Bottom straight brass sill bar of the dial
    b.box("bronze_dk", 0.0, dy - 0.04, 0.46, 1.62, 0.10, 0.14, bevel=0.010)
    b.box("brass",     0.0, dy + 0.01, 0.51, 1.56, 0.035, 0.08, bevel=0.006)
    # 11 embossed bronze floor tick markers around the dial arc (Floors 01 .. 50!)
    for i in range(11):
        ang = math.radians(168.0 - i * (156.0 / 10.0))
        r0 = 0.48 if i % 5 == 0 else 0.53
        r1 = 0.63
        p0 = (r0 * math.cos(ang), dy + r0 * math.sin(ang))
        p1 = (r1 * math.cos(ang), dy + r1 * math.sin(ang))
        b.quad_bar("bronze_dk", p0, p1, 0.016 if i % 5 == 0 else 0.010, 0.46, 0.49)
    # Cast-bronze mechanical floor pointer arrow pointing boldly at FLOOR 01 (leftmost tick = 166 deg)!
    needle_ang = math.radians(164.0)
    b.quad_bar("obsidian", (0.0, dy + 0.02), (0.56 * math.cos(needle_ang), dy + 0.56 * math.sin(needle_ang)), 0.022, 0.48, 0.52)
    b.quad_bar("brass_hi", (0.0, dy + 0.02), (0.52 * math.cos(needle_ang), dy + 0.52 * math.sin(needle_ang)), 0.010, 0.51, 0.535)
    # Center turned brass hub pivot boss
    b.cyl_z("bronze_dk", 0.0, dy + 0.02, 0.46, 0.55, 0.095, segs=24)
    b.cyl_z("brass_hi",  0.0, dy + 0.02, 0.53, 0.57, 0.055, segs=24)

    # Stepped Monumental Ceiling Crown Cornice (Y = 6.95 .. 7.16)
    b.box("bronze_dk", 0.0, 7.00, 0.44, 9.06, 0.16, 0.24, bevel=0.012)
    b.box("brass",     0.0, 7.10, 0.48, 9.14, 0.08, 0.30, bevel=0.010)

    # --- H. GROUNDED MARBLE & BRASS THRESHOLD SILL ---
    b.box("obsidian", 0.0, 0.028, 0.34, 3.12, 0.056, 0.62, bevel=0.008)
    b.box("brass",    0.0, 0.058, 0.15, 2.48, 0.010, 0.06, bevel=0.003)
    b.box("brass",    0.0, 0.058, 0.62, 3.10, 0.010, 0.03, bevel=0.003)


# =============================================================================
# 2. SCULPTED ART-DECO BRONZE & FILIGREE SLIDING DOOR LEAF (1.18m x 2.50m)
# =============================================================================
def build_elevator_door_leaf(b: Builder):
    # Origin at door leaf center (0,0,0), width=1.18, height=2.50, thickness=0.08
    # Drawn twice: leaf=-1 (left) and leaf=+1 (right)
    w, h, d = 1.18, 2.50, 0.082
    hw, hh = w * 0.5, h * 0.5

    # Deep beveled dark-bronze perimeter stiles & rails
    b.box("bronze_dk", -hw + 0.045, 0.0, 0.0, 0.09, h, d, bevel=0.008)
    b.box("bronze_dk",  hw - 0.045, 0.0, 0.0, 0.09, h, d, bevel=0.008)
    b.box("bronze_dk",  0.0,  hh - 0.05, 0.0, w, 0.10, d, bevel=0.008)
    b.box("bronze_dk",  0.0, -hh + 0.21, 0.0, w, 0.42, d, bevel=0.008)

    # Recessed brushed-brass lower kickplate panel (both faces)
    for fz in (-1, 1):
        b.box("bronze", 0.0, -hh + 0.21, fz * 0.040, w - 0.20, 0.30, 0.012, bevel=0.004)
        b.box("brass",  0.0, -hh + 0.365, fz * 0.044, w - 0.16, 0.022, 0.014, bevel=0.003)
        b.box("brass",  0.0, -hh + 0.055, fz * 0.044, w - 0.16, 0.022, 0.014, bevel=0.003)

        # Inner stepped brushed-brass window-frame beading around the smoked glass opening
        b.box("brass", -hw + 0.095, 0.18, fz * 0.040, 0.022, 1.98, 0.018, bevel=0.003)
        b.box("brass",  hw - 0.095, 0.18, fz * 0.040, 0.022, 1.98, 0.018, bevel=0.003)
        b.box("brass",  0.0,  hh - 0.105, fz * 0.040, w - 0.18, 0.022, 0.018, bevel=0.003)
        b.box("brass",  0.0, -hh + 0.425, fz * 0.040, w - 0.18, 0.022, 0.018, bevel=0.003)

        # Elegant Art-Deco vertical brass muntins & geometric diamond filigree bars over the smoked glass!
        for mx in (-0.24, 0.24):
            b.box("bronze", mx, 0.18, fz * 0.036, 0.024, 1.96, 0.016, bevel=0.003)
            b.box("brass",  mx, 0.18, fz * 0.044, 0.010, 1.94, 0.008)
        # Upper Art-Deco chevron / geometric frieze band across top third of the glass
        b.box("bronze", 0.0, 0.82, fz * 0.036, w - 0.18, 0.024, 0.016, bevel=0.003)
        b.box("brass",  0.0, 0.82, fz * 0.044, w - 0.18, 0.010, 0.008)
        z0, z1 = (0.028, 0.044) if fz > 0 else (-0.044, -0.028)
        # Art-Deco geometric diamond chevrons in upper transom grille
        for cx in (-0.36, 0.0, 0.36):
            b.quad_bar("brass", (cx - 0.11, 0.83), (cx, 1.13), 0.008, z0, z1)
            b.quad_bar("brass", (cx + 0.11, 0.83), (cx, 1.13), 0.008, z0, z1)
        # Mid-height Art-Deco diamond emblem at center of door leaf
        b.quad_bar("brass", (-0.23, 0.02), (0.0, 0.34), 0.009, z0, z1)
        b.quad_bar("brass", ( 0.23, 0.02), (0.0, 0.34), 0.009, z0, z1)
        b.quad_bar("brass", (-0.23, 0.02), (0.0, -0.30), 0.009, z0, z1)
        b.quad_bar("brass", ( 0.23, 0.02), (0.0, -0.30), 0.009, z0, z1)

    # Vertical rubber bumper astragal edges on both sides so meeting seam looks crisp
    b.box("rubber", -hw - 0.006, 0.0, 0.0, 0.014, h - 0.02, 0.038)
    b.box("rubber",  hw + 0.006, 0.0, 0.0, 0.014, h - 0.02, 0.038)


# =============================================================================
# 3. LUXURY EXECUTIVE ELEVATOR CABIN INTERIOR (origin at CentralElevatorCorePos)
# =============================================================================
def build_elevator_cabin_interior(b: Builder):
    # Cabin spans inside core: X in [-4.22, +4.22], Z in [-4.22, +4.22], divided by center core divider at Z=0!
    # South cabin: Z in [+0.20, +4.22], North cabin: Z in [-4.22, -0.20]. Ceiling at Y = 2.74.
    for side in (-1, 1):
        # Back wall facing the doors (at Z = side * 0.21)
        bz = side * 0.21
        # Ribbed dark walnut lower wainscoting (y = 0.10 .. 1.02)
        b.box("walnut_dk", 0.0, 0.56, bz, 8.28, 0.92, 0.04)
        for k in range(-18, 19):
            b.box("walnut", k * 0.22, 0.56, bz + side * 0.025, 0.09, 0.86, 0.025, bevel=0.004)
        # Chair-rail brass belt
        b.box("brass", 0.0, 1.04, bz + side * 0.035, 8.28, 0.045, 0.04, bevel=0.005)
        # Upper framed burgundy-leather & walnut luxury panels (3 grand panels across back wall)
        for px in (-2.55, 0.0, 2.55):
            b.box("bronze_dk", px, 1.88, bz + side * 0.02, 2.26, 1.54, 0.03, bevel=0.006)
            b.box("burgundy",  px, 1.88, bz + side * 0.036, 2.08, 1.36, 0.024, bevel=0.006)
            b.box("brass",     px, 1.88 + 0.69, bz + side * 0.048, 2.12, 0.022, 0.016)
            b.box("brass",     px, 1.88 - 0.69, bz + side * 0.048, 2.12, 0.022, 0.016)
        # Tubular polished brass handrail along back wall (Y = 0.94) + curved wall brackets
        b.cyl_x("brass_hi", -3.92, 3.92, 0.94, bz + side * 0.12, 0.024, segs=16)
        for hx in (-3.2, -1.6, 0.0, 1.6, 3.2):
            b.cyl_z("bronze", hx, 0.94, bz, bz + side * 0.12, 0.016, segs=12)
            b.cyl_z("brass",  hx, 0.94, bz, bz + side * 0.025, 0.042, segs=16)

        # Side walls (X = ±4.22, Z from 0.22 to 4.18)
        cz_mid = side * 2.22
        for sx in (-1, 1):
            wx = sx * 4.22
            b.box("walnut_dk", wx, 0.56, cz_mid, 0.04, 0.92, 3.88)
            b.box("brass",     wx - sx * 0.03, 1.04, cz_mid, 0.04, 0.045, 3.88, bevel=0.005)
            b.box("bronze_dk", wx - sx * 0.02, 1.88, cz_mid, 0.03, 1.54, 3.40, bevel=0.006)
            b.box("walnut",    wx - sx * 0.035, 1.88, cz_mid, 0.024, 1.36, 3.16, bevel=0.006)
            # Side brass handrails
            b.cyl_z("brass_hi", wx - sx * 0.12, 0.94, side * 0.55, side * 3.85, 0.024, segs=16)

        # Coffered Art-Deco Illuminated Alabaster Ceiling Dome (Y = 2.66 .. 2.74)
        b.box("bronze_dk",  0.0, 2.72, cz_mid, 5.40, 0.08, 3.10, bevel=0.010)
        b.box("brass",      0.0, 2.67, cz_mid, 5.06, 0.05, 2.76, bevel=0.008)
        b.box("milk_glass", 0.0, 2.655, cz_mid, 4.78, 0.028, 2.48)
        # Coffer brass cross-ribs dividing the glowing ceiling skylight into 6 luxury coffers!
        for rx in (-1.60, 0.0, 1.60):
            b.box("bronze", rx, 2.642, cz_mid, 0.065, 0.032, 2.54, bevel=0.004)
        b.box("bronze", 0.0, 2.642, cz_mid, 4.86, 0.032, 0.065, bevel=0.004)

        # Luxury Cabin Floor Marble & Brass Inlay Frame (y = 0.091)
        b.box("obsidian",   0.0, 0.090, cz_mid, 4.60, 0.006, 2.60)
        b.box("travertine", 0.0, 0.092, cz_mid, 3.90, 0.006, 2.00)
        b.box("nero",       0.0, 0.094, cz_mid, 3.56, 0.006, 1.66)
        b.box("brass",      0.0, 0.096, cz_mid + 0.98, 3.96, 0.006, 0.036)
        b.box("brass",      0.0, 0.096, cz_mid - 0.98, 3.96, 0.006, 0.036)


# =============================================================================
# 4. ART-DECO LOBBY FLOOR MEDALLION INLAY (2.6m octagon in front of elevator!)
# =============================================================================
def build_elevator_floor_medallion(b: Builder):
    # Low-profile 16mm architectural marble-and-brass inlay medallion centered at (0,0,0)
    def poly_disc(mat_key, y0, y1, r, segs=32, rot=0.0):
        bm = b.bms[mat_key]
        r_bot, r_top = [], []
        for i in range(segs):
            a = rot + i * math.tau / segs
            ca, sa = math.cos(a), math.sin(a)
            r_bot.append(bm.verts.new(xyz((r * ca, y0, r * sa))))
            r_top.append(bm.verts.new(xyz((r * ca, y1, r * sa))))
        for i in range(segs):
            ni = (i + 1) % segs
            bm.faces.new((r_bot[i], r_bot[ni], r_top[ni], r_top[i]))
        bm.faces.new(r_top)

    # Outer beveled brass octagonal border frame
    poly_disc("bronze_dk",  0.000, 0.012, 1.38, segs=8, rot=math.pi / 8)
    poly_disc("brass",      0.010, 0.015, 1.32, segs=8, rot=math.pi / 8)
    poly_disc("obsidian",   0.012, 0.016, 1.25, segs=8, rot=math.pi / 8)
    poly_disc("travertine", 0.013, 0.017, 1.06, segs=32)
    poly_disc("brass",      0.014, 0.018, 0.98, segs=32)
    poly_disc("nero",       0.015, 0.019, 0.92, segs=32)
    poly_disc("burgundy",   0.016, 0.020, 0.56, segs=32)
    poly_disc("brass_hi",   0.017, 0.0215, 0.22, segs=24)
    poly_disc("obsidian",   0.018, 0.0225, 0.14, segs=24)

    # 8-point Art-Deco brushed-brass compass rose rays embedded flush in the medallion!
    for i in range(8):
        ang = i * math.tau / 8.0
        ca, sa = math.cos(ang), math.sin(ang)
        r_len = 0.96 if i % 2 == 0 else 0.72
        w_ray = 0.042 if i % 2 == 0 else 0.024
        # Build flat diamond ray in XZ plane at y = 0.019..0.022
        bm = b.bms["brass_hi" if i % 2 == 0 else "brass"]
        nx, nz = -sa * w_ray, ca * w_ray
        pts = [
            (0.18 * ca, 0.18 * sa),
            (0.45 * ca + nx, 0.45 * sa + nz),
            (r_len * ca, r_len * sa),
            (0.45 * ca - nx, 0.45 * sa - nz),
        ]
        bot = [bm.verts.new(xyz((x, 0.016, z))) for (x, z) in pts]
        top = [bm.verts.new(xyz((x, 0.022, z))) for (x, z) in pts]
        bm.faces.new(top)
        for k in range(4):
            nk = (k + 1) % 4
            bm.faces.new((bot[k], bot[nk], top[nk], top[k]))


def main():
    export_builder_glb(build_elevator_lobby_facade, "elevator_lobby_facade")
    export_builder_glb(build_elevator_door_leaf, "elevator_door_leaf")
    export_builder_glb(build_elevator_cabin_interior, "elevator_cabin_interior")
    export_builder_glb(build_elevator_floor_medallion, "elevator_floor_medallion")


if __name__ == "__main__":
    main()
