"""Blender 5.1 high-craft generator for Office Props Wave 1 (How to Vibe Coder):
  1. assets/models/prop_whiteboard.glb    — Anodized-aluminum rolling easel whiteboard with tubular legs,
     4 swivel casters, marker tray + 4 colored markers + felt eraser, 1024x1024 double-sided Russian startup
     architecture & Kanban atlas, crooked 3D sticky notes, and colorful magnets.
  2. assets/models/prop_water_cooler.glb  — Sleek contoured modern matte-ivory & brushed-silver water dispenser
     with recessed dark cup alcove, red/blue push-lever spigots, perforated stainless drip grille, side translucent
     cup dispenser tube with nested paper cups, and ribbed translucent 19L sky-blue polycarbonate jug with visible
     inner water column & meniscus.
  3. assets/models/prop_xerox_printer.glb — Sculpted multifunction office copier/printer on rolling casters with
     stepped cabinet, 3 recessed paper drawers (Tray 2 slightly ajar with jam), tilted glowing Russian touchscreen
     control panel, hinged slightly-open ADF scanner lid revealing green scan bar, side output tray with crooked
     printed pass sheets, and red paper-jam warning beacon.

Quality Gates:
  - 100% outward normals (`recalc_face_normals` + `use_backface_culling = True` verified for Raylib GL_CULL_FACE)
  - Calibrated PBR linear colors (no chalky washout under Raylib Finish.Imported `pow(base, 0.78)`)
  - 24-ray BVHTree Ambient Occlusion baked into COLOR_0 (`export_vertex_color="ACTIVE"`)
  - Exact floor origin Z_bl = 0.000 (Y_gltf = 0.000) & preserved collision/placement footprints
  - Syncs to assets/models/, bin/Release/net10.0/assets/models/, bin/Debug/net10.0/assets/models/
  - Renders 3-panel studio proof PNG for critic-lead inspection
"""
import json
import math
import os
import shutil
import struct
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
OFFICE_DIR = Path(__file__).resolve().parent
TEX_DIR = OFFICE_DIR / "textures_wave1"
PROOF_PATH = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\office_props_wave1_proof.png")


# ==============================================================================
# CORE HELPERS (Materials, Sculpted Primitives, 24-Ray BVHTree AO, Export)
# ==============================================================================
def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def load_packed_image(filename):
    path = str(TEX_DIR / filename)
    img = bpy.data.images.load(path, check_existing=True)
    img.colorspace_settings.name = "sRGB"
    img.pack()
    return img


def make_mat(
    name,
    rgba=(0.8, 0.8, 0.8, 1.0),
    metallic=0.05,
    roughness=0.45,
    image=None,
    emission_rgb=None,
    emission_strength=0.0,
    alpha=1.0,
):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    mat.use_backface_culling = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (rgba[0], rgba[1], rgba[2], 1.0)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Alpha"].default_value = alpha

    if alpha < 0.99:
        mat.blend_method = "BLEND" if hasattr(mat, "blend_method") else "BLEND"
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = "BLENDED"

    if image is not None:
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        if rgba[:3] != (1.0, 1.0, 1.0):
            mix = nodes.new("ShaderNodeMix")
            mix.data_type = "RGBA"
            mix.blend_type = "MULTIPLY"
            mix.inputs["Factor"].default_value = 1.0
            mix.inputs[7].default_value = (rgba[0], rgba[1], rgba[2], 1.0)
            links.new(tex.outputs["Color"], mix.inputs[6])
            links.new(mix.outputs[2], bsdf.inputs["Base Color"])
        else:
            links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if emission_strength > 0.0:
            if emission_rgb is not None:
                bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
            else:
                links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = emission_strength
    elif emission_strength > 0.0:
        ec = emission_rgb if emission_rgb is not None else rgba[:3]
        bsdf.inputs["Emission Color"].default_value = (ec[0], ec[1], ec[2], 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return mat


def get_uv_layer(bm):
    """Always use the exact same UV layer name 'UVMap' so bpy.ops.object.join() NEVER drops UVs."""
    uv = bm.loops.layers.uv.get("UVMap")
    if uv is None:
        uv = bm.loops.layers.uv.new("UVMap")
    return uv


def _finish_obj(obj, mat, smooth=True, box_uv_scale=1.0):
    obj.data.materials.append(mat)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    uv_layer = get_uv_layer(bm)
    for f in bm.faces:
        f.smooth = smooth
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for lp in f.loops:
            co = lp.vert.co
            if az >= ax and az >= ay:
                u, v = co.x * box_uv_scale, co.y * box_uv_scale
            elif ay >= ax:
                u, v = co.x * box_uv_scale, co.z * box_uv_scale
            else:
                u, v = co.y * box_uv_scale, co.z * box_uv_scale
            lp[uv_layer].uv = (u * 0.5 + 0.5, v * 0.5 + 0.5)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    return obj


def add_beveled_box(name, loc, size, mat, bevel=0.008, segments=3, rot=(0, 0, 0), uv_scale=1.0):
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    sx, sy, sz = size
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=list(bm.verts))
    max_b = min(sx, sy, sz) * 0.42
    bv = min(bevel, max_b)
    if bv > 1e-4:
        bmesh.ops.bevel(
            bm,
            geom=list(bm.edges),
            offset=bv,
            segments=segments,
            profile=0.5,
            affect="EDGES",
            clamp_overlap=True,
        )
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.rotation_euler = rot
    obj.location = loc
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return _finish_obj(obj, mat, smooth=True, box_uv_scale=uv_scale)


def add_smooth_cyl(name, loc, r_bot, r_top, depth, mat, verts=24, bevel=0.002, rot=(0, 0, 0), uv_scale=1.0):
    bpy.ops.mesh.primitive_cone_add(
        vertices=verts,
        radius1=r_bot,
        radius2=r_top,
        depth=depth,
        end_fill_type="NGON",
        location=(0, 0, 0),
    )
    obj = bpy.context.active_object
    obj.name = name
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    cap_edges = [e for e in bm.edges if any(len(f.verts) > 4 for f in e.link_faces)]
    bv = min(bevel, min(r_bot, r_top, depth * 0.4) * 0.45)
    if bv > 1e-4 and cap_edges:
        bmesh.ops.bevel(
            bm,
            geom=cap_edges,
            offset=bv,
            segments=2,
            profile=0.5,
            affect="EDGES",
            clamp_overlap=True,
        )
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.rotation_euler = rot
    obj.location = loc
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return _finish_obj(obj, mat, smooth=True, box_uv_scale=uv_scale)


def add_tube_between(name, p0, p1, radius, mat, verts=18, bevel=0.002):
    p0 = V(p0)
    p1 = V(p1)
    d = p1 - p0
    length = d.length
    if length < 1e-5:
        return None
    mid = (p0 + p1) * 0.5
    q = V((0, 0, 1)).rotation_difference(d.normalized())
    return add_smooth_cyl(name, mid, radius, radius, length, mat, verts=verts, bevel=bevel, rot=q.to_euler())


def add_sphere(name, loc, radius, mat, scale=(1, 1, 1), segments=18, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments, ring_count=rings, radius=radius, location=(0, 0, 0))
    obj = bpy.context.active_object
    obj.name = name
    obj.scale = scale
    obj.location = loc
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return _finish_obj(obj, mat, smooth=True)


def add_torus(name, loc, major_r, minor_r, mat, rot=(0, 0, 0), major_seg=28, minor_seg=12):
    bpy.ops.mesh.primitive_torus_add(
        major_radius=major_r,
        minor_radius=minor_r,
        major_segments=major_seg,
        minor_segments=minor_seg,
        location=(0, 0, 0),
    )
    obj = bpy.context.active_object
    obj.name = name
    obj.rotation_euler = rot
    obj.location = loc
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return _finish_obj(obj, mat, smooth=True)


def add_lathe_shell(name, profile, mat, n_seg=44, cap_bottom=True, cap_top=True, uv_v_range=None, loc=(0, 0, 0)):
    """Creates a smooth closed lathe solid from [(z, r), ...] ascending in Z with outward normals & cylindrical UVs."""
    bm = bmesh.new()
    rings = []
    for z, r in profile:
        rr = max(1e-4, r)
        ring = []
        for i in range(n_seg):
            a = i * math.tau / n_seg
            v = bm.verts.new((rr * math.cos(a) + loc[0], rr * math.sin(a) + loc[1], z + loc[2]))
            ring.append(v)
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        for i in range(n_seg):
            ni = (i + 1) % n_seg
            bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))
    if cap_bottom:
        bm.faces.new(list(reversed(rings[0])))
    if cap_top:
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))

    z_min = profile[0][0] + loc[2]
    z_max = profile[-1][0] + loc[2]
    dz = max(1e-5, z_max - z_min)
    uv_layer = get_uv_layer(bm)
    for f in bm.faces:
        f.smooth = True
        for lp in f.loops:
            co = lp.vert.co
            u = (math.atan2(co.y - loc[1], co.x - loc[0]) / math.tau) % 1.0
            v = (co.z - z_min) / dz
            if uv_v_range:
                v = uv_v_range[0] + v * (uv_v_range[1] - uv_v_range[0])
            lp[uv_layer].uv = (u, v)

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    mesh.update()
    return obj


def add_superellipse_loft(name, sections, mat, n_seg=48, uv_z_range=None):
    """Builds a sleek contoured superellipse body from [(z, rx, ry, y_off, power), ...] ascending in Z."""
    bm = bmesh.new()
    rings = []
    for z, rx, ry, y_off, pwr in sections:
        ring = []
        for i in range(n_seg):
            a = i * math.tau / n_seg
            ca, sa = math.cos(a), math.sin(a)
            x = math.copysign(abs(ca) ** (2.0 / pwr), ca) * rx
            y = math.copysign(abs(sa) ** (2.0 / pwr), sa) * ry + y_off
            ring.append(bm.verts.new((x, y, z)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        for i in range(n_seg):
            ni = (i + 1) % n_seg
            bm.faces.new((r0[i], r0[ni], r1[ni], r1[i]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))

    z0 = uv_z_range[0] if uv_z_range else sections[0][0]
    z1 = uv_z_range[1] if uv_z_range else sections[-1][0]
    dz = max(1e-5, z1 - z0)
    uv_layer = get_uv_layer(bm)
    for f in bm.faces:
        f.smooth = True
        for lp in f.loops:
            co = lp.vert.co
            u = (co.x / 0.48) + 0.5
            v = max(0.0, min(1.0, (co.z - z0) / dz))
            lp[uv_layer].uv = (u, v)

    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    mesh.update()
    return obj


def add_caster_wheel(prefix, x, y, z_stem_top, mat_metal, mat_rubber, mat_brake=None, wheel_r=0.032, yaw=0.0):
    """Realistic multi-part swiveling office caster wheel touching exact Z=0.000."""
    # Wheel tire center at Z = wheel_r so bottom touches Z = 0.000 exactly!
    dx = -math.sin(yaw) * 0.012
    dy = math.cos(yaw) * 0.012
    # Swivel stem + collar mounting into leg/plinth
    stem_h = max(0.014, z_stem_top - (wheel_r * 1.65))
    add_smooth_cyl(f"{prefix}_Stem", (x, y, wheel_r * 1.65 + stem_h * 0.5), 0.011, 0.013, stem_h, mat_metal, verts=16, bevel=0.001)
    add_smooth_cyl(f"{prefix}_SwivelCap", (x, y, wheel_r * 1.62), 0.019, 0.017, 0.014, mat_metal, verts=18, bevel=0.002)
    # Bent U-fork housing cradling the wheel
    add_beveled_box(
        f"{prefix}_Fork",
        (x + dx * 0.5, y + dy * 0.5, wheel_r * 1.25),
        (0.036, 0.038, wheel_r * 0.85),
        mat_metal,
        bevel=0.004,
        segments=2,
        rot=(0, 0, yaw),
    )
    # Wheel rubber tire (cylinder rotated around local X/Y by yaw)
    wx, wy, wz = x + dx, y + dy, wheel_r
    add_smooth_cyl(
        f"{prefix}_Tire",
        (wx, wy, wz),
        wheel_r,
        wheel_r,
        0.024,
        mat_rubber,
        verts=24,
        bevel=0.004,
        rot=(0, math.pi / 2, yaw),
    )
    # Wheel hubcap + axle bolt
    add_smooth_cyl(
        f"{prefix}_Hub",
        (wx, wy, wz),
        wheel_r * 0.48,
        wheel_r * 0.48,
        0.028,
        mat_metal,
        verts=16,
        bevel=0.002,
        rot=(0, math.pi / 2, yaw),
    )
    if mat_brake is not None:
        add_beveled_box(
            f"{prefix}_Brake",
            (wx + dx * 1.6, wy + dy * 1.6, wz + wheel_r * 0.55),
            (0.018, 0.026, 0.007),
            mat_brake,
            bevel=0.002,
            rot=(0.25, 0, yaw),
        )


def add_uv_quad_solid(name, corners_front, thickness, mat, uv_rect=(0, 0, 1, 1), curl_bottom=0.0, normal_dir=None):
    """Creates a solid thin double-walled card/sheet/sticky-note with exact UV mapping on front face & optional peel curl."""
    # corners_front = [bl, br, tr, tl] in 3D space
    bl, br, tr, tl = [V(c) for c in corners_front]
    u0, v0, u1, v1 = uv_rect
    if normal_dir is None:
        n = (br - bl).cross(tl - bl).normalized()
    else:
        n = V(normal_dir).normalized()

    # Subdivide vertically (4 rows) so bottom edge can curl smoothly away from the surface!
    rows = 4
    cols = 2
    bm = bmesh.new()
    front_grid = []
    back_grid = []
    for r in range(rows + 1):
        tv = r / rows  # 0 = bottom, 1 = top
        # Smooth upward peel near bottom (tv=0)
        peel = curl_bottom * ((1.0 - tv) ** 2.2)
        f_row = []
        b_row = []
        for c in range(cols + 1):
            tu = c / cols
            p_bot = bl.lerp(br, tu)
            p_top = tl.lerp(tr, tu)
            p = p_bot.lerp(p_top, tv) + n * peel
            f_row.append(bm.verts.new(p))
            b_row.append(bm.verts.new(p - n * thickness))
        front_grid.append(f_row)
        back_grid.append(b_row)

    uv_layer = get_uv_layer(bm)
    # Front faces
    for r in range(rows):
        for c in range(cols):
            f = bm.faces.new((front_grid[r][c], front_grid[r][c + 1], front_grid[r + 1][c + 1], front_grid[r + 1][c]))
            f.smooth = True
            uvs = [
                (u0 + (c / cols) * (u1 - u0), v0 + (r / rows) * (v1 - v0)),
                (u0 + ((c + 1) / cols) * (u1 - u0), v0 + (r / rows) * (v1 - v0)),
                (u0 + ((c + 1) / cols) * (u1 - u0), v0 + ((r + 1) / rows) * (v1 - v0)),
                (u0 + (c / cols) * (u1 - u0), v0 + ((r + 1) / rows) * (v1 - v0)),
            ]
            for lp, uv in zip(f.loops, uvs):
                lp[uv_layer].uv = uv
    # Back faces (reversed winding)
    for r in range(rows):
        for c in range(cols):
            f = bm.faces.new((back_grid[r][c], back_grid[r + 1][c], back_grid[r + 1][c + 1], back_grid[r][c + 1]))
            f.smooth = True
            for lp in f.loops:
                lp[uv_layer].uv = (u0 + 0.05 * (u1 - u0), v0 + 0.92 * (v1 - v0))
    # Perimeter rim edges so closed watertight solid
    for c in range(cols):
        bm.faces.new((front_grid[0][c + 1], front_grid[0][c], back_grid[0][c], back_grid[0][c + 1]))
        bm.faces.new((front_grid[rows][c], front_grid[rows][c + 1], back_grid[rows][c + 1], back_grid[rows][c]))
    for r in range(rows):
        bm.faces.new((front_grid[r][0], front_grid[r + 1][0], back_grid[r + 1][0], back_grid[r][0]))
        bm.faces.new((front_grid[r + 1][cols], front_grid[r][cols], back_grid[r][cols], back_grid[r + 1][cols]))

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    mesh.update()
    return obj


def join_bake_ao_and_export(filename, mat_order, no_ao_mats=(), ao_strength=0.38, max_dist=0.14):
    """Joins all meshes, enforces material slot order (important for alpha draw order!),
    recalculates outward normals, snaps floor origin Z=0.000, bakes 24-ray BVHTree AO into COLOR_0,
    exports GLB with export_vertex_color='ACTIVE', and syncs to Release & Debug asset directories.
    """
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in mesh_objs:
        if not o.data.uv_layers:
            o.data.uv_layers.new(name="UVMap")
        else:
            o.data.uv_layers[0].name = "UVMap"
            o.data.uv_layers.active_index = 0
    bpy.ops.object.select_all(action="DESELECT")
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    if len(mesh_objs) > 1:
        bpy.ops.object.join()
    obj = bpy.context.active_object
    obj.name = Path(filename).stem
    mesh = obj.data

    # Enforce explicit material order (crucial so opaque inner water/cups draw BEFORE translucent outer shells!)
    mat_lookup = {m.name: m for m in mat_order}
    old_mats = [m.name.split(".")[0] if m else mat_order[0].name for m in mesh.materials]
    name_to_idx = {m.name: i for i, m in enumerate(mat_order)}
    poly_targets = [name_to_idx.get(old_mats[p.material_index], 0) for p in mesh.polygons]
    mesh.materials.clear()
    for m in mat_order:
        mesh.materials.append(m)
    for poly, ni in zip(mesh.polygons, poly_targets):
        poly.material_index = ni

    # Clean topology + enforce 100% outward normals (GL_CULL_FACE safe!)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-5)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    # Exact floor origin snap Z_min = 0.0000
    z_min = min(v.co.z for v in mesh.vertices)
    if abs(z_min) > 1e-5:
        for v in mesh.vertices:
            v.co.z -= z_min
    mesh.update()

    # Weighted normals for crisp beveled panels without shading artifacts
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.keep_sharp = False
    wn.weight = 50
    try:
        bpy.ops.object.modifier_apply(modifier=wn.name)
    except Exception:
        obj.modifiers.remove(wn)

    # Bake 24-ray BVHTree hemisphere Ambient Occlusion into COLOR_0
    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    vcol = mesh.color_attributes.new(name="Color", type="BYTE_COLOR", domain="POINT")

    verts = mesh.vertices
    n_verts = len(verts)
    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(p.vertices) for p in mesh.polygons])

    v_norms = [V((0.0, 0.0, 0.0)) for _ in range(n_verts)]
    v_mat = [0] * n_verts
    v_neighbors = [set() for _ in range(n_verts)]
    no_ao_indices = {i for i, m in enumerate(mat_order) if m.name in no_ao_mats}

    for poly in mesh.polygons:
        mi = poly.material_index
        pn = poly.normal * max(1e-6, poly.area)
        pverts = poly.vertices
        for vi in pverts:
            v_norms[vi] += pn
            if mi in no_ao_indices or v_mat[vi] not in no_ao_indices:
                v_mat[vi] = mi
        L = len(pverts)
        for idx_a in range(L):
            va, vb = pverts[idx_a], pverts[(idx_a + 1) % L]
            v_neighbors[va].add(vb)
            v_neighbors[vb].add(va)

    # 24 Fibonacci sphere directions
    golden_angle = 2.39996323
    world_rays = []
    for j in range(24):
        zr = 1.0 - 2.0 * (j + 0.5) / 24.0
        rr = math.sqrt(max(0.0, 1.0 - zr * zr))
        ang = j * golden_angle
        world_rays.append(V((rr * math.cos(ang), rr * math.sin(ang), zr)).normalized())

    z_top = max(0.5, max(v.co.z for v in verts))
    shades = [1.0] * n_verts
    for v in verts:
        vi = v.index
        if v_mat[vi] in no_ao_indices:
            shades[vi] = 1.0
            continue
        n = v_norms[vi].normalized()
        if n.length_squared < 0.01:
            n = V((0.0, 0.0, 1.0))
        origin = v.co + n * 0.004
        ao = 0.0
        valid = 0
        for ray in world_rays:
            nd = ray.dot(n)
            if nd <= 0.25:
                continue
            valid += 1
            hit = tree.ray_cast(origin, ray, max_dist)
            hit_dist = hit[3]
            if hit_dist is not None and hit_dist > 0.005:
                ao += (1.0 - hit_dist / max_dist) * nd
        if valid > 0:
            ao /= valid
        # Subtle floor grounding gradient (subtle contact shadow near Z=0)
        floor_grad = 0.88 + 0.12 * min(1.0, max(0.0, v.co.z / min(0.45, z_top)))
        shades[vi] = max(0.56, min(1.0, floor_grad * (1.0 - ao_strength * ao)))

    for v in verts:
        vi = v.index
        if v_mat[vi] in no_ao_indices:
            vcol.data[vi].color = (1.0, 1.0, 1.0, 1.0)
            continue
        nbrs = v_neighbors[vi]
        s = (0.60 * shades[vi] + 0.40 * (sum(shades[nb] for nb in nbrs) / len(nbrs))) if nbrs else shades[vi]
        vcol.data[vi].color = (s, s, s, 1.0)

    # Report bounds & tri count
    mn = tuple(round(min(v.co[i] for v in verts), 4) for i in range(3))
    mx = tuple(round(max(v.co[i] for v in verts), 4) for i in range(3))
    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)

    out_path = OUT_MODELS / filename
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_texcoords=True,
        export_materials="EXPORT",
        export_vertex_color="ACTIVE",
    )

    # Verify COLOR_0 is present in exported GLB
    raw = out_path.read_bytes()
    jlen = struct.unpack("<I", raw[12:16])[0]
    gltf = json.loads(raw[20 : 20 + jlen].decode("utf-8"))
    has_color0 = any("COLOR_0" in p.get("attributes", {}) for m in gltf.get("meshes", []) for p in m.get("primitives", []))

    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        d = ROOT / sub
        if d.exists():
            shutil.copy2(out_path, d / filename)

    print(f"EXPORTED {filename} | size={out_path.stat().st_size}B | tris={tris} | COLOR_0={has_color0} | BL_min={mn} | BL_max={mx}")
    return obj


# ==============================================================================
# PROP 1: `prop_whiteboard.glb`
# Target BL bounds: X in [-0.98, +0.98], Y in [-0.315, +0.315], Z in [0.000, 1.902]
# ==============================================================================
def build_prop_whiteboard():
    clear_scene()
    img_board = load_packed_image("whiteboard_atlas.png")
    img_sticky = load_packed_image("sticky_notes_atlas.png")

    # Calibrated linear PBR colors (deep anodized metals & rich accents, zero chalky washout!)
    m_stand = make_mat("AnodizedDarkSteel", (0.075, 0.088, 0.105, 1.0), metallic=0.72, roughness=0.34)
    m_alu = make_mat("AnodizedSilverAlu", (0.46, 0.50, 0.55, 1.0), metallic=0.82, roughness=0.26)
    m_rubber = make_mat("CasterRubber", (0.032, 0.035, 0.040, 1.0), metallic=0.05, roughness=0.72)
    m_board = make_mat("PorcelainBoard", (1.0, 1.0, 1.0, 1.0), metallic=0.04, roughness=0.18, image=img_board)
    m_sticky = make_mat("StickyNotes", (1.0, 1.0, 1.0, 1.0), metallic=0.0, roughness=0.72, image=img_sticky)
    m_marker_white = make_mat("MarkerBarrel", (0.76, 0.78, 0.80, 1.0), metallic=0.05, roughness=0.28)
    m_red = make_mat("AccentRed", (0.62, 0.055, 0.075, 1.0), metallic=0.08, roughness=0.26)
    m_blue = make_mat("AccentBlue", (0.045, 0.22, 0.68, 1.0), metallic=0.08, roughness=0.26)
    m_green = make_mat("AccentGreen", (0.045, 0.42, 0.16, 1.0), metallic=0.08, roughness=0.26)
    m_yellow = make_mat("AccentYellow", (0.76, 0.52, 0.04, 1.0), metallic=0.08, roughness=0.28)
    m_felt = make_mat("EraserFelt", (0.045, 0.048, 0.056, 1.0), metallic=0.0, roughness=0.92)

    # 1) Tubular rolling easel stand + 4 swiveling casters (exact Z_min = 0.000)
    leg_x = 0.928
    for sx in (-1, 1):
        lx = sx * leg_x
        # Lower horizontal runner tube (Y in [-0.265, +0.265] at Z=0.095)
        add_smooth_cyl(f"RunnerTube_{sx}", (lx, 0.0, 0.095), 0.019, 0.019, 0.54, m_stand, verts=22, bevel=0.003, rot=(math.pi / 2, 0, 0))
        for sy in (-1, 1):
            add_sphere(f"RunnerCap_{sx}_{sy}", (lx, sy * 0.275, 0.095), 0.020, m_rubber, scale=(1.0, 0.65, 1.0))
            # Swiveling caster wheel touching Z=0.000 (front casters have red brake lock!)
            add_caster_wheel(
                f"Caster_{sx}_{sy}",
                lx,
                sy * 0.235,
                0.095,
                m_alu,
                m_rubber,
                mat_brake=m_red if sy < 0 else None,
                wheel_r=0.031,
                yaw=(0.35 if sx * sy > 0 else -0.42),
            )
        # Main vertical upright tubular leg rising to pivot yoke
        add_smooth_cyl(f"UprightLeg_{sx}", (lx, 0.0, 0.76), 0.018, 0.018, 1.33, m_stand, verts=22, bevel=0.002)
        # Angled tubular gusset struts bracing runner to upright
        for sy in (-1, 1):
            add_tube_between(f"Gusset_{sx}_{sy}", (lx, sy * 0.19, 0.10), (lx, sy * 0.015, 0.34), 0.012, m_stand, verts=16)
        # Pivot clamp bracket + chunky fluted hand-tightening star knob on outer side
        add_beveled_box(f"PivotClamp_{sx}", (lx, 0.0, 1.39), (0.044, 0.054, 0.088), m_stand, bevel=0.008)
        add_smooth_cyl(f"PivotShaft_{sx}", (lx + sx * 0.028, 0.0, 1.39), 0.012, 0.012, 0.032, m_alu, verts=16, rot=(0, math.pi / 2, 0))
        knob_x = lx + sx * 0.042
        add_smooth_cyl(f"TiltKnob_{sx}", (knob_x, 0.0, 1.39), 0.025, 0.025, 0.020, m_rubber, verts=20, bevel=0.004, rot=(0, math.pi / 2, 0))
        for k in range(5):
            ang = k * math.tau / 5.0
            add_smooth_cyl(
                f"KnobLobe_{sx}_{k}",
                (knob_x, 0.021 * math.cos(ang), 1.39 + 0.021 * math.sin(ang)),
                0.0085,
                0.0085,
                0.018,
                m_rubber,
                verts=12,
                bevel=0.002,
                rot=(0, math.pi / 2, 0),
            )

    # Horizontal lower stabilizer crossbar + flanged collars
    add_smooth_cyl("LowerCrossbar", (0.0, 0.0, 0.28), 0.015, 0.015, leg_x * 2.0 - 0.02, m_stand, verts=20, bevel=0.002, rot=(0, math.pi / 2, 0))
    for sx in (-1, 1):
        add_smooth_cyl(f"CrossbarCollar_{sx}", (sx * (leg_x - 0.018), 0.0, 0.28), 0.022, 0.022, 0.036, m_alu, verts=18, rot=(0, math.pi / 2, 0))

    # 2) Anodized aluminum outer frame + rounded dark polymer corner guards (X in [-0.90, +0.90], Z in [0.89, 1.89])
    bw, bh = 1.80, 1.00
    bz_mid = 1.39
    z_bot, z_top = bz_mid - bh * 0.5, bz_mid + bh * 0.5  # 0.89 .. 1.89
    x_l, x_r = -bw * 0.5, bw * 0.5

    # Extruded beveled frame rails
    add_beveled_box("FrameTop", (0.0, 0.0, z_top - 0.012), (bw - 0.04, 0.036, 0.028), m_alu, bevel=0.005, segments=3)
    add_beveled_box("FrameBot", (0.0, 0.0, z_bot + 0.012), (bw - 0.04, 0.036, 0.028), m_alu, bevel=0.005, segments=3)
    add_beveled_box("FrameLeft", (x_l + 0.012, 0.0, bz_mid), (0.028, 0.036, bh - 0.04), m_alu, bevel=0.005, segments=3)
    add_beveled_box("FrameRight", (x_r - 0.012, 0.0, bz_mid), (0.028, 0.036, bh - 0.04), m_alu, bevel=0.005, segments=3)
    # Dark polymer rounded corner bumpers
    for cx in (x_l + 0.016, x_r - 0.016):
        for cz in (z_bot + 0.016, z_top - 0.016):
            add_beveled_box(f"CornerCap_{cx:.2f}_{cz:.2f}", (cx, 0.0, cz), (0.042, 0.042, 0.042), m_rubber, bevel=0.012, segments=3)

    # 3) Subtle curved glare porcelain whiteboard core (Front -Y = top half of atlas, Back +Y = bottom half of atlas)
    # Build a gently arched 12x4 grid board panel so studio & office lights catch realistic porcelain curvature!
    bm = bmesh.new()
    nx, nz = 12, 4
    px_l, px_r = x_l + 0.024, x_r - 0.024
    pz_b, pz_t = z_bot + 0.024, z_top - 0.024
    f_grid, b_grid = [], []
    for iz in range(nz + 1):
        tv = iz / nz
        z = pz_b + tv * (pz_t - pz_b)
        frow, brow = [], []
        for ix in range(nx + 1):
            tu = ix / nx
            x = px_l + tu * (px_r - px_l)
            # Subtle 3.5mm ergonomic optical convex bulge on front & back
            arch = 0.0035 * math.sin(tu * math.pi) * math.sin(tv * math.pi)
            frow.append(bm.verts.new((x, -0.013 - arch, z)))
            brow.append(bm.verts.new((x, +0.013 + arch, z)))
        f_grid.append(frow)
        b_grid.append(brow)

    uv_layer = get_uv_layer(bm)
    # Front (-Y) faces: looking from -Y toward +Y -> Left=-X (tu=0), Right=+X (tu=1), Top=v=1.0, Bot=v=0.505
    for iz in range(nz):
        for ix in range(nx):
            f = bm.faces.new((f_grid[iz][ix], f_grid[iz][ix + 1], f_grid[iz + 1][ix + 1], f_grid[iz + 1][ix]))
            f.smooth = True
            u0, u1 = ix / nx, (ix + 1) / nx
            v0, v1 = 0.506 + (iz / nz) * 0.488, 0.506 + ((iz + 1) / nz) * 0.488
            for lp, uv in zip(f.loops, ((u0, v0), (u1, v0), (u1, v1), (u0, v1))):
                lp[uv_layer].uv = uv

    # Back (+Y) faces: looking from +Y toward -Y -> Left=+X (tu=1 -> u=0), Right=-X (tu=0 -> u=1), Top=v=0.494, Bot=v=0.006
    for iz in range(nz):
        for ix in range(nx):
            f = bm.faces.new((b_grid[iz][ix + 1], b_grid[iz][ix], b_grid[iz + 1][ix], b_grid[iz + 1][ix + 1]))
            f.smooth = True
            u_left = 1.0 - (ix + 1) / nx
            u_right = 1.0 - ix / nx
            v0, v1 = 0.006 + (iz / nz) * 0.488, 0.006 + ((iz + 1) / nz) * 0.488
            for lp, uv in zip(f.loops, ((u_left, v0), (u_right, v0), (u_right, v1), (u_left, v1))):
                lp[uv_layer].uv = uv

    # Close perimeter edges of board core
    for ix in range(nx):
        bm.faces.new((f_grid[0][ix + 1], f_grid[0][ix], b_grid[0][ix], b_grid[0][ix + 1]))
        bm.faces.new((f_grid[nz][ix], f_grid[nz][ix + 1], b_grid[nz][ix + 1], b_grid[nz][ix]))
    for iz in range(nz):
        bm.faces.new((f_grid[iz][0], f_grid[iz + 1][0], b_grid[iz + 1][0], b_grid[iz][0]))
        bm.faces.new((f_grid[iz + 1][nx], f_grid[iz][nx], b_grid[iz][nx], b_grid[iz + 1][nx]))

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    b_mesh = bpy.data.meshes.new("PorcelainPanel")
    bm.to_mesh(b_mesh)
    bm.free()
    b_obj = bpy.data.objects.new("PorcelainPanel", b_mesh)
    bpy.context.collection.objects.link(b_obj)
    b_mesh.materials.append(m_board)
    b_mesh.update()

    # 4) Contoured aluminum marker tray (front -Y, z=0.875) with lip + end-caps
    tray_w = 1.24
    tray_z = 0.876
    add_beveled_box("MarkerTrayDeck", (0.0, -0.056, tray_z), (tray_w, 0.082, 0.010), m_alu, bevel=0.003, segments=2)
    add_beveled_box("MarkerTrayLip", (0.0, -0.094, tray_z + 0.011), (tray_w, 0.010, 0.022), m_alu, bevel=0.003, segments=2)
    for sx in (-1, 1):
        add_beveled_box(f"TrayEndCap_{sx}", (sx * (tray_w * 0.5 + 0.006), -0.058, tray_z + 0.008), (0.014, 0.088, 0.028), m_rubber, bevel=0.004)
        add_beveled_box(f"TrayBracket_{sx}", (sx * 0.44, -0.032, tray_z - 0.010), (0.028, 0.044, 0.018), m_stand, bevel=0.003)

    # 4 detailed dry-erase markers sitting naturally askew in the tray (black, blue, red, green)
    markers = [
        (-0.36, -0.056, 0.06, m_rubber, "Black"),
        (-0.18, -0.062, -0.08, m_blue, "Blue"),
        (0.02, -0.054, 0.04, m_red, "Red"),
        (0.21, -0.060, -0.05, m_green, "Green"),
    ]
    for mx, my, yaw, cap_mat, tag in markers:
        mz = tray_z + 0.0155
        # Barrel
        add_smooth_cyl(f"MarkerBarrel_{tag}", (mx, my, mz), 0.0088, 0.0094, 0.102, m_marker_white, verts=16, bevel=0.002, rot=(0, math.pi / 2, yaw))
        # Colored cap + pocket clip + end plug
        ca, sa = math.cos(yaw), math.sin(yaw)
        add_smooth_cyl(
            f"MarkerCap_{tag}",
            (mx + ca * 0.064, my + sa * 0.064, mz),
            0.0098,
            0.0086,
            0.036,
            cap_mat,
            verts=16,
            bevel=0.002,
            rot=(0, math.pi / 2, yaw),
        )
        add_beveled_box(
            f"MarkerClip_{tag}",
            (mx + ca * 0.058, my + sa * 0.058, mz + 0.0105),
            (0.028, 0.004, 0.004),
            cap_mat,
            bevel=0.001,
            rot=(0, 0, yaw),
        )
        add_smooth_cyl(
            f"MarkerPlug_{tag}",
            (mx - ca * 0.053, my - sa * 0.053, mz),
            0.0086,
            0.0082,
            0.010,
            cap_mat,
            verts=14,
            bevel=0.001,
            rot=(0, math.pi / 2, yaw),
        )

    # Sculpted felt board eraser sitting on the right side of the tray
    ex, ey, ez = 0.44, -0.058, tray_z + 0.006
    add_beveled_box("EraserFeltPad", (ex, ey, ez + 0.008), (0.132, 0.048, 0.014), m_felt, bevel=0.003, rot=(0, 0, 0.05))
    add_beveled_box("EraserGripBody", (ex, ey, ez + 0.026), (0.128, 0.044, 0.024), m_stand, bevel=0.008, segments=3, rot=(0, 0, 0.05))
    add_beveled_box("EraserGripAccent", (ex, ey, ez + 0.038), (0.106, 0.032, 0.007), m_blue, bevel=0.003, rot=(0, 0, 0.05))

    # 5) Crooked overlapping 3D paper sticky notes sticking out near edges + glossy round magnets!
    # Sticky quadrants in sticky_notes_atlas.png (PIL top-left -> UV v in [0.5, 1.0]):
    # Q0 yellow: (0.0, 0.5, 0.5, 1.0), Q1 pink: (0.5, 0.5, 1.0, 1.0)
    # Q2 green:  (0.0, 0.0, 0.5, 0.5), Q3 blue: (0.5, 0.0, 1.0, 0.5)
    quads = [(0.02, 0.52, 0.48, 0.98), (0.52, 0.52, 0.98, 0.98), (0.02, 0.02, 0.48, 0.48), (0.52, 0.02, 0.98, 0.48)]

    def place_sticky_front(tag, cx, cz, size, tilt_deg, q_idx, y_off=-0.0185, curl=0.010):
        ang = math.radians(tilt_deg)
        ca, sa = math.cos(ang), math.sin(ang)
        hs = size * 0.5
        local = [(-hs, -hs), (hs, -hs), (hs, hs), (-hs, hs)]
        pts = [V((cx + lx * ca - lz * sa, y_off, cz + lx * sa + lz * ca)) for lx, lz in local]
        add_uv_quad_solid(tag, pts, 0.0018, m_sticky, uv_rect=quads[q_idx], curl_bottom=curl, normal_dir=(0, -1, 0))

    def place_sticky_back(tag, cx, cz, size, tilt_deg, q_idx, y_off=+0.0185, curl=0.010):
        ang = math.radians(tilt_deg)
        ca, sa = math.cos(ang), math.sin(ang)
        hs = size * 0.5
        # Looking from +Y -> Left is +X, Right is -X
        local = [(hs, -hs), (-hs, -hs), (-hs, hs), (hs, hs)]
        pts = [V((cx + lx * ca - lz * sa, y_off, cz + lx * sa + lz * ca)) for lx, lz in local]
        add_uv_quad_solid(tag, pts, 0.0018, m_sticky, uv_rect=quads[q_idx], curl_bottom=curl, normal_dir=(0, +1, 0))

    # Front sticky notes — clustered naturally along edges & slightly overlapping bezel (zero overlap on title text!)
    place_sticky_front("StickyF1", 0.80, 1.73, 0.112, -7.5, 0, y_off=-0.019, curl=0.012)
    place_sticky_front("StickyF2", 0.87, 1.60, 0.110, 11.0, 1, y_off=-0.022, curl=0.015)  # overlaps F1 & right bezel!
    place_sticky_front("StickyF3", 0.81, 1.45, 0.106, -4.5, 2, y_off=-0.019, curl=0.009)
    place_sticky_front("StickyF4", -0.87, 1.48, 0.110, 9.5, 3, y_off=-0.021, curl=0.014)  # overhanging left bezel edge cleanly below title!
    place_sticky_front("StickyF5", -0.80, 0.99, 0.108, -10.5, 0, y_off=-0.019, curl=0.011)
    # Back sticky notes too!
    place_sticky_back("StickyB1", -0.80, 1.73, 0.115, 6.5, 1, y_off=+0.019, curl=0.012)
    place_sticky_back("StickyB2", 0.82, 1.66, 0.112, -9.0, 2, y_off=+0.020, curl=0.014)

    # Colorful round refrigerator/whiteboard magnets pinned around corners!
    magnets = [
        (-0.855, -0.021, 1.835, m_red, "MagF1"),
        (0.76, -0.021, 1.78, m_blue, "MagF2"),
        (0.84, -0.020, 1.12, m_yellow, "MagF3"),
        (-0.80, -0.020, 1.38, m_green, "MagF4"),
        (0.80, +0.021, 1.75, m_red, "MagB1"),
        (-0.78, +0.021, 1.78, m_blue, "MagB2"),
    ]
    for mx, my, mz, mmat, mtag in magnets:
        add_smooth_cyl(mtag, (mx, my, mz), 0.017, 0.015, 0.009, mmat, verts=20, bevel=0.003, rot=(math.pi / 2, 0, 0))

    mat_order = [m_stand, m_alu, m_rubber, m_board, m_sticky, m_marker_white, m_red, m_blue, m_green, m_yellow, m_felt]
    return join_bake_ao_and_export("prop_whiteboard.glb", mat_order, no_ao_mats=("PorcelainBoard",), ao_strength=0.36, max_dist=0.14)


# ==============================================================================
# PROP 2: `prop_water_cooler.glb`
# Target BL bounds: X in [-0.242, +0.309], Y in [-0.285, +0.225], Z in [0.000, 1.703]
# Front faces -Y_blender (+Z_raylib); Cup dispenser on +X
# ==============================================================================
def build_prop_water_cooler():
    clear_scene()
    img_body = load_packed_image("cooler_body_tex.png")
    img_jug = load_packed_image("cooler_jug_tex.png")

    # Calibrated matte-ivory, brushed silver, deep charcoal alcove, translucent sky-blue polycarbonate jug!
    m_ivory = make_mat("CoolerIvoryBody", (0.94, 0.93, 0.90, 1.0), metallic=0.08, roughness=0.34, image=img_body)
    m_silver = make_mat("CoolerBrushedSilver", (0.50, 0.54, 0.59, 1.0), metallic=0.84, roughness=0.24)
    m_chrome = make_mat("CoolerChrome", (0.68, 0.72, 0.76, 1.0), metallic=0.92, roughness=0.14)
    m_charcoal = make_mat("CoolerCharcoal", (0.055, 0.065, 0.080, 1.0), metallic=0.22, roughness=0.38)
    m_alcove = make_mat("CoolerDarkAlcove", (0.028, 0.034, 0.044, 1.0), metallic=0.18, roughness=0.32)
    m_hot = make_mat("TapHotRed", (0.68, 0.055, 0.075, 1.0), metallic=0.06, roughness=0.24)
    m_cold = make_mat("TapColdBlue", (0.045, 0.34, 0.76, 1.0), metallic=0.06, roughness=0.24)
    m_led_hot = make_mat("LedHotGlow", (0.95, 0.16, 0.12, 1.0), roughness=0.20, emission_rgb=(0.95, 0.18, 0.12), emission_strength=0.90)
    m_led_cold = make_mat("LedColdGlow", (0.10, 0.68, 0.98, 1.0), roughness=0.20, emission_rgb=(0.12, 0.72, 1.0), emission_strength=0.90)
    m_led_pwr = make_mat("LedPwrGlow", (0.16, 0.88, 0.42, 1.0), roughness=0.20, emission_rgb=(0.18, 0.92, 0.44), emission_strength=0.85)
    m_cup = make_mat("PaperCupWhite", (0.86, 0.85, 0.82, 1.0), metallic=0.0, roughness=0.58)
    # Opaque/bright inner water & meniscus placed BEFORE translucent outer shells in mat_order!
    m_water_inner = make_mat("InnerWaterVolume", (0.06, 0.44, 0.84, 1.0), metallic=0.02, roughness=0.10, emission_rgb=(0.04, 0.24, 0.48), emission_strength=0.22)
    m_meniscus = make_mat("WaterMeniscusRing", (0.48, 0.86, 0.98, 1.0), metallic=0.05, roughness=0.06, emission_rgb=(0.24, 0.56, 0.72), emission_strength=0.35)
    # Translucent outer polycarbonate shells (alphaMode=BLEND, drawn LAST!)
    m_jug_glass = make_mat("PolycarbonateJug19L", (0.82, 0.94, 1.0, 1.0), metallic=0.04, roughness=0.08, image=img_jug, alpha=0.46)
    m_cup_tube = make_mat("CupTubeTranslucent", (0.76, 0.84, 0.90, 1.0), metallic=0.06, roughness=0.14, alpha=0.34)

    # 1) Grounded rubber feet (exact Z=0.000) & recessed dark plinth
    for fx in (-0.155, 0.155):
        for fy in (-0.155, 0.155):
            add_smooth_cyl(f"Foot_{fx}_{fy}", (fx, fy, 0.015), 0.028, 0.034, 0.030, m_charcoal, verts=20, bevel=0.003)
    add_superellipse_loft(
        "BasePlinth",
        [(0.022, 0.195, 0.192, 0.0, 4.5), (0.068, 0.208, 0.202, 0.0, 4.5)],
        m_charcoal,
        n_seg=44,
    )

    # 2) Sleek contoured superellipse matte-ivory dispenser body (NOT a boxy cube!)
    # Main body front extends to y = -0.204 so the dark recessed alcove at y = -0.214 reads deeply sculpted!
    body_sections = [
        (0.064, 0.214, 0.200, 0.004, 4.2),
        (0.110, 0.220, 0.204, 0.000, 3.8),
        (0.560, 0.222, 0.205, -0.001, 3.6),
        (0.950, 0.220, 0.203, 0.000, 3.6),
        (1.015, 0.212, 0.196, 0.002, 3.4),
        (1.038, 0.188, 0.178, 0.002, 3.2),
    ]
    add_superellipse_loft("CoolerMainBody", body_sections, m_ivory, n_seg=48, uv_z_range=(0.06, 1.04))

    # Brushed anodized silver vertical side accent rails + charcoal recessed louver vents
    for sx in (-1, 1):
        add_beveled_box(f"SilverTrimRail_{sx}", (sx * 0.214, -0.072, 0.55), (0.022, 0.22, 0.89), m_silver, bevel=0.009, segments=3)
        add_beveled_box(f"SideVentFrame_{sx}", (sx * 0.218, 0.065, 0.34), (0.014, 0.16, 0.36), m_charcoal, bevel=0.005)
        for lv in range(7):
            add_beveled_box(f"SideLouvre_{sx}_{lv}", (sx * 0.223, 0.065, 0.21 + lv * 0.042), (0.008, 0.125, 0.013), m_alcove, bevel=0.002)

    # Lower contoured mini-fridge door panel + recessed brushed chrome handle
    add_beveled_box("FridgeDoorPanel", (0.0, -0.212, 0.31), (0.345, 0.022, 0.44), m_ivory, bevel=0.011, segments=3)
    add_beveled_box("FridgeDoorSeam", (0.0, -0.204, 0.31), (0.356, 0.012, 0.452), m_charcoal, bevel=0.005)
    add_beveled_box("FridgeHandlePocket", (0.115, -0.221, 0.39), (0.028, 0.012, 0.145), m_alcove, bevel=0.004)
    add_smooth_cyl("FridgeHandleBar", (0.115, -0.229, 0.39), 0.007, 0.007, 0.132, m_chrome, verts=16, bevel=0.002)

    # Sleek upper dark-glass status fascia with 3 subtle glowing LEDs (Power, Hot, Cold)
    add_beveled_box("StatusBezel", (0.0, -0.214, 0.962), (0.33, 0.020, 0.056), m_alcove, bevel=0.008, segments=3)
    add_beveled_box("StatusSilverTrim", (0.0, -0.210, 0.962), (0.342, 0.012, 0.066), m_silver, bevel=0.005)
    add_smooth_cyl("LedHot", (-0.072, -0.224, 0.962), 0.0075, 0.0075, 0.008, m_led_hot, verts=16, rot=(math.pi / 2, 0, 0))
    add_smooth_cyl("LedPwr", (0.0, -0.224, 0.962), 0.0075, 0.0075, 0.008, m_led_pwr, verts=16, rot=(math.pi / 2, 0, 0))
    add_smooth_cyl("LedCold", (0.072, -0.224, 0.962), 0.0075, 0.0075, 0.008, m_led_cold, verts=16, rot=(math.pi / 2, 0, 0))

    # 3) Deep sculpted recessed dark cup alcove (back wall clearly exposed at y=-0.215, cheeks jut out to y=-0.254!)
    add_beveled_box("AlcoveBackWall", (0.0, -0.208, 0.775), (0.30, 0.018, 0.275), m_alcove, bevel=0.006, segments=2)
    add_beveled_box("AlcoveArchTop", (0.0, -0.224, 0.908), (0.344, 0.058, 0.032), m_charcoal, bevel=0.010, segments=3)
    for sx in (-1, 1):
        add_beveled_box(f"AlcoveCheek_{sx}", (sx * 0.162, -0.224, 0.775), (0.034, 0.058, 0.280), m_charcoal, bevel=0.010, segments=3)

    # Sculpted protruding drip basin + perforated stainless-steel drip grille!
    add_beveled_box("DripBasinBody", (0.0, -0.242, 0.614), (0.332, 0.088, 0.044), m_charcoal, bevel=0.012, segments=3)
    add_beveled_box("DripPlateOuter", (0.0, -0.244, 0.636), (0.314, 0.078, 0.008), m_chrome, bevel=0.003)
    # Recessed dark basin slot + crisp stainless slats & cross-ribs (reads like perforated stainless grille!)
    add_beveled_box("DripInnerVoid", (0.0, -0.246, 0.639), (0.276, 0.058, 0.004), m_alcove, bevel=0.001)
    for gx in range(-6, 7):
        add_beveled_box(f"GrilleRibX_{gx}", (gx * 0.021, -0.246, 0.642), (0.006, 0.058, 0.004), m_chrome, bevel=0.001)
    for gy in (-0.225, -0.239, -0.253, -0.267):
        add_beveled_box(f"GrilleRibY_{gy:.3f}", (0.0, gy, 0.642), (0.276, 0.005, 0.004), m_chrome, bevel=0.001)

    # Ergonomic Hot (red w/ safety lock) & Cold (blue) push-lever spigots
    for sx, m_tap, tag in ((-0.068, m_hot, "Hot"), (0.068, m_cold, "Cold")):
        add_smooth_cyl(f"TapCollar_{tag}", (sx, -0.222, 0.876), 0.020, 0.018, 0.024, m_silver, verts=20, bevel=0.003, rot=(math.pi / 2, 0, 0))
        add_smooth_cyl(f"TapSpigot_{tag}", (sx, -0.240, 0.876), 0.014, 0.012, 0.038, m_chrome, verts=20, bevel=0.002, rot=(math.pi / 2, 0, 0))
        add_smooth_cyl(f"TapNozzle_{tag}", (sx, -0.250, 0.854), 0.008, 0.011, 0.032, m_chrome, verts=18, bevel=0.002)
        # Sculpted curved push-paddle lever
        add_beveled_box(f"PushPaddle_{tag}", (sx, -0.230, 0.806), (0.052, 0.012, 0.076), m_tap, bevel=0.005, segments=3, rot=(0.16, 0, 0))
        add_beveled_box(f"TopThumbLever_{tag}", (sx, -0.256, 0.892), (0.036, 0.030, 0.011), m_tap, bevel=0.004)
        if tag == "Hot":
            add_beveled_box("HotSafetyTab", (sx, -0.270, 0.896), (0.014, 0.010, 0.014), m_ivory, bevel=0.002)

    # Ribbed disposable paper cup resting slightly off-center under the cold spigot with water inside
    add_smooth_cyl("AlcoveCup", (0.064, -0.246, 0.684), 0.024, 0.033, 0.080, m_cup, verts=24, bevel=0.002)
    add_torus("AlcoveCupRim", (0.064, -0.246, 0.723), 0.033, 0.0028, m_cup, major_seg=24, minor_seg=8)
    add_smooth_cyl("AlcoveCupWater", (0.064, -0.246, 0.708), 0.029, 0.029, 0.006, m_meniscus, verts=20, bevel=0.001)

    # 4) Side cylindrical translucent paper-cup dispenser tube (+X) with visible nested cups inside!
    tube_x, tube_y = 0.264, -0.065
    for bz in (0.68, 0.88):
        add_beveled_box(f"CupMount_{bz:.2f}", (0.228, tube_y, bz), (0.044, 0.036, 0.026), m_charcoal, bevel=0.005)
        add_torus(f"CupMountRing_{bz:.2f}", (tube_x, tube_y, bz), 0.045, 0.0055, m_charcoal, major_seg=24, minor_seg=8)
    # Hinged top flip-lid & bottom stainless pull-out nozzle
    add_smooth_cyl("CupTubeLid", (tube_x, tube_y, 0.956), 0.045, 0.043, 0.020, m_charcoal, verts=26, bevel=0.004)
    add_smooth_cyl("CupTubeNozzle", (tube_x, tube_y, 0.608), 0.033, 0.043, 0.034, m_silver, verts=24, bevel=0.003)
    # Nested stack of 9 opaque paper cups INSIDE the translucent tube (visible through alpha BLEND!)
    for ci in range(9):
        cz = 0.642 + ci * 0.028
        add_smooth_cyl(f"NestedCup_{ci}", (tube_x, tube_y, cz), 0.024, 0.033, 0.056, m_cup, verts=20, bevel=0.0015)
        add_torus(f"NestedCupRim_{ci}", (tube_x, tube_y, cz + 0.027), 0.033, 0.0025, m_cup, major_seg=20, minor_seg=8)
    # Translucent outer cylindrical polycarbonate tube (drawn AFTER nested cups!)
    add_smooth_cyl("CupDispenserTube", (tube_x, tube_y, 0.785), 0.042, 0.042, 0.324, m_cup_tube, verts=28, bevel=0.003)

    # 5) Bottle receiver collar + Contoured ribbed 19L translucent sky-blue polycarbonate jug!
    add_lathe_shell(
        "ReceiverCollar",
        [(1.028, 0.165), (1.055, 0.158), (1.084, 0.136), (1.092, 0.118), (1.082, 0.074)],
        m_charcoal,
        n_seg=40,
    )
    add_torus("CollarSilverRing", (0.0, 0.0, 1.044), 0.162, 0.008, m_silver, major_seg=36, minor_seg=10)

    # Inner opaque/luminous sky-blue water column + bright glossy meniscus disc + tiny trapped bubbles
    inner_water_profile = [
        (1.095, 0.056),
        (1.132, 0.062),
        (1.168, 0.125),
        (1.198, 0.171),
        (1.485, 0.171),
        (1.505, 0.168),
    ]
    add_lathe_shell("InnerWaterColumn", inner_water_profile, m_water_inner, n_seg=40)
    # Concave-rim water meniscus surface (bright sparkling cyan ring!)
    add_torus("WaterMeniscusRim", (0.0, 0.0, 1.508), 0.163, 0.009, m_meniscus, major_seg=40, minor_seg=10)
    add_smooth_cyl("WaterMeniscusDisc", (0.0, 0.0, 1.504), 0.166, 0.166, 0.007, m_meniscus, verts=36, bevel=0.002)
    # Tiny rising trapped air bubbles inside the water!
    for bx, by, bz, br in [(0.04, -0.05, 1.24, 0.011), (-0.05, -0.03, 1.32, 0.009), (0.02, -0.08, 1.40, 0.013), (-0.02, 0.04, 1.46, 0.008)]:
        add_sphere(f"AirBubble_{bz:.2f}", (bx, by, bz), br, m_meniscus, segments=12, rings=8)

    # Outer contoured deeply-ribbed 19L polycarbonate jug (Z up to 1.702, translucent BLEND drawn LAST!)
    jug_profile = [
        (1.078, 0.062),  # Inverted bottle neck seated inside collar
        (1.128, 0.065),
        (1.142, 0.082),  # Neck flange ring
        (1.158, 0.128),  # Flared shoulder
        (1.186, 0.180),
        (1.202, 0.194),  # Rib 1 crest
        (1.220, 0.194),
        (1.234, 0.181),  # Rib 1 trough
        (1.298, 0.181),
        (1.314, 0.197),  # Rib 2 crest
        (1.334, 0.197),
        (1.350, 0.181),  # Rib 2 trough
        (1.418, 0.181),
        (1.434, 0.197),  # Rib 3 crest
        (1.454, 0.197),
        (1.470, 0.181),  # Rib 3 trough
        (1.552, 0.183),
        (1.592, 0.174),  # Upper chamfered dome
        (1.638, 0.146),
        (1.674, 0.106),
        (1.694, 0.082),  # Molded inverted-base rim
        (1.702, 0.058),
    ]
    add_lathe_shell("PolycarbonateJug19L", jug_profile, m_jug_glass, n_seg=48)
    # Glossy molded rim crown ring on top of inverted 19L bottle
    add_torus("JugTopCrownRim", (0.0, 0.0, 1.694), 0.082, 0.008, m_jug_glass, major_seg=36, minor_seg=10)

    # Material order: ALL opaque inner materials FIRST, translucent BLEND shells LAST!
    mat_order = [
        m_ivory,
        m_silver,
        m_chrome,
        m_charcoal,
        m_alcove,
        m_hot,
        m_cold,
        m_led_hot,
        m_led_cold,
        m_led_pwr,
        m_cup,
        m_water_inner,
        m_meniscus,
        m_cup_tube,
        m_jug_glass,
    ]
    return join_bake_ao_and_export(
        "prop_water_cooler.glb",
        mat_order,
        no_ao_mats=("LedHotGlow", "LedColdGlow", "LedPwrGlow", "InnerWaterVolume", "WaterMeniscusRing", "PolycarbonateJug19L", "CupTubeTranslucent"),
        ao_strength=0.34,
        max_dist=0.12,
    )


# ==============================================================================
# PROP 3: `prop_xerox_printer.glb`
# Target BL bounds: X in [-0.882, +0.640], Y in [-0.437, +0.450], Z in [0.000, 1.315]
# FRONT faces +Y_blender (-Z_raylib -> rotated 180 deg in game!), Side Output Tray on -X
# ==============================================================================
def build_prop_xerox_printer():
    clear_scene()
    img_body = load_packed_image("copier_body_tex.png")
    img_panel = load_packed_image("copier_panel_tex.png")
    img_sheet = load_packed_image("printed_sheet_tex.png")

    # Calibrated industrial office copier palette (warm off-white polymer + deep charcoal + glowing controls)
    m_shell = make_mat("XeroxWarmShell", (0.90, 0.90, 0.91, 1.0), metallic=0.06, roughness=0.38, image=img_body)
    m_trim = make_mat("XeroxDarkSlate", (0.065, 0.076, 0.094, 1.0), metallic=0.22, roughness=0.40)
    m_mid = make_mat("XeroxMidGraphite", (0.18, 0.20, 0.24, 1.0), metallic=0.28, roughness=0.36)
    m_chrome = make_mat("XeroxChrome", (0.64, 0.68, 0.74, 1.0), metallic=0.88, roughness=0.20)
    m_rubber = make_mat("XeroxRubber", (0.032, 0.035, 0.040, 1.0), metallic=0.04, roughness=0.76)
    m_glass = make_mat("ScannerGlass", (0.025, 0.045, 0.055, 1.0), metallic=0.15, roughness=0.06)
    m_scan_green = make_mat("ScannerGreenBar", (0.14, 0.96, 0.48, 1.0), roughness=0.15, emission_rgb=(0.14, 0.98, 0.50), emission_strength=1.25)
    m_panel = make_mat("ControlPanelScreen", (1.0, 1.0, 1.0, 1.0), metallic=0.08, roughness=0.20, image=img_panel, emission_strength=0.42)
    m_btn_green = make_mat("BtnStartGreen", (0.12, 0.78, 0.34, 1.0), roughness=0.22, emission_rgb=(0.14, 0.85, 0.38), emission_strength=0.85)
    m_btn_amber = make_mat("BtnWarnAmber", (0.94, 0.56, 0.08, 1.0), roughness=0.22, emission_rgb=(0.98, 0.62, 0.10), emission_strength=0.90)
    m_beacon_red = make_mat("JamBeaconRed", (0.95, 0.08, 0.06, 1.0), roughness=0.14, emission_rgb=(1.0, 0.12, 0.08), emission_strength=1.35)
    m_paper = make_mat("PrintedPassSheet", (1.0, 1.0, 1.0, 1.0), metallic=0.0, roughness=0.68, image=img_sheet)
    m_paper_plain = make_mat("PaperReamWhite", (0.88, 0.87, 0.84, 1.0), metallic=0.0, roughness=0.72)

    # 1) Rolling caster base plinth (exact Z=0.000)
    for cx in (-0.48, 0.48):
        for cy in (-0.30, 0.30):
            add_caster_wheel(f"CopierCaster_{cx}_{cy}", cx, cy, 0.090, m_chrome, m_rubber, wheel_r=0.032, yaw=(0.25 if cx > 0 else -0.35))
    add_beveled_box("BasePlinth", (0.0, 0.0, 0.098), (1.16, 0.76, 0.056), m_trim, bevel=0.012, segments=3)

    # 2) Stepped multi-tier main cabinet (Z in [0.12, 1.055])
    # Lower paper-tray housing (Z: 0.12 .. 0.74)
    add_beveled_box("LowerCabinetCore", (0.0, -0.02, 0.43), (1.20, 0.76, 0.62), m_shell, bevel=0.024, segments=4)
    # Dark charcoal front recessed bay surround for the 3 paper trays
    add_beveled_box("TrayBaySurround", (-0.04, 0.352, 0.425), (0.92, 0.048, 0.565), m_trim, bevel=0.010, segments=2)

    # 3 recessed paper drawer trays facing +Y with sculpted grips (Tray 2 slightly ajar with jammed corner!)
    trays = [
        (0.22, 0.0, "Tray3_A3"),
        (0.425, 0.028, "Tray2_Jammed"),  # slightly pulled out!
        (0.625, 0.0, "Tray1_A4"),
    ]
    for tz, pull_y, tag in trays:
        ty = 0.378 + pull_y
        add_beveled_box(f"{tag}_Front", (-0.04, ty, tz), (0.87, 0.056, 0.168), m_shell, bevel=0.012, segments=3)
        # Dark recessed finger-pull pocket + curved horizontal handle bar
        add_beveled_box(f"{tag}_Pocket", (-0.04, ty + 0.022, tz + 0.022), (0.26, 0.024, 0.048), m_trim, bevel=0.006)
        add_beveled_box(f"{tag}_Handle", (-0.04, ty + 0.034, tz + 0.038), (0.28, 0.018, 0.016), m_mid, bevel=0.005)
        # Paper level gauge window + tiny size tag
        add_beveled_box(f"{tag}_Gauge", (0.29, ty + 0.026, tz), (0.014, 0.008, 0.085), m_trim, bevel=0.002)
        add_beveled_box(f"{tag}_Tag", (-0.36, ty + 0.026, tz + 0.035), (0.068, 0.008, 0.028), m_mid, bevel=0.002)
        if pull_y > 0.01:
            # Show inner steel drawer sides + white paper stack + jammed corner sticking out!
            add_beveled_box(f"{tag}_InnerBox", (-0.04, ty - 0.045, tz - 0.01), (0.82, 0.08, 0.13), m_mid, bevel=0.004)
            add_beveled_box(f"{tag}_PaperReam", (-0.04, ty - 0.035, tz - 0.02), (0.76, 0.07, 0.075), m_paper_plain, bevel=0.002)
            # Jammed crooked sheet sticking clearly out of Tray 2 gap!
            add_uv_quad_solid(
                "JammedSheetCorner",
                [(-0.24, ty + 0.005, tz + 0.068), (0.04, ty + 0.008, tz + 0.072), (0.01, ty + 0.088, tz + 0.110), (-0.27, ty + 0.080, tz + 0.102)],
                0.0022,
                m_paper,
                uv_rect=(0.05, 0.05, 0.95, 0.95),
                curl_bottom=0.012,
                normal_dir=(0.0, 0.45, 0.89),
            )

    # Right-side toner access door + vertical recessed latch + fold-out bypass tray (folded flush on +X)
    add_beveled_box("TonerDoor", (0.485, 0.364, 0.425), (0.175, 0.022, 0.54), m_shell, bevel=0.009)
    add_beveled_box("TonerDoorGrip", (0.425, 0.376, 0.52), (0.022, 0.012, 0.095), m_trim, bevel=0.004)
    add_beveled_box("BypassTrayFolded", (0.612, 0.02, 0.56), (0.028, 0.46, 0.30), m_mid, bevel=0.008, segments=2)
    add_beveled_box("BypassTrayHandle", (0.628, 0.02, 0.66), (0.014, 0.16, 0.026), m_trim, bevel=0.004)
    # Ventilation louvres on right lower side
    for lv in range(6):
        add_beveled_box(f"SideVent_{lv}", (0.604, -0.22, 0.24 + lv * 0.034), (0.012, 0.22, 0.012), m_trim, bevel=0.002)

    # Stepped mid-section with recessed dark internal output cavity (Z: 0.74 .. 0.94)
    add_beveled_box("MidBackPillar", (0.0, -0.21, 0.84), (1.20, 0.38, 0.22), m_shell, bevel=0.020, segments=3)
    add_beveled_box("MidRightColumn", (0.44, 0.08, 0.84), (0.32, 0.54, 0.22), m_shell, bevel=0.018, segments=3)
    add_beveled_box("OutputCavityFloor", (-0.16, 0.11, 0.748), (0.86, 0.48, 0.024), m_trim, bevel=0.006)
    add_beveled_box("OutputCavityBack", (-0.16, -0.04, 0.84), (0.86, 0.04, 0.18), m_trim, bevel=0.005)
    # Paper exit roller slot inside the dark cavity
    add_smooth_cyl("ExitRoller", (-0.18, 0.02, 0.835), 0.016, 0.016, 0.64, m_mid, verts=16, rot=(0, math.pi / 2, 0))

    # Overhanging upper flatbed scanner deck (Z: 0.94 .. 1.055)
    add_beveled_box("ScannerDeckBody", (0.0, -0.01, 0.995), (1.22, 0.80, 0.115), m_shell, bevel=0.020, segments=4)
    add_beveled_box("ScannerDeckBumper", (0.0, 0.0, 0.952), (1.235, 0.815, 0.026), m_trim, bevel=0.008)

    # 3) Flatbed scanner glass + glowing green scanning carriage bar underneath!
    add_beveled_box("ScanBedBezel", (-0.04, -0.01, 1.054), (1.02, 0.68, 0.014), m_trim, bevel=0.004)
    add_beveled_box("ScanGlassPane", (-0.04, -0.01, 1.060), (0.94, 0.60, 0.006), m_glass, bevel=0.001)
    # Glowing emerald scanner light bar slightly visible inside!
    add_beveled_box("ScanGlowBar", (-0.18, -0.01, 1.063), (0.036, 0.56, 0.008), m_scan_green, bevel=0.002)
    add_beveled_box("ScanRail", (-0.04, -0.26, 1.061), (0.88, 0.014, 0.006), m_chrome, bevel=0.001)

    # 4) Openable hinged ADF scanner lid propped slightly open (~11.5 deg around rear hinge at Y = -0.37)
    hinge_y, hinge_z = -0.37, 1.065
    lid_pitch = math.radians(-11.5)  # opens upward toward +Y revealing green scan bar glow!
    # Rear robust hinges
    for hx in (-0.36, 0.28):
        add_beveled_box(f"LidHinge_{hx}", (hx, hinge_y - 0.02, hinge_z + 0.018), (0.088, 0.072, 0.058), m_trim, bevel=0.008)

    # Build lid sub-parts in local lid frame, then rotate around (0, hinge_y, hinge_z)
    lid_parts = []
    def add_lid_part(obj):
        lid_parts.append(obj)
        return obj

    add_lid_part(add_beveled_box("AdfBasePlate", (-0.04, 0.0, 1.088), (1.08, 0.72, 0.046), m_shell, bevel=0.014, segments=3))
    add_lid_part(add_beveled_box("AdfFrontHandleLip", (-0.04, 0.358, 1.088), (0.46, 0.026, 0.028), m_trim, bevel=0.008))
    # Raised ADF roller housing on left side of top lid
    add_lid_part(add_beveled_box("AdfFeederHousing", (-0.34, 0.0, 1.158), (0.38, 0.58, 0.115), m_shell, bevel=0.024, segments=4))
    add_lid_part(add_beveled_box("AdfFeederTrim", (-0.34, 0.0, 1.212), (0.32, 0.52, 0.016), m_mid, bevel=0.006))
    # Angled upper document input tray + adjustable side paper guides
    add_lid_part(add_beveled_box("AdfInputTray", (0.14, 0.0, 1.162), (0.56, 0.44, 0.016), m_mid, bevel=0.005, rot=(0, -0.14, 0)))
    for gy in (-0.17, 0.17):
        add_lid_part(add_beveled_box(f"AdfGuide_{gy}", (0.16, gy, 1.178), (0.42, 0.016, 0.022), m_trim, bevel=0.004, rot=(0, -0.14, 0)))
    # Stack of original pages waiting in the ADF feeder
    add_lid_part(add_beveled_box("AdfWaitingPages", (0.12, 0.0, 1.178), (0.34, 0.28, 0.014), m_paper_plain, bevel=0.002, rot=(0, -0.14, 0.04)))

    pivot = V((0.0, hinge_y, hinge_z))
    rot_m = Matrix.Rotation(lid_pitch, 4, "X")
    for p in lid_parts:
        for v in p.data.vertices:
            v.co = pivot + (rot_m @ (v.co - pivot))
        p.data.update()

    # 5) Tilted ergonomic touchscreen control panel facing +Y (front-right, x in [0.10, 0.56])
    px_c, py_c, pz_c = 0.33, 0.398, 1.072
    panel_tilt = math.radians(48.0)  # tilted back toward standing user at +Y
    # Support pedestal neck
    add_beveled_box("PanelNeck", (px_c, 0.355, 1.032), (0.18, 0.075, 0.068), m_trim, bevel=0.010)
    # Console housing bezel
    add_beveled_box("PanelHousing", (px_c, py_c, pz_c), (0.42, 0.036, 0.255), m_trim, bevel=0.011, segments=3, rot=(panel_tilt, 0, 0))

    # Crisp glowing UV-mapped touchscreen + keypad face sitting 19mm in front of console center!
    # Local face coordinates rotated by panel_tilt around X:
    # Viewing from +Y looking toward -Y: Left is +X, Right is -X!
    # Wait: let's make sure Russian text "XEROX-9000 PRO" reads LEFT-TO-RIGHT when standing at +Y looking at -Y!
    # Standing at +Y looking toward -Y: viewer's RIGHT is -X, viewer's LEFT is +X!
    rm_p = Matrix.Rotation(panel_tilt, 4, "X")
    p_center = V((px_c, py_c, pz_c))
    hw, hh = 0.192, 0.112
    # [bl, br, tr, tl] as seen by viewer standing at +Y (bl = +X, -Z; br = -X, -Z; tr = -X, +Z; tl = +X, +Z)
    local_corners = [V((hw, 0.020, -hh)), V((-hw, 0.020, -hh)), V((-hw, 0.020, hh)), V((hw, 0.020, hh))]
    world_corners = [p_center + (rm_p @ c) for c in local_corners]
    n_panel = (rm_p @ V((0, 1, 0))).normalized()
    add_uv_quad_solid("ControlPanelLCD", world_corners, 0.004, m_panel, uv_rect=(0.01, 0.01, 0.99, 0.99), normal_dir=n_panel)

    # Raised physical 3D glowing tactile status buttons along the lower front lip of the console bezel (zero text overlap!)
    for bx_l, bz_l, bmat, btag in [(-0.140, -0.120, m_btn_green, "StartBtn3D"), (-0.075, -0.120, m_btn_amber, "WarnBtn3D"), (0.145, -0.120, m_beacon_red, "AlertLed3D")]:
        bpos = p_center + (rm_p @ V((bx_l, 0.019, bz_l)))
        add_beveled_box(btag, bpos, (0.044, 0.010, 0.014), bmat, bevel=0.003, rot=(panel_tilt, 0, 0))

    # 6) Tiny red paper-jam warning beacon on slim chrome stalk (front-left corner, x=-0.52, y=+0.30)
    bx, by = -0.52, 0.30
    add_smooth_cyl("BeaconBase", (bx, by, 1.062), 0.026, 0.022, 0.016, m_trim, verts=20, bevel=0.003)
    add_smooth_cyl("BeaconStalk", (bx, by, 1.102), 0.007, 0.007, 0.068, m_chrome, verts=14)
    add_smooth_cyl("BeaconCollar", (bx, by, 1.138), 0.022, 0.020, 0.016, m_trim, verts=20, bevel=0.003)
    add_lathe_shell(
        "JamBeaconDome",
        [(1.145, 0.021), (1.178, 0.020), (1.192, 0.015), (1.200, 0.004)],
        m_beacon_red,
        n_seg=24,
        loc=(bx, by, 0.0),
    )

    # 7) Side output finisher tray on -X (x in [-0.88, -0.60], Z ~ 0.67..0.82) + crooked freshly printed sheets!
    tray_rot = (0, math.radians(-16.0), 0)
    add_beveled_box("SideOutputTray", (-0.735, 0.0, 0.725), (0.31, 0.44, 0.018), m_trim, bevel=0.006, segments=2, rot=tray_rot)
    add_beveled_box("SideOutputLip", (-0.868, 0.0, 0.778), (0.018, 0.44, 0.044), m_mid, bevel=0.005, rot=tray_rot)
    for sy in (-0.215, 0.215):
        add_beveled_box(f"SideOutputWing_{sy:.2f}", (-0.735, sy, 0.742), (0.29, 0.014, 0.036), m_mid, bevel=0.004, rot=tray_rot)
    # Support struts beneath side tray
    for sy in (-0.15, 0.15):
        add_tube_between(f"TrayStrut_{sy:.2f}", (-0.60, sy, 0.60), (-0.78, sy, 0.735), 0.010, m_trim, verts=14)

    # 6 crooked freshly-printed Russian pass sheets fanned naturally on the output tray!
    rm_t = Matrix.Rotation(math.radians(-16.0), 4, "Y")
    tray_origin = V((-0.735, 0.0, 0.738))
    sheet_specs = [
        (0.015, -0.015, 0.002, 0.04, 0.002),
        (0.008, 0.012, 0.006, -0.07, 0.004),
        (-0.002, -0.008, 0.010, 0.11, 0.006),
        (-0.010, 0.018, 0.014, -0.14, 0.009),
        (-0.018, -0.012, 0.018, 0.08, 0.014),
        (-0.025, 0.005, 0.023, -0.18, 0.022),  # top sheet curled dynamically!
    ]
    for si, (sx, sy, sz, yaw, curl) in enumerate(sheet_specs):
        ca, sa = math.cos(yaw), math.sin(yaw)
        sw, sh = 0.118, 0.152  # A4-ish pass sheet half-sizes
        loc_pts = [(-sw, -sh), (sw, -sh), (sw, sh), (-sw, sh)]
        w_pts = []
        for lx, ly in loc_pts:
            rx = lx * ca - ly * sa + sx
            ry = lx * sa + ly * ca + sy
            w_pts.append(tray_origin + (rm_t @ V((rx, ry, sz))))
        n_sh = (rm_t @ V((0, 0, 1))).normalized()
        add_uv_quad_solid(f"PrintedSheet_{si}", w_pts, 0.0016, m_paper, uv_rect=(0.02, 0.02, 0.98, 0.98), curl_bottom=curl, normal_dir=n_sh)

    mat_order = [
        m_shell,
        m_trim,
        m_mid,
        m_chrome,
        m_rubber,
        m_glass,
        m_scan_green,
        m_panel,
        m_btn_green,
        m_btn_amber,
        m_beacon_red,
        m_paper,
        m_paper_plain,
    ]
    return join_bake_ao_and_export(
        "prop_xerox_printer.glb",
        mat_order,
        no_ao_mats=("ScannerGreenBar", "ControlPanelScreen", "BtnStartGreen", "BtnWarnAmber", "JamBeaconRed", "PrintedPassSheet"),
        ao_strength=0.38,
        max_dist=0.14,
    )


# ==============================================================================
# 3-PANEL STUDIO PROOF RENDER (Cycles + Backface Culling Check)
# ==============================================================================
def render_single_panel(fname, out_png, rot_z_deg, cam_loc, cam_target, lens=46.0):
    clear_scene()
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 40
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 800
    scene.render.resolution_y = 900
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Punchy"
    scene.view_settings.exposure = 0.25

    world = bpy.data.worlds.new("ProofWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value = (0.042, 0.048, 0.062, 1.0)
    bg.inputs["Strength"].default_value = 0.90

    bpy.ops.mesh.primitive_plane_add(size=18.0, location=(0, 0, 0.0))
    floor = bpy.context.active_object
    m_floor = make_mat("StudioFloor", (0.085, 0.095, 0.115, 1.0), metallic=0.05, roughness=0.60)
    floor.data.materials.append(m_floor)

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / fname))
    added = [o for o in bpy.context.scene.objects if o not in before]
    for o in added:
        if o.parent is None:
            o.rotation_mode = "XYZ"
            o.rotation_euler = (0.0, 0.0, math.radians(rot_z_deg))
        if o.type == "MESH":
            for m in o.data.materials:
                if m:
                    m.use_backface_culling = True

    def add_area(name, loc, target, energy, size, color):
        ld = bpy.data.lights.new(name, type="AREA")
        ld.energy = energy
        ld.size = size
        ld.color = color
        lo = bpy.data.objects.new(name, ld)
        bpy.context.collection.objects.link(lo)
        lo.location = loc
        direction = V(target) - V(loc)
        lo.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    add_area("KeyLight", (-2.2, -3.6, 3.4), cam_target, 320, 2.2, (1.0, 0.95, 0.88))
    add_area("FillLight", (2.8, -2.8, 2.2), cam_target, 160, 2.6, (0.78, 0.90, 1.0))
    add_area("RimLight", (0.3, 3.2, 2.8), cam_target, 210, 2.6, (0.96, 0.94, 1.0))

    cam_data = bpy.data.cameras.new("ProofCam")
    cam_data.lens = lens
    cam_obj = bpy.data.objects.new("ProofCam", cam_data)
    bpy.context.collection.objects.link(cam_obj)
    cam_obj.location = cam_loc
    cam_obj.rotation_euler = (V(cam_target) - cam_obj.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam_obj

    scene.render.filepath = str(out_png)
    bpy.ops.render.render(write_still=True)


def render_wave1_proof():
    import numpy as np

    p1 = TEX_DIR / "_panel_whiteboard.png"
    p2 = TEX_DIR / "_panel_cooler.png"
    p3 = TEX_DIR / "_panel_xerox.png"

    # Panel 1: Whiteboard (front is -Y; slight 16 deg turn shows tubular legs, casters, tray, sticky notes & diagrams)
    render_single_panel("prop_whiteboard.glb", p1, rot_z_deg=14.0, cam_loc=(0.0, -3.55, 1.18), cam_target=(0.0, 0.0, 0.98), lens=44.0)
    # Panel 2: Water Cooler (front is -Y; -20 deg turn shows alcove, red/blue taps, drip grille, cup tube & 19L jug)
    render_single_panel("prop_water_cooler.glb", p2, rot_z_deg=-22.0, cam_loc=(0.04, -2.65, 1.02), cam_target=(0.04, 0.0, 0.86), lens=48.0)
    # Panel 3: Xerox Copier (front is +Y; +148 deg turn shows front 3 trays + jammed Tray 2 + glowing Russian touchscreen + green scan bar + red jam beacon + side output tray with printed passes!)
    render_single_panel("prop_xerox_printer.glb", p3, rot_z_deg=146.0, cam_loc=(0.05, -2.55, 1.12), cam_target=(0.05, 0.0, 0.64), lens=44.0)

    # Stitch 3 panels (800x900 each) cleanly into 2400x900 PNG using Blender image buffers + numpy
    clear_scene()
    imgs = [bpy.data.images.load(str(p)) for p in (p1, p2, p3)]
    W, H = 800, 900
    canvas = np.zeros((H, W * 3, 4), dtype=np.float32)
    for idx, im in enumerate(imgs):
        arr = np.empty(W * H * 4, dtype=np.float32)
        im.pixels.foreach_get(arr)
        canvas[:, idx * W : (idx + 1) * W, :] = arr.reshape((H, W, 4))
    # Subtle 2px dark divider seams between the 3 panels
    canvas[:, W - 1 : W + 1, :3] = 0.08
    canvas[:, 2 * W - 1 : 2 * W + 1, :3] = 0.08

    out_img = bpy.data.images.new("Wave1Proof3Panel", width=W * 3, height=H, alpha=False)
    out_img.pixels.foreach_set(canvas.ravel())
    PROOF_PATH.parent.mkdir(parents=True, exist_ok=True)
    out_img.filepath_raw = str(PROOF_PATH)
    out_img.file_format = "PNG"
    out_img.save()
    print("RENDERED 3-PANEL PROOF:", PROOF_PATH)


if __name__ == "__main__":
    build_prop_whiteboard()
    build_prop_water_cooler()
    build_prop_xerox_printer()
    render_wave1_proof()
