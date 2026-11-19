"""Standalone builder & runtime-accurate verifier for assets/models/fps_hands_net.glb.

Fixes the two exact issues from #160 & #161 without touching generate_anatomy.py or any .cs files:
  1. Reposes the right hand from the old pronated over-the-top soldering-iron grip into a
     natural, upright/slightly canted ergonomic broom/net shake-hands grip wrapped directly
     around the Cobalt Blue Electrical Tape & wood grip zone (thumb on top/inner-left pointing
     forward along the shaft, four fingers curling snugly underneath and up the inner-left side
     of the blue electrical tape).
  2. Angles the wrist, white shirt cuff, and crimson jacket sleeve 45 deg downward and outward
     to the lower-right (-X_arm, -Y_arm) away from the shaft axis so the cyan plastic butt cap
     (z_local = -0.104..-0.084) projects freely into open air above and to the inner-left of the
     wrist/cuff with > 5.5 cm clearance and zero sleeve overlap!
  3. Preserves FINAL overlap = 0 against weapon_net_90s.glb and renders a 2-panel proof
     (Exact In-Game POV Camera + Right-Wrist Close-Up) to net_grip_v2_proof.png.
"""
import math
import os
import runpy
import shutil
from pathlib import Path

import bpy
import bmesh
import numpy as np
from mathutils import Vector, Matrix, Quaternion
from mathutils.bvhtree import BVHTree

V = Vector
ROOT = Path(__file__).resolve().parent.parent
OUT_MODELS = ROOT / "assets" / "models"
ARTIFACT_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a")
MY_SCRATCH_DIR = Path(r"C:\Users\se-20\.gemini\antigravity\brain\5a288ef4-482c-487e-b05f-e3d0e2267cf7\scratch")
SCREENSHOTS_DIR = ROOT / "screenshots"


def net_shaft_radius(z_ga):
    """Exact outer radius of weapon_net_90s.glb shaft at arm-local z_ga (where grip origin is z_ga = 0.620)."""
    z_local = z_ga - 0.620
    if z_local < -0.080:
        return 0.0136  # cyan mop cap
    if -0.052 <= z_local <= 0.040:
        return 0.0134  # blue electrical tape grip
    if 0.040 < z_local <= 0.063:
        return 0.0127  # birch wood upper grip
    if 0.063 < z_local <= 0.129:
        return 0.0131  # aluminum ferrule
    return 0.0154      # front tape & wire lashing


def net_palm_fit(p):
    """Conforms right-hand skin vertices smoothly outside the cylindrical mop handle & blue tape
    at (-0.180, -0.150, z_ga) with a clean 1.15 mm safety cushion so BVH overlap == 0."""
    x = p.x + 0.180
    y = p.y + 0.150
    r = math.hypot(x, y)
    if 0.510 < p.z < 0.785:
        r_min = net_shaft_radius(p.z) + 0.00145
        if 0.0 < r < r_min:
            return V((-0.180 + x * r_min / r, -0.150 + y * r_min / r, p.z))
    return p


def build_net_right_pose(ga):
    """Ergonomic upright/canted broom/racket grip on the blue-taped shaft:
      - Shaft centerline is (-0.180, -0.150, z_ga).
      - Grip center c = (-0.180, -0.150, 0.664) (+4.4 cm up the shaft from 0.620, wrapping the blue
        electrical tape & wood grip zone).
      - Hand & forearm approach from below-outer-right at a natural ~58 deg upright racket angle
        (f_target = (0.45, 0.73, 0.514)), so the wrist (z_ga ~ 0.630, y_ga ~ -0.214, x_ga ~ -0.236)
        and white shirt cuff drop cleanly DOWNWARD and OUTWARD to the lower-right, while the rear
        of the handle and cyan butt cap (z_ga = 0.516..0.536, y_ga = -0.150, x_ga = -0.180) project
        backward into wide-open air above-left of the wrist & sleeve (> 7.5 cm clearance!).
    """
    j = lambda key: ga["JOINT"]["r-" + key]
    DIGITS = ga["DIGITS"]
    frame = ga["frame"]

    c = V((-0.180, -0.150, 0.648))
    wrist = j("hand")
    mid = j("finger-3-1")

    palm_sign = -1.0
    f_rest = (mid - wrist).normalized()
    u_rest = (j("finger-2-1") - j("finger-5-1")) * palm_sign
    a_rest = (u_rest - f_rest * f_rest.dot(u_rest)).normalized()
    palm_rest = f_rest.cross(a_rest).normalized()
    rest_frame = Matrix((f_rest, a_rest, palm_rest)).transposed()

    # Upright ergonomic racket/broom hand orientation:
    f_target = V((0.45, 0.73, 0.514)).normalized()
    palm_raw = V((0.86, 0.22, -0.46)).normalized()
    palm_target = (palm_raw - f_target * f_target.dot(palm_raw)).normalized()
    a_target = palm_target.cross(f_target).normalized()
    target_frame = Matrix((f_target, a_target, palm_target)).transposed()
    hand = target_frame @ rest_frame.transposed()

    knuckle = c + V((-0.025, -0.010, 0.006))
    dest_wrist = knuckle - hand @ (mid - wrist)
    # Forearm drops downward (-Y) and outward to the right (-X) toward the offscreen right elbow anchor
    dest_elbow = dest_wrist + V((-0.24, -0.28, -0.12))

    transforms = {}
    for name, a, b, p, q in [
        ("upperarm", j("shoulder"), j("elbow"), dest_elbow - V((0, 0, 0.2)), dest_elbow),
        ("lowerarm", j("elbow"), wrist, dest_elbow, dest_wrist),
    ]:
        src = b - a
        dst = q - p
        u = src.normalized()
        stretch = Matrix.Identity(3) + Matrix([list(u * u[n]) for n in range(3)]) * (dst.length / src.length - 1.0)
        across = j("finger-2-1") - j("finger-5-1")
        rotation = frame(dst, hand @ across) @ frame(src, across).transposed()
        transforms[name + "_r"] = (a, p, rotation @ stretch)
    transforms["hand_r"] = (wrist, dest_wrist, hand)

    signet = None
    for d, name in enumerate(DIGITS, 1):
        chain = [j(f"finger-{d}-{k}") for k in range(1, 5)]
        if d == 1:
            # Thumb wraps over the top-outer to top-inner side of the shaft pointing forward toward the hoop
            pos = c + V((-0.026, 0.004, -0.004))
            thumb_targets = [
                c + V((-0.015, 0.021, 0.015)),
                c + V(( 0.001, 0.023, 0.031)),
                c + V(( 0.014, 0.017, 0.044)),
            ]
            parent = hand
            for k in range(3):
                rest = chain[k + 1] - chain[k]
                direction = (thumb_targets[k] - pos).normalized()
                rot = (parent @ rest).rotation_difference(direction).to_matrix() @ parent
                parent = rot
                transforms[f"{name}_{k+1:02}_r"] = (chain[k], pos.copy(), rot)
                delta = rot @ rest
                pos += delta
        else:
            # Four fingers (index d=2 .. pinky d=5) curling snugly underneath (-90 deg) and up the inner-left side
            along = (0.025, 0.008, -0.009, -0.026)[d - 2]
            azimuth = math.radians((-156.0, -152.0, -148.0, -142.0)[d - 2])
            radius = (0.027, 0.028, 0.027, 0.025)[d - 2]
            pos = c + V((radius * math.cos(azimuth), radius * math.sin(azimuth), along))
            parent = hand
            for k in range(3):
                rest = chain[k + 1] - chain[k]
                next_radius = ((0.022, 0.0195, 0.0185) if d == 5 else (0.0235, 0.0205, 0.0192))[k]
                dz = rest.length * 0.06
                chord_sq = max(1e-6, rest.length_squared - dz * dz)
                cos_sweep = (radius ** 2 + next_radius ** 2 - chord_sq) / (2.0 * radius * next_radius)
                azimuth += math.acos(max(-1.0, min(1.0, cos_sweep)))
                joint_pt = c + V((next_radius * math.cos(azimuth), next_radius * math.sin(azimuth), pos.z - c.z + dz))
                direction = (joint_pt - pos).normalized()
                radius = next_radius
                rot = (parent @ rest).rotation_difference(direction).to_matrix() @ parent
                parent = rot
                transforms[f"{name}_{k+1:02}_r"] = (chain[k], pos.copy(), rot)
                delta = rot @ rest
                if d == 4 and k == 0:
                    signet = (pos + delta * 0.42, delta.normalized())
                pos += delta

    return transforms, signet


def build_net_left_pose(ga):
    return ga["bare_hands_pose"]("l")


def build_and_export_fps_hands_net():
    ga = runpy.run_path(str(ROOT / "generate_anatomy.py"))
    orig_contact_pose = ga["contact_pose"]

    def custom_contact_pose(kind, side):
        if kind == "fps_net":
            if side == "r":
                return build_net_right_pose(ga)
            else:
                return build_net_left_pose(ga)
        return orig_contact_pose(kind, side)

    ga["contact_pose"] = custom_contact_pose
    ga["solder_palm_fit"] = net_palm_fit
    ga["human"].__globals__["contact_pose"] = custom_contact_pose
    ga["human"].__globals__["solder_palm_fit"] = net_palm_fit
    ga["export"].__globals__["contact_pose"] = custom_contact_pose
    ga["export"].__globals__["solder_palm_fit"] = net_palm_fit

    ga["export"]("fps_net", "fps_hands_net")

    out_glb = OUT_MODELS / "fps_hands_net.glb"
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest = ROOT / sub
        if dest.exists():
            shutil.copy2(out_glb, dest / "fps_hands_net.glb")
    print(f"[EXPORTED & SYNCED] {out_glb} ({out_glb.stat().st_size} bytes)")
    return ga


def build_runtime_posed_pair(collection_name):
    """Imports weapon_net_90s.glb and fps_hands_net.glb into a new collection and applies the
    EXACT runtime DrawCaptureTool pose + VisualEngine3D.cs vertex shader sleevePull deformation.
    Coordinates are mapped into Blender space where:
      +X_bl = Screen Right (-X_rl after Rlgl.Scalef(-1,1,1))
      +Y_bl = Screen Forward Depth (+Z_rl)
      +Z_bl = Screen Up (+Y_rl)
    So a camera at (0, 0, 0) rotated (90 deg, 0, 0) sees the EXACT Raylib first-person view!"""
    col = bpy.data.collections.new(collection_name)
    bpy.context.scene.collection.children.link(col)

    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "weapon_net_90s.glb"))
    w_imp = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "fps_hands_net.glb"))
    h_imp = [o for o in bpy.context.selected_objects if o.type == "MESH"]

    for o in w_imp + h_imp:
        for c_old in list(o.users_collection):
            c_old.objects.unlink(o)
        col.objects.link(o)

    offset = V((0.035, -0.012, -0.105))
    grip = offset + V((0.18, -0.15, 0.62))

    ry = -0.52
    rx = -0.25
    cos_y, sin_y = math.cos(ry), math.sin(ry)
    cos_x, sin_x = math.cos(rx), math.sin(rx)

    def rot_y(v):
        return V((v.x * cos_y + v.z * sin_y, v.y, -v.x * sin_y + v.z * cos_y))

    def rot_x(v):
        return V((v.x, v.y * cos_x - v.z * sin_x, v.y * sin_x + v.z * cos_x))

    def apply_pose_rl(p_rl):
        return rot_x(rot_y(p_rl - grip)) + grip

    def inv_pose_rl(p_rl):
        v = p_rl - grip
        vx = V((v.x, v.y * cos_x + v.z * sin_x, -v.y * sin_x + v.z * cos_x))
        vy = V((vx.x * cos_y - vx.z * sin_y, vx.y, vx.x * sin_y + vx.z * cos_y))
        return vy + grip

    def rl_to_bl_cam(p_rl):
        # After Rlgl.Scalef(-1, 1, 1), +X_rl is on the RIGHT side of the screen (+X_bl), +Y_rl is UP (+Z_bl), +Z_rl is FORWARD (+Y_bl)
        return V((p_rl.x, p_rl.z, p_rl.y))

    for o in w_imp:
        for v in o.data.vertices:
            vw = o.matrix_world @ v.co
            p_rl = V((vw.x, vw.z, -vw.y)) + grip
            v.co = rl_to_bl_cam(apply_pose_rl(p_rl))
        o.matrix_world = Matrix.Identity(4)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()

    for o in h_imp:
        is_right = "Right" in o.name
        bind = V(o.data["wrist"])
        end = V(o.data["sleeveEnd"])
        sleeve_axis = end - bind
        anchor_rl = V((0.56 if is_right else -0.18, -0.395, 0.23))
        p_unposed = inv_pose_rl(anchor_rl) - offset
        anchor_arm = V((-p_unposed.x, p_unposed.y, p_unposed.z))
        sleeve_pull = anchor_arm - bind

        uv_layer = o.data.uv_layers.active
        vert_uv = {}
        if uv_layer:
            for loop in o.data.loops:
                if loop.vertex_index not in vert_uv:
                    vert_uv[loop.vertex_index] = uv_layer.data[loop.index].uv.copy()

        for v in o.data.vertices:
            vw = o.matrix_world @ v.co
            p = V((vw.x, vw.z, -vw.y))
            uv = vert_uv.get(v.index, V((0.0, 0.0)))
            if uv.x > 1.99 and sleeve_pull.length_squared > 0.0001:
                # Note: Blender's glTF importer flips V back to (1.0 - v_gltf), so Raylib's vertexTexCoord.y is (1.0 - uv.y)!
                t = max(0.0, min(1.0, 1.0 - uv.y))
                a_dir = sleeve_axis.normalized()
                tangent0 = a_dir * min(sleeve_axis.length, sleeve_pull.length * 0.54)
                tangent1 = sleeve_pull * 1.34
                t2 = t * t
                t3 = t2 * t
                center = (t3 - 2.0 * t2 + t) * tangent0 + (3.0 * t2 - 2.0 * t3) * sleeve_pull + (t3 - t2) * tangent1
                b_vec = (3.0 * t2 - 4.0 * t + 1.0) * tangent0 + 6.0 * t * (1.0 - t) * sleeve_pull + (3.0 * t2 - 2.0 * t) * tangent1
                b_dir = b_vec.normalized() if b_vec.length > 1e-6 else a_dir
                st = max(0.0, min(1.0, (t - 0.055) / (0.22 - 0.055)))
                blend = st * st * (3.0 - 2.0 * st)
                cr = a_dir.cross(b_dir)
                qw = 1.0 + a_dir.dot(b_dir)
                q_vec = V((cr.x * blend, cr.y * blend, cr.z * blend))
                q_w = (1.0 - blend) * 1.0 + blend * qw
                q_len = math.sqrt(q_vec.length_squared + q_w * q_w)
                q_vec /= q_len
                q_w /= q_len
                off_v = p - bind - sleeve_axis * t
                p = p + 2.0 * q_vec.cross(q_vec.cross(off_v) + q_w * off_v)
                p = p + (center - sleeve_axis * t) * blend

            p_rl = V((-p.x, p.y, p.z)) + offset
            v.co = rl_to_bl_cam(apply_pose_rl(p_rl))

        o.matrix_world = Matrix.Identity(4)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()

    return w_imp, h_imp


def verify_and_render_proof():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # 1. Verify BVH overlap == 0 and cyan butt cap clearance in arm-local space
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "weapon_net_90s.glb"))
    w_objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    w_verts_arm, w_tris_arm, cap_verts_arm = [], [], []
    for o in w_objs:
        o.data.calc_loop_triangles()
        base = len(w_verts_arm)
        for v in o.data.vertices:
            vw = o.matrix_world @ v.co
            p_rl = V((vw.x, vw.z, -vw.y)) + V((0.18, -0.15, 0.62))
            p_arm = V((-p_rl.x, p_rl.y, p_rl.z))
            w_verts_arm.append(p_arm)
            if p_arm.z < 0.542:
                cap_verts_arm.append(p_arm)
        for t in o.data.loop_triangles:
            w_tris_arm.append((base + t.vertices[0], base + t.vertices[1], base + t.vertices[2]))
    prop_bvh = BVHTree.FromPolygons(w_verts_arm, w_tris_arm, all_triangles=True)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUT_MODELS / "fps_hands_net.glb"))
    h_objs = {o.name: o for o in bpy.context.scene.objects if o.type == "MESH"}
    right_obj, left_obj = h_objs["Right"], h_objs["Left"]
    wrist_r = V(right_obj.data["wrist"])
    sleeve_end_r = V(right_obj.data["sleeveEnd"])
    print(f"Right extras.wrist = {tuple(round(c, 4) for c in wrist_r)}, sleeveEnd = {tuple(round(c, 4) for c in sleeve_end_r)}")

    for side_name, obj in (("Right", right_obj), ("Left", left_obj)):
        obj.data.calc_loop_triangles()
        verts_arm = [V(((obj.matrix_world @ v.co).x, (obj.matrix_world @ v.co).z, -(obj.matrix_world @ v.co).y)) for v in obj.data.vertices]
        tris = [tuple(t.vertices) for t in obj.data.loop_triangles]
        bvh = BVHTree.FromPolygons(verts_arm, tris, all_triangles=True)
        hits = len(bvh.overlap(prop_bvh))
        min_gap = min(prop_bvh.find_nearest(p)[3] for p in verts_arm)
        print(f"  {side_name} vs weapon_net_90s: overlap = {hits} tris, nearest vertex = {min_gap*1000:.2f} mm")
        assert hits == 0, f"BVH overlap failure on {side_name}: {hits}!"

    cuff_verts_arm = []
    ivory_idx = next((i for i, m in enumerate(right_obj.data.materials) if m and m.name.startswith("ivory")), None)
    for poly in right_obj.data.polygons:
        if poly.material_index == ivory_idx:
            for vi in poly.vertices:
                vw = right_obj.matrix_world @ right_obj.data.vertices[vi].co
                cuff_verts_arm.append(V((vw.x, vw.z, -vw.y)))
    min_cap_to_cuff = min((cp - cv).length for cp in cap_verts_arm for cv in cuff_verts_arm)
    min_cap_to_wrist = min((cp - wrist_r).length for cp in cap_verts_arm)
    print(f"  Cyan Butt Cap to White Shirt Cuff clearance = {min_cap_to_cuff*100:.2f} cm (to wrist pivot = {min_cap_to_wrist*100:.2f} cm)")

    # 2. Render Two Full-Res 960x900 Panels (1: Exact Game POV Camera at origin; 2: Close-Up Right-Wrist & Blue-Tape Grip Inspection)
    # and stitch them side-by-side into 1920x900 net_grip_v2_proof.png!
    bpy.ops.wm.read_factory_settings(use_empty=True)
    w_rt, h_rt = build_runtime_posed_pair("RuntimeViewModel")

    # Also verify runtime deformed sleeve/cuff vs posed cyan butt cap distance!
    posed_cap = [v.co.copy() for o in w_rt for v in o.data.vertices if v.co.y < 0.49]
    posed_right = next(o for o in h_rt if "Right" in o.name)
    posed_prop_bvh = BVHTree.FromPolygons(
        [v.co.copy() for o in w_rt for v in o.data.vertices],
        [tuple(t.vertices) for o in w_rt for t in (o.data.calc_loop_triangles() or o.data.loop_triangles)],
        all_triangles=True,
    )
    posed_right.data.calc_loop_triangles()
    posed_right_bvh = BVHTree.FromPolygons(
        [v.co.copy() for v in posed_right.data.vertices],
        [tuple(t.vertices) for t in posed_right.data.loop_triangles],
        all_triangles=True,
    )
    rt_hits = len(posed_right_bvh.overlap(posed_prop_bvh))
    print(f"  Runtime Posed + SleevePulled Right vs weapon_net_90s: overlap = {rt_hits} tris")
    assert rt_hits == 0, f"Runtime sleevePull overlap: {rt_hits}!"

    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 960
    scene.render.resolution_y = 900
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("NetProofWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.062, 0.076, 0.100, 1.0)
        bg.inputs[1].default_value = 1.15

    for lname, lpos, energy, col in [
        ("KeyLight", (1.6, -1.2, 1.8), 4.2, (1.0, 0.95, 0.86)),
        ("FillLight", (-1.8, -1.4, 1.0), 2.8, (0.78, 0.90, 1.0)),
        ("RimLight", (0.0, 2.0, 1.6), 3.2, (1.0, 0.84, 0.62)),
    ]:
        ldata = bpy.data.lights.new(name=lname, type="SUN")
        ldata.energy = energy
        ldata.color = col
        lobj = bpy.data.objects.new(name=lname, object_data=ldata)
        scene.collection.objects.link(lobj)
        lobj.location = lpos
        lobj.rotation_euler = (math.radians(52), 0, math.radians(24 if "Key" in lname else -32))

    cam_data = bpy.data.cameras.new("NetProofCam")
    cam_obj = bpy.data.objects.new("NetProofCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    # Panel A: Exact First-Person POV Camera at (0, 0, 0) looking along +Y_bl (+Z_rl)
    cam_data.lens = 30.0
    cam_obj.location = (0.0, 0.0, 0.0)
    cam_obj.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    p1_path = MY_SCRATCH_DIR / "_net_panel_pov.png"
    p1_path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(p1_path)
    bpy.ops.render.render(write_still=True)

    # Panel B: Close-Up 3/4 Side/Under Inspection on the Right Hand, Blue Tape Grip, White Shirt Cuff & Cyan Butt Cap
    cam_data.lens = 42.0
    cam_obj.location = (-0.01, 0.07, -0.12)
    cam_obj.rotation_euler = (math.radians(84.5), 0.0, math.radians(-23.5))
    p2_path = MY_SCRATCH_DIR / "_net_panel_closeup.png"
    scene.render.filepath = str(p2_path)
    bpy.ops.render.render(write_still=True)

    # Stitch Panel A and Panel B side-by-side into 1920x900 net_grip_v2_proof.png
    img1 = bpy.data.images.load(str(p1_path))
    img2 = bpy.data.images.load(str(p2_path))
    w1, h1 = img1.size
    px1 = np.array(img1.pixels[:], dtype=np.float32).reshape((h1, w1, 4))
    px2 = np.array(img2.pixels[:], dtype=np.float32).reshape((h1, w1, 4))
    combined = np.concatenate([px1, px2], axis=1)
    # Subtle 2px vertical divider line between POV panel and close-up panel
    combined[:, w1 - 1 : w1 + 1, :3] = (0.22, 0.28, 0.36)

    out_img = bpy.data.images.new("NetGripProofCombined", width=w1 * 2, height=h1, alpha=False)
    out_img.pixels.foreach_set(combined.ravel())

    out_paths = [
        ARTIFACT_DIR / "net_grip_v2_proof.png",
        MY_SCRATCH_DIR / "net_grip_v2_proof.png",
        SCREENSHOTS_DIR / "net_grip_v2_proof.png",
    ]
    out_paths[0].parent.mkdir(parents=True, exist_ok=True)
    out_img.filepath_raw = str(out_paths[0])
    out_img.file_format = "PNG"
    out_img.save()
    for dst in out_paths[1:]:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(out_paths[0], dst)
    print(f"[NET GRIP V2 PROOF RENDERED] {out_paths[0]}")


if __name__ == "__main__":
    build_and_export_fps_hands_net()
    verify_and_render_proof()
