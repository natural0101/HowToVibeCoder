"""High-craft Blender 5.1 generator for the 3 signature Floor 1 landmark GLBs:
  1. assets/models/prop_server_rack.glb
  2. assets/models/prop_macbook_trap_desk.glb
  3. assets/models/prop_hr_reception_desk.glb

Preserves exact bounding boxes, origins, and gameplay heights (e.g. TrapTop = 0.94m)
while adding smooth beveled geometry, weighted normals, box-projected UVs, and
embedded procedural PBR textures (marble, brushed brass, quilted velvet, server matrix,
MacBook Retina IDE display, and Tarot spread).
"""
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parent
OUT_MODELS = ROOT / "assets/models"
OUT_MODELS.mkdir(parents=True, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def rl_pos(p):
    return (p[0], -p[2], p[1])


def rl_scale(s):
    return (s[0], s[2], s[1])


def rl_rot(r):
    return (r[0], -r[2], r[1])


def pack_image(name, rgb_array):
    """Packs an HxWx4 float32 RGBA numpy array (0..1) into a Blender Image."""
    h, w, _ = rgb_array.shape
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    flat = np.flipud(rgb_array).reshape(-1)
    img.pixels.foreach_set(flat.astype(np.float32))
    img.pack()
    return img


def make_textured_mat(name, img, metallic=0.15, roughness=0.35, emission_rgb=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission_rgb is not None and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = img
    links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if emission_rgb is not None and emission_strength > 0 and "Emission Color" in bsdf.inputs:
        links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
    return mat


def make_mat(name, rgba, metallic=0.15, roughness=0.35, emission_rgb=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission_rgb is not None and "Emission Color" in bsdf.inputs:
        bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


# ==============================================================================
# PROCEDURAL TEXTURE GENERATORS (512x512 embedded PNGs)
# ==============================================================================
def tex_obsidian_marble(name="Tex_ObsidianMarble", size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    rng = np.random.default_rng(1998)
    # Multi-octave organic FBM turbulence (non-periodic, natural Nero Marquina slabs)
    turb = np.zeros((size, size), dtype=np.float32)
    turb2 = np.zeros((size, size), dtype=np.float32)
    for freq, amp in [(1.3, 0.45), (2.7, 0.26), (5.3, 0.14), (10.7, 0.07), (21.1, 0.035)]:
        ang = rng.uniform(0, math.tau)
        ph1, ph2 = rng.uniform(0, math.tau), rng.uniform(0, math.tau)
        u = x * math.cos(ang) - y * math.sin(ang)
        v = x * math.sin(ang) + y * math.cos(ang)
        turb += amp * np.sin(u * freq * math.tau + ph1) * np.cos(v * freq * math.tau + ph2)
        turb2 += amp * np.cos((u + 0.3 * v) * freq * math.tau + ph2)

    base = np.zeros((size, size, 4), dtype=np.float32)
    cloud = np.clip(0.5 + 0.45 * turb, 0.0, 1.0)
    # Deep polished obsidian-anthracite base (#0B0D12)
    base[..., 0] = 0.048 + 0.032 * cloud
    base[..., 1] = 0.056 + 0.036 * cloud
    base[..., 2] = 0.072 + 0.045 * cloud

    # Sparse, natural branching mineral veins (only along sharp FBM zero-crossings with variable mask)
    mask1 = np.clip(0.65 + 0.65 * turb2, 0.0, 1.0) ** 2.2
    vein_gold = np.exp(-((np.abs(turb + 0.18 * turb2 - 0.12) / 0.011) ** 2)) * mask1
    vein_silver = np.exp(-((np.abs(turb2 - 0.22 * turb + 0.19) / 0.008) ** 2)) * np.clip(0.5 - 0.7 * turb, 0.0, 1.0)

    gold_col = (0.64, 0.49, 0.26)
    silv_col = (0.42, 0.46, 0.52)
    for c in range(3):
        base[..., c] = base[..., c] * (1.0 - 0.65 * vein_gold) + gold_col[c] * vein_gold * 0.72
        base[..., c] = base[..., c] * (1.0 - 0.50 * vein_silver) + silv_col[c] * vein_silver * 0.55

    base[..., 3] = 1.0
    return pack_image(name, np.clip(base, 0.0, 1.0))


def tex_brushed_brass(name="Tex_BrushedBrass", size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    rng = np.random.default_rng(1994)
    streak = rng.normal(0.0, 0.035, (size, 1)).astype(np.float32)
    streak = np.repeat(streak, size, axis=1)
    shimmer = 0.06 * np.sin(x * math.tau * 2.0) + 0.04 * np.cos(y * math.tau * 8.0)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 0.78 + streak + shimmer
    img[..., 1] = 0.58 + streak * 0.8 + shimmer * 0.8
    img[..., 2] = 0.26 + streak * 0.4 + shimmer * 0.4
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_quilted_velvet(name="Tex_QuiltedVelvet", size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    # Diamond quilting pattern
    u = (x * 8.0 + y * 5.0) % 1.0
    v = (x * 8.0 - y * 5.0) % 1.0
    du = np.minimum(u, 1.0 - u)
    dv = np.minimum(v, 1.0 - v)
    seam = np.minimum(du, dv)
    pillow = np.clip(np.sin(u * math.pi) * np.sin(v * math.pi), 0.0, 1.0) ** 0.45
    stitch = np.exp(-((seam / 0.035) ** 2))
    # Button dimple at diamond intersections
    tuft = np.exp(-(((u - 0.5) ** 2 + (v - 0.5) ** 2) / 0.008))

    img = np.zeros((size, size, 4), dtype=np.float32)
    # Rich royal plum / burgundy velvet with golden button tufts
    shade = 0.35 + 0.65 * pillow * (1.0 - 0.55 * stitch)
    img[..., 0] = 0.34 * shade + 0.52 * tuft
    img[..., 1] = 0.08 * shade + 0.38 * tuft
    img[..., 2] = 0.30 * shade + 0.14 * tuft
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_server_blade_matrix(name="Tex_ServerMatrix", size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    u = x / size
    v = y / size
    img = np.zeros((size, size, 4), dtype=np.float32)
    # Gunmetal anodized steel chassis background
    img[..., 0] = 0.14
    img[..., 1] = 0.17
    img[..., 2] = 0.22

    # Horizontal perforation slots / honeycomb grille across drive bays
    gx = np.sin(u * math.tau * 36.0)
    gy = np.sin(v * math.tau * 24.0)
    holes = (gx > 0.15) & (gy > 0.15)
    img[holes, 0] = 0.04
    img[holes, 1] = 0.05
    img[holes, 2] = 0.07

    # Brushed steel bay dividers
    bay_border = (np.abs((u * 8.0) % 1.0 - 0.5) > 0.44) | (np.abs((v * 4.0) % 1.0 - 0.5) > 0.42)
    img[bay_border, 0] = 0.26
    img[bay_border, 1] = 0.30
    img[bay_border, 2] = 0.36

    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_macbook_display(name="Tex_MacBookDisplay", size=512):
    """Realistic 512x256 dark IDE (#0B0F14) with 15% file-tree sidebar, tab bar, 2px indented token lines, minimap & compact ACCEPT ALL modal."""
    h, w = 256, 512
    img = np.zeros((h, w, 4), dtype=np.float32)
    img[..., 0] = 0.043
    img[..., 1] = 0.059
    img[..., 2] = 0.078
    img[..., 3] = 1.0

    # Left file-tree sidebar (15% width)
    img[:, :78, :3] = (0.028, 0.039, 0.055)
    img[:, 77:79, :3] = (0.09, 0.13, 0.18)
    rng = np.random.default_rng(1998)
    for row in range(18, h - 22, 8):
        indent = 8 + (row % 3) * 6
        tlen = rng.integers(24, 52)
        img[row : row + 2, indent : min(72, indent + tlen), :3] = (0.34, 0.44, 0.56)
        img[row : row + 2, indent - 5 : indent - 2, :3] = (0.18, 0.72, 0.88) if row % 16 == 2 else (0.78, 0.62, 0.26)

    # Top tab bar & bottom status bar (in Blender UV v=0 is bottom, v=1 is top)
    img[h - 18 :, 78:, :3] = (0.032, 0.045, 0.064)
    img[h - 17 : h - 3, 84:182, :3] = (0.058, 0.082, 0.115)
    img[h - 3 : h - 1, 84:182, :3] = (0.14, 0.85, 0.96)
    img[:12, :, :3] = (0.05, 0.28, 0.38)

    # Thin 2px left-aligned indented code lines with token segments (step 6px)
    palette = [
        (0.24, 0.82, 0.96),  # cyan keyword
        (0.76, 0.82, 0.90),  # slate identifier
        (0.96, 0.74, 0.32),  # amber literal
        (0.38, 0.90, 0.64),  # mint type
        (0.68, 0.52, 0.94),  # violet call
    ]
    indent_levels = [0, 1, 2, 2, 3, 2, 1, 0, 0, 1, 2, 3, 3, 2, 1, 2, 1, 0]
    for idx, row in enumerate(range(h - 28, 22, -6)):
        if idx % 7 == 6:
            continue  # Blank line between functions
        ind = indent_levels[idx % len(indent_levels)]
        cx = 92 + ind * 14
        # Gutter line number
        img[row : row + 2, 82:87, :3] = (0.22, 0.29, 0.38)
        n_tokens = rng.integers(3, 7)
        for t in range(n_tokens):
            tw = rng.integers(14, 46)
            if cx + tw > 425:
                break
            col = palette[(idx + t * 2) % len(palette)]
            img[row : row + 2, cx : cx + tw, :3] = col
            cx += tw + rng.integers(5, 11)

    # Right minimap column
    img[18 : h - 20, 452:502, :3] = (0.034, 0.047, 0.065)
    for mrow in range(24, h - 24, 3):
        mw = 12 + ((mrow * 13) % 28)
        img[mrow : mrow + 1, 456 : 456 + mw, :3] = (0.20, 0.36, 0.46)

    # Compact floating "CLOUD ENTROPIC MAX // ACCEPT ALL" diff widget in bottom-right
    img[22:74, 286:442, :3] = (0.055, 0.085, 0.125)
    img[72:74, 286:442, :3] = (0.16, 0.88, 0.74)
    img[28:46, 300:382, :3] = (0.12, 0.86, 0.58)  # Emerald Accept All pill
    img[28:46, 390:430, :3] = (0.22, 0.28, 0.36)  # Reject pill
    img[54:57, 300:418, :3] = (0.78, 0.88, 0.96)
    img[62:65, 300:376, :3] = (0.28, 0.84, 0.96)

    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_zero_oracle_display(name="Tex_ZeroOracleScreen", size=512):
    """Dedicated 512x256 SYSTEM ZERO // HR PREDICTION API terminal screen (arcane natal radar + SQL telemetry)."""
    h, w = 256, 512
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    img = np.zeros((h, w, 4), dtype=np.float32)
    img[..., :3] = (0.035, 0.038, 0.058)
    img[..., 3] = 1.0

    # Top header bar & frame borders
    img[h - 20 :, :, :3] = (0.075, 0.055, 0.115)
    img[h - 20 : h - 18, :, :3] = (0.86, 0.66, 0.26)
    img[h - 12 : h - 8, 16:168, :3] = (0.94, 0.76, 0.34)

    # Left panel: Natal / Tarot Prediction Radar Wheel
    cx, cy = 118.0, 118.0
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2)
    for rad, col in [(76.0, (0.78, 0.58, 0.24)), (52.0, (0.22, 0.78, 0.82)), (28.0, (0.72, 0.36, 0.86))]:
        ring = np.abs(r - rad) < 1.3
        img[ring, :3] = col
    cross = ((np.abs(x - cx) < 1.0) & (r < 78.0)) | ((np.abs(y - cy) < 1.0) & (r < 78.0))
    img[cross, :3] = (0.35, 0.28, 0.46)

    # Right panel: Candidate scoring bars & thin SQL prediction log lines
    img[18 : h - 26, 224:226, :3] = (0.28, 0.22, 0.38)
    rng = np.random.default_rng(2026)
    for idx, row in enumerate(range(h - 34, 96, -7)):
        tx = 238
        for t in range(rng.integers(2, 5)):
            tw = rng.integers(18, 54)
            if tx + tw > 492:
                break
            col = (0.92, 0.74, 0.32) if (idx + t) % 3 == 0 else ((0.26, 0.86, 0.88) if t % 2 == 0 else (0.72, 0.68, 0.84))
            img[row : row + 2, tx : tx + tw, :3] = col
            tx += tw + 8

    # Bottom-right risk telemetry progress bars (TD / PT / SEC / BURNOUT)
    for b_idx, (brow, bfill, bcol) in enumerate([
        (72, 180, (0.22, 0.88, 0.66)),
        (54, 142, (0.94, 0.72, 0.26)),
        (36, 210, (0.24, 0.82, 0.94)),
        (18, 115, (0.88, 0.34, 0.68)),
    ]):
        img[brow : brow + 8, 238:486, :3] = (0.06, 0.07, 0.10)
        img[brow + 1 : brow + 7, 240 : 240 + bfill, :3] = bcol

    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_tarot_card_gold(name="Tex_TarotSpread", size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    img = np.zeros((size, size, 4), dtype=np.float32)
    # Ivory-gold parchment card with dark arcane border and geometric circle
    border = (x < 0.08) | (x > 0.92) | (y < 0.06) | (y > 0.94)
    inner = (np.abs(x - 0.5) < 0.36) & (np.abs(y - 0.5) < 0.40)
    r = np.sqrt((x - 0.5) ** 2 + (y - 0.5) ** 2)
    ring = np.abs(r - 0.24) < 0.025
    star = np.abs(x - 0.5) + np.abs(y - 0.5) < 0.16

    img[..., :3] = (0.92, 0.82, 0.58)
    img[border, :3] = (0.16, 0.08, 0.20)
    img[inner, :3] = (0.12, 0.07, 0.18)
    img[ring, :3] = (0.96, 0.78, 0.32)
    img[star, :3] = (0.28, 0.88, 0.82)
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


# ==============================================================================
# MESH HELPERS WITH BOX-PROJECTED UVs & SMOOTH NORMALS
# ==============================================================================
def add_box(name, pos, size, mat, rot=(0, 0, 0), bevel=0.012, segments=2):
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


def add_vcyl(name, pos, radius, height, mat, rot=(0, 0, 0), vertices=28, bevel=0.004):
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


def add_rod(name, p0, p1, r0, r1, mat, vertices=18):
    v0 = Vector(rl_pos(p0))
    v1 = Vector(rl_pos(p1))
    d = v1 - v0
    length = d.length
    if length < 1e-5:
        return None
    mid = (v0 + v1) * 0.5
    bpy.ops.mesh.primitive_cone_add(vertices=vertices, radius1=r0, radius2=r1, depth=length, location=mid)
    obj = bpy.context.active_object
    obj.name = name
    quat = Vector((0, 0, 1)).rotation_difference(d.normalized())
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = quat
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.use_smooth = True
    return obj


def add_sphere(name, pos, radius, mat, scale=(1, 1, 1), segments=24, ring_count=16):
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


def apply_box_uvs(obj, scale_uv=1.2):
    """Assigns clean triplanar/box-projected UVs based on polygon normal, or normalized 0..1 planar UVs for displays/cards."""
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active.data
    verts = mesh.vertices
    is_screen = any(k in obj.name for k in ("Display", "Screen"))
    is_card = "Tarot_Card" in obj.name
    if (is_screen or is_card) and len(verts) > 0:
        xs = [ (obj.matrix_world @ v.co).x for v in verts ]
        ys = [ (obj.matrix_world @ v.co).y for v in verts ]
        zs = [ (obj.matrix_world @ v.co).z for v in verts ]
        min_x, max_x = min(xs), max(xs)
        min_y, max_y = min(ys), max(ys)
        min_z, max_z = min(zs), max(zs)
        sx = max(1e-5, max_x - min_x)
        sy = max(1e-5, max_y - min_y)
        sz = max(1e-5, max_z - min_z)
        for poly in mesh.polygons:
            flip_u = poly.normal.y > 0.0
            for loop_idx in poly.loop_indices:
                vw = obj.matrix_world @ verts[mesh.loops[loop_idx].vertex_index].co
                raw_u = (vw.x - min_x) / sx
                u = (1.0 - raw_u) if (is_screen and flip_u) else raw_u
                w = (vw.z - min_z) / sz if is_screen else (vw.y - min_y) / sy
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


def clamp_to_target_bbox(obj, target_min, target_max):
    """Softly scales/shifts the joined mesh only if needed so its bounding box matches target_min..target_max to 0.1mm."""
    verts = [v.co for v in obj.data.vertices]
    cur_min = Vector((min(v.x for v in verts), min(v.y for v in verts), min(v.z for v in verts)))
    cur_max = Vector((max(v.x for v in verts), max(v.y for v in verts), max(v.z for v in verts)))
    t_min = Vector(target_min)
    t_max = Vector(target_max)
    for axis in range(3):
        span_cur = cur_max[axis] - cur_min[axis]
        span_tgt = t_max[axis] - t_min[axis]
        if span_cur > 1e-5 and abs(span_cur - span_tgt) > 0.002:
            for v in obj.data.vertices:
                norm_t = (v.co[axis] - cur_min[axis]) / span_cur
                v.co[axis] = t_min[axis] + norm_t * span_tgt
    obj.data.update()


def finalize_and_export(filename, target_min=None, target_max=None, uv_scale=1.0):
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
    obj.data.update()

    if target_min is not None and target_max is not None:
        clamp_to_target_bbox(obj, target_min, target_max)

    for poly in obj.data.polygons:
        poly.use_smooth = True

    out_path = OUT_MODELS / filename
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_materials="EXPORT",
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest = ROOT / sub
        if dest.exists():
            shutil.copy2(out_path, dest / filename)

    verts = [v.co for v in obj.data.vertices]
    mn = tuple(round(min(v[i] for v in verts), 4) for i in range(3))
    mx = tuple(round(max(v[i] for v in verts), 4) for i in range(3))
    tris = len(obj.data.polygons)
    print(f"[EXPORTED] {filename}: tris={tris}, min={mn}, max={mx}, size_bytes={out_path.stat().st_size}")


# ==============================================================================
# 1. PROP: 42U GLASS-DOOR ENTERPRISE SERVER RACK (prop_server_rack.glb)
# Target BBox (Blender coords): min=(-0.47, -0.4575, 0.0), max=(0.47, 0.4575, 2.3)
# ==============================================================================
def build_server_rack():
    clear_scene()
    img_matrix = tex_server_blade_matrix()
    img_brass = tex_brushed_brass("Tex_RackBrass", 128)

    m_frame = make_mat("RackFrame", (0.11, 0.13, 0.17, 1.0), metallic=0.72, roughness=0.32)
    m_blade = make_textured_mat("RackBlade", img_matrix, metallic=0.68, roughness=0.28)
    m_trim = make_textured_mat("RackTrimBrass", img_brass, metallic=0.85, roughness=0.24)
    m_glass = make_mat("RackSmokedGlass", (0.08, 0.16, 0.22, 1.0), metallic=0.25, roughness=0.12)
    m_cable = make_mat("RackBraidedCable", (0.06, 0.28, 0.46, 1.0), metallic=0.15, roughness=0.45)
    m_led_g = make_mat("LedGreen", (0.18, 0.98, 0.52, 1.0), roughness=0.10, emission_rgb=(0.18, 0.98, 0.52), emission_strength=2.2)
    m_led_c = make_mat("LedCyan", (0.14, 0.88, 1.00, 1.0), roughness=0.10, emission_rgb=(0.14, 0.88, 1.00), emission_strength=2.2)
    m_led_a = make_mat("LedAmber", (1.00, 0.68, 0.16, 1.0), roughness=0.10, emission_rgb=(1.00, 0.68, 0.16), emission_strength=2.2)

    # Outer structural plinth, roof manifold & 4 heavy beveled corner pillars
    add_box("BasePlinth", (0, 0.04, 0), (0.94, 0.08, 0.90), m_frame, bevel=0.015)
    add_box("TopManifold", (0, 2.25, 0), (0.94, 0.10, 0.90), m_frame, bevel=0.018)
    add_box("InnerCore", (0, 1.15, 0), (0.84, 2.14, 0.78), m_frame, bevel=0.012)

    # Top exhaust fan rings
    for fx in (-0.22, 0.22):
        add_vcyl(f"FanShroud_{fx}", (fx, 2.29, 0), 0.15, 0.02, m_trim, vertices=24, bevel=0.003)

    # 4 vertical corner posts & side acoustic panels with louver vents
    for sx in (-1, 1):
        for sz in (-1, 1):
            add_box(f"CornerPost_{sx}_{sz}", (sx * 0.435, 1.15, sz * 0.415), (0.07, 2.16, 0.07), m_frame, bevel=0.012)
        add_box(f"SidePanel_{sx}", (sx * 0.45, 1.15, 0), (0.03, 2.08, 0.74), m_frame, bevel=0.008)
        for lv in range(6):
            ly = 0.42 + lv * 0.28
            add_box(f"SideLouver_{sx}_{lv}", (sx * 0.466, ly, 0), (0.008, 0.028, 0.56), m_trim, bevel=0.002)

    # Front & rear rack bay stack: 9 2U blade server units + vertical cable raceways + glass door frame
    for face in (-1, 1):
        fz = face * 0.405
        # Vertical cable management raceways on left and right
        for side in (-1, 1):
            add_box(f"CableRaceway_{face}_{side}", (side * 0.385, 1.15, fz), (0.045, 2.04, 0.035), m_frame, bevel=0.005)

        for u in range(9):
            uy = 0.24 + u * 0.225
            # Main 2U server chassis faceplate with textured drive matrix
            add_box(f"Blade_{face}_{u}", (0, uy, fz), (0.72, 0.185, 0.036), m_blade, bevel=0.006)
            # Brushed metallic eject handles on both ends
            for side in (-1, 1):
                add_box(f"HandleBracket_{face}_{u}_{side}", (side * 0.34, uy, face * 0.430), (0.022, 0.135, 0.022), m_trim, bevel=0.004)
            # 4 hot-swap NVMe caddy bezels per blade
            for bay in range(4):
                bx = -0.21 + bay * 0.115
                add_box(f"CaddyBezel_{face}_{u}_{bay}", (bx, uy - 0.01, face * 0.426), (0.095, 0.11, 0.008), m_frame, bevel=0.002)
            # Glowing LED status telemetry bar + activity indicators
            led_mat = m_led_g if u % 3 == 0 else (m_led_c if u % 3 == 1 else m_led_a)
            add_box(f"LedStrip_{face}_{u}", (-0.18, uy + 0.064, face * 0.432), (0.28, 0.018, 0.010), led_mat, bevel=0.002)
            for dot in range(3):
                d_mat = m_led_c if (u + dot) % 2 == 0 else m_led_g
                add_box(f"LedDot_{face}_{u}_{dot}", (0.14 + dot * 0.045, uy + 0.064, face * 0.432), (0.022, 0.018, 0.010), d_mat, bevel=0.002)

            # Curved patch cable loop from blade to right cable raceway
            c_p0 = (0.26, uy - 0.02, face * 0.432)
            c_p1 = (0.37, uy - 0.05, face * 0.442)
            add_rod(f"PatchCable_{face}_{u}", c_p0, c_p1, 0.007, 0.007, m_cable if u % 2 == 0 else m_led_c, vertices=10)

        # Smoked glass door frame & recessed center panel
        for side in (-1, 1):
            add_box(f"DoorStile_{face}_{side}", (side * 0.415, 1.15, face * 0.446), (0.032, 2.08, 0.022), m_frame, bevel=0.005)
        add_box(f"DoorHandle_{face}", (0.395, 1.12, face * 0.453), (0.022, 0.24, 0.012), m_trim, bevel=0.003)

    finalize_and_export(
        "prop_server_rack.glb",
        target_min=(-0.47, -0.4575, 0.0),
        target_max=(0.47, 0.4575, 2.30),
        uv_scale=1.35,
    )


# ==============================================================================
# 2. PROP: ARCHITECTURAL MACBOOK TRAP PODIUM DESK (prop_macbook_trap_desk.glb)
# Target BBox (Blender coords): min=(-1.2592, -0.72, 0.0075), max=(1.2586, 0.72, 1.4202)
# CRITICAL: Tabletop upper surface is at Y_rl = 0.94m (Z_bl = 0.94m) for TrapTop collision!
# ==============================================================================
def build_macbook_trap_desk():
    clear_scene()
    img_marble = tex_obsidian_marble("Tex_TrapMarble", 512)
    img_brass = tex_brushed_brass("Tex_TrapBrass", 256)
    img_display = tex_macbook_display("Tex_TrapMacBookScreen", 512)

    marble = make_textured_mat("Smoked_Walnut", img_marble, metallic=0.18, roughness=0.22)
    brass = make_textured_mat("Walnut_Grain", img_brass, metallic=0.86, roughness=0.22)
    titanium = make_mat("Matte_Titanium", (0.20, 0.23, 0.27, 1.0), metallic=0.80, roughness=0.34)
    alu = make_mat("MacBook_Aluminum", (0.66, 0.71, 0.76, 1.0), metallic=0.86, roughness=0.22)
    black = make_mat("MacBook_Keyboard", (0.03, 0.04, 0.055, 1.0), metallic=0.25, roughness=0.42)
    display = make_textured_mat("MacBook_Display", img_display, metallic=0.05, roughness=0.16, emission_rgb=(0.25, 0.85, 0.95), emission_strength=0.85)
    glow_cyan = make_mat("Logo_Glow", (0.22, 0.94, 0.98, 1.0), roughness=0.15, emission_rgb=(0.22, 0.94, 0.98), emission_strength=1.8)

    # 1. Architectural Dark Nero Marquina Marble Tabletop (top surface strictly at Y_rl = 0.940m!)
    add_box("Marble_Table_Slab", (0, 0.88, 0), (2.48, 0.12, 1.44), marble, bevel=0.028, segments=3)
    # Brushed brass inlay reveal band wrapping the perimeter of the marble slab
    add_box("Brass_Perimeter_Belt", (0, 0.88, 0), (2.505, 0.026, 1.432), brass, bevel=0.006, segments=2)
    add_box("Titanium_Underframe", (0, 0.795, 0), (2.22, 0.05, 1.22), titanium, bevel=0.015, segments=2)

    # 2. Splayed Architectural Blade Legs with Brass Fluting & Plinth Pads
    for side in (-1, 1):
        add_box(f"Splayed_Titanium_Blade_{side}", (side * 0.87, 0.41, 0), (0.125, 0.76, 0.98), titanium, rot=(0, 0, side * -0.24), bevel=0.022, segments=3)
        add_box(f"Blade_Brass_Rib_{side}", (side * 0.935, 0.41, 0), (0.022, 0.72, 1.00), brass, rot=(0, 0, side * -0.24), bevel=0.005)
        add_box(f"Recessed_Foot_{side}", (side * 0.96, 0.038, 0), (0.35, 0.06, 1.06), black, bevel=0.018, segments=2)
        add_box(f"Foot_Brass_Trim_{side}", (side * 0.96, 0.068, 0), (0.33, 0.014, 1.04), brass, bevel=0.004)

    # Central horizontal brass stretcher bar between the splayed legs
    add_box("Central_Stretcher_Beam", (0, 0.34, 0), (1.76, 0.055, 0.14), titanium, bevel=0.012)
    add_box("Central_Stretcher_Brass", (0, 0.34, 0), (1.78, 0.020, 0.155), brass, bevel=0.004)

    # 3. Glowing Induction Bait Ring & Brass Halo Inlaid Flush on the Marble Tabletop (Y_rl = 0.941..0.948)
    add_vcyl("Induction_Outer_Brass_Ring", (0, 0.942, 0.02), 0.56, 0.006, brass, vertices=48, bevel=0.002)
    add_vcyl("Induction_Cyan_Glow_Ring", (0, 0.944, 0.02), 0.52, 0.007, glow_cyan, vertices=48, bevel=0.002)
    add_vcyl("Induction_Inner_Marble_Pad", (0, 0.946, 0.02), 0.47, 0.008, marble, vertices=48, bevel=0.002)
    add_vcyl("Induction_Core_Brass_Ring", (0, 0.948, 0.02), 0.44, 0.006, brass, vertices=48, bevel=0.002)

    # 4. High-Detail CNC Aluminum MacBook Pro on top of the Induction Pad
    add_box("MacBook_Unibody", (0, 0.968, 0.02), (0.68, 0.035, 0.46), alu, bevel=0.012, segments=3)
    add_box("Keyboard_Recess", (0, 0.988, -0.045), (0.57, 0.005, 0.205), black, bevel=0.006)
    for row in range(5):
        for col in range(12):
            add_box(f"Key_{row}_{col}", (-0.254 + col * 0.046, 0.994, -0.124 + row * 0.036), (0.037, 0.005, 0.027), titanium, bevel=0.002, segments=1)
    add_box("Spacebar", (0, 0.996, 0.046), (0.22, 0.005, 0.022), titanium, bevel=0.002, segments=1)
    add_box("Trackpad_Reveal", (0, 0.988, 0.155), (0.25, 0.003, 0.12), brass, bevel=0.006)
    add_box("Trackpad", (0, 0.990, 0.155), (0.238, 0.003, 0.108), alu, bevel=0.005)

    for side in (-1, 1):
        add_box(f"Speaker_Grille_{side}", (side * 0.312, 0.988, -0.045), (0.024, 0.003, 0.19), black, bevel=0.001)
        for i in range(2):
            add_box(f"USB_C_{side}_{i}", (side * 0.341, 0.969, -0.12 + i * 0.07), (0.004, 0.010, 0.026), black, bevel=0.002)

    # Tilted Retina Display Lid with Embedded Glowing IDE Texture & Rear Glowing Emblem
    angle = 0.15
    def lid_part(name, x, y, depth, w, h, d, mat, bevel=0.004):
        pos = (x, 0.995 + y * math.cos(angle) - depth * math.sin(angle), 0.245 + y * math.sin(angle) + depth * math.cos(angle))
        return add_box(name, pos, (w, h, d), mat, rot=(angle, 0, 0), bevel=bevel, segments=2)

    lid_part("Slim_Aluminum_Lid", 0, 0.215, 0, 0.68, 0.43, 0.016, alu, 0.007)
    lid_part("Black_Display_Bezel", 0, 0.215, -0.010, 0.652, 0.40, 0.003, black, 0.004)
    scr_obj = lid_part("Inset_Retina_Display", 0, 0.215, -0.013, 0.62, 0.366, 0.002, display, 0.002)
    lid_part("Camera_Notch", 0, 0.398, -0.015, 0.065, 0.019, 0.002, black, 0.001)
    lid_part("Rear_Glowing_Core_Badge", 0, 0.235, 0.010, 0.075, 0.075, 0.003, glow_cyan, 0.012)

    finalize_and_export(
        "prop_macbook_trap_desk.glb",
        target_min=(-1.2592, -0.72, 0.0075),
        target_max=(1.2586, 0.72, 1.4202),
        uv_scale=1.1,
    )


# ==============================================================================
# 3. PROP: WHITE SHARK HR TAROT & ORACLE RECEPTION DESK (prop_hr_reception_desk.glb)
# Target BBox (Blender coords): min=(-2.57, -0.30, 0.035), max=(2.57, 1.51, 1.4485)
# ==============================================================================
def build_hr_reception_desk():
    clear_scene()
    img_marble = tex_obsidian_marble("Tex_ReceptionMarble", 512)
    img_velvet = tex_quilted_velvet("Tex_ReceptionVelvet", 512)
    img_brass = tex_brushed_brass("Tex_ReceptionBrass", 256)
    img_tarot = tex_tarot_card_gold("Tex_ReceptionTarot", 256)
    img_screen = tex_zero_oracle_display("Tex_ZeroOracleScreen", 512)

    marble = make_textured_mat("Reception_ObsidianMarble", img_marble, metallic=0.18, roughness=0.20)
    plum = make_textured_mat("Reception_VelvetPlum", img_velvet, metallic=0.05, roughness=0.72)
    brass = make_textured_mat("Reception_BrushedBrass", img_brass, metallic=0.88, roughness=0.22)
    black = make_mat("Terminal_Graphite", (0.03, 0.038, 0.05, 1.0), metallic=0.62, roughness=0.28)
    screen = make_textured_mat("Terminal_Display", img_screen, metallic=0.08, roughness=0.18, emission_rgb=(0.20, 0.85, 0.88), emission_strength=0.9)
    tarot_mat = make_textured_mat("Tarot_Card_Gold", img_tarot, metallic=0.25, roughness=0.30, emission_rgb=(0.95, 0.76, 0.32), emission_strength=0.45)
    crystal_mat = make_mat("Arcane_Crystal_Glow", (0.98, 0.82, 0.48, 1.0), metallic=0.10, roughness=0.14, emission_rgb=(1.0, 0.74, 0.32), emission_strength=1.8)

    # Smooth 64-segment elliptical U-shell with beveled profile bands
    def arc(name, rx, rz, thickness, low, high, material):
        vertices = []
        faces = []
        n = 64
        for i in range(n + 1):
            t = math.pi * i / n
            for rxx, rzz, y in [
                (rx, rz, low),
                (rx, rz, high),
                (rx - thickness, rz - thickness, low),
                (rx - thickness, rz - thickness, high),
            ]:
                vertices.append(rl_pos((rxx * math.cos(t), y, 0.30 - rzz * math.sin(t))))
        for i in range(n):
            k = i * 4
            j = k + 4
            faces.extend([(k, j, j + 1, k + 1), (k + 2, k + 3, j + 3, j + 2), (k + 1, j + 1, j + 3, k + 3), (k, k + 2, j + 2, j)])
        faces.extend([(0, 1, 3, 2), (n * 4, n * 4 + 2, n * 4 + 3, n * 4 + 1)])
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(vertices, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.collection.objects.link(obj)
        mesh.materials.append(material)
        for f in mesh.polygons:
            f.use_smooth = True
        return obj

    arc("Continuous_Marble_U_Shell", 2.48, 1.72, 0.28, 0.12, 0.84, marble)
    arc("Continuous_Velvet_Inlay", 2.495, 1.735, 0.018, 0.23, 0.71, plum)
    arc("Floating_Marble_Counter", 2.57, 1.81, 0.55, 0.84, 0.925, marble)
    arc("Counter_Brass_Bullnose", 2.572, 1.812, 0.025, 0.87, 0.898, brass)
    for y in [0.16, 0.22, 0.715, 0.822]:
        arc(f"Continuous_Brass_Reveal_{y}", 2.505, 1.745, 0.022, y, y + 0.020, brass)
    arc("Recessed_Dark_Plinth", 2.36, 1.60, 0.24, 0.035, 0.13, black)

    # Radial sculpted brass pilaster ribs along the curved velvet front
    for i in range(1, 24):
        t = math.pi * i / 24
        px = 2.502 * math.cos(t)
        pz = 0.30 - 1.742 * math.sin(t)
        add_rod(f"Inlay_Brass_Seam_{i}", (px, 0.24, pz), (px, 0.70, pz), 0.010, 0.010, brass, vertices=12)

    # Center Tarot Spread Mat & 5 Fanned Illuminated Tarot Cards on the Front Counter (Z_rl ≈ -1.24)
    add_box("Tarot_Velvet_Runner", (0, 0.930, -1.22), (1.12, 0.012, 0.44), plum, bevel=0.004)
    add_box("Tarot_Brass_Border", (0, 0.928, -1.22), (1.15, 0.008, 0.47), brass, bevel=0.003)
    for c_idx in range(-2, 3):
        cx = c_idx * 0.17
        cz = -1.22 + abs(c_idx) * 0.025
        yaw = c_idx * 0.14
        add_box(f"Tarot_Card_{c_idx}", (cx, 0.939, cz), (0.13, 0.005, 0.22), tarot_mat, rot=(0, yaw, 0), bevel=0.002)

    # Warm Glowing Crystal Lamp / Arcane Orb on Sculpted Brass Pedestal & Twin Candlesticks
    orb_x, orb_z = -0.78, -1.16
    add_vcyl("Orb_Pedestal_Base", (orb_x, 0.95, orb_z), 0.11, 0.05, brass, vertices=24, bevel=0.004)
    add_vcyl("Orb_Pedestal_Stem", (orb_x, 1.04, orb_z), 0.042, 0.14, brass, vertices=20, bevel=0.003)
    add_vcyl("Orb_Cradle_Ring", (orb_x, 1.12, orb_z), 0.095, 0.025, brass, vertices=24, bevel=0.004)
    add_sphere("Crystal_Arcane_Orb", (orb_x, 1.24, orb_z), 0.135, crystal_mat, segments=28, ring_count=20)
    add_vcyl("Orb_Crown_Finial", (orb_x, 1.40, orb_z), 0.032, 0.095, brass, vertices=16, bevel=0.003)

    for c_side, cx in enumerate((0.72, 0.88)):
        cz = -1.15 + c_side * 0.08
        ch = 0.22 + c_side * 0.06
        add_vcyl(f"Candle_Holder_{c_side}", (cx, 0.945, cz), 0.048, 0.04, brass, vertices=18, bevel=0.003)
        add_vcyl(f"Candle_Wax_{c_side}", (cx, 0.96 + ch * 0.5, cz), 0.024, ch, crystal_mat, vertices=16, bevel=0.002)
        add_sphere(f"Candle_Flame_{c_side}", (cx, 0.98 + ch, cz), 0.024, crystal_mat, scale=(0.7, 1.6, 0.7), segments=14, ring_count=10)

    # Dual ZERO Prediction-API Terminals on Left & Right Counter Wings
    for side in (-1, 1):
        x = side * 2.12
        add_box(f"Terminal_Foot_{side}", (x, 0.94, -0.22), (0.34, 0.028, 0.24), black, bevel=0.010)
        add_rod(f"Inclined_Metal_Stand_{side}", (x, 0.95, -0.22), (x, 1.15, -0.30), 0.028, 0.022, brass, vertices=16)
        add_box(f"Thin_Terminal_{side}", (x, 1.29, -0.31), (0.54, 0.33, 0.030), black, rot=(-0.15, 0, 0), bevel=0.012)
        add_box(f"Terminal_Brass_Rim_{side}", (x, 1.29, -0.312), (0.555, 0.345, 0.018), brass, rot=(-0.15, 0, 0), bevel=0.005)
        add_box(f"Terminal_Screen_{side}", (x, 1.29, -0.328), (0.48, 0.28, 0.006), screen, rot=(-0.15, 0, 0), bevel=0.004)

    finalize_and_export(
        "prop_hr_reception_desk.glb",
        target_min=(-2.57, -0.30, 0.035),
        target_max=(2.57, 1.51, 1.4485),
        uv_scale=0.85,
    )


def render_landmark_proof_sheet():
    """Renders a 3-panel side-by-side studio proof image of all 3 upgraded landmark GLBs from their front faces."""
    clear_scene()
    placements = [
        ("prop_server_rack.glb", (-4.15, 0.0, 0.0), math.pi + 0.32),
        ("prop_macbook_trap_desk.glb", (-1.35, 0.0, 0.0), math.pi - 0.16),
        ("prop_hr_reception_desk.glb", (2.40, 0.30, 0.0), math.pi - 0.10),
    ]
    for fname, loc, rot_z in placements:
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / fname))
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        for o in imported:
            o.rotation_mode = "XYZ"
            o.rotation_euler = (0.0, 0.0, rot_z)
            o.location += Vector(loc)

    bpy.ops.mesh.primitive_plane_add(size=26.0, location=(0.0, 0.0, 0.0))
    floor = bpy.context.active_object
    m_floor = make_mat("ProofFloor", (0.08, 0.10, 0.13, 1.0), metallic=0.15, roughness=0.55)
    floor.data.materials.append(m_floor)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 800
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("ProofWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.065, 0.082, 0.110, 1.0)
        bg.inputs[1].default_value = 1.15

    cam_data = bpy.data.cameras.new("ProofCam")
    cam_data.lens = 27.5
    cam_obj = bpy.data.objects.new("ProofCam", cam_data)
    scene.collection.objects.link(cam_obj)
    cam_obj.location = (0.10, -8.35, 3.35)
    cam_obj.rotation_euler = (math.radians(70.0), 0, 0)
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("KeyLight", (4.0, -6.0, 6.0), 4.8, (1.0, 0.94, 0.84)),
        ("FillLight", (-5.0, -4.0, 4.0), 2.8, (0.72, 0.88, 1.0)),
        ("RimLight", (0.0, 5.0, 5.0), 3.4, (1.0, 0.82, 0.58)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        lobj.rotation_euler = (math.radians(48), 0, math.radians(25 if "Key" in lname else -35))

    proof_path = Path(r"C:\Users\se-20\.gemini\antigravity\brain\5a288ef4-482c-487e-b05f-e3d0e2267cf7\scratch\landmarks_overhaul_proof.png")
    proof_path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(proof_path)
    bpy.ops.render.render(write_still=True)
    print(f"[PROOF RENDERED] {proof_path}")


# ==============================================================================
# 4. PROP: BUG-TRACKER DEPOSIT TERMINAL «ПРИЁМНИК БАГОВ $1-$8» (prop_bug_terminal.glb)
# Origin at bottom base (Y_rl = 0 / Z_bl = 0), facing +Z in Raylib (Y_bl < 0),
# ~0.75m wide x 0.62m deep x 1.38m tall, ~3,000-6,000 tris, smooth bevels, 7 materials.
# ==============================================================================
_GLYPHS_5X7 = {
    "А": ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
    "Б": ["11111", "10000", "10000", "11110", "10001", "10001", "11110"],
    "В": ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
    "Г": ["11111", "10000", "10000", "10000", "10000", "10000", "10000"],
    "Д": ["00110", "01001", "01001", "01001", "01001", "11111", "10001"],
    "Е": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "Ё": ["01010", "11111", "10000", "11110", "10000", "10000", "11111"],
    "З": ["01110", "10001", "00001", "00110", "00001", "10001", "01110"],
    "И": ["10001", "10001", "10011", "10101", "11001", "10001", "10001"],
    "Й": ["01110", "10001", "10011", "10101", "11001", "10001", "10001"],
    "К": ["10001", "10010", "10100", "11000", "10100", "10010", "10001"],
    "Л": ["00111", "01001", "01001", "01001", "01001", "01001", "10001"],
    "М": ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
    "Н": ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
    "О": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
    "П": ["11111", "10001", "10001", "10001", "10001", "10001", "10001"],
    "Р": ["11110", "10001", "10001", "11110", "10000", "10000", "10000"],
    "С": ["01110", "10001", "10000", "10000", "10000", "10001", "01110"],
    "Т": ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
    "У": ["10001", "10001", "10001", "01111", "00001", "10001", "01110"],
    "Ц": ["10010", "10010", "10010", "10010", "10010", "11111", "00001"],
    "Ч": ["10001", "10001", "10001", "01111", "00001", "00001", "00001"],
    "Ы": ["10001", "10001", "10001", "11101", "10011", "10011", "11101"],
    "Ь": ["10000", "10000", "10000", "11110", "10001", "10001", "11110"],
    "Я": ["01111", "10001", "10001", "01111", "00101", "01001", "10001"],
    "E": ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
    "-": ["00000", "00000", "00000", "11111", "00000", "00000", "00000"],
    "/": ["00001", "00010", "00010", "00100", "01000", "01000", "10000"],
    ":": ["00000", "00100", "00100", "00000", "00100", "00100", "00000"],
    "$": ["00100", "01111", "10100", "01110", "00101", "11110", "00100"],
    "1": ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
    "2": ["01110", "10001", "00001", "00110", "01000", "10000", "11111"],
    "4": ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    "8": ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
    "0": ["01110", "10001", "10011", "10101", "11001", "10001", "01110"],
    "[": ["01110", "01000", "01000", "01000", "01000", "01000", "01110"],
    "]": ["01110", "00010", "00010", "00010", "00010", "00010", "01110"],
    ">": ["10000", "01000", "00100", "00010", "00100", "01000", "10000"],
    " ": ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
}


def _draw_text_5x7(img, text, x0, y_top, scale, rgb):
    """Draws crisp 5x7 Russian/ASCII bitmap text into a bottom-up Blender numpy texture (row 0 = bottom)."""
    h, w, _ = img.shape
    cx = x0
    for ch in text:
        glyph = _GLYPHS_5X7.get(ch, _GLYPHS_5X7[" "])
        for gy in range(7):
            row_str = glyph[gy]
            py_top = y_top - gy * scale
            py0 = max(0, py_top - scale)
            py1 = min(h, py_top)
            if py1 <= py0:
                continue
            for gx in range(5):
                if row_str[gx] == "1":
                    px0 = max(0, cx + gx * scale)
                    px1 = min(w, cx + (gx + 1) * scale)
                    if px1 > px0:
                        img[py0:py1, px0:px1, :3] = rgb
        cx += 6 * scale


def tex_bug_terminal_steel(name="Tex_BugTermSteel", size=512):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    rng = np.random.default_rng(1999)
    brush = rng.normal(0.0, 0.016, (1, size)).astype(np.float32)
    brush = np.repeat(brush, size, axis=0)
    panel = (np.abs((x * 4.0) % 1.0 - 0.5) > 0.47) | (np.abs((y * 4.0) % 1.0 - 0.5) > 0.47)
    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = 0.14 + brush
    img[..., 1] = 0.165 + brush
    img[..., 2] = 0.205 + brush
    img[panel, :3] = (0.08, 0.095, 0.12)
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_hopper_bioglass(name="Tex_HopperBioGlass", size=256):
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    img = np.zeros((size, size, 4), dtype=np.float32)
    # Illuminated cyan-emerald specimen jar glass with caustic gradient & graduation ticks
    caustic = 0.5 + 0.5 * np.sin(x * math.tau * 4.0) * np.cos(y * math.tau * 3.0)
    img[..., 0] = 0.06 + 0.08 * caustic
    img[..., 1] = 0.34 + 0.22 * caustic
    img[..., 2] = 0.44 + 0.26 * caustic
    # Horizontal measurement ticks & bio-scan lines
    scan = (np.abs((y * 12.0) % 1.0 - 0.5) < 0.05)
    img[scan, :3] = (0.24, 0.88, 0.96)
    img[..., 3] = 1.0
    return pack_image(name, np.clip(img, 0.0, 1.0))


def tex_bug_terminal_screen(name="Tex_BugTerminalScreen"):
    """Crisp 512x256 Russian Bug-Tracker Terminal Screen: БАГ-ТРЕКЕР // ПРИЁМ $1-$8 + Oscilloscope + Tariff Cards."""
    h, w = 256, 512
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    img = np.zeros((h, w, 4), dtype=np.float32)
    img[..., :3] = (0.032, 0.045, 0.068)
    img[..., 3] = 1.0

    # Outer CRT bezel frame & top title bar
    img[h - 42 :, :, :3] = (0.065, 0.095, 0.145)
    img[h - 44 : h - 41, :, :3] = (0.94, 0.74, 0.26)
    _draw_text_5x7(img, "БАГ-ТРЕКЕР // ПРИЁМ $1-$8", 22, h - 11, 3, (0.98, 0.84, 0.34))

    # Left panel (x: 16..244, y: 44..204): Bug Bio-Frequency Oscilloscope
    img[44:204, 16:244, :3] = (0.020, 0.032, 0.048)
    img[44:204, 16:18, :3] = (0.16, 0.58, 0.72)
    img[44:204, 242:244, :3] = (0.16, 0.58, 0.72)
    img[202:204, 16:244, :3] = (0.16, 0.58, 0.72)
    img[44:46, 16:244, :3] = (0.16, 0.58, 0.72)
    for gx in range(40, 240, 28):
        img[46:202, gx : gx + 1, :3] = (0.05, 0.11, 0.16)
    for gy in range(64, 200, 24):
        img[gy : gy + 1, 18:242, :3] = (0.05, 0.11, 0.16)

    _draw_text_5x7(img, "СПЕКТР БАГОВ", 32, 194, 2, (0.28, 0.88, 0.96))

    # Dual live bug waveform curves inside oscilloscope
    for px in range(22, 238):
        t = (px - 22) / 216.0
        wy1 = int(118 + 34 * math.sin(t * math.tau * 3.0) * math.cos(t * math.tau * 1.5) + 12 * math.sin(t * math.tau * 9.0))
        wy2 = int(112 + 22 * math.cos(t * math.tau * 2.0 + 0.8))
        img[max(48, wy2 - 1) : min(198, wy2 + 2), px, :3] = (0.94, 0.68, 0.22)
        img[max(48, wy1 - 2) : min(198, wy1 + 2), px, :3] = (0.18, 0.96, 0.72)

    # Right panel (x: 256..496, y: 44..204): 4 Color-Coded Frog-Bug Tariff Rows ($1, $2, $4, $8)
    tariffs = [
        (166, "$1 ЗЕЛЁНЫЙ", (0.22, 0.92, 0.46)),
        (128, "$2 ЦИАНОВЫЙ", (0.18, 0.86, 0.98)),
        (90, "$4 КРИМЗОН", (0.96, 0.28, 0.42)),
        (52, "$8 ЗОЛОТОЙ", (0.98, 0.78, 0.24)),
    ]
    for ry, label, col in tariffs:
        img[ry : ry + 30, 256:496, :3] = (0.045, 0.065, 0.095)
        img[ry : ry + 30, 256:266, :3] = col
        img[ry + 28 : ry + 30, 256:496, :3] = (0.12, 0.18, 0.26)
        _draw_text_5x7(img, label, 278, ry + 22, 2, col)

    # Bottom status & interaction prompt bar
    img[6:36, 16:496, :3] = (0.045, 0.18, 0.16)
    img[34:36, 16:496, :3] = (0.22, 0.94, 0.72)
    _draw_text_5x7(img, "[E] СДАТЬ БАГ > ВЫДАЧА $$$", 48, 28, 2, (0.36, 0.98, 0.78))

    # Flip vertically because pack_image() applies np.flipud() expecting top-down image arrays
    return pack_image(name, np.clip(np.flipud(img), 0.0, 1.0))


def build_bug_terminal():
    clear_scene()
    img_steel = tex_bug_terminal_steel("Tex_BugTermSteel", 512)
    img_brass = tex_brushed_brass("Tex_BugTermBrass", 256)
    img_obsidian = tex_obsidian_marble("Tex_BugTermObsidian", 512)
    img_screen = tex_bug_terminal_screen("Tex_BugTerminalScreen")
    img_glass = tex_hopper_bioglass("Tex_HopperBioGlass", 256)

    m_steel = make_textured_mat("BugTerm_CharcoalSteel", img_steel, metallic=0.72, roughness=0.30)
    m_brass = make_textured_mat("BugTerm_BrushedBrass", img_brass, metallic=0.88, roughness=0.22)
    m_obsidian = make_textured_mat("BugTerm_ObsidianTrim", img_obsidian, metallic=0.28, roughness=0.24)
    m_screen = make_textured_mat(
        "BugTerm_DisplayScreen", img_screen, metallic=0.06, roughness=0.16, emission_rgb=(0.24, 0.88, 0.92), emission_strength=1.15
    )
    m_glass = make_textured_mat(
        "BugTerm_HopperGlass", img_glass, metallic=0.15, roughness=0.12, emission_rgb=(0.12, 0.68, 0.82), emission_strength=0.70
    )
    m_cyan = make_mat(
        "BugTerm_CyanGlow", (0.18, 0.94, 1.0, 1.0), metallic=0.05, roughness=0.10, emission_rgb=(0.14, 0.92, 1.0), emission_strength=2.2
    )
    m_amber = make_mat(
        "BugTerm_AmberGlow", (1.0, 0.76, 0.22, 1.0), metallic=0.15, roughness=0.14, emission_rgb=(1.0, 0.72, 0.18), emission_strength=2.0
    )

    # 1. Heavy Armored Base Plinth (Y_rl = 0.00..0.11m, width=0.75m, depth=0.62m)
    add_box("Base_Plinth_Core", (0, 0.045, 0), (0.75, 0.09, 0.62), m_obsidian, bevel=0.015, segments=1)
    add_box("Base_Brass_Step", (0, 0.098, 0), (0.72, 0.022, 0.59), m_brass, bevel=0.006, segments=1)
    for sx in (-1, 1):
        for sz in (-1, 1):
            add_box(f"Base_Corner_Bumper_{sx}_{sz}", (sx * 0.355, 0.048, sz * 0.29), (0.045, 0.088, 0.045), m_brass, bevel=0.006, segments=1)

    # 2. Lower Brushed Charcoal-Steel Cabinet & Brass Corner Pilasters (Y_rl = 0.11..0.76m)
    add_box("Lower_Cabinet_Body", (0, 0.435, -0.01), (0.68, 0.65, 0.52), m_steel, bevel=0.018, segments=2)
    for sx in (-1, 1):
        add_box(f"Cabinet_Brass_Pilaster_{sx}", (sx * 0.342, 0.44, 0.235), (0.036, 0.66, 0.055), m_brass, bevel=0.008, segments=1)
        add_box(f"Cabinet_Rear_Pilaster_{sx}", (sx * 0.342, 0.44, -0.255), (0.032, 0.66, 0.045), m_brass, bevel=0.006, segments=1)
        # Side ventilation louver grilles
        for lv in range(5):
            ly = 0.26 + lv * 0.085
            add_box(f"Side_Vent_Louver_{sx}_{lv}", (sx * 0.344, ly, -0.02), (0.014, 0.028, 0.28), m_obsidian, bevel=0.003, segments=1)

    # Horizontal brass belt molding
    add_box("Mid_Brass_Belt", (0, 0.745, 0.01), (0.73, 0.028, 0.58), m_brass, bevel=0.007, segments=1)

    # 3. Front Specimen Intake Chute (Left, +Z_rl) & Cash/Coin Payout Bay (Right, +Z_rl)
    # Left: Specimen Vacuum Hopper Door («ПРИЁМ БАГОВ»)
    add_box("Intake_Chute_Frame", (-0.155, 0.50, 0.255), (0.25, 0.27, 0.035), m_brass, bevel=0.008, segments=1)
    add_box("Intake_Chute_Door", (-0.155, 0.50, 0.268), (0.21, 0.23, 0.022), m_obsidian, bevel=0.006, segments=1)
    add_rod("Intake_Brass_Handle", (-0.22, 0.55, 0.292), (-0.09, 0.55, 0.292), 0.011, 0.011, m_brass, vertices=12)
    add_box("Intake_Laser_Strip", (-0.155, 0.605, 0.278), (0.18, 0.016, 0.012), m_cyan, bevel=0.002, segments=1)

    # Right: Cash & Coin Payout Dispenser («ВЫДАЧА $$$»)
    add_box("Payout_Housing_Frame", (0.165, 0.50, 0.255), (0.24, 0.27, 0.035), m_obsidian, bevel=0.008, segments=1)
    add_box("Payout_Bill_Bezel", (0.165, 0.56, 0.272), (0.18, 0.065, 0.022), m_brass, bevel=0.005, segments=1)
    add_box("Payout_Bill_Slot", (0.165, 0.56, 0.282), (0.14, 0.018, 0.010), m_obsidian, bevel=0.002, segments=1)
    add_box("Payout_Amber_LED", (0.165, 0.602, 0.278), (0.15, 0.014, 0.010), m_amber, bevel=0.002, segments=1)
    add_box("Payout_Coin_Tray", (0.165, 0.42, 0.276), (0.18, 0.075, 0.048), m_brass, bevel=0.006, segments=1)
    add_box("Payout_Coin_Well", (0.165, 0.435, 0.278), (0.14, 0.045, 0.036), m_obsidian, bevel=0.004, segments=1)

    # Lower Front Exhaust Grille & Power Badge
    add_box("Lower_Front_Grille_Frame", (0, 0.22, 0.252), (0.56, 0.14, 0.024), m_obsidian, bevel=0.005, segments=1)
    for gl in range(4):
        gy = 0.175 + gl * 0.030
        add_box(f"Front_Grille_Slat_{gl}", (0, gy, 0.262), (0.50, 0.012, 0.012), m_steel, bevel=0.002, segments=1)

    # 4. Ergonomic Control Console Deck (Y_rl = 0.76..0.82m, projecting forward to Z_rl = +0.31m)
    add_box("Control_Deck_Slab", (0, 0.78, 0.05), (0.71, 0.055, 0.52), m_obsidian, bevel=0.012, segments=1)
    # Left: Glowing Cyan Frog-Scanner Pad on Control Deck
    add_vcyl("Deck_Scanner_Rim", (-0.16, 0.812, 0.19), 0.105, 0.016, m_brass, vertices=20, bevel=0.003)
    add_vcyl("Deck_Scanner_Pad", (-0.16, 0.818, 0.19), 0.088, 0.012, m_cyan, vertices=20, bevel=0.002)
    # Right: Mechanical Keypad & Oversized Amber/Brass Deposit Button
    for kr in range(2):
        for kc in range(3):
            kx = 0.05 + kc * 0.048
            kz = 0.16 + kr * 0.052
            add_box(f"Deck_Key_{kr}_{kc}", (kx, 0.814, kz), (0.034, 0.016, 0.034), m_steel, bevel=0.002, segments=1)
    add_vcyl("Deck_Confirm_Btn_Rim", (0.245, 0.814, 0.185), 0.044, 0.018, m_brass, vertices=16, bevel=0.002)
    add_vcyl("Deck_Confirm_Btn", (0.245, 0.822, 0.185), 0.034, 0.018, m_amber, vertices=16, bevel=0.002)

    # 5. Inclined Upper CRT/LCD Terminal Housing & Crisp 512x256 Russian Display (Y_rl = 0.80..1.08m, facing +Z_rl)
    add_box("Upper_Terminal_Cabinet", (0, 0.93, -0.03), (0.66, 0.28, 0.40), m_steel, bevel=0.016, segments=2)
    for sx in (-1, 1):
        add_box(f"Upper_Side_Brass_Fin_{sx}", (sx * 0.335, 0.93, -0.02), (0.024, 0.27, 0.36), m_brass, bevel=0.006, segments=1)

    # Slanted front display bezel & screen tilted -15 deg back so it faces player eye level (+Z_rl, +Y_rl)
    scr_tilt = -0.24
    add_box("Terminal_Display_OuterBezel", (0, 0.935, 0.172), (0.58, 0.29, 0.042), m_brass, rot=(scr_tilt, 0, 0), bevel=0.008, segments=1)
    add_box("Terminal_Display_InnerHood", (0, 0.935, 0.182), (0.53, 0.255, 0.028), m_obsidian, rot=(scr_tilt, 0, 0), bevel=0.005, segments=1)
    add_box("BugTerm_Display_Screen", (0, 0.936, 0.194), (0.495, 0.228, 0.008), m_screen, rot=(scr_tilt, 0, 0), bevel=0.002, segments=1)

    # 6. Top Illuminated Glass Specimen Hopper / Jar with Glowing Cyan & Amber Rings (Y_rl = 1.05..1.38m)
    hop_z = -0.03
    add_box("Crown_Platform_Slab", (0, 1.072, hop_z), (0.62, 0.032, 0.38), m_obsidian, bevel=0.008, segments=1)
    add_vcyl("Hopper_Base_Collar", (0, 1.098, hop_z), 0.215, 0.036, m_brass, vertices=22, bevel=0.004)
    # Glowing Cyan Lower Induction Ring
    add_vcyl("Hopper_Lower_Cyan_Ring", (0, 1.122, hop_z), 0.222, 0.018, m_cyan, vertices=22, bevel=0.003)

    # Illuminated Glass Specimen Hopper Cylinder
    add_vcyl("Hopper_Glass_Cylinder", (0, 1.215, hop_z), 0.192, 0.175, m_glass, vertices=24, bevel=0.003)

    # Equatorial Bio-Laser Scan Ring around the Hopper Glass
    add_vcyl("Hopper_Equatorial_Laser_Ring", (0, 1.215, hop_z), 0.196, 0.010, m_cyan, vertices=24, bevel=0.002)

    # 4 External Brushed-Brass Containment Tie-Rods around the Glass Hopper
    for ang_deg in (45, 135, 225, 315):
        rad = math.radians(ang_deg)
        rx = 0.208 * math.cos(rad)
        rz = hop_z + 0.208 * math.sin(rad)
        add_rod(f"Hopper_Brass_TieRod_{ang_deg}", (rx, 1.11, rz), (rx, 1.315, rz), 0.011, 0.011, m_brass, vertices=10)

    # Glowing Amber Upper Containment Ring & Domed Brass Top Cap + Beacon Finial (Y_rl up to 1.38m)
    add_vcyl("Hopper_Upper_Amber_Ring", (0, 1.308, hop_z), 0.218, 0.018, m_amber, vertices=22, bevel=0.003)
    add_vcyl("Hopper_Top_Brass_Cap", (0, 1.332, hop_z), 0.205, 0.032, m_brass, vertices=22, bevel=0.005)
    add_vcyl("Hopper_Beacon_Pedestal", (0, 1.355, hop_z), 0.075, 0.022, m_obsidian, vertices=16, bevel=0.003)
    add_sphere("Hopper_Top_Beacon_Dome", (0, 1.368, hop_z), 0.042, m_cyan, scale=(1.0, 0.55, 1.0), segments=16, ring_count=10)

    finalize_and_export(
        "prop_bug_terminal.glb",
        target_min=(-0.375, -0.31, 0.0),
        target_max=(0.375, 0.31, 1.38),
        uv_scale=1.25,
    )


def render_bug_terminal_proof():
    """Renders a 3-angle studio proof sheet of prop_bug_terminal.glb to screenshots/bug_terminal_proof.png."""
    clear_scene()
    # Note: prop_bug_terminal faces +Z in Raylib -> -Y in Blender!
    # So rotation_euler=(0, 0, 0) faces -Y (directly toward camera at Y = -3.45)!
    views = [
        ((-1.15, 0.0, 0.0), math.radians(-28.0)),  # 3/4 Front-Left view
        ((0.0, -0.15, 0.0), 0.0),                  # Direct Front close-up on screen & hopper
        ((1.15, 0.0, 0.0), math.radians(32.0)),    # 3/4 Front-Right view
    ]
    for loc, rot_z in views:
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_bug_terminal.glb"))
        imported = [o for o in bpy.context.selected_objects if o.type == "MESH"]
        for o in imported:
            o.rotation_mode = "XYZ"
            o.rotation_euler = (0.0, 0.0, rot_z)
            o.location += Vector(loc)

    bpy.ops.mesh.primitive_plane_add(size=16.0, location=(0.0, 0.0, 0.0))
    floor = bpy.context.active_object
    m_floor = make_mat("BugProofFloor", (0.07, 0.085, 0.11, 1.0), metallic=0.18, roughness=0.50)
    floor.data.materials.append(m_floor)

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 960
    scene.render.film_transparent = False
    world = bpy.data.worlds.new("BugProofWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.055, 0.070, 0.095, 1.0)
        bg.inputs[1].default_value = 1.15

    cam_data = bpy.data.cameras.new("BugProofCam")
    cam_data.lens = 35.0
    cam_obj = bpy.data.objects.new("BugProofCam", cam_data)
    scene.collection.objects.link(cam_obj)
    cam_obj.location = (0.0, -3.55, 1.16)
    cam_obj.rotation_euler = (math.radians(81.0), 0.0, 0.0)
    scene.camera = cam_obj

    for lname, lpos, energy, col in [
        ("KeyLight", (2.8, -3.5, 4.2), 4.6, (1.0, 0.95, 0.86)),
        ("FillLight", (-3.2, -2.8, 3.0), 2.8, (0.74, 0.90, 1.0)),
        ("RimLight", (0.0, 3.5, 3.8), 3.4, (1.0, 0.82, 0.56)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        lobj.rotation_euler = (math.radians(48), 0, math.radians(25 if "Key" in lname else -35))

    out_screenshots = ROOT / "screenshots" / "bug_terminal_proof.png"
    out_screenshots.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(out_screenshots)
    bpy.ops.render.render(write_still=True)
    scratch_copy = Path(r"C:\Users\se-20\.gemini\antigravity\brain\5a288ef4-482c-487e-b05f-e3d0e2267cf7\scratch\bug_terminal_proof.png")
    shutil.copy2(out_screenshots, scratch_copy)
    print(f"[BUG TERMINAL PROOF RENDERED] {out_screenshots}")


if __name__ == "__main__":
    import sys
    if "--bug-terminal-only" in sys.argv:
        build_bug_terminal()
        render_bug_terminal_proof()
    else:
        build_server_rack()
        build_macbook_trap_desk()
        build_hr_reception_desk()
        build_bug_terminal()
        render_landmark_proof_sheet()
        render_bug_terminal_proof()
