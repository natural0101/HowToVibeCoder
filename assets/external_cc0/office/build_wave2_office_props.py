"""
Office Realism Wave 2 Props Builder:
  1. `assets/models/prop_vending_machine.glb` (Facing `+Z`, footprint `1.18m × 2.16m × 0.90m`, Y=0 floor)
  2. `assets/models/prop_filing_cabinet.glb`  (Facing `-X`, footprint `0.93m × 1.85m × 1.12m`, Y=0 floor)
  3. `assets/models/prop_trash_bin.glb`       (Cylindrical `0.39m × 0.51m × 0.39m`, Y=0 floor)
  4. Renders 3-panel proof `office_wave2_props_proof.png` with `use_backface_culling = True`.
"""

import math
import os
from pathlib import Path
import bmesh
import bpy
import numpy as np
from mathutils import Euler, Vector as V
from mathutils.bvhtree import BVHTree


ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets" / "models"
ARTIFACT_PROOF_1 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\office_wave2_props_proof.png")
ARTIFACT_PROOF_2 = Path(r"C:\Users\se-20\.gemini\antigravity\brain\34a5dbd6-7dba-4b8d-8112-53de935b5112\scratch\office_wave2_props_proof.png")
SCREENSHOT_PROOF = ROOT / "screenshots" / "office_wave2_props_proof.png"

MATS = {}


def xyz(p):
    """Game/Raylib (x, y_up, z_forward) -> Blender (x, -z_forward, y_up)."""
    return (p[0], -p[2], p[1])


def srgb_to_linear(c):
    c = np.clip(c, 0.0, 1.0)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


def build_ao_swatch_texture(name, hex_col, pattern=None):
    existing = bpy.data.images.get(name)
    if existing is not None:
        return existing
    W, H = 128, 128
    rgb = [int(hex_col[i : i + 2], 16) / 255.0 for i in (0, 2, 4)]
    lin_rgb = srgb_to_linear(np.array(rgb, dtype=np.float32))
    arr = np.ones((H, W, 4), dtype=np.float32)
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u = x / float(W - 1)
    v = y / float(H - 1)

    # Smooth AO + convex edge curve along u (`0.22` in deep crevices -> `1.0` at flat wall -> `1.32` on beveled edge)
    ao_part = np.clip(u / 0.62, 0.0, 1.0)
    edge_part = np.clip((u - 0.62) / 0.38, 0.0, 1.0)
    shade = (0.22 + 0.78 * (ao_part ** 1.30)) * (1.0 + 0.32 * (edge_part ** 1.5))

    rng = np.random.default_rng(2026 + sum(ord(c) for c in name))
    grain = 1.0 + 0.035 * rng.normal(0.0, 1.0, (H, W)).astype(np.float32) + 0.025 * np.sin(y * 0.9)

    if pattern == "mesh":
        # High-contrast silver-on-charcoal diamond wire-mesh perforation pattern for the office trash bin wall
        d1 = np.abs(np.sin((x + y) * 0.42))
        d2 = np.abs(np.sin((x - y) * 0.42))
        wire = np.maximum(np.exp(-((d1) / 0.22) ** 2), np.exp(-((d2) / 0.22) ** 2))
        hole_bg = np.array([0.008, 0.010, 0.014], dtype=np.float32)
        wire_fg = np.array([0.16, 0.19, 0.24], dtype=np.float32)
        w_mask = np.clip(wire, 0.0, 1.0)[..., None]
        arr[:, :, :3] = np.clip((hole_bg * (1.0 - w_mask) + wire_fg * w_mask) * (0.55 + 0.55 * u)[..., None], 0.002, 0.98)
        img = bpy.data.images.new(name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img
    elif pattern == "marquee":
        # High-contrast 90s/2026 cyber-office illuminated vending marquee ("B12 • 24/7")
        srgb = np.zeros((H, W, 3), dtype=np.float32)
        # Deep cyber-navy background with glowing cyan border & golden lightning/text bars
        srgb[:, :, 0] = 0.06 + 0.08 * np.sin(u * math.pi)
        srgb[:, :, 1] = 0.16 + 0.22 * np.sin(v * math.pi)
        srgb[:, :, 2] = 0.34 + 0.30 * np.sin(v * math.pi)
        border = (u < 0.05) | (u > 0.95) | (v < 0.10) | (v > 0.90)
        srgb[border] = np.array([0.15, 0.92, 1.00])
        # Bold golden-amber & neon-cyan graphic lettering blocks across the middle
        for bx0, bx1, col_b in [
            (0.10, 0.18, (1.0, 0.78, 0.18)),
            (0.20, 0.25, (1.0, 0.78, 0.18)),
            (0.27, 0.35, (1.0, 0.78, 0.18)),
            (0.42, 0.52, (0.20, 0.96, 1.00)),
            (0.55, 0.65, (0.20, 0.96, 1.00)),
            (0.68, 0.78, (0.98, 0.24, 0.38)),
            (0.81, 0.89, (1.0, 0.78, 0.18)),
        ]:
            mask_b = (u >= bx0) & (u <= bx1) & (v >= 0.28) & (v <= 0.72)
            srgb[mask_b] = np.array(col_b)
        hole_b = (v >= 0.44) & (v <= 0.54) & (u >= 0.12) & (u <= 0.87)
        srgb[hole_b] *= 0.35
        arr[:, :, :3] = srgb_to_linear(srgb)
        img = bpy.data.images.new(name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img
    elif pattern == "label_paper":
        # Cream index card with crisp dark printed heading + subline
        paper = np.ones((H, W, 3), dtype=np.float32) * lin_rgb[None, None, :]
        border = (u < 0.06) | (u > 0.94) | (v < 0.08) | (v > 0.92)
        line1 = (v > 0.52) & (v < 0.72) & (u > 0.14) & (u < 0.86) & ((np.sin(u * 48.0) > -0.4))
        line2 = (v > 0.26) & (v < 0.40) & (u > 0.20) & (u < 0.80)
        paper[border] *= 0.45
        paper[line1] = np.array([0.02, 0.02, 0.03])
        paper[line2] = np.array([0.18, 0.04, 0.05])
        arr[:, :, :3] = paper
        img = bpy.data.images.new(name, width=W, height=H, alpha=False)
        img.pixels.foreach_set(arr.ravel())
        img.pack()
        return img

    arr[:, :, :3] = np.clip(lin_rgb[None, None, :] * (shade * grain)[..., None], 0.001, 0.99)
    img = bpy.data.images.new(name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


MAT_LIBRARY = {
    # name: (hex_srgb, metallic, roughness, emissive, pattern)
    # Vending Machine
    "VendCab":        ("1C222E", 0.45, 0.36, None, None),
    "VendTrim":       ("343E4D", 0.68, 0.28, None, None),
    "VendChrome":     ("C4CCD6", 0.88, 0.18, None, None),
    "VendIn":         ("C8D4E4", 0.08, 0.25, (0.16, 0.20, 0.25), None),
    "VendShelf":      ("262D38", 0.58, 0.32, None, None),
    "VendScreen":     ("18D8F8", 0.05, 0.14, (0.28, 0.78, 0.95), "marquee"),
    "VendLedGreen":   ("24F078", 0.05, 0.14, (0.18, 0.88, 0.42), None),
    "VendFlap":       ("11151C", 0.30, 0.42, None, None),
    "VendGlassGlint": ("8CA8C4", 0.20, 0.08, (0.08, 0.12, 0.16), None),
    "CanRed":         ("D81E2B", 0.55, 0.22, None, None),
    "CanCyan":        ("12A8E8", 0.55, 0.22, None, None),
    "CanGold":        ("E8A818", 0.55, 0.22, None, None),
    "SnackEmerald":   ("189854", 0.25, 0.30, None, None),
    "SnackPurple":    ("6E28B8", 0.25, 0.30, None, None),
    # Filing Cabinet
    "CabSteel":       ("3A4350", 0.52, 0.36, None, None),
    "DrawerFront":    ("4C5766", 0.48, 0.32, None, None),
    "CabDarkRecess":  ("14181F", 0.25, 0.55, None, None),
    "ChromeHandle":   ("D0D8E2", 0.90, 0.18, None, None),
    "BrassFrame":     ("C89B3C", 0.85, 0.24, None, None),
    "LabelCard":      ("EAE3CE", 0.02, 0.55, None, "label_paper"),
    "FolderManila":   ("D4AD6A", 0.02, 0.58, None, None),
    "FolderRed":      ("A8242C", 0.04, 0.52, None, None),
    "StickyYellow":   ("F2D43B", 0.02, 0.48, None, "label_paper"),
    # Trash Bin
    "BinSteel":       ("262C36", 0.65, 0.34, None, "mesh"),
    "BinRim":         ("3A4250", 0.72, 0.28, None, None),
    "PaperWhite":     ("E6E4DC", 0.02, 0.62, None, None),
    "PaperBlue":      ("78A8D8", 0.02, 0.58, None, None),
    "CoffeeSleeve":   ("9E7248", 0.02, 0.65, None, None),
}


def mat(key):
    if key in MATS:
        return MATS[key]
    hex_col, metallic, roughness, emissive, pattern = MAT_LIBRARY[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    m.use_backface_culling = True
    tree = m.node_tree
    bsdf = tree.nodes.get("Principled BSDF")
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness

    tex = tree.nodes.new("ShaderNodeTexImage")
    tex.image = build_ao_swatch_texture(f"Wave2Tex_{key}", hex_col, pattern=pattern)
    tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])

    if emissive is not None:
        if key == "VendScreen" and "Emission Color" in bsdf.inputs:
            tree.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 0.65
        else:
            if "Emission Color" in bsdf.inputs:
                bsdf.inputs["Emission Color"].default_value = (*emissive, 1.0)
            if "Emission Strength" in bsdf.inputs:
                bsdf.inputs["Emission Strength"].default_value = 0.45

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


def add_box(name, p, size, key, bevel=0.006, segments=3, rot=(0.0, 0.0, 0.0)):
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


def add_cyl_y(name, p, radius, height, key, bevel=0.002, vertices=28, cone_r2=None, rot=(0.0, 0.0, 0.0)):
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
    if any(abs(a) > 1e-6 for a in rot):
        obj.rotation_euler = Euler((rot[0], -rot[2], rot[1]), "XYZ")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel > 0:
        bpy.context.view_layer.objects.active = obj
        mod = obj.modifiers.new("Bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.limit_method = 'ANGLE'
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return apply_weighted_smooth(obj)


def add_curve_tube(name, points, radius, key, res=6):
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


def finalize_and_export_prop(export_name, custom_uv_tag_key=None):
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    poly_tags = []
    for o in mesh_objs:
        tag = int(o.get("uv_mode", 0))
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
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
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

    # 24-ray BVHTree Ambient Occlusion + local edge curvature
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

    max_dist = 0.16
    vert_u = [0.62] * len(verts)
    vert_col = [1.0] * len(verts)

    for idx, v in enumerate(verts):
        p = v.co
        n = v.normal.normalized() if v.normal.length > 1e-6 else V((0.0, 0.0, 1.0))
        up = V((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else V((1.0, 0.0, 0.0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()
        origin = p + n * 0.0012
        occ = 0.0
        for ld in local_dirs:
            d = (t1 * ld.x + t2 * ld.y + n * ld.z).normalized()
            hit_loc, _, _, hit_dist = bvh.ray_cast(origin, d, max_dist)
            if hit_loc is not None and hit_dist is not None:
                occ += (1.0 - min(1.0, hit_dist / max_dist)) ** 1.35
        ao = max(0.0, 1.0 - (occ / n_rays) * 1.10)

        curv = 0.0
        if neighbors[idx]:
            acc = 0.0
            for nb in neighbors[idx]:
                dv = v_coords[nb] - p
                if dv.length > 1e-6:
                    acc += dv.normalized().dot(n)
            curv = max(-0.20, min(0.24, -(acc / len(neighbors[idx])) * 0.60))

        vert_u[idx] = min(0.96, max(0.04, 0.10 + 0.62 * (ao ** 1.25) + curv))
        vert_col[idx] = min(1.0, max(0.18, 0.18 + 0.82 * (ao ** 1.25)))

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    col_attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")

    mat_names = [m.name for m in mesh.materials]
    for poly in mesh.polygons:
        poly.use_smooth = True
        m_name = mat_names[poly.material_index]
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            v_co = verts[vi].co
            if m_name == "BinSteel":
                ang = math.atan2(v_co.y, v_co.x) / math.tau + 0.5
                tu = (ang * 6.0) % 1.0
                tv = min(0.98, max(0.02, (v_co.z / 0.46) * 2.0 % 1.0))
                uv_layer.data[li].uv = (tu, tv)
            elif m_name == "VendScreen":
                tu = min(0.98, max(0.02, (v_co.x + 0.51) / 1.02))
                tv = min(0.98, max(0.02, (v_co.z - 1.94) / 0.14 if v_co.z > 1.85 else (v_co.z - 1.61) / 0.09))
                uv_layer.data[li].uv = (tu, tv)
            elif m_name in ("LabelCard", "StickyYellow"):
                tu = min(0.96, max(0.04, ((v_co.x + v_co.y) * 5.8) % 0.92 + 0.04))
                tv = min(0.96, max(0.04, (v_co.z * 18.0) % 0.92 + 0.04))
                uv_layer.data[li].uv = (tu, tv)
            else:
                tv = min(0.96, max(0.04, (v_co.z % 0.50) / 0.50))
                uv_layer.data[li].uv = (vert_u[vi], tv)
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


# ==============================================================================
# 1. PROP: VENDING MACHINE (`prop_vending_machine.glb`)
#    Footprint: X in [-0.59, +0.59], Y in [0.00, 2.16], Z in [-0.43, +0.475], Front = +Z
# ==============================================================================
def build_prop_vending_machine():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    # 1. Heavy-duty leveling feet (`y = 0.00..0.05`)
    for sx in (-0.50, 0.50):
        for sz in (-0.35, 0.35):
            add_cyl_y(f"LevelingFootPad_{sx}_{sz}", (sx, 0.012, sz), 0.036, 0.024, "VendFlap", bevel=0.004, vertices=24)
            add_cyl_y(f"LevelingFootStem_{sx}_{sz}", (sx, 0.036, sz), 0.020, 0.028, "VendChrome", bevel=0.002, vertices=20)

    # 2. Main Sculpted Dark-Steel Chassis with REAL Recessed Display Cavity (`x in [-0.59, +0.59], y in [0.04, 2.16], z in [-0.43, +0.43]`)
    cab = add_box("MainVendingChassis", (0.0, 1.10, -0.01), (1.18, 2.12, 0.84), "VendCab", bevel=0.032, segments=4)
    # Carve deep recessed showcase cavity (`x in [-0.51, +0.17], y in [0.58, 1.86], z in [0.16, 0.48]`)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz((-0.17, 1.22, 0.32)))
    cavity_cut = bpy.context.object
    cavity_cut.dimensions = (0.68, 0.32, 1.28)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    cavity_cut.data.materials.append(mat("VendCab"))
    bpy.context.view_layer.objects.active = cab
    mod = cab.modifiers.new("ShowcaseCavity", "BOOLEAN")
    mod.operation = 'DIFFERENCE'
    mod.object = cavity_cut
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(cavity_cut, do_unlink=True)

    # Carve bottom pickup chute recess (`x in [-0.50, +0.16], y in [0.20, 0.48], z in [0.22, 0.48]`)
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz((-0.17, 0.34, 0.35)))
    chute_cut = bpy.context.object
    chute_cut.dimensions = (0.66, 0.26, 0.28)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    chute_cut.data.materials.append(mat("VendCab"))
    bpy.context.view_layer.objects.active = cab
    mod2 = cab.modifiers.new("ChuteCavity", "BOOLEAN")
    mod2.operation = 'DIFFERENCE'
    mod2.object = chute_cut
    bpy.ops.object.modifier_apply(modifier=mod2.name)
    bpy.data.objects.remove(chute_cut, do_unlink=True)
    apply_weighted_smooth(cab)

    # 3. Illuminated Backlit Interior Panels & Vertical LED Light Strips inside the Showcase Bay
    add_box("InteriorBackWall", (-0.17, 1.22, 0.175), (0.67, 1.26, 0.02), "VendIn", bevel=0.003)
    for sx in (-0.495, 0.155):
        add_box(f"InteriorLedStrip_{sx}", (sx, 1.22, 0.36), (0.014, 1.22, 0.018), "VendScreen", bevel=0.002)

    # 4. Outer Brushed-Steel Window Frame & Glossy Glass Reflection Strips
    add_box("WindowFrameTop",    (-0.17, 1.875, 0.428), (0.74, 0.038, 0.036), "VendTrim", bevel=0.006, segments=3)
    add_box("WindowFrameBottom", (-0.17, 0.565, 0.428), (0.74, 0.038, 0.036), "VendTrim", bevel=0.006, segments=3)
    for sx in (-0.525, 0.185):
        add_box(f"WindowFrameSide_{sx}", (sx, 1.22, 0.428), (0.038, 1.34, 0.036), "VendTrim", bevel=0.006, segments=3)
    # Subtle diagonal glass sheen bars on the front pane (`z = +0.436`) so the window reads as glass without hiding the 3D cans/coils
    add_box("GlassGlintLeft",  (-0.46, 1.22, 0.436), (0.022, 1.20, 0.003), "VendGlassGlint", bevel=0.001)
    add_box("GlassGlintRight", ( 0.11, 1.22, 0.436), (0.014, 1.20, 0.003), "VendGlassGlint", bevel=0.001)

    # 5. 4 Interior Shelves (`sy = 0.68, 0.98, 1.28, 1.58`) with Chrome Spiral Coils, Energy Cans & Snack Bags
    can_mats = ["CanRed", "CanCyan", "CanGold"]
    snack_mats = ["SnackEmerald", "CanRed", "SnackPurple", "CanGold"]
    for s_idx, sy in enumerate((0.68, 0.98, 1.28, 1.58)):
        add_box(f"ShelfTray_{s_idx}", (-0.17, sy, 0.29), (0.66, 0.022, 0.23), "VendShelf", bevel=0.004)
        add_box(f"ShelfFrontLip_{s_idx}", (-0.17, sy + 0.014, 0.402), (0.66, 0.024, 0.012), "VendChrome", bevel=0.002)

        for c_idx in range(5):
            cx = -0.43 + c_idx * 0.13
            # Price tag card on front shelf lip
            add_box(f"PriceTag_{s_idx}_{c_idx}", (cx, sy + 0.014, 0.410), (0.042, 0.018, 0.004), "LabelCard", bevel=0.0008)
            # Chrome dispenser spiral wire coil on each lane
            coil_pts = []
            for k in range(17):
                t = k / 16.0
                ang = k * math.pi * 0.65
                coil_pts.append((cx + 0.026 * math.cos(ang), sy + 0.036 + 0.026 * math.sin(ang), 0.20 + t * 0.19))
            add_curve_tube(f"DispenserCoil_{s_idx}_{c_idx}", coil_pts, 0.0032, "VendChrome", res=5)

            if s_idx < 2:
                # Rows of 3D Contoured Aluminum Energy-Drink Cans (2 cans deep per lane)
                c_mat = can_mats[(s_idx * 2 + c_idx) % 3]
                for depth_i, cz in enumerate((0.365, 0.255)):
                    tilt = (0.18, 0.0, 0.08) if (s_idx == 1 and c_idx == 2 and depth_i == 0) else (0.0, 0.0, 0.0)
                    add_cyl_y(f"EnergyCan_{s_idx}_{c_idx}_{depth_i}", (cx, sy + 0.088, cz), 0.034, 0.132, c_mat, bevel=0.005, vertices=24, rot=tilt)
                    if tilt == (0.0, 0.0, 0.0):
                        add_cyl_y(f"CanTopRim_{s_idx}_{c_idx}_{depth_i}", (cx, sy + 0.155, cz), 0.031, 0.005, "VendChrome", bevel=0.001, vertices=24)
            else:
                # Sculpted Puffed Foil Chips Bags & Chocolate Bars on upper shelves
                s_mat = snack_mats[(s_idx + c_idx) % 4]
                for depth_i, cz in enumerate((0.360, 0.250)):
                    bag = add_box(f"SnackBag_{s_idx}_{c_idx}_{depth_i}", (cx, sy + 0.095, cz), (0.088, 0.145, 0.038), s_mat, bevel=0.010, segments=3, rot=(math.radians(-8.0), 0.0, 0.0))
                    # Pinch top & bottom crimp seals of foil snack bag
                    for v in bag.data.vertices:
                        if abs(v.co.z) > 0.055:
                            v.co.y *= 0.28
                            v.co.x *= 1.06
                    bag.data.update()
                    apply_weighted_smooth(bag)

    # 6. Top Illuminated Curved Marquee Sign (`y = 2.00, z = +0.435`)
    add_box("MarqueeBezelFrame", (0.0, 2.01, 0.428), (1.08, 0.18, 0.036), "VendTrim", bevel=0.008, segments=3)
    add_box("MarqueeGlowSign",   (0.0, 2.01, 0.444), (1.02, 0.135, 0.016), "VendScreen", bevel=0.004, segments=2)

    # 7. Right-Side Control & Payment Column (`x = +0.38, y in [0.56, 1.88], z = +0.435`)
    add_box("ControlColumnHousing", (0.39, 1.22, 0.430), (0.32, 1.32, 0.034), "VendTrim", bevel=0.008, segments=3)
    add_box("ControlRecessPlate",   (0.39, 1.28, 0.444), (0.25, 1.06, 0.012), "VendFlap", bevel=0.004, segments=2)

    # Glowing Digital Credit OLED Screen (`y = 1.66`)
    add_box("DigitalDisplayBezel",  (0.39, 1.66, 0.452), (0.19, 0.11, 0.012), "VendChrome", bevel=0.003)
    add_box("DigitalDisplayScreen", (0.39, 1.66, 0.457), (0.165, 0.082, 0.006), "VendScreen", bevel=0.001)

    # 12 Individual 3D Tactile Keypad Buttons (4 rows x 3 cols, `y = 1.34..1.52`)
    add_box("KeypadBackingPlate", (0.39, 1.43, 0.451), (0.15, 0.22, 0.010), "VendTrim", bevel=0.002)
    for r_i in range(4):
        for c_i in range(3):
            bx = 0.345 + c_i * 0.045
            by = 1.50 - r_i * 0.046
            add_box(f"KeypadBtn_{r_i}_{c_i}", (bx, by, 0.458), (0.032, 0.030, 0.008), "VendChrome", bevel=0.002)

    # Bill Acceptor with Glowing Green LED Arrows + Coin Slot + NFC Contactless Reader (`y = 0.92..1.22`)
    add_box("BillAcceptorBox",  (0.39, 1.18, 0.454), (0.16, 0.11, 0.022), "VendFlap", bevel=0.004)
    add_box("BillAcceptorSlot", (0.39, 1.18, 0.466), (0.11, 0.016, 0.006), "VendLedGreen", bevel=0.001)
    add_box("NfcTapReaderPad",  (0.39, 1.02, 0.454), (0.13, 0.11, 0.016), "CanGold", bevel=0.004)
    add_box("NfcTapScreen",     (0.39, 1.02, 0.462), (0.09, 0.07, 0.005), "VendFlap", bevel=0.001)
    add_box("CoinReturnCup",    (0.39, 0.84, 0.450), (0.10, 0.11, 0.018), "VendChrome", bevel=0.003)
    add_box("CoinReturnWell",   (0.39, 0.83, 0.456), (0.07, 0.07, 0.012), "VendFlap", bevel=0.002)

    # 8. Bottom Recessed Hinged Delivery Chute (`x = -0.17, y = 0.34, z = +0.42`)
    add_box("DeliveryChuteFrame", (-0.17, 0.34, 0.426), (0.72, 0.32, 0.032), "VendTrim", bevel=0.006, segments=3)
    add_box("DeliverySwingingFlap", (-0.17, 0.35, 0.415), (0.64, 0.25, 0.024), "VendFlap", bevel=0.006, segments=3, rot=(math.radians(-14.0), 0.0, 0.0))
    add_box("DeliveryPushBar", (-0.17, 0.32, 0.444), (0.52, 0.032, 0.022), "VendChrome", bevel=0.005, segments=3)
    # Lower front ventilation louvers (`y = 0.08..0.15`)
    for lv_i in range(3):
        add_box(f"LowerVentLouver_{lv_i}", (-0.17, 0.085 + lv_i * 0.026, 0.432), (0.64, 0.012, 0.012), "VendFlap", bevel=0.002)

    return finalize_and_export_prop("prop_vending_machine")


# ==============================================================================
# 2. PROP: LIVED-IN 4-DRAWER OFFICE FILING CABINET (`prop_filing_cabinet.glb`)
#    Footprint: X in [-0.4925, +0.4400], Y in [0.00, 1.85], Z in [-0.56, +0.56], Front = -X
# ==============================================================================
def build_prop_filing_cabinet():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    # 1. Base Plinth & Corner Leveling Glides (`y = 0.00..0.05`)
    add_box("CabinetBasePlinth", (0.0, 0.035, 0.0), (0.82, 0.07, 1.06), "CabDarkRecess", bevel=0.008, segments=3)

    # 2. Main Chamfered Dark-Slate Steel Cabinet Shell (`x in [-0.42, +0.44], y in [0.05, 1.83], z in [-0.55, +0.55]`)
    body = add_box("CabinetMainShell", (0.01, 0.94, 0.0), (0.86, 1.78, 1.10), "CabSteel", bevel=0.018, segments=4)
    # Carve a deep recess on the top-drawer bay (`y in [1.40, 1.76]`) so the ajar top drawer reveals real interior folders!
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=xyz((-0.24, 1.57, 0.0)))
    top_cut = bpy.context.object
    top_cut.dimensions = (0.42, 0.98, 0.35)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    top_cut.data.materials.append(mat("CabDarkRecess"))
    bpy.context.view_layer.objects.active = body
    mod = body.modifiers.new("OpenTopDrawerBay", "BOOLEAN")
    mod.operation = 'DIFFERENCE'
    mod.object = top_cut
    bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.data.objects.remove(top_cut, do_unlink=True)
    apply_weighted_smooth(body)

    # Top Overhanging Chamfered Steel Cap (`y = 1.82..1.85`)
    add_box("CabinetTopCapPlate", (0.0, 1.834, 0.0), (0.88, 0.032, 1.12), "CabSteel", bevel=0.008, segments=3)

    # Recessed Side Architectural Stamping Panels on `-Z` and `+Z` flanks
    for side_z in (-1, 1):
        add_box(f"SideStampedPanelFrame_{side_z}", (0.01, 0.94, side_z * 0.552), (0.72, 1.58, 0.012), "DrawerFront", bevel=0.004)
        add_box(f"SideStampedPanelInner_{side_z}", (0.01, 0.94, side_z * 0.556), (0.64, 1.48, 0.008), "CabSteel", bevel=0.003)

    # 3. 4 Lateral Archive Drawers on the `-X` Front Face (`dy = 0.25, 0.67, 1.09, 1.56`)
    drawer_heights = (0.25, 0.67, 1.09, 1.56)
    for d_idx, dy in enumerate(drawer_heights):
        # Top drawer (`d_idx == 3`) is pulled slightly ajar (`36 mm` further forward to `x = -0.456`, max handle at `-0.491`)
        dx = -0.456 if d_idx == 3 else -0.428

        # Dark shadow channel frame behind each drawer front
        add_box(f"DrawerShadowChannel_{d_idx}", (-0.418, dy, 0.0), (0.016, 0.395, 1.035), "CabDarkRecess", bevel=0.002)

        # Beveled Steel Drawer Front Panel (`width Z = 1.02 m, height Y = 0.38 m`)
        add_box(f"DrawerFrontPanel_{d_idx}", (dx, dy, 0.0), (0.036, 0.376, 1.015), "DrawerFront", bevel=0.010, segments=4)
        # Subtle recessed inner bevel panel on drawer face
        add_box(f"DrawerInnerRecess_{d_idx}", (dx - 0.016, dy - 0.012, 0.0), (0.008, 0.26, 0.92), "CabSteel", bevel=0.003)

        # Twin Brass Label Holder Frames & Printed Archive Index Cards (`z = -0.26, +0.26`)
        for sz in (-0.26, 0.26):
            add_box(f"BrassLabelFrame_{d_idx}_{sz}", (dx - 0.020, dy + 0.088, sz), (0.010, 0.058, 0.195), "BrassFrame", bevel=0.002)
            add_box(f"PaperLabelCard_{d_idx}_{sz}",  (dx - 0.024, dy + 0.088, sz), (0.006, 0.042, 0.172), "LabelCard",  bevel=0.001)

        # Sculpted 3D Brushed-Chrome Arch Pull Handles (2 handles per wide lateral drawer at `z = -0.26, +0.26`)
        for sz in (-0.26, 0.26):
            hx = dx - 0.018
            hy = dy - 0.038
            handle_pts = [
                (hx + 0.004, hy, sz - 0.095),
                (hx - 0.024, hy, sz - 0.082),
                (hx - 0.026, hy, sz),
                (hx - 0.024, hy, sz + 0.082),
                (hx + 0.004, hy, sz + 0.095),
            ]
            add_curve_tube(f"ChromePullHandle_{d_idx}_{sz}", handle_pts, 0.0085, "ChromeHandle", res=6)

        # Chrome cylinder lock on the right corner of each drawer (`z = +0.44`)
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=20,
            radius=0.012,
            depth=0.014,
            location=xyz((dx - 0.018, dy + 0.125, 0.44)),
            rotation=(0.0, math.pi / 2.0, 0.0),
        )
        lock_obj = finish(bpy.context.object, f"DrawerKeyLock_{d_idx}", "ChromeHandle")
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        apply_weighted_smooth(lock_obj)

    # 4. Lived-In Details: Open Top Drawer Sides + Crooked Manila/Red Folders + Jammed Paper & Sticky Note!
    # Drawer side walls visible on the ajar top drawer (`d_idx = 3`, `y = 1.56`)
    for sz in (-0.48, 0.48):
        add_box(f"OpenDrawerSideWall_{sz}", (-0.36, 1.53, sz), (0.18, 0.28, 0.018), "CabSteel", bevel=0.003)
    add_box("OpenDrawerBottomFloor", (-0.35, 1.40, 0.0), (0.20, 0.016, 0.96), "CabDarkRecess", bevel=0.002)

    # Row of crooked Manila & Red Classified (`СЕКРЕТНО / NDA`) Folders sticking up inside the ajar top drawer
    folder_specs = [
        (-0.42, 1.65, -0.32, "FolderManila", (0.08, 0.02,  0.14)),
        (-0.41, 1.67, -0.18, "FolderRed",    (-0.06, -0.04, 0.22)),
        (-0.43, 1.66, -0.04, "PaperWhite",   (0.09, 0.03,  0.12)),
        (-0.41, 1.68,  0.12, "FolderManila", (-0.07, 0.02, 0.18)),
        (-0.40, 1.66,  0.28, "FolderRed",    (0.05, -0.03, 0.16)),
        (-0.42, 1.65,  0.39, "PaperWhite",   (-0.06, 0.05, 0.11)),
    ]
    for f_i, (fx, fy, fz, f_mat, f_rot) in enumerate(folder_specs):
        add_box(f"TopDrawerFolder_{f_i}", (fx, fy, fz), (0.014, 0.26, 0.22), f_mat, bevel=0.002, rot=f_rot)

    # Crooked yellow sticky note stuck on the 3rd drawer front (`y = 1.14, x = -0.448, z = -0.04`)
    add_box("DrawerStickyNote", (-0.447, 1.13, -0.03), (0.006, 0.075, 0.075), "StickyYellow", bevel=0.001, rot=(math.radians(9.0), 0.0, math.radians(-4.0)))
    # Corner of a white NDA sheet sticking out of the 2nd drawer seam (`y = 0.86, x = -0.446, z = 0.31`)
    add_box("JammedPaperCorner", (-0.442, 0.862, 0.31), (0.038, 0.004, 0.11), "PaperWhite", bevel=0.0008, rot=(math.radians(4.0), math.radians(22.0), math.radians(-6.0)))

    # Lived-in crooked folder stack & red office stapler resting on top of the cabinet (`y = 1.85`)
    add_box("TopStackFolderManila", (-0.18, 1.856, -0.22), (0.26, 0.014, 0.34), "FolderManila", bevel=0.002, rot=(0.0, math.radians(8.0), 0.0))
    add_box("TopStackFolderRed",    (-0.16, 1.870, -0.20), (0.25, 0.014, 0.33), "FolderRed",    bevel=0.002, rot=(0.0, math.radians(-11.0), 0.0))
    add_box("TopStackPaperSheet",   (-0.17, 1.880, -0.21), (0.22, 0.005, 0.29), "PaperWhite",   bevel=0.001, rot=(0.0, math.radians(16.0), 0.0))
    # Red swingline stapler on top corner (`x = -0.26, y = 1.86, z = +0.24`)
    add_box("StaplerBase", (-0.26, 1.856, 0.24), (0.042, 0.012, 0.145), "CabDarkRecess", bevel=0.002, rot=(0.0, math.radians(-24.0), 0.0))
    add_box("StaplerTop",  (-0.26, 1.874, 0.24), (0.040, 0.022, 0.140), "FolderRed",     bevel=0.004, rot=(math.radians(-4.0), math.radians(-24.0), 0.0))

    return finalize_and_export_prop("prop_filing_cabinet")


# ==============================================================================
# 3. PROP: PERFORATED METAL-MESH OFFICE TRASH BIN (`prop_trash_bin.glb`)
#    Footprint: X in [-0.193, +0.193], Y in [0.00, 0.505], Z in [-0.198, +0.198]
# ==============================================================================
def build_prop_trash_bin():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()

    # 1. Molded Solid Steel Bottom Cup Base (`y = 0.00..0.055`, `r = 0.136..0.144`)
    add_cyl_y("BinSolidBaseCup", (0.0, 0.026, 0.0), 0.136, 0.052, "BinRim", bevel=0.005, vertices=36, cone_r2=0.143)
    add_cyl_y("BinInnerDarkFloor", (0.0, 0.054, 0.0), 0.138, 0.008, "CabDarkRecess", bevel=0.001, vertices=36)

    # 2. Hollow Double-Walled Tapered Perforated Metal-Mesh Basket (`y = 0.048..0.462`, `r_bot = 0.141 -> r_top = 0.188`)
    # Built by lofting outer and inner 36-gon rings so both outside AND inside render with outward normals under GL_CULL_FACE!
    n_seg = 36
    rings_yz = [
        (0.048, 0.142),
        (0.180, 0.157),
        (0.320, 0.173),
        (0.462, 0.189),
    ]
    wall_thick = 0.0055
    verts = []
    # Outer wall rings (0..3), then Inner wall rings (4..7)
    for is_inner in (False, True):
        for ry, rr in rings_yz:
            r_use = (rr - wall_thick) if is_inner else rr
            for j in range(n_seg):
                a = j * math.tau / n_seg
                verts.append(xyz((r_use * math.cos(a), ry, r_use * math.sin(a))))

    faces = []
    for r_i in range(len(rings_yz) - 1):
        b0 = r_i * n_seg
        b1 = (r_i + 1) * n_seg
        i0 = (r_i + len(rings_yz)) * n_seg
        i1 = (r_i + 1 + len(rings_yz)) * n_seg
        for j in range(n_seg):
            jn = (j + 1) % n_seg
            # Outer wall face (outward normal)
            faces.append((b0 + j, b0 + jn, b1 + jn, b1 + j))
            # Inner wall face (inward normal so viewing inside the open bin is GL_CULL_FACE safe!)
            faces.append((i0 + j, i1 + j, i1 + jn, i0 + jn))

    mesh_wall = bpy.data.meshes.new("PerforatedBinMeshWall")
    mesh_wall.from_pydata(verts, [], faces)
    mesh_wall.update()
    wall_obj = bpy.data.objects.new("PerforatedBinMeshWall", mesh_wall)
    bpy.context.collection.objects.link(wall_obj)
    finish(wall_obj, "PerforatedBinMeshWall", "BinSteel")
    apply_weighted_smooth(wall_obj)

    # 3. Rolled Brushed-Steel Top Rim Torus (`y = 0.464, R = 0.187, r = 0.0095`) & Mid Reinforcing Band (`y = 0.26`)
    bpy.ops.mesh.primitive_torus_add(
        major_segments=36,
        minor_segments=14,
        major_radius=0.187,
        minor_radius=0.0095,
        location=xyz((0.0, 0.464, 0.0)),
    )
    rim_obj = finish(bpy.context.object, "BinRolledTopRim", "BinRim")
    apply_weighted_smooth(rim_obj)

    bpy.ops.mesh.primitive_torus_add(
        major_segments=36,
        minor_segments=10,
        major_radius=0.166,
        minor_radius=0.0040,
        location=xyz((0.0, 0.260, 0.0)),
    )
    mid_band = finish(bpy.context.object, "BinMidReinforcingBand", "BinRim")
    apply_weighted_smooth(mid_band)

    # 4. Realistic Crumpled Office Paper Balls, Crushed Energy-Drink Can & Tilted Paper Coffee Cup Inside!
    # Lower trash bed fill (`y = 0.06..0.28`)
    paper_clusters = [
        (-0.045, 0.16,  0.035, 0.072, "PaperWhite", 101),
        ( 0.050, 0.19, -0.040, 0.068, "PaperBlue",  202),
        ( 0.010, 0.25,  0.048, 0.074, "PaperWhite", 303),
        (-0.055, 0.31, -0.035, 0.070, "PaperWhite", 404),
        ( 0.058, 0.34,  0.030, 0.066, "PaperBlue",  505),
        (-0.015, 0.38, -0.055, 0.065, "PaperWhite", 606),
        (-0.062, 0.42,  0.045, 0.062, "PaperWhite", 707),
        ( 0.042, 0.44, -0.052, 0.058, "PaperBlue",  808),
    ]
    for p_i, (px, py, pz, pr, p_mat, seed) in enumerate(paper_clusters):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=pr, location=xyz((px, py, pz)))
        p_obj = finish(bpy.context.object, f"CrumpledPaperBall_{p_i}", p_mat)
        rng = np.random.default_rng(seed)
        # Displace vertices along angular folding planes so it looks like genuine crumpled office paper
        a1, a2, a3 = rng.uniform(0.0, math.tau, 3)
        for v in p_obj.data.vertices:
            n = v.co.normalized()
            fold = 0.24 * math.sin(n.x * 5.2 + a1) * math.cos(n.y * 4.8 + a2) + 0.16 * math.cos(n.z * 6.1 + a3)
            v.co *= (0.86 + fold)
        p_obj.data.update()
        apply_weighted_smooth(p_obj)

    # Tilted Takeout Paper Coffee Cup with Cardboard Sleeve & White Sip-Lid (`y = 0.445, x = 0.045, z = 0.038`)
    cup_rot = (math.radians(28.0), math.radians(-18.0), math.radians(-22.0))
    add_cyl_y("TrashCoffeeCupBody",   (0.042, 0.440, 0.038), 0.032, 0.112, "PaperWhite",   bevel=0.002, vertices=24, cone_r2=0.044, rot=cup_rot)
    add_cyl_y("TrashCoffeeCupSleeve", (0.042, 0.440, 0.038), 0.037, 0.054, "CoffeeSleeve", bevel=0.001, vertices=24, cone_r2=0.042, rot=cup_rot)

    # Discarded Crushed Crimson B12 Energy-Drink Can & Cyan Can inside the bin (`y = 0.450`)
    can = add_cyl_y("TrashCrushedEnergyCan", (-0.035, 0.448, 0.015), 0.033, 0.122, "CanRed", bevel=0.004, vertices=24, rot=(math.radians(-44.0), math.radians(18.0), math.radians(28.0)))
    for v in can.data.vertices:
        if abs(v.co.z) < 0.028:
            v.co.x *= 0.68
    can.data.update()
    apply_weighted_smooth(can)

    # Crumpled Manila NDA folder corner sticking up at the rear rim (`y = 0.455`)
    add_box("TrashCrumpledFolder", (0.012, 0.452, -0.068), (0.12, 0.11, 0.008), "FolderManila", bevel=0.0015, rot=(math.radians(-24.0), math.radians(15.0), math.radians(18.0)))

    return finalize_and_export_prop("prop_trash_bin")


def render_wave2_props_proof():
    """Render 1920x1080 3-panel proof `office_wave2_props_proof.png` with `use_backface_culling = True`."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1920
    sc.render.resolution_y = 1080
    sc.render.film_transparent = False
    world = bpy.data.worlds.new("Wave2ProofWorld")
    sc.world = world
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.11, 0.12, 0.16, 1.0)
        bg.inputs[1].default_value = 1.0

    # Panel 1 (Left, x = -1.25): Hero Vending Machine (`prop_vending_machine.glb`, front is +Z -> -Y in Blender, rotate +18 deg)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_vending_machine.glb"))
    p1_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in p1_objs:
        o.scale = (0.86, 0.86, 0.86)
        o.location = V((-1.25, 0.0, 0.04))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(18.0))

    # Panel 2 (Center, x = 0.05): Lived-In 4-Drawer Filing Cabinet (`prop_filing_cabinet.glb`, front is -X -> rotate +72 deg so -X faces camera -Y!)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_filing_cabinet.glb"))
    p2_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in p2_objs:
        o.scale = (0.96, 0.96, 0.96)
        o.location = V((0.05, 0.0, 0.06))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (0.0, 0.0, math.radians(72.0))

    # Panel 3 (Right, x = +1.28): Perforated Metal-Mesh Office Trash Bin (`prop_trash_bin.glb`, scaled up 2.8x and tilted toward camera)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "prop_trash_bin.glb"))
    p3_objs = [o for o in set(bpy.data.objects) - before if o.type == "MESH"]
    for o in p3_objs:
        o.scale = (2.85, 2.85, 2.85)
        o.location = V((1.28, 0.0, 0.24))
        o.rotation_mode = "XYZ"
        o.rotation_euler = (math.radians(22.0), 0.0, math.radians(24.0))

    for m in bpy.data.materials:
        m.use_backface_culling = True
        if m.name.startswith("VendScreen") and m.use_nodes:
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            if bsdf and "Emission Color" in bsdf.inputs:
                for link in list(m.node_tree.links):
                    if link.to_node == bsdf and link.to_socket.name == "Base Color":
                        m.node_tree.links.new(link.from_socket, bsdf.inputs["Emission Color"])
                        if "Emission Strength" in bsdf.inputs:
                            bsdf.inputs["Emission Strength"].default_value = 0.65
                        break

    for name, loc, energy, col in [
        ("Key",  (2.6, -3.6, 2.8), 3.8, (1.0, 0.95, 0.88)),
        ("Fill", (-2.8, -2.6, 1.8), 2.4, (0.84, 0.90, 1.0)),
        ("Rim",  (1.5, 3.0, 2.6),   2.6, (1.0, 0.82, 0.62)),
    ]:
        ld = bpy.data.lights.new(name, type="SUN")
        ld.energy = energy
        ld.color = col
        lo = bpy.data.objects.new(name, ld)
        sc.collection.objects.link(lo)
        lo.location = loc
        d = V((0.0, 0.0, 0.95)) - V(loc)
        lo.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    cd = bpy.data.cameras.new("ProofCam")
    cd.type = "ORTHO"
    cd.ortho_scale = 3.85
    co = bpy.data.objects.new("ProofCam", cd)
    sc.collection.objects.link(co)
    sc.camera = co
    co.location = (0.0, -3.5, 1.18)
    d = V((0.0, 0.0, 0.96)) - co.location
    co.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()

    for target_path in (ARTIFACT_PROOF_1, ARTIFACT_PROOF_2, SCREENSHOT_PROOF):
        try:
            target_path.parent.mkdir(parents=True, exist_ok=True)
            sc.render.filepath = str(target_path)
            bpy.ops.render.render(write_still=True)
            print("Saved Wave 2 Office Props proof:", target_path)
        except Exception as exc:
            print("Warning saving proof to", target_path, ":", exc)


if __name__ == "__main__":
    build_prop_vending_machine()
    build_prop_filing_cabinet()
    build_prop_trash_bin()
    render_wave2_props_proof()
