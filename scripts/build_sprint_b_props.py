"""Blender 5.1 generator for Sprint B 3D Hero Office Props:
  1. assets/models/prop_coffee_machine.glb — Commercial Dual-Group Studio Espresso Station on Dark Walnut & Brushed-Steel Cabinet
     - Exact bounds: ~0.85m wide (X in [-0.425, +0.425]) x 1.39m tall (Z_bl in [0.0, 1.39]) x 0.68m deep (Y_bl in [-0.34, +0.34])
     - Origin at bottom-center (Z_bl = 0 / Y_gltf = 0), front facing +Z in glTF (-Y in Blender)
  2. assets/models/prop_extinguisher.glb — High-Poly CO2 Fire Extinguisher on Matte-Slate Floor Stand (next to Rack B12)
     - Exact bounds: ~0.82m tall (Z_bl in [0.0, 0.82]), diameter ~0.24m (X, Y_bl in [-0.12, +0.12])
     - Origin at bottom-center (Z_bl = 0 / Y_gltf = 0), front facing +Z in glTF (-Y in Blender)

Includes embedded procedural PBR textures, smooth normals, 14-ray hemisphere BVHTree vertex AO (`COLOR_0`),
and renders 3-angle studio turntable proof sheets:
  - C:/Users/se-20/.gemini/antigravity/brain/50fc13b9-24ea-4541-8bcc-1ae74f353f5a/coffee_machine_proof.png
  - C:/Users/se-20/.gemini/antigravity/brain/50fc13b9-24ea-4541-8bcc-1ae74f353f5a/extinguisher_proof.png
"""
import math
import os
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix
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
    return (r[0], -r[2], r[1])


def make_packed_image(name, width, height, rgb_array):
    img = bpy.data.images.new(name, width=width, height=height, alpha=False)
    rgba = np.ones((height, width, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_array, 0.0, 1.0)
    img.pixels.foreach_set(rgba.ravel())
    img.pack()
    return img


def make_walnut_texture():
    """Generates a 256x256 realistic straight-grain matte American walnut veneer texture (no wavy stripes)."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / w
    v = y / h
    # Subtle vertical wood fiber streaks along v with very gentle organic cathedral modulation
    fiber = (
        0.50
        + 0.14 * np.sin(u * math.tau * 11.0 + 0.35 * np.sin(v * math.tau * 2.0))
        + 0.08 * np.cos(u * math.tau * 27.0 + 0.20 * np.cos(v * math.tau * 3.0))
        + 0.05 * np.sin(u * math.tau * 63.0 + v * math.tau * 4.0)
        + 0.03 * np.cos(v * math.tau * 19.0)
    )
    fiber = np.clip(fiber, 0.25, 0.85)
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    rgb[:, :, 0] = 0.13 + 0.13 * fiber
    rgb[:, :, 1] = 0.072 + 0.078 * fiber
    rgb[:, :, 2] = 0.040 + 0.045 * fiber
    return make_packed_image("WalnutCabinetTex", w, h, rgb)


def make_brushed_steel_texture(name="BrushedStainlessTex", base_rgb=(0.76, 0.79, 0.83)):
    """Generates a 256x256 ultra-fine satin brushed metal texture without visible banding stripes."""
    h, w = 256, 256
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / w
    v = y / h
    streak = (
        0.010 * np.sin(v * math.tau * 89.0)
        + 0.008 * np.cos(v * math.tau * 137.0)
        + 0.015 * np.sin(u * math.pi)
    )
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    for c in range(3):
        rgb[:, :, c] = base_rgb[c] + streak
    return make_packed_image(name, w, h, rgb)


def make_extinguisher_cylinder_texture():
    """Generates a 512x512 deep matte crimson steel cylinder texture with a crisp technical stencil instruction band."""
    h, w = 512, 512
    rgb = np.zeros((h, w, 3), dtype=np.float32)
    # Deep crimson powder-coated steel base (sRGB ~0.58, 0.08, 0.09)
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    micro = 0.005 * np.sin(x * 0.45) * np.cos(y * 0.45)
    rgb[:, :, 0] = 0.58 + micro
    rgb[:, :, 1] = 0.08 + micro * 0.2
    rgb[:, :, 2] = 0.09 + micro * 0.2

    # Front stencil instruction label band lives strictly in the middle height v in [0.18, 0.62]
    # so the top dome (v = 0.85..0.98) and bottom base (v = 0.02..0.12) can sample the same texture seamlessly!
    y0, y1 = int(h * 0.18), int(h * 0.62)
    x0, x1 = int(w * 0.30), int(w * 0.70)
    # Off-white/silver metallic inspection decal plate
    rgb[y0:y1, x0:x1, :] = (0.88, 0.89, 0.91)
    # Dark charcoal border frame
    rgb[y0:y0 + 5, x0:x1, :] = (0.12, 0.14, 0.17)
    rgb[y1 - 5:y1, x0:x1, :] = (0.12, 0.14, 0.17)
    rgb[y0:y1, x0:x0 + 5, :] = (0.12, 0.14, 0.17)
    rgb[y0:y1, x1 - 5:x1, :] = (0.12, 0.14, 0.17)

    # Top crimson header bar inside label ("ОУ-5 // CO2")
    rgb[y1 - 38:y1 - 8, x0 + 8:x1 - 8, :] = (0.64, 0.07, 0.08)
    for bx in range(x0 + 22, x1 - 28, 30):
        rgb[y1 - 29:y1 - 16, bx:bx + 18, :] = (0.94, 0.94, 0.96)

    # 4 crisp schematic instruction rows
    for step in range(4):
        ry0 = y1 - 62 - step * 32
        ry1 = ry0 + 18
        rgb[ry0:ry1, x0 + 14:x0 + 36, :] = (0.14, 0.16, 0.20)
        rgb[ry0 + 4:ry1 - 4, x0 + 18:x0 + 32, :] = (0.88, 0.68, 0.22) if step == 0 else (0.88, 0.89, 0.91)
        rgb[ry0 + 10:ry0 + 15, x0 + 44:x1 - 18, :] = (0.16, 0.18, 0.22)
        rgb[ry0 + 3:ry0 + 7, x0 + 44:x1 - 42, :] = (0.34, 0.37, 0.42)

    # Lower hazard class B/E badges
    for b_idx, b_col in enumerate(((0.14, 0.16, 0.20), (0.76, 0.54, 0.14), (0.14, 0.48, 0.72))):
        bx0 = x0 + 18 + b_idx * 58
        rgb[y0 + 12:y0 + 32, bx0:bx0 + 48, :] = b_col
        rgb[y0 + 17:y0 + 27, bx0 + 8:bx0 + 40, :] = (0.92, 0.93, 0.95)

    return make_packed_image("ExtinguisherStencilTex", w, h, rgb)


def make_mat(name, rgba, metallic=0.1, roughness=0.38, image=None, emission_rgba=None, emission_strength=0.0):
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


def add_box(name, pos_rl, size_rl, mat, bevel=0.012, segments=3, rot_rl=(0, 0, 0), uv_scale=1.8):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=rl_pos(pos_rl), rotation=rl_rot(rot_rl))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = rl_scale(size_rl)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        mod.profile = 0.55
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=uv_scale)


def add_cyl(name, pos_rl, r0, r1, height_rl, mat, rot_rl=(0, 0, 0), verts=28, bevel=0.002):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts, radius1=r0, radius2=r1, depth=height_rl,
        location=rl_pos(pos_rl), rotation=rl_rot(rot_rl),
    )
    obj = bpy.context.active_object
    obj.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(mat)
    return apply_box_uv_and_smooth(obj, scale_uv=2.0)


def add_tube_curve(name, pts_radii, mat, sides=16, steps_per_span=3):
    """Lofts a smooth Catmull-Rom tubular curve through `(raylib_pos, radius)` control points."""
    raw = [(rl_pos(p), r) for p, r in pts_radii]
    b_pts = []
    n_raw = len(raw)
    steps = steps_per_span if n_raw >= 3 else 1
    for i in range(n_raw - 1):
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
    b_pts.append(raw[-1])

    verts, faces = [], []
    prev_tang, prev_right = None, None
    for idx, (pt, rad) in enumerate(b_pts):
        if idx == 0:
            tang = (b_pts[1][0] - pt).normalized()
        elif idx == len(b_pts) - 1:
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
    for k in range(len(b_pts) - 1):
        b0, b1 = k * sides, (k + 1) * sides
        for s in range(sides):
            sn = (s + 1) % sides
            faces.append((b0 + s, b0 + sn, b1 + sn, b1 + s))
    faces.append(tuple(reversed(range(sides))))
    last = (len(b_pts) - 1) * sides
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
            hit_dist = tree.ray_cast(v.co + n * 0.001, ray, 0.08)[3]
            if hit_dist is not None:
                ao += (1.0 - hit_dist / 0.08) / 14.0
        shade = max(0.70, min(1.0, 1.0 - 0.34 * ao))
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
# 1. `prop_coffee_machine.glb` — Commercial Studio Espresso Station
#    Exact overall bounds: ~0.85m wide (X) x 1.39m tall (Y_rl / Z_bl) x 0.68m deep (Z_rl / Y_bl)
#    Origin at bottom-center (Y_rl = 0), front facing +Z in glTF (-Y in Blender)
# ==============================================================================
def build_prop_coffee_machine():
    clear_scene()
    tex_walnut = make_walnut_texture()
    tex_steel = make_brushed_steel_texture("EspressoSteelTex", (0.78, 0.81, 0.85))
    tex_slate = make_brushed_steel_texture("EspressoSlateTex", (0.14, 0.16, 0.20))

    m_walnut = make_mat("CabinetWalnut", (1.0, 1.0, 1.0, 1.0), metallic=0.08, roughness=0.36, image=tex_walnut)
    m_steel = make_mat("BrushedStainless", (1.0, 1.0, 1.0, 1.0), metallic=0.88, roughness=0.22, image=tex_steel)
    m_slate = make_mat("MatteGraphite", (1.0, 1.0, 1.0, 1.0), metallic=0.55, roughness=0.34, image=tex_slate)
    m_brass = make_mat("WarmBrushedBrass", (0.86, 0.66, 0.28, 1.0), metallic=0.90, roughness=0.20)
    m_ceramic = make_mat("MatteCeramicCup", (0.94, 0.93, 0.90, 1.0), metallic=0.04, roughness=0.28)
    m_dial = make_mat("GaugeDialIvory", (0.95, 0.92, 0.84, 1.0), metallic=0.05, roughness=0.25)
    m_red_sw = make_mat(
        "PowerRockerRed",
        (0.92, 0.14, 0.16, 1.0),
        metallic=0.10,
        roughness=0.22,
        emission_rgba=(0.95, 0.18, 0.14, 1.0),
        emission_strength=0.55,
    )

    # --------------------------------------------------------------------------
    # A. LOWER CABINET (Width 0.85m, Depth 0.68m, Height 0.0 -> 0.86m)
    # --------------------------------------------------------------------------
    # 4 Adjustable brushed-steel leveling feet (Y_rl = 0.0 -> 0.05m)
    for fx in (-0.36, 0.36):
        for fz in (-0.27, 0.27):
            add_cyl(f"CabinetFoot_{fx}_{fz}", (fx, 0.025, fz), 0.026, 0.032, 0.050, m_steel, verts=20, bevel=0.003)
    # Recessed dark graphite kick-plinth (Y_rl = 0.04 -> 0.10m)
    add_box("CabinetKickPlinth", (0.0, 0.070, -0.01), (0.78, 0.060, 0.60), m_slate, bevel=0.008)

    # Main dark matte walnut cabinet body (X: 0.83m, Y_rl: 0.10..0.82m -> center 0.46, height 0.72, Z_rl: 0.64m)
    add_box("WalnutCabinetBody", (0.0, 0.460, -0.01), (0.83, 0.720, 0.64), m_walnut, bevel=0.014, segments=3, uv_scale=1.2)
    # Brushed graphite corner pilasters
    for sx in (-0.412, 0.412):
        for sz in (-0.322, 0.302):
            add_box(f"CabinetPilaster_{sx}_{sz}", (sx, 0.460, sz), (0.024, 0.722, 0.024), m_slate, bevel=0.004)

    # Dual front walnut cabinet doors (+Z_rl = +0.32m) with reveal gap, upper knock-box drawer & brushed brass pulls
    add_box("KnockBoxDrawerFront", (0.0, 0.745, 0.320), (0.77, 0.115, 0.022), m_slate, bevel=0.006)
    add_box("DrawerBrassPull", (0.0, 0.745, 0.338), (0.18, 0.014, 0.018), m_brass, bevel=0.003)

    for side in (-1, 1):
        dx = side * 0.196
        add_box(f"CabinetDoor_{side}", (dx, 0.390, 0.320), (0.378, 0.555, 0.022), m_walnut, bevel=0.007, uv_scale=1.4)
        # Vertical brushed brass handle bar near the center seam
        hx = side * 0.042
        add_cyl(f"DoorHandleBar_{side}", (hx, 0.440, 0.344), 0.007, 0.007, 0.180, m_brass, verts=16, bevel=0.002)
        for hy in (0.375, 0.505):
            add_cyl(f"DoorHandleStandoff_{side}_{hy}", (hx, hy, 0.333), 0.005, 0.005, 0.020, m_brass, rot_rl=(math.pi / 2, 0, 0), verts=12, bevel=0.001)

    # Thick overhanging matte-graphite & stainless countertop slab (Width 0.85m, Depth 0.68m, Y_rl = 0.82..0.865m)
    add_box("CountertopSlab", (0.0, 0.842, 0.0), (0.85, 0.045, 0.68), m_slate, bevel=0.010, segments=3)
    add_box("CountertopBrassInlay", (0.0, 0.842, 0.0), (0.854, 0.010, 0.684), m_brass, bevel=0.002)

    # --------------------------------------------------------------------------
    # B. COMMERCIAL DUAL-GROUP ESPRESSO MACHINE (Y_rl = 0.865 -> 1.390m)
    # --------------------------------------------------------------------------
    # 4 short chrome machine feet on the counter
    for mx in (-0.30, 0.30):
        for mz in (-0.20, 0.16):
            add_cyl(f"MachineFoot_{mx}_{mz}", (mx, 0.878, mz), 0.022, 0.026, 0.028, m_steel, verts=18, bevel=0.002)

    # Main lower chassis & perforated stainless steel drip tray basin (+Z_rl front)
    add_box("EspressoBaseChassis", (0.0, 0.945, -0.04), (0.70, 0.110, 0.44), m_steel, bevel=0.014, segments=3)
    add_box("DripTrayBasin", (0.0, 0.912, 0.215), (0.66, 0.046, 0.14), m_slate, bevel=0.008)
    add_box("DripTrayPerfPlate", (0.0, 0.936, 0.215), (0.63, 0.006, 0.12), m_steel, bevel=0.002)
    for slot_i in range(11):
        sx = -0.26 + slot_i * 0.052
        add_box(f"DripGrateSlot_{slot_i}", (sx, 0.938, 0.215), (0.028, 0.005, 0.088), m_slate, bevel=0.001)

    # Sculpted curved side cheeks (dark matte walnut + brushed steel trim) & upper boiler hood
    for side in (-1, 1):
        add_box(f"MachineSideCheek_{side}", (side * 0.342, 1.085, -0.03), (0.032, 0.340, 0.45), m_slate, bevel=0.012, segments=3)
        add_box(f"MachineWalnutInlay_{side}", (side * 0.354, 1.085, -0.03), (0.014, 0.260, 0.36), m_walnut, bevel=0.006)

    # Rear boiler tower & overhang group-head valance
    add_box("BoilerBackTower", (0.0, 1.095, -0.105), (0.66, 0.330, 0.30), m_steel, bevel=0.018, segments=4)
    add_box("UpperGroupValance", (0.0, 1.195, 0.055), (0.66, 0.125, 0.22), m_steel, bevel=0.016, segments=4)
    add_box("FrontControlFascia", (0.0, 1.200, 0.164), (0.62, 0.092, 0.014), m_slate, bevel=0.004)
    add_box("SplashBackPanel", (0.0, 1.050, 0.048), (0.64, 0.175, 0.012), m_steel, bevel=0.003)

    # Central Analog Dual-Scale Boiler Pressure Gauge + Clean Red Power Rocker Switch + Shot Buttons
    add_cyl("PressureGaugeBezel", (0.0, 1.200, 0.172), 0.036, 0.038, 0.016, m_brass, rot_rl=(math.pi / 2, 0, 0), verts=28, bevel=0.002)
    add_cyl("PressureGaugeFace", (0.0, 1.200, 0.178), 0.030, 0.030, 0.006, m_dial, rot_rl=(math.pi / 2, 0, 0), verts=24, bevel=0.001)
    add_box("GaugeNeedle1", (-0.006, 1.205, 0.182), (0.018, 0.003, 0.003), m_red_sw, rot_rl=(0, 0, 0.45), bevel=0.0005)
    add_box("GaugeNeedle2", (0.006, 1.195, 0.182), (0.016, 0.003, 0.003), m_slate, rot_rl=(0, 0, -0.35), bevel=0.0005)

    # Illuminated Red Power Rocker Switch on left fascia
    add_box("PowerSwitchBezel", (-0.255, 1.200, 0.172), (0.032, 0.044, 0.012), m_slate, bevel=0.003)
    add_box("PowerRockerRed", (-0.255, 1.200, 0.179), (0.020, 0.030, 0.010), m_red_sw, rot_rl=(0.18, 0, 0), bevel=0.002)

    # --------------------------------------------------------------------------
    # C. DUAL BRASS/CHROME E61 GROUP HEADS & PORTAFILTERS WITH DARK WALNUT HANDLES
    # --------------------------------------------------------------------------
    for g_idx, gx in enumerate((-0.165, 0.165)):
        # Row of 3 brushed-brass brew dose buttons above each group head
        for b_i in range(3):
            bx = gx + (b_i - 1) * 0.034
            add_cyl(f"DoseBtn_{g_idx}_{b_i}", (bx, 1.200, 0.173), 0.009, 0.010, 0.010, m_brass, rot_rl=(math.pi / 2, 0, 0), verts=16, bevel=0.001)

        # Heavy chrome/brass E61 group head bell & bayonet ring
        add_cyl(f"GroupBellTop_{g_idx}", (gx, 1.122, 0.125), 0.044, 0.048, 0.045, m_steel, verts=28, bevel=0.004)
        add_cyl(f"GroupBrassCollar_{g_idx}", (gx, 1.092, 0.125), 0.046, 0.046, 0.018, m_brass, verts=28, bevel=0.002)
        add_cyl(f"PortafilterBasket_{g_idx}", (gx, 1.066, 0.125), 0.038, 0.043, 0.034, m_steel, verts=24, bevel=0.003)
        # Dual espresso spouts underneath basket
        for sp_side in (-1, 1):
            add_cyl(f"Spout_{g_idx}_{sp_side}", (gx + sp_side * 0.012, 1.040, 0.125), 0.005, 0.006, 0.022, m_brass, verts=12, bevel=0.001)

        # Angled chrome portafilter tang + turned dark-walnut ergonomic handle sticking out forward (+Z_rl)
        yaw_pf = -0.14 if g_idx == 0 else 0.14
        dx_pf = math.sin(yaw_pf)
        dz_pf = math.cos(yaw_pf)
        add_tube_curve(
            f"PortafilterHandle_{g_idx}",
            [
                ((gx + dx_pf * 0.038, 1.066, 0.125 + dz_pf * 0.038), 0.010),
                ((gx + dx_pf * 0.068, 1.062, 0.125 + dz_pf * 0.068), 0.011),
                ((gx + dx_pf * 0.115, 1.055, 0.125 + dz_pf * 0.115), 0.016),
                ((gx + dx_pf * 0.175, 1.048, 0.125 + dz_pf * 0.175), 0.018),
                ((gx + dx_pf * 0.195, 1.046, 0.125 + dz_pf * 0.195), 0.012),
            ],
            m_walnut,
            sides=18,
        )
        add_cyl(
            f"PortafilterFerrule_{g_idx}",
            (gx + dx_pf * 0.056, 1.064, 0.125 + dz_pf * 0.056),
            0.012, 0.012, 0.024, m_brass,
            rot_rl=(math.pi / 2 + 0.08, 0, -yaw_pf), verts=16, bevel=0.001,
        )

        # Ceramic espresso demitasse cup sitting on the drip grate under each group head
        add_cyl(f"BrewCup_{g_idx}", (gx, 0.966, 0.135), 0.024, 0.032, 0.054, m_ceramic, verts=22, bevel=0.002)
        add_box(f"BrewCupHandle_{g_idx}", (gx + 0.032, 0.966, 0.135), (0.014, 0.030, 0.007), m_ceramic, bevel=0.002)

    # --------------------------------------------------------------------------
    # D. ARTICULATED STAINLESS STEAM WAND, HOT WATER SPOUT & WALNUT STEAM KNOBS
    # --------------------------------------------------------------------------
    for side in (-1, 1):
        kx = side * 0.285
        # Brushed brass steam valve turret & dark walnut rotary knob
        add_cyl(f"SteamValveCollar_{side}", (kx, 1.185, 0.172), 0.016, 0.018, 0.018, m_brass, rot_rl=(math.pi / 2, 0, 0), verts=18, bevel=0.002)
        add_cyl(f"SteamWalnutKnob_{side}", (kx, 1.185, 0.192), 0.024, 0.021, 0.024, m_walnut, rot_rl=(math.pi / 2, 0, 0), verts=20, bevel=0.003)
        add_cyl(f"SteamKnobCap_{side}", (kx, 1.185, 0.205), 0.010, 0.010, 0.004, m_brass, rot_rl=(math.pi / 2, 0, 0), verts=16, bevel=0.001)

        # Articulated stainless steel steam wand (left) & hot water wand (right)
        add_tube_curve(
            f"SteamWand_{side}",
            [
                ((side * 0.275, 1.125, 0.125), 0.0075),
                ((side * 0.292, 1.075, 0.155), 0.0060),
                ((side * 0.280, 1.015, 0.185), 0.0055),
                ((side * 0.268, 0.968, 0.195), 0.0070),
            ],
            m_steel,
            sides=14,
        )
        # Black rubber anti-burn grip sleeve on the upper wand
        add_tube_curve(
            f"WandRubberGrip_{side}",
            [
                ((side * 0.284, 1.100, 0.140), 0.0095),
                ((side * 0.288, 1.055, 0.165), 0.0095),
            ],
            m_slate,
            sides=14,
        )

    # Stainless milk frothing pitcher resting on right side of counter
    add_cyl("MilkPitcherBody", (0.355, 0.915, 0.165), 0.038, 0.044, 0.100, m_steel, verts=24, bevel=0.003)
    add_box("MilkPitcherHandle", (0.398, 0.915, 0.165), (0.018, 0.056, 0.008), m_steel, bevel=0.002)

    # --------------------------------------------------------------------------
    # E. TOP CUP WARMER TRAY, TUBULAR GUARD RAIL & STACKED MATTE CERAMIC CUPS (Y_rl up to 1.390m)
    # --------------------------------------------------------------------------
    add_box("CupWarmerDeck", (0.0, 1.262, -0.025), (0.64, 0.016, 0.39), m_steel, bevel=0.005)
    # Perimeter tubular stainless guard rail around the top cup warmer (Y_rl = 1.298m)
    rail_pts = [
        ((-0.305, 1.298,  0.145), 0.006),
        ((-0.305, 1.298, -0.205), 0.006),
        (( 0.000, 1.298, -0.205), 0.006),
        (( 0.305, 1.298, -0.205), 0.006),
        (( 0.305, 1.298,  0.145), 0.006),
    ]
    add_tube_curve("CupWarmerGuardRail", rail_pts, m_steel, sides=12, steps_per_span=2)
    for rx, rz in ((-0.305, 0.135), (-0.305, -0.195), (0.0, -0.195), (0.305, -0.195), (0.305, 0.135)):
        add_cyl(f"RailPost_{rx}_{rz}", (rx, 1.282, rz), 0.007, 0.007, 0.032, m_brass, verts=12, bevel=0.001)

    # 4 Stacks of inverted/nested matte ceramic cappuccino cups on the top warmer reaching Y_rl = 1.390m
    for s_idx, (cx, cz, n_cups) in enumerate((
        (-0.21, -0.08, 2),
        (-0.07, -0.08, 2),
        ( 0.07, -0.08, 2),
        ( 0.21, -0.08, 2),
        (-0.14,  0.04, 1),
        ( 0.14,  0.04, 1),
    )):
        # Saucer at bottom of stack
        add_cyl(f"Saucer_{s_idx}", (cx, 1.274, cz), 0.046, 0.034, 0.010, m_ceramic, verts=22, bevel=0.002)
        for c_level in range(n_cups):
            cy = 1.304 + c_level * 0.056
            add_cyl(f"WarmCup_{s_idx}_{c_level}", (cx, cy, cz), 0.028, 0.038, 0.056, m_ceramic, verts=22, bevel=0.003)
            add_cyl(f"WarmCupRim_{s_idx}_{c_level}", (cx, cy + 0.027, cz), 0.039, 0.039, 0.004, m_brass if c_level == n_cups - 1 else m_ceramic, verts=22, bevel=0.001)

    bake_ao_and_export("prop_coffee_machine.glb")


# ==============================================================================
# 2. `prop_extinguisher.glb` — High-Poly CO2 Fire Extinguisher on Matte-Slate Floor Stand
#    Exact overall height: ~0.82m (Y_rl in [0.0, 0.82]), diameter ~0.24m (X, Z_rl in [-0.12, +0.12])
#    Origin at bottom-center (Y_rl = 0), front facing +Z in glTF (-Y in Blender)
# ==============================================================================
def build_prop_extinguisher():
    clear_scene()
    tex_body = make_extinguisher_cylinder_texture()
    tex_slate = make_brushed_steel_texture("StandSlateTex", (0.15, 0.17, 0.21))

    m_red = make_mat("CrimsonExtinguisherSteel", (1.0, 1.0, 1.0, 1.0), metallic=0.22, roughness=0.30, image=tex_body)
    m_red_plain = make_mat("CrimsonDomeSteel", (0.34, 0.022, 0.026, 1.0), metallic=0.22, roughness=0.30)
    m_slate = make_mat("MatteSlateStand", (1.0, 1.0, 1.0, 1.0), metallic=0.48, roughness=0.38, image=tex_slate)
    m_brass = make_mat("ValveBrushedBrass", (0.88, 0.68, 0.24, 1.0), metallic=0.92, roughness=0.18)
    m_chrome = make_mat("SafetyPinChrome", (0.86, 0.89, 0.93, 1.0), metallic=0.92, roughness=0.16)
    m_rubber = make_mat("BraidedHoseHornBlack", (0.07, 0.08, 0.10, 1.0), metallic=0.08, roughness=0.52)
    m_gauge = make_mat("GaugeGreenZone", (0.18, 0.86, 0.38, 1.0), metallic=0.05, roughness=0.20, emission_strength=0.35)

    # --------------------------------------------------------------------------
    # A. LOW MATTE-SLATE FLOOR STAND (Diameter 0.24m -> radius 0.12m, Y_rl = 0.0 -> 0.16m)
    # --------------------------------------------------------------------------
    # 4 rubber floor pads at radius ~0.105m (overall X, Z_rl in [-0.120, +0.120])
    for k in range(4):
        ang = math.radians(45 + k * 90)
        px, pz = 0.096 * math.cos(ang), 0.096 * math.sin(ang)
        add_cyl(f"StandRubberFoot_{k}", (px, 0.008, pz), 0.022, 0.024, 0.016, m_rubber, verts=18, bevel=0.002)

    # Heavy chamfered matte-slate circular floor tray & retaining collar ring
    add_cyl("StandBaseDisc", (0.0, 0.028, 0.0), 0.120, 0.114, 0.032, m_slate, verts=40, bevel=0.005)
    add_cyl("StandCupWall", (0.0, 0.078, 0.0), 0.102, 0.104, 0.076, m_slate, verts=40, bevel=0.004)
    add_cyl("StandBrassTrimRing", (0.0, 0.116, 0.0), 0.106, 0.106, 0.008, m_brass, verts=40, bevel=0.002)
    # Rear vertical stabilizer spine on the floor stand (Y_rl = 0.04..0.50m)
    add_box("StandRearSpine", (0.0, 0.265, -0.102), (0.048, 0.460, 0.018), m_slate, bevel=0.004)
    add_cyl("StandUpperCradle", (0.0, 0.485, 0.0), 0.095, 0.095, 0.022, m_slate, verts=40, bevel=0.003)
    add_box("StandLatchBuckle", (0.0, 0.485, 0.097), (0.030, 0.024, 0.010), m_brass, bevel=0.002)

    # --------------------------------------------------------------------------
    # B. HIGH-POLY SEAMLESS CO2 STEEL CYLINDER WITH CHAMFERED SHOULDER & BASE (Y_rl = 0.035 -> 0.675m)
    # --------------------------------------------------------------------------
    cyl_profile = [
        # (y_rl,  radius)
        (0.035, 0.010),
        (0.038, 0.078),
        (0.048, 0.088),
        (0.065, 0.091),
        (0.515, 0.091),
        (0.545, 0.088),
        (0.578, 0.078),
        (0.608, 0.062),
        (0.635, 0.042),
        (0.658, 0.028),
        (0.678, 0.026),
    ]
    n_seg = 48
    c_verts, c_faces = [], []
    for yh, rad in cyl_profile:
        for i in range(n_seg):
            a = i * math.tau / n_seg
            # Place seam at back (-Z_rl) and sweep clockwise so u=0.5 faces directly forward (+Z_rl -> -Y_bl)
            # and u increases from left (-X) to right (+X) across the front face!
            c_verts.append(rl_pos((-rad * math.sin(a), yh, -rad * math.cos(a))))
    for k in range(len(cyl_profile) - 1):
        for i in range(n_seg):
            c_faces.append((k * n_seg + i, k * n_seg + (i + 1) % n_seg, (k + 1) * n_seg + (i + 1) % n_seg, (k + 1) * n_seg + i))
    c_faces.append(tuple(reversed(range(n_seg))))
    c_faces.append(tuple(range((len(cyl_profile) - 1) * n_seg, len(cyl_profile) * n_seg)))
    c_mesh = bpy.data.meshes.new("ExtinguisherCylinder")
    c_mesh.from_pydata(c_verts, [], c_faces)
    c_mesh.update()
    c_obj = bpy.data.objects.new("ExtinguisherCylinder", c_mesh)
    bpy.context.collection.objects.link(c_obj)
    c_mesh.materials.append(m_red)  # single unified crimson texture across cylinder & dome!
    uv = c_mesh.uv_layers.new(name="UVMap")
    for k in range(len(cyl_profile) - 1):
        for i in range(n_seg):
            poly = c_mesh.polygons[k * n_seg + i]
            poly.use_smooth = True
            for l_idx in poly.loop_indices:
                v_idx = c_mesh.loops[l_idx].vertex_index
                row, col = divmod(v_idx, n_seg)
                if i == n_seg - 1 and col == 0:
                    col = n_seg
                u_val = col / n_seg
                # Map full height 0.035..0.678 -> v in [0.02, 0.96] so label (v in 0.18..0.62) sits on straight cylinder wall
                v_val = 0.02 + 0.94 * ((cyl_profile[row][0] - 0.035) / (0.678 - 0.035))
                uv.data[l_idx].uv = (u_val, v_val)
    for cap_p in c_mesh.polygons[(len(cyl_profile) - 1) * n_seg:]:
        cap_p.use_smooth = True
        for l_idx in cap_p.loop_indices:
            uv.data[l_idx].uv = (0.10, 0.95)

    # --------------------------------------------------------------------------
    # C. BRUSHED BRASS VALVE ASSEMBLY, SQUEEZE LEVER, SAFETY PIN & PRESSURE GAUGE (Y_rl = 0.675 -> 0.820m)
    # --------------------------------------------------------------------------
    # Hexagonal brass neck nut & valve body tower
    add_cyl("BrassNeckNut", (0.0, 0.684, 0.0), 0.032, 0.032, 0.022, m_brass, verts=6, bevel=0.003)
    add_cyl("BrassValveBody", (0.0, 0.724, 0.0), 0.024, 0.026, 0.064, m_brass, verts=28, bevel=0.003)
    add_cyl("BrassValveCap", (0.0, 0.762, 0.0), 0.022, 0.018, 0.018, m_brass, verts=24, bevel=0.002)

    # Readable Front Pressure Gauge Dial (+Z_rl = +0.036m, Y_rl = 0.726m)
    add_cyl("GaugeHousing", (0.0, 0.726, 0.026), 0.021, 0.022, 0.018, m_brass, rot_rl=(math.pi / 2, 0, 0), verts=24, bevel=0.002)
    add_cyl("GaugeDialFace", (0.0, 0.726, 0.036), 0.017, 0.017, 0.004, m_gauge, rot_rl=(math.pi / 2, 0, 0), verts=24, bevel=0.001)
    add_box("GaugeNeedle", (0.002, 0.730, 0.039), (0.011, 0.0025, 0.002), m_red_plain, rot_rl=(0, 0, 0.55), bevel=0.0004)

    # Chrome Safety Pin & Pull Ring on the right side of the valve (+X = +0.038m)
    add_cyl("SafetyPinShaft", (0.018, 0.742, 0.006), 0.003, 0.003, 0.044, m_chrome, rot_rl=(0, 0, math.pi / 2), verts=12, bevel=0.0005)
    bpy.ops.mesh.primitive_torus_add(
        major_segments=24, minor_segments=10,
        major_radius=0.018, minor_radius=0.0032,
        location=rl_pos((0.046, 0.738, 0.006)),
        rotation=(math.pi / 2, 0, 0),
    )
    ring = bpy.context.active_object
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    ring.data.materials.append(m_chrome)
    apply_box_uv_and_smooth(ring)

    # Fixed Lower Carry Handle (dark slate steel) & Upper Crimson Squeeze Trigger Lever reaching Y_rl = 0.820m
    add_tube_curve(
        "LowerCarryHandle",
        [
            ((0.0, 0.736, -0.015), 0.010),
            ((0.0, 0.732, -0.055), 0.011),
            ((0.0, 0.715, -0.095), 0.011),
            ((0.0, 0.692, -0.114), 0.009),
        ],
        m_slate,
        sides=14,
    )
    add_tube_curve(
        "UpperSqueezeLever",
        [
            ((0.0, 0.764,  0.005), 0.010),
            ((0.0, 0.808, -0.038), 0.012),
            ((0.0, 0.798, -0.082), 0.012),
            ((0.0, 0.768, -0.112), 0.009),
        ],
        m_red_plain,
        sides=14,
    )

    # --------------------------------------------------------------------------
    # D. BRAIDED RUBBER HOSE & FLARED MATTE-BLACK CO2 DISCHARGE HORN
    #    Stays neatly within the 0.24m diameter envelope (X, Z_rl in [-0.12, +0.12])
    # --------------------------------------------------------------------------
    add_cyl("HoseBrassElbow", (-0.026, 0.720, 0.0), 0.011, 0.011, 0.022, m_brass, rot_rl=(0, 0, math.pi / 2), verts=16, bevel=0.002)
    add_tube_curve(
        "BraidedRubberHose",
        [
            ((-0.036, 0.720,  0.000), 0.0095),
            ((-0.078, 0.710,  0.005), 0.0095),
            ((-0.104, 0.650,  0.012), 0.0095),
            ((-0.106, 0.545,  0.020), 0.0095),
            ((-0.096, 0.455,  0.026), 0.0110),
        ],
        m_rubber,
        sides=16,
    )
    add_cyl("HornBrassCoupling", (-0.094, 0.440, 0.027), 0.013, 0.013, 0.024, m_brass, verts=18, bevel=0.002)

    # Flared conical matte-black CO2 discharge horn clipped vertically alongside the left flank
    add_tube_curve(
        "Co2DischargeHorn",
        [
            ((-0.094, 0.428, 0.027), 0.014),
            ((-0.094, 0.375, 0.030), 0.022),
            ((-0.094, 0.295, 0.034), 0.034),
            ((-0.094, 0.215, 0.038), 0.042),
            ((-0.094, 0.195, 0.039), 0.043),
        ],
        m_rubber,
        sides=24,
    )
    # Deep dark internal nozzle recess at bottom of CO2 horn
    add_cyl("HornInnerBore", (-0.094, 0.198, 0.039), 0.036, 0.024, 0.016, m_slate, verts=22, bevel=0.001)

    bake_ao_and_export("prop_extinguisher.glb")


def render_3angle_proof(glb_name, png_name, ortho_scale, target_z, cam_dist=2.4, cam_height=1.15, spacing_mult=0.33):
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

    # Studio floor plinth
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
    build_prop_coffee_machine()
    build_prop_extinguisher()
    render_3angle_proof("prop_coffee_machine.glb", "coffee_machine_proof.png", ortho_scale=3.85, target_z=0.68, cam_dist=2.8, cam_height=1.18, spacing_mult=0.33)
    render_3angle_proof("prop_extinguisher.glb", "extinguisher_proof.png", ortho_scale=1.95, target_z=0.41, cam_dist=2.2, cam_height=0.68, spacing_mult=0.33)
