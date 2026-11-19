"""High-craft smooth-shaded Vibe Coder character builder for char_vibe_coder.glb and char_vibe_coder_seated.glb.
Preserves exact bounding boxes, origin, facing (+Z), node extras, and exact glTF material slot indices:
  0 (Raylib 1): skin
  1 (Raylib 2): hair
  2 (Raylib 3): blue      (dynamically tinted by c.HoodieColor)
  3 (Raylib 4): denim
  4 (Raylib 5): navy      (dynamically tinted by ColorBrightness(c.HoodieColor, -0.55f))
  5 (Raylib 6): sole
  6 (Raylib 7): ivory
  7 (Raylib 8): iris
  8 (Raylib 9): teal      (dynamically tinted by c.GlassesColor)
  9 (Raylib 10): coal
 10 (Raylib 11): laptop   (hidden when c.LaptopLost)
 11 (Raylib 12): glass    (hidden when c.LaptopLost)
 12 (Raylib 13): display  (hidden when c.LaptopLost)
 13 (Raylib 14): cyan     (hidden when c.LaptopLost)
"""
import json
import math
import struct
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

V = Vector

CODER_MAT_ORDER = [
    "skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal",
    "laptop", "glass", "display", "cyan"
]
SEATED_MAT_ORDER = CODER_MAT_ORDER[:10]

PBR_SPECS = {
    "skin":    (0.00, 0.54),
    "hair":    (0.00, 0.72),
    "blue":    (0.00, 0.80),
    "denim":   (0.00, 0.84),
    "navy":    (0.00, 0.78),
    "sole":    (0.00, 0.70),
    "ivory":   (0.00, 0.62),
    "iris":    (0.00, 0.22),
    "teal":    (0.35, 0.14),
    "coal":    (0.15, 0.45),
    "laptop":  (0.88, 0.22),
    "glass":   (0.20, 0.18),
    "display": (0.05, 0.16),
    "cyan":    (0.00, 0.15),
}


def build_detail_atlas():
    """High-frequency, moire-free micro-detail texture atlas (0.93..1.00) so dynamic material tinting stays pure."""
    img = bpy.data.images.get("VibeCoderDetailAtlas")
    if img is not None:
        return img
    W = 512
    H = 512
    arr = np.ones((H, W, 4), dtype=np.float32)
    y_idx, x_idx = np.mgrid[0:H, 0:W].astype(np.float32)

    # deterministic multi-seed stochastic micro-grain (100% free of periodic Nyquist moire patches at any angle!)
    n1 = np.mod(np.sin(x_idx * 12.9898 + y_idx * 78.233) * 43758.5453, 1.0) - 0.5
    n2 = np.mod(np.sin((x_idx // 2) * 39.346 + (y_idx // 2) * 11.135) * 24634.6345, 1.0) - 0.5
    n3 = np.mod(np.sin(((x_idx + y_idx) // 2) * 27.1828 + ((x_idx - y_idx) // 2) * 31.4159) * 31415.9265, 1.0) - 0.5

    # Quadrant 0 (x < 256, y < 256): Fine French Terry Cotton Knit (blue, navy, ivory)
    q0 = (x_idx < 256) & (y_idx < 256)
    val0 = 0.968 + 0.016 * n1[q0] + 0.014 * n2[q0]
    arr[q0, 0] = np.clip(val0, 0.93, 1.0)
    arr[q0, 1] = np.clip(val0, 0.93, 1.0)
    arr[q0, 2] = np.clip(val0, 0.93, 1.0)

    # Quadrant 1 (x >= 256, y < 256): Fine Diagonal Denim Twill (denim, sole, coal)
    q1 = (x_idx >= 256) & (y_idx < 256)
    val1 = 0.965 + 0.018 * n1[q1] + 0.016 * n3[q1]
    arr[q1, 0] = np.clip(val1, 0.92, 1.0)
    arr[q1, 1] = np.clip(val1, 0.92, 1.0)
    arr[q1, 2] = np.clip(val1, 0.92, 1.0)

    # Quadrant 2 (x < 256, y >= 256): Subtle Skin & Hair Micro-Grain (skin, hair, iris)
    q2 = (x_idx < 256) & (y_idx >= 256)
    val2 = 0.976 + 0.015 * n1[q2] + 0.010 * n2[q2]
    arr[q2, 0] = np.clip(val2, 0.94, 1.0)
    arr[q2, 1] = np.clip(val2 * 0.996, 0.94, 1.0)
    arr[q2, 2] = np.clip(val2 * 0.992, 0.94, 1.0)

    # Quadrant 3 (x >= 256, y >= 256): Fine Anodized Aluminum Micro-Grain (laptop, glass, display, cyan, teal)
    q3 = (x_idx >= 256) & (y_idx >= 256)
    val3 = 0.978 + 0.014 * n1[q3]
    arr[q3, 0] = np.clip(val3, 0.95, 1.0)
    arr[q3, 1] = np.clip(val3, 0.95, 1.0)
    arr[q3, 2] = np.clip(val3, 0.95, 1.0)

    img = bpy.data.images.new("VibeCoderDetailAtlas", width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


def srgb_hex_to_linear(hex_str):
    rgb = bytes.fromhex(hex_str)
    return [round(((x / 255.0 + 0.055) / 1.055) ** 2.4, 6) for x in rgb] + [1.0]


def make_coder_mat(ga, key, atlas_img):
    if key not in ga.MATS:
        lin = srgb_hex_to_linear(ga.PALETTE[key])
        m = bpy.data.materials.new(key)
        m.diffuse_color = tuple(lin)
        m.use_nodes = True
        tree = m.node_tree
        shader = tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = tuple(lin)
        met, rough = PBR_SPECS.get(key, (0.0, 0.75))
        shader.inputs["Metallic"].default_value = met
        shader.inputs["Roughness"].default_value = rough
        if key in ("cyan", "teal"):
            shader.inputs["Emission Color"].default_value = tuple(lin)
            shader.inputs["Emission Strength"].default_value = 1.6 if key == "cyan" else 0.35
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = atlas_img
        tree.links.new(tex.outputs["Color"], shader.inputs["Base Color"])
        ga.MATS[key] = m
    return ga.MATS[key]


def smooth_obj(obj):
    for poly in obj.data.polygons:
        poly.use_smooth = True
    return obj


def add_loft_grid(ga, name, rings_pts, key, closed_u=True, cap_ends=True):
    n_rings = len(rings_pts)
    n_seg = len(rings_pts[0])
    pts = [p for ring in rings_pts for p in ring]
    faces = []
    u_limit = n_seg if closed_u else (n_seg - 1)
    for k in range(n_rings - 1):
        for j in range(u_limit):
            jn = (j + 1) % n_seg
            a = k * n_seg + j
            b = k * n_seg + jn
            c = (k + 1) * n_seg + jn
            d = (k + 1) * n_seg + j
            faces.append((a, b, c, d))
    if cap_ends and closed_u:
        faces.append(tuple(reversed(range(n_seg))))
        faces.append(tuple(range(len(pts) - n_seg, len(pts))))
    obj = ga.mesh(name, pts, faces, key)
    return smooth_obj(obj)


def smooth_wire(ga, name, points, radius, key, closed=False, res=4):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.bevel_depth = radius
    curve.bevel_resolution = res
    spline = curve.splines.new("BEZIER" if len(points) >= 3 else "POLY")
    if spline.type == "BEZIER":
        spline.bezier_points.add(len(points) - 1)
        for bp, p in zip(spline.bezier_points, points):
            bp.co = ga.xyz(p)
            bp.handle_left_type = bp.handle_right_type = "AUTO"
    else:
        spline.points.add(len(points) - 1)
        for sp, p in zip(spline.points, points):
            sp.co = (*ga.xyz(p), 1.0)
    spline.use_cyclic_u = closed
    o = bpy.data.objects.new(name, curve)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(ga.mat(key))
    bpy.context.view_layer.objects.active = o
    o.select_set(True)
    bpy.ops.object.convert(target="MESH")
    o.select_set(False)
    return smooth_obj(o)


def smooth_oval(ga, name, p, radii, key, segs=28, rings=18):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=rings, location=ga.xyz(p))
    obj = bpy.context.object
    obj.scale = (radii[0], radii[2], radii[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ga.finish(obj, name, key)
    return smooth_obj(obj)


def smooth_box(ga, name, p, size, key, bevel=0.003, segments=3):
    bpy.ops.mesh.primitive_cube_add(size=1, location=ga.xyz(p))
    obj = bpy.context.object
    obj.dimensions = (size[0], size[2], size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        mod = obj.modifiers.new("SmoothBevel", "BEVEL")
        mod.width = bevel
        mod.segments = segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ga.finish(obj, name, key)
    return smooth_obj(obj)


def hoodie_cross_section(h, kind):
    """Return (rx, rz, cz) of the tailored hoodie torso shell at height h (in un-shifted standing space)."""
    if kind == "coder_seated":
        # Seated torso ends cleanly at the seated waist (h = 0.935) above the bent thighs
        table = [
            (0.935, 0.172, 0.134, 0.036),
            (0.968, 0.176, 0.138, 0.038),
            (1.040, 0.168, 0.132, 0.040),
            (1.185, 0.186, 0.128, 0.048),
            (1.305, 0.206, 0.115, 0.025),
            (1.368, 0.186, 0.088, 0.018),
            (1.402, 0.078, 0.074, 0.038),
        ]
    else:
        table = [
            (0.872, 0.176, 0.136, 0.030),
            (0.908, 0.180, 0.140, 0.032),
            (1.030, 0.165, 0.130, 0.040),
            (1.190, 0.186, 0.128, 0.050),
            (1.310, 0.208, 0.115, 0.025),
            (1.369, 0.188, 0.088, 0.018),
            (1.402, 0.078, 0.074, 0.038),
        ]
    if h <= table[0][0]:
        return table[0][1:]
    if h >= table[-1][0]:
        return table[-1][1:]
    for (h0, rx0, rz0, cz0), (h1, rx1, rz1, cz1) in zip(table, table[1:]):
        if h0 <= h <= h1:
            t = (h - h0) / (h1 - h0)
            ts = t * t * (3.0 - 2.0 * t)
            return (
                rx0 + (rx1 - rx0) * ts,
                rz0 + (rz1 - rz0) * ts,
                cz0 + (cz1 - cz0) * ts,
            )
    return table[-1][1:]


def build_sculpted_coder_human(ga, kind):
    """Build smooth MPFB head, neck, hands, and sculpted hoodie sleeves, while tucking the covered
    inner torso and legs inward so they never poke through the tailored hoodie or trouser shells."""
    transforms = ga.lower_pose(kind) if kind == "coder_seated" else {}
    for side in ("l", "r"):
        arm, _ = ga.pose(kind, side)
        transforms.update(arm)

    faces = ga.BODY
    ids = sorted({i for face in faces for i in face})
    lookup = {old: i for i, old in enumerate(ids)}

    pts = []
    for i in ids:
        p = ga.REST[i].copy()
        b = max(ga.WEIGHTS[i], key=lambda a: a[1])[0] if ga.WEIGHTS[i] else ""
        arm_vert = abs(p.x) > 0.195 and p.y > 0.90

        if b.startswith(("upperarm", "lowerarm")) or arm_vert:
            s = "l" if p.x > 0 else "r"
            wrist = ga.JOINT[s + "-hand"]
            elbow = ga.JOINT[s + "-elbow"]
            shoulder = ga.JOINT[s + "-shoulder"]
            fore_axis = (wrist - elbow).normalized()
            along_wrist = (p - wrist).dot(fore_axis)
            if along_wrist < -0.003:
                a, c = (elbow, wrist) if (b.startswith("lower") or abs(p.x) > 0.34) else (shoulder, elbow)
                axis = (c - a).normalized()
                centre = a + axis * (p - a).dot(axis)
                radial = p - centre
                ang = math.atan2(radial.y, radial.z)
                dist_wrist = max(0.0, -along_wrist)
                cuff_t = min(1.0, max(0.0, (dist_wrist - 0.006) / 0.034))
                arm_blend = min(1.0, max(0.0, (abs(p.x) - 0.175) / 0.045))
                fullness = (0.24 + 0.26 * cuff_t) * arm_blend
                fold = 0.014 * math.sin(dist_wrist * 95.0) * cuff_t * arm_blend
                fold += 0.025 * math.exp(-((dist_wrist - 0.044) / 0.015) ** 2) * arm_blend
                if dist_wrist < 0.038:
                    fold += 0.005 * math.cos(ang * 20.0) * arm_blend
                p += radial * (fullness + fold)
                if abs(p.x) < 0.205 and p.z < 0.0:
                    p.z *= 0.68
        elif 0.870 <= p.y <= 1.385 and abs(p.x) <= 0.220:
            # Tuck the covered inner torso well inside the outer tailored hoodie shell (zero overlap!)
            p.x *= 0.55
            p.z = 0.025 + (p.z - 0.025) * 0.52
        elif 0.115 <= p.y < 0.875 and abs(p.x) < 0.27:
            # Smooth continuous tailored denim jeans directly on the rigged legs (both standing & seated)
            if abs(p.x) < 0.065 and 0.74 < p.y < 0.885 and p.z > 0.052:
                p.z = 0.052 + (p.z - 0.052) * 0.15
                p.y = max(p.y, 0.795)
            s_leg = 1.0 if p.x >= 0 else -1.0
            hip = ga.JOINT[("l" if s_leg > 0 else "r") + "-upper-leg"]
            ankle = ga.JOINT[("l" if s_leg > 0 else "r") + "-ankle"]
            t_leg = min(1.0, max(0.0, (p.y - ankle.y) / max(0.01, hip.y - ankle.y)))
            leg_cx = ankle.x + (hip.x - ankle.x) * t_leg
            leg_cz = ankle.z + (hip.z - ankle.z) * t_leg
            groin_blend = min(1.0, abs(p.x) / 0.058) if t_leg > 0.78 else 1.0
            lx = p.x - leg_cx * groin_blend
            lz = p.z - leg_cz
            if kind == "coder":
                r_orig = max(0.018, math.hypot(lx, lz))
                r_tx = 0.060 + 0.025 * t_leg
                r_tz = 0.065 + 0.027 * t_leg
                blend_cyl = 0.70 * groin_blend
                sx = ((1.0 - blend_cyl) * r_orig + blend_cyl * r_tx) / r_orig
                sz = ((1.0 - blend_cyl) * r_orig + blend_cyl * r_tz) / r_orig
                p.x = leg_cx * groin_blend + lx * sx
                p.z = leg_cz + lz * sz
            else:
                taper = (0.18 + 0.08 * math.sin(t_leg * math.pi)) * (0.4 + 0.6 * groin_blend)
                knee_fold = 0.012 * math.sin((p.y - 0.46) * 75.0) * math.exp(-((p.y - 0.48) / 0.070) ** 2)
                ankle_fold = 0.016 * math.sin((p.y - 0.14) * 85.0) * math.exp(-((p.y - 0.165) / 0.042) ** 2)
                mult = 1.0 + taper + knee_fold + ankle_fold
                p.x = leg_cx * groin_blend + lx * mult
                p.z = leg_cz + lz * (mult + 0.03 * groin_blend)

        out = V()
        total = 0.0
        for bone, w in ga.WEIGHTS[i]:
            if bone in transforms:
                org, dest, rot = transforms[bone]
                out += (dest + rot @ (p - org)) * w
            else:
                out += p * w
            total += w
        pts.append(out / total if total else p)

    obj = ga.mesh("MPFB body", pts, [tuple(lookup[i] for i in f) for f in faces], "skin")

    base_keys = ("skin", "hair", "blue", "denim", "navy", "sole")
    slots = {k_mat: idx for idx, k_mat in enumerate(base_keys)}
    for k_mat in base_keys[1:]:
        obj.data.materials.append(ga.mat(k_mat))

    for poly, face in zip(obj.data.polygons, faces):
        p = sum((ga.REST[i] for i in face), V()) / len(face)
        arm = abs(p.x) > 0.205 and p.y > 0.90
        sidekey = "l" if p.x > 0 else "r"
        wrist = ga.JOINT[sidekey + "-hand"]
        fore = (wrist - ga.JOINT[sidekey + "-elbow"]).normalized()
        along = (p - wrist).dot(fore)
        if arm:
            if along > -0.004:
                key = "skin"
            elif along > -0.036:
                key = "navy"  # ribbed wrist cuff (slot 5)
            else:
                key = "blue"  # hoodie sleeve (slot 3)
        elif p.y < 0.115:
            key = "sole" if p.y < 0.033 else "navy"
        elif p.y < 0.927:
            key = "denim"
        elif p.y < 1.388 or (p.y < 1.422 and abs(p.x) > 0.066):
            key = "blue"
        elif p.y > 1.572 + 0.030 * min(1.0, max(-1.0, (p.z - 0.06) / 0.09)):
            key = "hair"
        else:
            key = "skin"
        poly.material_index = slots[key]

    denim_slot = slots["denim"]
    bm_leg = bmesh.new()
    bm_leg.from_mesh(obj.data)
    bm_leg.verts.ensure_lookup_table()
    denim_verts = [
        v for v in bm_leg.verts
        if any(f.material_index == denim_slot for f in v.link_faces)
        and all(f.material_index == denim_slot for f in v.link_faces)
    ]
    for _ in range(3):
        bmesh.ops.smooth_vert(bm_leg, verts=denim_verts, factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    bm_leg.to_mesh(obj.data)
    bm_leg.free()

    return smooth_obj(obj)


def build_tailored_hoodie_and_trousers(ga, kind):
    """Build smooth 44-segment sculpted hoodie torso and ribbed waistband hem
    with zero Z-fighting and full seated hip-knee-ankle articulation."""
    shift = ga.seated_shift() if kind == "coder_seated" else V((0, 0, 0))
    S = lambda p: (p[0] + shift.x, p[1] + shift.y, p[2] + shift.z)

    # 1) SCULPTED HOODIE TORSO SHELL ("blue", slot 3) & RIBBED WAISTBAND HEM ("navy", slot 5)
    n_torso = 44
    h_start = 0.962 if kind == "coder_seated" else 0.904
    h_end = 1.401
    n_h_rings = 16
    torso_rings = []
    for k in range(n_h_rings):
        t = k / (n_h_rings - 1)
        h = h_start + (h_end - h_start) * t
        rx, rz, cz = hoodie_cross_section(h, kind)
        ring = []
        for j in range(n_torso):
            a = j * math.tau / n_torso
            sa, ca = math.sin(a), math.cos(a)
            # Subtle vertical/diagonal cloth drape folds above the waistband
            waist_gather = math.exp(-((h - h_start - 0.025) / 0.035) ** 2)
            fold = 1.0 + 0.015 * waist_gather * math.cos(a * 6.0) + 0.008 * math.sin(t * math.pi * 3.0) * (0.5 + 0.5 * ca)
            x = rx * sa * fold
            z = cz + rz * ca * fold
            ring.append(S((x, h, z)))
        torso_rings.append(ring)
    add_loft_grid(ga, "SculptedHoodieTorso", torso_rings, "blue")

    # Ribbed waistband hem at bottom of hoodie ("navy", slot 5)
    hem_h0 = 0.928 if kind == "coder_seated" else 0.868
    hem_h1 = h_start + 0.008
    hem_rings = []
    for k, h in enumerate([hem_h0, 0.5 * (hem_h0 + hem_h1), hem_h1]):
        rx, rz, cz = hoodie_cross_section(h, kind)
        rx += 0.006 if k == 1 else 0.004
        rz += 0.006 if k == 1 else 0.004
        ring = []
        for j in range(n_torso):
            a = j * math.tau / n_torso
            rib = 1.0 + 0.005 * math.cos(a * 22.0)
            ring.append(S((rx * math.sin(a) * rib, h, cz + rz * math.cos(a) * rib)))
        hem_rings.append(ring)
    add_loft_grid(ga, "SculptedRibbedHem", hem_rings, "navy")


def build_coder_footwear(ga, kind):
    """High-craft 32-segment sculpted tech-sneakers with chunky treaded sole, padded collar, tongue & laces."""
    legs = ga.lower_pose(kind) if kind == "coder_seated" else None
    n_seg = 32
    for side in (-1, 1):
        side_key = "l" if side > 0 else "r"
        shift = V((0, 0, 0))
        if legs is not None:
            org, dest, _ = legs["foot_" + side_key]
            shift = dest - org

        def shoe_ring(h, rx, rz, cx, cz, tread=0.0):
            ring = []
            for j in range(n_seg):
                a = j * math.tau / n_seg
                r_mod = 1.0 + tread * math.cos(a * 16.0)
                x = rx * r_mod * math.sin(a)
                z = rz * r_mod * math.cos(a)
                toe_w = 1.0 + 0.07 * math.sin(a) * max(0.0, math.cos(a))
                pt = V((side * (cx + x * toe_w + 0.14 * z), h, cz + z)) + shift
                ring.append((pt.x, pt.y, pt.z))
            return ring

        base = (0.238, 0.074)
        outsole_rings = [
            shoe_ring(0.000, 0.062, 0.145, *base, tread=0.015),
            shoe_ring(0.010, 0.066, 0.148, *base, tread=0.018),
            shoe_ring(0.024, 0.065, 0.147, *base, tread=0.006),
            shoe_ring(0.034, 0.063, 0.144, *base, tread=0.000),
        ]
        add_loft_grid(ga, "SneakerOutsole", outsole_rings, "sole")

        upper_rings = [
            shoe_ring(0.028, 0.064, 0.145, *base),
            shoe_ring(0.048, 0.063, 0.139, *base),
            shoe_ring(0.072, 0.056, 0.112, 0.234, 0.054),
            shoe_ring(0.098, 0.050, 0.076, 0.228, 0.018),
            shoe_ring(0.120, 0.048, 0.064, 0.225, 0.003),
        ]
        add_loft_grid(ga, "SneakerUpper", upper_rings, "navy")

        t_pos = V((side * 0.232, 0.098, 0.068)) + shift
        tongue = smooth_oval(ga, "SneakerTongue", (t_pos.x, t_pos.y, t_pos.z), (0.025, 0.030, 0.013), "navy", segs=20, rings=12)
        tongue.rotation_euler.x = math.radians(-28)

        h_pos = V((side * 0.222, 0.092, -0.058)) + shift
        smooth_oval(ga, "SneakerHeelTab", (h_pos.x, h_pos.y, h_pos.z), (0.013, 0.020, 0.007), "ivory", segs=16, rings=10)

        for j in range(4):
            lz = 0.058 + j * 0.016
            ly = 0.098 - j * 0.0065
            p_in = V((side * 0.205, ly, lz)) + shift
            p_mid = V((side * 0.233, ly + 0.005, lz + 0.006)) + shift
            p_out = V((side * 0.258, ly, lz + 0.011)) + shift
            smooth_wire(ga, "SneakerLace", [(p_in.x, p_in.y, p_in.z), (p_mid.x, p_mid.y, p_mid.z), (p_out.x, p_out.y, p_out.z)], 0.0030, "sole", res=3)


def build_coder_clothing_and_head(ga, kind):
    """Build non-clipping sculpted hood collar, contoured kangaroo pocket, drawstrings, natural hair cap, AR visor, and headphones."""
    shift = ga.seated_shift() if kind == "coder_seated" else V((0, 0, 0))
    S = lambda p: (p[0] + shift.x, p[1] + shift.y, p[2] + shift.z)

    # 1) NON-CLIPPING SCULPTED HOOD & COLLAR ("navy", slot 5)
    # Positioned strictly outside the upper torso shoulder slope (y = 1.385..1.430 in front, draping to y = 1.255..1.425 at back)
    smooth_oval(ga, "FoldedBackHood", S((0.0, 1.335, -0.078)), (0.160, 0.096, 0.084), "navy", segs=32, rings=20)
    # Sculpted rolled hood collar ring sitting cleanly on top of the shoulder/neck opening (y >= 1.388)
    collar_pts = []
    n_col = 28
    for j in range(n_col):
        a = j * math.tau / n_col
        sa, ca = math.sin(a), math.cos(a)
        cx = 0.112 * sa
        cz = 0.018 + 0.094 * ca
        cy = 1.396 - 0.014 * ca + 0.006 * (sa ** 2)
        collar_pts.append(S((cx, cy, cz)))
    smooth_wire(ga, "HoodCollarRoll", collar_pts, 0.022, "navy", closed=True, res=5)

    # 2) NON-CLIPPING 3D KANGAROO POCKET ("navy", slot 5)
    # Computed directly from hoodie_cross_section(y, kind) so the inner wall sits 1.5 mm OUTSIDE the blue hoodie surface!
    pock_y0 = 0.968 if kind == "coder_seated" else 0.922
    pock_y1 = 1.062 if kind == "coder_seated" else 1.042
    pock_y_mid = 0.5 * (pock_y0 + pock_y1)
    n_pock = 28
    pock_rings = []
    for y_p, half_w, thick in [
        (pock_y0,          0.112, 0.002),
        (pock_y0 + 0.012,  0.116, 0.009),
        (pock_y_mid,       0.104, 0.011),
        (pock_y1 - 0.010,  0.082, 0.008),
        (pock_y1,          0.076, 0.002),
    ]:
        rx_t, rz_t, cz_t = hoodie_cross_section(y_p, kind)
        ring = []
        for j in range(n_pock):
            a = j * math.tau / n_pock
            u = math.sin(a)
            side_fb = math.cos(a)  # +1 outer face, -1 inner face against torso
            px = u * half_w
            # Exact torso front Z at (px, y_p) plus safety margin so zero triangles ever clip the torso
            cos_torso = math.sqrt(max(0.05, 1.0 - min(0.95, (px / rx_t) ** 2)))
            torso_front_z = cz_t + rz_t * cos_torso * 1.015
            pouch = thick * max(0.0, side_fb) * (1.0 - 0.20 * (u ** 4))
            pz = torso_front_z + 0.0015 + pouch
            ring.append(S((px, y_p, pz)))
        pock_rings.append(ring)
    add_loft_grid(ga, "KangarooPocket3D", pock_rings, "navy")

    # 3) BRAIDED HOODIE DRAWSTRINGS WITH EYELETS & AGLETS ("ivory" slot 7 + "coal" slot 10)
    for s in (-1, 1):
        smooth_oval(ga, "DrawstringEyelet", S((s * 0.054, 1.384, 0.112)), (0.005, 0.005, 0.003), "coal", segs=16, rings=10)
        cord_pts = [
            S((s * 0.054, 1.382, 0.114)),
            S((s * 0.048, 1.345, 0.138)),
            S((s * 0.046, 1.298, 0.160)),
            S((s * 0.049, 1.248, 0.173)),
        ]
        smooth_wire(ga, "HoodieDrawstring", cord_pts, 0.0034, "ivory", res=4)
        aglet_pts = [
            S((s * 0.049, 1.250, 0.173)),
            S((s * 0.050, 1.233, 0.176)),
        ]
        smooth_wire(ga, "DrawstringAglet", aglet_pts, 0.0040, "coal", res=4)

    # 4) EYES, SCULPTED SINGLE-PIECE TAPER-FADE HAIR CAP & EYEBROWS ("ivory" 7, "iris" 8, "hair" 2)
    for side_k in ("l", "r"):
        p_eye = ga.JOINT[side_k + "-eye"]
        smooth_oval(ga, "EyeSclera", S((p_eye.x, p_eye.y + 0.001, p_eye.z + 0.003)), (0.012, 0.010, 0.014), "ivory", segs=20, rings=14)
        smooth_oval(ga, "EyeIris", S((p_eye.x, p_eye.y + 0.001, p_eye.z + 0.015)), (0.006, 0.006, 0.003), "iris", segs=18, rings=12)

    # Single continuous sculpted hair mesh with natural side-parted crown volume and clean hairline (no floating blobs!)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=36, ring_count=24, location=ga.xyz(S((0.0, 1.595, 0.053))))
    hair_obj = bpy.context.object
    hair_obj.scale = (0.082, 0.105, 0.083)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for v in hair_obj.data.vertices:
        # Blender coords: x = game_x, y = -game_z, z = game_y - shift.y
        gx = v.co.x
        gz = -v.co.y
        gy = v.co.z - shift.y
        # Subtle textured crown wave and natural side-parted front quiff
        if gy > 1.58 and gz > 0.08:
            quiff = 0.007 * math.exp(-(((gz - 0.135) / 0.035) ** 2)) * (1.0 + 0.25 * math.sin(gx * 35.0 + 0.5))
            v.co.z += quiff
            v.co.y -= quiff * 0.45
    ga.finish(hair_obj, "SculptedCrownHair", "hair")
    smooth_obj(hair_obj)

    for s in (-1, 1):
        brow_pts = [
            S((s * 0.014, 1.573, 0.156)),
            S((s * 0.036, 1.577, 0.152)),
            S((s * 0.058, 1.571, 0.140)),
        ]
        smooth_wire(ga, "SculptedEyebrow", brow_pts, 0.0032, "hair", res=3)

    # 5) SLEEK 3D VOLUMETRIC WRAPAROUND CYBER-VISOR GLASSES ("teal" slot 9 lens + "coal" slot 10 frame)
    n_vis_cols = 25
    vis_front_top, vis_front_bot, vis_back_top, vis_back_bot = [], [], [], []
    for c_i in range(n_vis_cols):
        u = (c_i / (n_vis_cols - 1)) * 2.0 - 1.0
        ang = u * 0.82
        vx = 0.082 * math.sin(ang)
        vz_front = 0.114 + 0.052 * math.cos(ang)
        vz_back = vz_front - 0.0045
        vy_top = 1.568 + 0.005 * math.cos(ang * 1.5) - 0.006 * (u ** 2)
        nose_arch = 0.011 * math.exp(-((u / 0.22) ** 2))
        vy_bot = 1.531 + nose_arch + 0.008 * (u ** 4)
        vis_front_top.append(S((vx, vy_top, vz_front)))
        vis_front_bot.append(S((vx, vy_bot, vz_front)))
        vis_back_top.append(S((vx * 0.96, vy_top - 0.001, vz_back)))
        vis_back_bot.append(S((vx * 0.96, vy_bot + 0.001, vz_back)))

    v_pts = vis_front_top + vis_front_bot + vis_back_bot + vis_back_top
    v_faces = []
    for k_r in range(4):
        k_next = (k_r + 1) % 4
        for c_i in range(n_vis_cols - 1):
            a = k_r * n_vis_cols + c_i
            b = k_r * n_vis_cols + c_i + 1
            c = k_next * n_vis_cols + c_i + 1
            d = k_next * n_vis_cols + c_i
            v_faces.append((a, d, c, b))
    v_faces.append((0, n_vis_cols, 2 * n_vis_cols, 3 * n_vis_cols))
    r_last = n_vis_cols - 1
    v_faces.append((3 * n_vis_cols + r_last, 2 * n_vis_cols + r_last, n_vis_cols + r_last, r_last))
    visor_obj = ga.mesh("WraparoundARVisor3D", v_pts, v_faces, "teal")
    smooth_obj(visor_obj)

    smooth_wire(ga, "VisorTopBrowFrame", vis_front_top[1:-1], 0.0034, "coal", res=4)
    smooth_wire(ga, "VisorNoseBridge", vis_front_bot[9:16], 0.0026, "coal", res=3)
    for s in (-1, 1):
        temple_pts = [
            S((s * 0.075, 1.554, 0.132)),
            S((s * 0.082, 1.552, 0.105)),
            S((s * 0.083, 1.540, 0.058)),
        ]
        smooth_wire(ga, "VisorTempleArm", temple_pts, 0.0038, "coal", res=3)

    # 6) STUDIO OVER-EAR HEADPHONES ("coal" slot 10 + "navy" slot 5 accent)
    for s in (-1, 1):
        smooth_oval(ga, "HeadphoneEarCushion", S((s * 0.084, 1.539, 0.056)), (0.016, 0.041, 0.037), "coal", segs=26, rings=16)
        smooth_oval(ga, "HeadphoneOuterCup", S((s * 0.095, 1.539, 0.056)), (0.015, 0.037, 0.033), "coal", segs=26, rings=16)
        smooth_oval(ga, "HeadphoneAccentCap", S((s * 0.105, 1.539, 0.056)), (0.007, 0.028, 0.025), "navy", segs=22, rings=14)
        yoke_pts = [
            S((s * 0.096, 1.576, 0.056)),
            S((s * 0.102, 1.555, 0.071)),
            S((s * 0.102, 1.539, 0.073)),
        ]
        smooth_wire(ga, "HeadphoneYoke", yoke_pts, 0.0034, "coal", res=3)

    band_pts = [
        S((0.093 * math.cos(i * math.pi / 20), 1.548 + 0.131 * math.sin(i * math.pi / 20), 0.055))
        for i in range(21)
    ]
    smooth_wire(ga, "HeadphoneHeadband", band_pts, 0.0105, "coal", res=4)


def build_coder_laptop(ga):
    """Build sleek beveled unibody aluminum laptop using ONLY materials 11..14 ('laptop', 'glass', 'display', 'cyan')
    so 100% of the laptop disappears when c.LaptopLost is true!"""
    bottom = 1.052
    deck_top = bottom + 0.016
    width = 0.389
    hinge = 0.457

    # 1) Beveled unibody aluminum base deck & cylindrical display hinge ("laptop", slot 11)
    smooth_box(ga, "LaptopUnibodyDeck", (0.0, bottom + 0.008, 0.347), (width, 0.016, 0.224), "laptop", bevel=0.005, segments=3)
    smooth_wire(ga, "LaptopHingeBarrel", [(-0.145, deck_top, hinge - 0.004), (0.145, deck_top, hinge - 0.004)], 0.0055, "laptop", res=4)

    # 2) Dark recessed keyboard well ("display", slot 13) & precision glass trackpad ("glass", slot 12)
    smooth_box(ga, "LaptopKeyboardWell", (0.0, deck_top + 0.0006, 0.372), (0.318, 0.002, 0.098), "display", bevel=0.0015, segments=2)
    smooth_box(ga, "LaptopGlassTrackpad", (0.0, deck_top + 0.0007, 0.281), (0.124, 0.0016, 0.056), "glass", bevel=0.0015, segments=2)

    # 3) 5 rows x 13 columns of crisp keycaps ("glass", slot 12)
    for row in range(5):
        kz = 0.332 + row * 0.019
        if row == 0:
            for kx, kw in [(-0.136, 0.022), (-0.110, 0.022), (-0.084, 0.022), (0.0, 0.132), (0.084, 0.022), (0.110, 0.022), (0.136, 0.022)]:
                smooth_box(ga, "LaptopKey", (kx, deck_top + 0.0020, kz), (kw, 0.0020, 0.0145), "glass", bevel=0.0, segments=1)
        else:
            for col in range(13):
                kx = (col - 6.0) * 0.0235
                smooth_box(ga, "LaptopKey", (kx, deck_top + 0.0020, kz), (0.020, 0.0020, 0.0145), "glass", bevel=0.0, segments=1)

    # 4) Angled thin-bezel aluminum lid ("laptop", slot 11) + glowing back emblem ("glass" 12 & "cyan" 14)
    lean = math.radians(14.0)

    def lid_point(x, h, depth=0.0):
        return (
            x,
            deck_top + h * math.cos(lean) - depth * math.sin(lean),
            hinge + h * math.sin(lean) + depth * math.cos(lean),
        )

    lid = smooth_box(ga, "LaptopOpenLid", lid_point(0.0, 0.112, 0.0), (width, 0.224, 0.009), "laptop", bevel=0.0045, segments=3)
    lid.rotation_euler.x = lean

    emblem_outer = smooth_oval(ga, "LaptopLidBadgeRing", lid_point(0.0, 0.112, 0.0050), (0.022, 0.019, 0.0018), "cyan", segs=24, rings=12)
    emblem_outer.rotation_euler.x = lean
    emblem_inner = smooth_oval(ga, "LaptopLidBadgeCore", lid_point(0.0, 0.112, 0.0056), (0.016, 0.014, 0.0018), "glass", segs=24, rings=12)
    emblem_inner.rotation_euler.x = lean

    # 5) High-contrast dark IDE display ("display", slot 13) + crisp left-aligned syntax token layout ("cyan", slot 14)
    screen = smooth_box(ga, "LaptopIDEDisplay", lid_point(0.0, 0.113, -0.0046), (0.366, 0.200, 0.002), "display", bevel=0.0015, segments=2)
    screen.rotation_euler.x = lean

    code_bars = [
        ( 0.000, 0.196, 0.346, 0.0020),
        ( 0.000, 0.026, 0.346, 0.0020),
        (-0.112, 0.111, 0.0016, 0.166),
    ]
    for row_i, (indent, w_tok) in enumerate([
        (0.000, 0.028), (0.006, 0.032), (0.006, 0.026), (0.012, 0.030),
        (0.012, 0.024), (0.006, 0.034), (0.000, 0.026), (0.006, 0.028),
        (0.006, 0.032), (0.012, 0.022),
    ]):
        ty = 0.182 - row_i * 0.0145
        tx = -0.164 + indent + w_tok * 0.5
        code_bars.append((tx, ty, w_tok, 0.0019))

    editor_lines = [
        (0, [0.032, 0.048, 0.026]),
        (1, [0.024, 0.062, 0.038]),
        (2, [0.044, 0.056, 0.032, 0.024]),
        (2, [0.036, 0.074, 0.028]),
        (3, [0.052, 0.042, 0.034]),
        (3, [0.028, 0.068]),
        (2, [0.018]),
        (1, [0.034, 0.054, 0.040]),
        (2, [0.048, 0.064, 0.030]),
        (2, [0.030, 0.052, 0.044, 0.022]),
        (1, [0.022]),
        (0, [0.018]),
    ]
    for row_i, (tabs, tokens) in enumerate(editor_lines):
        ty = 0.184 - row_i * 0.0125
        cur_x = -0.098 + tabs * 0.014
        for w_tok in tokens:
            tx = cur_x + w_tok * 0.5
            code_bars.append((tx, ty, w_tok, 0.0020))
            cur_x += w_tok + 0.007

    for cx, cy, cw, ch in code_bars:
        bar = smooth_box(ga, "LaptopCodeLine", lid_point(cx, cy, -0.0058), (cw, ch, 0.0010), "cyan", bevel=0.0, segments=1)
        bar.rotation_euler.x = lean


def assign_uvs_and_bake_ao(ga, body, mat_order):
    mesh = body.data

    old_names = [m.name.split(".")[0] if m else "skin" for m in mesh.materials]
    name_to_target_idx = {name: idx for idx, name in enumerate(mat_order)}
    poly_target_indices = [
        name_to_target_idx.get(old_names[poly.material_index], 0)
        for poly in mesh.polygons
    ]
    mesh.materials.clear()
    for name in mat_order:
        mesh.materials.append(ga.mat(name))
    for poly, new_idx in zip(mesh.polygons, poly_target_indices):
        poly.material_index = new_idx

    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
    bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = True
    mesh.update()

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    quad_offsets = {
        "blue":    (0.04, 0.04, 5.5),
        "navy":    (0.04, 0.04, 6.0),
        "ivory":   (0.04, 0.04, 6.5),
        "denim":   (0.54, 0.04, 5.5),
        "sole":    (0.54, 0.04, 6.5),
        "coal":    (0.54, 0.04, 6.0),
        "skin":    (0.04, 0.54, 4.5),
        "hair":    (0.04, 0.54, 6.5),
        "iris":    (0.04, 0.54, 8.0),
        "teal":    (0.54, 0.54, 5.0),
        "laptop":  (0.54, 0.54, 4.5),
        "glass":   (0.54, 0.54, 6.0),
        "display": (0.54, 0.54, 4.0),
        "cyan":    (0.54, 0.54, 6.0),
    }

    verts = mesh.vertices
    for poly in mesh.polygons:
        m_name = mat_order[poly.material_index]
        u_base, v_base, scale = quad_offsets.get(m_name, (0.04, 0.04, 5.0))
        for loop_idx in poly.loop_indices:
            v_co = verts[mesh.loops[loop_idx].vertex_index].co
            raw_u = (v_co.x + 0.45 * v_co.y) * scale
            raw_v = (v_co.z + 0.25 * v_co.y) * scale
            tu = 1.0 - abs((raw_u % 2.0) - 1.0)
            tv = 1.0 - abs((raw_v % 2.0) - 1.0)
            fu = tu * 0.42 + u_base
            fv = tv * 0.42 + v_base
            uv_layer.data[loop_idx].uv = (fu, fv)

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    vcol = mesh.color_attributes.new(name="Color", type="BYTE_COLOR", domain="POINT")

    tree = BVHTree.FromPolygons([v.co for v in verts], [tuple(p.vertices) for p in mesh.polygons])

    v_mat = [0] * len(verts)
    for poly in mesh.polygons:
        mi = poly.material_index
        for vi in poly.vertices:
            if mi > v_mat[vi]:
                v_mat[vi] = mi

    golden_angle = 2.39996323
    rays_local = [
        (math.cos(j * golden_angle) * math.sqrt((j + 0.5) / 8.0),
         math.sin(j * golden_angle) * math.sqrt((j + 0.5) / 8.0),
         math.sqrt(max(0.0, 1.0 - (j + 0.5) / 8.0)))
        for j in range(8)
    ]

    for v in verts:
        m_name = mat_order[v_mat[v.index]]
        if m_name in ("cyan", "display", "teal", "iris"):
            vcol.data[v.index].color = (1.0, 1.0, 1.0, 1.0)
            continue
        n = v.normal.normalized()
        if n.length_squared < 0.01:
            n = V((0.0, -1.0, 0.0))
        up = V((1.0, 0.0, 0.0)) if abs(n.x) < 0.85 else V((0.0, 1.0, 0.0))
        t1 = (up - n * n.dot(up)).normalized()
        t2 = n.cross(t1)
        origin = v.co + n * 0.0015
        ao = 0.0
        max_dist = 0.055
        for rx, ry, rz in rays_local:
            ray = (t1 * rx + t2 * ry + n * (rz * 0.85 + 0.25)).normalized()
            hit_dist = tree.ray_cast(origin, ray, max_dist)[3]
            if hit_dist is not None:
                ao += (1.0 - hit_dist / max_dist) / 8.0
        h_game = v.co.z
        v_grad = 0.95 + 0.05 * min(1.0, max(0.0, h_game / 1.55))
        ao_strength = 0.30 if m_name == "skin" else 0.36
        shade = max(0.62, min(1.0, v_grad * (1.0 - ao_strength * ao)))
        if m_name == "skin":
            vcol.data[v.index].color = (shade, shade * 0.97, shade * 0.95, 1.0)
        else:
            vcol.data[v.index].color = (shade, shade, shade, 1.0)


def patch_exported_glb_materials(glb_path, ga, mat_order):
    raw = glb_path.read_bytes()
    magic, version, total_len = struct.unpack("<III", raw[:12])
    assert magic == 0x46546C67
    json_len, json_type = struct.unpack("<II", raw[12:20])
    assert json_type == 0x4E4F534A
    gltf = json.loads(raw[20 : 20 + json_len].decode("utf-8"))
    bin_chunk = raw[20 + json_len :]

    has_tex = bool(gltf.get("textures"))
    materials = gltf.get("materials", [])
    assert len(materials) == len(mat_order), f"Expected {len(mat_order)} materials, got {len(materials)}: {[m.get('name') for m in materials]}"

    for idx, key in enumerate(mat_order):
        m_dict = materials[idx]
        m_dict["name"] = key
        pbr = m_dict.setdefault("pbrMetallicRoughness", {})
        pbr["baseColorFactor"] = srgb_hex_to_linear(ga.PALETTE[key])
        met, rough = PBR_SPECS.get(key, (0.0, 0.75))
        pbr["metallicFactor"] = met
        pbr["roughnessFactor"] = rough
        if has_tex:
            pbr["baseColorTexture"] = {"index": 0}
        if key in ("cyan", "teal"):
            lin = srgb_hex_to_linear(ga.PALETTE[key])[:3]
            m_dict["emissiveFactor"] = lin

    if len(mat_order) == 10 and gltf.get("meshes"):
        gltf["meshes"][0].setdefault("extras", {})["pose"] = "Hip and knee 90 degrees; hands on workstation keyboard; no laptop"

    new_json_bytes = json.dumps(gltf, separators=(",", ":")).encode("utf-8")
    pad = (4 - (len(new_json_bytes) % 4)) % 4
    new_json_bytes += b" " * pad
    new_total = 12 + 8 + len(new_json_bytes) + len(bin_chunk)
    out_bytes = (
        struct.pack("<III", magic, version, new_total)
        + struct.pack("<II", len(new_json_bytes), json_type)
        + new_json_bytes
        + bin_chunk
    )
    glb_path.write_bytes(out_bytes)


def export_vibe_coder(ga, kind, filename):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ga.MATS.clear()
    atlas_img = build_detail_atlas()
    mat_order = SEATED_MAT_ORDER if kind == "coder_seated" else CODER_MAT_ORDER
    for key in mat_order:
        make_coder_mat(ga, key, atlas_img)

    body_main = build_sculpted_coder_human(ga, kind)
    build_tailored_hoodie_and_trousers(ga, kind)
    build_coder_clothing_and_head(ga, kind)
    build_coder_footwear(ga, kind)
    if kind == "coder":
        build_coder_laptop(ga)

    all_meshes = [body_main] + [o for o in bpy.context.scene.objects if o.type == "MESH" and o != body_main]
    body = ga.join(all_meshes, filename)
    if kind == "coder_seated":
        body["pose"] = "Hip and knee 90 degrees; hands on workstation keyboard; no laptop"

    assign_uvs_and_bake_ao(ga, body, mat_order)

    out_path = ga.OUT / (filename + ".glb")
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_vertex_color="ACTIVE",
        export_normals=True,
        export_texcoords=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
        export_extras=True,
    )
    patch_exported_glb_materials(out_path, ga, mat_order)
    print(f"UPGRADED {filename}.glb: {len(body.data.vertices)} verts, {len(body.data.polygons)} polys, {len(body.data.materials)} materials")
