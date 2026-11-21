"""AAA Anti-AI-Slop PC Installer Key Art Renderer (Installer/installer_cover.png, 536x900).

Renders the 90s Executive Partner hero (full-resolution undecimated MPFB anatomy with
baked anatomical AO, matte deep-burgundy virgin wool 90s blazer, torso-conforming
notched lapels, black silk shirt, full-loop 18k gold curb chain, skull-fitted slick-back
executive hair, and 90s rectangular navigator sunglasses) framed waist-up in a
three-quarter portrait slightly off-center right.

Includes real 3D background assets (prop_leather_sofa.glb, prop_server_rack.glb) and
floor-to-ceiling dark steel skyscraper window mullions overlooking a warm golden-hour
Moscow-City dusk skyline. Strictly zero neon pink/purple/cyan slop, shallow depth of
field, AgX Filmic color management, and a calm dark bottom 32% (#0F1216) for white
title typography.
"""
import math
import os
import runpy
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parent.parent
ASSETS_MODELS = ROOT / "assets" / "models"
OUT_PNG = ROOT / "Installer" / "installer_cover.png"
SCRATCH_PNG = Path(r"C:\Users\se-20\.gemini\antigravity\brain\5a288ef4-482c-487e-b05f-e3d0e2267cf7\scratch\installer_cover_preview.png")


def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def xyz(p):
    return (p[0], -p[2], p[1])


def make_wool_texture(name="Tex_BurgundyWool", size=512, base_rgb=(0.175, 0.0075, 0.025)):
    """Generates a rich matte burgundy/oxblood virgin wool herringbone micro-texture."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32) / size
    rng = np.random.default_rng(1994)
    grain = rng.normal(0.0, 0.024, (size, size)).astype(np.float32)
    zig = np.abs((x * 32.0) % 2.0 - 1.0) * 14.0
    herring = 0.020 * np.sin((x * 160.0 + zig + y * 160.0) * math.tau)
    weave = 0.012 * np.cos((x * 320.0 - y * 320.0) * math.tau)
    shade = np.clip(1.0 + grain + herring + weave, 0.88, 1.10)

    img = np.zeros((size, size, 4), dtype=np.float32)
    img[..., 0] = base_rgb[0] * shade
    img[..., 1] = base_rgb[1] * shade
    img[..., 2] = base_rgb[2] * shade
    img[..., 3] = 1.0

    b_img = bpy.data.images.new(name, width=size, height=size, alpha=False)
    b_img.pixels.foreach_set(img.ravel())
    b_img.pack()
    return b_img


def make_skyline_backdrop_texture(name="Tex_GoldenHourSky", w=512, h=512):
    """Warm golden-hour dusk sky gradient (dark slate zenith -> warm amber/gold horizon)."""
    y, x = np.mgrid[0:h, 0:w].astype(np.float32)
    u = x / w
    v = y / h  # v=0 bottom, v=1 top
    img = np.zeros((h, w, 4), dtype=np.float32)

    horizon_glow = np.exp(-(((v - 0.36) / 0.24) ** 2))
    sun_disc = np.exp(-(((u - 0.28) ** 2) / 0.07 + ((v - 0.40) ** 2) / 0.05))

    img[..., 0] = 0.022 + 0.34 * horizon_glow + 0.56 * sun_disc
    img[..., 1] = 0.026 + 0.17 * horizon_glow + 0.32 * sun_disc
    img[..., 2] = 0.036 + 0.06 * horizon_glow + 0.11 * sun_disc
    img[..., 3] = 1.0

    b_img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    b_img.pixels.foreach_set(np.clip(img, 0.0, 1.0).ravel())
    b_img.pack()
    return b_img


def make_pbr_mat(
    name,
    rgba,
    metallic=0.0,
    roughness=0.5,
    specular=0.35,
    emission_rgb=None,
    emission_strength=0.0,
    tex_img=None,
    subsurf=0.0,
    use_vertex_ao=False,
):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    bsdf = nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = specular
        if subsurf > 0.0 and "Subsurface Weight" in bsdf.inputs:
            bsdf.inputs["Subsurface Weight"].default_value = subsurf
            if "Subsurface Scale" in bsdf.inputs:
                bsdf.inputs["Subsurface Scale"].default_value = 0.008
        if emission_rgb and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*emission_rgb, 1.0)
            bsdf.inputs["Emission Strength"].default_value = emission_strength
        if tex_img is not None:
            tex = nodes.new("ShaderNodeTexImage")
            tex.image = tex_img
            links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        elif use_vertex_ao:
            attr = nodes.new("ShaderNodeAttribute")
            attr.attribute_name = "AnatomicalAO"
            links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def set_only_mat(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.material_index = 0
        p.use_smooth = True


def apply_box_uvs(obj, scale_uv=3.0):
    mesh = obj.data
    if not mesh.uv_layers:
        mesh.uv_layers.new(name="UVMap")
    uv_layer = mesh.uv_layers.active.data
    verts = mesh.vertices
    for poly in mesh.polygons:
        n = poly.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop_idx in poly.loop_indices:
            v = verts[mesh.loops[loop_idx].vertex_index].co
            if az >= ax and az >= ay:
                u, w = v.x * scale_uv, v.y * scale_uv
            elif ax >= ay and ax >= az:
                u, w = v.y * scale_uv, v.z * scale_uv
            else:
                u, w = v.x * scale_uv, v.z * scale_uv
            uv_layer[loop_idx].uv = (u + 0.5, w + 0.5)


def blazer_section(y):
    """Smooth Hermite-interpolated (rx, rz, cz) of the 90s burgundy wool blazer at height y."""
    table = [
        (0.850, 0.180, 0.140, 0.030),
        (0.904, 0.182, 0.141, 0.032),
        (1.030, 0.172, 0.135, 0.040),
        (1.190, 0.194, 0.134, 0.046),
        (1.310, 0.214, 0.118, 0.026),
        (1.368, 0.192, 0.092, 0.018),
        (1.404, 0.084, 0.076, 0.038),
    ]
    if y <= table[0][0]:
        return table[0][1:]
    if y >= table[-1][0]:
        return table[-1][0 + 1 :]
    for (y0, rx0, rz0, cz0), (y1, rx1, rz1, cz1) in zip(table, table[1:]):
        if y0 <= y <= y1:
            t = (y - y0) / (y1 - y0)
            ts = t * t * (3.0 - 2.0 * t)
            return rx0 + (rx1 - rx0) * ts, rz0 + (rz1 - rz0) * ts, cz0 + (cz1 - cz0) * ts
    return table[-1][1:]


def torso_front_z(x, y, offset=0.0):
    rx, rz, cz = blazer_section(y)
    u = min(0.96, abs(x) / max(0.05, rx))
    return cz + rz * math.sqrt(max(0.04, 1.0 - u * u)) + offset


def executive_arm_transforms(ga):
    """Poses the Partner's arms in a composed, authoritative executive low stance at his sides/waist
    so his chest, tailored burgundy lapels, black silk shirt, and gold chain are 100% unobstructed."""
    transforms = {}
    frame_fn = ga["frame"]
    for side in ("l", "r"):
        sign = 1.0 if side == "l" else -1.0
        j = lambda k: ga["JOINT"][side + "-" + k]
        shoulder, elbow, wrist = j("shoulder"), j("elbow"), j("hand")

        # Natural tailored executive arms hanging down cleanly at sides with slight forward bend to lower waist
        dest_elbow = V((sign * 0.242, 1.07, 0.025))
        dest_wrist = V((sign * 0.175, 0.855, 0.145))

        across = j("finger-2-1") - j("finger-5-1")
        mid = j("finger-3-1")
        f_target = (dest_wrist - dest_elbow).normalized()
        palm_target = V((-sign * 0.85, 0.15, -0.50)).normalized()
        a_target = palm_target.cross(f_target).normalized()
        hand_rot = Matrix((f_target, a_target, palm_target)).transposed() @ frame_fn(mid - wrist, across).transposed()

        for name, a, b, p, q in [
            ("upperarm", shoulder, elbow, shoulder, dest_elbow),
            ("lowerarm", elbow, wrist, dest_elbow, dest_wrist),
        ]:
            src = b - a
            dst = q - p
            u = src.normalized()
            stretch = Matrix.Identity(3) + Matrix([list(u * u[n]) for n in range(3)]) * (dst.length / src.length - 1.0)
            rot = src.rotation_difference(dst).to_matrix()
            transforms[name + "_" + side] = (a, p, rot @ stretch)
        transforms["hand_" + side] = (wrist, dest_wrist, hand_rot)
    return transforms


def bake_skin_ao_and_contour(body_obj, base_rgb=(0.28, 0.155, 0.108)):
    """Bakes 24-ray BVHTree anatomical ambient occlusion + facial contour shading into 'AnatomicalAO'."""
    mesh = body_obj.data
    mesh.update()
    verts = mesh.vertices
    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(f.vertices) for f in mesh.polygons])

    if "AnatomicalAO" in mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes["AnatomicalAO"])
    col_attr = mesh.color_attributes.new(name="AnatomicalAO", type="FLOAT_COLOR", domain="POINT")

    golden_angle = math.pi * (3.0 - math.sqrt(5.0))
    n_rays = 24
    max_dist = 0.045

    for v in verts:
        n = v.normal.normalized()
        up = V((1, 0, 0)) if abs(n.x) < 0.85 else V((0, 1, 0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()
        occ = 0.0
        origin = v.co + n * 0.0005
        for k in range(n_rays):
            z_h = (k + 0.5) / n_rays
            r_h = math.sqrt(max(0.0, 1.0 - z_h * z_h))
            phi = k * golden_angle
            ray_dir = (n * (0.35 + 0.65 * z_h) + t1 * (r_h * math.cos(phi)) + t2 * (r_h * math.sin(phi))).normalized()
            hit_dist = tree.ray_cast(origin, ray_dir, max_dist)[3]
            if hit_dist is not None:
                occ += (1.0 - hit_dist / max_dist) / n_rays

        h_rl = v.co.z
        z_rl = -v.co.y
        lip_zone = math.exp(-(((v.co.x / 0.026) ** 2) + (((h_rl - 1.481) / 0.009) ** 2) + (((z_rl - 0.156) / 0.015) ** 2)))
        beard_shadow = math.exp(-(((h_rl - 1.475) / 0.042) ** 2)) * (1.0 if 1.41 < h_rl < 1.525 and z_rl > 0.08 else 0.0)

        ao_factor = max(0.30, 1.0 - 0.74 * occ)
        r = base_rgb[0] * ao_factor * (1.0 + 0.12 * lip_zone - 0.16 * beard_shadow)
        g = base_rgb[1] * ao_factor * (1.0 - 0.12 * lip_zone - 0.14 * beard_shadow)
        b = base_rgb[2] * ao_factor * (1.0 - 0.10 * lip_zone - 0.10 * beard_shadow)
        col_attr.data[v.index].color = (r, g, b, 1.0)


def build_skull_fitted_executive_hair(ga, m_hair):
    """Extracts the actual MPFB cranial/scalp topology above the natural high forehead hairline
    (y > 1.615 at front z=0.14, descending to y = 1.515 at nape) with subtle matte groomed volume."""
    raw_faces = ga["BODY"]
    scalp_faces = []
    for face in raw_faces:
        fc = sum((ga["REST"][i] for i in face), V()) / len(face)
        # Natural high executive hairline: y >= 1.614 at center forehead (z ~ 0.145), temples at 1.595, nape at 1.510
        front_t = min(1.0, max(-1.0, (fc.z - 0.045) / 0.095))
        temple_rec = 0.008 * math.exp(-(((abs(fc.x) - 0.048) / 0.020) ** 2)) * max(0.0, front_t)
        hairline_y = 1.568 + 0.046 * front_t + temple_rec
        ear_cutout = (abs(fc.x) > 0.068) and (fc.y < 1.562) and (-0.020 < fc.z < 0.072)
        if fc.y > hairline_y and not ear_cutout:
            scalp_faces.append(face)

    ids = sorted({i for face in scalp_faces for i in face})
    lookup = {old: i for i, old in enumerate(ids)}

    base_pts = [ga["REST"][i].copy() for i in ids]
    m = bpy.data.meshes.new("Hero_Scalp_Hair")
    m.from_pydata([xyz(p) for p in base_pts], [], [tuple(lookup[i] for i in f) for f in scalp_faces])
    m.update()

    bm = bmesh.new()
    bm.from_mesh(m)
    bm.verts.ensure_lookup_table()
    bm.normal_update()
    boundary_coords = [v.co.copy() for v in bm.verts if v.is_boundary]

    for v in bm.verts:
        d_bound = min((v.co - bc).length for bc in boundary_coords) if boundary_coords else 0.02
        taper = min(1.0, (d_bound / 0.016) ** 0.80)
        h_rl = v.co.z
        z_rl = -v.co.y
        crown_lift = 0.0055 * max(0.0, (h_rl - 1.58) / 0.10) * (0.6 + 0.4 * max(0.0, z_rl / 0.12))
        ang = math.atan2(v.co.x, max(0.001, h_rl - 1.54))
        ridge = 0.0014 * math.cos(ang * 48.0) * taper

        offset = 0.0008 + (0.0042 + crown_lift) * taper + ridge
        v.co += v.normal * offset
        v.co.z += crown_lift * 0.45 * taper

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(m)
    bm.free()
    m.update()

    obj = bpy.data.objects.new("Hero_Scalp_Hair", m)
    bpy.context.collection.objects.link(obj)
    sub = obj.modifiers.new("Subsurf", "SUBSURF")
    sub.levels = 2
    sub.render_levels = 2
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=sub.name)
    set_only_mat(obj, m_hair)
    return obj


def build_smooth_blazer_torso(m_wool):
    """Builds a 24-ring x 64-segment smooth Hermite-interpolated burgundy wool blazer torso
    with an indented front V-neck chest channel (so it never pokes through the black silk shirt)
    and a sculpted collar roll around the neck."""
    n_seg = 64
    n_rings = 24
    y_min, y_max = 0.850, 1.404
    verts = []
    faces = []

    for ir in range(n_rings):
        tr = ir / (n_rings - 1)
        y = y_min + (y_max - y_min) * tr
        rx, rz, cz = blazer_section(y)
        for j in range(n_seg):
            a = j * math.tau / n_seg
            sa, ca = math.sin(a), math.cos(a)
            x = rx * sa
            z = cz + rz * ca
            # Indent the front V-neck chest opening on the blazer mesh so the black shirt sits cleanly outside
            if ca > 0.25 and y > 1.065:
                ty = (y - 1.065) / (1.404 - 1.065)
                v_half_w = 0.008 + 0.068 * ty
                if abs(x) < v_half_w:
                    v_depth = 0.016 * (1.0 - (abs(x) / v_half_w) ** 2)
                    z -= v_depth
            verts.append(xyz((x, y, z)))

    for ir in range(n_rings - 1):
        for j in range(n_seg):
            jn = (j + 1) % n_seg
            a = ir * n_seg + j
            b = ir * n_seg + jn
            c = (ir + 1) * n_seg + jn
            d = (ir + 1) * n_seg + j
            faces.append((a, b, c, d))

    m = bpy.data.meshes.new("Hero_Blazer_Torso")
    m.from_pydata(verts, [], faces)
    bm = bmesh.new()
    bm.from_mesh(m)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(m)
    bm.free()
    m.update()

    obj = bpy.data.objects.new("Hero_Blazer_Torso", m)
    bpy.context.collection.objects.link(obj)
    set_only_mat(obj, m_wool)
    apply_box_uvs(obj, scale_uv=3.2)
    return obj


def build_torso_conforming_lapels_and_chain(m_shirt, m_lapel, m_gold, m_coal):
    """Builds 3D chest-conforming black silk crew-neck shirt, rolled notched wool lapels,
    and a full 360-degree 18k gold curb-link chain resting directly on the chest & neck."""
    # 1. Curved 3D Black Silk Crew-Neck Shirt Panel with natural curved collar scoop (y_top dips to 1.374 at center)
    nx, ny = 22, 24
    s_verts, s_faces = [], []
    for iy in range(ny + 1):
        ty = iy / ny
        for ix in range(nx + 1):
            tx = (ix / nx) * 2.0 - 1.0
            y_top = 1.374 + 0.026 * (tx ** 2)
            y = 1.065 + (y_top - 1.065) * ty
            half_w = 0.014 + 0.072 * (ty ** 0.75)
            x = tx * half_w
            z = torso_front_z(x, y, offset=0.0025)
            s_verts.append(xyz((x, y, z)))
    for iy in range(ny):
        for ix in range(nx):
            a = iy * (nx + 1) + ix
            b = a + 1
            c = a + (nx + 1) + 1
            d = a + (nx + 1)
            s_faces.append((a, b, c, d))
    sm = bpy.data.meshes.new("Hero_Conforming_Shirt")
    sm.from_pydata(s_verts, [], s_faces)
    sobj = bpy.data.objects.new("Hero_Conforming_Shirt", sm)
    bpy.context.collection.objects.link(sobj)
    solid_s = sobj.modifiers.new("Solidify", "SOLIDIFY")
    solid_s.thickness = 0.005
    bpy.context.view_layer.objects.active = sobj
    bpy.ops.object.modifier_apply(modifier=solid_s.name)
    bm_s = bmesh.new()
    bm_s.from_mesh(sobj.data)
    bmesh.ops.recalc_face_normals(bm_s, faces=list(bm_s.faces))
    bm_s.to_mesh(sobj.data)
    bm_s.free()
    set_only_mat(sobj, m_shirt)

    # 2. Contoured 3D Notched Burgundy Wool Lapels
    for side in (-1, 1):
        l_verts, l_faces = [], []
        n_rows = 26
        n_cols = 9
        for ir in range(n_rows + 1):
            tr = ir / n_rows
            y = 1.060 + (1.402 - 1.060) * tr
            x_in = 0.003 + 0.066 * (tr ** 0.92)
            if tr < 0.74:
                flap_w = 0.012 + 0.082 * math.sin((tr / 0.74) * math.pi * 0.62)
            elif tr < 0.78:
                flap_w = 0.048
            else:
                flap_w = 0.062 * (1.0 - 0.30 * ((tr - 0.78) / 0.22))
            x_out = x_in + flap_w

            for ic in range(n_cols + 1):
                tc = ic / n_cols
                x = side * (x_in + (x_out - x_in) * tc)
                roll = 0.0035 * math.sin(tc * math.pi)
                z = torso_front_z(x, y, offset=0.0055 + roll)
                l_verts.append(xyz((x, y, z)))

        for ir in range(n_rows):
            for ic in range(n_cols):
                a = ir * (n_cols + 1) + ic
                b = a + 1
                c = a + (n_cols + 1) + 1
                d = a + (n_cols + 1)
                l_faces.append((a, b, c, d) if side > 0 else (a, d, c, b))

        lm = bpy.data.meshes.new(f"Hero_Sculpted_Lapel_{side}")
        lm.from_pydata(l_verts, [], l_faces)
        lobj = bpy.data.objects.new(f"Hero_Sculpted_Lapel_{side}", lm)
        bpy.context.collection.objects.link(lobj)
        solid_l = lobj.modifiers.new("Solidify", "SOLIDIFY")
        solid_l.thickness = 0.005
        bev_l = lobj.modifiers.new("Bevel", "BEVEL")
        bev_l.width = 0.0014
        bev_l.segments = 2
        bpy.context.view_layer.objects.active = lobj
        bpy.ops.object.modifier_apply(modifier=solid_l.name)
        bpy.ops.object.modifier_apply(modifier=bev_l.name)
        bm = bmesh.new()
        bm.from_mesh(lobj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(lobj.data)
        bm.free()
        set_only_mat(lobj, m_lapel)
        apply_box_uvs(lobj, scale_uv=3.2)

    # 3. Full 360-degree Interlocking 18k Gold Curb Chain hugging neck & upper chest
    n_links = 46
    for k in range(n_links):
        a = k * math.tau / n_links
        sa, ca = math.sin(a), math.cos(a)
        cx = 0.058 * sa
        if ca >= 0:
            cy = 1.394 - 0.062 * (ca ** 1.35)
            cz = torso_front_z(cx, cy, offset=0.0072)
        else:
            cy = 1.394 + 0.006 * (-ca)
            cz = 0.038 + 0.056 * ca

        a_next = (k + 1) * math.tau / n_links
        sa2, ca2 = math.sin(a_next), math.cos(a_next)
        cx2 = 0.058 * sa2
        cy2 = (1.394 - 0.062 * (ca2 ** 1.35)) if ca2 >= 0 else (1.394 + 0.006 * (-ca2))
        cz2 = torso_front_z(cx2, cy2, offset=0.0072) if ca2 >= 0 else (0.038 + 0.056 * ca2)

        p0 = V(xyz((cx, cy, cz)))
        p1 = V(xyz((cx2, cy2, cz2)))
        tangent = (p1 - p0).normalized()
        bpy.ops.mesh.primitive_torus_add(
            major_radius=0.0054,
            minor_radius=0.0021,
            major_segments=18,
            minor_segments=10,
            location=(p0 + p1) * 0.5,
        )
        link_obj = bpy.context.active_object
        link_obj.name = f"GoldCurbLink_{k}"
        quat = V((0, 0, 1)).rotation_difference(tangent)
        twist = Matrix.Rotation(math.radians(28.0 if k % 2 == 0 else -28.0), 4, tangent)
        link_obj.matrix_world = Matrix.Translation((p0 + p1) * 0.5) @ twist @ quat.to_matrix().to_4x4()
        link_obj.scale = (1.15, 0.72, 1.0)
        set_only_mat(link_obj, m_gold)


def build_rectangular_90s_sunglasses(m_frame, m_lens, m_gold):
    """Builds sleek 90s rectangular navigator sunglasses sitting cleanly on the nose bridge
    with dark smoked anti-glare lenses and gold brow/bridge hardware."""
    for s in (-1, 1):
        cx = s * 0.0335
        cy = 1.549
        cz = 0.153

        w_half, h_half = 0.0255, 0.0138
        n_pts = 28
        ring_pts = []
        for i in range(n_pts):
            ang = i * math.tau / n_pts
            ca, sa = math.cos(ang), math.sin(ang)
            rx = math.copysign(abs(ca) ** 0.55, ca) * w_half
            ry = math.copysign(abs(sa) ** 0.55, sa) * h_half
            px = cx + s * rx
            py = cy + ry - 0.0012 * rx
            pz = cz - 0.0055 * ((px / 0.062) ** 2)
            ring_pts.append((px, py, pz))

        l_verts = [xyz(p) for p in ring_pts] + [xyz((p[0], p[1], p[2] - 0.0025)) for p in ring_pts]
        l_faces = [tuple(range(n_pts)), tuple(reversed(range(n_pts, 2 * n_pts)))]
        for i in range(n_pts):
            inext = (i + 1) % n_pts
            l_faces.append((i, inext, n_pts + inext, n_pts + i))
        lm = bpy.data.meshes.new(f"Hero_RectLens_{s}")
        lm.from_pydata(l_verts, [], l_faces)
        lobj = bpy.data.objects.new(f"Hero_RectLens_{s}", lm)
        bpy.context.collection.objects.link(lobj)
        bm = bmesh.new()
        bm.from_mesh(lobj.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(lobj.data)
        bm.free()
        set_only_mat(lobj, m_lens)

        curve = bpy.data.curves.new(f"Hero_RectRim_{s}", "CURVE")
        curve.dimensions = "3D"
        curve.bevel_depth = 0.0023
        curve.bevel_resolution = 4
        sp = curve.splines.new("POLY")
        sp.points.add(len(ring_pts) - 1)
        for pt_c, p in zip(sp.points, ring_pts):
            pt_c.co = (*xyz(p), 1.0)
        sp.use_cyclic_u = True
        rim_obj = bpy.data.objects.new(f"Hero_RectRim_{s}", curve)
        bpy.context.collection.objects.link(rim_obj)
        bpy.context.view_layer.objects.active = rim_obj
        rim_obj.select_set(True)
        bpy.ops.object.convert(target="MESH")
        rim_obj.select_set(False)
        set_only_mat(rim_obj, m_frame)

        t_pts = [
            (s * 0.060, 1.552, 0.147),
            (s * 0.077, 1.551, 0.118),
            (s * 0.081, 1.538, 0.062),
        ]
        t_curve = bpy.data.curves.new(f"Hero_Temple_{s}", "CURVE")
        t_curve.dimensions = "3D"
        t_curve.bevel_depth = 0.0026
        t_curve.bevel_resolution = 3
        tsp = t_curve.splines.new("POLY")
        tsp.points.add(len(t_pts) - 1)
        for pt_c, p in zip(tsp.points, t_pts):
            pt_c.co = (*xyz(p), 1.0)
        t_obj = bpy.data.objects.new(f"Hero_Temple_{s}", t_curve)
        bpy.context.collection.objects.link(t_obj)
        bpy.context.view_layer.objects.active = t_obj
        t_obj.select_set(True)
        bpy.ops.object.convert(target="MESH")
        t_obj.select_set(False)
        set_only_mat(t_obj, m_frame)

        bpy.ops.mesh.primitive_uv_sphere_add(segments=14, ring_count=10, radius=0.0030, location=xyz((s * 0.059, 1.554, 0.149)))
        hinge = bpy.context.active_object
        set_only_mat(hinge, m_gold)

    for by, bz, br in [(1.552, 0.157, 0.0016), (1.562, 0.155, 0.0014)]:
        b_pts = [(-0.011, by, bz - 0.002), (0.0, by + 0.001, bz), (0.011, by, bz - 0.002)]
        bc = bpy.data.curves.new("Hero_GoldBridge", "CURVE")
        bc.dimensions = "3D"
        bc.bevel_depth = br
        bc.bevel_resolution = 3
        bsp = bc.splines.new("POLY")
        bsp.points.add(len(b_pts) - 1)
        for pt_c, p in zip(bsp.points, b_pts):
            pt_c.co = (*xyz(p), 1.0)
        b_obj = bpy.data.objects.new("Hero_GoldBridge", bc)
        bpy.context.collection.objects.link(b_obj)
        bpy.context.view_layer.objects.active = b_obj
        b_obj.select_set(True)
        bpy.ops.object.convert(target="MESH")
        b_obj.select_set(False)
        set_only_mat(b_obj, m_gold)


def build_highres_partner_hero():
    """Builds the 90s Partner hero using full-resolution undecimated MPFB anatomy + tailored 90s blazer,
    torso-conforming notched lapels, black shirt, 360-degree 18k gold curb chain, skull-fitted hair, and 90s sunglasses."""
    ga = runpy.run_path(str(ROOT / "generate_anatomy.py"))

    wool_img = make_wool_texture("Tex_BurgundyWool", 512, base_rgb=(0.185, 0.007, 0.026))
    lapel_img = make_wool_texture("Tex_BurgundyLapel", 512, base_rgb=(0.140, 0.005, 0.019))
    m_skin = make_pbr_mat("Hero_Skin", (0.28, 0.155, 0.108, 1.0), metallic=0.0, roughness=0.46, specular=0.35, subsurf=0.15, use_vertex_ao=True)
    m_hair = make_pbr_mat("Hero_Hair", (0.012, 0.009, 0.007, 1.0), metallic=0.0, roughness=0.82, specular=0.14)
    m_wool = make_pbr_mat("Hero_BurgundyWool", (0.185, 0.007, 0.026, 1.0), metallic=0.0, roughness=0.80, specular=0.22, tex_img=wool_img)
    m_lapel = make_pbr_mat("Hero_BurgundyLapel", (0.140, 0.005, 0.019, 1.0), metallic=0.0, roughness=0.68, specular=0.28, tex_img=lapel_img)
    m_shirt = make_pbr_mat("Hero_BlackShirt", (0.011, 0.012, 0.015, 1.0), metallic=0.0, roughness=0.74, specular=0.20)
    m_coal = make_pbr_mat("Hero_CoalFrame", (0.014, 0.015, 0.018, 1.0), metallic=0.22, roughness=0.24, specular=0.45)
    m_lens = make_pbr_mat("Hero_SunglassLens", (0.004, 0.005, 0.007, 1.0), metallic=0.05, roughness=0.14, specular=0.25)
    m_gold = make_pbr_mat("Hero_Gold18k", (0.88, 0.64, 0.22, 1.0), metallic=0.94, roughness=0.20, specular=0.50)
    m_ivory = make_pbr_mat("Hero_IvoryCuff", (0.76, 0.73, 0.66, 1.0), metallic=0.0, roughness=0.58, specular=0.25)

    transforms = executive_arm_transforms(ga)
    digits_set = set(ga["DIGITS"])

    # Full-resolution undecimated MPFB body mesh (culling hidden inner torso polygons covered by blazer)
    raw_faces = ga["BODY"]
    faces = []
    for face in raw_faces:
        fc = sum((ga["REST"][i] for i in face), V()) / len(face)
        is_arm = any(
            (max(ga["WEIGHTS"][i], key=lambda a: a[1])[0] if ga["WEIGHTS"][i] else "").startswith(("upperarm", "lowerarm", "hand", "finger"))
            for i in face
        )
        if not is_arm and 0.88 < fc.y < 1.368 and abs(fc.x) < 0.208:
            continue
        faces.append(face)

    ids = sorted({i for face in faces for i in face})
    lookup = {old: i for i, old in enumerate(ids)}
    pts = []
    for i in ids:
        p = ga["REST"][i].copy()
        b = max(ga["WEIGHTS"][i], key=lambda a: a[1])[0] if ga["WEIGHTS"][i] else ""
        arm_vert = abs(p.x) > 0.205 and p.y > 1.22

        # Smooth shoulder-to-sleeve fullness using arm_blend (zero step at shoulder seam!)
        if b.startswith(("upperarm", "lowerarm")) or arm_vert:
            s = "l" if p.x > 0 else "r"
            wrist = ga["JOINT"][s + "-hand"]
            elbow = ga["JOINT"][s + "-elbow"]
            shoulder = ga["JOINT"][s + "-shoulder"]
            fore_axis = (wrist - elbow).normalized()
            along_wrist = (p - wrist).dot(fore_axis)
            if along_wrist < -0.003:
                a, c = (elbow, wrist) if (b.startswith("lower") or abs(p.x) > 0.34) else (shoulder, elbow)
                axis = (c - a).normalized()
                centre = a + axis * (p - a).dot(axis)
                radial = p - centre
                dist_wrist = max(0.0, -along_wrist)
                cuff_t = min(1.0, max(0.0, (dist_wrist - 0.006) / 0.034))
                arm_blend = min(1.0, max(0.0, (abs(p.x) - 0.175) / 0.048))
                fullness = (0.22 + 0.24 * cuff_t) * arm_blend
                p += radial * fullness
                if abs(p.x) < 0.205 and p.z < 0.0:
                    p.z *= 0.68

        out = V()
        total = 0.0
        for bone, w in ga["WEIGHTS"][i]:
            # Map all finger phalanx weights rigidly to the parent hand transform so zero fingers ever stretch!
            if bone.split("_")[0] in digits_set:
                bone = "hand_" + bone[-1]
            if bone in transforms:
                org, dest, rot = transforms[bone]
                out += (dest + rot @ (p - org)) * w
            else:
                out += p * w
            total += w
        pts.append(out / total if total else p)

    body_obj = ga["mesh"]("Hero_MPFB_Body", pts, [tuple(lookup[i] for i in f) for f in faces], "skin")
    body_obj.data.materials.clear()
    slot_mats = [m_skin, m_wool, m_ivory, m_shirt]
    for sm in slot_mats:
        body_obj.data.materials.append(sm)

    for poly, face in zip(body_obj.data.polygons, faces):
        p = sum((ga["REST"][i] for i in face), V()) / len(face)
        arm = abs(p.x) > 0.205 and p.y > 0.90
        sidekey = "l" if p.x > 0 else "r"
        wrist = ga["JOINT"][sidekey + "-hand"]
        fore = (wrist - ga["JOINT"][sidekey + "-elbow"]).normalized()
        along = (p - wrist).dot(fore)
        if arm:
            poly.material_index = 0 if along > -0.004 else (2 if along > -0.031 else 1)
        elif p.y < 0.927:
            poly.material_index = 3
        elif p.y < 1.388 or (p.y < 1.418 and abs(p.x) > 0.072):
            poly.material_index = 1
        else:
            poly.material_index = 0

    sub = body_obj.modifiers.new("Subsurf", "SUBSURF")
    sub.levels = 1
    sub.render_levels = 1
    bpy.context.view_layer.objects.active = body_obj
    bpy.ops.object.modifier_apply(modifier=sub.name)
    for poly in body_obj.data.polygons:
        poly.use_smooth = True
    apply_box_uvs(body_obj, scale_uv=3.0)
    bake_skin_ao_and_contour(body_obj)

    # Smooth 24-ring x 64-segment Burgundy Wool Blazer Torso
    build_smooth_blazer_torso(m_wool)

    # Torso-conforming black silk shirt, sculpted notched lapels, collar roll, and 360-degree 18k gold curb chain
    build_torso_conforming_lapels_and_chain(m_shirt, m_lapel, m_gold, m_coal)

    # Anatomically skull-fitted 90s executive slick-back hair + eyebrows
    build_skull_fitted_executive_hair(ga, m_hair)
    for s in (-1, 1):
        b_pts = [(s * 0.013, 1.569, 0.151), (s * 0.034, 1.574, 0.149), (s * 0.054, 1.567, 0.139)]
        bc = bpy.data.curves.new(f"Hero_Eyebrow_{s}", "CURVE")
        bc.dimensions = "3D"
        bc.bevel_depth = 0.0030
        bc.bevel_resolution = 3
        bsp = bc.splines.new("POLY")
        bsp.points.add(len(b_pts) - 1)
        for pt_c, p in zip(bsp.points, b_pts):
            pt_c.co = (*xyz(p), 1.0)
        b_obj = bpy.data.objects.new(f"Hero_Eyebrow_{s}", bc)
        bpy.context.collection.objects.link(b_obj)
        bpy.context.view_layer.objects.active = b_obj
        b_obj.select_set(True)
        bpy.ops.object.convert(target="MESH")
        b_obj.select_set(False)
        set_only_mat(b_obj, m_hair)

    # Rectangular 90s navigator sunglasses
    build_rectangular_90s_sunglasses(m_coal, m_lens, m_gold)

    hero_root = bpy.data.objects.new("HeroPartnerRoot", None)
    bpy.context.collection.objects.link(hero_root)
    for o in list(bpy.context.scene.objects):
        if o != hero_root and o.type == "MESH":
            for p in o.data.polygons:
                p.use_smooth = True
            o.parent = hero_root

    return hero_root


def build_architecture_and_background():
    """Imports real 3D office assets (prop_leather_sofa.glb, prop_server_rack.glb) and builds
    the dark steel skyscraper window mullions + golden-hour Moscow-City skyline outside."""
    m_dark_steel = make_pbr_mat("Arch_DarkSteel", (0.022, 0.026, 0.034, 1.0), metallic=0.84, roughness=0.34)
    m_warm_brass = make_pbr_mat("Arch_WarmBrass", (0.52, 0.36, 0.15, 1.0), metallic=0.88, roughness=0.28)
    m_dark_floor = make_pbr_mat("Arch_DarkMarbleFloor", (0.012, 0.015, 0.020, 1.0), metallic=0.15, roughness=0.22)
    m_tower_sil = make_pbr_mat("Skyline_TowerSilhouette", (0.018, 0.020, 0.026, 1.0), metallic=0.45, roughness=0.38)
    m_window_warm = make_pbr_mat(
        "Skyline_WarmWindow", (0.95, 0.66, 0.30, 1.0), metallic=0.0, roughness=0.2, emission_rgb=(0.95, 0.62, 0.24), emission_strength=1.1
    )

    sofa_path = ASSETS_MODELS / "prop_leather_sofa.glb"
    if sofa_path.exists():
        bpy.ops.import_scene.gltf(filepath=str(sofa_path))
        for o in bpy.context.selected_objects:
            if o.type == "MESH":
                o.location = (-0.85, 1.65, 0.0)
                o.rotation_euler = (0.0, 0.0, math.radians(-24.0))

    rack_path = ASSETS_MODELS / "prop_server_rack.glb"
    if rack_path.exists():
        bpy.ops.import_scene.gltf(filepath=str(rack_path))
        for o in bpy.context.selected_objects:
            if o.type == "MESH":
                o.location = (1.02, 2.35, 0.0)
                o.rotation_euler = (0.0, 0.0, math.radians(152.0))
                for slot in o.material_slots:
                    if slot.material and ("Cyan" in slot.material.name or "Emerald" in slot.material.name):
                        slot.material = m_warm_brass

    win_y = 2.85
    for mx in (-1.15, -0.38, 0.42, 1.20):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(mx, win_y, 1.40))
        mull = bpy.context.active_object
        mull.scale = (0.072, 0.14, 3.20)
        mull.data.materials.append(m_dark_steel)
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(mx, win_y - 0.072, 1.40))
        trim = bpy.context.active_object
        trim.scale = (0.016, 0.012, 3.20)
        trim.data.materials.append(m_warm_brass)

    for tz in (0.82, 1.98):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, win_y, tz))
        tran = bpy.context.active_object
        tran.scale = (3.60, 0.12, 0.060)
        tran.data.materials.append(m_dark_steel)

    towers = [
        (-1.85, 8.2, 1.10, 0.68, 0.70, 2.70),
        (-0.88, 7.6, 1.38, 0.62, 0.65, 3.20),
        (-0.08, 9.0, 1.18, 0.74, 0.70, 2.85),
        ( 0.82, 8.0, 1.28, 0.65, 0.65, 3.00),
        ( 1.75, 9.4, 1.05, 0.78, 0.75, 2.50),
    ]
    rng = np.random.default_rng(1998)
    for idx, (tx, ty, tz, tw, td, th) in enumerate(towers):
        bpy.ops.mesh.primitive_cube_add(size=1.0, location=(tx, ty, tz))
        t_obj = bpy.context.active_object
        t_obj.name = f"MoscowCity_Tower_{idx}"
        t_obj.scale = (tw, td, th)
        t_obj.data.materials.append(m_tower_sil)
        for row in range(7):
            wz = 0.72 + row * 0.28
            if rng.random() < 0.58:
                wx = tx + rng.uniform(-tw * 0.28, tw * 0.28)
                bpy.ops.mesh.primitive_cube_add(size=1.0, location=(wx, ty - td * 0.51, wz))
                w_obj = bpy.context.active_object
                w_obj.scale = (0.13, 0.02, 0.028)
                w_obj.data.materials.append(m_window_warm)

    sky_img = make_skyline_backdrop_texture()
    m_sky = make_pbr_mat(
        "GoldenHour_SkyPlane",
        (1.0, 1.0, 1.0, 1.0),
        metallic=0.0,
        roughness=1.0,
        tex_img=sky_img,
    )
    nodes = m_sky.node_tree.nodes
    links = m_sky.node_tree.links
    tex_node = next((n for n in nodes if n.type == "TEX_IMAGE"), None)
    bsdf_node = nodes.get("Principled BSDF")
    if tex_node and bsdf_node and "Emission Color" in bsdf_node.inputs:
        links.new(tex_node.outputs["Color"], bsdf_node.inputs["Emission Color"])
        bsdf_node.inputs["Emission Strength"].default_value = 1.35

    bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.0, 14.5, 1.55), rotation=(math.radians(90.0), 0.0, 0.0))
    sky_plane = bpy.context.active_object
    sky_plane.scale = (14.0, 9.0, 1.0)
    sky_plane.data.materials.append(m_sky)

    bpy.ops.mesh.primitive_plane_add(size=12.0, location=(0.0, 1.5, 0.0))
    floor = bpy.context.active_object
    floor.data.materials.append(m_dark_floor)


def apply_bottom_dark_vignette(png_path):
    """Smoothly grades the bottom 33% of the 536x900 render into calm dark graphite (#0F1216)
    and adds subtle top/corner optical vignette so white installer typography reads effortlessly."""
    img = bpy.data.images.load(str(png_path))
    w, h = img.size
    pixels = np.array(img.pixels[:], dtype=np.float32).reshape((h, w, 4))
    y_idx, x_idx = np.mgrid[0:h, 0:w].astype(np.float32)
    v = y_idx / h
    u = x_idx / w

    target_bottom = np.array([15.0 / 255.0, 18.0 / 255.0, 22.0 / 255.0], dtype=np.float32)

    t_bot = np.clip((0.35 - v) / 0.35, 0.0, 1.0)
    t_bot_smooth = (t_bot ** 1.50) * 0.97

    r_edge = np.sqrt(((u - 0.54) * 1.15) ** 2 + ((v - 0.56) * 0.95) ** 2)
    t_vig = np.clip((r_edge - 0.46) / 0.54, 0.0, 1.0) ** 1.8 * 0.36

    for c in range(3):
        pixels[..., c] = pixels[..., c] * (1.0 - t_vig) + target_bottom[c] * t_vig
        pixels[..., c] = pixels[..., c] * (1.0 - t_bot_smooth) + target_bottom[c] * t_bot_smooth

    pixels[..., 3] = 1.0
    img.pixels.foreach_set(np.clip(pixels, 0.0, 1.0).ravel())
    img.save_render(str(png_path))


def main():
    clear_scene()
    hero_root = build_highres_partner_hero()
    build_architecture_and_background()

    # Position the Hero slightly off-center right (x = +0.068m), turned 19 deg in an authoritative 3/4 portrait
    hero_root.location = (0.068, 0.0, 0.0)
    hero_root.rotation_euler = (0.0, 0.0, math.radians(-19.0))

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = True
    scene.cycles.use_adaptive_sampling = True
    scene.render.resolution_x = 536
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"

    try:
        scene.view_settings.view_transform = "AgX"
        scene.view_settings.look = "AgX - Medium High Contrast"
    except Exception:
        scene.view_settings.view_transform = "Filmic"
    scene.view_settings.exposure = -0.05

    world = bpy.data.worlds.new("InstallerWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.015, 0.019, 0.025, 1.0)
        bg.inputs[1].default_value = 0.55

    cam_data = bpy.data.cameras.new("InstallerCam")
    cam_data.lens = 62.0
    cam_data.dof.use_dof = True
    cam_data.dof.focus_distance = 1.78
    cam_data.dof.aperture_fstop = 2.2
    cam_obj = bpy.data.objects.new("InstallerCam", cam_data)
    scene.collection.objects.link(cam_obj)
    cam_obj.location = (0.012, -1.78, 1.32)
    cam_obj.rotation_euler = (math.radians(90.2), 0.0, 0.0)
    scene.camera = cam_obj

    def add_area_light(name, loc, rot_deg, energy, color, size_x, size_y):
        ldata = bpy.data.lights.new(name=name, type="AREA")
        ldata.energy = energy
        ldata.color = color
        ldata.shape = "RECTANGLE"
        ldata.size = size_x
        ldata.size_y = size_y
        lobj = bpy.data.objects.new(name=name, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = loc
        lobj.rotation_euler = tuple(math.radians(a) for a in rot_deg)
        return lobj

    # 1. Neutral-Warm Studio Rembrandt Key Light (preserves true rich burgundy/oxblood wool color & sculpts face)
    add_area_light("StudioRembrandtKey", (-1.35, -0.95, 1.92), (55.0, 0.0, -52.0), 86.0, (1.0, 0.91, 0.82), 0.75, 0.85)
    # 2. Warm Amber Sunset Rim Light from behind-left window (sculpting shoulder wool, jawline & hair rim)
    add_area_light("GoldenWindowRim", (-1.05, 1.15, 1.76), (110.0, 0.0, -135.0), 170.0, (1.0, 0.66, 0.30), 1.25, 1.45)
    # 3. Cool Slate-Graphite Studio Fill from camera-right (#14181F tone, lifting dark burgundy wool folds without neon)
    add_area_light("CoolSlateFill", (1.35, -1.25, 1.42), (78.0, 0.0, 46.0), 30.0, (0.64, 0.75, 0.88), 1.10, 1.10)
    # 4. Warm Chest & Burgundy Lapel / Gold Chain Accent Fill
    add_area_light("LapelGoldAccent", (-0.50, -1.45, 1.22), (96.0, 0.0, -20.0), 28.0, (1.0, 0.90, 0.78), 0.55, 0.45)

    OUT_PNG.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(OUT_PNG)
    bpy.ops.render.render(write_still=True)

    apply_bottom_dark_vignette(OUT_PNG)

    SCRATCH_PNG.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(OUT_PNG, SCRATCH_PNG)
    print(f"[INSTALLER COVER RENDERED] {OUT_PNG} (size={OUT_PNG.stat().st_size} bytes)")


if __name__ == "__main__":
    main()
