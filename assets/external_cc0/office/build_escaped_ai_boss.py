"""v3 AAA SMOOTH ANATOMICAL MPFB2 CYBER-ANDROID BOSS -> assets/models/escaped_ai_boss.glb
ZERO blocky Minecraft boxes! Built directly over a real posed heroic athletic MPFB2 human body:
  - Preserves exact bounding height (Y=0 at soles, height=2.2365m) and +Z forward orientation (-Y in Blender)
  - Smooth biomechanical matte carbon-fiber musculature undersuit (`CarbonSubframe`)
  - Body-hugging sculpted curved armor plates extracted directly from real MPFB2 anatomical face zones
    (Smooth porcelain-white blind android head mask + glossy obsidian wrap-around visor band,
     sculpted porcelain-white pectoral cuirass, dark-titanium clavicle/abdomen/pelvis/gauntlets/boots,
     curved shoulder pauldrons, articulated knee/elbow chrome rotary rings, and deeply recessed
     glowing triangular `CrimsonCore` chest reactor!)
  - Dedicated per-material 256x256 24-ray BVHTree Baked Ambient Occlusion + Curvature Edge-Wear LUT textures
    (fixing the old v2 bug where a single near-white atlas overwrote every material's baseColor to white!)
"""
import math
import os
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Euler, Matrix, Vector
from mathutils.bvhtree import BVHTree

from bl_ext.blender_org.mpfb.services.humanservice import HumanService
from bl_ext.blender_org.mpfb.services.targetservice import TargetService

ROOT = Path(__file__).resolve().parents[3]
OUT_MODELS = ROOT / "assets/models"
TARGET_HEIGHT = 2.2365

MAT_ORDER = [
    "DarkArmor",
    "VisorObsidian",
    "WhiteCeramic",
    "CarbonSubframe",
    "CrimsonCore",
    "SatinTitanium",
    "PistonChrome",
]


def build_boss_lut_texture(key):
    """Per-material 256x256 2D AO+Curvature (u) x Micro-Grain (v) LUT calibrated for Raylib's `pow(base, 0.78)`."""
    img_name = f"BossLUT_{key}"
    existing = bpy.data.images.get(img_name)
    if existing is not None:
        return existing

    W, H = 256, 256
    arr = np.ones((H, W, 4), dtype=np.float32)
    rng = np.random.default_rng(2026 + sum(ord(c) for c in key))
    y, x = np.mgrid[0:H, 0:W].astype(np.float32)
    u = x / float(W - 1)

    ao_part = np.clip(u / 0.58, 0.0, 1.0)
    edge_part = np.clip((u - 0.58) / 0.42, 0.0, 1.0)
    shade = (0.18 + 0.82 * (ao_part ** 1.30)) * (1.0 + 0.65 * (edge_part ** 1.55))
    noise = rng.normal(0.0, 1.0, (H, W)).astype(np.float32)

    if key == "DarkArmor":
        # Menacing dark gunmetal-titanium corporate security exo-armor
        grain = 1.0 + 0.045 * np.sin(y * 1.1) + 0.025 * noise
        base = np.clip(0.026 * shade * grain, 0.004, 0.068)
        arr[:, :, 0] = base * 0.90
        arr[:, :, 1] = base * 0.96
        arr[:, :, 2] = base * 1.12
    elif key == "SatinTitanium":
        # Sleek brushed steel-grey titanium accents & segmented ribs/collar
        grain = 1.0 + 0.055 * np.sin(y * 1.35) + 0.028 * noise
        base = np.clip(0.088 * shade * grain, 0.015, 0.21)
        arr[:, :, 0] = base * 0.93
        arr[:, :, 1] = base * 0.98
        arr[:, :, 2] = base * 1.09
    elif key == "WhiteCeramic":
        # Clean satin porcelain-white carapace (Atomic Heart / I, Robot sleekness with deep crevice AO!)
        c_shade = (0.28 + 0.72 * (ao_part ** 1.25)) * (1.0 + 0.14 * (edge_part ** 1.4))
        grain = 1.0 + 0.012 * noise
        base = np.clip(0.62 * c_shade * grain, 0.12, 0.82)
        arr[:, :, 0] = base * 0.98
        arr[:, :, 1] = base * 0.99
        arr[:, :, 2] = base * 1.02
    elif key == "CarbonSubframe":
        # Matte woven charcoal-black biomechanical muscle fiber undersuit
        weave = 1.0 + 0.08 * np.sin(x * 1.2) * np.cos(y * 1.2) + 0.025 * noise
        base = np.clip(0.010 * shade * weave, 0.0018, 0.026)
        arr[:, :, 0] = base * 0.94
        arr[:, :, 1] = base * 0.97
        arr[:, :, 2] = base * 1.06
    elif key == "VisorObsidian":
        # Razor-glossy jet-black obsidian glass visor
        base = np.clip(0.0045 * (0.5 + 0.6 * shade), 0.001, 0.014)
        arr[:, :, 0] = base * 0.88
        arr[:, :, 1] = base * 0.94
        arr[:, :, 2] = base * 1.18
    elif key == "PistonChrome":
        # Bright machined chrome joints & core lugs
        grain = 1.0 + 0.04 * np.sin(y * 1.8) + 0.02 * noise
        base = np.clip(0.32 * (0.35 + 0.65 * shade) * grain, 0.08, 0.62)
        arr[:, :, 0] = base * 0.95
        arr[:, :, 1] = base * 0.98
        arr[:, :, 2] = base * 1.04
    elif key == "CrimsonCore":
        # Intense glowing crimson reactor plasma cell
        pulse = 0.86 + 0.14 * np.clip(u, 0.0, 1.0)
        arr[:, :, 0] = np.clip(0.96 * pulse, 0.65, 1.0)
        arr[:, :, 1] = np.clip(0.035 * pulse, 0.01, 0.08)
        arr[:, :, 2] = np.clip(0.025 * pulse, 0.01, 0.06)

    img = bpy.data.images.new(img_name, width=W, height=H, alpha=False)
    img.pixels.foreach_set(arr.ravel())
    img.pack()
    return img


MAT_PROPS = {
    # key: (metallic, roughness, emission_rgb_or_None)
    "DarkArmor":      (0.80, 0.34, None),
    "VisorObsidian":  (0.90, 0.10, None),
    "WhiteCeramic":   (0.08, 0.22, None),
    "CarbonSubframe": (0.35, 0.56, None),
    "CrimsonCore":    (0.00, 0.30, (1.00, 0.06, 0.03)),
    "SatinTitanium":  (0.86, 0.26, None),
    "PistonChrome":   (0.95, 0.15, None),
}


def init_materials():
    mats = {}
    for key in MAT_ORDER:
        met, rough, em = MAT_PROPS[key]
        m = bpy.data.materials.new(name=key)
        m.use_nodes = True
        m.diffuse_color = (1.0, 1.0, 1.0, 1.0)
        tree = m.node_tree
        bsdf = tree.nodes.get("Principled BSDF")
        bsdf.inputs["Base Color"].default_value = (1.0, 1.0, 1.0, 1.0)
        bsdf.inputs["Metallic"].default_value = met
        bsdf.inputs["Roughness"].default_value = rough
        if em is not None and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (*em, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 5.5
        tex = tree.nodes.new("ShaderNodeTexImage")
        tex.image = build_boss_lut_texture(key)
        tex.extension = "EXTEND"
        tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        mats[key] = m
    return mats


def apply_weighted_smooth(obj):
    for p in obj.data.polygons:
        p.use_smooth = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    wn = obj.modifiers.new("WeightedNormal", "WEIGHTED_NORMAL")
    wn.mode = "FACE_AREA"
    wn.weight = 50
    wn.keep_sharp = False
    bpy.ops.object.modifier_apply(modifier=wn.name)
    obj.select_set(False)
    return obj


def create_posed_heroic_mpfb_body():
    """Spawn heroic athletic MPFB2 humanoid, pose into imposing corporate-enforcer stance, scale to 2.2365m."""
    h = HumanService.create_human()
    try:
        TargetService.set_macro_info_dict(h, {
            "gender": 1.0, "age": 0.32, "muscle": 0.96,
            "weight": 0.44, "proportions": 0.72, "height": 0.75,
            "cupsize": 0.0, "firmness": 0.5,
            "race": {"asian": 0.1, "caucasian": 0.8, "african": 0.1},
        })
        TargetService.reapply_macro_details(h)
    except Exception:
        pass

    rig = HumanService.add_builtin_rig(h, "game_engine", import_weights=True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="POSE")

    def orient_bone(bname, target_dir):
        pb = rig.pose.bones[bname]
        b = rig.data.bones[bname]
        cur_dir = (b.tail_local - b.head_local).normalized()
        q_world = cur_dir.rotation_difference(target_dir.normalized())
        bone_mat = b.matrix_local.to_3x3()
        q_local = (bone_mat.inverted() @ q_world.to_matrix() @ bone_mat).to_quaternion()
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = q_local

    # Menacing powerful corporate-sentinel stance (broad shoulders, slightly abducted combat arms, planted feet)
    orient_bone("upperarm_l", Vector(( 0.22,  0.04, -0.97)))
    orient_bone("upperarm_r", Vector((-0.22,  0.04, -0.97)))
    orient_bone("thigh_l",    Vector(( 0.06, -0.02, -0.99)))
    orient_bone("thigh_r",    Vector((-0.06, -0.02, -0.99)))
    bpy.context.view_layer.update()

    # Slight aggressive forward elbow bend so arms look alive & ready (NOT stiff T/I-pose!)
    for bname, tdir in [
        ("lowerarm_l", Vector(( 0.06, -0.26, -0.96))),
        ("lowerarm_r", Vector((-0.06, -0.26, -0.96))),
        ("calf_l",     Vector(( 0.01,  0.03, -0.99))),
        ("calf_r",     Vector((-0.01,  0.03, -0.99))),
    ]:
        pb = rig.pose.bones[bname]
        cur_dir = (pb.tail - pb.head).normalized()
        q_delta = cur_dir.rotation_difference(tdir.normalized())
        mat_p = pb.matrix.to_3x3()
        q_loc = (mat_p.inverted() @ q_delta.to_matrix() @ mat_p).to_quaternion()
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = pb.rotation_quaternion @ q_loc
        bpy.context.view_layer.update()

    bpy.ops.object.mode_set(mode="OBJECT")

    deps = bpy.context.evaluated_depsgraph_get()
    eval_obj = h.evaluated_get(deps)
    mesh = bpy.data.meshes.new_from_object(eval_obj)
    body = bpy.data.objects.new("MPFB_BossBase", mesh)
    bpy.context.collection.objects.link(body)

    # Copy vertex groups for clean anatomical region extraction
    for vg in h.vertex_groups:
        body.vertex_groups.new(name=vg.name)
    for v in h.data.vertices:
        for g in v.groups:
            body.vertex_groups[g.group].add([v.index], g.weight, "REPLACE")

    # Normalize strictly to Z in [0.0, TARGET_HEIGHT], centered on X=0
    zs = [v.co.z for v in mesh.vertices]
    min_z, max_z = min(zs), max(zs)
    scale = TARGET_HEIGHT / max(0.5, max_z - min_z)
    for v in mesh.vertices:
        v.co = Vector((v.co.x * scale, v.co.y * scale, (v.co.z - min_z) * scale))

    # Extract key bone landmarks in scaled coordinates
    def bpos(bname, tail=False):
        pb = rig.pose.bones[bname]
        co = pb.tail if tail else pb.head
        return Vector((co.x * scale, co.y * scale, (co.z - min_z) * scale))

    joints = {
        "head":      bpos("head"),
        "neck":      bpos("neck_01"),
        "chest":     bpos("spine_03"),
        "spine2":    bpos("spine_02"),
        "pelvis":    bpos("pelvis"),
        "sh_l":      bpos("upperarm_l"),
        "sh_r":      bpos("upperarm_r"),
        "elb_l":     bpos("lowerarm_l"),
        "elb_r":     bpos("lowerarm_r"),
        "wri_l":     bpos("hand_l"),
        "wri_r":     bpos("hand_r"),
        "hip_l":     bpos("thigh_l"),
        "hip_r":     bpos("thigh_r"),
        "knee_l":    bpos("calf_l"),
        "knee_r":    bpos("calf_r"),
        "ankle_l":   bpos("foot_l"),
        "ankle_r":   bpos("foot_r"),
    }

    bpy.data.objects.remove(h, do_unlink=True)
    bpy.data.objects.remove(rig, do_unlink=True)
    return body, joints


def extract_armor_shell(body_obj, name, face_predicate, mat, thickness=0.016, offset=1.0, smooth_iters=4, bevel_w=0.0045):
    """Extract a smooth body-hugging sculpted armor carapace plate directly from the real MPFB2 surface!"""
    src_mesh = body_obj.data
    bm = bmesh.new()
    bm.from_mesh(src_mesh)
    bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()

    # Delete faces that don't match the anatomical armor zone
    to_del = [f for f in bm.faces if not face_predicate(f.calc_center_median(), f.normal)]
    if len(to_del) >= len(bm.faces):
        bm.free()
        return None
    bmesh.ops.delete(bm, geom=to_del, context="FACES")

    # Remove tiny disconnected stray islands (< 10 faces)
    visited = set()
    islands = []
    for f in bm.faces:
        if f in visited:
            continue
        comp = []
        stack = [f]
        visited.add(f)
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf not in visited:
                        visited.add(nf)
                        stack.append(nf)
        islands.append(comp)
    stray = [f for comp in islands if len(comp) < 12 for f in comp]
    if stray:
        bmesh.ops.delete(bm, geom=stray, context="FACES")
    if len(bm.faces) == 0:
        bm.free()
        return None

    # Clean smooth boundary & surface fairing so armor plates look sleek and precision-molded
    if smooth_iters > 0:
        bmesh.ops.smooth_vert(bm, verts=list(bm.verts), factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for _ in range(smooth_iters - 1):
            bmesh.ops.smooth_vert(bm, verts=list(bm.verts), factor=0.45, use_axis_x=True, use_axis_y=True, use_axis_z=True)

    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(mat)

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)

    sol = obj.modifiers.new("Solidify", "SOLIDIFY")
    sol.thickness = thickness
    sol.offset = offset
    sol.use_even_offset = False
    sol.use_quality_normals = True
    bpy.ops.object.modifier_apply(modifier=sol.name)

    if bevel_w > 0.0:
        bev = obj.modifiers.new("Bevel", "BEVEL")
        bev.width = min(bevel_w, thickness * 0.35)
        bev.segments = 2
        bev.profile = 0.55
        bev.limit_method = "ANGLE"
        bev.angle_limit = math.radians(42.0)
        bev.use_clamp_overlap = True
        bpy.ops.object.modifier_apply(modifier=bev.name)

    obj.data.validate(verbose=False)
    obj.select_set(False)
    return apply_weighted_smooth(obj)


def add_ring_joint(name, center, axis_dir, radius, tube_r, mat, segs=28):
    """Sleek machined titanium/chrome rotary joint ring at elbow/knee/collar/waist."""
    bpy.ops.mesh.primitive_torus_add(
        major_segments=segs, minor_segments=14,
        major_radius=radius, minor_radius=tube_r,
        location=center,
    )
    obj = bpy.context.active_object
    obj.name = name
    q = Vector((0.0, 0.0, 1.0)).rotation_difference(Vector(axis_dir).normalized())
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = q
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    obj.data.materials.append(mat)
    return apply_weighted_smooth(obj)


def add_crimson_delta_core(chest_front_y, chest_z, mats):
    """Iconic deeply recessed triangular Crimson Core chest reactor + dark-titanium bezel + 3 chrome lugs!"""
    created = []
    # 1) Outer beveled dark-titanium triangular housing hugging the sternum
    bm_bez = bmesh.new()
    # Downward-pointing corporate delta triangle on front chest (-Y is forward in Blender!)
    r_out = 0.118
    r_in  = 0.078
    y_back = chest_front_y + 0.025
    y_front = chest_front_y - 0.026
    angles = [math.radians(90 + 180), math.radians(90 + 180 + 120), math.radians(90 + 180 + 240)]
    # Point down: -90 deg, +30 deg, +150 deg in (X, Z)
    pts_out = [(r_out * math.cos(a), r_out * math.sin(a)) for a in (-math.pi / 2, math.pi / 6, 5 * math.pi / 6)]
    pts_in  = [(r_in  * math.cos(a), r_in  * math.sin(a)) for a in (-math.pi / 2, math.pi / 6, 5 * math.pi / 6)]

    # Build thick chamfered triangular frame
    ob = [bm_bez.verts.new((px, y_back,  chest_z + pz)) for px, pz in pts_out]
    of = [bm_bez.verts.new((px * 0.94, y_front, chest_z + pz * 0.94)) for px, pz in pts_out]
    ib = [bm_bez.verts.new((px, y_back,  chest_z + pz)) for px, pz in pts_in]
    if_ = [bm_bez.verts.new((px, y_front - 0.002, chest_z + pz)) for px, pz in pts_in]
    for i in range(3):
        ni = (i + 1) % 3
        bm_bez.faces.new((ob[i], ob[ni], of[ni], of[i]))
        bm_bez.faces.new((of[i], of[ni], if_[ni], if_[i]))
        bm_bez.faces.new((if_[i], if_[ni], ib[ni], ib[i]))
        bm_bez.faces.new((ib[i], ib[ni], ob[ni], ob[i]))
    bmesh.ops.recalc_face_normals(bm_bez, faces=list(bm_bez.faces))
    m_bez = bpy.data.meshes.new("CoreBezel")
    bm_bez.to_mesh(m_bez)
    bm_bez.free()
    o_bez = bpy.data.objects.new("CoreBezel", m_bez)
    bpy.context.collection.objects.link(o_bez)
    o_bez.data.materials.append(mats["DarkArmor"])
    bpy.context.view_layer.objects.active = o_bez
    o_bez.select_set(True)
    bev = o_bez.modifiers.new("Bevel", "BEVEL")
    bev.width = 0.0055
    bev.segments = 3
    bev.use_clamp_overlap = True
    bpy.ops.object.modifier_apply(modifier=bev.name)
    o_bez.select_set(False)
    apply_weighted_smooth(o_bez)
    created.append(o_bez)

    # 2) Recessed glowing triangular CrimsonCore plasma plate
    bm_core = bmesh.new()
    cf = [bm_core.verts.new((px * 1.02, y_front + 0.008, chest_z + pz * 1.02)) for px, pz in pts_in]
    cb = [bm_core.verts.new((px * 1.02, y_back,          chest_z + pz * 1.02)) for px, pz in pts_in]
    bm_core.faces.new(list(reversed(cf)))
    bm_core.faces.new(cb)
    for i in range(3):
        ni = (i + 1) % 3
        bm_core.faces.new((cf[i], cf[ni], cb[ni], cb[i]))
    bmesh.ops.recalc_face_normals(bm_core, faces=list(bm_core.faces))
    m_core = bpy.data.meshes.new("CrimsonReactor")
    bm_core.to_mesh(m_core)
    bm_core.free()
    o_core = bpy.data.objects.new("CrimsonReactor", m_core)
    bpy.context.collection.objects.link(o_core)
    o_core.data.materials.append(mats["CrimsonCore"])
    apply_weighted_smooth(o_core)
    created.append(o_core)

    # 3) 3 Machined Chrome Corner Lugs anchoring the triangular reactor
    for i, (px, pz) in enumerate(pts_out):
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=20, radius=0.014, depth=0.022,
            location=(px * 0.86, y_front + 0.002, chest_z + pz * 0.86),
            rotation=(math.pi / 2.0, 0.0, 0.0),
        )
        lug = bpy.context.active_object
        lug.name = f"CoreChromeLug_{i}"
        lug.data.materials.append(mats["PistonChrome"])
        apply_weighted_smooth(lug)
        created.append(lug)

    return created


def build_boss_v3():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = init_materials()
    body, j = create_posed_heroic_mpfb_body()

    # Smooth the raw MPFB2 face features slightly ONLY on the head region so the android has a
    # chilling, smooth, featureless blind porcelain android head while keeping sharp cheekbones & jaw!
    head_z = j["head"].z
    neck_z = j["neck"].z
    chest_z = j["chest"].z
    spine2_z = j["spine2"].z
    pelvis_z = j["pelvis"].z
    knee_z = (j["knee_l"].z + j["knee_r"].z) * 0.5
    ankle_z = (j["ankle_l"].z + j["ankle_r"].z) * 0.5
    sh_x = abs(j["sh_l"].x)
    sh_z = j["sh_l"].z
    elb_z = (j["elb_l"].z + j["elb_r"].z) * 0.5
    wri_z = (j["wri_l"].z + j["wri_r"].z) * 0.5

    # Measure front chest Y surface on the real MPFB2 torso
    chest_pts = [v.co.y for v in body.data.vertices if abs(v.co.x) < 0.08 and abs(v.co.z - (chest_z + 0.02)) < 0.06 and v.co.y < 0]
    chest_front_y = min(chest_pts) if chest_pts else -0.175

    # Smooth out eyes/mouth/nose depressions on head vertices of a dedicated smooth head clone
    # so the porcelain faceplate looks sleek, sculpted, and blind like a high-end cyber-android!
    bm_head_src = bmesh.new()
    bm_head_src.from_mesh(body.data)
    head_verts = [v for v in bm_head_src.verts if v.co.z > neck_z + 0.02]
    for _ in range(14):
        bmesh.ops.smooth_vert(bm_head_src, verts=head_verts, factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    head_src_mesh = bpy.data.meshes.new("SmoothHeadSrc")
    bm_head_src.to_mesh(head_src_mesh)
    bm_head_src.free()
    head_src_obj = bpy.data.objects.new("SmoothHeadSrc", head_src_mesh)
    bpy.context.collection.objects.link(head_src_obj)

    parts = []

    # -------------------------------------------------------------------------
    # 1. FULL BIOMECHANICAL CARBON-FIBER MUSCLE UNDERSUIT (Directly from MPFB2!)
    # -------------------------------------------------------------------------
    undersuit_mesh = body.data.copy()
    undersuit = bpy.data.objects.new("CarbonUndersuit", undersuit_mesh)
    bpy.context.collection.objects.link(undersuit)
    undersuit.data.materials.clear()
    undersuit.data.materials.append(mats["CarbonSubframe"])
    # Slightly inset undersuit by 3mm on non-head body so armor plates sit razor-clean with zero z-fighting
    for v in undersuit.data.vertices:
        if v.co.z < neck_z + 0.01:
            v.co -= v.normal * 0.003
    # Mild smooth for sleek synthetic muscle fiber look
    bpy.context.view_layer.objects.active = undersuit
    undersuit.select_set(True)
    sm = undersuit.modifiers.new("Smooth", "SMOOTH")
    sm.factor = 0.45
    sm.iterations = 3
    bpy.ops.object.modifier_apply(modifier=sm.name)
    undersuit.select_set(False)
    apply_weighted_smooth(undersuit)
    parts.append(undersuit)

    # -------------------------------------------------------------------------
    # 2. SLEEK BLIND WHITE-PORCELAIN ANDROID HEAD MASK + GLOSSY OBSIDIAN VISOR!
    # -------------------------------------------------------------------------
    # Main sleek sculpted porcelain helmet/faceplate
    o = extract_armor_shell(
        head_src_obj, "PorcelainHeadShell",
        lambda c, n: c.z > neck_z + 0.045,
        mats["WhiteCeramic"], thickness=0.011, offset=1.0, smooth_iters=3, bevel_w=0.003,
    )
    if o:
        parts.append(o)

    # Razor-sleek wrap-around Glossy Obsidian Visor Band across upper eyes & temples
    visor_z = head_z + 0.075
    o = extract_armor_shell(
        head_src_obj, "ObsidianVisorBand",
        lambda c, n: (visor_z - 0.036 < c.z < visor_z + 0.036) and (c.y < 0.025),
        mats["VisorObsidian"], thickness=0.016, offset=1.0, smooth_iters=3, bevel_w=0.003,
    )
    if o:
        parts.append(o)

    # Dark Titanium Armored Neck Collar / Gorget
    o = extract_armor_shell(
        body, "TitaniumCollar",
        lambda c, n: (neck_z - 0.06 < c.z < neck_z + 0.03) and abs(c.x) < 0.19,
        mats["DarkArmor"], thickness=0.018, offset=1.0, smooth_iters=3, bevel_w=0.004,
    )
    if o:
        parts.append(o)

    # -------------------------------------------------------------------------
    # 3. SCULPTED ANATOMICAL TORSO CARAPACE (PORCELAIN CHEST + TITANIUM RIBS/SPINE)
    # -------------------------------------------------------------------------
    # Heroic Porcelain-White Upper Pectoral Cuirass (hugging real MPFB2 chest!)
    o = extract_armor_shell(
        body, "PorcelainChestCuirass",
        lambda c, n: (chest_z - 0.14 < c.z < sh_z + 0.04) and abs(c.x) < sh_x - 0.035 and c.y < 0.02,
        mats["WhiteCeramic"], thickness=0.022, offset=1.0, smooth_iters=4, bevel_w=0.0055,
    )
    if o:
        parts.append(o)

    # Dark-Titanium Upper Back & Scapula Carapace
    o = extract_armor_shell(
        body, "DarkBackCarapace",
        lambda c, n: (spine2_z - 0.04 < c.z < sh_z + 0.06) and abs(c.x) < sh_x - 0.025 and c.y >= -0.01,
        mats["DarkArmor"], thickness=0.022, offset=1.0, smooth_iters=4, bevel_w=0.005,
    )
    if o:
        parts.append(o)

    # Segmented Satin-Titanium Abdominal Armor Plates (front torso between chest & waist)
    o = extract_armor_shell(
        body, "TitaniumAbdomen",
        lambda c, n: (pelvis_z + 0.06 < c.z < chest_z - 0.13) and abs(c.x) < 0.22 and c.y < 0.02,
        mats["SatinTitanium"], thickness=0.016, offset=1.0, smooth_iters=3, bevel_w=0.004,
    )
    if o:
        parts.append(o)

    # Dark-Titanium Armored Pelvis / Waist Harness
    o = extract_armor_shell(
        body, "DarkPelvisArmor",
        lambda c, n: (pelvis_z - 0.13 < c.z < pelvis_z + 0.07) and abs(c.x) < 0.25,
        mats["DarkArmor"], thickness=0.018, offset=1.0, smooth_iters=3, bevel_w=0.0045,
    )
    if o:
        parts.append(o)

    # Glowing Triangular Crimson Core embedded seamlessly on the sternum!
    parts.extend(add_crimson_delta_core(chest_front_y - 0.014, chest_z + 0.025, mats))

    # Dorsal Spine Chrome Vertebral Nodes (sleek anatomical spine accents along back!)
    for idx in range(5):
        vz = spine2_z + 0.02 + idx * 0.072
        back_pts = [v.co.y for v in body.data.vertices if abs(v.co.x) < 0.04 and abs(v.co.z - vz) < 0.03 and v.co.y > 0]
        by = (max(back_pts) + 0.022) if back_pts else 0.165
        bpy.ops.mesh.primitive_cylinder_add(
            vertices=20, radius=0.022, depth=0.056,
            location=(0.0, by, vz), rotation=(0.0, math.pi / 2.0, 0.0),
        )
        node = bpy.context.active_object
        node.name = f"SpineNode_{idx}"
        node.data.materials.append(mats["SatinTitanium"] if idx % 2 == 0 else mats["PistonChrome"])
        apply_weighted_smooth(node)
        parts.append(node)

    # -------------------------------------------------------------------------
    # 4. CURVED SHOULDER PAULDRONS + SLEEK ANATOMICAL ARM & GAUNTLET ARMOR
    # -------------------------------------------------------------------------
    for side_tag, sgn in (("L", 1.0), ("R", -1.0)):
        sh_p = j["sh_l"] if sgn > 0 else j["sh_r"]
        elb_p = j["elb_l"] if sgn > 0 else j["elb_r"]

        # Broad Curved Porcelain-White Deltoid Pauldron hugging real MPFB2 shoulder!
        o = extract_armor_shell(
            body, f"Pauldron_{side_tag}",
            lambda c, n, s=sgn, sp=sh_p: (c.x * s > sh_x - 0.06) and (sp.z - 0.14 < c.z < sp.z + 0.12),
            mats["WhiteCeramic"], thickness=0.026, offset=1.0, smooth_iters=4, bevel_w=0.0055,
        )
        if o:
            parts.append(o)

        # Dark-Titanium Upper Arm Shell
        o = extract_armor_shell(
            body, f"BicepArmor_{side_tag}",
            lambda c, n, s=sgn, sp=sh_p, ep=elb_p: (c.x * s > sh_x - 0.02) and (ep.z + 0.06 < c.z < sp.z - 0.13),
            mats["DarkArmor"], thickness=0.015, offset=1.0, smooth_iters=3, bevel_w=0.004,
        )
        if o:
            parts.append(o)

        # Sleek Sculpted Satin-Titanium & Dark Forearm Gauntlets (ZERO Minecraft blocks!)
        o = extract_armor_shell(
            body, f"ForearmGauntlet_{side_tag}",
            lambda c, n, s=sgn, ep=elb_p: (c.x * s > sh_x - 0.02) and (wri_z + 0.02 < c.z < ep.z - 0.03),
            mats["DarkArmor"], thickness=0.017, offset=1.0, smooth_iters=3, bevel_w=0.004,
        )
        if o:
            parts.append(o)

        # White-Ceramic Forearm Dorsal Crest Plate (outer forearm contrast!)
        o = extract_armor_shell(
            body, f"ForearmCrest_{side_tag}",
            lambda c, n, s=sgn, ep=elb_p: (c.x * s > sh_x + 0.05) and (wri_z + 0.05 < c.z < ep.z - 0.06) and (n.x * s > 0.25),
            mats["WhiteCeramic"], thickness=0.022, offset=1.0, smooth_iters=3, bevel_w=0.0035,
        )
        if o:
            parts.append(o)

        # Armored Hand Backplate
        o = extract_armor_shell(
            body, f"HandArmor_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > sh_x - 0.04) and (wri_z - 0.14 < c.z < wri_z + 0.025) and (n.x * s > 0.15),
            mats["SatinTitanium"], thickness=0.008, offset=1.0, smooth_iters=2, bevel_w=0.002,
        )
        if o:
            parts.append(o)

        # Sleek Chrome Rotary Elbow Ring
        parts.append(add_ring_joint(f"ElbowRing_{side_tag}", elb_p, (0.0, 0.2, 1.0), 0.064, 0.011, mats["PistonChrome"]))

    # -------------------------------------------------------------------------
    # 5. SCULPTED ANATOMICAL THIGH PLATES, KNEE CAPS, SHIN GREAVES & BOOTS
    # -------------------------------------------------------------------------
    for side_tag, sgn in (("L", 1.0), ("R", -1.0)):
        kp = j["knee_l"] if sgn > 0 else j["knee_r"]

        # Porcelain-White Outer Sculpted Thigh Plate
        o = extract_armor_shell(
            body, f"ThighPlateOuter_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > 0.04) and (knee_z + 0.10 < c.z < pelvis_z - 0.10) and (c.y < 0.06),
            mats["WhiteCeramic"], thickness=0.020, offset=1.0, smooth_iters=4, bevel_w=0.005,
        )
        if o:
            parts.append(o)

        # Dark-Titanium Rear Thigh Armor
        o = extract_armor_shell(
            body, f"ThighPlateBack_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > 0.03) and (knee_z + 0.12 < c.z < pelvis_z - 0.12) and (c.y >= 0.04),
            mats["DarkArmor"], thickness=0.016, offset=1.0, smooth_iters=3, bevel_w=0.004,
        )
        if o:
            parts.append(o)

        # Sculpted Satin-Titanium Kneecap
        o = extract_armor_shell(
            body, f"KneeCap_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > 0.04) and (knee_z - 0.075 < c.z < knee_z + 0.095) and (c.y < -0.01),
            mats["SatinTitanium"], thickness=0.024, offset=1.0, smooth_iters=3, bevel_w=0.0045,
        )
        if o:
            parts.append(o)

        # Sleek Contoured Dark-Titanium Shin Greaves
        o = extract_armor_shell(
            body, f"ShinGreave_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > 0.03) and (ankle_z + 0.05 < c.z < knee_z - 0.065),
            mats["DarkArmor"], thickness=0.018, offset=1.0, smooth_iters=4, bevel_w=0.0045,
        )
        if o:
            parts.append(o)

        # Porcelain-White Front Shin Crest
        o = extract_armor_shell(
            body, f"ShinCrest_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > 0.05) and (ankle_z + 0.08 < c.z < knee_z - 0.09) and (c.y < -0.025),
            mats["WhiteCeramic"], thickness=0.023, offset=1.0, smooth_iters=3, bevel_w=0.004,
        )
        if o:
            parts.append(o)

        # Sculpted Tactical Cyber-Boot Shell (ZERO Minecraft block feet!)
        o = extract_armor_shell(
            body, f"CyberBoot_{side_tag}",
            lambda c, n, s=sgn: (c.x * s > 0.02) and (0.012 < c.z < ankle_z + 0.065),
            mats["DarkArmor"], thickness=0.016, offset=1.0, smooth_iters=3, bevel_w=0.004,
        )
        if o:
            parts.append(o)

        # Chrome Knee Mechanical Ring
        parts.append(add_ring_joint(f"KneeRing_{side_tag}", kp + Vector((0.0, 0.01, 0.0)), (0.0, 0.0, 1.0), 0.076, 0.010, mats["PistonChrome"]))

    # Remove temporary source meshes
    bpy.data.objects.remove(body, do_unlink=True)
    bpy.data.objects.remove(head_src_obj, do_unlink=True)

    # -------------------------------------------------------------------------
    # 6. JOIN ALL PARTS + NORMALIZE EXACT HEIGHT (Y=0..2.2365m) + 24-RAY BVH AO!
    # -------------------------------------------------------------------------
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    boss = bpy.context.active_object
    boss.name = "EscapedAIBoss_v3"
    mesh = boss.data

    # Ensure soles sit strictly at Z=0.0 and total height is exactly TARGET_HEIGHT (2.2365m)
    zs = [v.co.z for v in mesh.vertices]
    lo_z, hi_z = min(zs), max(zs)
    sc = TARGET_HEIGHT / max(0.5, hi_z - lo_z)
    for v in mesh.vertices:
        # Subtle heroic V-taper broadening above waist so sentinel silhouette reads commanding!
        z_norm = (v.co.z - lo_z) * sc
        v_taper = 1.0 + 0.14 * max(0.0, min(1.0, (z_norm - 1.10) / 0.72)) * (1.0 if z_norm < 1.92 else 0.35)
        v.co = Vector((v.co.x * sc * v_taper, v.co.y * sc, z_norm))
    mesh.validate(verbose=False)

    # Build BVHTree & bake 24-ray hemisphere Ambient Occlusion + Curvature Edge-Wear into UV.u!
    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bmesh.ops.triangulate(bm_ao, faces=bm_ao.faces)
    bvh = BVHTree.FromBMesh(bm_ao)
    bm_ao.free()

    n_rays = 24
    golden = math.pi * (3.0 - math.sqrt(5.0))
    local_dirs = []
    for k in range(n_rays):
        zc = (k + 0.5) / n_rays
        r_h = math.sqrt(max(0.0, 1.0 - zc * zc))
        theta = golden * k
        local_dirs.append(Vector((math.cos(theta) * r_h, math.sin(theta) * r_h, zc)).normalized())

    verts = mesh.vertices
    v_coords = [v.co.copy() for v in verts]
    neighbors = [[] for _ in range(len(verts))]
    for edge in mesh.edges:
        a, c = edge.vertices[0], edge.vertices[1]
        neighbors[a].append(c)
        neighbors[c].append(a)

    max_dist = 0.28
    vert_u = [0.58] * len(verts)
    vert_col = [1.0] * len(verts)

    for idx, v in enumerate(verts):
        p = v.co
        n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0.0, 0.0, 1.0))
        up = Vector((0.0, 0.0, 1.0)) if abs(n.z) < 0.9 else Vector((1.0, 0.0, 0.0))
        t1 = n.cross(up).normalized()
        t2 = n.cross(t1).normalized()

        origin = p + n * 0.0025
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
            mean_dot = acc / len(neighbors[idx])
            curv = max(-0.18, min(0.24, -mean_dot * 0.55))

        vert_u[idx] = min(0.97, max(0.04, 0.10 + 0.54 * (ao ** 1.25) + curv))
        vert_col[idx] = min(1.0, max(0.26, 0.26 + 0.74 * (ao ** 1.18)))

    while mesh.uv_layers:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UVMap")

    while mesh.color_attributes:
        mesh.color_attributes.remove(mesh.color_attributes[0])
    col_attr = mesh.color_attributes.new(name="Col", type="BYTE_COLOR", domain="CORNER")

    crimson_idx = next((i for i, m in enumerate(mesh.materials) if m and m.name == "CrimsonCore"), -1)
    for poly in mesh.polygons:
        poly.use_smooth = True
        is_core = (poly.material_index == crimson_idx)
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            v_co = verts[vi].co
            tu = 0.92 if is_core else vert_u[vi]
            tv = min(0.98, max(0.02, v_co.z / TARGET_HEIGHT))
            uv_layer.data[li].uv = (tu, tv)
            c = 1.0 if is_core else vert_col[vi]
            col_attr.data[li].color = (c, c, c, 1.0)

    mesh.update()

    # Report exact bounds
    xs = [v.co.x for v in verts]
    ys = [v.co.y for v in verts]
    zs = [v.co.z for v in verts]
    print(f"Boss v3 bounds: X=[{min(xs):.3f},{max(xs):.3f}] Y=[{min(ys):.3f},{max(ys):.3f}] Z=[{min(zs):.3f},{max(zs):.3f}] Polys={len(mesh.polygons)} Mats={len(mesh.materials)}")

    out_path = OUT_MODELS / "escaped_ai_boss.glb"
    bpy.ops.export_scene.gltf(
        filepath=str(out_path),
        export_format="GLB",
        export_normals=True,
        export_texcoords=True,
        export_attributes=True,
        export_active_vertex_color_when_no_material=True,
        export_vertex_color="ACTIVE",
        export_animations=False,
    )
    print("Exported AAA Anatomical MPFB2 escaped_ai_boss.glb:", out_path, os.path.getsize(out_path))


if __name__ == "__main__":
    build_boss_v3()
