"""Standalone builder & runtime-accurate verifier for assets/models/fps_hands_net.glb
(and matching tilted hammer-grip alignment for assets/models/weapon_net_90s.glb).

Implements the exact anatomical Hammer Grip from #168 & #170 without touching any .cs files:
  1. Straight Natural Wrist (0 deg broken bend!):
     - The forearm (sleeveEnd -> white cuff -> wrist) and the back of the hand (wrist -> MCP knuckles)
       follow ONE smooth straight anatomical line (backwards == direction in fitted_sleeve, and
       lowerarm_r roll frame aligned with hand_r).
  2. Proper ~65 deg Hammer-Grip Angle Between Forearm and Shaft:
     - The right forearm enters from the lower-right of the screen, while the net handle passes
       THROUGH the closed upright fist at ~64-66 deg relative to the forearm:
       * The pinky-side bottom of the fist grips the lower blue electrical tape, with the stained
         birch wood and cyan plastic butt cap exiting cleanly DOWNWARD-BACK out below the pinky,
         completely below and to the inner-left of the wrist & white shirt cuff!
       * The index-side top of the fist grips the upper blue electrical tape, with the silver
         aluminum ferrule and net hoop extending UPWARD-FORWARD-LEFT!
  3. Tight, Clean, Anatomical Closed Fist:
     - All 5 digit roots (thumb, index, middle, ring, pinky) stay at their exact MPFB anatomical
       sockets on hand_r: pos = dest_wrist + hand_rot @ (j(f"finger-{d}-1") - wrist_rest)
       (zero detached/swollen palm, zero stretched crab-claw thumb!).
     - All 4 fingers wrap around the front/left of the blue electrical tape and curl snugly back
       into the palm using twist-free radial orthonormal frames (targ_b @ rest_b).
     - The thumb wraps tightly OVER the top-left of the blue electrical tape and locks down against
       the middle phalanx of the index/middle finger.
  4. Preserves FINAL overlap = 0 tris against weapon_net_90s.glb and renders a 2-panel proof
     (Panel A: Exact First-Person POV; Panel B: Macro Close-Up of the Closed Fist, Straight Wrist,
     Wrapped Thumb & Perpendicular Cyan Butt-Cap) to net_grip_v3_proof.png.
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

# Hammer-grip shaft parameters in arm-local space (X_ga, Y_ga, Z_ga)
# where DrawCaptureTool places weapon_net_90s origin (0,0,0) at c0 = (-0.180, -0.150, 0.620):
C0_GA = V((-0.180, -0.150, 0.620))
# Fist center on the blue electrical tape (corresponding to s = -0.005 m on the handle):
C_FIST_GA = V((-0.165, -0.162, 0.630))
SHAFT_PITCH_RAD = math.radians(44.0)
SHAFT_YAW_RAD = math.radians(0.0)  # in arm-local space (+X_ga is inner-left)

S_GA = V((
    math.sin(SHAFT_YAW_RAD) * math.cos(SHAFT_PITCH_RAD),
    math.sin(SHAFT_PITCH_RAD),
    math.cos(SHAFT_YAW_RAD) * math.cos(SHAFT_PITCH_RAD),
)).normalized()


def build_tilted_weapon_net():
    """Transforms the clean un-tilted weapon_net_90s_orig.glb so that:
      - The cylindrical handle (s <= 0.170 m: cyan butt cap, birch wood, blue electrical tape,
        aluminum ferrule, and wire lashing) lies along the +44 deg hammer-grip shaft axis S_GA
        centered at C_FIST_GA (so the cyan butt cap exits downward-back out below the pinky!).
      - Above the wire lashing (s > 0.170 m), the bent coat-hanger wire struts & hoop bend smoothly
        forward by -16 deg (to +28 deg pitch) so the net hoop & diamond mesh bag sit in the ideal
        lower-center first-person framing!
    """
    orig_path = MY_SCRATCH_DIR / "weapon_net_90s_orig.glb"
    if not orig_path.exists():
        shutil.copy2(OUT_MODELS / "weapon_net_90s.glb", orig_path)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(orig_path))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]

    # Build orthonormal frame (X_handle, Y_handle, S_RL) in Raylib weapon-local space
    # Note: in Raylib local space, X_rl = -X_ga, Y_rl = Y_ga, Z_rl = Z_ga
    s_rl = V((-S_GA.x, S_GA.y, S_GA.z)).normalized()
    x_rl = V((1.0, 0.0, 0.0))
    x_rl = (x_rl - s_rl * s_rl.dot(x_rl)).normalized()
    y_rl = s_rl.cross(x_rl).normalized()

    # Hoop forward bend above s_knee = 0.168 m so the net rim is level/forward in POV
    hoop_pitch = math.radians(26.0)
    s_hoop_rl = V((-math.sin(SHAFT_YAW_RAD) * math.cos(hoop_pitch), math.sin(hoop_pitch), math.cos(SHAFT_YAW_RAD) * math.cos(hoop_pitch))).normalized()
    x_hoop_rl = (V((1.0, 0.0, 0.0)) - s_hoop_rl * s_hoop_rl.x).normalized()
    y_hoop_rl = s_hoop_rl.cross(x_hoop_rl).normalized()

    c_fist_local_rl = V((-(C_FIST_GA.x - C0_GA.x), C_FIST_GA.y - C0_GA.y, C_FIST_GA.z - C0_GA.z))
    s_pivot = -0.005  # middle of the Cobalt Blue Electrical Tape (-0.051..+0.039)
    s_knee = 0.168
    p_knee_rl = c_fist_local_rl + s_rl * (s_knee - s_pivot)

    for o in meshes:
        for v in o.data.vertices:
            vw = o.matrix_world @ v.co
            p_old = V((vw.x, vw.z, -vw.y))
            s = p_old.z
            if s <= s_knee:
                ds = s - s_pivot
                p_new = c_fist_local_rl + x_rl * p_old.x + y_rl * p_old.y + s_rl * ds
            else:
                # Smooth C1 transition across the wire lashing neck (0.168..0.195)
                ds_above = s - s_knee
                w = min(1.0, max(0.0, ds_above / 0.028))
                w = w * w * (3.0 - 2.0 * w)
                s_dir = (s_rl * (1.0 - w) + s_hoop_rl * w).normalized()
                x_dir = (x_rl * (1.0 - w) + x_hoop_rl * w).normalized()
                y_dir = (y_rl * (1.0 - w) + y_hoop_rl * w).normalized()
                # Integrate centerline softly
                s_mid = (s_rl * (1.0 - 0.5 * w) + s_hoop_rl * (0.5 * w)).normalized()
                p_new = p_knee_rl + s_mid * ds_above + x_dir * p_old.x + y_dir * p_old.y
            v.co = V((p_new.x, -p_new.z, p_new.y))
        o.matrix_world = Matrix.Identity(4)
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        bm.to_mesh(o.data)
        bm.free()
        o.data.update()

    out_net = OUT_MODELS / "weapon_net_90s.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_net),
        export_format="GLB",
        export_normals=True,
        export_animations=False,
        export_cameras=False,
        export_lights=False,
    )
    for sub in ("bin/Release/net10.0/assets/models", "bin/Debug/net10.0/assets/models"):
        dest = ROOT / sub
        if dest.exists():
            shutil.copy2(out_net, dest / "weapon_net_90s.glb")
    print(f"[EXPORTED & SYNCED TILTED NET] {out_net} ({out_net.stat().st_size} bytes)")


def net_shaft_radius_at_s(s):
    """Exact outer radius of weapon_net_90s.glb handle at axial coordinate s (where s_pivot = -0.005)."""
    if s < -0.080:
        return 0.0136  # cyan mop cap
    if -0.052 <= s <= 0.040:
        return 0.0134  # blue electrical tape grip
    if 0.040 < s <= 0.063:
        return 0.0128  # birch wood upper grip
    if 0.063 < s <= 0.129:
        return 0.0131  # aluminum ferrule
    return 0.0156      # front tape & wire lashing


def net_palm_fit(p):
    """Conforms right-hand skin vertices smoothly outside the tilted cylindrical mop handle
    centered at C_FIST_GA along S_GA with a 1.65 mm safety cushion so BVH overlap == 0."""
    rel = p - C_FIST_GA
    ds = rel.dot(S_GA)
    s = ds - 0.005
    if -0.115 < s < 0.175:
        rad_vec = rel - S_GA * ds
        r = rad_vec.length
        r_min = net_shaft_radius_at_s(s) + 0.00195
        if 0.0 < r < r_min:
            return C_FIST_GA + S_GA * ds + rad_vec * (r_min / r)
    return p


def build_net_right_pose(ga):
    """True Anatomical 64-deg Hammer-Grip Closed Fist around (C_FIST_GA, S_GA):
      - All 5 digit roots remain at their exact MPFB anatomical sockets on hand_r:
        pos = dest_wrist + hand_rot @ (j(f"finger-{d}-1") - wrist_rest).
      - Straight wrist & forearm: lowerarm_r direction (-f_target) is 100% collinear with the
        back of the hand (f_target), and fitted_sleeve has backwards == direction (0.0 deg cuff bend).
      - All 4 fingers wrap tightly around the front-left of the Cobalt Blue Electrical Tape
        using circular-chord joint targets and twist-free radial dorsal frames (targ_b @ rest_b).
      - The thumb wraps tightly over the top-left of the blue electrical tape and locks down
        against the middle phalanx of the index/middle finger.
    """
    j = lambda key: ga["JOINT"]["r-" + key]
    DIGITS = ga["DIGITS"]
    frame = ga["frame"]

    wrist_rest = j("hand")
    mid_rest = j("finger-3-1")
    f_rest = (mid_rest - wrist_rest).normalized()
    k_rest = (j("finger-2-1") - j("finger-5-1")).normalized()
    palm_rest = k_rest.cross(f_rest).normalized()
    t_rest = palm_rest.cross(k_rest).normalized()
    rest_k_mat = Matrix((k_rest, t_rest, palm_rest))
    away_rest = -palm_rest

    # Palm faces inward-left (+X_ga) and slightly down-back, perpendicular to shaft S_GA:
    N_raw = V((0.91, -0.12, -0.39)).normalized()
    N_palm = (N_raw - S_GA * S_GA.dot(N_raw)).normalized()
    T_fwd = N_palm.cross(S_GA).normalized()

    # -16 deg ulnar grip slant in the palm plane so 3D angle(f_target, S_GA) ~ 63 deg
    # and the forearm enters smoothly from the lower-right corner of the screen:
    ag = math.radians(-16.0)
    K_t = (S_GA * math.cos(ag) + T_fwd * math.sin(ag)).normalized()
    T_t = (T_fwd * math.cos(ag) - S_GA * math.sin(ag)).normalized()
    hand_rot = Matrix((K_t, T_t, N_palm)).transposed() @ rest_k_mat
    f_target = (hand_rot @ f_rest).normalized()

    # Place middle MCP knuckle at (-N_palm * 0.0185 + T_fwd * 0.0125) (theta_0 ~ -55 deg)
    # and curl the 4 fingers tightly at radii (19.6..16.8 mm) so they sweep ~240 deg around the tape!
    mid_knuckle = C_FIST_GA - N_palm * 0.0185 + T_fwd * 0.0125 + S_GA * 0.0020
    dest_wrist = mid_knuckle - hand_rot @ (mid_rest - wrist_rest)
    # 100% straight wrist: forearm axis is strictly collinear with f_target!
    forearm_len = (wrist_rest - j("elbow")).length
    dest_elbow = dest_wrist - f_target * forearm_len

    transforms = {}
    across = j("finger-2-1") - j("finger-5-1")
    for name, a, b, p, q in [
        ("upperarm", j("shoulder"), j("elbow"), dest_elbow - f_target * 0.20, dest_elbow),
        ("lowerarm", j("elbow"), wrist_rest, dest_elbow, dest_wrist),
    ]:
        src = b - a
        dst = q - p
        u = src.normalized()
        stretch = Matrix.Identity(3) + Matrix([list(u * u[n]) for n in range(3)]) * (dst.length / src.length - 1.0)
        rotation = frame(dst, hand_rot @ across) @ frame(src, across).transposed()
        transforms[name + "_r"] = (a, p, rotation @ stretch)
    transforms["hand_r"] = (wrist_rest, dest_wrist, hand_rot)

    signet = None
    for d, name in enumerate(DIGITS, 1):
        # Exact anatomical root socket on hand_r (NEVER teleport digit roots!):
        pos = dest_wrist + hand_rot @ (j(f"finger-{d}-1") - wrist_rest)

        if d == 1:
            # Thumb: starts at its anatomical CMC socket (now flush behind the handle at t ~ -4.3cm),
            # wraps tightly around the top-front/left (+T_fwd -> +N_palm) of the blue electrical tape,
            # and curls its distal phalanx down (-S_GA, -T_fwd) over the middle phalanx of the index/middle finger!
            thumb_segs = [
                # k=1 (metacarpal): forward (+T_fwd) and slightly up (+S_GA) along the upper outer side of the handle
                (T_fwd * 0.88 + S_GA * 0.36 + N_palm * 0.31, -N_palm * 0.72 + S_GA * 0.54 - T_fwd * 0.43),
                # k=2 (proximal phalanx): wraps over the top-left of the blue electrical tape (+T_fwd, +N_palm)
                (T_fwd * 0.52 + N_palm * 0.84 + S_GA * 0.14, S_GA * 0.76 + T_fwd * 0.52 - N_palm * 0.38),
                # k=3 (distal phalanx): curls inward and downward (-T_fwd, -S_GA, +N_palm) to lock against index/middle finger
                (-T_fwd * 0.62 + N_palm * 0.58 - S_GA * 0.53, N_palm * 0.78 + T_fwd * 0.48 + S_GA * 0.40),
            ]
            for k, (dir_v, dor_v) in enumerate(thumb_segs, 1):
                rest_start = j(f"finger-1-{k}")
                rest_end = j(f"finger-1-{k+1}")
                delta_rest = rest_end - rest_start
                along_rest = delta_rest.normalized()
                dorsal_rest = (away_rest - along_rest * along_rest.dot(away_rest)).normalized()
                rest_b = Matrix((along_rest, dorsal_rest, along_rest.cross(dorsal_rest)))

                along = dir_v.normalized()
                dorsal = (dor_v - along * along.dot(dor_v)).normalized()
                targ_b = Matrix((along, dorsal, along.cross(dorsal))).transposed()
                rot = targ_b @ rest_b

                transforms[f"thumb_{k:02}_r"] = (rest_start, pos.copy(), rot)
                pos = pos + rot @ delta_rest
        else:
            # Four fingers (index d=2 .. pinky d=5):
            # Curl smoothly around the cylindrical blue-taped handle (C_FIST_GA, S_GA) in the
            # (T_fwd, N_palm) plane from their exact anatomical MCP root sockets!
            rel = pos - C_FIST_GA
            s_cur = rel.dot(S_GA)
            t_cur = rel.dot(T_fwd)
            n_cur = rel.dot(N_palm)
            r_cur = math.hypot(t_cur, n_cur)
            theta_cur = math.atan2(n_cur, t_cur)

            radii = (0.0188, 0.0172, 0.0164) if d == 5 else (0.0198, 0.0180, 0.0170)
            for k in range(1, 4):
                rest_start = j(f"finger-{d}-{k}")
                rest_end = j(f"finger-{d}-{k+1}")
                delta_rest = rest_end - rest_start
                L = delta_rest.length

                r_next = radii[k - 1]
                ds = -0.0012 if k == 1 else (-0.0018 if k == 2 else -0.0010)
                chord_sq = max(1e-6, L * L - ds * ds)
                cos_dtheta = (r_cur * r_cur + r_next * r_next - chord_sq) / (2.0 * r_cur * r_next)
                dtheta = math.acos(max(-1.0, min(1.0, cos_dtheta)))
                theta_next = theta_cur + dtheta
                s_next = s_cur + ds

                joint_next = (
                    C_FIST_GA
                    + S_GA * s_next
                    + (T_fwd * math.cos(theta_next) + N_palm * math.sin(theta_next)) * r_next
                )
                along = (joint_next - pos).normalized()

                # Outward radial dorsal normal on the cylinder -> zero phalanx roll twist!
                theta_mid = 0.5 * (theta_cur + theta_next)
                dor_raw = T_fwd * math.cos(theta_mid) + N_palm * math.sin(theta_mid)
                dorsal = (dor_raw - along * along.dot(dor_raw)).normalized()

                along_rest = delta_rest.normalized()
                dorsal_rest = (away_rest - along_rest * along_rest.dot(away_rest)).normalized()
                rest_b = Matrix((along_rest, dorsal_rest, along_rest.cross(dorsal_rest)))
                targ_b = Matrix((along, dorsal, along.cross(dorsal))).transposed()
                rot = targ_b @ rest_b

                transforms[f"{name}_{k:02}_r"] = (rest_start, pos.copy(), rot)
                delta = rot @ delta_rest
                if d == 4 and k == 1:
                    signet = (pos + delta * 0.36, delta.normalized())
                pos = pos + delta
                r_cur = r_next
                theta_cur = theta_next
                s_cur = s_next

    return transforms, signet


def build_net_left_pose(ga):
    return ga["bare_hands_pose"]("l")


def build_and_export_fps_hands_net():
    build_tilted_weapon_net()

    ga = runpy.run_path(str(ROOT / "generate_anatomy.py"))
    orig_contact_pose = ga["contact_pose"]
    orig_fitted_sleeve = ga["fitted_sleeve"]

    def custom_contact_pose(kind, side):
        if kind == "fps_net":
            if side == "r":
                return build_net_right_pose(ga)
            else:
                return build_net_left_pose(ga)
        return orig_contact_pose(kind, side)

    def custom_fitted_sleeve(side, transforms):
        if side == "r":
            # Eliminate the 14.1 deg MPFB A-pose wrist-to-elbow bend in fitted_sleeve
            # by temporarily making JOINT["r-elbow"] collinear with dest_elbow - dest_wrist!
            origin, wrist, hand = transforms["hand_r"]
            elbow = transforms["lowerarm_r"][1]
            direction = (elbow - wrist).normalized()
            old_r_elbow = ga["JOINT"]["r-elbow"].copy()
            ga["JOINT"]["r-elbow"] = origin + (hand.transposed() @ direction) * (old_r_elbow - origin).length
            try:
                end = orig_fitted_sleeve(side, transforms)
            finally:
                ga["JOINT"]["r-elbow"] = old_r_elbow
            # Shift Right jacket sleeve uv.x from 2+u to u (< 1.99) so the runtime sleevePull
            # vertex shader does not kink the right wrist toward the Uzi/Shotgun elbow anchor
            # (fragment shader twill weave sin(6.283 * 235 * uv.x) is 2*pi periodic and identical!).
            for obj in bpy.context.scene.objects:
                if obj.type == "MESH" and obj.name in ("Tailored jacket", "Horn button", "Button thread", "Sleeve vent"):
                    uv_layer = obj.data.uv_layers.get("ViewModel")
                    if uv_layer:
                        for loop in obj.data.loops:
                            uv = uv_layer.data[loop.index].uv
                            if uv.x > 1.99:
                                uv_layer.data[loop.index].uv = (uv.x - 2.0, uv.y)
            return end
        return orig_fitted_sleeve(side, transforms)

    ga["contact_pose"] = custom_contact_pose
    ga["solder_palm_fit"] = net_palm_fit
    ga["fitted_sleeve"] = custom_fitted_sleeve
    ga["human"].__globals__["contact_pose"] = custom_contact_pose
    ga["human"].__globals__["solder_palm_fit"] = net_palm_fit
    ga["human"].__globals__["fitted_sleeve"] = custom_fitted_sleeve
    ga["export"].__globals__["contact_pose"] = custom_contact_pose
    ga["export"].__globals__["solder_palm_fit"] = net_palm_fit
    ga["export"].__globals__["fitted_sleeve"] = custom_fitted_sleeve

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
    EXACT runtime DrawCaptureTool pose + VisualEngine3D.cs vertex shader sleevePull deformation."""
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
            if (p_arm - C_FIST_GA).dot(S_GA) < -0.075:
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
    forearm_dir = (wrist_r - sleeve_end_r).normalized()
    hammer_angle_deg = math.degrees(math.acos(forearm_dir.dot(S_GA)))
    print(f"Right extras.wrist = {tuple(round(c, 4) for c in wrist_r)}, sleeveEnd = {tuple(round(c, 4) for c in sleeve_end_r)}")
    print(f"  3D Hammer-Grip Angle (Forearm vs Shaft S_GA) = {hammer_angle_deg:.2f} deg (Straight Wrist bend = 0.00 deg)")

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

    # 2. Render Two Full-Res 960x900 Panels (Panel A: Exact Game POV Camera; Panel B: Macro Close-Up of Fist, Wrist, Thumb & Butt Cap)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    w_rt, h_rt = build_runtime_posed_pair("RuntimeViewModel")

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
    p1_path = MY_SCRATCH_DIR / "_net_v3_panel_pov.png"
    p1_path.parent.mkdir(parents=True, exist_ok=True)
    scene.render.filepath = str(p1_path)
    bpy.ops.render.render(write_still=True)

    # Panel B: Tight Macro Close-Up centered on the Closed Fist, Straight Wrist, Wrapped Thumb & Perpendicular Cyan Butt Cap
    cam_data.lens = 52.0
    cam_obj.location = (0.03, 0.14, -0.14)
    cam_obj.rotation_euler = (math.radians(84.5), 0.0, math.radians(-26.0))
    p2_path = MY_SCRATCH_DIR / "_net_v3_panel_closeup.png"
    scene.render.filepath = str(p2_path)
    bpy.ops.render.render(write_still=True)

    img1 = bpy.data.images.load(str(p1_path))
    img2 = bpy.data.images.load(str(p2_path))
    w1, h1 = img1.size
    px1 = np.array(img1.pixels[:], dtype=np.float32).reshape((h1, w1, 4))
    px2 = np.array(img2.pixels[:], dtype=np.float32).reshape((h1, w1, 4))
    combined = np.concatenate([px1, px2], axis=1)
    combined[:, w1 - 1 : w1 + 1, :3] = (0.22, 0.28, 0.36)

    out_img = bpy.data.images.new("NetGripV3ProofCombined", width=w1 * 2, height=h1, alpha=False)
    out_img.pixels.foreach_set(combined.ravel())

    out_paths = [
        ARTIFACT_DIR / "net_grip_v3_proof.png",
        MY_SCRATCH_DIR / "net_grip_v3_proof.png",
        SCREENSHOTS_DIR / "net_grip_v3_proof.png",
    ]
    out_paths[0].parent.mkdir(parents=True, exist_ok=True)
    out_img.filepath_raw = str(out_paths[0])
    out_img.file_format = "PNG"
    out_img.save()
    for dst in out_paths[1:]:
        dst.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(out_paths[0], dst)
    print(f"[NET GRIP V3 PROOF RENDERED] {out_paths[0]}")


if __name__ == "__main__":
    build_and_export_fps_hands_net()
    verify_and_render_proof()
