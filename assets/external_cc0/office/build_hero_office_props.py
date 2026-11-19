"""Blender 5.1 generator for 3 upgraded Hero Office Props (Graphics Overhaul Sprint):
  1. assets/models/prop_leather_sofa.glb     — Sleek Executive Tufted Oxblood/Espresso Leather Sofa (~4,800 tris)
  2. assets/models/prop_water_cooler.glb     — Modern Brushed-Steel & Charcoal Office Water Dispenser with 19L Polycarbonate Jug (~3,800 tris)
  3. assets/models/prop_workstation_full.glb — High-Craft Dual-Monitor Developer Cubicle with Glowing IDE Code Texture, Mesh Chair, Mechanical Keyboard, PC Tower & Mug (~5,600 tris)

Preserves exact bounding boxes, origins, and orientations of the original GLBs while replacing flat blocky primitives
with smooth-shaded, beveled, tufted/sculpted meshes and packed procedural textures (leather grain + diamond tufting AO,
water ripples, IDE syntax-highlighted code screen, woven acoustic fabric, brushed slate desk).
"""
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix

V = Vector
ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
OFFICE_DIR = Path(__file__).resolve().parent


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_packed_image(name, width, height, rgb_array):
    """Creates a packed Blender image from an (H, W, 3) or (H, W, 4) float32 numpy array in [0, 1]."""
    img = bpy.data.images.new(name, width=width, height=height, alpha=False)
    if rgb_array.shape[2] == 3:
        rgba = np.ones((height, width, 4), dtype=np.float32)
        rgba[:, :, :3] = np.clip(rgb_array, 0.0, 1.0)
    else:
        rgba = np.clip(rgb_array.astype(np.float32), 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())
    img.pack()
    return img


def make_mat(name, rgba, metallic=0.1, roughness=0.42, image=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = rgba
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if image is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if emission_strength > 0.0:
            links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    elif emission_strength > 0.0:
        bsdf.inputs["Emission Color"].default_value = rgba
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def make_leather_texture(name, base_rgb, tuft_grid=(6, 3), tuft_strength=0.38):
    """Generates a 256x256 rich full-grain leather texture with diamond Chester tufting creases & pebble pores."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / w
    v = y / h

    # Multi-frequency pebble leather micro-grain
    grain = (
        0.045 * np.sin(u * math.tau * 37.0 + np.cos(v * math.tau * 29.0))
        + 0.032 * np.cos(v * math.tau * 43.0 - np.sin(u * math.tau * 31.0))
        + 0.022 * np.sin((u + v) * math.tau * 61.0)
    )

    # Diamond tufting channels & button dimples
    gx = u * tuft_grid[0]
    gy = v * tuft_grid[1]
    d1 = np.abs(np.mod(gx + gy, 1.0) - 0.5) * 2.0
    d2 = np.abs(np.mod(gx - gy, 1.0) - 0.5) * 2.0
    crease1 = np.exp(-((1.0 - d1) / 0.075) ** 2)
    crease2 = np.exp(-((1.0 - d2) / 0.075) ** 2)
    fx = np.mod(gx, 1.0) - 0.5
    fy = np.mod(gy, 1.0) - 0.5
    button_ao = np.exp(-(fx * fx + fy * fy) / 0.018)
    pillow_highlight = np.clip(1.0 - 2.2 * (fx * fx + fy * fy), 0.0, 1.0) * 0.14

    shade = 1.0 + grain + pillow_highlight - tuft_strength * np.maximum(crease1, crease2) - (tuft_strength * 1.15) * button_ao
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    for c in range(3):
        rgb[:, :, c] = base_rgb[c] * shade
    return make_packed_image(name, w, h, rgb)


def make_water_bottle_texture():
    """Generates a 256x256 translucent cyan-blue polycarbonate water jug texture with caustic bands and meniscus."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / w
    v = y / h
    caustic = 0.08 * np.sin(u * math.tau * 6.0 + np.sin(v * math.tau * 8.0)) + 0.05 * np.cos(v * math.tau * 14.0)
    meniscus = np.exp(-((v - 0.78) / 0.025) ** 2) * 0.28
    depth_tint = 0.78 + 0.25 * v + caustic + meniscus
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[:, :, 0] = 0.12 * depth_tint + meniscus * 0.45
    rgb[:, :, 1] = 0.56 * depth_tint + meniscus * 0.48
    rgb[:, :, 2] = 0.94 * depth_tint + meniscus * 0.30
    return make_packed_image("WaterBottleTex", w, h, rgb)


def make_ide_code_texture(name, accent_rgb, layout="editor"):
    """Generates a 512x256 realistic dark-mode IDE monitor texture (#0B0F14) with crisp 2px left-aligned code lines,
    file tree, minimap, and distinct layouts for left ('editor') vs right ('split_terminal') monitors.
    """
    h, w = 256, 512
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    # Deep dark slate IDE background (#0B0F14 -> linear ~0.018, 0.025, 0.038)
    rgb[:, :, 0] = 0.022
    rgb[:, :, 1] = 0.030
    rgb[:, :, 2] = 0.045

    # Left file-tree sidebar (15% width = 76 px)
    sb_w = 76 if layout == "editor" else 56
    rgb[:, :sb_w, 0] = 0.015
    rgb[:, :sb_w, 1] = 0.021
    rgb[:, :sb_w, 2] = 0.032
    rgb[:, sb_w:sb_w + 1, :] = (0.08, 0.11, 0.15)

    # Top tab bar (y in [238, 256])
    rgb[238:, :, 0] = 0.030
    rgb[238:, :, 1] = 0.040
    rgb[238:, :, 2] = 0.058
    rgb[238:240, sb_w + 6:sb_w + 96, :] = accent_rgb
    rgb[244:248, sb_w + 16:sb_w + 78, :] = (0.45, 0.54, 0.66)
    rgb[244:248, sb_w + 112:sb_w + 170, :] = (0.22, 0.28, 0.36)

    # Bottom status bar (y in [0, 11])
    rgb[:11, :, 0] = accent_rgb[0] * 0.28
    rgb[:11, :, 1] = accent_rgb[1] * 0.28
    rgb[:11, :, 2] = accent_rgb[2] * 0.28
    rgb[4:7, 12:58, :] = accent_rgb

    # Restrained syntax palette (slate, cyan, soft mint, muted amber, soft lavender)
    palette = [
        (0.52, 0.62, 0.74),  # default slate identifier
        accent_rgb,          # primary accent
        (0.28, 0.72, 0.86),  # cyan method/property
        (0.84, 0.66, 0.32),  # muted amber literal
        (0.36, 0.76, 0.56),  # soft mint type
        (0.64, 0.52, 0.84),  # soft purple keyword
    ]

    min_y = 16 if layout == "editor" else 88
    if layout == "split_terminal":
        # Bottom terminal / telemetry panel (y in [12, 84])
        rgb[12:85, sb_w + 1:, 0] = 0.014
        rgb[12:85, sb_w + 1:, 1] = 0.019
        rgb[12:85, sb_w + 1:, 2] = 0.028
        rgb[84:86, sb_w + 1:, :] = (0.10, 0.14, 0.20)
        # Terminal header tabs ("TERMINAL", "OUTPUT", "ZERO TELEMETRY")
        for tx_off, t_col in ((14, accent_rgb), (76, (0.30, 0.38, 0.48)), (134, (0.30, 0.38, 0.48))):
            rgb[76:79, sb_w + tx_off:sb_w + tx_off + 42, :] = t_col
        # Left-aligned terminal log lines (2px tall, 5px step) + right-side live sparkline bars
        for trow in range(11):
            ty = 18 + trow * 5
            rgb[ty:ty + 2, sb_w + 12:sb_w + 26, :] = (0.25, 0.62, 0.48)
            cur = sb_w + 32
            for seg in range(2 + (trow % 4)):
                slen = 18 + ((trow * 17 + seg * 23) % 44)
                if cur + slen > 360:
                    break
                rgb[ty:ty + 2, cur:cur + slen, :] = (0.38, 0.48, 0.58) if seg > 0 else accent_rgb
                cur += slen + 6
        # Telemetry bar chart on the bottom-right of the split monitor (x in [380, 496])
        rgb[18:74, 376:377, :] = (0.10, 0.14, 0.20)
        for b_i in range(18):
            bx = 384 + b_i * 6
            bh = 10 + ((b_i * 11 + 7) % 42)
            bcol = accent_rgb if b_i % 4 != 0 else (0.86, 0.56, 0.26)
            rgb[20:20 + bh, bx:bx + 3, :] = bcol

    # Main code editor area (thin 2px lines, 5px vertical pitch, left-aligned with realistic block indentation)
    indent_pattern = [
        0, 0, 1, 2, 2, 3, 3, 2, 2, 1, 0,
        0, 1, 1, 2, 3, 4, 3, 2, 1, 1, 0,
        0, 1, 2, 2, 1, 0, 0, 1, 2, 1, 0,
        1, 2, 3, 3, 2, 1, 0, 0, 1, 0, 0,
    ]
    n_rows = (230 - min_y) // 5
    for row in range(n_rows):
        y0 = 228 - row * 5
        y1 = y0 + 2
        if y0 < min_y:
            break
        # File tree entry in left sidebar
        if row < 28 and row % 2 == 0:
            sb_indent = 10 + ((row // 2) % 3) * 6
            sb_len = min(sb_w - sb_indent - 8, 22 + ((row * 9) % 26))
            if sb_len > 6:
                rgb[y0:y1, sb_indent:sb_indent + 4, :] = accent_rgb if row % 6 == 0 else (0.28, 0.36, 0.46)
                rgb[y0:y1, sb_indent + 7:sb_indent + 7 + sb_len, :] = (0.34, 0.42, 0.52)

        # Leave blank lines between functions
        if row in (10, 21, 27, 38):
            continue

        # Line number in gutter
        rgb[y0:y1, sb_w + 10:sb_w + 18, :] = (0.20, 0.26, 0.34)

        # Highlight active line subtly
        if row == 14:
            rgb[y0 - 1:y1 + 1, sb_w + 2:452, 0] = 0.042
            rgb[y0 - 1:y1 + 1, sb_w + 2:452, 1] = 0.058
            rgb[y0 - 1:y1 + 1, sb_w + 2:452, 2] = 0.082

        # Left-aligned code tokens with tab indentation
        tabs = indent_pattern[row % len(indent_pattern)]
        cursor = sb_w + 28 + tabs * 14
        n_tokens = 2 + ((row * 5 + 3) % 5)
        for tok in range(n_tokens):
            t_len = 10 + ((row * 19 + tok * 29) % 34)
            if cursor + t_len > 442:
                break
            col = palette[(row + tok * 2) % len(palette)]
            rgb[y0:y1, cursor:cursor + t_len, 0] = col[0]
            rgb[y0:y1, cursor:cursor + t_len, 1] = col[1]
            rgb[y0:y1, cursor:cursor + t_len, 2] = col[2]
            cursor += t_len + 5

        # Right-edge code minimap (x in [462, 502])
        mm_x = 464 + tabs * 3
        mm_len = min(502 - mm_x, 12 + ((row * 7) % 22))
        rgb[y0:y0 + 1, mm_x:mm_x + mm_len, :] = (0.22, 0.30, 0.40)

    return make_packed_image(name, w, h, rgb)


def make_fabric_texture(name, base_rgb):
    """Generates a 128x128 fine micro-grain satin/fabric texture without diagonal macro stripes."""
    h, w = 128, 128
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    weave = 0.018 * np.sin(x * math.pi * 0.75) * np.cos(y * math.pi * 0.75) + 0.010 * np.cos(y * math.pi * 0.5)
    vignette = 0.97 + 0.03 * np.sin((y / h) * math.pi)
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    for c in range(3):
        rgb[:, :, c] = base_rgb[c] * vignette + weave
    return make_packed_image(name, w, h, rgb)


def apply_box_uv_and_smooth(obj, scale_uv=1.5):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active
    for poly in mesh.polygons:
        poly.use_smooth = True
        n = poly.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop_idx in poly.loop_indices:
            v = mesh.vertices[mesh.loops[loop_idx].vertex_index].co
            if az >= ax and az >= ay:
                u, w_uv = v.x * scale_uv + 0.5, v.y * scale_uv + 0.5
            elif ay >= ax:
                u, w_uv = v.x * scale_uv + 0.5, v.z * scale_uv + 0.5
            else:
                u, w_uv = v.y * scale_uv + 0.5, v.z * scale_uv + 0.5
            uv_layer.data[loop_idx].uv = (u, w_uv)
    return obj


def add_beveled_box(name, loc, dims, mat, bevel=0.025, segments=3, rot=(0, 0, 0), subsurf=0, uv_scale=1.5):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = dims
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.profile = 0.55
        bpy.ops.object.modifier_apply(modifier=mod.name)
    if subsurf > 0:
        sub = obj.modifiers.new("Subsurf", "SUBSURF")
        sub.levels = subsurf
        bpy.ops.object.modifier_apply(modifier=sub.name)
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=uv_scale)


def add_smooth_cyl(name, loc, r0, r1, depth, mat, rot=(0, 0, 0), verts=28, bevel=0.003):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r0, radius2=r1, depth=depth, location=loc, rotation=rot)
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=1.8)


def add_pillow(name, loc, dims, mat, crown=0.14, tuft_indent=0.0, cuts=3, uv_scale=1.2):
    """Builds a sculpted soft cushion with organic pillow crowning and optional diamond tufting dimples."""
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=cuts, use_grid_fill=True)
    hx, hy, hz = dims[0] * 0.5, dims[1] * 0.5, dims[2] * 0.5
    for v in bm.verts:
        # Normalized [-1, 1] cube coords
        ux, uy, uz = v.co.x * 2.0, v.co.y * 2.0, v.co.z * 2.0
        # Spherified rounded-box blend + soft pillow dome on top/front faces
        dome_xy = max(0.0, 1.0 - 0.45 * (ux * ux + uy * uy))
        dome_xz = max(0.0, 1.0 - 0.45 * (ux * ux + uz * uz))
        # Round corners smoothly
        rx = math.copysign(abs(ux) ** 0.82, ux) * hx
        ry = math.copysign(abs(uy) ** 0.82, uy) * hy
        rz = math.copysign(abs(uz) ** 0.82, uz) * hz
        rz += crown * hz * dome_xy * uz
        ry += crown * 0.65 * hy * dome_xz * uy
        if tuft_indent > 0.0 and (uz > 0.35 or uy > 0.35):
            # Subtle diamond quilt wave on the surface
            q = math.sin(ux * math.pi * 2.0) * math.sin((uz if uy > 0.35 else uy) * math.pi * 1.5)
            d_amt = tuft_indent * (q * q)
            if uz > 0.35:
                rz -= d_amt
            if uy > 0.35:
                ry -= d_amt
        v.co = V((loc[0] + rx, loc[1] + ry, loc[2] + rz))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=uv_scale)


def export_hero_glb(filename):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    for poly in obj.data.polygons:
        poly.use_smooth = True

    verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    mn = tuple(round(min(v[i] for v in verts), 4) for i in range(3))
    mx = tuple(round(max(v[i] for v in verts), 4) for i in range(3))
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)

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
        d = ROOT / sub
        if d.exists():
            shutil.copy2(out_path, d / filename)
    print(f"EXPORTED {filename} | size={out_path.stat().st_size}B | tris={tris} | min_bl={mn} | max_bl={mx}")


# ==============================================================================
# 1. `prop_leather_sofa.glb` — Executive Tufted Oxblood/Espresso Leather Sofa
#    Target Blender BBox: X in [-1.24, +1.24], Y in [-0.52, +0.54], Z in [0.06, 1.02]
# ==============================================================================
def build_prop_leather_sofa():
    clear_scene()
    tex_frame = make_leather_texture("LeatherFrameTex", (0.25, 0.085, 0.075), tuft_grid=(8, 3), tuft_strength=0.30)
    tex_cush = make_leather_texture("LeatherCushionTex", (0.34, 0.115, 0.098), tuft_grid=(4, 3), tuft_strength=0.40)

    m_sofa = make_mat("Leather90s", (1.0, 1.0, 1.0, 1.0), metallic=0.08, roughness=0.36, image=tex_frame)
    m_cush = make_mat("LeatherCushion", (1.0, 1.0, 1.0, 1.0), metallic=0.06, roughness=0.40, image=tex_cush)
    m_wood = make_mat("EspressoPlinth", (0.11, 0.065, 0.045, 1.0), metallic=0.10, roughness=0.34)
    m_chrome = make_mat("DarkChromeFeet", (0.58, 0.52, 0.42, 1.0), metallic=0.88, roughness=0.20)
    m_button = make_mat("TuftButton", (0.16, 0.045, 0.040, 1.0), metallic=0.25, roughness=0.28)

    # 1) Recessed dark espresso wood plinth & 4 tapered brushed-brass/chrome executive feet (Z down to 0.06)
    add_beveled_box("PlinthFrame", (0.0, 0.01, 0.145), (2.30, 0.94, 0.07), m_wood, bevel=0.015, segments=3)
    for lx in (-1.08, -0.36, 0.36, 1.08):
        for ly in (-0.41, 0.43):
            add_smooth_cyl(f"Foot_{lx}_{ly}", (lx, ly, 0.095), 0.024, 0.036, 0.070, m_chrome, verts=20, bevel=0.004)
            add_smooth_cyl(f"FootCollar_{lx}_{ly}", (lx, ly, 0.066), 0.028, 0.028, 0.012, m_chrome, verts=20, bevel=0.002)

    # 2) Upholstered lower sofa deck & front bullion piping rail
    add_pillow("LowerDeck", (0.0, 0.01, 0.255), (2.28, 0.98, 0.17), m_sofa, crown=0.08, tuft_indent=0.004, uv_scale=1.1)
    add_smooth_cyl("FrontPipingWelt", (0.0, 0.495, 0.325), 0.018, 0.018, 1.98, m_button, rot=(0, math.pi / 2, 0), verts=18, bevel=0.002)
    add_smooth_cyl("BasePipingWelt", (0.0, 0.495, 0.185), 0.016, 0.016, 2.24, m_button, rot=(0, math.pi / 2, 0), verts=18, bevel=0.002)

    # 3) Sculpted rolled Chesterfield armrests with front brass nailhead trim & cylindrical bolster pillows
    for side in (-1, 1):
        ax = side * 1.11
        add_pillow(f"ArmBlock_{side}", (ax, 0.01, 0.44), (0.24, 1.02, 0.42), m_sofa, crown=0.10, tuft_indent=0.004, uv_scale=1.3)
        # Rolled upper scroll of the armrest
        add_smooth_cyl(f"ArmScroll_{side}", (ax - side * 0.015, 0.01, 0.64), 0.125, 0.125, 1.03, m_sofa, rot=(math.pi / 2, 0, 0), verts=28, bevel=0.018)
        # Front decorative leather rosette & brass nailhead ring
        add_smooth_cyl(f"ArmFrontCap_{side}", (ax - side * 0.015, 0.525, 0.64), 0.095, 0.112, 0.025, m_cush, rot=(math.pi / 2, 0, 0), verts=24, bevel=0.005)
        for k in range(8):
            ang = k * math.tau / 8.0
            nx = ax - side * 0.015 + 0.078 * math.cos(ang)
            nz = 0.64 + 0.078 * math.sin(ang)
            bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=0.0085, location=(nx, 0.536, nz))
            stud = bpy.context.active_object
            stud.data.materials.append(m_chrome)
            for p in stud.data.polygons:
                p.use_smooth = True
        # Plush inner cylindrical bolster cushion resting against each armrest
        add_smooth_cyl(f"Bolster_{side}", (side * 0.90, 0.06, 0.52), 0.078, 0.078, 0.68, m_cush, rot=(math.pi / 2, 0, 0), verts=20, bevel=0.015)

    # 4) Three plump crowned seat cushions with perimeter welt seams & tufted dimple buttons
    for idx, cx in enumerate((-0.64, 0.0, 0.64)):
        add_pillow(f"SeatCushion_{idx}", (cx, 0.06, 0.415), (0.635, 0.80, 0.165), m_cush, crown=0.16, tuft_indent=0.008, uv_scale=1.4)
        for tx in (-0.15, 0.15):
            for ty in (-0.12, 0.18):
                bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=0.013, location=(cx + tx, 0.06 + ty, 0.495))
                btn = bpy.context.active_object
                btn.scale = (1.0, 1.0, 0.45)
                bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
                btn.data.materials.append(m_button)
                for p in btn.data.polygons:
                    p.use_smooth = True

    # 5) Sculpted 3-section diamond-tufted backrest + upper curved headrest roll (Z up to 1.02, Y back to -0.52)
    add_pillow("BackFrameShell", (0.0, -0.39, 0.66), (2.38, 0.25, 0.66), m_sofa, crown=0.08, tuft_indent=0.004, uv_scale=1.0)
    add_smooth_cyl("BackTopRoll", (0.0, -0.38, 0.94), 0.080, 0.080, 2.36, m_sofa, rot=(0, math.pi / 2, 0), verts=20, bevel=0.018)
    for idx, cx in enumerate((-0.64, 0.0, 0.64)):
        add_pillow(f"BackRestPillow_{idx}", (cx, -0.255, 0.69), (0.63, 0.19, 0.46), m_cush, crown=0.18, tuft_indent=0.010, uv_scale=1.5)
        for row_z, n_cols in ((0.61, 3), (0.73, 2), (0.83, 3)):
            for c_i in range(n_cols):
                bx = cx + (c_i - (n_cols - 1) * 0.5) * 0.18
                bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=6, radius=0.013, location=(bx, -0.158, row_z))
                btn = bpy.context.active_object
                btn.scale = (1.0, 0.45, 1.0)
                bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
                btn.data.materials.append(m_button)
                for p in btn.data.polygons:
                    p.use_smooth = True

    export_hero_glb("prop_leather_sofa.glb")


# ==============================================================================
# 2. `prop_water_cooler.glb` — Modern Brushed-Steel & Charcoal Water Dispenser
#    Target Blender BBox: X in [-0.24, +0.308], Y in [-0.285, +0.23], Z in [0.0, 1.7025]
# ==============================================================================
def build_prop_water_cooler():
    clear_scene()
    tex_bottle = make_water_bottle_texture()
    tex_steel = make_fabric_texture("BrushedSteelTex", (0.84, 0.88, 0.93))

    m_body = make_mat("CoolerBody", (1.0, 1.0, 1.0, 1.0), metallic=0.35, roughness=0.26, image=tex_steel)
    m_side = make_mat("CoolerSide", (0.22, 0.26, 0.32, 1.0), metallic=0.55, roughness=0.32)
    m_recess = make_mat("CoolerRecess", (0.08, 0.10, 0.14, 1.0), metallic=0.40, roughness=0.24)
    m_grill = make_mat("CoolerGrill", (0.62, 0.68, 0.75, 1.0), metallic=0.85, roughness=0.22)
    m_bottle = make_mat("WaterBottle", (1.0, 1.0, 1.0, 1.0), metallic=0.05, roughness=0.10, image=tex_bottle)
    m_water = make_mat("WaterInner", (0.05, 0.48, 0.92, 1.0), metallic=0.05, roughness=0.08, emission_strength=0.18)
    m_tap_hot = make_mat("TapHot", (0.94, 0.14, 0.20, 1.0), roughness=0.22, emission_strength=0.15)
    m_tap_cold = make_mat("TapCold", (0.10, 0.62, 0.98, 1.0), roughness=0.22, emission_strength=0.15)
    m_cup = make_mat("CupWhite", (0.95, 0.97, 0.99, 1.0), roughness=0.30)

    # Note: In Blender coordinates for prop_water_cooler (built via rl_pos(x, y, z) = (x, -z, y)):
    # Front of dispenser is -Y_blender (+Z_raylib -> -Y_blender = -0.23..-0.285), Height is +Z_blender (0.0..1.7025).
    # 1) Anti-slip dark feet & sculpted brushed-steel cabinet tower
    for fx in (-0.18, 0.18):
        for fy in (-0.18, 0.18):
            add_smooth_cyl(f"Foot_{fx}_{fy}", (fx, fy, 0.015), 0.032, 0.038, 0.030, m_recess, verts=20, bevel=0.003)

    add_beveled_box("CabinetTower", (0.0, 0.0, 0.53), (0.45, 0.45, 1.02), m_body, bevel=0.038, segments=4)
    # Brushed charcoal side armor panels with ventilation louvres
    for sx in (-0.230, 0.230):
        add_beveled_box(f"SideArmor_{sx}", (sx, 0.0, 0.53), (0.020, 0.37, 0.96), m_side, bevel=0.008, segments=2)
        for lv in range(7):
            lz = 0.22 + lv * 0.045
            add_beveled_box(f"SideLouvre_{sx}_{lv}", (sx + (0.008 if sx > 0 else -0.008), 0.04, lz), (0.008, 0.20, 0.016), m_recess, bevel=0.002)

    # Lower refrigerator storage door with recessed chrome handle & status LED header bar
    add_beveled_box("FridgeDoor", (0.0, -0.228, 0.28), (0.37, 0.024, 0.44), m_body, bevel=0.012, segments=3)
    add_beveled_box("FridgeHandleRecess", (0.12, -0.238, 0.34), (0.028, 0.012, 0.16), m_recess, bevel=0.003)
    add_smooth_cyl("FridgeHandleBar", (0.12, -0.244, 0.34), 0.007, 0.007, 0.15, m_grill, verts=16, bevel=0.001)

    # Upper status indicator bezel (Power / Heating Red / Cooling Cyan LEDs)
    add_beveled_box("StatusHeader", (0.0, -0.228, 0.96), (0.36, 0.020, 0.055), m_recess, bevel=0.006)
    add_smooth_cyl("LedHot", (-0.065, -0.238, 0.96), 0.008, 0.008, 0.008, m_tap_hot, rot=(math.pi / 2, 0, 0), verts=16, bevel=0.001)
    add_smooth_cyl("LedCold", (0.065, -0.238, 0.96), 0.008, 0.008, 0.008, m_tap_cold, rot=(math.pi / 2, 0, 0), verts=16, bevel=0.001)

    # Deep sculpted dispensing alcove + slatted stainless-steel drip tray
    add_beveled_box("AlcoveBack", (0.0, -0.185, 0.77), (0.32, 0.08, 0.28), m_recess, bevel=0.012, segments=3)
    for side in (-1, 1):
        add_beveled_box(f"AlcoveWall_{side}", (side * 0.165, -0.205, 0.77), (0.032, 0.065, 0.28), m_side, bevel=0.008)
    add_beveled_box("DripTrayBasin", (0.0, -0.238, 0.615), (0.33, 0.095, 0.042), m_side, bevel=0.010, segments=3)
    add_beveled_box("DripTrayRim", (0.0, -0.238, 0.636), (0.315, 0.082, 0.008), m_grill, bevel=0.003)
    for g_i in range(7):
        gy = -0.206 - g_i * 0.0105
        add_beveled_box(f"DripGrateBar_{g_i}", (0.0, gy, 0.641), (0.28, 0.0045, 0.004), m_recess, bevel=0.001)

    # Ergonomic hot (red) & cold (blue) push paddles, chrome spigots, and a paper cup sitting in the alcove
    for sx, m_tap, tag in ((-0.068, m_tap_hot, "Hot"), (0.068, m_tap_cold, "Cold")):
        add_smooth_cyl(f"SpigotBody_{tag}", (sx, -0.215, 0.875), 0.016, 0.018, 0.048, m_grill, rot=(math.pi / 2, 0, 0), verts=20, bevel=0.002)
        add_smooth_cyl(f"SpigotNozzle_{tag}", (sx, -0.230, 0.852), 0.009, 0.012, 0.032, m_grill, verts=18, bevel=0.002)
        add_beveled_box(f"PushPaddle_{tag}", (sx, -0.215, 0.805), (0.052, 0.014, 0.075), m_tap, bevel=0.005, rot=(0.18, 0, 0))
        add_beveled_box(f"TopLever_{tag}", (sx, -0.242, 0.892), (0.038, 0.034, 0.012), m_tap, bevel=0.004)

    # Filled cup on the drip tray under the cold tap
    add_smooth_cyl("AlcoveCup", (0.068, -0.232, 0.682), 0.025, 0.033, 0.078, m_cup, verts=24, bevel=0.002)
    add_smooth_cyl("AlcoveCupWater", (0.068, -0.232, 0.714), 0.029, 0.029, 0.006, m_water, verts=20, bevel=0.001)

    # Side cylindrical cup dispenser with transparent view slot, stacked inner cups & mounting bracket
    add_beveled_box("CupBracketTop", (0.238, -0.10, 0.89), (0.048, 0.044, 0.032), m_side, bevel=0.005)
    add_beveled_box("CupBracketBot", (0.238, -0.10, 0.67), (0.048, 0.044, 0.032), m_side, bevel=0.005)
    add_smooth_cyl("CupTubeBody", (0.264, -0.10, 0.78), 0.043, 0.043, 0.33, m_cup, verts=28, bevel=0.004)
    add_smooth_cyl("CupTubeCap", (0.264, -0.10, 0.952), 0.045, 0.044, 0.022, m_side, verts=28, bevel=0.003)
    add_smooth_cyl("CupTubeNozzle", (0.264, -0.10, 0.602), 0.034, 0.042, 0.032, m_grill, verts=24, bevel=0.002)

    # 2) Sculpted 19-Litre Polycarbonate Water Jug with smooth profile loft, ribs, internal water & meniscus
    add_smooth_cyl("ReceiverCollar", (0.0, 0.0, 1.058), 0.162, 0.142, 0.055, m_side, verts=36, bevel=0.006)
    jug_profile = [
        # (z_height, radius)
        (1.080, 0.064),
        (1.135, 0.066),
        (1.168, 0.135),
        (1.195, 0.188),
        (1.215, 0.198),  # Rib 1 peak
        (1.235, 0.186),
        (1.315, 0.186),
        (1.338, 0.200),  # Rib 2 peak
        (1.360, 0.186),
        (1.438, 0.186),
        (1.460, 0.200),  # Rib 3 peak
        (1.482, 0.186),
        (1.565, 0.186),
        (1.605, 0.174),
        (1.648, 0.132),
        (1.678, 0.088),
        (1.685, 0.010),
    ]
    n_seg = 40
    j_verts = []
    for zh, rad in jug_profile:
        for i in range(n_seg):
            a = i * math.tau / n_seg
            j_verts.append(V((rad * math.cos(a), rad * math.sin(a), zh)))
    j_faces = [
        (k * n_seg + i, k * n_seg + (i + 1) % n_seg, (k + 1) * n_seg + (i + 1) % n_seg, (k + 1) * n_seg + i)
        for k in range(len(jug_profile) - 1) for i in range(n_seg)
    ]
    j_faces.append(tuple(range((len(jug_profile) - 1) * n_seg, len(jug_profile) * n_seg)))
    j_mesh = bpy.data.meshes.new("PolycarbonateJug")
    j_mesh.from_pydata(j_verts, [], j_faces)
    j_mesh.update()
    j_obj = bpy.data.objects.new("PolycarbonateJug", j_mesh)
    bpy.context.collection.objects.link(j_obj)
    j_mesh.materials.append(m_bottle)
    uv = j_mesh.uv_layers.new(name="UVMap")
    for poly in j_mesh.polygons:
        poly.use_smooth = True
        for loop_idx in poly.loop_indices:
            v = j_mesh.vertices[j_mesh.loops[loop_idx].vertex_index].co
            uv.data[loop_idx].uv = (math.atan2(v.y, v.x) / math.tau + 0.5, (v.z - 1.08) / 0.62)

    # Inner deep-blue water column + bright top meniscus disc + top handle cap (Z = 1.7025)
    add_smooth_cyl("InnerWaterColumn", (0.0, 0.0, 1.365), 0.173, 0.173, 0.35, m_water, verts=36, bevel=0.008)
    add_smooth_cyl("BottleTopCrownCap", (0.0, 0.0, 1.690), 0.090, 0.086, 0.025, m_water, verts=28, bevel=0.004)

    export_hero_glb("prop_water_cooler.glb")


# ==============================================================================
# 3. `prop_workstation_full.glb` — High-Craft Dual-Monitor Developer Cubicle
#    Target Blender BBox: X in [-1.13, +1.125], Y in [-0.63, +1.0144], Z in [0.0, 1.46]
# ==============================================================================
def build_prop_workstation_full():
    clear_scene()
    tex_part = make_fabric_texture("AcousticPartitionTex", (0.21, 0.25, 0.34))
    tex_desk = make_fabric_texture("DeskSurfaceTex", (0.28, 0.32, 0.39))
    tex_code1 = make_ide_code_texture("IdeScreenCyan", (0.14, 0.82, 0.96), layout="editor")
    tex_code2 = make_ide_code_texture("IdeScreenMint", (0.22, 0.88, 0.56), layout="split_terminal")
    tex_chair = make_fabric_texture("ChairMeshTex", (0.14, 0.17, 0.23))

    m_part = make_mat("PartitionFabric", (1.0, 1.0, 1.0, 1.0), roughness=0.78, image=tex_part)
    m_glass = make_mat("FrostedGlass", (0.58, 0.82, 0.98, 1.0), metallic=0.15, roughness=0.12, emission_strength=0.12)
    m_desk = make_mat("DeskTop", (1.0, 1.0, 1.0, 1.0), metallic=0.12, roughness=0.36, image=tex_desk)
    m_legs = make_mat("DarkMetal", (0.13, 0.15, 0.20, 1.0), metallic=0.82, roughness=0.28)
    m_pc = make_mat("PcCase", (0.08, 0.09, 0.12, 1.0), metallic=0.72, roughness=0.26)
    m_code1 = make_mat("MonitorCode1", (1.0, 1.0, 1.0, 1.0), roughness=0.12, image=tex_code1, emission_strength=0.72)
    m_code2 = make_mat("MonitorCode2", (1.0, 1.0, 1.0, 1.0), roughness=0.12, image=tex_code2, emission_strength=0.72)
    m_chair = make_mat("ChairMesh", (1.0, 1.0, 1.0, 1.0), roughness=0.55, image=tex_chair)
    m_folder_r = make_mat("RedFolder", (0.88, 0.20, 0.26, 1.0), roughness=0.45)

    # 1) L-Shaped Acoustic Partition Wall with Aluminum Trim & Frosted Glass Transom (Z up to 1.46)
    add_beveled_box("BackPartitionPanel", (0.0, -0.58, 0.55), (2.23, 0.09, 1.08), m_part, bevel=0.016, segments=3)
    add_beveled_box("BackPartitionRailMid", (0.0, -0.58, 1.10), (2.25, 0.10, 0.035), m_legs, bevel=0.006)
    add_beveled_box("BackPartitionRailTop", (0.0, -0.58, 1.445), (2.25, 0.09, 0.030), m_legs, bevel=0.006)
    add_beveled_box("BackFrostedGlass", (0.0, -0.58, 1.28), (2.20, 0.045, 0.34), m_glass, bevel=0.006)
    for gx in (-1.11, -0.37, 0.37, 1.11):
        add_beveled_box(f"GlassMullion_{gx}", (gx, -0.58, 1.28), (0.032, 0.085, 0.36), m_legs, bevel=0.005)

    add_beveled_box("SidePartitionPanel", (-1.08, -0.05, 0.55), (0.09, 1.13, 1.08), m_part, bevel=0.016, segments=3)
    add_beveled_box("SidePartitionCap", (-1.08, -0.05, 1.095), (0.10, 1.15, 0.030), m_legs, bevel=0.006)

    # 2) Ergonomic Beveled Desk Surface + Desk Mat + Cable Grommet + Cantilever Steel Frame Legs
    add_beveled_box("DeskTopSlab", (0.0, 0.0, 0.76), (2.10, 1.00, 0.062), m_desk, bevel=0.018, segments=4)
    add_beveled_box("DeskPadMat", (-0.06, 0.14, 0.793), (0.86, 0.38, 0.006), m_pc, bevel=0.002)
    add_smooth_cyl("CableGrommet", (-0.65, -0.32, 0.792), 0.036, 0.036, 0.008, m_pc, verts=20, bevel=0.002)
    for lx in (-0.95, 0.95):
        add_beveled_box(f"LegFootBar_{lx}", (lx, 0.0, 0.025), (0.075, 0.90, 0.045), m_legs, bevel=0.008)
        for ly in (-0.42, 0.42):
            add_smooth_cyl(f"LegColumn_{lx}_{ly}", (lx, ly, 0.38), 0.034, 0.034, 0.72, m_legs, verts=20, bevel=0.004)
    add_beveled_box("UnderDeskBeam", (0.0, -0.25, 0.70), (1.92, 0.08, 0.055), m_legs, bevel=0.006)

    # 3) Dual Thin-Bezel Curved Monitors on Articulated VESA Arms with 0..1 Local Planar Screen UVs
    for idx, (mx, rot_z, m_scr) in enumerate(((-0.42, -0.18, m_code1), (0.34, 0.18, m_code2))):
        add_beveled_box(f"MonBase_{idx}", (mx, -0.32, 0.796), (0.22, 0.16, 0.014), m_legs, bevel=0.004)
        add_smooth_cyl(f"MonPillar_{idx}", (mx, -0.34, 0.94), 0.024, 0.028, 0.28, m_legs, verts=20, bevel=0.003)
        add_beveled_box(f"MonChassis_{idx}", (mx, -0.24, 1.12), (0.72, 0.036, 0.44), m_pc, bevel=0.012, segments=3, rot=(0, 0, rot_z))
        # Build the display panel unrotated first so we can map exact 0..1 screen UVs, then rotate & translate!
        disp = add_beveled_box(f"MonDisplay_{idx}", (0.0, 0.0, 0.0), (0.685, 0.010, 0.402), m_scr, bevel=0.002)
        uv_d = disp.data.uv_layers.active
        for poly in disp.data.polygons:
            for loop_idx in poly.loop_indices:
                v = disp.data.vertices[disp.data.loops[loop_idx].vertex_index].co
                # Front of screen is +Y in Blender local space; -X is viewer-right when viewed from +Y, so u = 0.5 - v.x / 0.685
                u = max(0.0, min(1.0, 0.5 - v.x / 0.685))
                w_uv = max(0.0, min(1.0, 0.5 + v.z / 0.402))
                uv_d.data[loop_idx].uv = (u, w_uv)
        disp.rotation_euler = (0.0, 0.0, rot_z)
        disp.location = (mx, -0.219, 1.124)
        bpy.ops.object.select_all(action="DESELECT")
        disp.select_set(True)
        bpy.context.view_layer.objects.active = disp
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        add_beveled_box(f"MonChin_{idx}", (mx, -0.218, 0.910), (0.71, 0.014, 0.022), m_legs, bevel=0.003, rot=(0, 0, rot_z))

    # 4) Mechanical Keyboard with Sculpted Keycap Rows, Ergonomic Mouse, Coffee Mug & Project Binders
    add_beveled_box("KeyboardCase", (-0.08, 0.13, 0.806), (0.44, 0.16, 0.022), m_pc, bevel=0.005, segments=3)
    for r_i in range(5):
        ky = 0.075 + r_i * 0.027
        add_beveled_box(f"KeyRow_{r_i}", (-0.08, ky, 0.821), (0.405, 0.021, 0.010), m_legs, bevel=0.002)

    add_pillow("ErgonomicMouse", (0.26, 0.13, 0.808), (0.072, 0.118, 0.034), m_pc, crown=0.22, uv_scale=2.0)
    add_smooth_cyl("MouseWheel", (0.26, 0.095, 0.825), 0.008, 0.008, 0.006, m_legs, rot=(0, math.pi / 2, 0), verts=14, bevel=0.001)

    # Ceramic coffee mug with handle and dark espresso inside
    add_smooth_cyl("CoffeeMugBody", (-0.42, 0.20, 0.842), 0.038, 0.042, 0.096, m_folder_r, verts=24, bevel=0.003)
    add_smooth_cyl("CoffeeMugInner", (-0.42, 0.20, 0.885), 0.035, 0.035, 0.008, m_pc, verts=20, bevel=0.001)
    add_beveled_box("CoffeeMugHandle", (-0.465, 0.20, 0.842), (0.022, 0.012, 0.058), m_folder_r, bevel=0.004)

    # Workstation Tower with Brushed Dark-Steel Mesh Front Panel, Louvres & Single Subtle Status LED
    add_beveled_box("PcChassis", (0.82, -0.12, 1.02), (0.24, 0.46, 0.46), m_pc, bevel=0.016, segments=3)
    add_beveled_box("PcFrontBezel", (0.82, 0.112, 1.02), (0.21, 0.014, 0.42), m_legs, bevel=0.005)
    for lv in range(8):
        lz = 0.85 + lv * 0.038
        add_beveled_box(f"PcFrontMeshSlat_{lv}", (0.82, 0.118, lz), (0.175, 0.006, 0.016), m_pc, bevel=0.001)
    add_smooth_cyl("PcPowerLed", (0.82, 0.120, 1.19), 0.007, 0.007, 0.006, m_glass, rot=(math.pi / 2, 0, 0), verts=16, bevel=0.001)

    # Stacked Folders & Architect Lamp on left side of desk
    add_beveled_box("FolderRed", (-0.82, 0.15, 0.812), (0.26, 0.34, 0.036), m_folder_r, bevel=0.006, rot=(0, 0, 0.15))
    add_beveled_box("FolderDark", (-0.80, 0.13, 0.846), (0.25, 0.33, 0.028), m_legs, bevel=0.005, rot=(0, 0, -0.06))

    # 5) Executive Ergonomic Mesh Task Chair (5-star caster base, contoured cushion, curved lumbar mesh back & armrests)
    cx_c, cy_c = 0.0, 0.72
    add_smooth_cyl("ChairGasCylinder", (cx_c, cy_c, 0.26), 0.030, 0.034, 0.40, m_legs, verts=20, bevel=0.002)
    for k in range(5):
        ang = k * math.tau / 5.0 + math.pi / 5.0
        dx, dy = math.cos(ang), math.sin(ang)
        add_beveled_box(f"ChairStarArm_{k}", (cx_c + dx * 0.14, cy_c + dy * 0.14, 0.065), (0.044, 0.28, 0.032), m_legs, bevel=0.006, rot=(0, 0, ang - math.pi / 2))
        add_smooth_cyl(f"ChairCaster_{k}", (cx_c + dx * 0.26, cy_c + dy * 0.26, 0.028), 0.026, 0.026, 0.026, m_pc, rot=(0, math.pi / 2, ang), verts=16, bevel=0.003)

    add_pillow("ChairSeatCushion", (cx_c, cy_c, 0.48), (0.52, 0.50, 0.085), m_chair, crown=0.16, uv_scale=1.6)
    add_pillow("ChairMeshBackrest", (cx_c, 0.94, 0.82), (0.48, 0.075, 0.56), m_chair, crown=0.12, uv_scale=1.6)
    add_beveled_box("ChairSpineFrame", (cx_c, 0.975, 0.74), (0.07, 0.045, 0.52), m_legs, bevel=0.008, rot=(-0.10, 0, 0))
    add_beveled_box("ChairLumbarPad", (cx_c, 0.945, 0.68), (0.42, 0.045, 0.11), m_pc, bevel=0.010)
    add_pillow("ChairHeadrest", (cx_c, 0.955, 1.15), (0.28, 0.075, 0.13), m_chair, crown=0.15, uv_scale=1.8)
    for side in (-1, 1):
        ax = side * 0.275
        add_beveled_box(f"ChairArmPost_{side}", (ax, cy_c + 0.02, 0.56), (0.035, 0.055, 0.18), m_legs, bevel=0.005)
        add_beveled_box(f"ChairArmPad_{side}", (ax, cy_c + 0.02, 0.655), (0.072, 0.24, 0.028), m_pc, bevel=0.008)

    export_hero_glb("prop_workstation_full.glb")


def render_hero_props_proof():
    """Renders a high-resolution 3-panel verification image of the 3 upgraded hero props."""
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 680

    world = bpy.data.worlds.new("PropStudio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.11, 0.14, 0.19, 1.0)

    placements = [
        ("prop_leather_sofa.glb",     (-2.25, 0.0, 0.0), 0.78, 0.0),
        ("prop_water_cooler.glb",     ( 0.00, 0.0, 0.0), 0.95, math.pi),
        ("prop_workstation_full.glb", ( 2.25, 0.0, 0.0), 0.82, 0.0),
    ]
    for fname, offset, sc, rz in placements:
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / fname))
        for o in bpy.context.selected_objects:
            if o.type == "MESH":
                o.rotation_mode = "XYZ"
                o.scale = (sc, sc, sc)
                o.rotation_euler = (0.0, 0.0, rz)
                o.location = V(offset)

    cam_data = bpy.data.cameras.new("ProofCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 6.5
    cam = bpy.data.objects.new("ProofCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.35, 4.2, 2.35)
    target = V((0.0, 0.0, 0.68))
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (target - cam.location).to_track_quat("-Z", "Y")

    for loc, energy, col in [
        ((-3.5, 4.5, 4.2), 4.5, (1.0, 0.94, 0.84)),
        (( 3.8, 3.0, 3.5), 3.0, (0.82, 0.90, 1.0)),
        (( 0.0, -3.5, 2.8), 2.2, (1.0, 0.88, 0.72)),
    ]:
        ld = bpy.data.lights.new("Sun", type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new("Sun", ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_mode = "QUATERNION"
        lo.rotation_quaternion = (target - V(loc)).to_track_quat("-Z", "Y")

    out_png = OFFICE_DIR / "hero_props_overhaul_r12.png"
    scene.render.filepath = str(out_png)
    bpy.ops.render.render(write_still=True)
    print("RENDERED PROOF:", out_png)


if __name__ == "__main__":
    build_prop_leather_sofa()
    build_prop_water_cooler()
    build_prop_workstation_full()
    render_hero_props_proof()
