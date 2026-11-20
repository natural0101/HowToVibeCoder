"""Blender 5.1 generator for Office Realism Swarm Overhaul (@cc0-asset-lead slice):
  1. assets/models/prop_workstation_full.glb — High-Craft Dual-Monitor Developer Workstation Cubicle
     - Exact Blender BBox parity: X in [-1.13, +1.125], Y in [-0.63, +1.022], Z in [0.0, 1.46]
     - Exact desktop top surface at Z_bl = 0.792m (Y_gltf = 0.792m) for PodPizza/DrawPod/BuildSolids
     - Exact PodGeometry shader deformation compatibility (`VisualEngine3D.cs:36-39`):
       * Chair vertices strictly in `p.z < -0.405` (`Y_bl > +0.405`), `abs(p.x) < 0.38`, `(p.y < 0.70 || p.z < -0.77)`, pivot at `(0.0, -0.72)` (`Y_bl = +0.72`)
       * Monitor head vertices strictly in `p.y > 0.897` (`Z_bl > 0.897`), `abs(p.x) < 0.81`, `abs(p.z - 0.24) < 0.18` (`Y_bl in [-0.42, -0.06]`), pivots at `(-0.42, +0.25)` and `(+0.34, +0.25)` (`Y_bl = -0.25`), while fixed clamp arms stay at `Z_bl <= 0.895`!
     - Features:
       * L-shaped acoustic fabric cubicle partition with brushed dark-aluminum framing & frosted transom glass
       * Beveled dark-oak & slate-inlay desktop with rounded corners, dual cable grommets, cantilever steel frame legs, under-desk cable management tray & tangled power strip with glowing red rocker switch + power bricks
       * Dual ultra-slim curved-bezel 27" monitors on articulated desk-clamp VESA arms with crisp 512x256 dark-mode IDE / split terminal / team chat textures (`MonitorCode1`, `MonitorCode2`, shader tag 228) + sticky notes attached to bezel
       * Sculpted 75% mechanical keyboard with individual sculpted keycap rows, accent ESC/Enter keys & subtle RGB underglow strip, contoured ergonomic wireless mouse on stitched deskmat
       * Contoured ergonomic mesh office chair (5-star caster base, twin-wheel hooded casters, pneumatic gas cylinder, waterfall seat cushion, breathable mesh backrest with curved spine frame, lumbar support pad, headrest & 3D armrests)
       * Lived-in developer clutter: studio over-ear headphones hanging on desk hook, open spiral notebook & brass pen, ceramic espresso mug, project binders, and glass-panel workstation tower with internal GPU/cooler glow & front mesh slats.

  2. assets/models/prop_server_rack.glb — Perforated Steel 42U Enterprise IT Server Rack
     - Exact Blender BBox parity: X in [-0.47, +0.47], Y in [-0.4575, +0.4575], Z in [0.0, 2.30]
     - Features:
       * Heavy-duty dark anthracite steel 42U cabinet on 4 rolling swivel casters & leveling feet
       * Framed perforated honeycomb-mesh side panels with ventilation louvres & brass latches
       * Recessed 1U/2U server blades (`RackBlade`) with brushed-brass eject handles, hot-swap NVMe drive caddies, fine mesh intake grilles, and animated telemetry status LEDs (`LedGreen`, `LedCyan`, `LedAmber`, shader tag 192)
       * Vertical cable management raceways & colorful 3D curved Ethernet patch cable harnesses (cyan, cobalt blue, amber, crimson) looping between patch panels and switches
       * Top roof exhaust manifold with twin 3D axial cooling fan grilles and smoked-glass front/rear doors.

Includes 24-ray BVHTree Ambient Occlusion + curvature baked directly into every procedural texture (`UV.x` lookup for textured materials, plus `COLOR_0` vertex colors), strict outward normals (`recalc_face_normals`), and renders a 2-panel studio turntable proof `office_workstation_rack_proof.png`.
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
ROOT = Path(__file__).resolve().parents[1]
OUT_MODELS = ROOT / "assets/models"
BIN_REL_MODELS = ROOT / "bin/Release/net10.0/assets/models"
BIN_DBG_MODELS = ROOT / "bin/Debug/net10.0/assets/models"
SCREENSHOTS = ROOT / "screenshots"
COORD_BRAIN = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")

for d in (OUT_MODELS, SCREENSHOTS, COORD_BRAIN):
    os.makedirs(d, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def make_packed_image(name, width, height, rgb_array):
    img = bpy.data.images.new(name, width=width, height=height, alpha=False)
    rgba = np.ones((height, width, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_array, 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())
    img.pack()
    return img


def make_mat(name, rgba, metallic=0.1, roughness=0.42, image=None, emission_rgb=None, emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    mat.use_backface_culling = True
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
        if emission_strength > 0.0 and emission_rgb is None:
            links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    if emission_strength > 0.0 and emission_rgb is not None:
        bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    elif emission_strength > 0.0 and image is None:
        bsdf.inputs["Emission Color"].default_value = rgba
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def make_ao_Lookup_texture(name, base_rgb, style="metal", edge_boost=0.55):
    """Generates a 256x256 2D texture where:
      - Horizontal axis `u` (`0..1`): baked 24-ray BVHTree Ambient Occlusion + local curvature
        (`u=0.0` -> deep crevice shadow, `u=0.62` -> base color, `u=1.0` -> subtle bevel highlight)
      - Vertical axis `v` (`0..1`): realistic micro-grain (wood oak grain, brushed metal, woven acoustic fabric, perforated mesh, or matte polymer)
    """
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / float(w - 1)
    v = y / float(h - 1)
    rng = np.random.default_rng(2026 + sum(ord(c) for c in name))
    noise = rng.normal(0.0, 1.0, (h, w)).astype(np.float32)

    ao_part = np.clip(u / 0.62, 0.0, 1.0)
    edge_part = np.clip((u - 0.62) / 0.38, 0.0, 1.0)
    shade = (0.14 + 0.86 * (ao_part ** 1.35)) * (1.0 + edge_boost * (edge_part ** 1.55))

    if style == "wood_oak":
        grain = (
            1.0
            + 0.085 * np.sin(v * math.tau * 11.0 + 0.35 * np.sin(u * math.tau * 3.0))
            + 0.045 * np.cos(v * math.tau * 27.0)
            + 0.025 * noise
        )
    elif style == "fabric":
        grain = (
            1.0
            + 0.055 * np.sin(x * math.pi * 0.65) * np.cos(y * math.pi * 0.65)
            + 0.030 * noise
        )
    elif style == "mesh_perf":
        # Honeycomb perforated steel rack door/side panel pattern along v & u
        hx = np.abs(np.mod(y * 0.42, 1.0) - 0.5) * 2.0
        hy = np.abs(np.mod(x * 0.42 + 0.5 * np.mod(np.floor(y * 0.42), 2.0), 1.0) - 0.5) * 2.0
        hole = np.clip(1.0 - np.hypot(hx, hy) * 1.45, 0.0, 1.0)
        grain = (1.0 - 0.52 * (hole ** 0.65)) + 0.03 * noise
    elif style == "brass":
        grain = 1.0 + 0.065 * np.sin(y * 1.35) + 0.035 * noise
    else:  # brushed dark metal / polymer
        grain = 1.0 + 0.055 * np.sin(y * 1.10) + 0.035 * noise

    rgb = np.zeros((h, w, 3), dtype=np.float32)
    for c in range(3):
        rgb[:, :, c] = base_rgb[c] * shade * grain
    return make_packed_image(name, w, h, rgb)


def make_ide_code_texture(name, accent_rgb, layout="editor"):
    """Generates a 512x256 realistic dark-mode IDE monitor texture (#0A0E14) with syntax-highlighted code,
    file tree, terminal telemetry, and team chat / architecture widgets.
    """
    h, w = 256, 512
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[:, :, 0] = 0.018
    rgb[:, :, 1] = 0.025
    rgb[:, :, 2] = 0.038

    sb_w = 74 if layout == "editor" else 56
    rgb[:, :sb_w, :] = (0.012, 0.017, 0.027)
    rgb[:, sb_w:sb_w + 1, :] = (0.08, 0.11, 0.16)

    # Top tab bar
    rgb[238:, :, :] = (0.026, 0.036, 0.052)
    rgb[238:240, sb_w + 6:sb_w + 98, :] = accent_rgb
    rgb[244:248, sb_w + 14:sb_w + 82, :] = (0.48, 0.58, 0.70)
    rgb[244:248, sb_w + 112:sb_w + 174, :] = (0.24, 0.30, 0.40)

    # Bottom status bar
    rgb[:11, :, :] = (accent_rgb[0] * 0.26, accent_rgb[1] * 0.26, accent_rgb[2] * 0.26)
    rgb[4:7, 12:62, :] = accent_rgb

    palette = [
        (0.54, 0.64, 0.76),
        accent_rgb,
        (0.26, 0.76, 0.90),
        (0.90, 0.68, 0.30),
        (0.34, 0.82, 0.58),
        (0.68, 0.52, 0.88),
    ]

    min_y = 16 if layout == "editor" else 90
    if layout == "split_terminal":
        rgb[12:86, sb_w + 1:, :] = (0.012, 0.016, 0.025)
        rgb[85:87, sb_w + 1:, :] = (0.10, 0.14, 0.20)
        for tx_off, t_col in ((14, accent_rgb), (76, (0.30, 0.38, 0.48)), (136, (0.30, 0.38, 0.48))):
            rgb[77:80, sb_w + tx_off:sb_w + tx_off + 44, :] = t_col
        for trow in range(11):
            ty = 18 + trow * 5
            rgb[ty:ty + 2, sb_w + 12:sb_w + 26, :] = (0.24, 0.68, 0.50)
            cur = sb_w + 32
            for seg in range(2 + (trow % 4)):
                slen = 18 + ((trow * 17 + seg * 23) % 44)
                if cur + slen > 362:
                    break
                rgb[ty:ty + 2, cur:cur + slen, :] = (0.40, 0.50, 0.62) if seg > 0 else accent_rgb
                cur += slen + 6
        rgb[18:76, 376:377, :] = (0.10, 0.14, 0.20)
        for b_i in range(18):
            bx = 384 + b_i * 6
            bh = 10 + ((b_i * 11 + 7) % 44)
            bcol = accent_rgb if b_i % 4 != 0 else (0.90, 0.56, 0.24)
            rgb[20:20 + bh, bx:bx + 3, :] = bcol

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
        if row < 28 and row % 2 == 0:
            sb_indent = 10 + ((row // 2) % 3) * 6
            sb_len = min(sb_w - sb_indent - 8, 22 + ((row * 9) % 26))
            if sb_len > 6:
                rgb[y0:y1, sb_indent:sb_indent + 4, :] = accent_rgb if row % 6 == 0 else (0.28, 0.36, 0.46)
                rgb[y0:y1, sb_indent + 7:sb_indent + 7 + sb_len, :] = (0.36, 0.44, 0.54)
        if row in (10, 21, 27, 38):
            continue
        rgb[y0:y1, sb_w + 10:sb_w + 18, :] = (0.20, 0.26, 0.34)
        if row == 14:
            rgb[y0 - 1:y1 + 1, sb_w + 2:452, :] = (0.042, 0.058, 0.082)
        tabs = indent_pattern[row % len(indent_pattern)]
        cursor = sb_w + 28 + tabs * 14
        n_tokens = 2 + ((row * 5 + 3) % 5)
        for tok in range(n_tokens):
            t_len = 10 + ((row * 19 + tok * 29) % 34)
            if cursor + t_len > 442:
                break
            col = palette[(row + tok * 2) % len(palette)]
            rgb[y0:y1, cursor:cursor + t_len, :] = col
            cursor += t_len + 5
        mm_x = 464 + tabs * 3
        mm_len = min(502 - mm_x, 12 + ((row * 7) % 22))
        rgb[y0:y0 + 1, mm_x:mm_x + mm_len, :] = (0.24, 0.32, 0.42)

    return make_packed_image(name, w, h, rgb)


def apply_box_uv_and_smooth(obj, scale_uv=1.8):
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


def add_beveled_box(name, loc, dims, mat, bevel=0.015, segments=3, rot=(0, 0, 0), uv_scale=1.6):
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
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=uv_scale)


def add_smooth_cyl(name, loc, r0, r1, depth, mat, rot=(0, 0, 0), verts=24, bevel=0.0025):
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


def add_pillow(name, loc, dims, mat, crown=0.14, cuts=3, rot=(0, 0, 0), uv_scale=1.4):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=cuts, use_grid_fill=True)
    hx, hy, hz = dims[0] * 0.5, dims[1] * 0.5, dims[2] * 0.5
    for v in bm.verts:
        ux, uy, uz = v.co.x * 2.0, v.co.y * 2.0, v.co.z * 2.0
        dome_xy = max(0.0, 1.0 - 0.45 * (ux * ux + uy * uy))
        dome_xz = max(0.0, 1.0 - 0.45 * (ux * ux + uz * uz))
        rx = math.copysign(abs(ux) ** 0.82, ux) * hx
        ry = math.copysign(abs(uy) ** 0.82, uy) * hy
        rz = math.copysign(abs(uz) ** 0.82, uz) * hz
        rz += crown * hz * dome_xy * uz
        ry += crown * 0.65 * hy * dome_xz * uy
        v.co = V((rx, ry, rz))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=uv_scale)


def add_tube_curve(name, pts_and_radii, mat, sides=12, steps_per_span=4, closed=False):
    """Builds a smooth 3D Catmull-Rom tubular cable/pipe from a list of ((x, y, z), radius) in Blender coords."""
    dense = []
    n = len(pts_and_radii)
    for i in range(n - 1):
        p0, r0 = V(pts_and_radii[max(0, i - 1)][0]), pts_and_radii[max(0, i - 1)][1]
        p1, r1 = V(pts_and_radii[i][0]), pts_and_radii[i][1]
        p2, r2 = V(pts_and_radii[i + 1][0]), pts_and_radii[i + 1][1]
        p3, r3 = V(pts_and_radii[min(n - 1, i + 2)][0]), pts_and_radii[min(n - 1, i + 2)][1]
        s_cnt = steps_per_span if i < n - 2 else steps_per_span + 1
        for s in range(s_cnt):
            if i < n - 2 and s == steps_per_span:
                continue
            t = s / float(steps_per_span)
            t2, t3 = t * t, t * t * t
            pos = 0.5 * (
                (2.0 * p1)
                + (-p0 + p2) * t
                + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
                + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3
            )
            rad = r1 + (r2 - r1) * t
            dense.append((pos, rad))

    verts, faces = [], []
    m_cnt = len(dense)
    prev_n = V((0.0, 0.0, 1.0))
    for i, (p, r) in enumerate(dense):
        if i == 0:
            t_vec = (dense[1][0] - p).normalized()
        elif i == m_cnt - 1:
            t_vec = (p - dense[i - 1][0]).normalized()
        else:
            t_vec = (dense[i + 1][0] - dense[i - 1][0]).normalized()
        if t_vec.length < 1e-6:
            t_vec = V((0.0, 1.0, 0.0))
        n_vec = prev_n - t_vec * prev_n.dot(t_vec)
        if n_vec.length < 1e-4:
            up = V((1.0, 0.0, 0.0)) if abs(t_vec.x) < 0.8 else V((0.0, 0.0, 1.0))
            n_vec = t_vec.cross(up)
        n_vec.normalize()
        prev_n = n_vec
        b_vec = t_vec.cross(n_vec).normalized()
        for k in range(sides):
            ang = k * math.tau / sides
            verts.append(p + (n_vec * math.cos(ang) + b_vec * math.sin(ang)) * r)

    for i in range(m_cnt - 1):
        b0 = i * sides
        b1 = (i + 1) * sides
        for k in range(sides):
            kn = (k + 1) % sides
            faces.append((b0 + k, b0 + kn, b1 + kn, b1 + k))
    if not closed:
        faces.append(tuple(reversed(range(sides))))
        last = (m_cnt - 1) * sides
        faces.append(tuple(last + k for k in range(sides)))

    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=2.0)


def bake_24ray_ao_and_export(filename, preserve_uv_materials=(), ray_dist=0.38):
    """Joins all meshes, enforces outward normals, bakes 24-ray BVHTree Ambient Occlusion + local curvature
    into both `COLOR_0` AND `UV.x` (for non-screen AO-lookup materials so Raylib's direct texture sampler renders
    deep crevice shadows and crisp bevel edge highlights!), and exports GLB to assets/models and bin/.
    """
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    joined = bpy.context.active_object
    joined.name = Path(filename).stem
    joined.data.name = Path(filename).stem
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    mesh = joined.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()

    # Build BVHTree for 24-ray Fibonacci hemisphere Ambient Occlusion + curvature
    v_coords = [v.co.copy() for v in mesh.vertices]
    polys = [tuple(p.vertices) for p in mesh.polygons]
    bvh = BVHTree.FromPolygons(v_coords, polys, all_triangles=False)

    n_rays = 24
    golden_angle = math.pi * (3.0 - math.sqrt(5.0))
    hemi_dirs = []
    for i in range(n_rays):
        z_h = (i + 0.5) / n_rays
        r_h = math.sqrt(max(0.0, 1.0 - z_h * z_h))
        phi = i * golden_angle
        hemi_dirs.append(V((r_h * math.cos(phi), r_h * math.sin(phi), z_h)))

    neighbors = [[] for _ in range(len(mesh.vertices))]
    for e in mesh.edges:
        v0, v1 = e.vertices[0], e.vertices[1]
        neighbors[v0].append(v1)
        neighbors[v1].append(v0)

    vert_u = [0.62] * len(mesh.vertices)
    vert_ao = [1.0] * len(mesh.vertices)

    for idx, v in enumerate(mesh.vertices):
        p = v.co
        n = v.normal.normalized() if v.normal.length > 1e-6 else V((0.0, 0.0, 1.0))
        up = V((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else V((1.0, 0.0, 0.0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()

        origin = p + n * 0.0025
        occ = 0.0
        for ld in hemi_dirs:
            d = (t1 * ld.x + t2 * ld.y + n * ld.z).normalized()
            hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, ray_dist)
            if hit_loc is not None and hit_dist is not None:
                occ += (1.0 - min(1.0, hit_dist / ray_dist)) ** 1.35
        ao = max(0.08, 1.0 - (occ / n_rays) * 1.08)
        if p.z < 0.22:
            ao *= 0.74 + 0.26 * (p.z / 0.22)

        curv = 0.0
        if neighbors[idx]:
            acc = 0.0
            for nb in neighbors[idx]:
                dv = v_coords[nb] - p
                if dv.length > 1e-6:
                    acc += dv.normalized().dot(n)
            mean_dot = acc / len(neighbors[idx])
            curv = max(-0.20, min(0.26, -mean_dot * 0.58))

        vert_u[idx] = max(0.03, min(0.97, 0.62 * (ao ** 0.88) + curv))
        vert_ao[idx] = max(0.15, min(1.0, ao))

    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active
    preserve_set = set(preserve_uv_materials)

    for poly in mesh.polygons:
        poly.use_smooth = True
        mat_name = mesh.materials[poly.material_index].name if poly.material_index < len(mesh.materials) and mesh.materials[poly.material_index] else ""
        if mat_name in preserve_set:
            continue
        for l_idx in poly.loop_indices:
            vi = mesh.loops[l_idx].vertex_index
            old_uv = uv_layer.data[l_idx].uv
            uv_layer.data[l_idx].uv = (vert_u[vi], old_uv[1] % 1.0)

    if "COLOR_0" in mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes["COLOR_0"])
    col_attr = mesh.color_attributes.new(name="COLOR_0", type="BYTE_COLOR", domain="CORNER")
    for poly in mesh.polygons:
        for l_idx in poly.loop_indices:
            vi = mesh.loops[l_idx].vertex_index
            c = vert_ao[vi]
            col_attr.data[l_idx].color = (c, c, c, 1.0)
    mesh.color_attributes.active_color = col_attr

    out_path = OUT_MODELS / filename
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_normals=True,
        export_texcoords=True,
        export_materials="EXPORT",
        export_vertex_color="ACTIVE",
    )
    for target_dir in (BIN_REL_MODELS, BIN_DBG_MODELS):
        if target_dir.exists():
            shutil.copy2(out_path, target_dir / filename)

    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    print(
        f"EXPORTED {filename} | size={out_path.stat().st_size}B | tris={len(mesh.polygons)} | "
        f"min=({min(xs):.4f}, {min(ys):.4f}, {min(zs):.4f}) max=({max(xs):.4f}, {max(ys):.4f}, {max(zs):.4f})"
    )


# ==============================================================================
# 1. `prop_workstation_full.glb` — Lived-in High-Craft Dual-Monitor Developer Pod
#    Exact Blender BBox: X in [-1.13, +1.125], Y in [-0.63, +1.022], Z in [0.0, 1.46]
#    Exact PodGeometry shader invariants (`VisualEngine3D.cs:36-39`):
#      - Chair vertices: `Y_bl > +0.405`, `abs(X_bl) < 0.38`, `(Z_bl < 0.70 or Y_bl > 0.77)`, pivot at `(0.0, +0.72)`
#      - Monitor head vertices: `Z_bl > 0.897`, `abs(X_bl) < 0.81`, `Y_bl in [-0.42, -0.06]`, pivots at `(-0.42, -0.25)` and `(+0.34, -0.25)`
# ==============================================================================
def build_prop_workstation_full():
    clear_scene()
    # Calibrated linear PBR colors so Raylib's direct sRGB texture sampler shows rich, non-chalky materials!
    tex_part = make_ao_Lookup_texture("AcousticPartitionTex", (0.11, 0.14, 0.21), style="fabric", edge_boost=0.45)
    tex_desk = make_ao_Lookup_texture("WarmOakDeskTex", (0.28, 0.175, 0.095), style="wood_oak", edge_boost=0.48)
    tex_metal = make_ao_Lookup_texture("DarkGraphiteMetalTex", (0.068, 0.078, 0.098), style="metal", edge_boost=0.62)
    tex_pc = make_ao_Lookup_texture("MatteObsidianTex", (0.036, 0.042, 0.056), style="metal", edge_boost=0.55)
    tex_chair = make_ao_Lookup_texture("ErgonomicMeshTex", (0.052, 0.062, 0.086), style="fabric", edge_boost=0.52)
    tex_red = make_ao_Lookup_texture("CrimsonAccentTex", (0.58, 0.085, 0.105), style="fabric", edge_boost=0.45)
    tex_sticky = make_ao_Lookup_texture("StickyNotePaperTex", (0.86, 0.72, 0.22), style="fabric", edge_boost=0.25)
    tex_code1 = make_ide_code_texture("IdeScreenCyan", (0.14, 0.84, 0.98), layout="editor")
    tex_code2 = make_ide_code_texture("IdeScreenMint", (0.22, 0.90, 0.58), layout="split_terminal")

    m_part = make_mat("PartitionFabric", (1.0, 1.0, 1.0, 1.0), roughness=0.78, image=tex_part)
    m_glass = make_mat("FrostedGlass", (0.42, 0.68, 0.88, 1.0), metallic=0.15, roughness=0.14, emission_rgb=(0.18, 0.42, 0.62), emission_strength=0.16)
    m_desk = make_mat("DeskTop", (1.0, 1.0, 1.0, 1.0), metallic=0.10, roughness=0.34, image=tex_desk)
    m_legs = make_mat("DarkMetal", (1.0, 1.0, 1.0, 1.0), metallic=0.82, roughness=0.28, image=tex_metal)
    m_pc = make_mat("PcCase", (1.0, 1.0, 1.0, 1.0), metallic=0.68, roughness=0.30, image=tex_pc)
    m_code1 = make_mat("MonitorCode1", (1.0, 1.0, 1.0, 1.0), roughness=0.12, image=tex_code1, emission_strength=0.80)
    m_code2 = make_mat("MonitorCode2", (1.0, 1.0, 1.0, 1.0), roughness=0.12, image=tex_code2, emission_strength=0.80)
    m_chair = make_mat("ChairMesh", (1.0, 1.0, 1.0, 1.0), roughness=0.52, image=tex_chair)
    m_folder_r = make_mat("RedFolder", (1.0, 1.0, 1.0, 1.0), roughness=0.42, image=tex_red)
    m_sticky = make_mat("StickyPaper", (1.0, 1.0, 1.0, 1.0), roughness=0.62, image=tex_sticky)
    m_rgb_strip = make_mat("LedCyan", (0.14, 0.88, 1.0, 1.0), roughness=0.12, emission_rgb=(0.14, 0.88, 1.0), emission_strength=1.8)

    # --------------------------------------------------------------------------
    # 1) L-Shaped Acoustic Cubicle Partition Wall with Aluminum Extrusion Frame & Frosted Glass Transom
    # --------------------------------------------------------------------------
    add_beveled_box("BackPartitionPanel", (0.0, -0.58, 0.55), (2.23, 0.088, 1.08), m_part, bevel=0.016, segments=3)
    # Acoustic horizontal pinboard tack grooves on the back partition
    for gz in (0.36, 0.62, 0.88):
        add_beveled_box(f"BackPartitionSeam_{gz}", (0.0, -0.533, gz), (2.16, 0.008, 0.010), m_legs, bevel=0.002)
    add_beveled_box("BackPartitionRailBot", (0.0, -0.58, 0.022), (2.25, 0.098, 0.040), m_legs, bevel=0.006)
    add_beveled_box("BackPartitionRailMid", (0.0, -0.58, 1.10), (2.25, 0.10, 0.036), m_legs, bevel=0.006)
    add_beveled_box("BackPartitionRailTop", (0.0, -0.58, 1.445), (2.25, 0.092, 0.030), m_legs, bevel=0.006)
    add_beveled_box("BackFrostedGlass", (0.0, -0.58, 1.28), (2.20, 0.036, 0.34), m_glass, bevel=0.005)
    for gx in (-1.11, -0.37, 0.37, 1.11):
        add_beveled_box(f"GlassMullion_{gx}", (gx, -0.58, 1.28), (0.034, 0.086, 0.36), m_legs, bevel=0.005)

    # Left return partition wall (X = -1.08, Y in [-0.62, +0.52], Z up to 1.11)
    add_beveled_box("SidePartitionPanel", (-1.08, -0.05, 0.55), (0.088, 1.13, 1.08), m_part, bevel=0.016, segments=3)
    add_beveled_box("SidePartitionBase", (-1.08, -0.05, 0.022), (0.098, 1.15, 0.040), m_legs, bevel=0.006)
    add_beveled_box("SidePartitionCap", (-1.08, -0.05, 1.095), (0.10, 1.15, 0.032), m_legs, bevel=0.006)
    add_beveled_box("SidePartitionFrontPost", (-1.08, 0.51, 0.55), (0.096, 0.038, 1.09), m_legs, bevel=0.006)

    # Pinned Kanban / architectural diagram note cards on the inside of the partition wall (below Z=0.89 so not in screen shader box!)
    for nx, nz, n_mat, n_rot in (
        (-0.88, 0.87, m_sticky, (0.0, -0.05, 0.0)),
        (-0.77, 0.86, m_folder_r, (0.0, 0.04, 0.0)),
        (-0.98, 0.85, m_sticky, (0.0, 0.08, 0.0)),
    ):
        add_beveled_box(f"PartitionPinNote_{nx}", (nx, -0.531, nz), (0.075, 0.006, 0.075), n_mat, bevel=0.001, rot=n_rot)

    # --------------------------------------------------------------------------
    # 2) Beveled Warm-Oak Desktop (Top Surface at exact Z_bl = 0.792m!), Stitched Deskmat,
    #    Dual Cable Grommets, Cantilever Steel Frame Legs & Under-Desk Tangled Power Strip
    # --------------------------------------------------------------------------
    add_beveled_box("DeskTopSlab", (0.0, 0.0, 0.761), (2.10, 1.00, 0.062), m_desk, bevel=0.018, segments=4)
    add_beveled_box("DeskEdgeTrim", (0.0, 0.0, 0.742), (2.06, 0.96, 0.022), m_legs, bevel=0.005)
    # Extended stitched slate deskmat with wrist pad
    add_beveled_box("DeskPadMat", (-0.06, 0.14, 0.793), (0.92, 0.40, 0.006), m_pc, bevel=0.0025)
    add_beveled_box("DeskPadStitchRim", (-0.06, 0.14, 0.793), (0.926, 0.406, 0.004), m_legs, bevel=0.002)

    # Dual brushed aluminum cable grommets with flip covers
    for gx in (-0.65, 0.52):
        add_smooth_cyl(f"CableGrommetRim_{gx}", (gx, -0.34, 0.792), 0.038, 0.038, 0.008, m_legs, verts=24, bevel=0.002)
        add_smooth_cyl(f"CableGrommetHole_{gx}", (gx, -0.34, 0.794), 0.028, 0.028, 0.006, m_pc, verts=20, bevel=0.001)

    # Heavy-duty cantilever A-frame steel legs + leveling glides + structural modesty crossbar
    for lx in (-0.95, 0.95):
        add_beveled_box(f"LegFootBar_{lx}", (lx, 0.0, 0.026), (0.078, 0.92, 0.046), m_legs, bevel=0.008)
        for fy in (-0.40, 0.40):
            add_smooth_cyl(f"LegGlidePad_{lx}_{fy}", (lx, fy, 0.008), 0.032, 0.028, 0.016, m_pc, verts=18, bevel=0.002)
        for ly in (-0.42, 0.42):
            add_smooth_cyl(f"LegColumn_{lx}_{ly}", (lx, ly, 0.38), 0.034, 0.034, 0.71, m_legs, verts=22, bevel=0.004)
        add_beveled_box(f"LegTopBracket_{lx}", (lx, 0.0, 0.715), (0.082, 0.88, 0.042), m_legs, bevel=0.006)
    add_beveled_box("UnderDeskBeam", (0.0, -0.25, 0.70), (1.92, 0.085, 0.056), m_legs, bevel=0.006)

    # Under-desk perforated cable management basket & floor surge-protector power strip with tangled cables!
    add_beveled_box("UnderDeskCableTray", (-0.15, -0.36, 0.64), (1.28, 0.14, 0.045), m_legs, bevel=0.005)
    add_beveled_box("UnderDeskPowerBrick", (-0.38, -0.36, 0.672), (0.16, 0.075, 0.038), m_pc, bevel=0.004, rot=(0, 0, 0.18))
    # Tangled surge protector power strip on floor under the desk (`Y_bl = -0.26`, safely outside chair shader region `Y_bl > +0.405`)
    add_beveled_box("FloorPowerStrip", (-0.34, -0.26, 0.024), (0.34, 0.078, 0.038), m_pc, bevel=0.006, rot=(0, 0, -0.24))
    add_beveled_box("PowerStripSwitch", (-0.21, -0.23, 0.044), (0.026, 0.018, 0.008), m_folder_r, bevel=0.002, rot=(0, 0, -0.24))
    for p_i in range(4):
        px = -0.44 + p_i * 0.055
        py = -0.285 + p_i * 0.014
        add_beveled_box(f"PowerPlugAdapter_{p_i}", (px, py, 0.052), (0.034, 0.038, 0.032), m_legs if p_i % 2 == 0 else m_pc, bevel=0.004, rot=(0, 0, -0.24))

    # Tangled 3D power & display cables snaking from grommet/monitors down to under-desk tray and floor power strip
    add_tube_curve("FloorPowerCord1", [
        ((-0.65, -0.34, 0.78), 0.007),
        ((-0.68, -0.38, 0.56), 0.007),
        ((-0.58, -0.34, 0.24), 0.007),
        ((-0.46, -0.29, 0.035), 0.007),
    ], m_pc, sides=10, steps_per_span=4)
    add_tube_curve("FloorPowerCord2", [
        ((0.76, -0.22, 0.78), 0.007),
        ((0.48, -0.36, 0.65), 0.007),
        ((0.05, -0.36, 0.65), 0.007),
        ((-0.24, -0.34, 0.34), 0.007),
        ((-0.22, -0.24, 0.035), 0.007),
    ], m_pc, sides=10, steps_per_span=4)

    # --------------------------------------------------------------------------
    # 3) Dual Thin-Bezel Curved Monitors on Articulated Desk-Clamp VESA Arms
    #    CRITICAL SHADER RULE (`VisualEngine3D.cs:37-39`):
    #      `bool screen = p.y > 0.897 && abs(p.x) < 0.81 && abs(p.z - 0.24) < 0.18;`
    #      Pivots: Left = (-0.42, -0.25), Right = (+0.34, -0.25) in Blender `(X, Y)`.
    #      Keep stationary desk clamp & lower arm at `Z_bl <= 0.894`, and rotating VESA head + monitor above `Z_bl >= 0.899`!
    # --------------------------------------------------------------------------
    for idx, (mx, rot_z, m_scr) in enumerate(((-0.42, -0.18, m_code1), (0.34, 0.18, m_code2))):
        # Heavy C-clamp desk mount at rear desk edge (`Y_bl = -0.46`, `Z_bl <= 0.892` -> stationary)
        clamp_x = mx - 0.08 if idx == 0 else mx + 0.08
        add_beveled_box(f"VesaDeskClamp_{idx}", (clamp_x, -0.46, 0.775), (0.095, 0.095, 0.095), m_legs, bevel=0.008)
        add_smooth_cyl(f"VesaLowerPillar_{idx}", (clamp_x, -0.46, 0.840), 0.022, 0.024, 0.092, m_legs, verts=20, bevel=0.003)
        # Articulated lower swing arm reaching from clamp `(clamp_x, -0.46, 0.875)` to pivot `(mx, -0.25, 0.885)` (stays `<= 0.895`!)
        mid_ax = 0.5 * (clamp_x + mx)
        arm_ang = math.atan2(mx - clamp_x, -0.25 - (-0.46))
        add_beveled_box(f"VesaSwingArm_{idx}", (mid_ax, -0.355, 0.876), (0.038, 0.23, 0.026), m_legs, bevel=0.005, rot=(0, 0, -arm_ang))
        add_smooth_cyl(f"VesaPivotSocket_{idx}", (mx, -0.25, 0.882), 0.026, 0.028, 0.024, m_pc, verts=20, bevel=0.002)

        # Rotating upper VESA knuckle & monitor chassis (`Z_bl >= 0.900`, centered around pivot `(mx, -0.25)`)
        add_smooth_cyl(f"VesaUpperNeck_{idx}", (mx, -0.25, 0.985), 0.020, 0.022, 0.165, m_legs, verts=18, bevel=0.002)
        add_beveled_box(f"VesaMountPlate_{idx}", (mx, -0.245, 1.11), (0.11, 0.022, 0.11), m_legs, bevel=0.004, rot=(0, 0, rot_z))
        add_beveled_box(f"MonChassis_{idx}", (mx, -0.24, 1.125), (0.72, 0.036, 0.435), m_pc, bevel=0.012, segments=3, rot=(0, 0, rot_z))
        add_beveled_box(f"MonRearSpine_{idx}", (mx, -0.258, 1.125), (0.28, 0.018, 0.18), m_legs, bevel=0.006, rot=(0, 0, rot_z))

        # Display panel built at origin for exact 0..1 planar screen UVs, then rotated & placed at `(mx, -0.219, 1.128)`
        disp = add_beveled_box(f"MonDisplay_{idx}", (0.0, 0.0, 0.0), (0.686, 0.010, 0.398), m_scr, bevel=0.002)
        uv_d = disp.data.uv_layers.active
        for poly in disp.data.polygons:
            for loop_idx in poly.loop_indices:
                v = disp.data.vertices[disp.data.loops[loop_idx].vertex_index].co
                u = max(0.0, min(1.0, 0.5 - v.x / 0.686))
                w_uv = max(0.0, min(1.0, 0.5 + v.z / 0.398))
                uv_d.data[loop_idx].uv = (u, w_uv)
        disp.rotation_euler = (0.0, 0.0, rot_z)
        disp.location = (mx, -0.219, 1.128)
        bpy.ops.object.select_all(action="DESELECT")
        disp.select_set(True)
        bpy.context.view_layer.objects.active = disp
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

        add_beveled_box(f"MonChin_{idx}", (mx, -0.218, 0.916), (0.71, 0.014, 0.022), m_legs, bevel=0.003, rot=(0, 0, rot_z))

        # Sticky notes stuck to the bottom/side monitor bezel (inside the rotating screen volume so they turn with the monitor!)
        sn_local_x = -0.29 if idx == 0 else 0.29
        sn_wx = mx + sn_local_x * math.cos(rot_z)
        sn_wy = -0.212 + sn_local_x * math.sin(rot_z)
        add_beveled_box(f"MonitorStickyNoteA_{idx}", (sn_wx, sn_wy, 0.932), (0.056, 0.004, 0.056), m_sticky, bevel=0.001, rot=(0.08, 0.05, rot_z))
        if idx == 0:
            add_beveled_box("MonitorStickyNoteB", (sn_wx + 0.062 * math.cos(rot_z), sn_wy + 0.062 * math.sin(rot_z), 0.928), (0.054, 0.004, 0.054), m_folder_r, bevel=0.001, rot=(0.06, -0.06, rot_z))
        else:
            # 4K Webcam perched on top of the right monitor bezel
            add_beveled_box("MonitorWebcamBody", (mx, -0.218, 1.350), (0.092, 0.032, 0.026), m_pc, bevel=0.005, rot=(0, 0, rot_z))
            add_smooth_cyl("MonitorWebcamLens", (mx, -0.200, 1.350), 0.009, 0.009, 0.008, m_glass, rot=(math.pi / 2, 0, rot_z), verts=16, bevel=0.001)

    # --------------------------------------------------------------------------
    # 4) Sculpted Mechanical Keyboard with Individual Keycap Rows & RGB Underglow Strip,
    #    Contoured Ergonomic Mouse, Over-Ear Headphones on Hook, Open Notebook & Pen, Mug & PC Tower
    # --------------------------------------------------------------------------
    kb_x, kb_y = -0.08, 0.13
    add_beveled_box("KeyboardCase", (kb_x, kb_y, 0.805), (0.43, 0.162, 0.022), m_pc, bevel=0.005, segments=3)
    # Glowing cyan RGB underglow lightbar along keyboard front & side perimeter
    add_beveled_box("KeyboardRgbUnderglow", (kb_x, kb_y, 0.797), (0.436, 0.168, 0.005), m_rgb_strip, bevel=0.001)
    add_beveled_box("KeyboardWristRest", (kb_x, kb_y + 0.118, 0.800), (0.43, 0.068, 0.014), m_chair, bevel=0.004)

    # Sculpted individual keycap clusters & rows across 5 rows (ESC crimson accent, alphas, spacebar, enter, arrows)
    row_y_offsets = (-0.056, -0.028, 0.000, 0.028, 0.056)
    for r_i, dy in enumerate(row_y_offsets):
        ry = kb_y + dy
        rz = 0.820 - r_i * 0.0008
        if r_i == 0:
            # Function row: crimson ESC key + 3 F-key groups
            add_beveled_box("KeyCap_ESC", (kb_x - 0.188, ry, rz), (0.024, 0.020, 0.011), m_folder_r, bevel=0.002)
            for g_i, gx_off in enumerate((-0.115, 0.005, 0.125)):
                for k_i in range(4):
                    kx = kb_x + gx_off + (k_i - 1.5) * 0.026
                    add_beveled_box(f"KeyCap_F_{g_i}_{k_i}", (kx, ry, rz), (0.022, 0.020, 0.011), m_legs, bevel=0.002)
        elif r_i == 4:
            # Bottom modifier row + sculpted convex Spacebar + arrow cluster
            for kx_off, kw, k_m in (
                (-0.182, 0.032, m_legs),
                (-0.144, 0.030, m_legs),
                (-0.108, 0.030, m_legs),
                (-0.022, 0.132, m_legs),  # Spacebar
                ( 0.068, 0.030, m_legs),
                ( 0.104, 0.030, m_legs),
                ( 0.146, 0.022, m_folder_r),  # Left arrow
                ( 0.171, 0.022, m_folder_r),  # Up/Down arrow
                ( 0.196, 0.022, m_folder_r),  # Right arrow
            ):
                add_beveled_box(f"KeyCap_Bot_{kx_off}", (kb_x + kx_off, ry, rz), (kw, 0.021, 0.011), k_m, bevel=0.002)
        else:
            # 14 individual sculpted keycaps per alphanumeric row + crimson Enter key on row 2
            for k_i in range(14):
                kx = kb_x - 0.185 + k_i * 0.0285
                is_enter = (r_i == 2 and k_i == 13)
                kw = 0.034 if (k_i in (0, 13)) else 0.023
                add_beveled_box(
                    f"KeyCap_R{r_i}_{k_i}",
                    (kx, ry, rz),
                    (kw, 0.020, 0.011),
                    m_folder_r if is_enter else m_legs,
                    bevel=0.002,
                )

    # Coiled aviator USB-C keyboard cable leading to grommet
    add_tube_curve("KeyboardAviatorCable", [
        ((kb_x - 0.12, kb_y - 0.082, 0.802), 0.005),
        ((-0.34, -0.06, 0.802), 0.009),
        ((-0.52, -0.18, 0.800), 0.009),
        ((-0.65, -0.34, 0.794), 0.005),
    ], m_legs, sides=10, steps_per_span=3)

    # Contoured Ergonomic Wireless Mouse on Deskmat
    add_pillow("ErgonomicMouse", (0.26, 0.13, 0.808), (0.072, 0.118, 0.034), m_pc, crown=0.22, uv_scale=2.0)
    add_smooth_cyl("MouseWheel", (0.26, 0.095, 0.825), 0.008, 0.008, 0.006, m_legs, rot=(0, math.pi / 2, 0), verts=14, bevel=0.001)
    add_beveled_box("MouseThumbRest", (0.222, 0.135, 0.800), (0.018, 0.072, 0.008), m_legs, bevel=0.003)

    # Ceramic Espresso Mug with Handle & Coaster
    add_smooth_cyl("MugCoaster", (-0.44, 0.20, 0.794), 0.052, 0.052, 0.005, m_legs, verts=24, bevel=0.001)
    add_smooth_cyl("CoffeeMugBody", (-0.44, 0.20, 0.844), 0.038, 0.042, 0.096, m_folder_r, verts=24, bevel=0.003)
    add_smooth_cyl("CoffeeMugInner", (-0.44, 0.20, 0.888), 0.035, 0.035, 0.008, m_pc, verts=20, bevel=0.001)
    add_beveled_box("CoffeeMugHandle", (-0.485, 0.20, 0.844), (0.022, 0.012, 0.058), m_folder_r, bevel=0.004)

    # Open Developer Notebook & Stylus Pen on Right Side of Deskmat (safely outside PodPizza at X=+0.56!)
    nb_x, nb_y = 0.44, 0.22
    add_beveled_box("NotebookCoverLeft", (nb_x - 0.075, nb_y, 0.796), (0.145, 0.205, 0.008), m_pc, bevel=0.002, rot=(0, 0, -0.10))
    add_beveled_box("NotebookPageLeft", (nb_x - 0.073, nb_y, 0.801), (0.138, 0.196, 0.006), m_sticky, bevel=0.002, rot=(0, 0, -0.10))
    add_beveled_box("NotebookPageRight", (nb_x + 0.073, nb_y - 0.014, 0.801), (0.138, 0.196, 0.006), m_sticky, bevel=0.002, rot=(0, 0, -0.10))
    add_smooth_cyl("NotebookSpiralSpine", (nb_x, nb_y - 0.007, 0.804), 0.007, 0.007, 0.198, m_legs, rot=(math.pi / 2, 0, -0.10), verts=14, bevel=0.001)
    add_smooth_cyl("ArchitectPen", (nb_x + 0.05, nb_y + 0.02, 0.808), 0.0055, 0.0045, 0.145, m_folder_r, rot=(math.pi / 2, 0, 0.45), verts=14, bevel=0.001)

    # Studio Over-Ear Headphones Hanging on Under-Desk Front-Left Hook (`X = -0.96, Y = 0.34, Z = 0.62..0.78`)
    hp_x, hp_y = -0.96, 0.34
    add_beveled_box("HeadphoneDeskHook", (hp_x, hp_y, 0.745), (0.045, 0.075, 0.028), m_legs, bevel=0.004)
    add_tube_curve("HeadphoneHeadband", [
        ((hp_x, hp_y - 0.070, 0.635), 0.013),
        ((hp_x, hp_y - 0.055, 0.715), 0.014),
        ((hp_x, hp_y,         0.748), 0.015),
        ((hp_x, hp_y + 0.055, 0.715), 0.014),
        ((hp_x, hp_y + 0.070, 0.635), 0.013),
    ], m_pc, sides=12, steps_per_span=4)
    for ear_s in (-1, 1):
        ey = hp_y + ear_s * 0.072
        add_smooth_cyl(f"HeadphoneCup_{ear_s}", (hp_x, ey, 0.620), 0.046, 0.042, 0.032, m_legs, rot=(math.pi / 2, 0, 0), verts=22, bevel=0.004)
        add_smooth_cyl(f"HeadphonePad_{ear_s}", (hp_x, ey - ear_s * 0.016, 0.620), 0.046, 0.046, 0.016, m_chair, rot=(math.pi / 2, 0, 0), verts=22, bevel=0.003)

    # High-Craft Workstation PC Tower on Right Rear Corner (`X = 0.82, Y = -0.14, Z = 0.792..1.25`)
    add_beveled_box("PcChassis", (0.82, -0.14, 1.022), (0.24, 0.46, 0.46), m_pc, bevel=0.016, segments=3)
    add_beveled_box("PcFrontBezel", (0.82, 0.092, 1.022), (0.215, 0.016, 0.425), m_legs, bevel=0.005)
    for lv in range(9):
        lz = 0.845 + lv * 0.038
        add_beveled_box(f"PcFrontMeshSlat_{lv}", (0.82, 0.100, lz), (0.178, 0.006, 0.016), m_pc, bevel=0.0015)
    # Tempered glass side window + internal glowing GPU bar & CPU cooler ring
    add_beveled_box("PcSideGlassFrame", (0.700, -0.14, 1.022), (0.012, 0.39, 0.38), m_legs, bevel=0.004)
    add_beveled_box("PcGpuGlowBar", (0.718, -0.14, 0.98), (0.018, 0.26, 0.024), m_rgb_strip, bevel=0.003)
    add_smooth_cyl("PcCpuAioCooler", (0.718, -0.09, 1.10), 0.044, 0.044, 0.018, m_glass, rot=(0, math.pi / 2, 0), verts=24, bevel=0.002)
    add_smooth_cyl("PcPowerLed", (0.82, 0.102, 1.20), 0.008, 0.008, 0.006, m_rgb_strip, rot=(math.pi / 2, 0, 0), verts=16, bevel=0.001)

    # Stacked Project Binders on Left Rear Corner (`X = -0.84, Y = -0.18` -> leaves `X = -0.56, Y = -0.06` clear for PodPizza!)
    add_beveled_box("FolderRed", (-0.84, -0.18, 0.810), (0.25, 0.32, 0.036), m_folder_r, bevel=0.006, rot=(0, 0, 0.15))
    add_beveled_box("FolderDark", (-0.82, -0.20, 0.844), (0.24, 0.31, 0.028), m_legs, bevel=0.005, rot=(0, 0, -0.06))

    # --------------------------------------------------------------------------
    # 5) Executive Ergonomic Mesh Office Chair
    #    CRITICAL SHADER RULE (`VisualEngine3D.cs:36`):
    #      `bool chair = p.z < -0.405 && abs(p.x) < 0.38 && (p.y < 0.70 || p.z < -0.77);`
    #      In Blender: ALL chair vertices MUST satisfy `Y_bl > +0.405` and `abs(X_bl) < 0.375`,
    #      and any vertex with `Z_bl >= 0.70` MUST have `Y_bl > +0.775`!
    #      Chair center pivot is at `(0.0, +0.72)`.
    # --------------------------------------------------------------------------
    cx_c, cy_c = 0.0, 0.72
    add_smooth_cyl("ChairCenterHub", (cx_c, cy_c, 0.085), 0.044, 0.048, 0.065, m_legs, verts=22, bevel=0.004)
    add_smooth_cyl("ChairGasCylinder", (cx_c, cy_c, 0.265), 0.028, 0.032, 0.36, m_legs, verts=22, bevel=0.002)
    add_smooth_cyl("ChairTelescopicShroud", (cx_c, cy_c, 0.18), 0.038, 0.042, 0.16, m_pc, verts=22, bevel=0.003)

    # 5-star contoured die-cast aluminum base + twin-wheel hooded casters (radius 0.25m -> Y_bl >= 0.445 > 0.405!)
    for k in range(5):
        ang = k * math.tau / 5.0 + math.pi / 5.0
        dx, dy = math.cos(ang), math.sin(ang)
        add_beveled_box(
            f"ChairStarArm_{k}",
            (cx_c + dx * 0.13, cy_c + dy * 0.13, 0.068),
            (0.044, 0.25, 0.032),
            m_legs,
            bevel=0.007,
            rot=(0, 0, ang - math.pi / 2),
        )
        wx, wy = cx_c + dx * 0.245, cy_c + dy * 0.245
        add_smooth_cyl(f"ChairCasterStem_{k}", (wx, wy, 0.046), 0.012, 0.014, 0.026, m_legs, verts=14, bevel=0.001)
        add_smooth_cyl(f"ChairCasterWheel_{k}", (wx, wy, 0.024), 0.024, 0.024, 0.028, m_pc, rot=(0, math.pi / 2, ang), verts=18, bevel=0.003)

    # Under-seat synchro-tilt mechanism housing & tension knob + adjustment paddles
    add_beveled_box("ChairTiltMechanism", (cx_c, cy_c + 0.02, 0.425), (0.22, 0.24, 0.055), m_pc, bevel=0.008)
    add_smooth_cyl("ChairTensionKnob", (cx_c, cy_c - 0.08, 0.395), 0.026, 0.022, 0.045, m_pc, rot=(0.45, 0, 0), verts=18, bevel=0.003)
    add_tube_curve("ChairHeightLever", [
        ((0.10, cy_c, 0.425), 0.007),
        ((0.22, cy_c - 0.02, 0.418), 0.008),
        ((0.26, cy_c - 0.03, 0.415), 0.012),
    ], m_legs, sides=10, steps_per_span=2)

    # Sculpted Waterfall Seat Cushion (`Z_bl = 0.44..0.53 < 0.70`, `Y_bl = 0.47..0.97 > 0.405`, `abs(X) <= 0.26 < 0.38`)
    add_pillow("ChairSeatCushion", (cx_c, cy_c, 0.485), (0.50, 0.47, 0.088), m_chair, crown=0.18, uv_scale=1.6)
    add_beveled_box("ChairSeatPanShell", (cx_c, cy_c, 0.452), (0.48, 0.45, 0.032), m_pc, bevel=0.008)

    # 3D Adjustable Armrests (`Z_bl <= 0.675 < 0.70`, `abs(X) <= 0.31 < 0.38`, `Y_bl >= 0.58 > 0.405`)
    for side in (-1, 1):
        ax = side * 0.265
        add_beveled_box(f"ChairArmBracket_{side}", (side * 0.21, cy_c + 0.02, 0.455), (0.11, 0.055, 0.028), m_legs, bevel=0.005)
        add_beveled_box(f"ChairArmPost_{side}", (ax, cy_c + 0.02, 0.555), (0.036, 0.058, 0.185), m_legs, bevel=0.006)
        add_pillow(f"ChairArmPad_{side}", (ax, cy_c + 0.01, 0.656), (0.072, 0.23, 0.030), m_pc, crown=0.12, uv_scale=2.0)

    # Curved S-Spine Exoskeleton Backrest Frame, Breathable Mesh Back, Adjustable Lumbar Support & Contoured Headrest
    # (`Z_bl >= 0.70` -> all vertices strictly at `Y_bl >= 0.84 > 0.77`!)
    add_pillow("ChairMeshBackrest", (cx_c, 0.935, 0.825), (0.46, 0.072, 0.55), m_chair, crown=0.12, rot=(-0.06, 0, 0), uv_scale=1.6)
    add_beveled_box("ChairOuterBackRim", (cx_c, 0.948, 0.825), (0.485, 0.038, 0.575), m_pc, bevel=0.010, rot=(-0.06, 0, 0))
    add_tube_curve("ChairSpineExoskeleton", [
        ((0.0, 0.82, 0.445), 0.024),
        ((0.0, 0.94, 0.560), 0.026),
        ((0.0, 0.985, 0.760), 0.026),
        ((0.0, 0.975, 0.980), 0.022),
        ((0.0, 0.960, 1.120), 0.018),
    ], m_legs, sides=14, steps_per_span=4)
    add_pillow("ChairLumbarSupport", (cx_c, 0.932, 0.685), (0.41, 0.048, 0.115), m_pc, crown=0.16, uv_scale=1.8)
    add_beveled_box("ChairLumbarWing", (cx_c, 0.968, 0.685), (0.43, 0.026, 0.042), m_legs, bevel=0.005)
    add_pillow("ChairHeadrest", (cx_c, 0.945, 1.155), (0.28, 0.075, 0.135), m_chair, crown=0.18, rot=(0.10, 0, 0), uv_scale=1.8)

    bake_24ray_ao_and_export(
        "prop_workstation_full.glb",
        preserve_uv_materials=("MonitorCode1", "MonitorCode2"),
        ray_dist=0.38,
    )


# ==============================================================================
# 2. `prop_server_rack.glb` — Perforated Steel 42U Enterprise IT Server Rack
#    Exact Blender BBox: X in [-0.47, +0.47], Y in [-0.4575, +0.4575], Z in [0.0, 2.30]
#    Preserves exact animated LED material names (`LedGreen`, `LedCyan`, `LedAmber`)
# ==============================================================================
def build_prop_server_rack():
    clear_scene()
    tex_frame = make_ao_Lookup_texture("RackAnthraciteFrameTex", (0.056, 0.068, 0.088), style="metal", edge_boost=0.58)
    tex_perf = make_ao_Lookup_texture("RackPerforatedMeshTex", (0.044, 0.054, 0.072), style="mesh_perf", edge_boost=0.52)
    tex_blade = make_ao_Lookup_texture("RackServerBladeTex", (0.085, 0.098, 0.125), style="metal", edge_boost=0.65)
    tex_brass = make_ao_Lookup_texture("RackBrushedBrassTex", (0.52, 0.36, 0.14), style="brass", edge_boost=0.55)
    tex_cable_b = make_ao_Lookup_texture("RackCableCobaltTex", (0.055, 0.28, 0.68), style="fabric", edge_boost=0.35)
    tex_cable_r = make_ao_Lookup_texture("RackCableCrimsonTex", (0.62, 0.075, 0.095), style="fabric", edge_boost=0.35)

    m_frame = make_mat("RackFrame", (1.0, 1.0, 1.0, 1.0), metallic=0.80, roughness=0.30, image=tex_frame)
    m_perf = make_mat("RackPerforatedMesh", (1.0, 1.0, 1.0, 1.0), metallic=0.76, roughness=0.36, image=tex_perf)
    m_blade = make_mat("RackBlade", (1.0, 1.0, 1.0, 1.0), metallic=0.72, roughness=0.28, image=tex_blade)
    m_trim = make_mat("RackTrimBrass", (1.0, 1.0, 1.0, 1.0), metallic=0.88, roughness=0.24, image=tex_brass)
    m_cable = make_mat("RackBraidedCable", (1.0, 1.0, 1.0, 1.0), metallic=0.15, roughness=0.44, image=tex_cable_b)
    m_cable_r = make_mat("RackCrimsonCable", (1.0, 1.0, 1.0, 1.0), metallic=0.15, roughness=0.44, image=tex_cable_r)
    m_led_g = make_mat("LedGreen", (0.18, 0.98, 0.52, 1.0), roughness=0.10, emission_rgb=(0.18, 0.98, 0.52), emission_strength=2.2)
    m_led_c = make_mat("LedCyan", (0.14, 0.88, 1.00, 1.0), roughness=0.10, emission_rgb=(0.14, 0.88, 1.00), emission_strength=2.2)
    m_led_a = make_mat("LedAmber", (1.00, 0.68, 0.16, 1.0), roughness=0.10, emission_rgb=(1.00, 0.68, 0.16), emission_strength=2.2)

    # 1) Rolling Caster Wheels + Heavy Beveled Plinth Base + Roof Cooling Manifold (Z in [0.0, 2.30])
    for sx in (-1, 1):
        for sy in (-1, 1):
            wx, wy = sx * 0.38, sy * 0.36
            add_beveled_box(f"CasterPlate_{sx}_{sy}", (wx, wy, 0.048), (0.085, 0.085, 0.016), m_trim, bevel=0.003)
            add_smooth_cyl(f"CasterWheel_{sx}_{sy}", (wx, wy, 0.022), 0.022, 0.022, 0.032, m_blade, rot=(0, math.pi / 2, 0), verts=18, bevel=0.003)
            add_smooth_cyl(f"LevelingFoot_{sx}_{sy}", (sx * 0.42, sy * 0.40, 0.018), 0.018, 0.014, 0.036, m_trim, verts=16, bevel=0.002)

    add_beveled_box("BasePlinth", (0.0, 0.0, 0.068), (0.94, 0.90, 0.064), m_frame, bevel=0.014, segments=3)
    add_beveled_box("TopManifold", (0.0, 0.0, 2.248), (0.94, 0.90, 0.096), m_frame, bevel=0.016, segments=3)
    add_beveled_box("InnerShadowCore", (0.0, 0.0, 1.155), (0.82, 0.76, 2.12), m_frame, bevel=0.012)

    # Top exhaust fan housings with 3D fan hubs, radial blades & protective brass ring grilles (Z up to 2.300m)
    for fx in (-0.23, 0.23):
        add_smooth_cyl(f"FanShroud_{fx}", (fx, 0.0, 2.290), 0.165, 0.155, 0.020, m_trim, verts=28, bevel=0.003)
        add_smooth_cyl(f"FanWell_{fx}", (fx, 0.0, 2.292), 0.140, 0.140, 0.018, m_frame, verts=24, bevel=0.002)
        add_smooth_cyl(f"FanMotorHub_{fx}", (fx, 0.0, 2.294), 0.042, 0.038, 0.014, m_blade, verts=18, bevel=0.002)
        for b_i in range(7):
            ba = b_i * math.tau / 7.0
            bx = fx + 0.086 * math.cos(ba)
            by = 0.086 * math.sin(ba)
            add_beveled_box(f"FanBlade_{fx}_{b_i}", (bx, by, 2.293), (0.082, 0.024, 0.006), m_blade, bevel=0.0015, rot=(0.35, 0, ba))

    # 2) Four Heavy 42U Corner Pillars & Framed Perforated Honeycomb-Mesh Side Panels with Cooling Louvres
    for sx in (-1, 1):
        for sy in (-1, 1):
            add_beveled_box(f"CornerPillar_{sx}_{sy}", (sx * 0.435, sy * 0.415, 1.155), (0.070, 0.070, 2.12), m_frame, bevel=0.012, segments=3)
        # Upper and lower removable perforated steel side doors
        for half_i, hz in enumerate((0.64, 1.67)):
            add_beveled_box(f"SideDoorFrame_{sx}_{half_i}", (sx * 0.452, 0.0, hz), (0.026, 0.74, 0.99), m_frame, bevel=0.007)
            add_beveled_box(f"SidePerfMesh_{sx}_{half_i}", (sx * 0.458, 0.0, hz), (0.018, 0.65, 0.89), m_perf, bevel=0.004, uv_scale=3.8)
            add_beveled_box(f"SideLatch_{sx}_{half_i}", (sx * 0.464, 0.0, hz + 0.39), (0.012, 0.085, 0.038), m_trim, bevel=0.003)
            for lv in range(4):
                lz = hz - 0.27 + lv * 0.18
                add_beveled_box(f"SideLouverBar_{sx}_{half_i}_{lv}", (sx * 0.466, 0.0, lz), (0.008, 0.56, 0.024), m_trim, bevel=0.002)

    # 3) Front & Rear 42U Equipment Bays: Recessed 2U Server Blades, Patch Panels, NVMe Drive Bays,
    #    Cooling Fan Grilles, Glowing Status LEDs, and Colorful Curved 3D Ethernet Patch Cables!
    for face in (-1, 1):
        fy = face * 0.405
        # Vertical 42U EIA mounting rails & brushed cable management raceways with finger ducts
        for side in (-1, 1):
            add_beveled_box(f"CableRaceway_{face}_{side}", (side * 0.384, fy, 1.155), (0.046, 0.038, 2.04), m_frame, bevel=0.005)
            for ring_i in range(10):
                rz = 0.25 + ring_i * 0.20
                add_beveled_box(f"CableDuctRing_{face}_{side}_{ring_i}", (side * 0.384, face * 0.432, rz), (0.036, 0.024, 0.016), m_trim, bevel=0.003)

        # 9 rack-mounted 2U/1U enterprise blades & network switches
        for u in range(9):
            uz = 0.245 + u * 0.224
            is_switch = (u in (2, 6))
            # Recessed 2U server chassis faceplate with perforated intake grille & machined bezel
            add_beveled_box(f"BladeChassis_{face}_{u}", (0.0, fy, uz), (0.72, 0.038, 0.186), m_blade, bevel=0.006, segments=3)
            add_beveled_box(f"BladeIntakeMesh_{face}_{u}", (-0.04, face * 0.422, uz - 0.008), (0.58, 0.008, 0.125), m_perf, bevel=0.002, uv_scale=4.5)

            # Brushed brass rack-ear pull handles on both ends
            for side in (-1, 1):
                add_beveled_box(f"RackEarBracket_{face}_{u}_{side}", (side * 0.342, face * 0.426, uz), (0.024, 0.016, 0.155), m_frame, bevel=0.003)
                add_tube_curve(f"BladePullHandle_{face}_{u}_{side}", [
                    ((side * 0.342, face * 0.428, uz - 0.055), 0.007),
                    ((side * 0.342, face * 0.452, uz - 0.042), 0.007),
                    ((side * 0.342, face * 0.452, uz + 0.042), 0.007),
                    ((side * 0.342, face * 0.428, uz + 0.055), 0.007),
                ], m_trim, sides=10, steps_per_span=2)

            if is_switch:
                # 48-port Gigabit Patch Panel / Top-of-Rack Switch with RJ45 port banks & colorful curved 3D patch cords!
                for bank in range(3):
                    bx = -0.20 + bank * 0.16
                    add_beveled_box(f"SwitchPortBank_{face}_{u}_{bank}", (bx, face * 0.428, uz + 0.012), (0.135, 0.012, 0.062), m_frame, bevel=0.002)
                    add_beveled_box(f"SwitchPortLeds_{face}_{u}_{bank}", (bx, face * 0.434, uz + 0.050), (0.125, 0.006, 0.010), m_led_g if bank != 1 else m_led_c, bevel=0.001)
                # 5 colorful 3D curved Ethernet patch cables arching from switch ports into left/right vertical cable raceways
                for c_i, (px, c_mat, target_side) in enumerate((
                    (-0.24, m_led_c, -1),
                    (-0.15, m_cable, -1),
                    (-0.02, m_led_a,  1),
                    ( 0.11, m_cable_r, 1),
                    ( 0.22, m_cable,  1),
                )):
                    tx = target_side * 0.375
                    sag_z = uz - 0.055 - (c_i % 3) * 0.024
                    out_y = face * (0.452 + (c_i % 2) * 0.004)
                    add_tube_curve(f"SwitchPatchCord_{face}_{u}_{c_i}", [
                        ((px, face * 0.432, uz + 0.010), 0.0055),
                        ((0.5 * (px + tx), out_y, sag_z), 0.0058),
                        ((tx, face * 0.434, uz - 0.035), 0.0055),
                    ], c_mat, sides=10, steps_per_span=4)
            else:
                # 4 Hot-Swap NVMe / SAS Drive Caddies with release buttons & activity LEDs + 2 micro cooling fan grilles
                for bay in range(4):
                    bx = -0.22 + bay * 0.108
                    add_beveled_box(f"NvmeCaddy_{face}_{u}_{bay}", (bx, face * 0.428, uz - 0.010), (0.092, 0.012, 0.112), m_frame, bevel=0.0025)
                    add_beveled_box(f"NvmeReleaseBar_{face}_{u}_{bay}", (bx, face * 0.434, uz - 0.042), (0.068, 0.006, 0.014), m_trim if bay == 0 else m_blade, bevel=0.0015)
                for f_i in range(2):
                    fx_b = 0.19 + f_i * 0.088
                    add_smooth_cyl(f"BladeMiniFan_{face}_{u}_{f_i}", (fx_b, face * 0.429, uz - 0.010), 0.036, 0.034, 0.010, m_trim, rot=(math.pi / 2, 0, 0), verts=18, bevel=0.002)
                    add_smooth_cyl(f"BladeMiniFanCore_{face}_{u}_{f_i}", (fx_b, face * 0.431, uz - 0.010), 0.014, 0.014, 0.010, m_frame, rot=(math.pi / 2, 0, 0), verts=14, bevel=0.001)

                # Curved trunk patch cable connecting blade NIC to right cable raceway
                c_mat = m_cable if u % 3 == 0 else (m_led_c if u % 3 == 1 else m_cable_r)
                add_tube_curve(f"BladeNicCable_{face}_{u}", [
                    ((0.26, face * 0.430, uz - 0.015), 0.0062),
                    ((0.32, face * 0.452, uz - 0.052), 0.0065),
                    ((0.376, face * 0.432, uz - 0.040), 0.0062),
                ], c_mat, sides=10, steps_per_span=4)

            # Glowing LED status telemetry bar + individual node activity indicators (`LedGreen`, `LedCyan`, `LedAmber`)
            led_mat = m_led_g if u % 3 == 0 else (m_led_c if u % 3 == 1 else m_led_a)
            add_beveled_box(f"LedStrip_{face}_{u}", (-0.18, face * 0.434, uz + 0.065), (0.28, 0.010, 0.018), led_mat, bevel=0.002)
            for dot in range(4):
                d_mat = m_led_c if (u + dot) % 3 == 0 else (m_led_g if (u + dot) % 3 == 1 else m_led_a)
                add_beveled_box(f"LedDot_{face}_{u}_{dot}", (0.11 + dot * 0.042, face * 0.434, uz + 0.065), (0.020, 0.010, 0.016), d_mat, bevel=0.002)

        # Continuous vertical braided trunk cable bundles running inside both left & right raceways
        for side, t_mat in ((-1, m_cable), (1, m_cable_r)):
            add_tube_curve(f"VerticalTrunkBundle_{face}_{side}", [
                ((side * 0.368, face * 0.422, 0.18), 0.013),
                ((side * 0.368, face * 0.424, 0.85), 0.014),
                ((side * 0.368, face * 0.424, 1.52), 0.014),
                ((side * 0.368, face * 0.422, 2.12), 0.013),
            ], t_mat, sides=12, steps_per_span=3)

        # Perforated door stiles & brushed brass swing handle
        for side in (-1, 1):
            add_beveled_box(f"DoorStile_{face}_{side}", (side * 0.415, face * 0.445, 1.155), (0.032, 0.022, 2.08), m_frame, bevel=0.005)
        add_beveled_box(f"DoorHandle_{face}", (0.395, face * 0.452, 1.12), (0.022, 0.012, 0.24), m_trim, bevel=0.003)

    bake_24ray_ao_and_export(
        "prop_server_rack.glb",
        preserve_uv_materials=("LedGreen", "LedCyan", "LedAmber"),
        ray_dist=0.35,
    )


def render_proof_sheet():
    """Renders a high-resolution studio proof sheet of upgraded `prop_workstation_full.glb` (front & rear-3/4)
    and `prop_server_rack.glb` (front-3/4 & detail angle).
    """
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 860

    world = bpy.data.worlds.new("ProofStudio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.08, 0.10, 0.13, 1.0)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, -0.02))
    floor = bpy.context.active_object
    floor.scale = (8.5, 4.0, 0.04)
    m_floor = make_mat("StudioFloor", (0.10, 0.12, 0.15, 1.0), metallic=0.20, roughness=0.30)
    floor.data.materials.append(m_floor)

    placements = [
        ("prop_workstation_full.glb", -2.35, 0.88, math.radians(24)),
        ("prop_workstation_full.glb", -0.25, 0.88, math.radians(-145)),
        ("prop_server_rack.glb",       1.45, 0.88, math.radians(26)),
        ("prop_server_rack.glb",       2.65, 0.88, math.radians(-32)),
    ]
    for glb_name, dx, sc, rz in placements:
        before = set(bpy.context.scene.objects)
        bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / glb_name))
        imported = [o for o in (set(bpy.context.scene.objects) - before) if o.type == "MESH"]
        for o in imported:
            bpy.ops.object.select_all(action="DESELECT")
            o.select_set(True)
            bpy.context.view_layer.objects.active = o
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            bm = bmesh.new()
            bm.from_mesh(o.data)
            rot_m = Matrix.Rotation(rz, 4, "Z")
            for v in bm.verts:
                v.co = (rot_m @ (v.co * sc)) + V((dx, 0.0, 0.0))
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()

    cam_data = bpy.data.cameras.new("ProofCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 6.9
    cam = bpy.data.objects.new("ProofCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (-0.15, 4.8, 2.35)
    target = V((-0.15, 0.0, 0.88))
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (target - cam.location).to_track_quat("-Z", "Y")

    for loc, energy, col in [
        ((-2.5, 4.2, 3.6), 4.6, (1.0, 0.95, 0.86)),
        (( 2.8, 3.6, 2.8), 3.4, (0.82, 0.90, 1.0)),
        (( 0.0, -4.0, 2.8), 2.8, (1.0, 0.88, 0.74)),
    ]:
        ld = bpy.data.lights.new("Sun", type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new("Sun", ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_mode = "QUATERNION"
        lo.rotation_quaternion = (target - V(loc)).to_track_quat("-Z", "Y")

    out_local = SCREENSHOTS / "office_workstation_rack_proof.png"
    out_brain = COORD_BRAIN / "office_workstation_rack_proof.png"
    scene.render.filepath = str(out_local)
    bpy.ops.render.render(write_still=True)
    shutil.copy2(out_local, out_brain)
    print(f"RENDERED PROOF: {out_local} -> {out_brain}")


if __name__ == "__main__":
    build_prop_workstation_full()
    build_prop_server_rack()
    render_proof_sheet()
