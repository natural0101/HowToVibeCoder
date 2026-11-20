"""High-craft Blender 5.1 generator for Floor 1 Hero Table & Counter Props (#192 / #193):
  1. assets/models/prop_macbook_trap_desk.glb
     - Sleek designer Dark-Espresso Walnut & Smoked-Glass meeting table with chamfered brushed-brass
       perimeter inlay, corner L-brackets, cable-management under-tray, and 4 splayed walnut/titanium/brass
       architectural legs resting flush on Y_rl = 0.000m (exact tabletop surface Y_rl = 0.940m preserved for
       TrapTop = 0.94f * TrapScale!).
     - Thin sculpted brushed-aluminum ЯблоБук Pro M4 Max oriented toward the approaching player (+Z_rl)
       with 64 individual sculpted keycaps, Force-Touch trackpad, glowing cyan rear emblem, and a crisp
       512x512 glowing IDE + Wi-Fi Honeypot screen texture.
     - White/silver USB-C power adapter brick with 3D curved braided MagSafe/USB-C cord & AC cable loop
       entering a recessed brushed-brass desktop cable grommet.
     - Stealth matte-graphite Wi-Fi 6 Honeypot Router with 4 angled dipole antennas, 6 glowing status LEDs,
       and a curved blue Cat-6 ethernet patch cable.
     - Lived-in clutter: 2 sculpted aluminum energy-drink cans (with pull-tabs & printed wraps), dark
       espresso coffee ring stain + ceramic coffee mug, and 5 scattered colorful sticky notes.

  2. assets/models/prop_arsenal_counter.glb
     - Rich Dark-Espresso Walnut & Diamond-Tufted Burgundy Velvet reception/arsenal display counter
       matching Veronika's new Briefcase aesthetic (26_shop_3d_showcase.png).
     - Chamfered dark-walnut cabinet with 6 fluted walnut-and-brass pilasters, 5 recessed diamond-tufted
       burgundy velvet front panels with brass frame moldings, corner L-brackets, and tuft buttons,
       plus a continuous brushed-brass kickplate resting flush at Y_rl = 0.000m.
     - Overhanging walnut bullnose countertop at exact Y_rl = 1.088m topped with 5 plush burgundy velvet
       & brushed-brass weapon display mats at (x = -1.66, -0.83, 0.00, +0.83, +1.66, z = -0.28) so
       DrawWeaponRack's 3D weapons & pads sit flush.
     - Recessed under-counter walnut shelf alcoves stocked with kraft-cardboard ammo/hardware boxes
       (sealed with blue electrical tape & parcel labels) and olive-drab surplus cases.
     - Grand walnut & tufted-burgundy backwall showcase canopy (Y_rl = 1.088..3.22m) with brass cornice
       downlights, upper shelves, spare ammo boxes, and hardware canisters.

Strict Raylib OpenGL Compliance:
  - GL_CULL_FACE safe: all normals recalculated outward (recalc_face_normals) and verified with
    use_backface_culling = True.
  - Linear PBR calibration: procedural textures pre-calibrated with srgb ** 2.2 so dark walnut, graphite,
    and burgundy velvet render rich and deep in Raylib without chalky washout.
  - 24-ray BVHTree Ambient Occlusion baked into vertex color attribute COLOR_0 (export_vertex_color="ACTIVE").
"""
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parent.parent
OUT_MODELS = ROOT / "assets" / "models"
ARTIFACT_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")
MY_SCRATCH_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\5a288ef4-482c-487e-b05f-e3d0e2267cf7\scratch")
SCREENSHOTS_DIR = ROOT / "screenshots"


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def rl_pos(p):
    """Raylib (X=right, Y=up, Z=front) -> Blender (X=right, Y=-Z, Z=Y)."""
    return (float(p[0]), -float(p[2]), float(p[1]))


def rl_scale(s):
    return (float(s[0]), float(s[2]), float(s[1]))


def rl_rot(r):
    return (float(r[0]), -float(r[2]), float(r[1]))


def srgb_to_linear_val(c):
    return ((c + 0.055) / 1.055) ** 2.4 if c > 0.04045 else c / 12.92


def srgb_rgba(r, g, b, a=1.0):
    return (srgb_to_linear_val(r), srgb_to_linear_val(g), srgb_to_linear_val(b), a)


def pack_image(name, rgba_float, gamma_calibrate=True):
    """Packs an HxWx4 float32 sRGB array into a Blender image, applying x**2.2 linear calibration
    so Blender's PNG packer + Raylib's direct texture sampler render rich, non-chalky colors."""
    h, w, _ = rgba_float.shape
    arr = np.clip(rgba_float, 0.0, 1.0).copy()
    if gamma_calibrate:
        arr[..., :3] = np.power(arr[..., :3], 2.2)
    if name in bpy.data.images:
        bpy.data.images.remove(bpy.data.images[name])
    img = bpy.data.images.new(name=name, width=w, height=h, alpha=True)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


def make_mat(name, rgba, metallic=0.0, roughness=0.45, emission_rgb=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if emission_rgb is not None and emission_strength > 0.0:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def make_textured_mat(name, img, metallic=0.0, roughness=0.45, emission_rgb=None, emission_strength=0.0):
    mat = make_mat(name, (1.0, 1.0, 1.0, 1.0), metallic=metallic, roughness=roughness,
                   emission_rgb=emission_rgb, emission_strength=emission_strength)
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if emission_rgb is not None and emission_strength > 0.0 and "Emission Color" in bsdf.inputs:
        links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
    return mat


# ==============================================================================
# PROCEDURAL HIGH-CRAFT TEXTURES (Pre-calibrated linear x**2.2 for Raylib)
# ==============================================================================
def tex_dark_espresso_walnut(name="Tex_EspressoWalnut", size=512):
    """Rich dark-espresso walnut wood grain with warm mahogany cathedral planks and satin sheen."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    grain = (
        np.sin(x * 46.0 + np.sin(y * 14.0) * 2.2) * 0.5
        + np.sin(y * 165.0 + x * 24.0) * 0.3
        + np.cos((x * 18.0 - y * 9.0) + np.sin(x * 32.0) * 1.4) * 0.2
    )
    t = np.clip((grain + 1.0) * 0.5, 0.0, 1.0)
    # Subtle plank seam shading
    plank = 0.88 + 0.12 * np.clip(np.abs(np.sin(x * math.pi * 6.0)) * 4.0, 0.0, 1.0)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = (0.34 + 0.22 * t) * plank
    img[..., 1] = (0.20 + 0.13 * t) * plank
    img[..., 2] = (0.12 + 0.08 * t) * plank
    img[..., 3] = 1.0
    return pack_image(name, img, gamma_calibrate=True)


def tex_brushed_antique_brass(name="Tex_AntiqueBrass", size=256):
    """Warm brushed antique brass with fine directional metallic streaks."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    streak = 0.5 + 0.5 * np.sin(y * 210.0 + np.sin(x * 35.0) * 0.7)
    sheen = 0.86 + 0.14 * np.cos((x + y) * math.pi * 4.0)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = (0.68 + 0.14 * streak) * sheen
    img[..., 1] = (0.50 + 0.12 * streak) * sheen
    img[..., 2] = (0.22 + 0.07 * streak) * sheen
    img[..., 3] = 1.0
    return pack_image(name, img, gamma_calibrate=True)


def tex_tufted_burgundy_velvet(name="Tex_TuftedBurgundyVelvet", size=512):
    """Deep diamond-tufted burgundy-wine velvet matching Veronika's Briefcase (26_shop_3d_showcase.png)."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    u = x * 5.0 + y * 5.0
    v = x * 5.0 - y * 5.0
    du = np.abs(u - np.round(u))
    dv = np.abs(v - np.round(v))
    seam = np.minimum(du, dv)
    pillow = np.clip(seam * 3.6, 0.0, 1.0) ** 0.58
    # Button tuft dimple at diamond lattice vertices
    d_tuft = np.sqrt(du * du + dv * dv)
    dimple = np.clip(d_tuft * 5.5, 0.25, 1.0)
    # Subtle micro-velvet nap
    nap = 0.94 + 0.06 * np.sin(x * 320.0) * np.cos(y * 320.0)
    shade = pillow * dimple * nap

    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 0.26 + 0.46 * shade
    img[..., 1] = 0.055 + 0.125 * shade
    img[..., 2] = 0.13 + 0.24 * shade
    img[..., 3] = 1.0
    return pack_image(name, img, gamma_calibrate=True)


def tex_macbook_honeypot_display(name="Tex_MacBookHoneypotScreen", size=512):
    """High-contrast 512x512 IDE + Wi-Fi Honeypot Screen with colorful syntax lines,
    live Wi-Fi radar, terminal output, and FREE WI-FI // M4 MAX bait banner."""
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., :3] = (0.045, 0.058, 0.088)
    img[..., 3] = 1.0

    # Top macOS / Cursor titlebar & glowing emerald-cyan Honeypot status banner
    img[size - 34 :, :, :3] = (0.08, 0.11, 0.16)
    img[size - 36 : size - 33, :, :3] = (0.18, 0.88, 0.96)
    # Traffic light window dots (red, amber, green)
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    for cx, col in [(22.0, (0.96, 0.32, 0.30)), (40.0, (0.98, 0.76, 0.22)), (58.0, (0.24, 0.88, 0.44))]:
        dot = (x - cx) ** 2 + (y - (size - 18.0)) ** 2 < 36.0
        img[dot, :3] = col

    # Top-right glowing "FREE_CORP_WIFI_5G // CONNECTED" pill badge
    img[size - 28 : size - 8, 280:496, :3] = (0.08, 0.34, 0.32)
    img[size - 24 : size - 12, 292:484, :3] = (0.24, 0.96, 0.78)

    # Left file tree sidebar
    img[110 : size - 36, 0:82, :3] = (0.032, 0.042, 0.065)
    img[110 : size - 36, 82:85, :3] = (0.14, 0.22, 0.32)
    for idx, r in enumerate(range(size - 56, 130, -18)):
        w_bar = 34 + (idx * 13) % 28
        col = (0.28, 0.84, 0.96) if idx % 3 == 0 else (0.58, 0.68, 0.82)
        img[r : r + 6, 14 : 14 + w_bar, :3] = col

    # Center IDE Code Editor pane with colorful syntax tokens
    rng = np.random.default_rng(1337)
    palette = [
        (0.24, 0.92, 0.98),  # cyan keyword
        (0.98, 0.44, 0.68),  # magenta token
        (0.42, 0.96, 0.62),  # emerald string
        (0.98, 0.82, 0.32),  # gold function
        (0.68, 0.52, 0.98),  # purple type
        (0.84, 0.88, 0.95),  # crisp identifier
    ]
    for idx, row in enumerate(range(size - 54, 132, -13)):
        indent = 98 + (idx % 4) * 14
        cur_x = indent
        # Active line highlight on row 8
        if idx == 8:
            img[row - 3 : row + 9, 86:336, :3] = (0.10, 0.16, 0.25)
        for tok in range(rng.integers(2, 5)):
            tw = rng.integers(18, 52)
            if cur_x + tw > 330:
                break
            c = palette[(idx + tok) % len(palette)]
            img[row : row + 6, cur_x : cur_x + tw, :3] = c
            cur_x += tw + 8

    # Right pane: Live Wi-Fi Honeypot Signal Radar & Telemetry Bars
    img[110 : size - 36, 338:341, :3] = (0.16, 0.26, 0.38)
    rcx, rcy = 424.0, 340.0
    rr = np.sqrt((x - rcx) ** 2 + (y - rcy) ** 2)
    for rad, col in [(68.0, (0.18, 0.86, 0.96)), (46.0, (0.24, 0.94, 0.66)), (22.0, (0.98, 0.76, 0.26))]:
        ring = np.abs(rr - rad) < 2.2
        img[ring, :3] = col
    sweep = (rr < 68.0) & (x > rcx) & (y > rcy) & (np.abs((x - rcx) - (y - rcy)) < 14.0)
    img[sweep, :3] = (0.28, 0.98, 0.88)

    # Signal strength bars under radar
    for b_i, bh in enumerate((24, 38, 52, 68, 84)):
        bx = 368 + b_i * 22
        img[135 : 135 + bh, bx : bx + 14, :3] = (0.22, 0.94, 0.72) if b_i < 4 else (0.98, 0.78, 0.24)

    # Bottom Terminal / Claude API Token Stream pane
    img[0:108, :, :3] = (0.025, 0.032, 0.052)
    img[106:110, :, :3] = (0.96, 0.68, 0.22)
    for idx, r in enumerate(range(86, 16, -16)):
        img[r : r + 6, 18:46, :3] = (0.24, 0.96, 0.64)
        img[r : r + 6, 56 : 180 + (idx * 65) % 260, :3] = (0.78, 0.88, 0.96) if idx % 2 == 0 else (0.28, 0.88, 0.98)

    return pack_image(name, img, gamma_calibrate=False)


def tex_cardboard_ammo_parcel(name="Tex_CardboardParcel", size=256):
    """Kraft cardboard box texture with blue electrical tape sealing strip and shipping stencil label."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    noise = 0.92 + 0.08 * np.sin(x * 140.0) * np.cos(y * 140.0)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 0.72 * noise
    img[..., 1] = 0.56 * noise
    img[..., 2] = 0.36 * noise
    img[..., 3] = 1.0

    # Blue electrical tape sealing band across top/center seam
    tape_mask = np.abs(y - 0.52) < 0.065
    img[tape_mask, :3] = (0.16, 0.46, 0.92)

    # Cream shipping label with red/dark stamp lines on front
    label = (x > 0.18) & (x < 0.82) & (y > 0.12) & (y < 0.44)
    img[label, :3] = (0.95, 0.90, 0.78)
    red_stamp = (x > 0.22) & (x < 0.58) & (y > 0.32) & (y < 0.40)
    img[red_stamp, :3] = (0.85, 0.22, 0.22)
    barcode = (x > 0.22) & (x < 0.76) & (y > 0.16) & (y < 0.28) & (np.sin(x * 220.0) > 0.0)
    img[barcode, :3] = (0.14, 0.13, 0.16)

    return pack_image(name, img, gamma_calibrate=True)


# ==============================================================================
# MESH PRIMITIVE BUILDERS
# ==============================================================================
def add_box(name, pos, size, mat, rot=(0, 0, 0), bevel=0.010, segments=2):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos(pos), rotation=rl_rot(rot))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.limit_method = "ANGLE"
        mod.angle_limit = math.radians(30)
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_vcyl(name, pos, radius, height, mat, rot=(0, 0, 0), vertices=28, bevel=0.003):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices, radius=radius, depth=height, location=rl_pos(pos), rotation=rl_rot(rot)
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = "ANGLE"
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_sphere(name, pos, radius, mat, scale=(1, 1, 1), segments=20, ring_count=14):
    bpy.ops.mesh.primitive_uv_sphere_add(
        segments=segments, ring_count=ring_count, radius=radius, location=rl_pos(pos)
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(scale)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_tube(name, pts_rl, radius, mat, sides=8):
    """Builds a smooth 3D tubular cable/wire along a list of Raylib-space points."""
    pts = [V(rl_pos(p)) for p in pts_rl]
    n_pts = len(pts)
    verts, faces = [], []
    for idx, p in enumerate(pts):
        if idx == 0:
            tang = (pts[1] - p).normalized()
        elif idx == n_pts - 1:
            tang = (p - pts[-2]).normalized()
        else:
            tang = (pts[idx + 1] - pts[idx - 1]).normalized()
        up = V((0, 0, 1)) if abs(tang.z) < 0.9 else V((1, 0, 0))
        b1 = tang.cross(up).normalized()
        b2 = tang.cross(b1).normalized()
        for s_i in range(sides):
            a = s_i * math.tau / sides
            verts.append(p + b1 * (radius * math.cos(a)) + b2 * (radius * math.sin(a)))
    for r_i in range(n_pts - 1):
        for s_i in range(sides):
            ns = (s_i + 1) % sides
            faces.append((r_i * sides + s_i, r_i * sides + ns, (r_i + 1) * sides + ns, (r_i + 1) * sides + s_i))
    faces.append(tuple(reversed(range(sides))))
    faces.append(tuple(range((n_pts - 1) * sides, n_pts * sides)))
    m = bpy.data.meshes.new(name)
    m.from_pydata(verts, [], faces)
    for poly in m.polygons:
        poly.use_smooth = True
    m.materials.append(mat)
    o = bpy.data.objects.new(name, m)
    bpy.context.collection.objects.link(o)
    return o


def apply_box_uvs(obj, scale_uv=1.2):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active.data
    verts = mesh.vertices
    is_screen = any(k in obj.name for k in ("Display", "Screen"))
    if is_screen and len(verts) > 0:
        xs = [(obj.matrix_world @ v.co).x for v in verts]
        zs = [(obj.matrix_world @ v.co).z for v in verts]
        min_x, max_x = min(xs), max(xs)
        min_z, max_z = min(zs), max(zs)
        sx = max(1e-5, max_x - min_x)
        sz = max(1e-5, max_z - min_z)
        for poly in mesh.polygons:
            for loop_idx in poly.loop_indices:
                vw = obj.matrix_world @ verts[mesh.loops[loop_idx].vertex_index].co
                u = (vw.x - min_x) / sx
                w = (vw.z - min_z) / sz
                uv_layer[loop_idx].uv = (u, w)
        return

    is_parcel = any(k in obj.name for k in ("AmmoBox", "AmmoParcel"))
    if is_parcel and len(verts) > 0:
        xs = [v.co.x for v in verts]
        ys = [v.co.y for v in verts]
        zs = [v.co.z for v in verts]
        min_x, max_x = min(xs), max(xs)
        min_y, max_y = min(ys), max(ys)
        min_z, max_z = min(zs), max(zs)
        sx = max(1e-5, max_x - min_x)
        sy = max(1e-5, max_y - min_y)
        sz = max(1e-5, max_z - min_z)
        for poly in mesh.polygons:
            n = poly.normal
            ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
            for loop_idx in poly.loop_indices:
                vl = verts[mesh.loops[loop_idx].vertex_index].co
                if az >= ax and az >= ay:
                    u, w = (vl.x - min_x) / sx, (vl.y - min_y) / sy
                elif ax >= ay and ax >= az:
                    u, w = (vl.y - min_y) / sy, (vl.z - min_z) / sz
                else:
                    u, w = (vl.x - min_x) / sx, (vl.z - min_z) / sz
                uv_layer[loop_idx].uv = (u, w)
        return

    for poly in mesh.polygons:
        n = poly.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop_idx in poly.loop_indices:
            v = obj.matrix_world @ verts[mesh.loops[loop_idx].vertex_index].co
            if az >= ax and az >= ay:
                u, w = v.x * scale_uv, v.y * scale_uv
            elif ax >= ay and ax >= az:
                u, w = v.y * scale_uv, v.z * scale_uv
            else:
                u, w = v.x * scale_uv, v.z * scale_uv
            uv_layer[loop_idx].uv = (u + 0.5, w + 0.5)


def bake_24ray_ao(obj):
    """Bakes 24-ray hemisphere BVHTree Ambient Occlusion + subtle vertical floor gradient
    into vertex color attribute COLOR_0 (leaving emissive screens/LEDs at 1.0)."""
    mesh = obj.data
    mesh.update()
    verts = [v.co.copy() for v in mesh.vertices]
    polys = [tuple(p.vertices) for p in mesh.polygons]
    tree = BVHTree.FromPolygons(verts, polys, all_triangles=False)

    # Identify emissive material indices so glowing screens/LEDs never get darkened by AO
    emissive_mats = set()
    for idx, m in enumerate(mesh.materials):
        if m and any(k in m.name for k in ("Display", "Glow", "LED", "Screen", "Lamp")):
            emissive_mats.add(idx)
    emissive_verts = set()
    for p in mesh.polygons:
        if p.material_index in emissive_mats:
            emissive_verts.update(p.vertices)

    # Golden-spiral 24 hemisphere directions
    rays = []
    phi = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(24):
        z = (i + 0.5) / 24.0
        r = math.sqrt(max(0.0, 1.0 - z * z))
        theta = i * phi
        rays.append(V((r * math.cos(theta), r * math.sin(theta), z)))

    if "COLOR_0" in mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes["COLOR_0"])
    col_attr = mesh.color_attributes.new(name="COLOR_0", type="BYTE_COLOR", domain="POINT")

    max_z = max((v.z for v in verts), default=1.0)
    for v in mesh.vertices:
        if v.index in emissive_verts:
            col_attr.data[v.index].color = (1.0, 1.0, 1.0, 1.0)
            continue
        n = v.normal.normalized()
        if n.length_squared < 1e-4:
            n = V((0.0, 0.0, 1.0))
        up = V((1.0, 0.0, 0.0)) if abs(n.x) < 0.85 else V((0.0, 1.0, 0.0))
        b1 = n.cross(up).normalized()
        b2 = n.cross(b1).normalized()
        origin = v.co + n * 0.0025

        hits = 0.0
        for r_local in rays:
            r_dir = (b1 * r_local.x + b2 * r_local.y + n * r_local.z).normalized()
            hit_loc, _, _, hit_dist = tree.ray_cast(origin, r_dir, 0.42)
            if hit_loc is not None:
                hits += 1.0 - min(1.0, hit_dist / 0.42)
        ao = 1.0 - 0.44 * (hits / 24.0)
        height_lift = 0.92 + 0.08 * min(1.0, max(0.0, v.co.z / max(0.5, max_z)))
        shade = max(0.50, min(1.0, ao * height_lift))
        col_attr.data[v.index].color = (shade, shade, shade, 1.0)
    mesh.color_attributes.active_color = col_attr


def finalize_and_export(filename, uv_scale=1.1):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in mesh_objs:
        apply_box_uvs(o, scale_uv=uv_scale)
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=0.00001)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=0.000005)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bpy.ops.mesh.customdata_custom_splitnormals_clear()
    obj.data.update()

    bake_24ray_ao(obj)

    out_path = OUT_MODELS / filename
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_vertex_color="ACTIVE",
        export_materials="EXPORT",
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models", "bin/Release/net10.0/win-x64/assets/models"):
        dest = ROOT / sub
        if dest.exists():
            shutil.copy2(out_path, dest / filename)

    verts = [v.co for v in obj.data.vertices]
    mn = tuple(round(min(v[i] for v in verts), 4) for i in range(3))
    mx = tuple(round(max(v[i] for v in verts), 4) for i in range(3))
    tris = len(obj.data.polygons)
    print(f"[EXPORTED WITH 24-RAY AO] {filename}: tris={tris}, BL_min={mn}, BL_max={mx}, bytes={out_path.stat().st_size}")
    return obj


# ==============================================================================
# 1. PROP: DESIGNER WALNUT & SMOKED-GLASS MACBOOK TRAP DESK (prop_macbook_trap_desk.glb)
# Preserves exact tabletop surface Y_rl = 0.940m (for TrapTop = 0.94f * TrapScale)
# and collision footprint 2.48m x 1.44m with floor contact Y_rl = 0.000m!
# ==============================================================================
def build_macbook_trap_desk():
    clear_scene()
    img_walnut = tex_dark_espresso_walnut("Tex_TrapWalnut", 512)
    img_brass = tex_brushed_antique_brass("Tex_TrapBrass", 256)
    img_display = tex_macbook_honeypot_display("Tex_TrapMacBookScreen", 512)

    m_walnut = make_textured_mat("Dark_Espresso_Walnut", img_walnut, metallic=0.06, roughness=0.34)
    m_glass = make_mat("Smoked_Tempered_Glass", srgb_rgba(0.055, 0.070, 0.095), metallic=0.22, roughness=0.14)
    m_brass = make_textured_mat("Brushed_Antique_Brass", img_brass, metallic=0.86, roughness=0.24)
    m_graphite = make_mat("Matte_Graphite_Steel", srgb_rgba(0.11, 0.13, 0.16), metallic=0.72, roughness=0.36)
    m_alu = make_mat("YabloBook_CNC_Aluminum", srgb_rgba(0.58, 0.62, 0.67), metallic=0.85, roughness=0.22)
    m_dark_key = make_mat("Keycap_Charcoal", srgb_rgba(0.06, 0.07, 0.09), metallic=0.18, roughness=0.44)
    m_display = make_textured_mat("YabloBook_Retina_Display", img_display, metallic=0.05, roughness=0.15,
                                  emission_rgb=(0.32, 0.88, 0.96), emission_strength=0.95)
    m_glow_cyan = make_mat("Honeypot_Cyan_LED", srgb_rgba(0.18, 0.94, 0.98), roughness=0.15,
                           emission_rgb=(0.18, 0.94, 0.98), emission_strength=1.75)
    m_glow_green = make_mat("Router_Green_LED", srgb_rgba(0.18, 0.96, 0.48), roughness=0.15,
                            emission_rgb=(0.18, 0.96, 0.48), emission_strength=1.65)
    m_glow_amber = make_mat("MagSafe_Amber_LED", srgb_rgba(0.98, 0.68, 0.16), roughness=0.15,
                            emission_rgb=(0.98, 0.68, 0.16), emission_strength=1.55)
    m_charger = make_mat("Charger_White_Poly", srgb_rgba(0.84, 0.86, 0.88), metallic=0.05, roughness=0.26)
    m_cord = make_mat("Braided_Cable_Dark", srgb_rgba(0.08, 0.09, 0.11), roughness=0.55)
    m_cat6 = make_mat("Cat6_Cobalt_Cable", srgb_rgba(0.10, 0.34, 0.78), roughness=0.42)
    m_can_blue = make_mat("EnergyCan_Cobalt", srgb_rgba(0.10, 0.30, 0.76), metallic=0.78, roughness=0.24)
    m_can_red = make_mat("EnergyCan_Crimson", srgb_rgba(0.72, 0.12, 0.22), metallic=0.78, roughness=0.24)
    m_mug = make_mat("Ceramic_Espresso_Mug", srgb_rgba(0.88, 0.84, 0.76), roughness=0.24)
    m_coffee = make_mat("Coffee_Stain_Roast", srgb_rgba(0.42, 0.22, 0.10), roughness=0.38)
    m_espresso_liquid = make_mat("Dark_Espresso_Liquid", srgb_rgba(0.09, 0.045, 0.02), roughness=0.14)
    m_note_ylw = make_mat("Sticky_Yellow", srgb_rgba(0.92, 0.78, 0.24), roughness=0.65)
    m_note_cyan = make_mat("Sticky_Mint", srgb_rgba(0.28, 0.82, 0.74), roughness=0.65)
    m_note_pink = make_mat("Sticky_Coral", srgb_rgba(0.88, 0.42, 0.56), roughness=0.65)

    # 1. Designer Dark-Walnut Perimeter Tabletop Frame (top strictly at Y_rl = 0.940m!)
    # Left/Right/Front/Back chamfered dark-walnut frame rails surrounding the smoked-glass insert
    add_box("Walnut_Top_FrontRail", (0, 0.895, 0.57), (2.46, 0.090, 0.30), m_walnut, bevel=0.018, segments=3)
    add_box("Walnut_Top_BackRail", (0, 0.895, -0.57), (2.46, 0.090, 0.30), m_walnut, bevel=0.018, segments=3)
    add_box("Walnut_Top_LeftRail", (-1.01, 0.895, 0), (0.44, 0.090, 0.88), m_walnut, bevel=0.018, segments=3)
    add_box("Walnut_Top_RightRail", (1.01, 0.895, 0), (0.44, 0.090, 0.88), m_walnut, bevel=0.018, segments=3)

    # Inset Dark Smoked-Glass Center Panel (top at Y_rl = 0.938m) + Brushed-Brass Inner Bezel Frame
    add_box("Smoked_Glass_Center_Top", (0, 0.916, 0), (1.60, 0.044, 0.86), m_glass, bevel=0.006, segments=2)
    add_box("Glass_Inner_Brass_Front", (0, 0.936, 0.425), (1.62, 0.010, 0.018), m_brass, bevel=0.003)
    add_box("Glass_Inner_Brass_Back", (0, 0.936, -0.425), (1.62, 0.010, 0.018), m_brass, bevel=0.003)
    add_box("Glass_Inner_Brass_Left", (-0.80, 0.936, 0), (0.018, 0.010, 0.86), m_brass, bevel=0.003)
    add_box("Glass_Inner_Brass_Right", (0.80, 0.936, 0), (0.018, 0.010, 0.86), m_brass, bevel=0.003)

    # Outer Brushed-Brass Perimeter Inlay Belt & 4 Chamfered Brass Corner Bumpers
    add_box("Brass_Outer_Perimeter_Belt", (0, 0.895, 0), (2.48, 0.024, 1.44), m_brass, bevel=0.006, segments=2)
    for sx in (-1, 1):
        for sz in (-1, 1):
            add_box(f"Brass_Corner_Guard_{sx}_{sz}", (sx * 1.19, 0.896, sz * 0.67), (0.11, 0.094, 0.11), m_brass, bevel=0.010, segments=2)

    # Under-desk structural graphite apron, cable tray & twin brass cross-ribs (visible under smoked glass)
    add_box("Underframe_Apron", (0, 0.825, 0), (2.18, 0.052, 1.18), m_graphite, bevel=0.012, segments=2)
    add_box("Underdesk_Cable_Tray", (0, 0.790, -0.14), (1.36, 0.036, 0.22), m_graphite, bevel=0.008)
    for bx in (-0.52, 0.0, 0.52):
        add_box(f"UnderGlass_Brass_Crossbar_{bx}", (bx, 0.882, 0), (0.028, 0.022, 0.84), m_brass, bevel=0.004)

    # 2. Four Chamfered Splayed Dark-Walnut & Brushed-Brass Architectural Legs (floor Y_rl = 0.000m!)
    for sx in (-1, 1):
        for sz in (-1, 1):
            lx = sx * 0.92
            lz = sz * 0.44
            tilt_z = sx * -0.18
            tilt_x = sz * 0.08
            add_box(f"Walnut_Leg_Core_{sx}_{sz}", (lx, 0.43, lz), (0.105, 0.78, 0.125), m_walnut,
                    rot=(tilt_x, 0, tilt_z), bevel=0.016, segments=2)
            add_box(f"Brass_Leg_Flute_Outer_{sx}_{sz}", (lx + sx * 0.048, 0.43, lz), (0.024, 0.76, 0.065), m_brass,
                    rot=(tilt_x, 0, tilt_z), bevel=0.005)
            add_box(f"Brass_Leg_Top_Collar_{sx}_{sz}", (lx - sx * 0.05, 0.78, lz - sz * 0.02), (0.14, 0.065, 0.16), m_brass, bevel=0.008)
            add_box(f"Brass_Leg_Ankle_Cuff_{sx}_{sz}", (lx + sx * 0.06, 0.095, lz + sz * 0.025), (0.13, 0.085, 0.15), m_brass, bevel=0.008)
            # Plinth leveling foot pad strictly touching floor Y_rl = 0.000m (center Y = 0.022, height = 0.044)
            add_vcyl(f"Leg_Floor_Glide_{sx}_{sz}", (lx + sx * 0.075, 0.022, lz + sz * 0.03), 0.072, 0.044, m_graphite, vertices=24, bevel=0.005)
            add_vcyl(f"Leg_Floor_BrassRing_{sx}_{sz}", (lx + sx * 0.075, 0.046, lz + sz * 0.03), 0.064, 0.014, m_brass, vertices=24, bevel=0.003)

    # Architectural H-Stretcher Tie-Beams between the 4 legs
    for sx in (-1, 1):
        add_box(f"Side_Stretcher_Bar_{sx}", (sx * 0.94, 0.26, 0), (0.065, 0.055, 0.92), m_graphite, bevel=0.010)
        add_box(f"Side_Stretcher_Brass_{sx}", (sx * 0.97, 0.26, 0), (0.018, 0.026, 0.90), m_brass, bevel=0.004)
    add_box("Center_Stretcher_Walnut", (0, 0.26, 0), (1.88, 0.062, 0.11), m_walnut, bevel=0.012)
    add_box("Center_Stretcher_Brass", (0, 0.26, 0), (1.90, 0.022, 0.124), m_brass, bevel=0.004)

    # Recessed Brushed-Brass Desktop Cable Grommet at (-0.36, 0.940, -0.24)
    add_vcyl("Cable_Grommet_Brass_Bezel", (-0.36, 0.941, -0.24), 0.048, 0.008, m_brass, vertices=24, bevel=0.002)
    add_vcyl("Cable_Grommet_Dark_Slot", (-0.36, 0.943, -0.24), 0.036, 0.008, m_dark_key, vertices=24, bevel=0.001)

    # 3. Glowing Induction Honeypot Pad + Sculpted Brushed-Aluminum ЯблоБук Pro M4 Max
    # Note: Screen opens toward +Z_rl (South lounge / player approach!) so the player sees the glowing IDE!
    lx0, lz0 = -0.06, 0.04
    add_vcyl("Honeypot_Brass_BaseRing", (lx0, 0.942, lz0), 0.42, 0.006, m_brass, vertices=48, bevel=0.002)
    add_vcyl("Honeypot_Cyan_LED_Halo", (lx0, 0.944, lz0), 0.39, 0.007, m_glow_cyan, vertices=48, bevel=0.002)
    add_vcyl("Honeypot_Dark_Pad", (lx0, 0.946, lz0), 0.355, 0.008, m_graphite, vertices=48, bevel=0.002)

    # Laptop base deck (Y_rl = 0.950..0.978)
    add_box("YabloBook_Unibody_Deck", (lx0, 0.964, lz0), (0.62, 0.026, 0.42), m_alu, bevel=0.009, segments=3)
    add_box("YabloBook_Keyboard_Well", (lx0, 0.977, lz0 - 0.035), (0.52, 0.004, 0.19), m_dark_key, bevel=0.004)
    for r_i in range(5):
        for c_i in range(12):
            kx = lx0 - 0.231 + c_i * 0.042
            kz = lz0 - 0.105 + r_i * 0.034
            add_box(f"YabloKey_{r_i}_{c_i}", (kx, 0.981, kz), (0.034, 0.005, 0.025), m_dark_key, bevel=0.0015, segments=1)
    add_box("YabloBook_Spacebar", (lx0, 0.981, lz0 + 0.042), (0.19, 0.005, 0.024), m_dark_key, bevel=0.0015, segments=1)
    add_box("YabloBook_Trackpad_Border", (lx0, 0.977, lz0 + 0.132), (0.22, 0.003, 0.112), m_brass, bevel=0.004)
    add_box("YabloBook_Trackpad_Glass", (lx0, 0.978, lz0 + 0.132), (0.21, 0.003, 0.102), m_alu, bevel=0.004)
    for side in (-1, 1):
        add_box(f"YabloBook_Speaker_{side}", (lx0 + side * 0.282, 0.977, lz0 - 0.035), (0.022, 0.003, 0.175), m_dark_key, bevel=0.001)

    # Tilted Retina Display Lid hinged at rear (lz0 - 0.195), facing +Z_rl toward approaching player!
    lid_tilt = -0.22  # tilted back slightly so screen normal points toward (+Z_rl, +Y_rl)
    hinge_y, hinge_z = 0.976, lz0 - 0.195
    add_vcyl("YabloBook_Display_Hinge", (lx0, hinge_y, hinge_z), 0.011, 0.46, m_graphite, rot=(0, 0, math.pi * 0.5), vertices=16)

    def add_lid_layer(name, h_along, z_offset, w, h, d, mat, bevel=0.004):
        # h_along is distance up the lid from hinge; z_offset > 0 is toward +Z_rl (front screen face!)
        py = hinge_y + h_along * math.cos(lid_tilt) - z_offset * math.sin(lid_tilt)
        pz = hinge_z + h_along * math.sin(lid_tilt) + z_offset * math.cos(lid_tilt)
        return add_box(name, (lx0, py, pz), (w, h, d), mat, rot=(-lid_tilt, 0, 0), bevel=bevel, segments=2)

    add_lid_layer("YabloBook_Aluminum_Lid", 0.205, 0.000, 0.62, 0.41, 0.014, m_alu, 0.006)
    add_lid_layer("YabloBook_Display_Bezel", 0.205, 0.007, 0.596, 0.386, 0.003, m_dark_key, 0.003)
    add_lid_layer("YabloBook_Retina_Display", 0.205, 0.009, 0.568, 0.354, 0.002, m_display, 0.001)
    add_lid_layer("YabloBook_Webcam_Notch", 0.376, 0.010, 0.064, 0.016, 0.002, m_dark_key, 0.001)
    add_lid_layer("YabloBook_Webcam_GreenLED", 0.376, 0.011, 0.006, 0.006, 0.002, m_glow_green, 0.001)
    # Rear glowing cyan emblem on back of lid (-z_offset)
    add_lid_layer("YabloBook_Rear_Cyan_Logo", 0.215, -0.008, 0.072, 0.072, 0.003, m_glow_cyan, 0.010)

    # 4. White USB-C Power Brick & Tangled 3D Braided Cables on Left Side of Desk
    add_box("USBC_Charger_Brick", (-0.64, 0.962, -0.04), (0.095, 0.044, 0.095), m_charger, bevel=0.010, segments=2)
    add_box("USBC_Charger_Collar", (-0.585, 0.956, -0.04), (0.024, 0.018, 0.026), m_alu, bevel=0.003)
    add_box("MagSafe_Connector_Plug", (lx0 - 0.316, 0.964, lz0 - 0.09), (0.018, 0.010, 0.028), m_alu, bevel=0.003)
    add_sphere("MagSafe_Amber_Indicator", (lx0 - 0.322, 0.969, lz0 - 0.09), 0.0045, m_glow_amber, segments=12, ring_count=8)

    # Curved 3D braided MagSafe cord from laptop to brick, plus tangled AC loop into brass grommet
    add_tube("MagSafe_Braided_Cord", [
        (lx0 - 0.322, 0.964, lz0 - 0.09),
        (-0.44, 0.952, 0.03),
        (-0.53, 0.952, 0.12),
        (-0.63, 0.952, 0.06),
        (-0.585, 0.956, -0.04),
    ], 0.0062, m_charger, sides=10)
    add_tube("Charger_AC_Tangled_Cord", [
        (-0.688, 0.958, -0.04),
        (-0.80, 0.952, -0.11),
        (-0.84, 0.952, -0.25),
        (-0.68, 0.952, -0.34),
        (-0.50, 0.952, -0.31),
        (-0.42, 0.952, -0.18),
        (-0.36, 0.938, -0.24),
    ], 0.0068, m_cord, sides=10)

    # 5. Wi-Fi 6 Honeypot Router with 4 Angled Dipole Antennas & Glowing Status LEDs on Right Side
    rx0, rz0 = 0.68, -0.16
    add_box("WiFi6_Router_Chassis", (rx0, 0.962, rz0), (0.28, 0.044, 0.19), m_graphite, rot=(0, -0.22, 0), bevel=0.010, segments=2)
    add_box("WiFi6_Router_Top_Vent", (rx0, 0.985, rz0), (0.22, 0.005, 0.13), m_dark_key, rot=(0, -0.22, 0), bevel=0.002)
    add_box("WiFi6_Router_Brass_Crest", (rx0, 0.987, rz0), (0.14, 0.004, 0.016), m_brass, rot=(0, -0.22, 0), bevel=0.002)

    # 6 glowing status LEDs along the front bevel of the router
    for led_i in range(6):
        off_x = -0.085 + led_i * 0.034
        lx_led = rx0 + off_x * math.cos(0.22) + 0.088 * math.sin(0.22)
        lz_led = rz0 - off_x * math.sin(0.22) + 0.088 * math.cos(0.22)
        led_mat = m_glow_cyan if led_i in (0, 4) else (m_glow_amber if led_i == 5 else m_glow_green)
        add_sphere(f"Router_Status_LED_{led_i}", (lx_led, 0.972, lz_led), 0.0055, led_mat, segments=12, ring_count=8)

    # 4 Articulated Angled Dipole Antennas
    ant_mounts = [
        (rx0 - 0.13, rz0 - 0.08, (-0.22, 0.0, 0.24)),
        (rx0 - 0.05, rz0 - 0.10, (-0.28, 0.0, 0.08)),
        (rx0 + 0.05, rz0 - 0.11, (-0.28, 0.0, -0.08)),
        (rx0 + 0.14, rz0 - 0.09, (-0.22, 0.0, -0.24)),
    ]
    for a_i, (ax, az, a_rot) in enumerate(ant_mounts):
        add_vcyl(f"Router_Antenna_Knuckle_{a_i}", (ax, 0.972, az), 0.011, 0.036, m_dark_key, vertices=14)
        add_box(f"Router_Antenna_Blade_{a_i}", (ax, 1.085, az - 0.025), (0.016, 0.22, 0.008), m_dark_key, rot=a_rot, bevel=0.003)
        add_sphere(f"Router_Antenna_TipLED_{a_i}", (ax - a_rot[2] * 0.09, 1.192, az - 0.052), 0.005, m_glow_cyan, segments=10, ring_count=8)

    # Blue Cat-6 Ethernet cable from router rear to brass desktop grommet
    add_tube("Router_Cat6_PatchCable", [
        (rx0 - 0.02, 0.958, rz0 - 0.09),
        (0.46, 0.952, -0.32),
        (0.14, 0.952, -0.37),
        (-0.18, 0.952, -0.32),
        (-0.36, 0.938, -0.24),
    ], 0.0062, m_cat6, sides=10)

    # 6. Lived-in Clutter: 2 Energy-Drink Cans, Coffee Ring Stain + Mug, and Scattered Sticky Notes
    for c_idx, (cx, cz, c_mat) in enumerate([(0.48, 0.26, m_can_blue), (0.84, 0.16, m_can_red)]):
        add_vcyl(f"EnergyCan_Body_{c_idx}", (cx, 1.014, cz), 0.034, 0.142, c_mat, vertices=24, bevel=0.004)
        add_vcyl(f"EnergyCan_StripeBand_{c_idx}", (cx, 1.028, cz), 0.0346, 0.034, m_brass if c_idx == 1 else m_alu, vertices=24, bevel=0.001)
        add_vcyl(f"EnergyCan_LowerBand_{c_idx}", (cx, 0.978, cz), 0.0345, 0.014, m_dark_key, vertices=24, bevel=0.001)
        add_vcyl(f"EnergyCan_RimTop_{c_idx}", (cx, 1.086, cz), 0.031, 0.006, m_alu, vertices=24, bevel=0.002)
        add_vcyl(f"EnergyCan_RimBot_{c_idx}", (cx, 0.944, cz), 0.031, 0.005, m_alu, vertices=24, bevel=0.002)
        add_box(f"EnergyCan_PullTab_{c_idx}", (cx + 0.006, 1.090, cz), (0.016, 0.003, 0.010), m_alu, bevel=0.001)

    # Dark Espresso Coffee Ring Stain + Ceramic Coffee Mug on left-front corner
    # Double ring stain built as thin dark-roast torus rings flush at Y_rl = 0.944m
    for s_idx, (sx, sz, s_rad) in enumerate([(-0.60, 0.28, 0.052), (-0.585, 0.295, 0.044)]):
        bpy.ops.mesh.primitive_torus_add(
            major_radius=s_rad, minor_radius=0.0055, major_segments=32, minor_segments=8,
            location=rl_pos((sx, 0.943, sz))
        )
        stain = bpy.context.active_object
        stain.name = f"Coffee_Ring_Stain_{s_idx}"
        stain.scale = (1.0, 1.08, 0.28)
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        stain.data.materials.append(m_coffee)
        for p in stain.data.polygons:
            p.use_smooth = True

    add_vcyl("Coffee_Mug_Body", (-0.78, 0.994, 0.22), 0.046, 0.106, m_mug, vertices=24, bevel=0.004)
    add_vcyl("Coffee_Mug_BrassBand", (-0.78, 0.956, 0.22), 0.0468, 0.018, m_brass, vertices=24, bevel=0.001)
    add_vcyl("Coffee_Mug_Espresso", (-0.78, 1.042, 0.22), 0.040, 0.010, m_espresso_liquid, vertices=24, bevel=0.001)
    add_box("Coffee_Mug_Handle", (-0.834, 0.994, 0.22), (0.026, 0.066, 0.015), m_mug, bevel=0.004)

    # 5 Scattered Sticky Notes (Yellow, Mint, Coral) on Tabletop & Laptop Palmrest
    notes = [
        (-0.42, 0.942, 0.24, 0.28, m_note_ylw),
        (-0.32, 0.943, 0.30, -0.35, m_note_cyan),
        (0.34, 0.942, 0.32, 0.18, m_note_pink),
        (0.58, 0.942, 0.06, -0.42, m_note_ylw),
        (lx0 + 0.21, 0.979, lz0 + 0.13, 0.14, m_note_ylw),
    ]
    for n_i, (nx, ny, nz, nyaw, nmat) in enumerate(notes):
        add_box(f"Sticky_Note_{n_i}", (nx, ny, nz), (0.078, 0.003, 0.078), nmat, rot=(0, nyaw, 0), bevel=0.001, segments=1)

    finalize_and_export("prop_macbook_trap_desk.glb", uv_scale=1.1)


# ==============================================================================
# 2. PROP: WALNUT & TUFTED-BURGUNDY VELVET ARSENAL COUNTER (prop_arsenal_counter.glb)
# Matches Veronika's Briefcase aesthetic (26_shop_3d_showcase.png) and preserves
# exact countertop height Y_rl = 1.088..1.100m for DrawWeaponRack(s, false)!
# ==============================================================================
def build_arsenal_counter():
    clear_scene()
    img_walnut = tex_dark_espresso_walnut("Tex_ArsenalWalnut", 512)
    img_velvet = tex_tufted_burgundy_velvet("Tex_ArsenalVelvet", 512)
    img_brass = tex_brushed_antique_brass("Tex_ArsenalBrass", 256)
    img_parcel = tex_cardboard_ammo_parcel("Tex_ArsenalParcel", 256)

    m_walnut = make_textured_mat("Arsenal_Espresso_Walnut", img_walnut, metallic=0.06, roughness=0.34)
    m_velvet = make_textured_mat("Arsenal_Tufted_Burgundy_Velvet", img_velvet, metallic=0.04, roughness=0.72)
    m_brass = make_textured_mat("Arsenal_Antique_Brass", img_brass, metallic=0.86, roughness=0.24)
    m_parcel = make_textured_mat("Cardboard_Ammo_Box", img_parcel, metallic=0.02, roughness=0.68)
    m_slate = make_mat("Arsenal_Dark_Slate", srgb_rgba(0.12, 0.14, 0.18), metallic=0.45, roughness=0.38)
    m_crate = make_mat("Military_Surplus_Olive", srgb_rgba(0.28, 0.36, 0.22), metallic=0.15, roughness=0.50)
    m_parchment = make_mat("Cream_Price_Tag", srgb_rgba(0.92, 0.86, 0.72), roughness=0.55)
    m_lamp = make_mat("Gallery_Warm_Spot", srgb_rgba(1.0, 0.86, 0.56), roughness=0.15,
                      emission_rgb=(1.0, 0.82, 0.46), emission_strength=1.55)
    m_tape = make_mat("Cobalt_Electrical_Tape", srgb_rgba(0.14, 0.42, 0.88), roughness=0.34)

    # 1. Brushed-Brass Recessed Kickplate Plinth resting flush on floor Y_rl = 0.000m
    add_box("Counter_Brass_Kickplate", (0, 0.045, -0.02), (4.14, 0.090, 1.12), m_brass, bevel=0.008, segments=2)
    for sx in (-2.04, -1.224, -0.408, 0.408, 1.224, 2.04):
        add_box(f"Plinth_Brass_Boot_{sx:.2f}", (sx, 0.055, -0.56), (0.14, 0.11, 0.12), m_brass, bevel=0.010, segments=2)

    # 2. Main Dark-Espresso Walnut Cabinet Core & 6 Fluted Walnut/Brass Pilasters (Y_rl = 0.09..1.04m)
    add_box("Counter_Walnut_Core", (0, 0.56, 0.04), (4.12, 0.95, 1.04), m_walnut, bevel=0.018, segments=2)
    for p_idx, px in enumerate((-2.04, -1.224, -0.408, 0.408, 1.224, 2.04)):
        add_box(f"Front_Walnut_Pilaster_{p_idx}", (px, 0.56, -0.52), (0.11, 0.95, 0.12), m_walnut, bevel=0.012, segments=2)
        add_box(f"Pilaster_Brass_Flute_{p_idx}", (px, 0.56, -0.582), (0.028, 0.88, 0.018), m_brass, bevel=0.004)
        add_box(f"Pilaster_Brass_CapTop_{p_idx}", (px, 1.00, -0.53), (0.125, 0.042, 0.135), m_brass, bevel=0.005)

    # 5 Front Bays: Alternating Tufted-Burgundy Velvet Showcase Panels & Open Under-Counter Shelf Alcoves
    # with Cardboard Ammo/Hardware Boxes!
    bay_centers = (-1.632, -0.816, 0.0, 0.816, 1.632)
    for b_idx, bx in enumerate(bay_centers):
        # Upper front Tufted-Burgundy Velvet Frieze Panel (Y_rl = 0.64..1.00m) with Brass Frame & Corner L-Brackets
        add_box(f"Front_Velvet_Panel_{b_idx}", (bx, 0.82, -0.50), (0.71, 0.34, 0.035), m_velvet, bevel=0.008, segments=2)
        add_box(f"Velvet_Brass_FrameTop_{b_idx}", (bx, 0.99, -0.52), (0.72, 0.022, 0.025), m_brass, bevel=0.003)
        add_box(f"Velvet_Brass_FrameBot_{b_idx}", (bx, 0.65, -0.52), (0.72, 0.022, 0.025), m_brass, bevel=0.003)
        # 3 Diamond tuft brass buttons per velvet panel
        for tx_off in (-0.22, 0.0, 0.22):
            add_sphere(f"Velvet_Tuft_Button_{b_idx}_{tx_off:.2f}", (bx + tx_off, 0.82, -0.520), 0.012, m_brass, scale=(1.0, 1.0, 0.45), segments=12, ring_count=8)

        # Lower Under-Counter Recessed Walnut Shelf Alcove (Y_rl = 0.09..0.63m)
        add_box(f"Alcove_Bottom_Shelf_{b_idx}", (bx, 0.105, -0.56), (0.72, 0.032, 0.38), m_walnut, bevel=0.006)
        add_box(f"Alcove_Mid_Shelf_{b_idx}", (bx, 0.365, -0.55), (0.70, 0.028, 0.34), m_walnut, bevel=0.006)
        add_box(f"Alcove_Mid_BrassLip_{b_idx}", (bx, 0.365, -0.722), (0.70, 0.020, 0.012), m_brass, bevel=0.002)
        add_box(f"Alcove_Bot_BrassLip_{b_idx}", (bx, 0.105, -0.752), (0.72, 0.020, 0.012), m_brass, bevel=0.002)

        # Stock the under-counter shelves with Cardboard Ammo/Hardware Parcel Boxes & Surplus Cases!
        yaw_a = (b_idx % 3 - 1) * 0.08
        add_box(f"Cardboard_AmmoBox_BotL_{b_idx}", (bx - 0.17, 0.215, -0.56), (0.29, 0.19, 0.28), m_parcel, rot=(0, yaw_a, 0), bevel=0.008)
        add_box(f"Cardboard_AmmoBox_BotR_{b_idx}", (bx + 0.17, 0.200, -0.55), (0.27, 0.16, 0.26), m_crate if b_idx % 2 == 0 else m_parcel, rot=(0, -yaw_a, 0), bevel=0.008)
        add_box(f"Crate_Brass_Latch_{b_idx}", (bx + 0.17, 0.22, -0.685), (0.08, 0.035, 0.012), m_brass, bevel=0.002)
        # Upper shelf cardboard boxes + blue electrical tape rolls
        add_box(f"Cardboard_AmmoBox_Mid_{b_idx}", (bx - 0.08, 0.465, -0.55), (0.36, 0.17, 0.26), m_parcel, rot=(0, -yaw_a * 0.7, 0), bevel=0.008)
        add_vcyl(f"Spare_BlueTape_Roll_{b_idx}", (bx + 0.21, 0.405, -0.54), 0.052, 0.048, m_tape, vertices=20, bevel=0.004)

    # Side panels of the counter (X = ±2.08m) framed in walnut, tufted velvet, and brass
    for sx in (-1, 1):
        add_box(f"Side_Velvet_Inlay_{sx}", (sx * 2.07, 0.56, 0.02), (0.03, 0.76, 0.86), m_velvet, bevel=0.006)
        add_box(f"Side_Brass_BorderTop_{sx}", (sx * 2.085, 0.95, 0.02), (0.018, 0.024, 0.90), m_brass, bevel=0.003)
        add_box(f"Side_Brass_BorderBot_{sx}", (sx * 2.085, 0.17, 0.02), (0.018, 0.024, 0.90), m_brass, bevel=0.003)

    # 3. Overhanging Chamfered Dark-Espresso Walnut Bullnose Countertop (top strictly at Y_rl = 1.088m!)
    add_box("Walnut_Bullnose_Countertop", (0, 1.054, -0.04), (4.30, 0.068, 1.30), m_walnut, bevel=0.016, segments=3)
    add_box("Countertop_Brass_Perimeter_Belt", (0, 1.054, -0.04), (4.32, 0.024, 1.32), m_brass, bevel=0.005, segments=2)
    for sx in (-1, 1):
        for sz in (-0.69, 0.61):
            add_box(f"Countertop_Brass_Corner_{sx}_{sz:.2f}", (sx * 2.11, 1.056, sz), (0.12, 0.072, 0.12), m_brass, bevel=0.008, segments=2)

    # 4. Plush Tufted-Burgundy Velvet Weapon Display Mats on Top of the Counter (Y_rl = 1.088..1.102m)
    # Placed at exact DrawWeaponRack X stations (-1.66, -0.83, 0.00, +0.83) + 5th station (+1.66) at Z = -0.28!
    for s_idx, wx in enumerate((-1.66, -0.83, 0.00, 0.83, 1.66)):
        # Outer chamfered brushed-brass tray frame surrounding the plush burgundy velvet mat
        add_box(f"Weapon_Display_BrassTray_{s_idx}", (wx, 1.094, -0.28), (0.79, 0.018, 0.44), m_brass, bevel=0.005, segments=2)
        add_box(f"Weapon_Display_VelvetMat_{s_idx}", (wx, 1.099, -0.28), (0.75, 0.018, 0.40), m_velvet, bevel=0.006, segments=2)
        # Cream parchment & brass front price/spec plaque angled toward the player at z = -0.53
        add_box(f"Weapon_Plaque_BrassBase_{s_idx}", (wx, 1.102, -0.535), (0.28, 0.028, 0.055), m_brass, rot=(0.35, 0, 0), bevel=0.003)
        add_box(f"Weapon_Plaque_Parchment_{s_idx}", (wx, 1.108, -0.538), (0.25, 0.022, 0.045), m_parchment, rot=(0.35, 0, 0), bevel=0.002)

    # Rightmost counter top accessories (at z = +0.18, behind the 5th mat so nothing blocks the weapons):
    # Vintage Brass Service Bell + Stacked Ammo Boxes
    add_vcyl("Counter_Service_Bell_Base", (1.82, 1.096, -0.48), 0.045, 0.016, m_slate, vertices=20, bevel=0.002)
    add_sphere("Counter_Service_Bell_Dome", (1.82, 1.115, -0.48), 0.038, m_brass, scale=(1.0, 0.85, 1.0), segments=18, ring_count=12)
    add_sphere("Counter_Service_Bell_Button", (1.82, 1.150, -0.48), 0.009, m_brass, segments=10, ring_count=8)

    # 5. Grand Walnut, Brushed-Brass & Tufted-Burgundy Backwall Showcase Canopy (Z_rl = +0.46..+0.62, Y_rl = 1.088..3.22m)
    add_box("Backwall_Walnut_Backing", (0, 2.12, 0.56), (4.22, 2.08, 0.10), m_walnut, bevel=0.016, segments=2)
    add_box("Backwall_Velvet_Backdrop", (0, 2.10, 0.505), (3.96, 1.88, 0.035), m_velvet, bevel=0.010, segments=2)

    # 6 Vertical Walnut & Brass Backwall Columns dividing the canopy into 5 display bays
    for c_idx, cx in enumerate((-2.04, -1.224, -0.408, 0.408, 1.224, 2.04)):
        add_box(f"Backwall_Walnut_Column_{c_idx}", (cx, 2.10, 0.48), (0.105, 1.98, 0.12), m_walnut, bevel=0.012, segments=2)
        add_box(f"Backwall_Column_BrassStrip_{c_idx}", (cx, 2.10, 0.415), (0.026, 1.92, 0.016), m_brass, bevel=0.003)

    # Two full-width Walnut & Brass Backwall Shelves (Y_rl = 1.68m and Y_rl = 2.32m) stocked with Ammo/Hardware Boxes
    for sh_i, sy in enumerate((1.68, 2.32)):
        add_box(f"Backwall_Walnut_Shelf_{sh_i}", (0, sy, 0.36), (4.06, 0.042, 0.28), m_walnut, bevel=0.008, segments=2)
        add_box(f"Backwall_Shelf_BrassEdge_{sh_i}", (0, sy, 0.218), (4.08, 0.024, 0.014), m_brass, bevel=0.003)
        for b_idx, bx in enumerate(bay_centers):
            add_box(f"Shelf_AmmoParcel_{sh_i}_{b_idx}", (bx - 0.14, sy + 0.105, 0.35), (0.28, 0.17, 0.20), m_parcel,
                    rot=(0, (b_idx - 2) * 0.06, 0), bevel=0.006)
            add_box(f"Shelf_SurplusCase_{sh_i}_{b_idx}", (bx + 0.16, sy + 0.095, 0.36), (0.24, 0.15, 0.18), m_crate,
                    rot=(0, (2 - b_idx) * 0.05, 0), bevel=0.006)
            add_box(f"Shelf_Case_BrassClip_{sh_i}_{b_idx}", (bx + 0.16, sy + 0.11, 0.268), (0.07, 0.03, 0.01), m_brass, bevel=0.002)

    # Overhanging Chamfered Walnut & Brushed-Brass Cornice Canopy Header (Y_rl = 3.06..3.22m) with 5 Warm Spotlights
    add_box("Canopy_Walnut_Cornice", (0, 3.13, 0.40), (4.28, 0.16, 0.42), m_walnut, bevel=0.016, segments=2)
    add_box("Canopy_Brass_Fascia_Belt", (0, 3.13, 0.188), (4.30, 0.048, 0.018), m_brass, bevel=0.004)
    add_box("Canopy_Velvet_Nameplate_Back", (0, 3.13, 0.178), (1.84, 0.10, 0.016), m_velvet, bevel=0.004)
    add_box("Canopy_Brass_Nameplate_Bar", (0, 3.13, 0.168), (1.76, 0.065, 0.012), m_brass, bevel=0.003)
    for b_idx, bx in enumerate(bay_centers):
        add_vcyl(f"Canopy_Brass_Downlight_Ring_{b_idx}", (bx, 3.045, 0.32), 0.072, 0.018, m_brass, vertices=20, bevel=0.002)
        add_vcyl(f"Canopy_Warm_Downlight_Lens_{b_idx}", (bx, 3.040, 0.32), 0.056, 0.012, m_lamp, vertices=20, bevel=0.001)

    finalize_and_export("prop_arsenal_counter.glb", uv_scale=1.0)


# ==============================================================================
# 3. RENDER 2-PANEL STUDIO PROOF (office_hero_counters_proof.png)
# ==============================================================================
def render_proof():
    panels = []
    # Note: In Blender (Y_bl = -Z_rl):
    # - prop_macbook_trap_desk front (+Z_rl, player approach) is at -Y_bl -> camera at Y_bl < 0
    # - prop_arsenal_counter front (-Z_rl, lobby/weapon pads) is at +Y_bl -> camera at Y_bl > 0
    configs = [
        (
            "prop_macbook_trap_desk.glb",
            "_hero_panel_macbook_desk.png",
            (1.48, -1.92, 1.54),
            (0.0, 0.0, 0.72),
            40.0,
            [
                ("KeySun", (3.0, -3.5, 4.5), (0.0, 0.0, 0.8), 4.4, (1.0, 0.94, 0.84)),
                ("FillSun", (-3.5, -2.5, 2.8), (0.0, 0.0, 0.8), 2.8, (0.76, 0.88, 1.0)),
                ("RimSun", (0.0, 3.5, 3.2), (0.0, 0.0, 0.8), 2.6, (1.0, 0.82, 0.58)),
            ],
        ),
        (
            "prop_arsenal_counter.glb",
            "_hero_panel_arsenal_counter.png",
            (-2.15, 4.10, 2.15),
            (0.0, 0.10, 1.38),
            36.0,
            [
                ("KeySun", (-2.8, 4.6, 4.2), (0.0, 0.2, 1.2), 4.8, (1.0, 0.93, 0.82)),
                ("FillSun", (3.4, 3.8, 2.8), (0.0, 0.2, 1.2), 3.2, (0.80, 0.90, 1.0)),
                ("RimSun", (0.0, -3.5, 3.6), (0.0, 0.0, 1.4), 2.4, (1.0, 0.82, 0.58)),
            ],
        ),
    ]

    for glb_name, out_png, cam_loc, look_target, lens, sun_specs in configs:
        clear_scene()
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / glb_name))
        for o in bpy.context.scene.objects:
            if o.type == "MESH":
                for m in o.data.materials:
                    if m:
                        m.use_backface_culling = True

        # Studio dark slate floor pad for grounded contact verification
        bpy.ops.mesh.primitive_plane_add(size=16.0, location=(0.0, 0.0, 0.0))
        fl = bpy.context.active_object
        m_fl = make_mat("ProofFloor", srgb_rgba(0.08, 0.095, 0.125), metallic=0.15, roughness=0.42)
        fl.data.materials.append(m_fl)

        scene = bpy.context.scene
        scene.render.engine = "BLENDER_EEVEE"
        scene.render.resolution_x = 960
        scene.render.resolution_y = 900
        scene.render.film_transparent = False

        world = bpy.data.worlds.new("HeroProofWorld")
        scene.world = world
        world.use_nodes = True
        bg = world.node_tree.nodes.get("Background")
        if bg:
            bg.inputs[0].default_value = (0.055, 0.068, 0.092, 1.0)
            bg.inputs[1].default_value = 1.45

        for lname, lpos, ltarget, energy, col in sun_specs:
            ldata = bpy.data.lights.new(name=lname, type="SUN")
            ldata.energy = energy
            ldata.color = col
            lobj = bpy.data.objects.new(name=lname, object_data=ldata)
            scene.collection.objects.link(lobj)
            lobj.location = lpos
            ldir = V(ltarget) - V(lpos)
            lobj.rotation_euler = ldir.to_track_quat("-Z", "Y").to_euler()

        cam_data = bpy.data.cameras.new("ProofCam")
        cam_data.lens = lens
        cam_obj = bpy.data.objects.new("ProofCam", cam_data)
        scene.collection.objects.link(cam_obj)
        scene.camera = cam_obj
        cam_obj.location = cam_loc
        direction = V(look_target) - V(cam_loc)
        cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

        p_path = MY_SCRATCH_DIR / out_png
        p_path.parent.mkdir(parents=True, exist_ok=True)
        scene.render.filepath = str(p_path)
        bpy.ops.render.render(write_still=True)
        panels.append(p_path)

    img1 = bpy.data.images.load(str(panels[0]))
    img2 = bpy.data.images.load(str(panels[1]))
    w1, h1 = img1.size
    px1 = np.array(img1.pixels[:], dtype=np.float32).reshape((h1, w1, 4))
    px2 = np.array(img2.pixels[:], dtype=np.float32).reshape((h1, w1, 4))
    combined = np.concatenate([px1, px2], axis=1)
    combined[:, w1 - 2 : w1 + 2, :3] = (0.24, 0.30, 0.38)

    out_img = bpy.data.images.new("OfficeHeroCountersProof", width=w1 * 2, height=h1, alpha=False)
    out_img.pixels.foreach_set(combined.ravel())

    out_paths = [
        ARTIFACT_DIR / "office_hero_counters_proof.png",
        MY_SCRATCH_DIR / "office_hero_counters_proof.png",
        SCREENSHOTS_DIR / "office_hero_counters_proof.png",
    ]
    out_paths[0].parent.mkdir(parents=True, exist_ok=True)
    out_img.filepath_raw = str(out_paths[0])
    out_img.file_format = "PNG"
    out_img.save()
    for dst in out_paths[1:]:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(out_paths[0], dst)
    print(f"[HERO COUNTERS PROOF RENDERED] {out_paths[0]}")


if __name__ == "__main__":
    build_macbook_trap_desk()
    build_arsenal_counter()
    render_proof()
