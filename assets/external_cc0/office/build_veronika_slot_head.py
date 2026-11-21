"""
HR Veronika 1990s Retro-Luxury Casino Slot-Machine Head & Bandit Lever Builder (v2 Polished)
Exports:
  1. `assets/models/char_hr_veronika_slot_head.glb`
     - Origin `[0, 0, 0]` at exact `HEAD_PIVOT_LOCAL = (0.0f, 1.415f, 0.045f)` facing `+Z`,
       dropping onto `char_hr_veronika_body.glb`'s neck with 0.0 mm gap.
  2. `assets/models/char_hr_veronika_slot_lever.glb`
     - Origin `[0, 0, 0]` at exact `LEVER_PIVOT_OFFSET = (-0.118f, 0.128f, 0.000f)` relative to `HEAD_PIVOT_LOCAL`
       (i.e. `(-0.118f, 1.543f, 0.045f)` in body-local space), ready for C# pitch rotation `(0° -> +75° -> 0°)`.
  3. 2-panel proof `veronika_slot_head_proof.png` with `use_backface_culling = True` (`GL_CULL_FACE` verified).
"""

import math
import os
from pathlib import Path
import bmesh
import bpy
import numpy as np
from mathutils import Euler, Quaternion, Vector as V
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets" / "models"
ARTIFACT_PROOF_1 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\veronika_slot_head_proof.png")
ARTIFACT_PROOF_2 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\34a5dbd6-7dba-4b8d-8112-53de935b5112\scratch\veronika_slot_head_proof.png")
SCREENSHOT_PROOF = ROOT / "screenshots" / "veronika_slot_head_proof.png"

HEAD_PIVOT_LOCAL = V((0.0, 1.415, 0.045))
ARM_L_PIVOT_LOCAL = V((0.138, 1.298, 0.032))
ARM_R_PIVOT_LOCAL = V((-0.138, 1.298, 0.032))
LEVER_PIVOT_OFFSET = V((-0.118, 0.128, 0.000))  # Relative to HEAD_PIVOT_LOCAL

MATS = {}


def xyz(p):
    """Convert Game/Raylib local (x, y_up, z_forward) -> Blender (x, -z_forward, y_up)."""
    return (p[0], -p[2], p[1])


def srgb_to_linear(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def draw_filled_circle(img, cx, cy, r, col_rgb, feather=1.2):
    H, W, _ = img.shape
    y0, y1 = max(0, int(cy - r - 2)), min(H, int(cy + r + 3))
    x0, x1 = max(0, int(cx - r - 2)), min(W, int(cx + r + 3))
    if y0 >= y1 or x0 >= x1:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1].astype(np.float32)
    dist = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
    alpha = np.clip((r - dist) / max(0.5, feather) + 0.5, 0.0, 1.0)[..., None]
    img[y0:y1, x0:x1, :3] = img[y0:y1, x0:x1, :3] * (1.0 - alpha) + np.array(col_rgb, dtype=np.float32) * alpha


def draw_thick_segment(img, x0, y0, x1, y1, r, col_rgb, feather=1.1):
    H, W, _ = img.shape
    xmin = max(0, int(min(x0, x1) - r - 2))
    xmax = min(W, int(max(x0, x1) + r + 3))
    ymin = max(0, int(min(y0, y1) - r - 2))
    ymax = min(H, int(max(y0, y1) + r + 3))
    if xmin >= xmax or ymin >= ymax:
        return
    yy, xx = np.mgrid[ymin:ymax, xmin:xmax].astype(np.float32)
    dx, dy = x1 - x0, y1 - y0
    l2 = max(1e-6, dx * dx + dy * dy)
    t = np.clip(((xx - x0) * dx + (yy - y0) * dy) / l2, 0.0, 1.0)
    px = x0 + t * dx
    py = y0 + t * dy
    dist = np.sqrt((xx - px) ** 2 + (yy - py) ** 2)
    alpha = np.clip((r - dist) / max(0.5, feather) + 0.5, 0.0, 1.0)[..., None]
    img[ymin:ymax, xmin:xmax, :3] = img[ymin:ymax, xmin:xmax, :3] * (1.0 - alpha) + np.array(col_rgb, dtype=np.float32) * alpha


def draw_polygon_mask(img, pts, col_rgb):
    H, W, _ = img.shape
    xs = [p[0] for p in pts]
    ys = [p[1] for p in pts]
    xmin, xmax = max(0, int(min(xs) - 1)), min(W, int(max(xs) + 2))
    ymin, ymax = max(0, int(min(ys) - 1)), min(H, int(max(ys) + 2))
    if xmin >= xmax or ymin >= ymax:
        return
    yy, xx = np.mgrid[ymin:ymax, xmin:xmax].astype(np.float32)
    inside = np.zeros_like(xx, dtype=bool)
    n = len(pts)
    for i in range(n):
        x1, y1 = pts[i]
        x2, y2 = pts[(i + 1) % n]
        cond = ((y1 > yy) != (y2 > yy)) & (xx < (x2 - x1) * (yy - y1) / (y2 - y1 + 1e-7) + x1)
        inside ^= cond
    mask = inside[..., None].astype(np.float32)
    img[ymin:ymax, xmin:xmax, :3] = img[ymin:ymax, xmin:xmax, :3] * (1.0 - mask) + np.array(col_rgb, dtype=np.float32) * mask


def paint_symbol_seven(img, cx, cy, scale=1.0):
    """Paint a high-contrast 1990s Vegas Jackpot '7' with deep black-burgundy outline, crimson body & gold core."""
    s = scale
    # Deep dark drop-shadow + black-burgundy outline
    for off_x, off_y, r_mult, col in [
        (3.5 * s, -3.5 * s, 1.05, (0.28, 0.02, 0.08)),
        (0.0, 0.0, 1.12, (0.10, 0.02, 0.05)),
        (0.0, 0.0, 0.82, (0.86, 0.08, 0.20)),
        (0.0, 0.0, 0.50, (0.99, 0.82, 0.18)),
    ]:
        ro = 11.2 * s * r_mult
        draw_thick_segment(img, cx - 26 * s + off_x, cy + 25 * s + off_y, cx + 26 * s + off_x, cy + 25 * s + off_y, ro, col)
        draw_thick_segment(img, cx - 26 * s + off_x, cy + 25 * s + off_y, cx - 26 * s + off_x, cy + 12 * s + off_y, ro * 0.85, col)
        draw_thick_segment(img, cx + 24 * s + off_x, cy + 24 * s + off_y, cx - 8 * s + off_x, cy - 28 * s + off_y, ro, col)


def paint_symbol_diamond(img, cx, cy, scale=1.0):
    """Paint a high-contrast Electric-Violet faceted casino diamond."""
    s = scale
    rim = [(cx, cy + 33 * s), (cx + 31 * s, cy + 6 * s), (cx, cy - 33 * s), (cx - 31 * s, cy + 6 * s)]
    outer = [(cx, cy + 29 * s), (cx + 27 * s, cy + 6 * s), (cx, cy - 29 * s), (cx - 27 * s, cy + 6 * s)]
    inner = [(cx, cy + 24 * s), (cx + 22 * s, cy + 6 * s), (cx, cy - 24 * s), (cx - 22 * s, cy + 6 * s)]
    top_facet = [(cx, cy + 24 * s), (cx + 14 * s, cy + 9 * s), (cx - 14 * s, cy + 9 * s)]
    left_facet = [(cx - 22 * s, cy + 6 * s), (cx - 7 * s, cy + 6 * s), (cx, cy - 24 * s)]
    right_facet = [(cx + 22 * s, cy + 6 * s), (cx + 7 * s, cy + 6 * s), (cx, cy - 24 * s)]
    draw_polygon_mask(img, rim, (0.08, 0.02, 0.16))
    draw_polygon_mask(img, outer, (0.28, 0.04, 0.52))
    draw_polygon_mask(img, inner, (0.62, 0.16, 0.96))
    draw_polygon_mask(img, left_facet, (0.42, 0.08, 0.78))
    draw_polygon_mask(img, right_facet, (0.80, 0.36, 1.00))
    draw_polygon_mask(img, top_facet, (0.95, 0.78, 1.00))
    draw_filled_circle(img, cx - 5 * s, cy + 13 * s, 3.8 * s, (1.0, 0.98, 1.0))


def paint_symbol_uzi(img, cx, cy, scale=1.0):
    """Paint a bold high-contrast Micro-Uzi Jackpot Medallion."""
    s = scale
    draw_filled_circle(img, cx, cy, 33 * s, (0.12, 0.03, 0.06))
    draw_filled_circle(img, cx, cy, 29 * s, (0.96, 0.74, 0.14))
    draw_filled_circle(img, cx, cy, 24 * s, (0.62, 0.06, 0.18))
    dark = (0.06, 0.07, 0.10)
    steel = (0.22, 0.26, 0.32)
    gold = (0.99, 0.84, 0.28)
    # Chunky Micro-Uzi silhouette
    draw_thick_segment(img, cx - 16 * s, cy + 8 * s, cx + 17 * s, cy + 8 * s, 8.2 * s, dark)
    draw_thick_segment(img, cx - 14 * s, cy + 9 * s, cx + 15 * s, cy + 9 * s, 4.8 * s, steel)
    draw_thick_segment(img, cx - 26 * s, cy + 8 * s, cx - 15 * s, cy + 8 * s, 3.6 * s, dark)
    draw_filled_circle(img, cx - 12 * s, cy + 16 * s, 4.6 * s, dark)
    draw_filled_circle(img, cx + 13 * s, cy + 16 * s, 4.6 * s, dark)
    draw_thick_segment(img, cx + 2 * s, cy + 4 * s, cx + 4 * s, cy - 12 * s, 6.0 * s, dark)
    draw_thick_segment(img, cx + 3 * s, cy - 10 * s, cx + 4 * s, cy - 23 * s, 4.0 * s, steel)
    draw_thick_segment(img, cx - 11 * s, cy + 8 * s, cx + 12 * s, cy + 8 * s, 1.8 * s, gold)


def paint_symbol_crown(img, cx, cy, scale=1.0):
    s = scale
    pts_out = [
        (cx - 25 * s, cy - 17 * s),
        (cx - 29 * s, cy + 15 * s),
        (cx - 12 * s, cy + 0 * s),
        (cx, cy + 21 * s),
        (cx + 12 * s, cy + 0 * s),
        (cx + 29 * s, cy + 15 * s),
        (cx + 25 * s, cy - 17 * s),
    ]
    draw_polygon_mask(img, pts_out, (0.14, 0.05, 0.02))
    pts_in = [
        (cx - 21 * s, cy - 13 * s),
        (cx - 24 * s, cy + 10 * s),
        (cx - 11 * s, cy - 3 * s),
        (cx, cy + 15 * s),
        (cx + 11 * s, cy - 3 * s),
        (cx + 24 * s, cy + 10 * s),
        (cx + 21 * s, cy - 13 * s),
    ]
    draw_polygon_mask(img, pts_in, (0.98, 0.76, 0.14))
    draw_thick_segment(img, cx - 21 * s, cy - 15 * s, cx + 21 * s, cy - 15 * s, 4.5 * s, (0.78, 0.48, 0.06))
    draw_filled_circle(img, cx, cy - 5 * s, 5.0 * s, (0.88, 0.08, 0.22))
    draw_filled_circle(img, cx - 13 * s, cy - 6 * s, 3.8 * s, (0.06, 0.74, 0.50))
    draw_filled_circle(img, cx + 13 * s, cy - 6 * s, 3.8 * s, (0.06, 0.74, 0.50))


def paint_symbol_emerald_gem(img, cx, cy, scale=1.0):
    s = scale
    draw_thick_segment(img, cx - 23 * s, cy, cx + 23 * s, cy, 16 * s, (0.03, 0.18, 0.12))
    draw_thick_segment(img, cx - 20 * s, cy, cx + 20 * s, cy, 12.5 * s, (0.08, 0.76, 0.52))
    draw_thick_segment(img, cx - 16 * s, cy + 3 * s, cx + 16 * s, cy + 3 * s, 4.5 * s, (0.55, 0.98, 0.80))


def build_slot_reels_texture():
    img_name = "VeronikaSlotReelsTex"
    existing = bpy.data.images.get(img_name)
    if existing is not None:
        return existing

    W, H = 1024, 512
    srgb = np.ones((H, W, 3), dtype=np.float32)
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    v = y / float(H - 1)

    # 1. Warm ivory-cream drum surface with stronger top/bottom cylindrical shadow for 3D drum roundness
    cyl_shade = 0.66 + 0.34 * np.sin(np.clip((v - 0.04) / 0.92, 0.0, 1.0) * math.pi)
    srgb[:, :768, 0] = 0.96 * cyl_shade[:, :768]
    srgb[:, :768, 1] = 0.91 * cyl_shade[:, :768]
    srgb[:, :768, 2] = 0.81 * cyl_shade[:, :768]

    for r_idx in range(3):
        xL = r_idx * 256
        xR = xL + 256
        srgb[:, xL : xL + 12, :] = np.array([0.28, 0.04, 0.12])
        srgb[:, xR - 12 : xR, :] = np.array([0.28, 0.04, 0.12])
        srgb[:, xL + 12 : xL + 19, :] = np.array([0.82, 0.58, 0.12])
        srgb[:, xR - 19 : xR - 12, :] = np.array([0.82, 0.58, 0.12])
        for hy in (64, 176, 336, 448):
            srgb[hy - 3 : hy + 3, xL + 18 : xR - 18, :] = np.array([0.68, 0.50, 0.28])
        for ty in range(24, H - 24, 28):
            srgb[ty - 2 : ty + 2, xL + 20 : xL + 34, :] = np.array([0.48, 0.08, 0.18])
            srgb[ty - 2 : ty + 2, xR - 34 : xR - 20, :] = np.array([0.48, 0.08, 0.18])

    # Golden payline band at y = 256
    pay_glow = np.exp(-((y[:, :768] - 256.0) / 34.0) ** 2)[..., None]
    srgb[:, :768, :] = srgb[:, :768, :] * (1.0 - 0.22 * pay_glow) + np.array([1.0, 0.85, 0.32]) * (0.22 * pay_glow)

    # Center Payline (`cy = 256`): 7 - UZI - DIAMOND
    paint_symbol_seven(srgb, 128, 256, scale=1.60)
    paint_symbol_uzi(srgb, 384, 256, scale=1.60)
    paint_symbol_diamond(srgb, 640, 256, scale=1.55)

    # Upper row (`cy = 392`)
    paint_symbol_crown(srgb, 128, 392, scale=1.25)
    paint_symbol_seven(srgb, 384, 392, scale=1.25)
    paint_symbol_emerald_gem(srgb, 640, 392, scale=1.25)

    # Lower row (`cy = 120`)
    paint_symbol_diamond(srgb, 128, 120, scale=1.22)
    paint_symbol_crown(srgb, 384, 120, scale=1.25)
    paint_symbol_seven(srgb, 640, 120, scale=1.25)

    # 2. Top Jackpot Marquee Panel (`x in [768, 1024]`)
    mx = (x[:, 768:] - 896.0) / 128.0
    my = (y[:, 768:] - 256.0) / 256.0
    rad = np.sqrt(mx * mx + my * my)
    ang = np.arctan2(my, mx)
    rays = 0.5 + 0.5 * np.cos(ang * 18.0)
    m_bg = np.zeros((H, 256, 3), dtype=np.float32)
    m_bg[:, :, 0] = np.clip(0.54 + 0.34 * rays - 0.22 * rad, 0.18, 0.95)
    m_bg[:, :, 1] = np.clip(0.08 + 0.36 * rays - 0.12 * rad, 0.03, 0.62)
    m_bg[:, :, 2] = np.clip(0.14 + 0.08 * rays, 0.04, 0.28)
    srgb[:, 768:, :] = m_bg
    for sx in (834, 896, 958):
        paint_symbol_seven(srgb, sx, 256, scale=1.02)

    lin = np.ones((H, W, 4), dtype=np.float32)
    lin[:, :, :3] = srgb_to_linear(srgb)
    img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(lin.ravel())
    img.pack()
    return img


def build_solid_swatch_texture(name, hex_col):
    existing = bpy.data.images.get(name)
    if existing is not None:
        return existing
    W, H = 64, 64
    rgb = [int(hex_col[i : i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin_rgb = srgb_to_linear(np.array(rgb, dtype=np.float32))
    arr = np.ones((H, W, 4), dtype=np.float32)
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u = x / float(W - 1)
    shade = (0.38 + 0.77 * (u ** 1.18))[..., None]
    arr[:, :, :3] = np.clip(lin_rgb[None, None, :] * shade, 0.001, 0.99)
    img = bpy.data.images.new(name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


MAT_SPECS = {
    # name: (hex_color, metallic, roughness, emissiveFactor)
    "BurgundyEnamel": ("521024", 0.16, 0.24, None),
    "AntiqueBrass":   ("DBA83C", 0.88, 0.22, (0.05, 0.03, 0.01)),
    "ChromeTrim":     ("DCE2EA", 0.92, 0.16, (0.04, 0.05, 0.06)),
    "DarkRecess":     ("12060C", 0.08, 0.70, None),
    "SlotReels":      ("FFFFFF", 0.02, 0.20, (0.20, 0.18, 0.16)),
    "MarqueeBulb":    ("FFF2B2", 0.00, 0.08, (0.96, 0.76, 0.28)),
    "EmeraldGem":     ("14B888", 0.15, 0.12, (0.06, 0.34, 0.24)),
    "CherryKnob":     ("C8142E", 0.12, 0.10, (0.18, 0.02, 0.04)),
}


def mat(key):
    if key in MATS:
        return MATS[key]
    hex_col, metallic, roughness, emissive = MAT_SPECS[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    m.use_backface_culling = True
    tree = m.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness

    tex = tree.nodes.new("ShaderNodeTexImage")
    if key == "SlotReels":
        tex.image = build_slot_reels_texture()
    else:
        tex.image = build_solid_swatch_texture(f"SlotSwatch_{key}", hex_col)
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    if key == "SlotReels":
        # Connect texture color to Emission Color so Blender Eevee preserves full symbol contrast!
        if "Emission Color" in bsdf.inputs:
            tree.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 0.24
    elif emissive is not None:
        if "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emissive, 1.0)
        if "Emission Strength" in bsdf.inputs:
            bsdf.inputs["Emission Strength"].default_value = 1.0

    MATS[key] = m
    return m


def finish(obj, name, key):
    obj.name = name
    obj.data.materials.clear()
    obj.data.materials.append(mat(key))
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def apply_weighted_smooth(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.mode = 'FACE_AREA'
    wn.weight = 50
    wn.keep_sharp = False
    bpy.ops.object.modifier_apply(modifier=wn.name)
    obj.select_set(False)
    return obj


def add_box(name, p, size, key, bevel=0.004, segments=3, rot=(0.0, 0.0, 0.0)):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if any(abs(a) > 1e-6 for a in rot):
        obj.rotation_euler = Euler((rot[0], -rot[2], rot[1]), "XYZ")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    finish(obj, name, key)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.profile = 0.55
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def add_cyl_y(name, p, radius, height, key, bevel=0.002, vertices=36, cone_r2=None, rot_x=0.0):
    if cone_r2 is not None:
        bpy.ops.mesh.primitive_cone_add(
            vertices=vertices,
            radius1=radius,
            radius2=cone_r2,
            depth=height,
            location=xyz(p),
        )
    else:
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=vertices,
            radius=radius,
            depth=height,
            location=xyz(p),
        )
    obj = finish(bpy.context.object, name, key)
    if abs(rot_x) > 1e-5:
        obj.rotation_euler = Euler((rot_x, 0.0, 0.0), "XYZ")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def add_cyl_x(name, p, radius, width_x, key, bevel=0.0015, vertices=40):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=width_x,
        location=xyz(p),
        rotation=(0.0, math.pi / 2.0, 0.0),
    )
    obj = finish(bpy.context.object, name, key)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 3
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def add_cyl_z(name, p, radius, depth_z, key, bevel=0.0015, vertices=36):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth_z,
        location=xyz(p),
        rotation=(math.pi / 2.0, 0.0, 0.0),
    )
    obj = finish(bpy.context.object, name, key)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def add_sphere(name, p, radii, key, segs=32, rings=20):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, location=xyz(p))
    obj = bpy.context.object
    obj.scale = (radii[0], radii[2], radii[1])
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return apply_weighted_smooth(finish(obj, name, key))


def add_smooth_curve_tube(name, points, radius, key, res=8):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = res
    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for v, p in zip(spline.points, points):
        v.co = (*xyz(p), 1.0)
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mat(key))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o.select_set(False)
    return apply_weighted_smooth(o)


def shape_retro_cabinet_contour(obj, y_center=0.120):
    """Apply uniform retro-Vegas hourglass/arch taper to cabinet body and its brass bands so zero shelf corners overhang."""
    for v in obj.data.vertices:
        # World Z in Blender is game Y
        world_y = obj.location.z + v.co.z
        ht = (world_y - y_center) / 0.10
        v.co.x *= (1.0 + 0.032 * ht)
        # Taper rear (-Z in game = +Y in Blender) gently
        world_bl_y = obj.location.y + v.co.y
        if world_bl_y > 0.015:
            v.co.x *= 0.93
    obj.data.update()
    return apply_weighted_smooth(obj)


def build_veronika_slot_head_objects():
    """
    Build all meshes of `char_hr_veronika_slot_head.glb` in HEAD_PIVOT_LOCAL-relative
    coordinates (`[0, 0, 0]` = `HEAD_PIVOT_LOCAL`, facing `+Z`, `+Y` up).
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    # ==========================================================================
    # 1) STEPPED BRUSHED-BRASS MECHANICAL NECK SOCKET COLLAR (0.0mm fit at [0,0,0]!)
    # ==========================================================================
    add_cyl_y("NeckGasketBase",   (0.0, -0.008, -0.004), 0.044, 0.016, "DarkRecess",   bevel=0.0020, vertices=36, cone_r2=0.047)
    add_cyl_y("NeckCollarLower",  (0.0,  0.002, -0.004), 0.048, 0.012, "AntiqueBrass", bevel=0.0025, vertices=36, cone_r2=0.054)
    add_cyl_y("NeckCollarChrome", (0.0,  0.011, -0.004), 0.053, 0.008, "ChromeTrim",   bevel=0.0020, vertices=36)
    add_cyl_y("NeckCollarUpper",  (0.0,  0.020, -0.003), 0.056, 0.012, "AntiqueBrass", bevel=0.0025, vertices=36, cone_r2=0.068)

    # ==========================================================================
    # 2) RETRO-LUXURY 1990s VEGAS BURGUNDY-ENAMEL CABINET HEAD & FITTED BRASS BANDS
    # ==========================================================================
    cabinet = add_box("BurgundyCabinetBody", (0.0, 0.120, -0.002), (0.216, 0.190, 0.168), "BurgundyEnamel", bevel=0.022, segments=5)
    shape_retro_cabinet_contour(cabinet)

    # Fitted Antique-Gold Brass Lower Plinth & Upper Cornice Bands (matching cabinet bevel & taper so zero overhanging shelves!)
    plinth = add_box("BrassBasePlinth", (0.0, 0.028, -0.001), (0.219, 0.015, 0.172), "AntiqueBrass", bevel=0.020, segments=5)
    shape_retro_cabinet_contour(plinth)

    cornice = add_box("BrassUpperCornice", (0.0, 0.207, 0.000), (0.221, 0.015, 0.174), "AntiqueBrass", bevel=0.020, segments=5)
    shape_retro_cabinet_contour(cornice)

    for side in (-1, 1):
        sx = side * 0.104
        # Vertical Antique-Gold Brass Front Corner Columns
        add_box(f"BrassFrontPillar_{side}", (sx, 0.118, 0.064), (0.018, 0.176, 0.040), "AntiqueBrass", bevel=0.005, segments=3)
        # 3 Horizontal Brushed-Chrome Art-Deco Speed Ribs embedded snugly into the burgundy side walls
        for idx, ry in enumerate((0.074, 0.098, 0.166)):
            add_box(f"ChromeSideRib_{side}_{idx}", (side * 0.107, ry, -0.006), (0.010, 0.0075, 0.116), "ChromeTrim", bevel=0.0025, segments=2)

    # Right-Temple Lever Pivot Boss (`x = -0.106..-0.118, y = 0.128, z = 0.000`) matching `LEVER_PIVOT_OFFSET = (-0.118, 0.128, 0.000)`!
    add_cyl_x("TempleLeverMountFlange", (-0.106, 0.128, 0.000), 0.025, 0.016, "AntiqueBrass", bevel=0.0025, vertices=32)
    add_cyl_x("TempleLeverBearingRing", (-0.114, 0.128, 0.000), 0.018, 0.012, "ChromeTrim",   bevel=0.0018, vertices=32)

    # Left-Temple Symmetric Art-Deco Medallion Boss (`x = +0.108, y = 0.128, z = 0.000`)
    add_cyl_x("LeftTempleMedallion", (0.106, 0.128, 0.000), 0.023, 0.014, "AntiqueBrass", bevel=0.0025, vertices=32)
    add_sphere("LeftTempleEmeraldStud", (0.113, 0.128, 0.000), (0.005, 0.011, 0.011), "EmeraldGem", segs=24, rings=14)

    # ==========================================================================
    # 3) FRONT RECESSED DISPLAY WINDOW (`+Z` FACE) & 3 MECHANICAL REEL DRUMS
    # ==========================================================================
    add_box("ReelWindowTopBar",    (0.0, 0.184, 0.083), (0.198, 0.013, 0.022), "AntiqueBrass", bevel=0.0035, segments=3)
    add_box("ReelWindowBottomBar", (0.0, 0.072, 0.083), (0.198, 0.013, 0.022), "AntiqueBrass", bevel=0.0035, segments=3)
    for side in (-1, 1):
        add_box(f"ReelWindowSideBar_{side}", (side * 0.092, 0.128, 0.083), (0.015, 0.112, 0.022), "AntiqueBrass", bevel=0.0035, segments=3)
        add_box(f"ReelWindowChromeInner_{side}", (side * 0.084, 0.128, 0.087), (0.006, 0.100, 0.016), "ChromeTrim", bevel=0.0018, segments=2)

    add_box("ReelBayDarkRecess", (0.0, 0.128, 0.068), (0.168, 0.098, 0.014), "DarkRecess", bevel=0.002, segments=2)

    # 3 High-Poly Cylindrical Mechanical Reel Drums (`x = -0.054, 0.000, +0.054`, `y = 0.128`, `z = 0.055`, `r = 0.0445`)
    for r_idx, rx in enumerate((-0.054, 0.000, 0.054)):
        drum = add_cyl_x(f"SlotReelDrum_{r_idx}", (rx, 0.128, 0.055), 0.0445, 0.048, "SlotReels", bevel=0.0016, vertices=48)
        drum["reel_index"] = r_idx

    # Thin gleaming golden divider rings between the 3 reel drums
    for dx in (-0.027, 0.027):
        add_cyl_x(f"ReelDividerRing_{dx}", (dx, 0.128, 0.055), 0.0454, 0.0030, "AntiqueBrass", bevel=0.0007, vertices=40)

    # Center Golden Payline Wire & Side Ruby Payline Pointers across `y = 0.128, z = 0.1008`
    add_box("PaylineGoldenBar", (0.0, 0.128, 0.1008), (0.170, 0.0022, 0.0022), "MarqueeBulb", bevel=0.0005, segments=2)
    for side in (-1, 1):
        add_sphere(f"PaylineRubyPointer_{side}", (side * 0.086, 0.128, 0.100), (0.0065, 0.0055, 0.0045), "CherryKnob", segs=18, rings=12)

    # ==========================================================================
    # 4) ARCHED TOP JACKPOT MARQUEE CROWN WITH 7 GLOWING BULBS & EMERALD TIARA GEM
    # ==========================================================================
    crown_body = add_box("MarqueeCrownBody", (0.0, 0.233, 0.014), (0.204, 0.052, 0.128), "AntiqueBrass", bevel=0.014, segments=4)
    for v in crown_body.data.vertices:
        nx = min(1.0, abs(v.co.x) / 0.102)
        arch_drop = 0.024 * (nx ** 1.65)
        if v.co.z > 0.0:
            v.co.z += 0.014 * (1.0 - nx * nx) - arch_drop * 0.6
        else:
            v.co.z -= arch_drop * 0.25
    crown_body.data.update()
    apply_weighted_smooth(crown_body)

    # Illuminated Front Marquee Glass Panel (`SlotReels` mapped to `u in [0.76, 0.99]`)
    marquee_glass = add_box("MarqueeIlluminatedPanel", (0.0, 0.231, 0.076), (0.162, 0.034, 0.012), "SlotReels", bevel=0.004, segments=3)
    for v in marquee_glass.data.vertices:
        nx = min(1.0, abs(v.co.x) / 0.081)
        if v.co.z > 0.0:
            v.co.z += 0.009 * (1.0 - nx * nx)
    marquee_glass.data.update()
    apply_weighted_smooth(marquee_glass)
    marquee_glass["is_marquee"] = True

    # 7 Glowing Warm-Gold Carnival Marquee Bulbs along the Crown Arch
    for b_idx in range(7):
        bt = (b_idx / 6.0) * 2.0 - 1.0
        bx = bt * 0.088
        by = 0.252 + 0.020 * math.cos(bt * math.pi * 0.50)
        bz = 0.070
        add_cyl_z(f"MarqueeBulbSocket_{b_idx}", (bx, by, bz - 0.004), 0.0080, 0.010, "AntiqueBrass", bevel=0.0014, vertices=20)
        add_sphere(f"MarqueeGlowingBulb_{b_idx}", (bx, by, bz + 0.004), (0.0068, 0.0068, 0.0078), "MarqueeBulb", segs=20, rings=14)

    # Center-Top Emerald-Teal Drop Jewel in Golden Tiara Setting
    add_sphere("TiaraGoldSetting", (0.0, 0.238, 0.084), (0.013, 0.017, 0.007), "AntiqueBrass", segs=24, rings=16)
    add_sphere("TiaraEmeraldJewel", (0.0, 0.238, 0.088), (0.0095, 0.013, 0.007), "EmeraldGem", segs=24, rings=16)

    # ==========================================================================
    # 5) BOTTOM CHROME COIN-CHUTE "GRIN" WITH OPEN DARK MOUTH & STACKED GOLD COINS
    # ==========================================================================
    # Lower protruding Chrome Tray Shelf (`y = 0.028..0.042, z = 0.078..0.110`)
    tray_lip = add_box("ChromeCoinTrayShelf", (0.0, 0.036, 0.092), (0.156, 0.014, 0.034), "ChromeTrim", bevel=0.0045, segments=3)
    for v in tray_lip.data.vertices:
        nx = min(1.0, abs(v.co.x) / 0.078)
        v.co.z += 0.0055 * (nx ** 1.6)
    tray_lip.data.update()
    apply_weighted_smooth(tray_lip)

    # Side & upper chrome hood around the coin chute mouth
    for side in (-1, 1):
        add_box(f"ChromeCoinTraySideCheek_{side}", (side * 0.073, 0.048, 0.090), (0.012, 0.018, 0.026), "ChromeTrim", bevel=0.003, segments=3)

    # Open dark recessed payout chute mouth (`y = 0.042..0.058, z = 0.082..0.095`)
    chute_mouth = add_box("CoinChuteOpenMouth", (0.0, 0.049, 0.086), (0.136, 0.016, 0.016), "DarkRecess", bevel=0.0025, segments=2)
    for v in chute_mouth.data.vertices:
        nx = min(1.0, abs(v.co.x) / 0.068)
        v.co.z += 0.0045 * (nx ** 1.6)
    chute_mouth.data.update()
    apply_weighted_smooth(chute_mouth)

    # Gleaming stacked & forward-tilted Gold Casino Coins sitting VISIBLY on top of the lower chrome tray lip!
    coin_layouts = [
        (-0.048, 0.046, 0.098, 0.32),
        (-0.032, 0.045, 0.100, 0.35),
        (-0.016, 0.044, 0.101, 0.36),
        ( 0.000, 0.044, 0.102, 0.38),
        ( 0.016, 0.044, 0.101, 0.36),
        ( 0.032, 0.045, 0.100, 0.35),
        ( 0.048, 0.046, 0.098, 0.32),
        (-0.024, 0.049, 0.097, 0.25),
        (-0.007, 0.048, 0.099, 0.28),
        ( 0.009, 0.048, 0.099, 0.28),
        ( 0.025, 0.049, 0.097, 0.25),
    ]
    for c_i, (cx, cy, cz, tilt_x) in enumerate(coin_layouts):
        add_cyl_y(f"GoldJackpotCoin_{c_i}", (cx, cy, cz), 0.0092, 0.0038, "AntiqueBrass", bevel=0.0008, vertices=24, rot_x=tilt_x)


def build_veronika_slot_lever_objects():
    """
    Build all meshes of `char_hr_veronika_slot_lever.glb` with Origin `[0, 0, 0]`
    at `LEVER_PIVOT_OFFSET = (-0.118f, 0.128f, 0.000f)` relative to `HEAD_PIVOT_LOCAL`.
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    add_cyl_x("LeverClutchHub", (-0.004, 0.000, 0.000), 0.0175, 0.016, "AntiqueBrass", bevel=0.0025, vertices=32)
    add_cyl_x("LeverClutchCap", (-0.014, 0.000, 0.000), 0.0128, 0.008, "ChromeTrim",   bevel=0.0018, vertices=28)

    arm_pts = [
        (-0.010, 0.000, 0.000),
        (-0.024, 0.004, 0.006),
        (-0.033, 0.022, 0.013),
        (-0.037, 0.054, 0.020),
        (-0.039, 0.092, 0.025),
        (-0.040, 0.130, 0.028),
    ]
    add_smooth_curve_tube("ChromeBanditArm", arm_pts, 0.0078, "ChromeTrim", res=10)
    add_cyl_y("LeverKnobBrassCollar", (-0.040, 0.124, 0.028), 0.0118, 0.013, "AntiqueBrass", bevel=0.0020, vertices=28, cone_r2=0.0148)
    add_sphere("CherryRedBallKnob", (-0.040, 0.147, 0.028), (0.025, 0.025, 0.025), "CherryKnob", segs=36, rings=24)
    add_cyl_y("KnobTopBrassCap", (-0.040, 0.170, 0.028), 0.0080, 0.004, "AntiqueBrass", bevel=0.0010, vertices=24)


def finalize_and_export_slot_glb(export_name):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    poly_tags = []
    for o in mesh_objs:
        if "reel_index" in o:
            tag = int(o["reel_index"])
        elif o.get("is_marquee"):
            tag = 10
        else:
            tag = -1
        poly_tags.extend([tag] * len(o.data.polygons))

    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    obj.name = obj.data.name = export_name

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()

    mesh = obj.data
    mesh.calc_loop_triangles()

    used = sorted({p.material_index for p in mesh.polygons})
    old_mats = [mesh.materials[i] for i in used]
    remap = {old_i: new_i for new_i, old_i in enumerate(used)}
    remapped = [remap[p.material_index] for p in mesh.polygons]
    mesh.materials.clear()
    for m in old_mats:
        mesh.materials.append(m)
    for p, n_mi in zip(mesh.polygons, remapped):
        p.material_index = n_mi

    verts = mesh.vertices
    v_coords = [v.co.copy() for v in verts]
    bvh = BVHTree.FromPolygons(v_coords, [tuple(p.vertices) for p in mesh.polygons], all_triangles=False)

    n_rays = 24
    golden_angle = math.pi * (3.0 - math.sqrt(5.0))
    local_dirs = []
    for i in range(n_rays):
        z_h = (i + 0.5) / n_rays
        r_h = math.sqrt(max(0.0, 1.0 - z_h * z_h))
        phi = i * golden_angle
        local_dirs.append(V((r_h * math.cos(phi), r_h * math.sin(phi), z_h)))

    neighbors = [[] for _ in range(len(verts))]
    for e in mesh.edges:
        v0, v1 = e.vertices[0], e.vertices[1]
        neighbors[v0].append(v1)
        neighbors[v1].append(v0)

    max_dist = 0.045
    vert_u = [0.68] * len(verts)
    vert_col = [1.0] * len(verts)

    for idx, v in enumerate(verts):
        p = v.co
        n = v.normal.normalized() if v.normal.length > 1e-6 else V((0.0, 0.0, 1.0))
        up = V((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else V((1.0, 0.0, 0.0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()
        origin = p + n * 0.0006
        occ = 0.0
        for ld in local_dirs:
            d = (t1 * ld.x + t2 * ld.y + n * ld.z).normalized()
            hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, max_dist)
            if hit_loc is not None and hit_dist is not None:
                occ += (1.0 - min(1.0, hit_dist / max_dist)) ** 1.3
        ao = max(0.0, 1.0 - (occ / n_rays) * 1.05)

        curv = 0.0
        if neighbors[idx]:
            acc = 0.0
            for nb in neighbors[idx]:
                dv = v_coords[nb] - p
                if dv.length > 1e-6:
                    acc += dv.normalized().dot(n)
            curv = max(-0.18, min(0.22, -(acc / len(neighbors[idx])) * 0.55))

        vert_u[idx] = min(0.96, max(0.04, 0.14 + 0.66 * (ao ** 1.2) + curv))
        vert_col[idx] = min(1.0, max(0.22, 0.22 + 0.78 * (ao ** 1.2)))

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    col_attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")

    has_tag_match = len(poly_tags) == len(mesh.polygons)
    mat_names = [m.name for m in mesh.materials]

    for p_i, poly in enumerate(mesh.polygons):
        poly.use_smooth = True
        m_name = mat_names[poly.material_index]
        tag = poly_tags[p_i] if has_tag_match else -1

        if m_name == "SlotReels":
            cx_bl = poly.center.x
            cz_bl = poly.center.z
            if tag == 10 or cz_bl > 0.195:
                for li in poly.loop_indices:
                    v_co = verts[mesh.loops[li].vertex_index].co
                    tu = 0.76 + min(1.0, max(0.0, (v_co.x + 0.081) / 0.162)) * 0.23
                    tv = 0.18 + min(1.0, max(0.0, (v_co.z - 0.214) / 0.036)) * 0.64
                    uv_layer.data[li].uv = (tu, tv)
            else:
                if tag in (0, 1, 2):
                    r_idx = tag
                else:
                    r_idx = 0 if cx_bl < -0.027 else (2 if cx_bl > 0.027 else 1)
                rx_center = (-0.054, 0.000, 0.054)[r_idx]
                u0 = r_idx * 0.25 + 0.008
                u_span = 0.234
                for li in poly.loop_indices:
                    v_co = verts[mesh.loops[li].vertex_index].co
                    tu = u0 + min(1.0, max(0.0, (v_co.x - (rx_center - 0.024)) / 0.048)) * u_span
                    tv = 0.06 + min(1.0, max(0.0, (v_co.z - 0.0835) / 0.0890)) * 0.88
                    uv_layer.data[li].uv = (tu, tv)
        else:
            for li in poly.loop_indices:
                vi = mesh.loops[li].vertex_index
                uv_layer.data[li].uv = (vert_u[vi], 0.5)

        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            c = vert_col[vi]
            col_attr.data[li].color = (c, c, c, 1.0)

    mesh.update()

    OUT_MODELS.mkdir(parents=True, exist_ok=True)
    out_path = OUT_MODELS / f"{export_name}.glb"
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_attributes=True,
        export_active_vertex_color_when_no_material=True,
        export_vertex_color="ACTIVE",
        export_materials="EXPORT",
    )
    print(f"EXPORTED {export_name}.glb ({os.path.getsize(out_path)} bytes): {len(mesh.vertices)} verts, {len(mesh.polygons)} polys, {len(mesh.materials)} materials")
    return out_path


def render_veronika_slot_head_proof():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("SlotHeadProofWorld")
    sc.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.12, 0.07, 0.14, 1.0)
        bg.inputs[1].default_value = 1.0

    # --- Panel 1 (Left, x = -0.68): Close-up 3/4 of Slot Head + Chrome Bandit Lever ---
    p1_root = V((-0.68, -0.35, 0.58))
    p1_scale = 3.25
    p1_rot = Euler((math.radians(6.0), 0.0, math.radians(16.0)), "XYZ")

    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "char_hr_veronika_slot_head.glb"))
    head_p1 = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in head_p1:
        o.scale = (p1_scale, p1_scale, p1_scale)
        o.location = p1_root
        o.rotation_mode = "XYZ"
        o.rotation_euler = p1_rot

    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "char_hr_veronika_slot_lever.glb"))
    lever_p1 = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    bl_lever_off = V((LEVER_PIVOT_OFFSET.x, -LEVER_PIVOT_OFFSET.z, LEVER_PIVOT_OFFSET.y)) * p1_scale
    bl_lever_off.rotate(p1_rot)
    for o in lever_p1:
        o.scale = (p1_scale, p1_scale, p1_scale)
        o.location = p1_root + bl_lever_off
        o.rotation_mode = "XYZ"
        o.rotation_euler = p1_rot

    # --- Panel 2 (Right, x = +0.58): Assembled onto HR Veronika's Body at HEAD_PIVOT_LOCAL ---
    p2_base = V((0.58, 0.0, -0.02))
    lever_world_piv = HEAD_PIVOT_LOCAL + LEVER_PIVOT_OFFSET
    for glb_fn, local_piv in [
        ("char_hr_veronika_body.glb", V((0.0, 0.0, 0.0))),
        ("char_hr_veronika_arm_l.glb", ARM_L_PIVOT_LOCAL),
        ("char_hr_veronika_arm_r.glb", ARM_R_PIVOT_LOCAL),
        ("char_hr_veronika_slot_head.glb", HEAD_PIVOT_LOCAL),
        ("char_hr_veronika_slot_lever.glb", lever_world_piv),
        ("prop_veronika_tarot_table.glb", V((0.0, 0.0, 0.0))),
    ]:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / glb_fn))
        objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
        bl_piv = V((local_piv.x, -local_piv.z, local_piv.y))
        for o in objs:
            o.location = p2_base + bl_piv
            o.rotation_mode = "XYZ"

    # Re-link SlotReels texture to Emission Color on imported glTF materials so Eevee shows crisp symbols
    for m in bpy.data.materials:
        m.use_backface_culling = True
        if m.name.startswith("SlotReels") and m.use_nodes:
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            if bsdf and "Emission Color" in bsdf.inputs:
                for link in list(m.node_tree.links):
                    if link.to_node == bsdf and link.to_socket.name == "Base Color":
                        m.node_tree.links.new(link.from_socket, bsdf.inputs["Emission Color"])
                        if "Emission Strength" in bsdf.inputs:
                            bsdf.inputs["Emission Strength"].default_value = 0.25
                        break

    for name, loc, energy, col in [
        ("Key",  (2.4, -3.4, 2.4), 3.6, (1.0, 0.94, 0.86)),
        ("Fill", (-2.6, -2.8, 1.6), 2.2, (0.86, 0.88, 1.0)),
        ("Rim",  (1.8, 2.8, 2.2),   2.4, (1.0, 0.78, 0.58)),
    ]:
        ld = bpy.data.lights.new(name, type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        d = V((0.0, 0.0, 1.15)) - V(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("ProofCam")
    cd.type = "ORTHO"
    cd.ortho_scale = 2.68
    co = bpy.data.objects.new("ProofCam", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -3.1, 1.62)
    d = V((0.0, -0.10, 1.00)) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    for target_path in (ARTIFACT_PROOF_1, ARTIFACT_PROOF_2, SCREENSHOT_PROOF):
        try:
            target_path.parent.mkdir(parents=True, exist_ok=True)
            sc.render.filepath = str(target_path)
            bpy.ops.render.render(write_still=True)
            print("Saved Veronika Slot Head proof:", target_path)
        except Exception as exc:
            print("Warning saving proof to", target_path, ":", exc)


if __name__ == "__main__":
    build_veronika_slot_head_objects()
    finalize_and_export_slot_glb("char_hr_veronika_slot_head")
    build_veronika_slot_lever_objects()
    finalize_and_export_slot_glb("char_hr_veronika_slot_lever")
    render_veronika_slot_head_proof()
