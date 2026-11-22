import bpy
import bmesh
import math
import os
import json
import struct
import numpy as np
from mathutils import Vector, Matrix, Euler, Quaternion
from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.targetservice import TargetService
from bl_ext.blender_org.mpfb.services.locationservice import LocationService

PROJ = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
OUT_GLB = os.path.join(PROJ, "assets", "models", "char_partner_90s.glb")
TEX_DIR = os.path.join(PROJ, "scratch", "hero_v2_textures")
os.makedirs(TEX_DIR, exist_ok=True)


# =============================================================================
# UTILITY HELPERS
# =============================================================================
def set_smooth(obj):
    mesh = obj.data
    n = len(mesh.polygons)
    if n > 0:
        mesh.polygons.foreach_set("use_smooth", [True] * n)
    mesh.update()


def apply_all_modifiers(obj):
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    for mod in list(obj.modifiers):
        try:
            bpy.ops.object.modifier_apply(modifier=mod.name)
        except Exception as e:
            print(f"WARN modifier_apply {obj.name}/{mod.name}: {e}")
    obj.select_set(False)


def add_bevel_and_apply(obj, width=0.0016, segments=1):
    mod = obj.modifiers.new("Bevel", 'BEVEL')
    mod.width = width
    mod.segments = segments
    mod.limit_method = 'ANGLE'
    mod.angle_limit = math.radians(34.0)
    apply_all_modifiers(obj)


def rotate_bone_world(rig, bone_name, world_axis, angle_deg):
    pb = rig.pose.bones.get(bone_name)
    if not pb:
        return
    pb.rotation_mode = 'QUATERNION'
    m3 = pb.matrix.to_3x3()
    local_axis = (m3.inverted() @ Vector(world_axis).normalized()).normalized()
    q_delta = Quaternion(local_axis, math.radians(angle_deg))
    pb.rotation_quaternion = pb.rotation_quaternion @ q_delta
    bpy.context.view_layer.update()


def rotate_bone_local(rig, bone_name, local_axis, angle_deg):
    pb = rig.pose.bones.get(bone_name)
    if not pb:
        return
    pb.rotation_mode = 'QUATERNION'
    q_delta = Quaternion(Vector(local_axis).normalized(), math.radians(angle_deg))
    pb.rotation_quaternion = pb.rotation_quaternion @ q_delta
    bpy.context.view_layer.update()


def make_material(name, base_factor, roughness=0.55, metallic=0.0, img=None, specular_ior=None):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if specular_ior is not None and "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = specular_ior
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])

    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = img
        if tuple(round(c, 4) for c in base_factor[:3]) != (1.0, 1.0, 1.0):
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = 'RGBA'
            mix.blend_type = 'MULTIPLY'
            mix.inputs["Factor"].default_value = 1.0
            mix.inputs["B"].default_value = (base_factor[0], base_factor[1], base_factor[2], 1.0)
            nt.links.new(tex.outputs["Color"], mix.inputs["A"])
            nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
        else:
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    else:
        bsdf.inputs["Base Color"].default_value = (base_factor[0], base_factor[1], base_factor[2], 1.0)
    return mat


def assign_mat(obj, mat):
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def create_loft_mesh(name, rings, closed_v=False, cap_start=False, cap_end=False):
    mesh = bpy.data.meshes.new(name)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bm_rings = [[bm.verts.new(p) for p in ring] for ring in rings]
    bm.verts.ensure_lookup_table()
    nr = len(bm_rings)
    ns = len(bm_rings[0])
    for r in range(nr - 1):
        r0 = bm_rings[r]
        r1 = bm_rings[r + 1]
        for s in range(ns):
            s_next = (s + 1) % ns
            if not closed_v and s == ns - 1:
                continue
            try:
                bm.faces.new((r0[s], r0[s_next], r1[s_next], r1[s]))
            except ValueError:
                pass
    if cap_start and closed_v:
        vc = bm.verts.new(sum(rings[0], Vector()) / ns)
        for s in range(ns):
            sn = (s + 1) % ns
            try:
                bm.faces.new((bm_rings[0][sn], bm_rings[0][s], vc))
            except ValueError:
                pass
    if cap_end and closed_v:
        vc = bm.verts.new(sum(rings[-1], Vector()) / ns)
        for s in range(ns):
            sn = (s + 1) % ns
            try:
                bm.faces.new((bm_rings[-1][s], bm_rings[-1][sn], vc))
            except ValueError:
                pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    set_smooth(obj)
    return obj


def sample_bone_path(points, radii_xy, segs=20, folds_fn=None):
    rings = []
    n = len(points)
    for i in range(n):
        p = Vector(points[i])
        if i == 0:
            t = (Vector(points[1]) - p).normalized()
        elif i == n - 1:
            t = (p - Vector(points[n - 2])).normalized()
        else:
            t = (Vector(points[i + 1]) - Vector(points[i - 1])).normalized()
        ref = Vector((0.0, -1.0, 0.0))
        if abs(t.dot(ref)) > 0.92:
            ref = Vector((1.0, 0.0, 0.0))
        right = t.cross(ref).normalized()
        front = right.cross(t).normalized()
        if front.y > 0 and abs(t.z) > 0.3:
            front = -front
            right = -right
        rx, ry = radii_xy[i]
        ring = []
        u = i / max(1, n - 1)
        for s in range(segs):
            ang = 2.0 * math.pi * s / segs
            ca, sa = math.cos(ang), math.sin(ang)
            dr = folds_fn(u, ang, p) if folds_fn is not None else 0.0
            pt = p + right * (ca * (rx + dr)) + front * (sa * (ry + dr))
            ring.append(pt)
        rings.append(ring)
    return rings


# =============================================================================
# STEP 1: CREATE, SCULPT & POSE MPFB 2 ATHLETIC 1990s PROTAGONIST
# =============================================================================
def build_mpfb_hero():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    macro = TargetService.get_default_macro_info_dict()
    macro.update({
        "gender": 1.0,
        "age": 0.56,
        "muscle": 0.72,
        "weight": 0.46,
        "proportions": 0.60,
        "height": 0.60,
        "race": {"caucasian": 0.88, "asian": 0.08, "african": 0.04},
    })
    human = HumanService.create_human(
        mask_helpers=True,
        detailed_helpers=True,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=macro,
    )

    tdir = LocationService.get_mpfb_data("targets")
    targets = [
        ("head/head-rectangular.target.gz", 0.42),
        ("head/head-fat-decr.target.gz", 0.58),
        ("chin/chin-bones-incr.target.gz", 0.62),
        ("chin/chin-prominent-incr.target.gz", 0.48),
        ("chin/chin-jaw-drop-incr.target.gz", 0.16),
        ("cheek/l-cheek-bones-incr.target.gz", 0.72),
        ("cheek/r-cheek-bones-incr.target.gz", 0.72),
        ("cheek/l-cheek-inner-decr.target.gz", 0.46),
        ("cheek/r-cheek-inner-decr.target.gz", 0.46),
        ("eyebrows/eyebrows-trans-forward.target.gz", 0.65),
        ("eyebrows/eyebrows-angle-down.target.gz", 0.30),
        ("nose/nose-greek-incr.target.gz", 0.36),
        ("nose/nose-compression-compress.target.gz", 0.24),
        ("mouth/mouth-angles-up.target.gz", 0.22),
        ("torso/measure-shoulder-dist-incr.target.gz", 0.55),
    ]
    for rel, val in targets:
        full = os.path.join(tdir, rel.replace("/", os.sep))
        if os.path.exists(full):
            try:
                TargetService.load_target(human, full, weight=val)
            except Exception as e:
                print(f"WARN target {rel}: {e}")

    rig = HumanService.add_builtin_rig(human, "default")
    bpy.context.view_layer.update()

    # Pose into confident standing 1998 turnaround pose (arms hanging naturally down, slight elbow bend, relaxed fingers)
    rotate_bone_world(rig, "upperleg01.L", (0, 1, 0), +4.6)
    rotate_bone_world(rig, "upperleg01.R", (0, 1, 0), -4.6)
    rotate_bone_world(rig, "lowerleg01.L", (0, 1, 0), -2.2)
    rotate_bone_world(rig, "lowerleg01.R", (0, 1, 0), +2.2)
    rotate_bone_world(rig, "foot.L", (0, 0, 1), -5.0)
    rotate_bone_world(rig, "foot.R", (0, 0, 1), +5.0)

    rotate_bone_world(rig, "spine03", (1, 0, 0), +1.8)
    rotate_bone_world(rig, "neck01", (1, 0, 0), -1.5)

    # Arms naturally down clearing blazer hips (~11 deg off vertical)
    rotate_bone_world(rig, "clavicle.L", (0, 1, 0), +3.5)
    rotate_bone_world(rig, "clavicle.R", (0, 1, 0), -3.5)
    rotate_bone_world(rig, "upperarm01.L", (0, 1, 0), +26.5)
    rotate_bone_world(rig, "upperarm01.R", (0, 1, 0), -26.5)
    rotate_bone_world(rig, "upperarm01.L", (1, 0, 0), +3.5)
    rotate_bone_world(rig, "upperarm01.R", (1, 0, 0), +3.5)

    # Relaxed athletic elbow bend (~15 deg forward)
    rotate_bone_world(rig, "lowerarm01.L", (1, 0, 0), +37.0)
    rotate_bone_world(rig, "lowerarm01.R", (1, 0, 0), +37.0)
    rotate_bone_world(rig, "lowerarm01.L", (0, 1, 0), -4.5)
    rotate_bone_world(rig, "lowerarm01.R", (0, 1, 0), +4.5)

    # Palms facing naturally inward toward thighs
    rotate_bone_local(rig, "lowerarm02.L", (0, 1, 0), +14.0)
    rotate_bone_local(rig, "lowerarm02.R", (0, 1, 0), -14.0)
    rotate_bone_world(rig, "wrist.L", (0, 1, 0), -4.0)
    rotate_bone_world(rig, "wrist.R", (0, 1, 0), +4.0)

    # Natural masculine 5-fingered curl
    finger_curls = {
        2: (14.0, 20.0, 14.0),
        3: (18.0, 26.0, 18.0),
        4: (22.0, 31.0, 21.0),
        5: (27.0, 35.0, 25.0),
    }
    for side in ("L", "R"):
        for fi, (c1, c2, c3) in finger_curls.items():
            rotate_bone_local(rig, f"finger{fi}-1.{side}", (1, 0, 0), c1)
            rotate_bone_local(rig, f"finger{fi}-2.{side}", (1, 0, 0), c2)
            rotate_bone_local(rig, f"finger{fi}-3.{side}", (1, 0, 0), c3)
        sgn = 1.0 if side == "L" else -1.0
        rotate_bone_local(rig, f"finger1-1.{side}", (1, 0, 0), 10.0)
        rotate_bone_local(rig, f"finger1-2.{side}", (1, 0, 0), 14.0)
        rotate_bone_local(rig, f"finger1-3.{side}", (1, 0, 0), 12.0)
        rotate_bone_local(rig, f"metacarpal1.{side}", (0, 0, 1), sgn * 8.0)

    bpy.context.view_layer.update()

    # IMPORTANT: Bake evaluated posed + target-sculpted vertices cleanly via bmesh (ZERO shape-key reversion!).
    # Because mask_helpers=True already applies 'Hide helpers' MASK modifier, ev.to_mesh() is 100% pure body geometry!
    deps = bpy.context.evaluated_depsgraph_get()
    ev = human.evaluated_get(deps)
    tmp_mesh = ev.to_mesh()
    bm_baked = bmesh.new()
    bm_baked.from_mesh(tmp_mesh)
    ev.to_mesh_clear()
    body_mesh = bpy.data.meshes.new("HeroBodyFull")
    bm_baked.to_mesh(body_mesh)
    bm_baked.free()
    body_obj = bpy.data.objects.new("HeroBodyFull", body_mesh)
    bpy.context.scene.collection.objects.link(body_obj)

    joint_names = [
        "root", "pelvis.L", "pelvis.R", "spine01", "spine02", "spine03", "spine04", "spine05",
        "neck01", "head", "eye.L", "eye.R",
        "clavicle.L", "shoulder01.L", "upperarm01.L", "upperarm02.L", "lowerarm01.L", "lowerarm02.L", "wrist.L",
        "clavicle.R", "shoulder01.R", "upperarm01.R", "upperarm02.R", "lowerarm01.R", "lowerarm02.R", "wrist.R",
        "finger4-1.R", "finger4-2.R", "finger4-1.L", "finger2-1.L", "finger2-1.R",
        "upperleg01.L", "upperleg02.L", "lowerleg01.L", "lowerleg02.L", "foot.L", "toe1-1.L",
        "upperleg01.R", "upperleg02.R", "lowerleg01.R", "lowerleg02.R", "foot.R", "toe1-1.R",
    ]
    joints = {}
    for jn in joint_names:
        pb = rig.pose.bones.get(jn)
        if pb:
            h_w = (rig.matrix_world @ pb.head).copy()
            t_w = (rig.matrix_world @ pb.tail).copy()
            joints[jn] = (h_w, t_w, (rig.matrix_world.to_3x3() @ pb.matrix.to_3x3()).copy())

    bpy.data.objects.remove(human, do_unlink=True)
    bpy.data.objects.remove(rig, do_unlink=True)

    # Normalize height & center Y cleanly around skull/torso axis!
    # Target: shoes flat at Z=0.000, barefoot feet at Z=0.018, skull top at Z=1.694 -> sculpted hair top = 1.720m!
    zs = [v.co.z for v in body_obj.data.vertices]
    xs = [v.co.x for v in body_obj.data.vertices]
    raw_min_z, raw_max_z = min(zs), max(zs)
    raw_h = raw_max_z - raw_min_z
    scale_f = 1.676 / raw_h
    cx = (min(xs) + max(xs)) * 0.5

    # Compute skull center Y (using vertices in upper head) so head sits centered at Y = 0.000 (glTF Z = 0.000)!
    raw_head_v = [v.co for v in body_obj.data.vertices if v.co.z > raw_max_z - 0.16]
    head_cy_raw = (min(p.y for p in raw_head_v) + max(p.y for p in raw_head_v)) * 0.5

    def xform_pt(p):
        return Vector((
            (p.x - cx) * scale_f,
            (p.y - head_cy_raw) * scale_f,
            (p.z - raw_min_z) * scale_f + 0.018,
        ))

    for v in body_obj.data.vertices:
        v.co = xform_pt(v.co)
    body_obj.data.update()

    for jn in list(joints.keys()):
        h_w, t_w, rot_m = joints[jn]
        joints[jn] = (xform_pt(h_w), xform_pt(t_w), rot_m)

    # Subtle stylized head proportion boost (+6.5% width/depth around neck pivot -> heroic TF2/Disco Elysium silhouette + perfect hat fit!)
    neck_pivot = joints["neck01"][0].copy()
    head_boost = 1.07
    for v in body_obj.data.vertices:
        if v.co.z > neck_pivot.z - 0.03:
            w = max(0.0, min(1.0, (v.co.z - (neck_pivot.z - 0.03)) / 0.065))
            w = w * w * (3.0 - 2.0 * w)
            d = v.co - neck_pivot
            v.co = neck_pivot + Vector((
                d.x * (1.0 + (head_boost - 1.0) * w),
                d.y * (1.0 + (head_boost - 1.0) * 0.85 * w),
                d.z * (1.0 + (head_boost - 1.0) * 0.72 * w),
            ))
    for jn in ("head", "eye.L", "eye.R"):
        if jn in joints:
            h_w, t_w, rot_m = joints[jn]
            out_pts = []
            for pt in (h_w, t_w):
                d = pt - neck_pivot
                out_pts.append(neck_pivot + Vector((
                    d.x * head_boost,
                    d.y * (1.0 + (head_boost - 1.0) * 0.85),
                    d.z * (1.0 + (head_boost - 1.0) * 0.72),
                )))
            joints[jn] = (out_pts[0], out_pts[1], rot_m)

    # Add charismatic 1998 Moscow protagonist asymmetric smirk + confident slightly hooded eyelids!
    eye_l = joints["eye.L"][0]
    eye_r = joints["eye.R"][0]
    eye_z = (eye_l.z + eye_r.z) * 0.5
    eye_x = (abs(eye_l.x) + abs(eye_r.x)) * 0.5
    mouth_z = eye_z - 0.067
    mouth_y = eye_l.y - 0.008
    for v in body_obj.data.vertices:
        if v.co.z > 1.44 and v.co.y < -0.02:
            # 1) Confident slightly hooded upper eyelids (lowers upper lid ~2.2mm so gaze is cool & sharp, NOT wide-eyed!)
            for sgn in (1.0, -1.0):
                de_u = Vector(((v.co.x - sgn * eye_x) * 0.72, (v.co.y - (eye_l.y - 0.010)) * 1.1, (v.co.z - (eye_z + 0.0065)) * 1.4))
                ru = de_u.length
                if ru < 0.015:
                    fu = math.cos(ru / 0.015 * math.pi * 0.5) ** 2
                    v.co.z -= 0.0024 * fu
                    v.co.y -= 0.0012 * fu
                # Subtle confident lower-lid squint lift (+0.9mm)
                de_l = Vector(((v.co.x - sgn * eye_x) * 0.78, (v.co.y - (eye_l.y - 0.010)) * 1.1, (v.co.z - (eye_z - 0.0065)) * 1.5))
                rl = de_l.length
                if rl < 0.013:
                    fl = math.cos(rl / 0.013 * math.pi * 0.5) ** 2
                    v.co.z += 0.0010 * fl
            # 2) Lift character's left mouth corner (+X) into confident 1998 smirk
            dm = Vector((v.co.x - 0.025, (v.co.y - mouth_y) * 1.4, (v.co.z - mouth_z) * 1.5))
            r2 = dm.length
            if r2 < 0.028:
                f = math.cos(r2 / 0.028 * math.pi * 0.5) ** 2
                v.co.z += 0.0056 * f
                v.co.y += 0.0020 * f
                v.co.x += 0.0018 * f
            # 3) Subtle chiseling of jawline angles
            if 1.45 < v.co.z < 1.52 and abs(v.co.x) > 0.035 and -0.06 < v.co.y < 0.03:
                v.co.x *= 1.028

    body_obj.data.update()
    set_smooth(body_obj)

    head_v = [v.co for v in body_obj.data.vertices if v.co.z > 1.43]
    print(f"INTACT HEAD (Blender): Z=[{min(p.z for p in head_v):.4f}..{max(p.z for p in head_v):.4f}] "
          f"X=[{min(p.x for p in head_v):.4f},{max(p.x for p in head_v):.4f}] "
          f"Y=[{min(p.y for p in head_v):.4f},{max(p.y for p in head_v):.4f}]")
    print(f"EYES (Blender): L={tuple(round(v,4) for v in joints['eye.L'][0])} R={tuple(round(v,4) for v in joints['eye.R'][0])}")
    print(f"WRISTS (Blender): L={tuple(round(v,4) for v in joints['wrist.L'][0])} R={tuple(round(v,4) for v in joints['wrist.R'][0])}")
    print(f"FEET (Blender): L={tuple(round(v,4) for v in joints['foot.L'][0])} R={tuple(round(v,4) for v in joints['foot.R'][0])}")

    return body_obj, joints


# =============================================================================
# STEP 2: TRIM HIDDEN BODY SKIN (KEEP ONLY HEAD/NECK + 5-FINGERED HANDS!)
# =============================================================================
def extract_visible_skin(body_obj, joints):
    skin_mesh = body_obj.data.copy()
    skin_mesh.name = "HeroSkin"
    skin_obj = bpy.data.objects.new("HeroSkin", skin_mesh)
    bpy.context.scene.collection.objects.link(skin_obj)

    wrist_l = joints["wrist.L"][0]
    wrist_r = joints["wrist.R"][0]
    elbow_l = joints["lowerarm01.L"][0]
    elbow_r = joints["lowerarm01.R"][0]
    # Measure TRUE geometric center of the anatomical neck (not the cervical spine bone behind it!)
    neck_slice = [v.co for v in body_obj.data.vertices if 1.455 <= v.co.z <= 1.495 and abs(v.co.x) < 0.085]
    neck_cy = (min(p.y for p in neck_slice) + max(p.y for p in neck_slice)) * 0.5 if neck_slice else joints["neck01"][0].y
    fore_l = (wrist_l - elbow_l).normalized()
    fore_r = (wrist_r - elbow_r).normalized()

    bm = bmesh.new()
    bm.from_mesh(skin_obj.data)
    bm.verts.ensure_lookup_table()
    to_del = []
    for v in bm.verts:
        p = v.co
        # Keep Full Head & Ears strictly above neck/jaw transition (Z >= 1.486)
        if p.z >= 1.486 and abs(p.x) < 0.135:
            continue
        # Keep COMPLETE intact anatomical Neck cylinder (front throat + sides + back!) centered on TRUE neck_cy down to Z=1.380 inside shirt collar!
        if 1.380 <= p.z < 1.486:
            r_neck = math.hypot(p.x, p.y - neck_cy)
            # Slightly narrower cut at lower base (Z=1.38..1.44) so trapezius wings never poke out, full 6.4cm radius around upper throat (Z=1.44..1.486)!
            r_lim = 0.064 if p.z >= 1.440 else 0.058
            if r_neck < r_lim:
                continue
        # Keep Left 5-fingered Hand + Wrist ONLY (abs(x) > 0.215 guarantees ZERO thigh/hip quads ever!)
        if p.x > 0.215 and (p - wrist_l).length < 0.185:
            if (p - wrist_l).dot(fore_l) >= -0.036:
                continue
        # Keep Right 5-fingered Hand + Wrist ONLY
        if p.x < -0.215 and (p - wrist_r).length < 0.185:
            if (p - wrist_r).dot(fore_r) >= -0.036:
                continue
        to_del.append(v)

    bmesh.ops.delete(bm, geom=to_del, context='VERTS')
    bm.to_mesh(skin_obj.data)
    bm.free()
    set_smooth(skin_obj)

    # Add a smooth solid inner anatomical neck column (Z=1.380..1.486) tucked flush inside the MPFB neck & white shirt collar
    # so ZERO hollow throat gap can ever appear from any camera angle!
    nc_rings = []
    for k in range(6):
        t = k / 5.0
        zn = 1.380 + t * 0.106
        rn = 0.052 - t * 0.002
        nc_rings.append([Vector((math.cos(2 * math.pi * s / 20) * rn, neck_cy + math.sin(2 * math.pi * s / 20) * (rn * 0.96), zn)) for s in range(20)])
    neck_core = create_loft_mesh("HeroNeckCore", nc_rings, closed_v=True, cap_start=True, cap_end=True)
    skin_obj = join_objects([skin_obj, neck_core], "HeroSkin")
    set_smooth(skin_obj)
    return skin_obj


# =============================================================================
# STEP 3: SCULPTED 3D EYES (SCLERA, SEPARATE IRIS, PUPIL, CATCHLIGHT, LASHES, BROWS)
# =============================================================================
def build_eyes_and_brows(body_obj, joints):
    eye_l = joints["eye.L"][0]
    eye_r = joints["eye.R"][0]
    objs = {}

    # Measure eyelid rim depth right around eye_l/eye_r on the intact face mesh!
    def find_eye_center(eye_pt):
        near = [v.co for v in body_obj.data.vertices
                if abs(v.co.x - eye_pt.x) < 0.014 and abs(v.co.z - eye_pt.z) < 0.009 and v.co.y < -0.05]
        if near:
            front_y = min(p.y for p in near)
            cz = (min(p.z for p in near) + max(p.z for p in near)) * 0.5
            # Seat eyeball deeply INSIDE eyelid socket (zero bug-eyed protrusion!)
            return Vector((eye_pt.x, front_y + 0.0148, cz - 0.0006))
        return eye_pt.copy()

    ec_l = find_eye_center(eye_l)
    ec_r = find_eye_center(eye_r)
    ex = (abs(ec_l.x) + abs(ec_r.x)) * 0.5
    ey = (ec_l.y + ec_r.y) * 0.5
    ez = (ec_l.z + ec_r.z) * 0.5
    eye_centers = [Vector((+ex, ey, ez)), Vector((-ex, ey, ez))]
    eye_r_rad = 0.0114

    # 1) Sclera eyeballs
    sclera_bm = bmesh.new()
    for ec in eye_centers:
        bmesh.ops.create_uvsphere(sclera_bm, u_segments=20, v_segments=12, radius=eye_r_rad,
                                  matrix=Matrix.Translation(ec))
    m_scl = bpy.data.meshes.new("EyesSclera")
    sclera_bm.to_mesh(m_scl)
    sclera_bm.free()
    o_scl = bpy.data.objects.new("EyesSclera", m_scl)
    bpy.context.scene.collection.objects.link(o_scl)
    set_smooth(o_scl)
    objs["sclera"] = o_scl

    # 2) Separate Iris discs (curved corneal domes with crisp radial UVs!)
    iris_bm = bmesh.new()
    uv_layer = iris_bm.loops.layers.uv.new("UVMap")
    iris_rad = 0.0061
    for ec in eye_centers:
        center_v = iris_bm.verts.new(ec + Vector((0, -(eye_r_rad + 0.00045), 0)))
        rings = []
        for rf in (0.55, 1.0):
            r = iris_rad * rf
            y_off = -math.sqrt(max(0.00001, (eye_r_rad + 0.00045) ** 2 - r * r))
            ring = [iris_bm.verts.new(ec + Vector((math.cos(2 * math.pi * s / 20) * r, y_off,
                                                   math.sin(2 * math.pi * s / 20) * r))) for s in range(20)]
            rings.append(ring)
        for s in range(20):
            sn = (s + 1) % 20
            a0, a1 = 2 * math.pi * s / 20, 2 * math.pi * sn / 20
            f = iris_bm.faces.new((center_v, rings[0][sn], rings[0][s]))
            f.loops[0][uv_layer].uv = (0.5, 0.5)
            f.loops[1][uv_layer].uv = (0.5 + 0.27 * math.cos(a1), 0.5 + 0.27 * math.sin(a1))
            f.loops[2][uv_layer].uv = (0.5 + 0.27 * math.cos(a0), 0.5 + 0.27 * math.sin(a0))
            f2 = iris_bm.faces.new((rings[0][s], rings[0][sn], rings[1][sn], rings[1][s]))
            f2.loops[0][uv_layer].uv = (0.5 + 0.27 * math.cos(a0), 0.5 + 0.27 * math.sin(a0))
            f2.loops[1][uv_layer].uv = (0.5 + 0.27 * math.cos(a1), 0.5 + 0.27 * math.sin(a1))
            f2.loops[2][uv_layer].uv = (0.5 + 0.49 * math.cos(a1), 0.5 + 0.49 * math.sin(a1))
            f2.loops[3][uv_layer].uv = (0.5 + 0.49 * math.cos(a0), 0.5 + 0.49 * math.sin(a0))
    bmesh.ops.recalc_face_normals(iris_bm, faces=iris_bm.faces)
    m_iris = bpy.data.meshes.new("EyesIris")
    iris_bm.to_mesh(m_iris)
    iris_bm.free()
    o_iris = bpy.data.objects.new("EyesIris", m_iris)
    bpy.context.scene.collection.objects.link(o_iris)
    set_smooth(o_iris)
    objs["iris"] = o_iris

    # 3) Pupils & Catchlights
    pup_bm = bmesh.new()
    glint_bm = bmesh.new()
    for ec in eye_centers:
        bmesh.ops.create_uvsphere(pup_bm, u_segments=12, v_segments=8, radius=1.0,
                                  matrix=Matrix.Translation(ec + Vector((0, -(eye_r_rad + 0.0005), 0)))
                                  @ Matrix.Diagonal((0.0029, 0.0010, 0.0029, 1.0)))
        bmesh.ops.create_uvsphere(glint_bm, u_segments=8, v_segments=6, radius=1.0,
                                  matrix=Matrix.Translation(ec + Vector((+0.0018, -(eye_r_rad + 0.0011), +0.0020)))
                                  @ Matrix.Diagonal((0.0011, 0.0006, 0.0011, 1.0)))
    m_pup = bpy.data.meshes.new("EyesPupil")
    pup_bm.to_mesh(m_pup)
    pup_bm.free()
    o_pup = bpy.data.objects.new("EyesPupil", m_pup)
    bpy.context.scene.collection.objects.link(o_pup)
    set_smooth(o_pup)
    objs["pupil"] = o_pup

    m_gl = bpy.data.meshes.new("EyesCatchlight")
    glint_bm.to_mesh(m_gl)
    glint_bm.free()
    o_gl = bpy.data.objects.new("EyesCatchlight", m_gl)
    bpy.context.scene.collection.objects.link(o_gl)
    set_smooth(o_gl)
    objs["catchlight"] = o_gl

    # 4) Expressive Thick Arched Eyebrows & Upper Eyelid Lashline projected directly onto intact brow ridge!
    face_pts = np.array([[v.co.x, v.co.y, v.co.z] for v in body_obj.data.vertices
                         if v.co.y < -0.02 and v.co.z > 1.48], dtype=np.float32)

    def project_to_face(x, z, y_offset=-0.0024):
        d2 = (face_pts[:, 0] - x) ** 2 + (face_pts[:, 2] - z) ** 2
        idx = int(np.argmin(d2))
        return Vector((x, float(face_pts[idx, 1]) + y_offset, z))

    brow_objs = []
    for side in (1.0, -1.0):
        # Confident low-set sculpted masculine 1990s brows matching reference (slightly arched on left +X!)
        arch_extra = 0.0024 if side > 0 else 0.0
        pts = [
            project_to_face(side * 0.010, ez + 0.0125, -0.0028),
            project_to_face(side * 0.022, ez + 0.0158 + arch_extra * 0.4, -0.0032),
            project_to_face(side * 0.036, ez + 0.0185 + arch_extra, -0.0030),
            project_to_face(side * 0.048, ez + 0.0168 + arch_extra * 0.5, -0.0024),
            project_to_face(side * 0.057, ez + 0.0128, -0.0015),
        ]
        radii = [(0.0030, 0.0040), (0.0038, 0.0048), (0.0036, 0.0044), (0.0028, 0.0034), (0.0012, 0.0014)]
        b_obj = create_loft_mesh(f"Brow_{'L' if side > 0 else 'R'}", sample_bone_path(pts, radii, segs=8),
                                 closed_v=True, cap_start=True, cap_end=True)
        brow_objs.append(b_obj)

        lpts = [
            project_to_face(side * (ex - 0.012), ez + 0.0012, -0.0010),
            project_to_face(side * (ex - 0.004), ez + 0.0042, -0.0014),
            project_to_face(side * (ex + 0.004), ez + 0.0044, -0.0014),
            project_to_face(side * (ex + 0.012), ez + 0.0016, -0.0010),
        ]
        lradii = [(0.0010, 0.0013), (0.0016, 0.0019), (0.0016, 0.0019), (0.0010, 0.0013)]
        l_obj = create_loft_mesh(f"Lash_{'L' if side > 0 else 'R'}", sample_bone_path(lpts, lradii, segs=6),
                                 closed_v=True, cap_start=True, cap_end=True)
        brow_objs.append(l_obj)

    objs["brows"] = brow_objs
    objs["eye_center_z"] = ez
    objs["eye_front_y"] = ey - eye_r_rad
    return objs


# =============================================================================
# STEP 4: SCULPTED 1990s SWEPT-BACK MULTI-LOCK HAIR (HUGS INTACT SKULL!)
# =============================================================================
def build_sculpted_hair(body_obj, joints, eye_z):
    # Use ONLY true cranium vertices (exclude ears and nose tip!) so the hair cap hugs the real skull smoothly!
    head_verts = [v.co for v in body_obj.data.vertices if v.co.z > 1.48 and abs(v.co.x) < 0.088]
    crown_v = [p for p in head_verts if p.z > eye_z + 0.015]
    skull_cy = (min(p.y for p in crown_v) + max(p.y for p in crown_v)) * 0.5
    skull_cz = eye_z + 0.020
    skull_c = Vector((0.0, skull_cy, skull_cz))

    cr_pts = np.array([[p.x, p.y - skull_cy, p.z - skull_cz] for p in head_verts], dtype=np.float32)
    cr_lens = np.linalg.norm(cr_pts, axis=1)
    cr_dirs = cr_pts / np.maximum(cr_lens[:, None], 1e-6)

    def skull_surface(dir_vec, thickness=0.008):
        dv = np.array([dir_vec.x, dir_vec.y, dir_vec.z], dtype=np.float32)
        dv /= max(np.linalg.norm(dv), 1e-6)
        dots = cr_dirs @ dv
        top_idx = np.argpartition(dots, -10)[-10:]
        r = float(np.percentile(cr_lens[top_idx], 75))
        return skull_c + Vector((dv[0], dv[1], dv[2])) * (r + thickness)

    hair_parts = []

    # A) Sleek Sculpted Base Hair Cap (clean hairline, ear cutouts, tapered sideburns & nape!)
    n_az = 36
    n_el = 13
    rings = []
    for ei in range(n_el):
        t = ei / (n_el - 1)
        ring = []
        for ai in range(n_az):
            az = 2.0 * math.pi * ai / n_az  # 0=+X(L), pi/2=+Y(back), -pi/2=-Y(front)
            s_front = max(0.0, -math.sin(az))
            s_back = max(0.0, math.sin(az))
            c_side = abs(math.cos(az))

            # Subtle widow's peak in center front
            widow = 0.065 * math.exp(-((az + math.pi * 0.5) / 0.24) ** 2)
            # Clean ear cutouts right around left/right ears (az ~ +0.16 and pi - 0.16)
            ear_l = math.exp(-((az - 0.16) / 0.19) ** 2)
            ear_r = math.exp(-((az - (math.pi - 0.16)) / 0.19) ** 2)
            ear_cut = max(ear_l, ear_r)
            # Sharp 1990s tapered sideburns right in front of ears (az ~ -0.26 and pi + 0.26)!
            sb_l = math.exp(-((az + 0.26) / 0.17) ** 2)
            sb_r = math.exp(-((az - (math.pi + 0.26)) / 0.17) ** 2)
            sideburn = max(sb_l, sb_r)

            # Full natural temple coverage (no bald buzzed gap above ears!) + crisp tapered nape
            min_el = (
                0.35 * s_front
                - 0.68 * s_back
                - 0.18 * c_side
                - widow
                + 0.22 * ear_cut
                - 0.36 * sideburn
            )
            el = (math.pi * 0.485) * (1.0 - t) + min_el * t
            d = Vector((math.cos(el) * math.cos(az), math.cos(el) * math.sin(az), math.sin(el)))
            # Swept executive pompadour lift at front-top, sleek sides so ALL 8 hats sit cleanly!
            pomp = 0.015 * max(0.0, math.sin(el)) * (0.45 + 0.55 * s_front) * (1.0 - 0.22 * max(0.0, -math.cos(az)))
            combed = 0.0022 * math.sin(az * 9.0 + el * 3.0) * math.sin(t * math.pi)
            thick = (0.0068 + pomp + combed) * (0.44 + 0.56 * math.sin(min(1.0, (1.0 - t) * 2.4) * math.pi * 0.5))
            if ei == n_el - 1:
                thick = 0.0012
            p = skull_surface(d, thickness=thick)
            if p.z > 1.714:
                p.z = 1.714
            ring.append(p)
        rings.append(ring)

    cap_obj = create_loft_mesh("HairBaseCap", rings, closed_v=True, cap_start=True, cap_end=False)
    hair_parts.append(cap_obj)

    # B) 12 Sculpted Multi-Layer Swept-Back Hair Locks (side part on character's right -X sweeping across & back!)
    lock_specs = [
        # Front sweeping pompadour locks (from -X part sweeping smoothly across forehead toward +X & back!)
        (-0.040, -0.008, +0.022, 0.016, 0.020, 0.010, 0.024, 0.011),
        (-0.018, +0.016, +0.042, 0.018, 0.022, 0.011, 0.026, 0.012),
        (+0.008, +0.038, +0.056, 0.016, 0.019, 0.010, 0.024, 0.011),
        (+0.032, +0.055, +0.064, 0.011, 0.013, 0.008, 0.020, 0.009),
        # Lower front curl accent (signature hairline wave from reference!)
        (-0.030, +0.005, +0.034, 0.014, 0.016, 0.008, 0.016, 0.008),
        # Sleek temple combed-back locks (right & left)
        (-0.055, -0.066, -0.058, 0.008, 0.009, 0.007, 0.017, 0.007),
        (-0.065, -0.074, -0.062, 0.006, 0.007, 0.006, 0.015, 0.006),
        (+0.055, +0.068, +0.060, 0.008, 0.009, 0.007, 0.018, 0.007),
        (+0.065, +0.074, +0.060, 0.006, 0.007, 0.006, 0.015, 0.006),
        # Crown & back swept locks
        (-0.024, -0.018, -0.012, 0.010, 0.012, 0.009, 0.022, 0.009),
        (+0.004, +0.008, +0.006, 0.011, 0.013, 0.009, 0.024, 0.009),
        (+0.028, +0.032, +0.024, 0.010, 0.012, 0.008, 0.022, 0.009),
    ]

    for idx, (xs, xm, xe, lf, lm, lb, w_max, t_max) in enumerate(lock_specs):
        pts = []
        radii = []
        n_steps = 10
        for k in range(n_steps):
            u = k / (n_steps - 1)
            x = (1 - u) ** 2 * xs + 2 * (1 - u) * u * xm + u ** 2 * xe
            if idx < 5:
                ang_fb = -0.68 + u * 2.15
            elif idx < 9:
                ang_fb = -0.50 + u * 1.95
            else:
                ang_fb = 0.05 + u * 1.95
            y_dir = math.sin(ang_fb)
            z_dir = math.cos(ang_fb) if (idx < 5 or idx >= 9) else (0.40 - u * 0.52)
            d = Vector((x * 9.5, y_dir, z_dir)).normalized()
            lift = (1 - u) ** 2 * lf + 2 * (1 - u) * u * lm + u ** 2 * lb
            taper = math.sin(u * math.pi) ** 0.65
            p = skull_surface(d, thickness=0.003 + lift * (0.45 + 0.55 * taper))
            if p.z > 1.720:
                p.z = 1.720
            pts.append(p)
            radii.append((max(0.003, w_max * (0.30 + 0.70 * taper)), max(0.002, t_max * (0.30 + 0.70 * taper))))
        l_obj = create_loft_mesh(f"HairLock_{idx:02d}", sample_bone_path(pts, radii, segs=8),
                                 closed_v=True, cap_start=True, cap_end=True)
        hair_parts.append(l_obj)

    return hair_parts


# =============================================================================
# STEP 5: TAILORED DOUBLE-BREASTED 1990s WOOL BLAZER + LAPELS + SHIRT + TIE
# =============================================================================
def build_tailored_blazer_and_shirt(body_obj, joints):
    parts = {"crimson": [], "lapel": [], "shirt": [], "pocket_silk": [], "tie": [], "gold": []}

    # Exclude arms from torso slice measurement! (Only true ribcage/waist/hips!)
    torso_v = [v.co for v in body_obj.data.vertices
               if 0.76 <= v.co.z <= 1.46 and abs(v.co.x) < (0.215 if v.co.z > 1.25 else 0.168)]
    sh_l = joints["upperarm01.L"][0]
    sh_r = joints["upperarm01.R"][0]
    elb_l = joints["lowerarm01.L"][0]
    elb_r = joints["lowerarm01.R"][0]
    wri_l = joints["wrist.L"][0]
    wri_r = joints["wrist.R"][0]

    torso_mid_v = [p for p in torso_v if 1.00 <= p.z <= 1.32]
    torso_cy = (min(p.y for p in torso_mid_v) + max(p.y for p in torso_mid_v)) * 0.5
    pelv_v_ref = [v.co for v in body_obj.data.vertices if 0.76 <= v.co.z <= 0.98 and abs(v.co.x) < 0.18]
    pelv_back_y = max(p.y for p in pelv_v_ref) if pelv_v_ref else torso_cy + 0.11

    def measure_torso_slice(z_val, pad_x=0.012, pad_y_f=0.016, pad_y_b=0.014):
        sl = [p for p in torso_v if abs(p.z - z_val) < 0.030]
        if not sl:
            return 0.155, torso_cy - 0.11, torso_cy + 0.10
        return max(abs(p.x) for p in sl) + pad_x, min(p.y for p in sl) - pad_y_f, max(p.y for p in sl) + pad_y_b

    # 1) Sculpted Athletic V-Tapered Double-Breasted 1990s Blazer Shell (ZERO barrel waist, ZERO collar poke, ZERO hip hem bleed!)
    z_levels = [
        0.785, 0.822, 0.865, 0.912, 0.960, 1.008, 1.056, 1.104,
        1.150, 1.194, 1.238, 1.282, 1.324, 1.358, 1.380, 1.396, 1.408
    ]
    n_seg = 44
    rings = []
    for zi, z in enumerate(z_levels):
        rx_m, yf_m, yb_m = measure_torso_slice(min(z, 1.38))
        # Sharp Savile Row athletic V-taper: broad shoulders (0.216) -> chest (0.194) -> cinched waist (0.158) -> flared hip skirt (0.186)!
        if z < 0.96:
            t_h = (0.96 - z) / (0.96 - 0.785)
            rx = min(0.188, max(rx_m + 0.008, 0.166 + 0.020 * t_h))
            yf = torso_cy - 0.122 - 0.006 * t_h
            yb = max(torso_cy + 0.120 + 0.006 * t_h, pelv_back_y + 0.028)
        elif z < 1.14:
            t_w = abs(z - 1.065) / 0.085
            rx = 0.158 + 0.012 * (t_w ** 1.4)
            yf = torso_cy - 0.116
            yb = max(torso_cy + 0.110, pelv_back_y + 0.016)
        elif z < 1.35:
            t_c = (z - 1.14) / 0.21
            rx = 0.170 + 0.046 * (t_c ** 0.75)
            yf = torso_cy - 0.118 - 0.012 * math.sin(t_c * math.pi * 0.85)
            yb = torso_cy + 0.110 + 0.008 * math.sin(t_c * math.pi)
        else:
            # Smooth rounded shoulder slope ending cleanly at Z=1.408 OUTSIDE the shirt collar (ZERO red poke-through!)
            t_s = (z - 1.35) / (1.408 - 1.35)
            ease = t_s * t_s * (3.0 - 2.0 * t_s)
            rx = 0.216 * (1.0 - ease) + 0.082 * ease
            yf = (torso_cy - 0.120) * (1.0 - ease) + (torso_cy - 0.076) * ease
            yb = (torso_cy + 0.112) * (1.0 - ease) + (torso_cy + 0.080) * ease

        cy = (yf + yb) * 0.5
        ry_f = cy - yf
        ry_b = yb - cy

        ring = []
        for s in range(n_seg):
            ang = 2.0 * math.pi * s / n_seg
            ca, sa = math.cos(ang), math.sin(ang)
            sign_c = 1.0 if ca >= 0 else -1.0
            sign_s = 1.0 if sa >= 0 else -1.0
            p_exp = 0.87 if z > 1.18 else 0.91
            cx_s = sign_c * (abs(ca) ** p_exp)
            cy_s = sign_s * (abs(sa) ** p_exp)
            ry = ry_b if sa >= 0 else ry_f
            px = cx_s * rx
            py = cy + cy_s * ry

            # Deep tailored V-neck opening above Z=1.145
            if sa < -0.25 and z > 1.145:
                v_open_half_x = 0.008 + ((z - 1.145) / 0.273) * 0.066
                if abs(px) < v_open_half_x:
                    t_in = math.cos((abs(px) / max(0.001, v_open_half_x)) * math.pi * 0.5)
                    py += 0.044 * t_in * min(1.0, (z - 1.145) / 0.065)

            # Double-breasted front overlap placket step (left-over-right!)
            if sa < -0.55 and z <= 1.160:
                step = 1.0 / (1.0 + math.exp(-(px + 0.042) / 0.008))
                py -= 0.0055 * step

            # Subtle sculptural waist tension creases + back center seam
            if 0.92 < z < 1.22:
                fold = 0.0022 * math.sin((z - 1.06) * 56.0 + abs(px) * 16.0) * math.exp(-((z - 1.07) / 0.09) ** 2) * (0.4 + 0.6 * abs(ca))
                px += ca * fold
                py += sa * fold
            if sa > 0.65:
                py -= 0.0038 * math.exp(-(px / 0.018) ** 2)

            pz = z
            if zi == 0 and sa < -0.6 and abs(px + 0.042) < 0.03:
                pz += 0.008 * math.exp(-((px + 0.042) / 0.018) ** 2)

            ring.append(Vector((px, py, pz)))
        rings.append(ring)

    torso_obj = create_loft_mesh("BlazerTorso", rings, closed_v=True, cap_start=True, cap_end=False)
    parts["crimson"].append(torso_obj)

    # Helper to sample exact front surface Y of blazer torso at (x, z)
    torso_pts = np.array([[v.co.x, v.co.y, v.co.z] for v in torso_obj.data.vertices], dtype=np.float32)

    def blazer_front_y(x, z):
        mask = torso_pts[:, 1] < torso_cy
        sub = torso_pts[mask]
        d2 = (sub[:, 0] - x) ** 2 + (sub[:, 2] - z) ** 2
        return float(sub[int(np.argmin(d2)), 1])

    # 2) Seamless Tailored Sleeves (Smooth Curved Shoulder Cap flowing into Upper Arm -> Elbow Creases -> Wrist Cuff!)
    for side_name, sh_j, elb_j, wri_j in [
        ("L", sh_l, elb_l, wri_l),
        ("R", sh_r, elb_r, wri_r),
    ]:
        sgn = 1.0 if side_name == "L" else -1.0
        fore_dir = (wri_j - elb_j).normalized()
        # Smooth Savile Row shoulder-cap transition buried cleanly inside blazer torso (ZERO spiky horns or ridge!)
        p_in = Vector((sgn * 0.140, sh_j.y + 0.002, sh_j.z - 0.006))
        p_sh = Vector((sgn * 0.186, sh_j.y - 0.002, sh_j.z - 0.012))
        p_delt = sh_j + Vector((sgn * 0.020, -0.003, -0.044))
        p_bicep = sh_j.lerp(elb_j, 0.45) + Vector((sgn * 0.007, 0.0, 0.0))
        p_elb_hi = sh_j.lerp(elb_j, 0.82) + Vector((sgn * 0.004, 0.0, 0.0))
        p_elb = elb_j + Vector((sgn * 0.004, 0.003, 0.0))
        p_fore_1 = elb_j.lerp(wri_j, 0.32) + Vector((sgn * 0.003, 0.002, 0.0))
        p_fore_2 = elb_j.lerp(wri_j, 0.66) + Vector((sgn * 0.002, 0.0, 0.0))
        p_cuff_pre = wri_j - fore_dir * 0.042
        p_cuff = wri_j - fore_dir * 0.017

        spine = [p_in, p_sh, p_delt, p_bicep, p_elb_hi, p_elb, p_fore_1, p_fore_2, p_cuff_pre, p_cuff]
        radii = [
            (0.052, 0.056),
            (0.055, 0.059),
            (0.055, 0.058),
            (0.053, 0.055),
            (0.049, 0.051),
            (0.047, 0.049),
            (0.044, 0.045),
            (0.041, 0.042),
            (0.039, 0.040),
            (0.038, 0.039),
        ]

        def sleeve_folds(u, ang, p):
            elb_w = math.exp(-((u - 0.56) / 0.14) ** 2)
            crease = 0.0038 * elb_w * math.sin(u * 40.0 + math.sin(ang) * 2.2)
            drape = 0.0016 * math.sin(ang * 3.0 + u * 12.0) * math.sin(u * math.pi)
            return crease + drape

        sl_rings = sample_bone_path(spine, radii, segs=20, folds_fn=sleeve_folds)
        cuff_in = [p_cuff + (pt - p_cuff) * 0.85 - fore_dir * 0.014 for pt in sl_rings[-1]]
        sl_rings.append(cuff_in)
        sl_obj = create_loft_mesh(f"BlazerSleeve_{side_name}", sl_rings, closed_v=True, cap_start=True, cap_end=False)
        parts["crimson"].append(sl_obj)

        # 3 Miniature Brushed-Gold Sleeve Cuff Buttons
        for bi in range(3):
            b_pos = p_cuff - fore_dir * (0.014 + bi * 0.014) + Vector((sgn * 0.036, 0.013, 0.0))
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=0.0044,
                                      matrix=Matrix.Translation(b_pos) @ Matrix.Diagonal((0.45, 1.0, 1.0, 1.0)))
            m_b = bpy.data.meshes.new(f"CuffBtn_{side_name}_{bi}")
            bm.to_mesh(m_b)
            bm.free()
            o_b = bpy.data.objects.new(f"CuffBtn_{side_name}_{bi}", m_b)
            bpy.context.scene.collection.objects.link(o_b)
            set_smooth(o_b)
            parts["gold"].append(o_b)

        # Crisp White French Dress Shirt Cuff bridging smoothly from sleeve onto wrist (ZERO air gap!)
        cf_spine = [wri_j - fore_dir * 0.034, wri_j - fore_dir * 0.014, wri_j + fore_dir * 0.004]
        cf_radii = [(0.034, 0.034), (0.033, 0.033), (0.032, 0.032)]
        cf_rings = sample_bone_path(cf_spine, cf_radii, segs=16)
        cf_in = [cf_spine[-1] + (pt - cf_spine[-1]) * 0.82 - fore_dir * 0.010 for pt in cf_rings[-1]]
        cf_rings.append(cf_in)
        cf_obj = create_loft_mesh(f"ShirtCuff_{side_name}", cf_rings, closed_v=True, cap_start=False, cap_end=False)
        parts["shirt"].append(cf_obj)

    # 3) Wide Sculpted 1990s Peaked Lapels + Collar Stand (`lapel` material)
    # Center collar rings on TRUE geometric neck center (so front throat & back neck both sit symmetrically inside collar!)
    neck_slice = [v.co for v in body_obj.data.vertices if 1.455 <= v.co.z <= 1.495 and abs(v.co.x) < 0.085]
    neck_cy = (min(p.y for p in neck_slice) + max(p.y for p in neck_slice)) * 0.5 if neck_slice else joints["neck01"][0].y
    neck_c = Vector((0.0, neck_cy, joints["neck01"][0].z))
    col_rings = []
    for k in range(5):
        v = k / 4.0
        z_c = 1.396 + v * 0.050
        r_x = 0.078 - v * 0.005
        r_y = 0.076 - v * 0.004
        ring = []
        for s in range(28):
            ang = math.radians(-46.0 + s * (272.0 / 27.0))
            ring.append(Vector((math.cos(ang) * r_x, neck_c.y + 0.002 + math.sin(ang) * r_y, z_c)))
        col_rings.append(ring)
    col_obj = create_loft_mesh("BlazerCollarStand", col_rings, closed_v=False)
    ms = col_obj.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.0075
    ms.offset = 1.0  # Solidify strictly OUTWARD away from white shirt collar!
    apply_all_modifiers(col_obj)
    set_smooth(col_obj)
    parts["lapel"].append(col_obj)

    for sgn, lname in ((1.0, "L"), (-1.0, "R")):
        # Sample exact blazer chest surface so lapels start cleanly OUTSIDE the white shirt collar (x_in >= 0.074 at top!)
        z_stations = [1.422, 1.388, 1.350, 1.298, 1.245, 1.192, 1.142, 1.092]
        x_in = [0.074, 0.068, 0.060, 0.048, 0.036, 0.018, 0.002, -0.036 if sgn > 0 else -0.024]
        x_out = [0.094, 0.118, 0.146, 0.122, 0.098, 0.070, 0.035, -0.032 if sgn > 0 else -0.020]
        lap_rings = []
        for i, zs in enumerate(z_stations):
            xi = (sgn * x_in[i]) if i < len(z_stations) - 1 else x_in[i]
            xo = (sgn * x_out[i]) if i < len(z_stations) - 1 else x_out[i]
            yi = blazer_front_y(xi, min(zs, 1.40)) - 0.004
            yo = blazer_front_y(xo, min(zs, 1.40)) - 0.006
            row = []
            for j in range(5):
                t = j / 4.0
                px = xi * (1.0 - t) + xo * t
                py = yi * (1.0 - t) + yo * t - 0.0065 * math.sin(t * math.pi)
                row.append(Vector((px, py, zs)))
            lap_rings.append(row)
        lap_obj = create_loft_mesh(f"BlazerLapel_{lname}", lap_rings, closed_v=False)
        ms = lap_obj.modifiers.new("Sol", 'SOLIDIFY')
        ms.thickness = 0.0075
        ms.offset = -0.35
        apply_all_modifiers(lap_obj)
        add_bevel_and_apply(lap_obj, width=0.0014, segments=1)
        set_smooth(lap_obj)
        parts["lapel"].append(lap_obj)

    # 4) 6 Brushed-Gold Double-Breasted Buttons (exact reference 6-button layout hugging front surface!)
    btn_xz = [
        (-0.070, 1.170), (+0.070, 1.170),
        (-0.043, 1.064), (+0.043, 1.064),
        (-0.043, 0.956), (+0.043, 0.956),
    ]
    for bi, (bx, bz) in enumerate(btn_xz):
        by = blazer_front_y(bx, bz) - 0.0055
        bm = bmesh.new()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=18,
            radius1=0.0102, radius2=0.0090, depth=0.0056,
            matrix=Matrix.Translation((bx, by, bz)) @ Matrix.Rotation(math.radians(90), 4, 'X')
        )
        m_btn = bpy.data.meshes.new(f"BlazerBtn_{bi}")
        bm.to_mesh(m_btn)
        bm.free()
        o_btn = bpy.data.objects.new(f"BlazerBtn_{bi}", m_btn)
        bpy.context.scene.collection.objects.link(o_btn)
        add_bevel_and_apply(o_btn, width=0.0011, segments=1)
        set_smooth(o_btn)
        parts["gold"].append(o_btn)

    # 5) 2 Angled Hip Welt Pocket Flaps + Left Chest Welt Pocket with Folded Cream Silk Pocket Square!
    for sgn, pname in ((1.0, "L"), (-1.0, "R")):
        fp_rings = []
        for k in range(3):
            zf = 0.946 - k * 0.018
            row = []
            for j in range(6):
                t = j / 5.0
                px = sgn * (0.078 + t * 0.088)
                py = blazer_front_y(px, zf) - 0.0045
                row.append(Vector((px, py, zf)))
            fp_rings.append(row)
        fp_obj = create_loft_mesh(f"PocketFlap_{pname}", fp_rings, closed_v=False)
        ms = fp_obj.modifiers.new("Sol", 'SOLIDIFY')
        ms.thickness = 0.006
        ms.offset = -1.0
        apply_all_modifiers(fp_obj)
        add_bevel_and_apply(fp_obj, width=0.0012, segments=1)
        set_smooth(fp_obj)
        parts["lapel"].append(fp_obj)

    # Left Breast Welt Pocket (+X)
    w_rings = []
    for k in range(2):
        zw = 1.246 + k * 0.012
        row = []
        for j in range(5):
            t = j / 4.0
            px = 0.088 + t * 0.062
            py = blazer_front_y(px, zw) - 0.0045
            row.append(Vector((px, py, zw + t * 0.006)))
        w_rings.append(row)
    welt_obj = create_loft_mesh("BreastWelt", w_rings, closed_v=False)
    ms = welt_obj.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.0055
    ms.offset = -1.0
    apply_all_modifiers(welt_obj)
    set_smooth(welt_obj)
    parts["lapel"].append(welt_obj)

    # 2-Peak Folded Cream Silk Pocket Square emerging cleanly from breast welt
    py_ps1 = blazer_front_y(0.095, 1.260) - 0.004
    py_ps2 = blazer_front_y(0.145, 1.265) - 0.004
    bm = bmesh.new()
    ps_pts = [
        Vector((0.092, py_ps1, 1.254)),
        Vector((0.146, py_ps2, 1.260)),
        Vector((0.133, py_ps2 - 0.003, 1.288)),
        Vector((0.118, (py_ps1 + py_ps2) * 0.5 - 0.003, 1.275)),
        Vector((0.104, py_ps1 - 0.003, 1.292)),
    ]
    bm.faces.new([bm.verts.new(p) for p in ps_pts])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    m_ps = bpy.data.meshes.new("PocketSquare")
    bm.to_mesh(m_ps)
    bm.free()
    o_ps = bpy.data.objects.new("PocketSquare", m_ps)
    bpy.context.scene.collection.objects.link(o_ps)
    ms = o_ps.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.004
    apply_all_modifiers(o_ps)
    set_smooth(o_ps)
    parts["pocket_silk"].append(o_ps)

    # 6) Crisp White Dress Shirt + Structured Spread Collar
    sh_rings = []
    for k in range(6):
        t = k / 5.0
        zs = 1.115 + t * 0.295
        row = []
        for j in range(11):
            u = (j / 10.0) * 2.0 - 1.0
            px = u * (0.082 - t * 0.015)
            py_base = torso_cy - 0.108 + t * 0.044
            py = py_base + 0.022 * (u * u)
            row.append(Vector((px, py, zs)))
        sh_rings.append(row)
    sh_front = create_loft_mesh("ShirtFront", sh_rings, closed_v=False)
    ms = sh_front.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.007
    apply_all_modifiers(sh_front)
    set_smooth(sh_front)
    parts["shirt"].append(sh_front)

    # Smooth crisp white dress shirt collar band (+6mm taller, tight around neck, razor-smooth top & back rim!)
    sc_rings = []
    for k in range(6):
        t = k / 5.0
        zc = 1.402 + t * 0.066
        rx_c = 0.062 - t * 0.004
        ry_c = 0.061 - t * 0.004
        row = []
        for s in range(32):
            ang = math.radians(-82.0 + s * (344.0 / 31.0))
            dip = -0.012 * (1.0 - t) * max(0.0, -math.sin(ang)) ** 2
            row.append(Vector((math.cos(ang) * rx_c, neck_c.y + math.sin(ang) * ry_c, zc + dip)))
        sc_rings.append(row)
    sc_obj = create_loft_mesh("ShirtCollar", sc_rings, closed_v=False)
    ms = sc_obj.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.005
    ms.offset = 1.0
    apply_all_modifiers(sc_obj)
    set_smooth(sc_obj)
    parts["shirt"].append(sc_obj)

    # Anchor spread collar wings, Windsor tie, tie bar & gold chain to original chest-hugging spine Y so tie lies flush against shirtfront!
    chest_neck_y = joints["neck01"][0].y
    for sgn, cname in ((1.0, "L"), (-1.0, "R")):
        bm = bmesh.new()
        ny = chest_neck_y - 0.062
        pts = [
            Vector((sgn * 0.014, ny - 0.005, 1.430)),
            Vector((sgn * 0.062, ny + 0.010, 1.436)),
            Vector((sgn * 0.074, ny - 0.022, 1.384)),
            Vector((sgn * 0.022, ny - 0.030, 1.390)),
        ]
        bm.faces.new([bm.verts.new(p) for p in pts])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        mc = bpy.data.meshes.new(f"CollarWing_{cname}")
        bm.to_mesh(mc)
        bm.free()
        oc = bpy.data.objects.new(f"CollarWing_{cname}", mc)
        bpy.context.scene.collection.objects.link(oc)
        ms = oc.modifiers.new("Sol", 'SOLIDIFY')
        ms.thickness = 0.005
        apply_all_modifiers(oc)
        add_bevel_and_apply(oc, width=0.0010, segments=1)
        set_smooth(oc)
        parts["shirt"].append(oc)

    # 7) Black Silk Windsor-Knot Tie + Gold Tie Bar + Subtle Gold Collar Chain (strictly Z <= 1.43, flush on shirt!)
    ny_tie = chest_neck_y - 0.074
    knot_rings = []
    for k in range(5):
        t = k / 4.0
        zk = 1.428 - t * 0.040
        wk = 0.022 - t * 0.008
        dk = 0.012 - t * 0.0025
        yk = ny_tie - t * 0.015
        ring = [Vector((math.cos(2 * math.pi * s / 14) * wk, yk + math.sin(2 * math.pi * s / 14) * dk, zk)) for s in range(14)]
        knot_rings.append(ring)
    parts["tie"].append(create_loft_mesh("TieKnot", knot_rings, closed_v=True, cap_start=True, cap_end=True))

    blade_rings = []
    for k in range(9):
        t = k / 8.0
        zb = 1.392 - t * 0.270
        wb = 0.013 + t * 0.020
        yb = (ny_tie - 0.015) - math.sin(min(1.0, t * 1.4) * math.pi * 0.5) * 0.036
        ring = []
        for s in range(12):
            ang = 2.0 * math.pi * s / 12
            ca, sa = math.cos(ang), math.sin(ang)
            dimple = 0.0020 * (1.0 - t) * max(0.0, -sa) * math.exp(-(ca / 0.35) ** 2)
            ring.append(Vector((ca * wb, yb + sa * 0.007 + dimple, zb)))
        blade_rings.append(ring)
    parts["tie"].append(create_loft_mesh("TieBlade", blade_rings, closed_v=True, cap_start=True, cap_end=True))

    # Brushed-gold tie bar
    tb_y = (ny_tie - 0.015) - 0.036 - 0.007
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0,
                          matrix=Matrix.Translation((0.005, tb_y, 1.275)) @ Matrix.Diagonal((0.042, 0.005, 0.007, 1.0)))
    m_tb = bpy.data.meshes.new("TieBar")
    bm.to_mesh(m_tb)
    bm.free()
    o_tb = bpy.data.objects.new("TieBar", m_tb)
    bpy.context.scene.collection.objects.link(o_tb)
    add_bevel_and_apply(o_tb, width=0.0010, segments=1)
    set_smooth(o_tb)
    parts["gold"].append(o_tb)

    # Gold curb chain peeking at collar (strictly Z <= 1.425 < 1.50, flush on shirt!)
    ch_pts = []
    for k in range(16):
        t = k / 15.0
        ang = math.radians(-130.0 + t * 80.0)
        ch_pts.append(Vector((math.cos(ang) * 0.068, chest_neck_y - 0.012 + math.sin(ang) * 0.072, 1.416 - math.sin(t * math.pi) * 0.032)))
    parts["gold"].append(create_loft_mesh("GoldChain", sample_bone_path(ch_pts, [(0.0038, 0.0024)] * 16, segs=8),
                                          closed_v=True, cap_start=True, cap_end=True))

    return parts


# =============================================================================
# STEP 6: LEFT WRISTWATCH + RIGHT HAND GOLD SIGNET RING (SNAPPED TO ACTUAL MESH!)
# =============================================================================
def build_hand_accessories(skin_obj, joints):
    parts = {"gold": [], "belt": [], "watch_dial": []}
    skin_pts = np.array([[v.co.x, v.co.y, v.co.z] for v in skin_obj.data.vertices], dtype=np.float32)

    # 1) Classic Gold & Black-Leather Wristwatch on LEFT Wrist (snapped tightly to left wrist skin centroid!)
    wri_l = joints["wrist.L"][0]
    elb_l = joints["lowerarm01.L"][0]
    fore_l = (wri_l - elb_l).normalized()
    w_probe = wri_l + fore_l * 0.002
    # Refine wrist center from actual skin vertices near wrist!
    d_w = np.linalg.norm(skin_pts - np.array([w_probe.x, w_probe.y, w_probe.z]), axis=1)
    near_w = skin_pts[d_w < 0.042]
    w_pos = Vector(near_w.mean(axis=0)) if len(near_w) > 10 else w_probe
    # Estimate actual wrist radius perpendicular to fore_l
    if len(near_w) > 10:
        rel = near_w - np.array([w_pos.x, w_pos.y, w_pos.z])
        ax = np.array([fore_l.x, fore_l.y, fore_l.z])
        perp = rel - np.outer(rel @ ax, ax)
        w_rad = float(np.percentile(np.linalg.norm(perp, axis=1), 90))
    else:
        w_rad = 0.028

    up_w = fore_l
    out_w = Vector((1.0, -0.35, 0.0)).normalized()
    side_w = up_w.cross(out_w).normalized()
    out_w = side_w.cross(up_w).normalized()
    rot_w = Matrix((out_w, side_w, up_w)).transposed().to_4x4()

    st_rings = []
    for k in range(3):
        off = (k - 1) * 0.007
        ring = [w_pos + (rot_w.to_3x3() @ Vector((math.cos(2 * math.pi * s / 18) * (w_rad + 0.0025),
                                                 math.sin(2 * math.pi * s / 18) * (w_rad + 0.0015), off)))
                for s in range(18)]
        st_rings.append(ring)
    st_obj = create_loft_mesh("WatchStrap", st_rings, closed_v=True)
    ms = st_obj.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.0032
    apply_all_modifiers(st_obj)
    set_smooth(st_obj)
    parts["belt"].append(st_obj)

    case_center = w_pos + out_w * (w_rad + 0.0045)
    bm = bmesh.new()
    case_mat = Matrix.Translation(case_center) @ rot_w @ Matrix.Rotation(math.radians(90), 4, 'Y')
    bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.0150, radius2=0.0140, depth=0.0064, matrix=case_mat)
    bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=6, radius=0.0024,
                              matrix=Matrix.Translation(case_center - up_w * 0.0155))
    m_wc = bpy.data.meshes.new("WatchCase")
    bm.to_mesh(m_wc)
    bm.free()
    o_wc = bpy.data.objects.new("WatchCase", m_wc)
    bpy.context.scene.collection.objects.link(o_wc)
    set_smooth(o_wc)
    parts["gold"].append(o_wc)

    bm = bmesh.new()
    dial_mat = Matrix.Translation(case_center + out_w * 0.0030) @ rot_w @ Matrix.Rotation(math.radians(90), 4, 'Y')
    bmesh.ops.create_cone(bm, cap_ends=True, segments=18, radius1=0.0120, radius2=0.0120, depth=0.0012, matrix=dial_mat)
    m_wd = bpy.data.meshes.new("WatchDial")
    bm.to_mesh(m_wd)
    bm.free()
    o_wd = bpy.data.objects.new("WatchDial", m_wd)
    bpy.context.scene.collection.objects.link(o_wd)
    set_smooth(o_wd)
    parts["watch_dial"].append(o_wd)

    # 2) Heavy 1990s Gold Signet Ring snapped DIRECTLY around RIGHT Hand Ring Finger (`finger4-1.R`)!
    f4_h, f4_t, _ = joints["finger4-1.R"]
    f_dir = (f4_t - f4_h).normalized()
    ring_probe = f4_h.lerp(f4_t, 0.48)
    # Snap center & radius directly to actual ring-finger vertices!
    d_f = np.linalg.norm(skin_pts - np.array([ring_probe.x, ring_probe.y, ring_probe.z]), axis=1)
    near_f = skin_pts[d_f < 0.015]
    if len(near_f) >= 6:
        ring_pos = Vector(near_f.mean(axis=0))
        rel_f = near_f - np.array([ring_pos.x, ring_pos.y, ring_pos.z])
        ax_f = np.array([f_dir.x, f_dir.y, f_dir.z])
        perp_f = rel_f - np.outer(rel_f @ ax_f, ax_f)
        f_rad = float(np.percentile(np.linalg.norm(perp_f, axis=1), 85))
    else:
        ring_pos = ring_probe
        f_rad = 0.0086
    f_rad = max(0.0076, min(0.0108, f_rad))

    dorsal = Vector((-1.0, -0.25, 0.0)).normalized()
    tang = f_dir.cross(dorsal).normalized()
    dorsal = tang.cross(f_dir).normalized()
    rot_r = Matrix((dorsal, tang, f_dir)).transposed().to_4x4()

    rg_rings = []
    for k in range(5):
        v = (k / 4.0) * 2.0 - 1.0
        ring = []
        for s in range(18):
            ang = 2.0 * math.pi * s / 18
            ca, sa = math.cos(ang), math.sin(ang)
            top_w = max(0.0, ca) ** 2
            rad_o = f_rad + 0.0016 + 0.0032 * top_w
            half_band = 0.0034 + 0.0030 * top_w
            ring.append(ring_pos + (rot_r.to_3x3() @ Vector((ca * rad_o, sa * (f_rad + 0.0014), v * half_band))))
        rg_rings.append(ring)
    rg_obj = create_loft_mesh("GoldSignetRing", rg_rings, closed_v=True)
    ms = rg_obj.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.0022
    ms.offset = 1.0
    apply_all_modifiers(rg_obj)
    set_smooth(rg_obj)
    parts["gold"].append(rg_obj)

    return parts


# =============================================================================
# STEP 7: TAILORED CHARCOAL PLEATED TROUSERS + BELT + POLISHED LEATHER OXFORDS
# =============================================================================
def build_trousers_and_oxfords(body_obj, joints):
    parts = {"trousers": [], "belt": [], "gold": [], "shoes": [], "sole": []}
    pelv_v = [v.co for v in body_obj.data.vertices if 0.72 <= v.co.z <= 1.02]
    pelv_cy = (min(p.y for p in pelv_v) + max(p.y for p in pelv_v)) * 0.5

    # 1) Pelvis block of tailored trousers (snugly tucked inside blazer skirt — zero back bleed!)
    hip_rings = []
    for k in range(6):
        t = k / 5.0
        zh = 0.785 + t * 0.215
        rx = 0.156 - t * 0.006
        ry = 0.090 - t * 0.004
        ring = []
        for s in range(28):
            ang = 2.0 * math.pi * s / 28
            ca, sa = math.cos(ang), math.sin(ang)
            pleat = 0.004 * max(0.0, -sa) * math.exp(-((abs(ca) - 0.35) / 0.14) ** 2)
            ring.append(Vector((ca * rx, pelv_cy - 0.006 + sa * (ry + pleat), zh)))
        hip_rings.append(ring)
    parts["trousers"].append(create_loft_mesh("TrousersPelvis", hip_rings, closed_v=True, cap_start=True, cap_end=True))

    # Leather Belt + Brass Buckle
    bt_rings = [[Vector((math.cos(2 * math.pi * s / 28) * 0.156, pelv_cy + math.sin(2 * math.pi * s / 28) * 0.098,
                         0.968 + k * 0.015)) for s in range(28)] for k in range(3)]
    bt_obj = create_loft_mesh("LeatherBelt", bt_rings, closed_v=True)
    ms = bt_obj.modifiers.new("Sol", 'SOLIDIFY')
    ms.thickness = 0.005
    apply_all_modifiers(bt_obj)
    set_smooth(bt_obj)
    parts["belt"].append(bt_obj)

    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0,
                          matrix=Matrix.Translation((0.0, pelv_cy - 0.110, 0.983)) @ Matrix.Diagonal((0.044, 0.009, 0.036, 1.0)))
    m_bk = bpy.data.meshes.new("BeltBuckle")
    bm.to_mesh(m_bk)
    bm.free()
    o_bk = bpy.data.objects.new("BeltBuckle", m_bk)
    bpy.context.scene.collection.objects.link(o_bk)
    add_bevel_and_apply(o_bk, width=0.0020, segments=1)
    set_smooth(o_bk)
    parts["gold"].append(o_bk)

    # 2) Tailored Left & Right Pleated Trouser Legs + Stitched Polished Leather Oxford Shoes!
    for side_name, th_j, kn_j, ft_j in [
        ("L", joints["upperleg01.L"][0], joints["lowerleg01.L"][0], joints["foot.L"][0]),
        ("R", joints["upperleg01.R"][0], joints["lowerleg01.R"][0], joints["foot.R"][0]),
    ]:
        sgn = 1.0 if side_name == "L" else -1.0
        p0 = Vector((sgn * 0.080, pelv_cy - 0.004, 0.865))
        p1 = th_j.lerp(kn_j, 0.25) + Vector((sgn * -0.008, -0.002, 0.0))
        p2 = th_j.lerp(kn_j, 0.62)
        p3 = kn_j + Vector((0.0, -0.004, 0.02))
        p4 = kn_j.lerp(ft_j, 0.36)
        p5 = kn_j.lerp(ft_j, 0.72)
        p6 = Vector((ft_j.x, ft_j.y - 0.008, 0.115))
        p7 = Vector((ft_j.x, ft_j.y - 0.015, 0.064))

        spine = [p0, p1, p2, p3, p4, p5, p6, p7]
        radii = [
            (0.068, 0.076),
            (0.074, 0.082),
            (0.074, 0.081),
            (0.070, 0.077),
            (0.066, 0.073),
            (0.061, 0.068),
            (0.058, 0.065),
            (0.058, 0.066),
        ]

        def trouser_folds(u, ang, p):
            front_c = max(0.0, -math.sin(ang)) ** 10
            back_c = max(0.0, math.sin(ang)) ** 10
            crease = 0.0080 * front_c + 0.0055 * back_c
            knee_b = math.exp(-((u - 0.45) / 0.10) ** 2) * max(0.0, math.sin(ang))
            knee_fold = 0.0035 * knee_b * math.sin(u * 46.0)
            ank_w = max(0.0, (u - 0.78) / 0.22)
            ank_fold = 0.0045 * ank_w * math.sin(u * 38.0 + math.sin(ang) * 1.5)
            return crease + knee_fold + ank_fold

        leg_rings = sample_bone_path(spine, radii, segs=22, folds_fn=trouser_folds)
        hem_in = [p7 + (pt - p7) * 0.80 + Vector((0, 0, 0.018)) for pt in leg_rings[-1]]
        leg_rings.append(hem_in)
        parts["trousers"].append(create_loft_mesh(f"TrouserLeg_{side_name}", leg_rings, closed_v=True, cap_start=True, cap_end=False))

        # 3) Sleek Sculpted Polished Black Leather Oxford Dress Shoe!
        yaw = math.radians(+7.0 if side_name == "L" else -7.0)
        foot_base = Vector((ft_j.x, ft_j.y, 0.0))
        rot_sh = Matrix.Rotation(yaw, 4, 'Z')

        def to_shoe_world(lx, ly, lz):
            return foot_base + (rot_sh.to_3x3() @ Vector((lx, ly, lz)))

        shoe_stations = [
            (+0.062, 0.032, 0.018, 0.074),
            (+0.044, 0.040, 0.018, 0.092),
            (+0.012, 0.044, 0.016, 0.096),
            (-0.026, 0.044, 0.014, 0.078),
            (-0.070, 0.047, 0.013, 0.060),
            (-0.112, 0.048, 0.013, 0.047),   # Stitched cap-toe ridge!
            (-0.148, 0.041, 0.014, 0.038),
            (-0.178, 0.027, 0.016, 0.030),
            (-0.192, 0.011, 0.018, 0.024),
        ]
        sh_rings = []
        for si, (sy, hw, zb, zt) in enumerate(shoe_stations):
            ring = []
            for s in range(18):
                ang = 2.0 * math.pi * s / 18
                ca, sa = math.cos(ang), math.sin(ang)
                lx = ca * hw * (1.0 + sgn * 0.06 * ca)
                if sa >= 0:
                    ridge = 0.0022 if si == 5 else 0.0
                    lz = (zb + zt) * 0.5 + (sa ** 0.78) * ((zt - zb) * 0.5 + ridge)
                else:
                    lz = (zb + zt) * 0.5 + sa * ((zt - zb) * 0.5)
                ring.append(to_shoe_world(lx, sy, lz))
            sh_rings.append(ring)
        parts["shoes"].append(create_loft_mesh(f"OxfordUpper_{side_name}", sh_rings, closed_v=True, cap_start=True, cap_end=True))

        # Stacked Leather Heel Block & Outsole Welt flat at Z = 0.000!
        sole_rings = []
        for sy, hw, _, _ in shoe_stations:
            z_low = 0.000 if (sy > -0.005 or sy < -0.055) else 0.006
            z_hi = 0.019 if sy > -0.010 else 0.014
            ring = []
            for s in range(16):
                ang = 2.0 * math.pi * s / 16
                ca, sa = math.cos(ang), math.sin(ang)
                lx = ca * (hw + 0.0045) * (1.0 + sgn * 0.06 * ca)
                ring.append(to_shoe_world(lx, sy, z_low if sa < 0 else z_hi))
            sole_rings.append(ring)
        parts["sole"].append(create_loft_mesh(f"OxfordSole_{side_name}", sole_rings, closed_v=True, cap_start=True, cap_end=True))

        # Crisscross sculpted Oxford shoelaces
        for li in range(4):
            y_l = -0.028 - li * 0.014
            z_l = 0.076 - li * 0.0065
            pts_l = [to_shoe_world(-0.016, y_l, z_l), to_shoe_world(0.0, y_l - 0.002, z_l + 0.0042), to_shoe_world(+0.016, y_l, z_l)]
            parts["shoes"].append(create_loft_mesh(f"OxfordLace_{side_name}_{li}",
                                                   sample_bone_path(pts_l, [(0.0020, 0.0020)] * 3, segs=6),
                                                   closed_v=True, cap_start=True, cap_end=True))

    return parts


# =============================================================================
# STEP 8: 3 HIDDEN TINY DUMMY PRIMITIVES FOR VisualEngine3D.ReadMeshGroups
# =============================================================================
def build_hidden_prop_dummies():
    out = {}
    for idx, mat_name in enumerate(("GunMetal", "Gold90s", "PolymerGrip")):
        bm = bmesh.new()
        z0 = 0.94 + idx * 0.005
        v0 = bm.verts.new((0.0, 0.0, z0))
        v1 = bm.verts.new((0.001, 0.0, z0))
        v2 = bm.verts.new((0.0, 0.001, z0 + 0.001))
        bm.faces.new((v0, v1, v2))
        m = bpy.data.meshes.new(f"HiddenProp_{mat_name}")
        bm.to_mesh(m)
        bm.free()
        o = bpy.data.objects.new(f"HiddenProp_{mat_name}", m)
        bpy.context.scene.collection.objects.link(o)
        out[mat_name] = o
    return out


# =============================================================================
# STEP 9: UV UNWRAP + CYCLES AO BAKE + PROCEDURAL FABRIC/SKIN/IRIS TEXTURES
# =============================================================================
def smart_uv_unwrap(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    if not obj.data.uv_layers:
        obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.015)
    bpy.ops.object.mode_set(mode='OBJECT')
    obj.select_set(False)


def join_objects(objs, name):
    if not objs:
        return None
    if len(objs) == 1:
        objs[0].name = name
        return objs[0]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    res = bpy.context.view_layer.objects.active
    res.name = name
    res.select_set(False)
    return res


def bake_ao_for_object(obj, res=512, samples=16):
    img = bpy.data.images.new(f"AO_{obj.name}", width=res, height=res, alpha=False)
    img.colorspace_settings.name = 'Non-Color'
    orig_mats = list(obj.data.materials)
    bmat = bpy.data.materials.new(f"_BakeAO_{obj.name}")
    nt = bmat.node_tree
    tex_node = nt.nodes.new("ShaderNodeTexImage")
    tex_node.image = img
    nt.nodes.active = tex_node
    obj.data.materials.clear()
    obj.data.materials.append(bmat)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = samples
    scene.cycles.use_denoising = False
    scene.cycles.bake_type = 'AO'
    scene.render.bake.margin = 8
    scene.render.bake.use_selected_to_active = False
    bpy.ops.object.bake(type='AO')

    arr = np.array(img.pixels[:], dtype=np.float32).reshape((res, res, 4))
    ao_gray = arr[:, :, 0].copy()

    obj.data.materials.clear()
    for m in orig_mats:
        obj.data.materials.append(m)
    bpy.data.materials.remove(bmat)
    bpy.data.images.remove(img)
    return ao_gray


def create_textured_image(name, rgb_float_hwc, save_filename=None):
    h, w, _ = rgb_float_hwc.shape
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(rgb_float_hwc, 0.0, 1.0)
    img = bpy.data.images.new(name, width=w, height=h, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(rgba.ravel())
    if save_filename:
        p = os.path.join(TEX_DIR, save_filename)
        img.filepath_raw = p
        img.file_format = 'PNG'
        img.save()
    img.pack()
    return img


def make_wool_ao_texture(name, ao_gray, weave_amp=0.055, ao_floor=0.64, save_name=None):
    h, w = ao_gray.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    diag1 = np.sin((xx + yy) * (2.0 * math.pi / 4.0))
    diag2 = np.sin((xx - yy) * (2.0 * math.pi / 7.0)) * 0.35
    fine_grain = np.sin(xx * 1.9 + np.sin(yy * 0.43) * 3.1) * 0.25
    weave = 1.0 + weave_amp * (0.55 * diag1 + diag2 + fine_grain)
    ao_shaped = np.clip(ao_floor + (1.0 - ao_floor) * (ao_gray ** 0.72), ao_floor, 1.0)
    lum = np.clip(ao_shaped * weave, 0.54, 1.0)
    rgb = np.stack([
        np.clip(lum * 1.00, 0.0, 1.0),
        np.clip(lum * (0.985 + 0.015 * ao_shaped), 0.0, 1.0),
        np.clip(lum * (0.975 + 0.025 * ao_shaped), 0.0, 1.0),
    ], axis=-1)
    return create_textured_image(name, rgb, save_name)


def make_skin_ao_texture(name, ao_gray, save_name=None):
    h, w = ao_gray.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    micro = 1.0 + 0.014 * np.sin(xx * 0.35 + np.sin(yy * 0.21) * 2.0) * np.cos(yy * 0.29)
    ao_shaped = np.clip(0.75 + 0.25 * (ao_gray ** 0.78), 0.75, 1.0)
    crevice = np.clip(1.0 - ao_gray, 0.0, 1.0)
    r = np.clip(ao_shaped * micro * (1.00 + 0.055 * crevice), 0.68, 1.0)
    g = np.clip(ao_shaped * micro * (0.995 - 0.045 * crevice), 0.62, 1.0)
    b = np.clip(ao_shaped * micro * (0.990 - 0.070 * crevice), 0.58, 1.0)
    return create_textured_image(name, np.stack([r, g, b], axis=-1), save_name)


def make_iris_texture(name, res=256, save_name=None):
    yy, xx = np.mgrid[0:res, 0:res].astype(np.float32)
    u = (xx / (res - 1)) * 2.0 - 1.0
    v = (yy / (res - 1)) * 2.0 - 1.0
    r = np.sqrt(u * u + v * v)
    ang = np.arctan2(v, u)
    fibers = 0.86 + 0.14 * (0.55 * np.sin(ang * 32.0 + r * 6.0) + 0.45 * np.cos(ang * 54.0 - r * 4.0))
    inner_glow = 0.92 + 0.12 * np.exp(-((r - 0.42) / 0.18) ** 2)
    limbal = 1.0 - 0.62 * np.clip((r - 0.78) / 0.20, 0.0, 1.0)
    val = np.clip(fibers * inner_glow * limbal, 0.25, 1.0)
    return create_textured_image(name, np.stack([val, val, val], axis=-1), save_name)


def make_hair_ao_texture(name, ao_gray, save_name=None):
    h, w = ao_gray.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    strands = 0.85 + 0.15 * np.sin(xx * 0.95 + np.sin(yy * 0.08) * 4.0)
    ao_s = 0.42 + 0.58 * (ao_gray ** 0.8)
    base = np.clip(ao_s * strands, 0.24, 1.0)
    return create_textured_image(name, np.stack([base * 0.115, base * 0.108, base * 0.115], axis=-1), save_name)


def make_trousers_ao_texture(name, ao_gray, save_name=None):
    h, w = ao_gray.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    weave = 0.94 + 0.06 * np.sin((xx + yy) * 1.4) * np.cos((xx - yy) * 0.7)
    ao_s = 0.52 + 0.48 * (ao_gray ** 0.75)
    base = np.clip(ao_s * weave, 0.34, 1.0)
    return create_textured_image(name, np.stack([base * 0.185, base * 0.192, base * 0.210], axis=-1), save_name)


def make_shirt_ao_texture(name, ao_gray, save_name=None):
    h, w = ao_gray.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    weave = 0.98 + 0.02 * np.sin(xx * 2.2) * np.sin(yy * 2.2)
    ao_s = 0.72 + 0.28 * (ao_gray ** 0.8)
    v = np.clip(ao_s * weave, 0.68, 0.98)
    return create_textured_image(name, np.stack([v * 0.98, v * 0.975, v * 0.96], axis=-1), save_name)


def make_leather_ao_texture(name, ao_gray, save_name=None):
    h, w = ao_gray.shape
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    grain = 0.93 + 0.07 * np.sin(xx * 1.3 + np.cos(yy * 1.1) * 2.5)
    ao_s = 0.45 + 0.55 * (ao_gray ** 0.8)
    v = np.clip(ao_s * grain, 0.30, 1.0)
    return create_textured_image(name, np.stack([v * 0.105, v * 0.110, v * 0.125], axis=-1), save_name)


# =============================================================================
# MAIN BUILD PIPELINE
# =============================================================================
def main():
    print("=== [1/6] Building MPFB 2 Chiseled & Posed Protagonist ===")
    body_obj, joints = build_mpfb_hero()
    skin_obj = extract_visible_skin(body_obj, joints)

    print("=== [2/6] Sculpting 3D Eyes, Expressive Brows & Multi-Lock 1990s Hair ===")
    eye_data = build_eyes_and_brows(body_obj, joints)
    hair_parts = build_sculpted_hair(body_obj, joints, eye_data["eye_center_z"])

    print("=== [3/6] Tailoring Double-Breasted 1990s Blazer, Shirt, Tie & Accessories ===")
    blazer_parts = build_tailored_blazer_and_shirt(body_obj, joints)
    hand_acc = build_hand_accessories(skin_obj, joints)
    lower_parts = build_trousers_and_oxfords(body_obj, joints)
    dummies = build_hidden_prop_dummies()

    bpy.data.objects.remove(body_obj, do_unlink=True)

    groups = {
        "skin": [skin_obj],
        "sclera": [eye_data["sclera"]],
        "iris": [eye_data["iris"]],
        "pupil": [eye_data["pupil"]],
        "catchlight": [eye_data["catchlight"]],
        "hair": hair_parts + eye_data["brows"],
        "crimson": blazer_parts["crimson"],
        "lapel": blazer_parts["lapel"],
        "shirt": blazer_parts["shirt"],
        "pocket_silk": blazer_parts["pocket_silk"],
        "tie": blazer_parts["tie"],
        "gold": blazer_parts["gold"] + hand_acc["gold"] + lower_parts["gold"],
        "watch_dial": hand_acc["watch_dial"],
        "trousers": lower_parts["trousers"],
        "belt": hand_acc["belt"] + lower_parts["belt"],
        "shoes": lower_parts["shoes"],
        "sole": lower_parts["sole"],
        "GunMetal": [dummies["GunMetal"]],
        "Gold90s": [dummies["Gold90s"]],
        "PolymerGrip": [dummies["PolymerGrip"]],
    }

    joined = {}
    for mname, olist in groups.items():
        obj = join_objects(olist, f"Part_{mname}")
        if obj is not None:
            joined[mname] = obj

    print("=== [4/6] Smart UV Unwrapping & Baking Real Cycles Ambient Occlusion ===")
    bake_specs = {
        "crimson": 1024,
        "lapel": 512,
        "skin": 1024,
        "hair": 512,
        "trousers": 512,
        "shirt": 512,
        "shoes": 512,
    }
    for mname in bake_specs:
        smart_uv_unwrap(joined[mname])

    for dname in ("GunMetal", "Gold90s", "PolymerGrip"):
        joined[dname].hide_render = True

    ao_maps = {}
    for mname, res in bake_specs.items():
        print(f"  Baking Cycles AO ({res}x{res}) for [{mname}]...")
        ao_maps[mname] = bake_ao_for_object(joined[mname], res=res, samples=16)

    for dname in ("GunMetal", "Gold90s", "PolymerGrip"):
        joined[dname].hide_render = False

    print("=== [5/6] Compositing Procedural Wool Weave, Skin Shading & Iris Textures ===")
    img_crimson = make_wool_ao_texture("Tex_BlazerWoolAO", ao_maps["crimson"], weave_amp=0.055, ao_floor=0.64, save_name="tex_crimson_ao.png")
    img_lapel = make_wool_ao_texture("Tex_LapelWoolAO", ao_maps["lapel"], weave_amp=0.045, ao_floor=0.66, save_name="tex_lapel_ao.png")
    img_skin = make_skin_ao_texture("Tex_SkinAO", ao_maps["skin"], save_name="tex_skin_ao.png")
    img_iris = make_iris_texture("Tex_Iris", res=256, save_name="tex_iris.png")
    img_hair = make_hair_ao_texture("Tex_HairAO", ao_maps["hair"], save_name="tex_hair_ao.png")
    img_trousers = make_trousers_ao_texture("Tex_TrousersAO", ao_maps["trousers"], save_name="tex_trousers_ao.png")
    img_shirt = make_shirt_ao_texture("Tex_ShirtAO", ao_maps["shirt"], save_name="tex_shirt_ao.png")
    img_shoes = make_leather_ao_texture("Tex_ShoesAO", ao_maps["shoes"], save_name="tex_shoes_ao.png")

    # Deep rich 1990s crimson & calibrated Fair skin default factors!
    mats = {
        "crimson": make_material("crimson", (0.46, 0.022, 0.032), roughness=0.72, metallic=0.0, img=img_crimson),
        "lapel": make_material("lapel", (0.30, 0.012, 0.018), roughness=0.62, metallic=0.0, img=img_lapel),
        "skin": make_material("skin", (0.86, 0.65, 0.51), roughness=0.58, metallic=0.0, img=img_skin),
        "iris": make_material("iris", (0.36, 0.22, 0.13), roughness=0.18, metallic=0.0, img=img_iris),
        "sclera": make_material("sclera", (0.94, 0.92, 0.88), roughness=0.22, metallic=0.0),
        "pupil": make_material("pupil", (0.03, 0.03, 0.04), roughness=0.12, metallic=0.0),
        "catchlight": make_material("catchlight", (1.0, 0.99, 0.97), roughness=0.05, metallic=0.0),
        "hair": make_material("hair", (1.0, 1.0, 1.0), roughness=0.60, metallic=0.0, img=img_hair, specular_ior=0.22),
        "shirt": make_material("shirt", (1.0, 1.0, 1.0), roughness=0.68, metallic=0.0, img=img_shirt),
        "pocket_silk": make_material("pocket_silk", (0.95, 0.91, 0.80), roughness=0.45, metallic=0.0),
        "tie": make_material("tie", (0.055, 0.060, 0.070), roughness=0.36, metallic=0.05),
        "gold": make_material("gold", (0.88, 0.69, 0.28), roughness=0.26, metallic=0.88),
        "watch_dial": make_material("watch_dial", (0.94, 0.91, 0.82), roughness=0.25, metallic=0.0),
        "trousers": make_material("trousers", (1.0, 1.0, 1.0), roughness=0.76, metallic=0.0, img=img_trousers),
        "belt": make_material("belt", (0.09, 0.08, 0.08), roughness=0.38, metallic=0.05),
        "shoes": make_material("shoes", (1.0, 1.0, 1.0), roughness=0.24, metallic=0.08, img=img_shoes),
        "sole": make_material("sole", (0.08, 0.06, 0.05), roughness=0.68, metallic=0.0),
        "GunMetal": make_material("GunMetal", (0.18, 0.20, 0.22), roughness=0.4, metallic=0.8),
        "Gold90s": make_material("Gold90s", (0.85, 0.68, 0.25), roughness=0.3, metallic=0.9),
        "PolymerGrip": make_material("PolymerGrip", (0.10, 0.10, 0.12), roughness=0.7, metallic=0.0),
    }

    for mname, obj in joined.items():
        assign_mat(obj, mats[mname])

    # Measure EXACT crown hatband & nose bridge anchors BEFORE final join!
    hair_verts = [v.co for v in joined["hair"].data.vertices]
    skin_head_verts = [v.co for v in joined["skin"].data.vertices if v.co.z > 1.43]
    gold_verts = [v.co for v in joined["gold"].data.vertices]

    max_hair_z = max(p.z for p in hair_verts)
    min_hair_y = min(p.y for p in hair_verts)
    max_hair_y = max(p.y for p in hair_verts)
    max_hair_x = max(abs(p.x) for p in hair_verts)
    max_gold_z = max(p.z for p in gold_verts)
    eye_z = eye_data["eye_center_z"]

    # Measure actual nose bridge right at eye height (exclude nostrils/nose tip below!)
    bridge_v = [p for p in skin_head_verts if abs(p.x) < 0.012 and abs(p.z - eye_z) < 0.010 and p.y < -0.05]
    bridge_front_y = min(p.y for p in bridge_v) if bridge_v else eye_data["eye_front_y"]

    # Hatband anchor: ~5.6cm below top of hair (where hat brim rests snugly over forehead & temples!)
    hb_z = max_hair_z - 0.056
    hb_v = [p for p in (hair_verts + skin_head_verts) if abs(p.z - hb_z) < 0.016]
    hb_cy = (min(p.y for p in hb_v) + max(p.y for p in hb_v)) * 0.5 if hb_v else 0.0
    hb_rx = max(abs(p.x) for p in hb_v) if hb_v else 0.088
    hb_rz = (max(p.y for p in hb_v) - min(p.y for p in hb_v)) * 0.5 if hb_v else 0.102

    # Print per-part tri breakdown so we verify budget cleanly!
    print("=== TRIANGLE BREAKDOWN ===")
    for mname, obj in joined.items():
        tc = sum(max(1, len(p.vertices) - 2) for p in obj.data.polygons)
        if tc > 100:
            print(f"  {mname:14s}: {tc:6d} tris")

    hero = join_objects(list(joined.values()), "char_partner_90s")
    set_smooth(hero)

    bpy.context.view_layer.update()
    tri_count = sum(max(1, len(p.vertices) - 2) for p in hero.data.polygons)
    all_v = [v.co for v in hero.data.vertices]
    min_z, max_z = min(p.z for p in all_v), max(p.z for p in all_v)
    min_x, max_x = min(p.x for p in all_v), max(p.x for p in all_v)
    min_y, max_y = min(p.y for p in all_v), max(p.y for p in all_v)

    print("=== [6/6] Exporting char_partner_90s.glb ===")
    bpy.ops.object.select_all(action='DESELECT')
    hero.select_set(True)
    bpy.context.view_layer.objects.active = hero
    bpy.ops.export_scene.gltf(
        filepath=OUT_GLB,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_texcoords=True,
        export_normals=True,
        export_materials='EXPORT',
        export_image_format='AUTO',
    )

    # glTF Y-up (+Z forward): Blender (x, y, z) -> glTF (x, z, -y)
    hat_anchor_gltf = (0.0, round(hb_z, 4), round(-hb_cy, 4))
    eye_anchor_gltf = (0.0, round(eye_z + 0.002, 4), round(-(bridge_front_y - 0.008), 4))

    meta = {
        "tri_count": tri_count,
        "material_count": len(hero.data.materials),
        "materials": [m.name for m in hero.data.materials],
        "bounds_gltf": {
            "X": [round(min_x, 4), round(max_x, 4)],
            "Y": [round(min_z, 4), round(max_z, 4)],
            "Z": [round(-max_y, 4), round(-min_y, 4)],
        },
        "max_gold_Y_gltf": round(max_gold_z, 4),
        "hair_bounds_gltf": {
            "X": [-round(max_hair_x, 4), round(max_hair_x, 4)],
            "Y_max": round(max_hair_z, 4),
            "Z": [round(-max_hair_y, 4), round(-min_hair_y, 4)],
        },
        "hatband_radius": {"rx": round(hb_rx, 4), "rz": round(hb_rz, 4)},
        "HatAnchor_gltf": hat_anchor_gltf,
        "EyeAnchor_gltf": eye_anchor_gltf,
    }
    with open(os.path.join(PROJ, "scratch", "hero_v2_meta.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, indent=2)
    print("HERO_V2_META:", json.dumps(meta, indent=2))
    print("BUILD_HERO_V2_SUCCESS")


if __name__ == "__main__":
    main()
