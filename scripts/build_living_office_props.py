"""Blender 5.1 generator for Living & Chaotic Office Props Sprint (Task 1):
  1. assets/models/prop_pizza_stack.glb — Chaotic startup fuel pile for tables & desks
     - Exact footprint: ~0.52m wide (X) x 0.48m deep (Z_rl / Y_bl) x 0.24m tall (Y_rl / Z_bl)
     - Origin at bottom-center (Z_bl = 0 / Y_gltf = 0), facing +Z in glTF (-Y in Blender)
     - Features:
       * Three slightly rotated stacked corrugated delivery pizza boxes with printed red/charcoal pizzeria graphics
       * Top box half-open (~42 deg lid with folded side flaps & grease stain) revealing 5 realistic pizza slices inside
         (golden baked crust rim, tomato sauce, melted mozzarella cheese, sculpted pepperoni discs & basil leaves)
       * Two crushed aluminum energy drink cans (matte dark charcoal/crimson & brushed silver, pull tabs, realistic dent creases, zero garish neon)
       * Paper takeaway coffee cup with corrugated kraft sleeve and black sip-lid
  2. assets/models/prop_server_cart.glb — Rolling metal IT crash-cart / maintenance trolley for Server B12 corner
     - Exact footprint: ~0.78m wide (X) x 0.52m deep (Z_rl / Y_bl) x 0.96m tall (Y_rl / Z_bl)
     - Origin at bottom-center (Z_bl = 0 / Y_gltf = 0), facing +Z in glTF (-Y in Blender)
     - Features:
       * Tubular dark-graphite steel frame on 4 swivel caster wheels with side push-handle
       * Two slatted metal shelves
       * Bottom shelf: stacked brown corrugated hardware delivery boxes with shipping labels & barcode tape + coiled dark Ethernet/power cable bundles
       * Top shelf: Petrovich's vintage 1998 beige CRT diagnostic monitor (sculpted bezel, curved glass phosphor screen with retro BIOS/terminal diagnostics, tilt-swivel base, rear cathode housing with ventilation slots, coiled beige PS/2 keyboard) + yellow/black diagonal hazard caution tape roll

Includes embedded procedural PBR textures, smooth normals, 14-ray hemisphere BVHTree vertex AO (`COLOR_0`),
and renders 3-angle studio turntable proof sheets:
  - C:/Users/se-20/.gemini/antigravity/brain/50fc13b9-24ea-4541-8bcc-1ae74f353f5a/pizza_stack_proof.png
  - C:/Users/se-20/.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a/server_cart_proof.png
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
SCREENSHOTS = ROOT / "screenshots"
COORD_BRAIN = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")
os.makedirs(OUT_MODELS, exist_ok=True)
os.makedirs(SCREENSHOTS, exist_ok=True)
os.makedirs(COORD_BRAIN, exist_ok=True)


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


# Convert Raylib/glTF coordinates (X right, Y up, Z forward) -> Blender coordinates (X right, Y = -Z, Z = Y)
def rl_pos(p):
    return V((p[0], -p[2], p[1]))


def rl_scale(s):
    return (s[0], s[2], s[1])


def rl_rot(r):
    return Euler((r[0], -r[2], r[1]), "XZY").to_matrix().to_euler("XYZ")


def make_packed_image(name, width, height, rgb_array):
    img = bpy.data.images.new(name, width=width, height=height, alpha=False)
    rgba = np.ones((height, width, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_array, 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())
    img.pack()
    return img


def make_mat(name, rgba, metallic=0.1, roughness=0.42, image=None, emission_rgba=None, emission_strength=0.0):
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
        e_col = emission_rgba if emission_rgba is not None else rgba
        bsdf.inputs["Emission Color"].default_value = e_col
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def make_pizza_box_texture():
    """Generates a 512x512 kraft corrugated pizza box lid texture with vintage crimson/charcoal Italian pizzeria print."""
    h, w = 512, 512
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h

    # Warm corrugated kraft cardboard base with subtle flute ribs & organic paper fibers
    flutes = 0.015 * np.sin(u * math.tau * 85.0) + 0.012 * np.sin(x * 0.37) * np.cos(y * 0.41)
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[:, :, 0] = 0.72 + flutes
    rgb[:, :, 1] = 0.56 + flutes * 0.85
    rgb[:, :, 2] = 0.38 + flutes * 0.65

    # Subtle warm grease spot near center
    r_grease = np.hypot(u - 0.54, v - 0.46)
    grease = np.exp(-((r_grease / 0.19) ** 2)) * 0.14
    rgb[:, :, 0] -= grease * 0.8
    rgb[:, :, 1] -= grease * 0.9
    rgb[:, :, 2] -= grease * 1.0

    # Printed crimson & charcoal double-border frame on the box top
    crimson = np.array([0.58, 0.12, 0.11], dtype=np.float32)
    charcoal = np.array([0.16, 0.15, 0.14], dtype=np.float32)
    m1, m2 = 36, 48
    rgb[m1:m1 + 6, m1:w - m1, :] = crimson
    rgb[h - m1 - 6:h - m1, m1:w - m1, :] = crimson
    rgb[m1:h - m1, m1:m1 + 6, :] = crimson
    rgb[m1:h - m1, w - m1 - 6:w - m1, :] = crimson

    rgb[m2:m2 + 3, m2:w - m2, :] = charcoal
    rgb[h - m2 - 3:h - m2, m2:w - m2, :] = charcoal
    rgb[m2:h - m2, m2:m2 + 3, :] = charcoal
    rgb[m2:h - m2, w - m2 - 3:w - m2, :] = charcoal

    # Checkerboard pizzeria corners
    for cx0 in (m1, w - m1 - 36):
        for cy0 in (m1, h - m1 - 36):
            for r_i in range(3):
                for c_i in range(3):
                    if (r_i + c_i) % 2 == 0:
                        rgb[cy0 + r_i * 12:cy0 + (r_i + 1) * 12, cx0 + c_i * 12:cx0 + (c_i + 1) * 12, :] = crimson

    # Center circular pizzeria seal emblem
    dist_c = np.hypot(x - 256.0, y - 256.0)
    ring_mask = (dist_c >= 94.0) & (dist_c <= 104.0)
    inner_ring = (dist_c >= 84.0) & (dist_c <= 87.0)
    rgb[ring_mask] = crimson
    rgb[inner_ring] = charcoal

    # Stylized pizza slice icon & bold banner bars inside the emblem
    rgb[244:268, 132:380, :] = crimson
    for bx in range(152, 360, 26):
        rgb[250:262, bx:bx + 16, :] = (0.86, 0.74, 0.56)
    rgb[292:304, 184:328, :] = charcoal
    rgb[210:220, 192:320, :] = charcoal

    return make_packed_image("PizzaBoxKraftTex", w, h, rgb)


def make_pizza_topping_texture():
    """Generates a 256x256 baked melted mozzarella & tomato sauce pizza surface texture."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h
    r = np.hypot(u - 0.5, v - 0.5) * 2.0

    # Melted golden mozzarella & rich tomato base
    mottle = (
        0.5 + 0.28 * np.sin(u * math.tau * 7.0 + np.cos(v * math.tau * 6.0))
        + 0.22 * np.cos(v * math.tau * 11.0 - np.sin(u * math.tau * 9.0))
    )
    cheese_mask = np.clip((mottle - 0.28) / 0.45, 0.0, 1.0)
    sauce = np.array([0.56, 0.11, 0.07], dtype=np.float32)
    cheese = np.array([0.92, 0.74, 0.36], dtype=np.float32)
    crust = np.array([0.68, 0.44, 0.20], dtype=np.float32)

    rgb = np.zeros((h, w, 3), dtype=np.float32)
    for c in range(3):
        base = sauce[c] * (1.0 - cheese_mask) + cheese[c] * cheese_mask
        rim_blend = np.clip((r - 0.74) / 0.18, 0.0, 1.0)
        rgb[:, :, c] = base * (1.0 - rim_blend) + crust[c] * rim_blend

    # Charred wood-fired blisters
    blister = np.sin(u * math.tau * 19.0) * np.cos(v * math.tau * 23.0)
    rgb[blister > 0.78] *= 0.55
    return make_packed_image("PizzaToppingTex", w, h, rgb)


def make_energy_can_texture():
    """Generates a 256x256 matte dark-graphite & crimson/silver brushed aluminum energy drink can texture (zero neon)."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h
    rgb = np.zeros((h, w, 3), dtype=np.float32)

    # Matte charcoal aluminum body
    rgb[:, :, 0] = 0.11
    rgb[:, :, 1] = 0.12
    rgb[:, :, 2] = 0.14

    # Top and bottom brushed silver aluminum rims
    rgb[:22, :, :] = (0.76, 0.79, 0.82)
    rgb[234:, :, :] = (0.76, 0.79, 0.82)

    # Diagonal deep crimson racing stripe & silver typography blocks
    stripe = np.abs((v - 0.52) - 0.28 * (u - 0.5)) < 0.14
    rgb[stripe] = (0.58, 0.09, 0.11)
    border = (np.abs((v - 0.52) - 0.28 * (u - 0.5)) >= 0.14) & (np.abs((v - 0.52) - 0.28 * (u - 0.5)) < 0.165)
    rgb[border] = (0.82, 0.84, 0.88)

    # Crisp nutrition facts table on back side of can (u in [0.68, 0.92])
    rgb[44:210, 174:236, :] = (0.16, 0.17, 0.20)
    for row_y in range(52, 202, 12):
        rgb[row_y:row_y + 4, 180:230, :] = (0.68, 0.70, 0.74)

    return make_packed_image("EnergyCanMatteTex", w, h, rgb)


def make_shipping_carton_texture():
    """Generates a 256x256 brown corrugated shipping carton texture with packing tape, barcode label & handling icons."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h
    flute = 0.018 * np.sin(v * math.tau * 64.0) + 0.012 * np.cos(u * math.tau * 27.0)
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[:, :, 0] = 0.58 + flute
    rgb[:, :, 1] = 0.42 + flute * 0.8
    rgb[:, :, 2] = 0.26 + flute * 0.6

    # Center tan packing tape strip
    rgb[114:142, :, :] = (0.68, 0.53, 0.34)

    # White thermal shipping label with barcode bars
    rgb[38:104, 34:142, :] = (0.91, 0.90, 0.87)
    rgb[88:98, 42:134, :] = (0.15, 0.16, 0.18)
    for bx in range(44, 132, 5):
        if (bx * 7) % 3 != 0:
            rgb[46:78, bx:bx + 3, :] = (0.12, 0.13, 0.15)

    # Stencil "FRAGILE // B12" markings on right side
    rgb[44:48, 168:228, :] = (0.18, 0.16, 0.15)
    rgb[54:78, 174:194, :] = (0.18, 0.16, 0.15)
    rgb[54:78, 202:222, :] = (0.18, 0.16, 0.15)
    return make_packed_image("ShippingCartonTex", w, h, rgb)


def make_crt_screen_texture():
    """Generates a 256x256 vintage 1998 CRT monitor phosphor diagnostic screen texture (amber/green terminal & scanlines)."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h

    # Dark curved phosphor glass background with subtle vignette & CRT scanlines
    scan = 0.90 + 0.10 * np.sin(y * math.pi)
    vig = np.clip(1.12 - 0.85 * np.hypot(u - 0.5, v - 0.5), 0.25, 1.0)
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[:, :, 0] = 0.03 * vig
    rgb[:, :, 1] = 0.08 * vig
    rgb[:, :, 2] = 0.05 * vig

    phosphor_green = np.array([0.28, 0.92, 0.48], dtype=np.float32)
    phosphor_amber = np.array([0.96, 0.72, 0.22], dtype=np.float32)

    # Top retro BIOS header frame ("SYSTEM ZERO // B12 HARDWARE DIAG v1.98")
    rgb[222:225, 18:238, :] = phosphor_green * 0.8
    rgb[196:199, 18:238, :] = phosphor_green * 0.8
    for bx in range(26, 218, 18):
        rgb[206:215, bx:bx + 12, :] = phosphor_amber if bx < 80 else phosphor_green

    # Diagnostic memory hex dump rows & status bars
    for row in range(13):
        ry = 178 - row * 11
        if ry < 28:
            break
        # Left address column
        rgb[ry:ry + 5, 22:48, :] = phosphor_green * 0.65
        # Hex data blocks
        cur = 58
        for col in range(5):
            blen = 16 + ((row * 11 + col * 17) % 18)
            if cur + blen > 182:
                break
            c_use = phosphor_amber if (row == 4 and col == 2) else phosphor_green
            rgb[ry:ry + 5, cur:cur + blen, :] = c_use * (0.85 if col % 2 == 0 else 0.65)
            cur += blen + 6
        # Right load meter bar
        bar_w = 12 + ((row * 19 + 7) % 34)
        rgb[ry:ry + 5, 192:192 + bar_w, :] = phosphor_amber if bar_w > 34 else phosphor_green

    for c in range(3):
        rgb[:, :, c] *= scan
    return make_packed_image("CrtPhosphorDiagTex", w, h, rgb)


def make_caution_tape_texture():
    """Generates a 256x256 diagonal yellow-and-black hazard caution tape texture."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u, v = x / w, y / h
    stripe = np.mod(u * 8.0 + v * 2.0, 1.0) < 0.52
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[stripe] = (0.90, 0.70, 0.12)
    rgb[~stripe] = (0.10, 0.11, 0.13)
    return make_packed_image("CautionTapeTex", w, h, rgb)


def apply_box_uv_and_smooth(obj, scale_uv=2.2):
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


def add_box(name, pos_rl, size_rl, mat, bevel=0.008, segments=3, rot_rl=(0, 0, 0), uv_scale=2.2, top_zero_to_one_uv=False):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size_rl)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.profile = 0.55
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    if top_zero_to_one_uv:
        mesh = obj.data
        if not mesh.uv_layers:
            mesh.uv_layers.new(name="UVMap")
        uv_layer = mesh.uv_layers.active
        sx, sy, sz = rl_scale(size_rl)
        for poly in mesh.polygons:
            poly.use_smooth = True
            for l_idx in poly.loop_indices:
                v = mesh.vertices[mesh.loops[l_idx].vertex_index].co
                u = max(0.0, min(1.0, v.x / max(sx, 1e-4) + 0.5))
                w_uv = max(0.0, min(1.0, v.y / max(sy, 1e-4) + 0.5))
                uv_layer.data[l_idx].uv = (u, w_uv)
    else:
        apply_box_uv_and_smooth(obj, scale_uv=uv_scale)
    obj.rotation_euler = rl_rot(rot_rl)
    obj.location = rl_pos(pos_rl)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return obj


def add_cyl(name, pos_rl, r0, r1, height_rl, mat, rot_rl=(0, 0, 0), verts=24, bevel=0.002):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r0, radius2=r1, depth=height_rl,
        location=rl_pos(pos_rl), rotation=rl_rot(rot_rl),
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=2.4)


def add_tube_curve(name, pts_radii, mat, sides=14, steps_per_span=3, closed=False):
    """Lofts a smooth Catmull-Rom tubular curve through `(raylib_pos, radius)` control points."""
    raw = [(rl_pos(p), r) for p, r in pts_radii]
    if closed:
        raw = [raw[-1]] + raw + [raw[0], raw[1]]
        start_i, end_i = 1, len(raw) - 2
    else:
        start_i, end_i = 0, len(raw) - 1

    b_pts = []
    n_raw = len(raw)
    steps = steps_per_span if n_raw >= 3 else 1
    for i in range(start_i, end_i):
        p0, r0 = raw[max(0, i - 1)]
        p1, r1 = raw[i]
        p2, r2 = raw[i + 1]
        p3, r3 = raw[min(n_raw - 1, i + 2)]
        for s in range(steps):
            t = s / steps
            t2, t3 = t * t, t * t * t
            pt = 0.5 * ((2.0 * p1) + (-p0 + p2) * t + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2 + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3)
            rad = r1 * (1.0 - t) + r2 * t
            b_pts.append((pt, rad))
    if not closed:
        b_pts.append(raw[-1])

    verts, faces = [], []
    prev_tang, prev_right = None, None
    n_b = len(b_pts)
    for idx, (pt, rad) in enumerate(b_pts):
        if closed:
            tang = (b_pts[(idx + 1) % n_b][0] - b_pts[(idx - 1) % n_b][0]).normalized()
        elif idx == 0:
            tang = (b_pts[1][0] - pt).normalized()
        elif idx == n_b - 1:
            tang = (pt - b_pts[-2][0]).normalized()
        else:
            tang = (b_pts[idx + 1][0] - b_pts[idx - 1][0]).normalized()
        if prev_right is None:
            up = V((0, 0, 1)) if abs(tang.z) < 0.90 else V((1, 0, 0))
            right = tang.cross(up).normalized()
        else:
            q = prev_tang.rotation_difference(tang)
            right = q @ prev_right
            right = (right - tang * right.dot(tang)).normalized()
        ortho = right.cross(tang).normalized()
        prev_tang, prev_right = tang, right
        for s in range(sides):
            a = s * math.tau / sides
            verts.append(pt + (right * math.cos(a) + ortho * math.sin(a)) * rad)
    for k in range(n_b if closed else n_b - 1):
        b0 = k * sides
        b1 = ((k + 1) % n_b) * sides
        for s in range(sides):
            sn = (s + 1) % sides
            faces.append((b0 + s, b0 + sn, b1 + sn, b1 + s))
    if not closed:
        faces.append(tuple(reversed(range(sides))))
        last = (n_b - 1) * sides
        faces.append(tuple(range(last, last + sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    return apply_box_uv_and_smooth(obj)


def bake_ao_and_export(filename):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = obj.data.name = filename.removesuffix(".glb")

    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    min_z = min(v.co.z for v in obj.data.vertices)
    for v in obj.data.vertices:
        v.co.z -= min_z
    obj.data.update()
    for poly in obj.data.polygons:
        poly.use_smooth = True

    # Bake 14-ray hemisphere BVHTree ambient occlusion into COLOR_0
    verts = obj.data.vertices
    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(p.vertices) for p in obj.data.polygons])
    col_attr = obj.data.color_attributes.new(name="AO", type="BYTE_COLOR", domain="POINT")
    for v in verts:
        n = v.normal.normalized()
        up = V((1, 0, 0)) if abs(n.x) < 0.85 else V((0, 1, 0))
        u_axis = n.cross(up).normalized()
        v_axis = n.cross(u_axis).normalized()
        ao = 0.0
        for j in range(14):
            a = j * 2.39996
            ray = (n * 0.52 + u_axis * math.cos(a) + v_axis * math.sin(a)).normalized()
            hit_dist = tree.ray_cast(v.co + n * 0.001, ray, 0.065)[3]
            if hit_dist is not None:
                ao += (1.0 - hit_dist / 0.065) / 14.0
        shade = max(0.72, min(1.0, 1.0 - 0.32 * ao))
        col_attr.data[v.index].color = (shade, shade, shade, 1.0)

    b_verts = [obj.matrix_world @ v.co for v in obj.data.vertices]
    mn = tuple(round(min(v[i] for v in b_verts), 4) for i in range(3))
    mx = tuple(round(max(v[i] for v in b_verts), 4) for i in range(3))
    dims = tuple(round(mx[i] - mn[i], 4) for i in range(3))
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)

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
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        d = ROOT / sub
        if d.exists():
            shutil.copy2(out_path, d / filename)
    print(f"EXPORTED {filename} | size={out_path.stat().st_size}B | tris={tris} | dims_bl={dims} | min_bl={mn} | max_bl={mx}")


# ==============================================================================
# 1. `prop_pizza_stack.glb` — Chaotic Startup Fuel Pile
#    Target footprint: ~0.52m wide (X) x 0.48m deep (Z_rl / Y_bl) x 0.24m tall (Y_rl / Z_bl)
#    Origin at bottom-center (Y_rl = 0), front facing +Z in glTF (-Y in Blender)
# ==============================================================================
def build_prop_pizza_stack():
    clear_scene()
    tex_box = make_pizza_box_texture()
    tex_pizza = make_pizza_topping_texture()
    tex_can = make_energy_can_texture()

    m_box = make_mat("CorrugatedPizzaBox", (1.0, 1.0, 1.0, 1.0), metallic=0.04, roughness=0.68, image=tex_box)
    m_kraft = make_mat("KraftCardboardInner", (0.68, 0.52, 0.35, 1.0), metallic=0.04, roughness=0.74)
    m_pizza = make_mat("BakedPizzaCheese", (1.0, 1.0, 1.0, 1.0), metallic=0.05, roughness=0.38, image=tex_pizza)
    m_pepperoni = make_mat("CrispyPepperoni", (0.52, 0.08, 0.06, 1.0), metallic=0.06, roughness=0.32)
    m_can = make_mat("EnergyCanAluminum", (1.0, 1.0, 1.0, 1.0), metallic=0.78, roughness=0.26, image=tex_can)
    m_silver = make_mat("BrushedCanSilver", (0.80, 0.83, 0.87, 1.0), metallic=0.90, roughness=0.20)
    m_coffee = make_mat("CupWhiteTakeaway", (0.92, 0.90, 0.86, 1.0), metallic=0.04, roughness=0.45)
    m_lid = make_mat("MatteBlackSipLid", (0.10, 0.11, 0.13, 1.0), metallic=0.12, roughness=0.36)

    # --------------------------------------------------------------------------
    # A. THREE SLIGHTLY ROTATED STACKED CORRUGATED PIZZA BOXES
    # --------------------------------------------------------------------------
    # Box 1 (Bottom closed 36cm box, slightly shifted left-back, yaw = -8 deg)
    b1_pos = (-0.045, 0.022, -0.020)
    b1_yaw = math.radians(-8.0)
    add_box("PizzaBox1_Base", b1_pos, (0.36, 0.040, 0.36), m_kraft, bevel=0.004, rot_rl=(0, b1_yaw, 0))
    add_box("PizzaBox1_Lid", (b1_pos[0], 0.041, b1_pos[2]), (0.366, 0.012, 0.366), m_box, bevel=0.004, rot_rl=(0, b1_yaw, 0), top_zero_to_one_uv=True)

    # Box 2 (Middle closed 35cm box, shifted slightly right-front, yaw = +14 deg)
    b2_pos = (-0.022, 0.067, -0.005)
    b2_yaw = math.radians(14.0)
    add_box("PizzaBox2_Base", b2_pos, (0.35, 0.040, 0.35), m_kraft, bevel=0.004, rot_rl=(0, b2_yaw, 0))
    add_box("PizzaBox2_Lid", (b2_pos[0], 0.086, b2_pos[2]), (0.356, 0.012, 0.356), m_box, bevel=0.004, rot_rl=(0, b2_yaw, 0), top_zero_to_one_uv=True)

    # Box 3 (Top HALF-OPEN 34cm box at Y_rl = 0.092..0.132, yaw = -5 deg, lid propped open at ~36 deg so total height = 0.240m!)
    b3_cx, b3_cz = -0.035, -0.015
    b3_yaw = math.radians(-5.0)
    # Build open box tray: bottom floor + 4 perimeter walls
    add_box("PizzaBox3_TrayFloor", (b3_cx, 0.096, b3_cz), (0.34, 0.008, 0.34), m_kraft, bevel=0.003, rot_rl=(0, b3_yaw, 0))
    cy_w, sy_w = math.cos(b3_yaw), math.sin(b3_yaw)

    def box3_pt(lx, ly, lz):
        return (b3_cx + lx * cy_w + lz * sy_w, ly, b3_cz - lx * sy_w + lz * cy_w)

    for wx, wz, sx, sz in (
        (0.0,   0.164, 0.34, 0.012),  # front wall
        (0.0,  -0.164, 0.34, 0.012),  # rear hinge wall
        (-0.164, 0.0,  0.012, 0.32),  # left wall
        ( 0.164, 0.0,  0.012, 0.32),  # right wall
    ):
        add_box(f"PizzaBox3_Wall_{wx}_{wz}", box3_pt(wx, 0.114, wz), (sx, 0.036, sz), m_kraft, bevel=0.003, rot_rl=(0, b3_yaw, 0))

    # Half-open hinged top lid tilting upward from the rear wall (hinge at lz = -0.164, y = 0.132, open angle = 17.5 deg -> front edge reaches Y_rl = 0.236m!)
    open_ang = math.radians(17.5)
    lid_len = 0.344
    lid_mid_y = 0.132 + 0.5 * lid_len * math.sin(open_ang)
    lid_mid_lz = -0.164 + 0.5 * lid_len * math.cos(open_ang)
    add_box(
        "PizzaBox3_OpenLid",
        box3_pt(0.0, lid_mid_y, lid_mid_lz),
        (0.346, 0.009, lid_len),
        m_box,
        bevel=0.003,
        rot_rl=(-open_ang, b3_yaw, 0),
        top_zero_to_one_uv=True,
    )
    # Front tuck flap hanging down from the open lid's front edge
    flap_y = 0.132 + lid_len * math.sin(open_ang) - 0.012
    flap_lz = -0.164 + lid_len * math.cos(open_ang) + 0.003
    add_box(
        "PizzaBox3_FrontFlap",
        box3_pt(0.0, flap_y, flap_lz),
        (0.338, 0.026, 0.008),
        m_box,
        bevel=0.002,
        rot_rl=(0.15, b3_yaw, 0),
    )

    # --------------------------------------------------------------------------
    # B. FIVE REALISTIC PIZZA SLICES INSIDE THE HALF-OPEN TOP BOX (plus 1 slice pulled slightly forward!)
    # --------------------------------------------------------------------------
    # Each slice is a 40-degree wedge with a puffy golden crust rim, melted cheese/sauce body, and pepperoni discs
    for s_idx in range(5):
        a0 = math.radians(-20 + s_idx * 45)
        a1 = a0 + math.radians(39)
        a_mid = 0.5 * (a0 + a1)
        # Pull slice 0 slightly forward toward the open front lip!
        pull = 0.022 if s_idx == 0 else 0.004
        sx_off = pull * math.sin(a_mid)
        sz_off = pull * math.cos(a_mid)
        r_slice = 0.142
        n_arc = 7
        verts, faces = [], []
        # Tip vertex (bottom & top)
        p_tip_b = box3_pt(sx_off + 0.012 * math.sin(a_mid), 0.100, sz_off + 0.012 * math.cos(a_mid))
        p_tip_t = box3_pt(sx_off + 0.012 * math.sin(a_mid), 0.109, sz_off + 0.012 * math.cos(a_mid))
        verts.extend([rl_pos(p_tip_b), rl_pos(p_tip_t)])
        for k in range(n_arc + 1):
            ang = a0 + (a1 - a0) * (k / n_arc)
            px = sx_off + r_slice * math.sin(ang)
            pz = sz_off + r_slice * math.cos(ang)
            verts.append(rl_pos(box3_pt(px, 0.100, pz)))
            verts.append(rl_pos(box3_pt(px, 0.111, pz)))
        for k in range(n_arc):
            b_i = 2 + k * 2
            # Top triangle, bottom triangle, outer crust quad
            faces.append((1, b_i + 1, b_i + 3))
            faces.append((0, b_i + 2, b_i))
            faces.append((b_i, b_i + 2, b_i + 3, b_i + 1))
        # Side cut faces of the slice
        faces.append((0, 2, 3, 1))
        last_b = 2 + n_arc * 2
        faces.append((0, 1, last_b + 1, last_b))

        s_mesh = bpy.data.meshes.new(f"PizzaSlice_{s_idx}")
        s_mesh.from_pydata(verts, [], faces)
        s_mesh.update()
        s_obj = bpy.data.objects.new(f"PizzaSlice_{s_idx}", s_mesh)
        bpy.context.collection.objects.link(s_obj)
        s_mesh.materials.append(m_pizza)
        uv = s_mesh.uv_layers.new(name="UVMap")
        for poly in s_mesh.polygons:
            poly.use_smooth = True
            for l_idx in poly.loop_indices:
                co = s_mesh.vertices[s_mesh.loops[l_idx].vertex_index].co
                uv.data[l_idx].uv = (0.5 + (co.x - b3_cx) * 3.2, 0.5 + (co.y + b3_cz) * 3.2)

        # Puffy baked crust rim along the outer arc of the slice
        crust_pts = [
            (box3_pt(sx_off + (r_slice - 0.008) * math.sin(a0 + (a1 - a0) * (k / 5)), 0.114, sz_off + (r_slice - 0.008) * math.cos(a0 + (a1 - a0) * (k / 5))), 0.013)
            for k in range(6)
        ]
        add_tube_curve(f"PizzaCrustRim_{s_idx}", crust_pts, m_kraft, sides=12, steps_per_span=2)

        # 3 sculpted pepperoni discs on each slice
        for p_r, p_da in ((0.055, 0.0), (0.098, -0.18), (0.102, 0.18)):
            pa = a_mid + p_da
            ppx = sx_off + p_r * math.sin(pa)
            ppz = sz_off + p_r * math.cos(pa)
            add_cyl(f"Pepperoni_{s_idx}_{p_r}_{p_da}", box3_pt(ppx, 0.113, ppz), 0.015, 0.014, 0.004, m_pepperoni, verts=16, bevel=0.001)

    # White plastic pizza-saver tripod ("little table") in the center of the pizza
    add_cyl("PizzaSaverTop", box3_pt(0.0, 0.120, 0.0), 0.016, 0.016, 0.003, m_coffee, verts=16, bevel=0.0005)
    for leg_i in range(3):
        la = leg_i * math.tau / 3.0
        add_cyl(f"PizzaSaverLeg_{leg_i}", box3_pt(0.012 * math.cos(la), 0.110, 0.012 * math.sin(la)), 0.002, 0.002, 0.018, m_coffee, verts=8, bevel=0.0)

    # --------------------------------------------------------------------------
    # C. TWO CRUSHED ALUMINUM ENERGY DRINK CANS & TAKEAWAY COFFEE CUP ON THE SIDE
    # --------------------------------------------------------------------------
    def add_crushed_can(name, pos_rl, rot_rl, dent_strength=0.26):
        """Builds a 500ml aluminum energy drink can with realistic side crush indentation & pull-tab top."""
        n_c = 24
        rings = [
            (-0.072, 0.026, 1),
            (-0.066, 0.032, 0),
            (-0.038, 0.033, 0),
            (-0.012, 0.025, 0),  # crushed waist
            ( 0.014, 0.027, 0),  # crushed waist
            ( 0.042, 0.033, 0),
            ( 0.066, 0.032, 0),
            ( 0.074, 0.027, 1),
            ( 0.075, 0.002, 1),
        ]
        c_verts, c_faces = [], []
        for yh, rad, _ in rings:
            mid_crush = math.exp(-((yh / 0.032) ** 2)) * dent_strength
            for i in range(n_c):
                a = i * math.tau / n_c
                # Asymmetric crush dent (pinched along X axis, bulging slightly along Z)
                r_mod = rad * (1.0 - mid_crush * math.cos(2.0 * a) - 0.35 * mid_crush * math.sin(a))
                x = r_mod * math.cos(a)
                z = r_mod * math.sin(a) + (0.006 * mid_crush if abs(yh) < 0.025 else 0.0)
                c_verts.append(V((x, -z, yh)))
        for k in range(len(rings) - 1):
            for i in range(n_c):
                c_faces.append((k * n_c + i, k * n_c + (i + 1) % n_c, (k + 1) * n_c + (i + 1) % n_c, (k + 1) * n_c + i))
        c_faces.append(tuple(reversed(range(n_c))))
        c_mesh = bpy.data.meshes.new(name)
        c_mesh.from_pydata(c_verts, [], c_faces)
        c_mesh.update()
        c_obj = bpy.data.objects.new(name, c_mesh)
        bpy.context.collection.objects.link(c_obj)
        c_mesh.materials.append(m_can)     # 0: printed matte body
        c_mesh.materials.append(m_silver)  # 1: brushed aluminum top/bottom lids
        uv = c_mesh.uv_layers.new(name="UVMap")
        for k in range(len(rings) - 1):
            mat_i = 1 if (k == 0 or k >= len(rings) - 2) else 0
            for i in range(n_c):
                p = c_mesh.polygons[k * n_c + i]
                p.use_smooth = True
                p.material_index = mat_i
                for l_idx in p.loop_indices:
                    v_i = c_mesh.loops[l_idx].vertex_index
                    r_idx, c_idx = divmod(v_i, n_c)
                    if i == n_c - 1 and c_idx == 0:
                        c_idx = n_c
                    uv.data[l_idx].uv = (c_idx / n_c, (rings[r_idx][0] + 0.072) / 0.147)
        c_obj.rotation_euler = rl_rot(rot_rl)
        c_obj.location = rl_pos(pos_rl)
        bpy.ops.object.select_all(action="DESELECT")
        c_obj.select_set(True)
        bpy.context.view_layer.objects.active = c_obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        return c_obj

    # Can 1: Upright crushed energy drink can on the front-right corner
    add_crushed_can("CrushedCan_Upright", (0.195, 0.073, 0.135), (0.04, 0.35, -0.05), dent_strength=0.28)
    add_box("CanPullTab1", (0.195, 0.149, 0.135), (0.016, 0.002, 0.024), m_silver, bevel=0.0005, rot_rl=(0, 0.35, 0))

    # Can 2: Knocked-over crushed energy drink can lying on its side in front of the pizza boxes
    add_crushed_can("CrushedCan_Side", (0.125, 0.031, 0.202), (math.pi / 2, 0.48, 0.12), dent_strength=0.32)

    # Paper takeaway coffee cup with corrugated kraft sleeve & black dome sip-lid at back-right
    cx_cup, cz_cup = 0.205, -0.055
    add_cyl("TakeawayCupBody", (cx_cup, 0.068, cz_cup), 0.032, 0.044, 0.134, m_coffee, verts=28, bevel=0.002)
    add_cyl("TakeawayKraftSleeve", (cx_cup, 0.072, cz_cup), 0.036, 0.042, 0.058, m_kraft, verts=28, bevel=0.002)
    add_cyl("TakeawayLidRim", (cx_cup, 0.138, cz_cup), 0.046, 0.045, 0.010, m_lid, verts=28, bevel=0.002)
    add_cyl("TakeawayLidCrown", (cx_cup, 0.146, cz_cup), 0.043, 0.038, 0.012, m_lid, verts=28, bevel=0.002)
    add_box("TakeawaySipSpout", (cx_cup + 0.026, 0.153, cz_cup + 0.014), (0.014, 0.006, 0.018), m_lid, bevel=0.002, rot_rl=(0, -0.45, 0))

    bake_ao_and_export("prop_pizza_stack.glb")


# ==============================================================================
# 2. `prop_server_cart.glb` — Rolling Metal IT Crash-Cart / Maintenance Trolley
#    Target footprint: ~0.78m wide (X) x 0.52m deep (Z_rl / Y_bl) x 0.96m tall (Y_rl / Z_bl)
#    Origin at bottom-center (Y_rl = 0), front facing +Z in glTF (-Y in Blender)
# ==============================================================================
def build_prop_server_cart():
    clear_scene()
    tex_carton = make_shipping_carton_texture()
    tex_crt = make_crt_screen_texture()
    tex_tape = make_caution_tape_texture()

    m_frame = make_mat("TrolleyGraphiteSteel", (0.14, 0.16, 0.20, 1.0), metallic=0.82, roughness=0.28)
    m_chrome = make_mat("CasterBracketChrome", (0.76, 0.80, 0.85, 1.0), metallic=0.90, roughness=0.20)
    m_rubber = make_mat("WheelCableBlack", (0.07, 0.08, 0.10, 1.0), metallic=0.08, roughness=0.55)
    m_carton = make_mat("HardwareCarton", (1.0, 1.0, 1.0, 1.0), metallic=0.04, roughness=0.68, image=tex_carton)
    m_beige = make_mat("Retro98BeigePlastic", (0.82, 0.78, 0.68, 1.0), metallic=0.05, roughness=0.42)
    m_crt_scr = make_mat(
        "CrtPhosphorScreen",
        (1.0, 1.0, 1.0, 1.0),
        metallic=0.05,
        roughness=0.14,
        image=tex_crt,
        emission_rgba=(0.35, 0.95, 0.52, 1.0),
        emission_strength=0.65,
    )
    m_caution = make_mat("CautionTapeRoll", (1.0, 1.0, 1.0, 1.0), metallic=0.06, roughness=0.36, image=tex_tape)

    # --------------------------------------------------------------------------
    # A. TUBULAR DARK-GRAPHITE STEEL FRAME ON 4 CASTER WHEELS (X: 0.78m, Z_rl: 0.52m)
    # --------------------------------------------------------------------------
    # 4 Industrial swivel caster wheels at corners (X = +-0.31m, Z_rl = +-0.21m, Y_rl = 0.0 -> 0.11m)
    for cx in (-0.31, 0.31):
        for cz in (-0.21, 0.21):
            # Mounting plate & chrome swivel fork
            add_box(f"CasterPlate_{cx}_{cz}", (cx, 0.108, cz), (0.068, 0.010, 0.068), m_chrome, bevel=0.002)
            add_box(f"CasterFork_{cx}_{cz}", (cx, 0.072, cz - 0.008), (0.042, 0.062, 0.048), m_chrome, bevel=0.004)
            # Rubber wheel (radius 0.042m, bottom at Y_rl = 0.000m!) and hub axle
            add_cyl(f"CasterWheel_{cx}_{cz}", (cx, 0.042, cz - 0.012), 0.042, 0.042, 0.028, m_rubber, rot_rl=(0, 0, math.pi / 2), verts=24, bevel=0.003)
            add_cyl(f"CasterAxle_{cx}_{cz}", (cx, 0.042, cz - 0.012), 0.012, 0.012, 0.046, m_chrome, rot_rl=(0, 0, math.pi / 2), verts=16, bevel=0.001)

    # 4 Vertical tubular corner posts (Y_rl = 0.11 -> 0.72m)
    for px in (-0.31, 0.31):
        for pz in (-0.21, 0.21):
            add_cyl(f"CornerPost_{px}_{pz}", (px, 0.415, pz), 0.016, 0.016, 0.610, m_frame, verts=18, bevel=0.002)

    # Ergonomic curved tubular push-handle on the left end (-X reaching X = -0.390m -> total width 0.78m!)
    add_tube_curve(
        "CartPushHandle",
        [
            ((-0.310, 0.685, -0.210), 0.015),
            ((-0.375, 0.745, -0.210), 0.015),
            ((-0.375, 0.745,  0.000), 0.015),
            ((-0.375, 0.745,  0.210), 0.015),
            ((-0.310, 0.685,  0.210), 0.015),
        ],
        m_frame,
        sides=16,
        steps_per_span=3,
    )

    # --------------------------------------------------------------------------
    # B. TWO SLATTED METAL SHELVES WITH PERIMETER RETENTION LIPS (Y_rl = 0.13m and 0.56m)
    # --------------------------------------------------------------------------
    for s_idx, sy in enumerate((0.130, 0.560)):
        # Perimeter tray rails
        add_box(f"ShelfRailFront_{s_idx}", (0.0, sy + 0.016,  0.228), (0.66, 0.044, 0.020), m_frame, bevel=0.004)
        add_box(f"ShelfRailRear_{s_idx}",  (0.0, sy + 0.016, -0.228), (0.66, 0.044, 0.020), m_frame, bevel=0.004)
        add_box(f"ShelfRailLeft_{s_idx}",  (-0.320, sy + 0.016, 0.0), (0.020, 0.044, 0.44), m_frame, bevel=0.004)
        add_box(f"ShelfRailRight_{s_idx}", ( 0.320, sy + 0.016, 0.0), (0.020, 0.044, 0.44), m_frame, bevel=0.004)
        # 8 longitudinal slatted deck bars
        for b_i in range(8):
            bz = -0.189 + b_i * 0.054
            add_box(f"ShelfSlat_{s_idx}_{b_i}", (0.0, sy, bz), (0.62, 0.014, 0.040), m_frame, bevel=0.003)

    # Side cable-management hooks welded to right posts (+X = +0.390m -> symmetrical 0.78m width!)
    for hz in (-0.11, 0.11):
        add_tube_curve(
            f"CableHook_{hz}",
            [
                ((0.315, 0.520, hz), 0.008),
                ((0.375, 0.510, hz), 0.008),
                ((0.382, 0.545, hz), 0.008),
            ],
            m_chrome,
            sides=10,
        )

    # --------------------------------------------------------------------------
    # C. BOTTOM SHELF CARGO: STACKED HARDWARE CARTONS & COILED ETHERNET/POWER CABLES
    # --------------------------------------------------------------------------
    # Large bottom-left server PSU carton + stacked smaller router carton on top
    add_box("Carton_ServerPSU", (-0.135, 0.242, -0.015), (0.31, 0.210, 0.34), m_carton, bevel=0.006, rot_rl=(0, 0.08, 0), uv_scale=2.6)
    add_box("Carton_SwitchBox", (-0.145, 0.412, -0.025), (0.25, 0.130, 0.27), m_carton, bevel=0.005, rot_rl=(0, -0.18, 0), uv_scale=3.0)
    # Spare rack-mount patch box on bottom-right
    add_box("Carton_PatchPanel", (0.155, 0.202, -0.045), (0.23, 0.130, 0.28), m_carton, bevel=0.005, rot_rl=(0, 0.14, 0), uv_scale=3.0)

    # Heavy coiled dark power/Ethernet cable spool resting on the front-right of bottom shelf + hanging loop on right hooks
    for c_turn in range(4):
        bpy.ops.mesh.primitive_torus_add(
            major_segments=28, minor_segments=10,
            major_radius=0.082 - c_turn * 0.004, minor_radius=0.010,
            location=rl_pos((0.165, 0.152 + c_turn * 0.016, 0.115)),
            rotation=(math.radians(8 * (c_turn - 1.5)), math.radians(5 * c_turn), 0),
        )
        coil = bpy.context.active_object
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        coil.data.materials.append(m_rubber)
        apply_box_uv_and_smooth(coil)

    # Coiled Ethernet cable hanging off the right side hooks
    for h_turn in range(3):
        bpy.ops.mesh.primitive_torus_add(
            major_segments=28, minor_segments=10,
            major_radius=0.095, minor_radius=0.0085,
            location=rl_pos((0.352 + h_turn * 0.011, 0.435, 0.0)),
            rotation=(0, math.pi / 2, 0),
        )
        h_coil = bpy.context.active_object
        h_coil.scale = (1.0, 1.35, 0.85)
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        h_coil.data.materials.append(m_rubber)
        apply_box_uv_and_smooth(h_coil)

    # --------------------------------------------------------------------------
    # D. TOP SHELF HERO: PETROVICH'S VINTAGE 1998 BEIGE CRT MONITOR, KEYBOARD & CAUTION TAPE ROLL
    #    Reaches exact overall height Y_rl = 0.960m!
    # --------------------------------------------------------------------------
    crt_cx, crt_cz = -0.055, -0.010
    crt_yaw = math.radians(10.0)
    cy_c, sy_c = math.cos(crt_yaw), math.sin(crt_yaw)

    def crt_pt(lx, ly, lz):
        return (crt_cx + lx * cy_c + lz * sy_c, ly, crt_cz - lx * sy_c + lz * cy_c)

    # Tilt-swivel pedestal base on top shelf (Y_rl = 0.567 -> 0.625m)
    add_cyl("CrtSwivelBase", crt_pt(0.0, 0.580, 0.010), 0.135, 0.115, 0.026, m_beige, verts=28, bevel=0.004)
    add_cyl("CrtSwivelNeck", crt_pt(0.0, 0.608, 0.010), 0.068, 0.076, 0.036, m_beige, verts=24, bevel=0.003)

    # Thick 1998 beige front monitor bezel housing (Y_rl = 0.622 -> 0.960m -> center 0.791, height 0.338m)
    add_box("CrtFrontBezel", crt_pt(0.0, 0.791, 0.082), (0.365, 0.338, 0.145), m_beige, bevel=0.018, segments=4, rot_rl=(-0.05, crt_yaw, 0))
    # Recessed inner screen frame surround
    add_box("CrtScreenRecess", crt_pt(0.0, 0.802, 0.144), (0.305, 0.245, 0.024), m_frame, bevel=0.006, rot_rl=(-0.05, crt_yaw, 0))
    # Glowing 4:3 curved phosphor diagnostic screen with exact 0..1 planar UVs
    scr = add_box("CrtGlassPhosphor", (0.0, 0.0, 0.0), (0.286, 0.226, 0.014), m_crt_scr, bevel=0.004)
    uv_s = scr.data.uv_layers.active
    for poly in scr.data.polygons:
        for l_idx in poly.loop_indices:
            v = scr.data.vertices[scr.data.loops[l_idx].vertex_index].co
            u = max(0.0, min(1.0, 0.5 + v.x / 0.286))
            w_uv = max(0.0, min(1.0, 0.5 + v.z / 0.226))
            uv_s.data[l_idx].uv = (u, w_uv)
    scr.rotation_euler = rl_rot((-0.05, crt_yaw, 0))
    scr.location = rl_pos(crt_pt(0.0, 0.802, 0.152))
    bpy.ops.object.select_all(action="DESELECT")
    scr.select_set(True)
    bpy.context.view_layer.objects.active = scr
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    # Classic 90s power button, green LED & brightness/contrast rotary thumbwheels along bottom bezel
    add_box("CrtPowerBtn", crt_pt(0.132, 0.652, 0.155), (0.024, 0.016, 0.012), m_beige, bevel=0.002, rot_rl=(-0.05, crt_yaw, 0))
    add_cyl("CrtPowerLed", crt_pt(0.106, 0.652, 0.156), 0.004, 0.004, 0.006, m_crt_scr, rot_rl=(math.pi / 2, crt_yaw, 0), verts=12, bevel=0.0005)
    for dial_x in (-0.06, -0.02, 0.02):
        add_cyl(f"CrtThumbwheel_{dial_x}", crt_pt(dial_x, 0.650, 0.155), 0.008, 0.008, 0.008, m_frame, rot_rl=(0, crt_yaw, 0), verts=14, bevel=0.001)

    # Tapered rear cathode-ray tube housing with ventilation louvres
    add_box("CrtRearCathode", crt_pt(0.0, 0.785, -0.065), (0.275, 0.255, 0.185), m_beige, bevel=0.022, segments=4, rot_rl=(-0.05, crt_yaw, 0))
    add_box("CrtYokeCap", crt_pt(0.0, 0.780, -0.172), (0.155, 0.155, 0.075), m_beige, bevel=0.016, segments=3, rot_rl=(-0.05, crt_yaw, 0))
    for vent_i in range(6):
        vz = -0.02 - vent_i * 0.022
        add_box(f"CrtTopVent_{vent_i}", crt_pt(0.0, 0.912, vz), (0.19, 0.006, 0.009), m_frame, bevel=0.001, rot_rl=(-0.05, crt_yaw, 0))

    # Vintage beige PS/2 diagnostic keyboard resting in front-right of CRT on top shelf
    add_box("RetroKeyboardBody", (0.145, 0.578, 0.148), (0.28, 0.022, 0.115), m_beige, bevel=0.004, rot_rl=(0.06, -0.22, 0))
    add_box("RetroKeyboardKeys", (0.145, 0.588, 0.148), (0.25, 0.012, 0.088), m_frame, bevel=0.002, rot_rl=(0.06, -0.22, 0))

    # Yellow & Black Diagonal Caution Tape Roll sitting on the right side of the top shelf
    tx_r, tz_r = 0.225, -0.075
    add_cyl("CautionTapeOuter", (tx_r, 0.602, tz_r), 0.068, 0.068, 0.056, m_caution, verts=32, bevel=0.003)
    add_cyl("CautionTapeCore", (tx_r, 0.604, tz_r), 0.038, 0.038, 0.058, m_carton, verts=24, bevel=0.002)
    add_cyl("CautionTapeHole", (tx_r, 0.605, tz_r), 0.032, 0.032, 0.060, m_frame, verts=24, bevel=0.001)

    bake_ao_and_export("prop_server_cart.glb")


def render_3angle_proof(glb_name, png_name, ortho_scale, target_z, cam_dist=2.2, cam_height=0.95, spacing_mult=0.33):
    """Renders a 3-angle studio turntable proof (Front-Left 3/4, Direct Front, Rear-Right 3/4)
    to both `screenshots/<png_name>` and the coordinator's artifact directory.
    """
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1800
    scene.render.resolution_y = 760

    world = bpy.data.worlds.new("TurntableStudio")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.09, 0.11, 0.14, 1.0)

    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, -0.02))
    floor = bpy.context.active_object
    floor.scale = (5.5, 2.5, 0.04)
    m_floor = make_mat("StudioFloor", (0.11, 0.13, 0.17, 1.0), metallic=0.20, roughness=0.28)
    floor.data.materials.append(m_floor)

    spacing = ortho_scale * spacing_mult
    angles = [
        (-spacing, math.radians(28)),
        (0.0,      0.0),
        ( spacing, math.radians(-145)),
    ]
    for dx, rz in angles:
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
                v.co = (rot_m @ v.co) + V((dx, 0.0, 0.0))
            bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
            bm.to_mesh(o.data)
            bm.free()
            o.data.update()

    cam_data = bpy.data.cameras.new("ProofCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = ortho_scale
    cam = bpy.data.objects.new("ProofCam", cam_data)
    scene.collection.objects.link(cam)
    scene.camera = cam
    cam.location = (0.0, -cam_dist, cam_height)
    target = V((0.0, 0.0, target_z))
    cam.rotation_mode = "QUATERNION"
    cam.rotation_quaternion = (target - cam.location).to_track_quat("-Z", "Y")

    for loc, energy, col in [
        ((-2.2, -2.5, 2.6), 4.6, (1.0, 0.95, 0.86)),
        (( 2.4, -1.8, 2.0), 3.2, (0.82, 0.90, 1.0)),
        (( 0.0,  2.5, 2.2), 2.6, (1.0, 0.88, 0.72)),
    ]:
        ld = bpy.data.lights.new("Sun", type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new("Sun", ld)
        scene.collection.objects.link(lo)
        lo.location = loc
        lo.rotation_mode = "QUATERNION"
        lo.rotation_quaternion = (target - V(loc)).to_track_quat("-Z", "Y")

    out_local = SCREENSHOTS / png_name
    scene.render.filepath = str(out_local)
    bpy.ops.render.render(write_still=True)
    out_brain = COORD_BRAIN / png_name
    shutil.copy2(out_local, out_brain)
    print(f"RENDERED PROOF: {out_local} -> {out_brain}")


if __name__ == "__main__":
    build_prop_pizza_stack()
    build_prop_server_cart()
    render_3angle_proof("prop_pizza_stack.glb", "pizza_stack_proof.png", ortho_scale=1.75, target_z=0.11, cam_dist=1.8, cam_height=0.82, spacing_mult=0.33)
    render_3angle_proof("prop_server_cart.glb", "server_cart_proof.png", ortho_scale=2.75, target_z=0.46, cam_dist=2.4, cam_height=1.05, spacing_mult=0.33)
