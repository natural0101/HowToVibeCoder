import bpy
import bmesh
import math
import os
import shutil
import numpy as np
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

ROOT = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
SRC_GLB = os.path.join(ROOT, r"scratch\gen3d\hunyuan_hero_shape.glb")
CONCEPT_IMG = r"C:\Users\se-20\.gemini\antigravity\brain\50fc13b9-24ea-4541-8bcc-1ae74f353f5a\hero_concept_front_square.png"
WORK_DIR = os.path.join(ROOT, r"scratch\gen3d")
OUT_DIRS = [
    os.path.join(ROOT, r"assets\models"),
    os.path.join(ROOT, r"bin\Release\net10.0\assets\models"),
    os.path.join(ROOT, r"bin\Debug\net10.0\assets\models"),
    os.path.join(ROOT, r"bin\Release\net10.0\win-x64\assets\models"),
]

TARGET_HEIGHT = 1.745

# Right shoulder pivot & hand grip in Blender Z-up (converted to glTF Y-up on report)
# glTF(x, y, z) = Blender(x, z, -y)
RIGHT_SHOULDER_PIVOT_BL = Vector((-0.194, 0.052, 1.352))
RIGHT_HAND_PALM_BL = Vector((-0.258, -0.032, 0.802))
AIM_PITCH_DEG = -76.0

# Exact anchors for hats & eyewear in Blender Z-up (converted to glTF Y-up on report)
EYE_ANCHOR_BL = Vector((0.0, -0.103, 1.586))
HAT_ANCHOR_BL = Vector((0.0, 0.012, 1.690))


def enable_cycles_gpu(samples=32):
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = samples
    scene.cycles.use_adaptive_sampling = False
    scene.cycles.use_denoising = False
    prefs = bpy.context.preferences.addons['cycles'].preferences
    for backend in ('OPTIX', 'CUDA'):
        try:
            prefs.compute_device_type = backend
            prefs.get_devices()
            gpu_found = False
            for d in prefs.devices:
                if d.type != 'CPU':
                    d.use = True
                    gpu_found = True
                else:
                    d.use = False
            if gpu_found:
                scene.cycles.device = 'GPU'
                print(f"[CYCLES] Using GPU backend: {backend}")
                return
        except Exception:
            pass
    scene.cycles.device = 'CPU'
    print("[CYCLES] Fallback to CPU")


def load_and_normalize_high():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=SRC_GLB)
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(objs) == 1, f"Expected 1 mesh in {SRC_GLB}, got {len(objs)}"
    obj = objs[0]
    obj.name = "HunyuanHigh"
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    verts = np.array([list(v.co) for v in obj.data.vertices], dtype=np.float64)
    min_z, max_z = verts[:, 2].min(), verts[:, 2].max()
    scale = TARGET_HEIGHT / (max_z - min_z)
    head = verts[(verts[:, 2] - min_z) * scale > 1.50]
    hcx = 0.5 * (head[:, 0].min() + head[:, 0].max())
    hcy = 0.5 * (head[:, 1].min() + head[:, 1].max())

    for v in obj.data.vertices:
        v.co.x = (v.co.x - hcx) * scale
        v.co.y = (v.co.y - hcy) * scale
        v.co.z = (v.co.z - min_z) * scale
    obj.data.update()

    for p in obj.data.polygons:
        p.use_smooth = True
    print(f"[HIGH] Normalized: verts={len(obj.data.vertices)} polys={len(obj.data.polygons)} Z=[0, {TARGET_HEIGHT}]")
    return obj


def bisect_local(bm, filter_fn, plane_co, plane_no):
    """Slice ONLY faces matching filter_fn along an exact plane for razor-straight material seams!"""
    bm.faces.ensure_lookup_table()
    faces = [f for f in bm.faces if filter_fn(f.calc_center_median())]
    if not faces:
        return
    edges = list({e for f in faces for e in f.edges})
    verts = list({v for f in faces for v in f.verts})
    bmesh.ops.bisect_plane(
        bm,
        geom=faces + edges + verts,
        dist=0.0001,
        plane_co=Vector(plane_co),
        plane_no=Vector(plane_no).normalized(),
        use_snap_center=False,
        clear_outer=False,
        clear_inner=False,
    )


def build_decimated_low(high_obj, target_tris=30500):
    low_obj = high_obj.copy()
    low_obj.data = high_obj.data.copy()
    low_obj.name = "HunyuanLow"
    bpy.context.collection.objects.link(low_obj)

    vg = low_obj.vertex_groups.new(name="DetailKeep")
    for v in low_obj.data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        w = 0.44
        if z > 1.47:
            w = 0.72
        elif z > 1.02 and y < 0.03 and abs(x) < 0.20:
            w = 0.68
        elif abs(x) > 0.195 and 0.715 < z < 0.91:
            w = 0.74
        elif z < 0.14:
            w = 0.42
        vg.add([v.index], w, 'REPLACE')

    bpy.ops.object.select_all(action='DESELECT')
    low_obj.select_set(True)
    bpy.context.view_layer.objects.active = low_obj

    cur_polys = len(low_obj.data.polygons)
    ratio = min(0.95, target_tris / max(1, cur_polys))
    mod = low_obj.modifiers.new(name="Decimate", type='DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = ratio
    mod.vertex_group = "DetailKeep"
    mod.invert_vertex_group = True
    mod.vertex_group_factor = 0.35
    mod.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="Decimate")

    bm = bmesh.new()
    bm.from_mesh(low_obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0002)

    # --- RAZOR-STRAIGHT TAILORED SEAM CUTS VIA LOCAL BISECT_PLANE (ZERO SAWTOOTH ZIGZAGS!) ---
    # 1. Blazer bottom hem (Z=0.818) on torso only (|x| < 0.205)
    bisect_local(bm, lambda c: abs(c.x) < 0.205 and 0.78 < c.z < 0.85, (0, 0, 0.818), (0, 0, 1))
    # 2. Left & Right sleeve hem (Z=0.896) & shirt cuff bottom (Z=0.874) on wrists only
    bisect_local(bm, lambda c: abs(c.x) > 0.195 and 0.84 < c.z < 0.93, (0, 0, 0.896), (0, 0, 1))
    bisect_local(bm, lambda c: abs(c.x) > 0.195 and 0.84 < c.z < 0.92, (0, 0, 0.874), (0, 0, 1))
    # 3. Shirt V-collar left & right slanted edges (from (0, 0, 1.262) up to (±0.064, 0, 1.458))
    # Slope dx/dz = 0.064 / 0.196 = 0.3265 -> normal (+1, 0, -0.3265) and (-1, 0, -0.3265)
    bisect_local(bm, lambda c: c.y < 0.02 and 1.24 < c.z < 1.47 and -0.02 < c.x < 0.10, (0.004, 0, 1.262), (1.0, 0, -0.3265))
    bisect_local(bm, lambda c: c.y < 0.02 and 1.24 < c.z < 1.47 and -0.10 < c.x < 0.02, (-0.004, 0, 1.262), (-1.0, 0, -0.3265))
    # 4. Neck collar rim (Z=1.466 front/back)
    bisect_local(bm, lambda c: abs(c.x) < 0.095 and 1.44 < c.z < 1.50, (0, 0, 1.468), (0, 0, 1))
    # 5. Windsor tie left & right edges inside V (clean tapered blade!)
    bisect_local(bm, lambda c: c.y < 0.0 and 1.25 < c.z < 1.445 and -0.04 < c.x < 0.04, (0.019, 0, 1.350), (1.0, 0, 0.04))
    bisect_local(bm, lambda c: c.y < 0.0 and 1.25 < c.z < 1.445 and -0.04 < c.x < 0.04, (-0.019, 0, 1.350), (-1.0, 0, 0.04))
    # 6. Front hairline cut (Z=1.647) & back nape cut (Z=1.542)
    bisect_local(bm, lambda c: c.y < 0.01 and abs(c.x) < 0.085 and 1.62 < c.z < 1.67, (0, 0, 1.647), (0, 0, 1))
    bisect_local(bm, lambda c: c.y > 0.025 and 1.51 < c.z < 1.58, (0, 0, 1.542), (0, 0, 1))
    # 7. Shoe/trouser break front (Z=0.078)
    bisect_local(bm, lambda c: 0.04 < c.z < 0.11, (0, -0.05, 0.078), (0, 0.22, 1.0))
    # 8. Right armhole cut plane (clean straight tailored shoulder seam at X = -0.164!)
    bisect_local(bm, lambda c: 1.12 < c.z < 1.46 and -0.22 < c.x < -0.11, (-0.164, 0.05, 1.30), (1.0, 0, -0.03))

    # Triangulate all polygons created by bisect_plane & remove tiny slivers
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00015)
    bm.normal_update()
    bm.to_mesh(low_obj.data)
    bm.free()

    for p in low_obj.data.polygons:
        p.use_smooth = True
    low_obj.data.update()
    print(f"[LOW] Decimated + Seam-Bisected: verts={len(low_obj.data.vertices)} polys={len(low_obj.data.polygons)}")
    return low_obj


def segment_materials(low_obj):
    """
    Razor-sharp segmentation aligned strictly with our bisect_plane seam cuts!
    Material slots:
      0: crimson
      1: lapel
      2: skin
      3: hair
      4: shirt
      5: tie
      6: pocket_square
      7: trousers
      8: shoes
    """
    MAT_NAMES = [
        "crimson", "lapel", "skin", "hair",
        "shirt", "tie", "pocket_square", "trousers", "shoes"
    ]
    mat_idx = {n: i for i, n in enumerate(MAT_NAMES)}
    low_obj.data.materials.clear()
    for n in MAT_NAMES:
        m = bpy.data.materials.get(n) or bpy.data.materials.new(n)
        low_obj.data.materials.append(m)

    mesh = low_obj.data
    n_polys = len(mesh.polygons)
    labels = np.zeros(n_polys, dtype=np.int32)

    def point_in_poly(px_v, pz_v, poly):
        inside = False
        j = len(poly) - 1
        for i in range(len(poly)):
            xi, zi = poly[i]
            xj, zj = poly[j]
            if ((zi > pz_v) != (zj > pz_v)) and (px_v < (xj - xi) * (pz_v - zi) / (zj - zi + 1e-12) + xi):
                inside = not inside
            j = i
        return inside

    lapel_right_negX = [
        (-0.070, 1.466), (-0.112, 1.425), (-0.148, 1.358), (-0.145, 1.332),
        (-0.118, 1.292), (-0.072, 1.210), (-0.025, 1.155), (-0.006, 1.182),
        (-0.036, 1.268), (-0.058, 1.375), (-0.062, 1.455)
    ]
    lapel_left_posX = [
        (0.070, 1.466), (0.112, 1.425), (0.148, 1.358), (0.145, 1.332),
        (0.118, 1.292), (0.056, 1.202), (-0.022, 1.115), (-0.038, 1.138),
        (0.015, 1.230), (0.052, 1.355), (0.062, 1.455)
    ]
    flap_right = [(-0.168, 1.015), (-0.088, 1.015), (-0.088, 0.968), (-0.168, 0.968)]
    flap_left = [(0.088, 1.015), (0.168, 1.015), (0.168, 0.968), (0.088, 0.968)]

    for i, poly in enumerate(mesh.polygons):
        c = poly.center
        n = poly.normal
        x, y, z = c.x, c.y, c.z
        front_facing = (y < 0.025) and (n.y < 0.35)

        # 1. SHOES (aligned to bisect plane (0, -0.05, 0.078), normal (0, 0.22, 1.0))
        if (z - 0.078) + 0.22 * (y + 0.05) < 0.0:
            labels[i] = mat_idx["shoes"]
            continue

        # 2. HANDS & SHIRT CUFFS — strictly z in [0.715, 0.905] & |x| > 0.196, aligned to bisect planes Z=0.874 & Z=0.896!
        if 0.715 < z < 0.905 and abs(x) > 0.196:
            if z < 0.874:
                labels[i] = mat_idx["skin"]
            elif z < 0.896:
                labels[i] = mat_idx["shirt"]
            else:
                labels[i] = mat_idx["crimson"]
            continue

        # 3. TROUSERS (aligned strictly to bisect plane Z=0.818!)
        if z < 0.818:
            labels[i] = mat_idx["trousers"]
            continue

        # 4. HEAD & NECK (above neck collar bisect Z=1.468)
        if z >= 1.468:
            # Clean sculpted Hair vs Skin (aligned to bisect Z=1.647 front & Z=1.542 back!)
            if z >= 1.647:
                labels[i] = mat_idx["hair"]
                continue
            if y > 0.032 and z >= 1.542:
                labels[i] = mat_idx["hair"]
                continue
            if y >= -0.015 and z > 1.606:
                labels[i] = mat_idx["hair"]
                continue
            if abs(x) > 0.068 and y > -0.045 and z > 1.592:
                labels[i] = mat_idx["hair"]
                continue
            if abs(x) > 0.072 and -0.028 < y < 0.012 and 1.568 < z < 1.595:
                labels[i] = mat_idx["hair"]
                continue
            # Sculpted swoop hair lock dipping over left forehead
            if y < -0.114 and 0.002 < x < 0.045 and z > 1.628:
                labels[i] = mat_idx["hair"]
                continue
            labels[i] = mat_idx["skin"]
            continue

        # 5. CRISP BISECT-ALIGNED SHIRT V & WINDSOR TIE (front torso Z in [1.262, 1.468])
        if front_facing and z >= 1.262 and abs(x) < 0.090:
            # Exact bisect plane equation: |x| < 0.004 + 0.3265 * (z - 1.262)
            v_half_w = 0.004 + 0.3265 * (z - 1.262)
            if abs(x) < v_half_w:
                # Top neck skin triangle peeking above collar points (Z > 1.448, |x| < 0.020)
                if z > 1.450 and abs(x) < 0.020:
                    labels[i] = mat_idx["skin"]
                    continue
                # Crisp bisect-aligned Windsor tie blade (|x| < 0.019 - 0.04*(z - 1.350))
                tie_w = 0.019 - 0.04 * (z - 1.350)
                if z < 1.444 and abs(x) < min(tie_w, v_half_w - 0.001):
                    labels[i] = mat_idx["tie"]
                    continue
                labels[i] = mat_idx["shirt"]
                continue

        # Back shirt collar rim peeking cleanly above back jacket collar (Z in [1.454, 1.468], |x| < 0.078)
        if z >= 1.454 and abs(x) < 0.078 and y >= 0.005:
            labels[i] = mat_idx["shirt"]
            continue

        # 6. PEAKED LAPELS & POCKET FLAPS
        if front_facing and (
            point_in_poly(x, z, lapel_right_negX)
            or point_in_poly(x, z, lapel_left_posX)
            or point_in_poly(x, z, flap_right)
            or point_in_poly(x, z, flap_left)
        ):
            labels[i] = mat_idx["lapel"]
            continue

        if 1.430 < z < 1.454 and abs(x) < 0.095 and y > 0.005:
            labels[i] = mat_idx["lapel"]
            continue

        # 7. DEFAULT: BLAZER BODY & SLEEVES
        labels[i] = mat_idx["crimson"]

    # Clean tiny isolated 1-poly speckles
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    for _pass in range(2):
        new_labels = labels.copy()
        for f in bm.faces:
            fi = f.index
            my_l = labels[fi]
            nbr_ls = [labels[nf.index] for e in f.edges for nf in e.link_faces if nf.index != fi]
            if nbr_ls and sum(1 for nl in nbr_ls if nl == my_l) == 0:
                vals, cnts = np.unique(nbr_ls, return_counts=True)
                new_labels[fi] = vals[np.argmax(cnts)]
        labels = new_labels
    bm.free()

    for i, poly in enumerate(mesh.polygons):
        poly.material_index = int(labels[i])

    counts = {MAT_NAMES[k]: int((labels == k).sum()) for k in range(len(MAT_NAMES))}
    print(f"[SEGMENT] Poly counts: {counts}")
    return MAT_NAMES


def unwrap_dual_atlas(low_obj):
    bpy.ops.object.select_all(action='DESELECT')
    low_obj.select_set(True)
    bpy.context.view_layer.objects.active = low_obj

    if not low_obj.data.uv_layers:
        low_obj.data.uv_layers.new(name="UVMap")

    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in low_obj.data.polygons:
        p.select = (p.material_index == 2)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(56), island_margin=0.008, correct_aspect=True, scale_to_bounds=True)

    bpy.ops.mesh.select_all(action='INVERT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(58), island_margin=0.005, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    print("[UV] Dual-region non-overlapping Smart UV Project complete")


def make_top_relief_material():
    mat = bpy.data.materials.new("_BakeTopRelief")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")

    vec_dot = nt.nodes.new("ShaderNodeVectorMath")
    vec_dot.operation = 'DOT_PRODUCT'
    vec_dot.inputs[1].default_value = (0.22, -0.46, 0.86)
    nt.links.new(geo.outputs["Normal"], vec_dot.inputs[0])

    map_nd = nt.nodes.new("ShaderNodeMapRange")
    map_nd.inputs["From Min"].default_value = -1.0
    map_nd.inputs["From Max"].default_value = 1.0
    map_nd.inputs["To Min"].default_value = 0.44
    map_nd.inputs["To Max"].default_value = 1.00
    nt.links.new(vec_dot.outputs["Value"], map_nd.inputs["Value"])

    nt.links.new(map_nd.outputs["Result"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def make_ao_source_material(name, distance):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = 24
    ao.only_local = True
    ao.inputs["Distance"].default_value = distance
    nt.links.new(ao.outputs["AO"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def make_pos_source_material():
    mat = bpy.data.materials.new("_BakePos")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])

    for axis, lo, hi in [("X", -0.40, 0.40), ("Y", -0.25, 0.25), ("Z", 0.0, 1.80)]:
        mr = nt.nodes.new("ShaderNodeMapRange")
        mr.inputs["From Min"].default_value = lo
        mr.inputs["From Max"].default_value = hi
        mr.inputs["To Min"].default_value = 0.0
        mr.inputs["To Max"].default_value = 1.0
        nt.links.new(sep.outputs[axis], mr.inputs["Value"])
        nt.links.new(mr.outputs["Result"], comb.inputs[axis])

    nt.links.new(comb.outputs["Vector"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def bake_pass(high_obj, low_obj, src_mat, img_name, size, face_filter=None):
    low_obj.hide_render = True
    low_obj.hide_viewport = True
    high_obj.hide_render = False
    high_obj.hide_viewport = False

    high_obj.data.materials.clear()
    high_obj.data.materials.append(src_mat)

    tmp_obj = low_obj.copy()
    tmp_obj.data = low_obj.data.copy()
    tmp_obj.name = f"_BakeTarget_{img_name}"
    tmp_obj.hide_render = False
    tmp_obj.hide_viewport = False
    bpy.context.collection.objects.link(tmp_obj)

    if face_filter is not None:
        bm = bmesh.new()
        bm.from_mesh(tmp_obj.data)
        to_del = [f for f in bm.faces if not face_filter(f.material_index)]
        if to_del:
            bmesh.ops.delete(bm, geom=to_del, context='FACES')
        bm.to_mesh(tmp_obj.data)
        bm.free()

    bake_img = bpy.data.images.new(img_name, width=size, height=size, alpha=False, float_buffer=True)
    bake_img.colorspace_settings.name = 'Non-Color'
    tmat = bpy.data.materials.new(f"_TmpMat_{img_name}")
    tmat.use_nodes = True
    nt = tmat.node_tree
    nt.nodes.clear()
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bake_img
    tex.select = True
    nt.nodes.active = tex

    tmp_obj.data.materials.clear()
    tmp_obj.data.materials.append(tmat)
    for p in tmp_obj.data.polygons:
        p.material_index = 0

    bpy.ops.object.select_all(action='DESELECT')
    high_obj.select_set(True)
    tmp_obj.select_set(True)
    bpy.context.view_layer.objects.active = tmp_obj

    bpy.context.scene.render.bake.use_selected_to_active = True
    bpy.context.scene.render.bake.cage_extrusion = 0.012
    bpy.context.scene.render.bake.max_ray_distance = 0.04
    bpy.context.scene.render.bake.margin = 16
    bpy.context.scene.render.bake.margin_type = 'ADJACENT_FACES'
    bpy.ops.object.bake(type='EMIT')

    arr = np.array(bake_img.pixels[:], dtype=np.float32).reshape((size, size, 4))[:, :, :3].copy()

    bpy.data.objects.remove(tmp_obj, do_unlink=True)
    bpy.data.materials.remove(tmat, do_unlink=True)
    bpy.data.images.remove(bake_img, do_unlink=True)
    low_obj.hide_render = False
    low_obj.hide_viewport = False
    print(f"[BAKE] {img_name} ({size}x{size}) min={arr.min():.3f} mean={arr.mean():.3f} max={arr.max():.3f}")
    return arr


def box_blur_2d(arr, r=4):
    """Fast pure-numpy separable box blur for clean image-space sculptural crease unsharp mask!"""
    k = 2 * r + 1
    p = np.pad(arr, ((r, r), (r, r)), mode='edge')
    cs = np.cumsum(p, axis=0)
    v = (cs[k:, :] - cs[:-k, :]) / k
    # restore shape along axis 0
    v = np.pad(v, ((1, 0), (0, 0)), mode='edge')[:arr.shape[0], :]
    cs2 = np.cumsum(v, axis=1)
    h = (cs2[:, k:] - cs2[:, :-k]) / k
    h = np.pad(h, ((0, 0), (1, 0)), mode='edge')[:, :arr.shape[1]]
    return h


def save_png(arr_rgb, path):
    h, w, _ = arr_rgb.shape
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[:, :, :3] = np.clip(arr_rgb, 0.0, 1.0)
    img = bpy.data.images.new(os.path.basename(path), width=w, height=h, alpha=False)
    img.pixels[:] = rgba.flatten()
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img, do_unlink=True)
    print(f"[SAVE] {path}")


def generate_baked_textures(high_obj, low_obj):
    enable_cycles_gpu(samples=32)

    mat_ao_tight = make_ao_source_material("_BakeAOTight", 0.085)
    mat_ao_wide = make_ao_source_material("_BakeAOWide", 0.34)
    mat_relief = make_top_relief_material()
    mat_pos = make_pos_source_material()

    # --- 1. SUIT / HAIR / TROUSERS / SHOES 2048x2048 RICH SCULPTURAL AO + CREASE POP + FINE WOOL TWILL ---
    suit_ao_t = bake_pass(high_obj, low_obj, mat_ao_tight, "suit_ao_t", 2048, face_filter=lambda m: m != 2)[:, :, 0]
    suit_ao_w = bake_pass(high_obj, low_obj, mat_ao_wide, "suit_ao_w", 2048, face_filter=lambda m: m != 2)[:, :, 0]
    suit_rel = bake_pass(high_obj, low_obj, mat_relief, "suit_rel", 2048, face_filter=lambda m: m != 2)[:, :, 0]

    # Mask valid baked UV islands (non-zero in rel) so unsharp mask never creates dark borders at gutters!
    valid_suit = suit_rel > 0.05
    # Fill unused gutter pixels with warm-bright 0.90 so shoulder caps / mipmaps NEVER bleed black!
    suit_ao_t_f = np.where(valid_suit, suit_ao_t, 0.92)
    suit_ao_w_f = np.where(valid_suit, suit_ao_w, 0.92)
    suit_rel_f = np.where(valid_suit, suit_rel, 0.88)

    # Local-contrast sculptural fold pop (unsharp mask on relief + tight AO!)
    rel_blur = box_blur_2d(suit_rel_f, r=8)
    crease_pop = np.clip((suit_rel_f - rel_blur) * 1.45, -0.18, 0.16)

    ao_mix = np.clip(0.45 * suit_ao_t_f + 0.30 * suit_ao_w_f + 0.25 * suit_rel_f, 0.0, 1.0)
    # Rich Arcane-grade cloth contrast: deep lapel/elbow crevices ~0.36, tailored crests ~1.00!
    suit_luma = np.clip(0.36 + 0.66 * np.power(ao_mix, 1.42) + crease_pop, 0.34, 1.00)

    # Subtle tailored worsted-wool twill micro-grain
    yy, xx = np.mgrid[0:2048, 0:2048].astype(np.float32)
    twill = 0.012 * np.sin((xx + yy) * 1.55) + 0.007 * np.cos((xx - yy) * 2.25)
    suit_luma = np.where(valid_suit, np.clip(suit_luma + twill, 0.34, 1.00), 0.90)
    suit_rgb = np.stack([suit_luma, suit_luma, suit_luma], axis=-1)
    suit_tex_path = os.path.join(WORK_DIR, "hero_suit_ao_2k.png")
    save_png(suit_rgb, suit_tex_path)

    # Find a guaranteed bright valid texel UV for shoulder cap polygons (zero black gutter sampling!)
    bright_ys, bright_xs = np.where((suit_luma > 0.88) & valid_suit)
    if len(bright_xs) > 0:
        mid = len(bright_xs) // 2
        cap_uv = (float(bright_xs[mid]) / 2048.0, float(bright_ys[mid]) / 2048.0)
    else:
        cap_uv = (0.5, 0.5)
    print(f"[UV CAP] Bright suit UV sampled at {cap_uv}")

    # --- 2. SKIN 1024x1024 NEUTRAL-BRIGHT PAINTERLY SCULPTED AO + FACE/HAND DETAIL ---
    skin_ao_t = bake_pass(high_obj, low_obj, mat_ao_tight, "skin_ao_t", 1024, face_filter=lambda m: m == 2)[:, :, 0]
    skin_ao_w = bake_pass(high_obj, low_obj, mat_ao_wide, "skin_ao_w", 1024, face_filter=lambda m: m == 2)[:, :, 0]
    skin_rel = bake_pass(high_obj, low_obj, mat_relief, "skin_rel", 1024, face_filter=lambda m: m == 2)[:, :, 0]
    skin_pos = bake_pass(high_obj, low_obj, mat_pos, "skin_pos", 1024, face_filter=lambda m: m == 2)

    valid_skin = skin_rel > 0.05
    s_ao_t = np.where(valid_skin, skin_ao_t, 0.95)
    s_ao_w = np.where(valid_skin, skin_ao_w, 0.95)
    s_rel = np.where(valid_skin, skin_rel, 0.90)
    s_rel_blur = box_blur_2d(s_rel, r=5)
    s_pop = np.clip((s_rel - s_rel_blur) * 1.15, -0.10, 0.10)

    wx = skin_pos[:, :, 0] * 0.80 - 0.40
    wy = skin_pos[:, :, 1] * 0.50 - 0.25
    wz = skin_pos[:, :, 2] * 1.80

    s_ao = np.clip(0.42 * s_ao_t + 0.30 * s_ao_w + 0.28 * s_rel, 0.0, 1.0)
    base_l = np.clip(0.60 + 0.42 * np.power(s_ao, 1.25) + s_pop, 0.54, 1.00)
    skin_rgb = np.stack([base_l, base_l, base_l], axis=-1)

    is_front_face = (wz > 1.48) & (wy < -0.02) & valid_skin

    # Sculpted warm natural masculine lips + subtle confident smirk crease
    lip_d = np.sqrt((wx / 0.025) ** 2 + ((wz - 1.528) / 0.0085) ** 2)
    lip_mask = np.clip(1.0 - lip_d, 0.0, 1.0) ** 1.35 * is_front_face
    lip_line = np.exp(-((wz - 1.5285 - 0.002 * np.maximum(0.0, wx / 0.022)) / 0.0020) ** 2) * np.clip(1.0 - (wx / 0.024) ** 2, 0.0, 1.0) * is_front_face
    skin_rgb[:, :, 0] *= (1.0 - 0.08 * lip_mask - 0.18 * lip_line)
    skin_rgb[:, :, 1] *= (1.0 - 0.22 * lip_mask - 0.26 * lip_line)
    skin_rgb[:, :, 2] *= (1.0 - 0.24 * lip_mask - 0.26 * lip_line)

    # Warm cheekbone / nose-tip / brow blood-flow life
    cheek_l = np.exp(-(((wx - 0.042) / 0.028) ** 2 + ((wz - 1.564) / 0.020) ** 2))
    cheek_r = np.exp(-(((wx + 0.042) / 0.028) ** 2 + ((wz - 1.564) / 0.020) ** 2))
    nose_tip = np.exp(-((wx / 0.014) ** 2 + ((wz - 1.553) / 0.012) ** 2))
    warm_mask = np.clip(cheek_l + cheek_r + 0.60 * nose_tip, 0.0, 1.0) * is_front_face
    skin_rgb[:, :, 0] *= (1.0 + 0.04 * warm_mask)
    skin_rgb[:, :, 1] *= (1.0 - 0.04 * warm_mask)
    skin_rgb[:, :, 2] *= (1.0 - 0.065 * warm_mask)

    # Masculine 5-o'clock jaw/chin shadow
    jaw_z = np.clip(1.0 - np.abs(wz - 1.515) / 0.030, 0.0, 1.0)
    jaw_x = np.clip(1.0 - (np.abs(wx) / 0.062) ** 2, 0.0, 1.0)
    stubble = jaw_z * jaw_x * (1.0 - lip_mask) * is_front_face * 0.11
    skin_rgb[:, :, 0] *= (1.0 - stubble * 1.18)
    skin_rgb[:, :, 1] *= (1.0 - stubble * 1.06)
    skin_rgb[:, :, 2] *= (1.0 - stubble * 0.90)

    # Soft eye-socket orbital shading framing our 3D eyes
    eye_l = np.exp(-(((wx - 0.0320) / 0.017) ** 2 + ((wz - 1.5865) / 0.010) ** 2))
    eye_r = np.exp(-(((wx + 0.0320) / 0.017) ** 2 + ((wz - 1.5865) / 0.010) ** 2))
    eye_sh = np.clip(eye_l + eye_r, 0.0, 1.0) * is_front_face * 0.28
    skin_rgb *= (1.0 - eye_sh[:, :, None])

    # Subtle knuckle/finger crease warmth on hands
    is_hand = (wz < 0.875) & (np.abs(wx) > 0.19) & valid_skin
    knuckle_warm = is_hand * (1.0 - s_ao) * 0.18
    skin_rgb[:, :, 0] *= (1.0 + 0.03 * knuckle_warm)
    skin_rgb[:, :, 1] *= (1.0 - 0.07 * knuckle_warm)
    skin_rgb[:, :, 2] *= (1.0 - 0.10 * knuckle_warm)

    skin_rgb = np.where(valid_skin[:, :, None], np.clip(skin_rgb, 0.35, 1.00), 0.92)
    skin_tex_path = os.path.join(WORK_DIR, "hero_skin_detail_1k.png")
    save_png(skin_rgb, skin_tex_path)

    # --- 3. RADIAL IRIS 256x256 ---
    N = 256
    gy, gx = np.mgrid[0:N, 0:N].astype(np.float32)
    ux = (gx / (N - 1)) * 2.0 - 1.0
    uy = (gy / (N - 1)) * 2.0 - 1.0
    rad = np.sqrt(ux * ux + uy * uy)
    ang = np.arctan2(uy, ux)
    fibers = 0.78 + 0.14 * np.sin(ang * 24.0 + rad * 6.0) + 0.08 * np.cos(ang * 42.0 - rad * 10.0)
    limbal = 1.0 - 0.75 * np.clip((rad - 0.68) / 0.32, 0.0, 1.0) ** 1.4
    collarette = 1.0 + 0.24 * np.exp(-((rad - 0.42) / 0.14) ** 2)
    iris_val = np.clip(fibers * limbal * collarette, 0.10, 1.0)
    top_sh = 1.0 - 0.38 * np.clip((uy - 0.18) / 0.82, 0.0, 1.0)
    iris_val *= top_sh
    iris_rgb = np.stack([iris_val, iris_val, iris_val], axis=-1)
    iris_tex_path = os.path.join(WORK_DIR, "hero_iris_256.png")
    save_png(iris_rgb, iris_tex_path)

    for m in (mat_ao_tight, mat_ao_wide, mat_relief, mat_pos):
        bpy.data.materials.remove(m, do_unlink=True)

    return suit_tex_path, skin_tex_path, iris_tex_path, cap_uv


def build_surface_bvh(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    return bvh


def surface_point_front(bvh, x, z, y_start=-0.35):
    loc, norm, idx, dist = bvh.ray_cast(Vector((x, y_start, z)), Vector((0.0, 1.0, 0.0)), 0.70)
    return loc, norm


def add_sculpted_accessories(high_obj, cap_uv):
    bvh = build_surface_bvh(high_obj)
    created = []

    def new_obj_from_bm(bm, name, mat_name, is_right_arm=False, planar_uv=False):
        me = bpy.data.meshes.new(name)
        bm.normal_update()
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        uv_layer = me.uv_layers.new(name="UVMap")
        if planar_uv:
            xs = [v.co.x for v in me.vertices]
            zs = [v.co.z for v in me.vertices]
            cx = 0.5 * (min(xs) + max(xs))
            cz = 0.5 * (min(zs) + max(zs))
            rad = max(1e-5, 0.5 * max(max(xs) - min(xs), max(zs) - min(zs)))
            for poly in me.polygons:
                for li in poly.loop_indices:
                    v = me.vertices[me.loops[li].vertex_index].co
                    uv_layer.data[li].uv = (0.5 + (v.x - cx) / (2.0 * rad), 0.5 + (v.z - cz) / (2.0 * rad))
        else:
            for poly in me.polygons:
                for li in poly.loop_indices:
                    uv_layer.data[li].uv = cap_uv
        m = bpy.data.materials.get(mat_name) or bpy.data.materials.new(mat_name)
        me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        ob["is_right_arm"] = is_right_arm
        bpy.context.collection.objects.link(ob)
        created.append(ob)
        return ob

    # --- 1. CHARISMATIC 3D EYES + CRISP UPPER EYELID LASH FRAMING ---
    for side_tag, sx in [("L", +0.0320), ("R", -0.0320)]:
        ez = 1.5860
        hit, nrm = surface_point_front(bvh, sx, ez)
        ey_front = (hit.y if hit else -0.098) - 0.0005
        ey_center = ey_front + 0.0114
        yaw_out = math.radians(-6.0 if sx > 0 else 6.0)
        rot_eye = Euler((0.0, 0.0, yaw_out), 'XYZ').to_matrix().to_4x4()
        base_m = Matrix.Translation((sx, ey_center, ez)) @ rot_eye

        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=0.0114)
        for v in bm.verts:
            v.co.z *= 0.78
            v.co.x *= 1.08
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyeSclera_{side_tag}", "sclera")

        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=22, v_segments=12, radius=0.0065)
        for v in bm.verts:
            v.co.y *= 0.28
            v.co.y -= 0.0104
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyeIris_{side_tag}", "iris", planar_uv=True)

        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=0.0031)
        for v in bm.verts:
            v.co.y *= 0.26
            v.co.y -= 0.0113
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyePupil_{side_tag}", "pupil")

        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=8, radius=0.0011)
        for v in bm.verts:
            v.co.x += 0.0020
            v.co.z += 0.0018
            v.co.y *= 0.35
            v.co.y -= 0.0119
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyeCatchlight_{side_tag}", "catchlight")

        # Confident upper eyelid lash rim framing top 25% of iris
        bm = bmesh.new()
        lash_rings = []
        sgn_x = 1.0 if sx > 0 else -1.0
        for k in range(11):
            t = -1.0 + 2.0 * (k / 10.0)
            lx = t * 0.0128
            lz = 0.0038 - 0.0034 * (t * t) + (0.0008 * t * sgn_x)
            ly = -math.sqrt(max(0.0001, 0.0120 ** 2 - lx * lx * 0.65)) - 0.0007
            thick = 0.0016 * (1.0 - 0.30 * (t * t))
            ring = []
            for j in range(6):
                a = j * 2.0 * math.pi / 6.0
                p_loc = Vector((lx, ly + math.cos(a) * thick, lz + math.sin(a) * thick * 0.85))
                ring.append(bm.verts.new(base_m @ p_loc))
            lash_rings.append(ring)
        for i in range(len(lash_rings) - 1):
            for j in range(6):
                jn = (j + 1) % 6
                bm.faces.new((lash_rings[i][j], lash_rings[i][jn], lash_rings[i + 1][jn], lash_rings[i + 1][j]))
        new_obj_from_bm(bm, f"UpperLash_{side_tag}", "brow")

    # --- 2. CRISP SCULPTED MASCULINE EYEBROWS ---
    for side_tag, sgn in [("L", 1.0), ("R", -1.0)]:
        bm = bmesh.new()
        pts = [
            (0.011, 1.6000, 0.0042),
            (0.019, 1.6022, 0.0046),
            (0.028, 1.6050, 0.0048),
            (0.037, 1.6072, 0.0046),
            (0.046, 1.6082, 0.0038),
            (0.054, 1.6060, 0.0026),
            (0.060, 1.6022, 0.0014),
        ]
        rings = []
        for px_a, pz_a, rad_z in pts:
            rx = sgn * px_a
            hit, nrm = surface_point_front(bvh, rx, pz_a)
            base_p = hit if hit else Vector((rx, -0.112, pz_a))
            c_p = base_p + Vector((0.0, -0.0014, 0.0))
            ring = []
            for k in range(8):
                ang = k * 2.0 * math.pi / 8.0
                dy = math.cos(ang) * 0.0022
                dz = math.sin(ang) * rad_z
                ring.append(bm.verts.new((c_p.x, c_p.y + dy, c_p.z + dz)))
            rings.append(ring)
        for i in range(len(rings) - 1):
            for k in range(8):
                kn = (k + 1) % 8
                bm.faces.new((rings[i][k], rings[i][kn], rings[i + 1][kn], rings[i + 1][k]))
        bm.faces.new(rings[0][::-1])
        bm.faces.new(rings[-1])
        new_obj_from_bm(bm, f"Eyebrow_{side_tag}", "brow")

    # --- 3. CRISP SCULPTED FOLDED SILK POCKET SQUARE ---
    hit_ps, nrm_ps = surface_point_front(bvh, 0.095, 1.300)
    if hit_ps:
        bm = bmesh.new()
        base_ps = hit_ps + nrm_ps.normalized() * 0.0025
        for dx, dz, w_p, h_p, dy_o in [(-0.010, 0.000, 0.024, 0.022, 0.0), (0.009, 0.002, 0.020, 0.028, -0.0015)]:
            b0 = bm.verts.new(base_ps + Vector((dx - w_p, dy_o, dz)))
            b1 = bm.verts.new(base_ps + Vector((dx + w_p, dy_o, dz)))
            t0 = bm.verts.new(base_ps + Vector((dx + 0.003, dy_o - 0.002, dz + h_p)))
            b0b = bm.verts.new(base_ps + Vector((dx - w_p, dy_o + 0.004, dz)))
            b1b = bm.verts.new(base_ps + Vector((dx + w_p, dy_o + 0.004, dz)))
            t0b = bm.verts.new(base_ps + Vector((dx + 0.003, dy_o + 0.002, dz + h_p)))
            bm.faces.new((b0, b1, t0))
            bm.faces.new((b1b, b0b, t0b))
            bm.faces.new((b0, t0, t0b, b0b))
            bm.faces.new((b1, b1b, t0b, t0))
            bm.faces.new((b0, b0b, b1b, b1))
        new_obj_from_bm(bm, "SilkPocketSquare", "pocket_square")

    # --- 4. SIX BEVELED ANTIQUE GOLD BLAZER BUTTONS ---
    button_coords = [
        (-0.078, 1.212), (+0.072, 1.212),
        (-0.040, 1.118), (+0.038, 1.118),
        (-0.038, 1.028), (+0.038, 1.028),
    ]
    for bi, (bx, bz) in enumerate(button_coords):
        hit, nrm = surface_point_front(bvh, bx, bz)
        if not hit:
            continue
        bm = bmesh.new()
        bmesh.ops.create_cone(
            bm, cap_ends=True, cap_tris=False, segments=20,
            radius1=0.0110, radius2=0.0094, depth=0.0048
        )
        rot = Vector((0.0, 0.0, 1.0)).rotation_difference(nrm.normalized()).to_matrix().to_4x4()
        pos = hit + nrm.normalized() * 0.0018
        mat_x = Matrix.Translation(pos) @ rot
        for v in bm.verts:
            v.co = mat_x @ v.co
        new_obj_from_bm(bm, f"GoldButton_{bi}", "gold")

    # --- 5. ANTIQUE GOLD TIE BAR ---
    tz = 1.320
    hit, nrm = surface_point_front(bvh, 0.0, tz)
    if hit:
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co.x *= 0.040
            v.co.y *= 0.005
            v.co.z *= 0.0052
            v.co += Vector((0.0, hit.y - 0.0024, tz))
        new_obj_from_bm(bm, "GoldTieBar", "gold")

    # --- 6. SUBTLE ANTIQUE GOLD COLLAR CHAIN (snug under collar points, Z < 1.46!) ---
    bm = bmesh.new()
    chain_rings = []
    for k in range(17):
        t = -1.0 + 2.0 * (k / 16.0)
        cx_c = t * 0.046
        cz_c = 1.422 + 0.034 * (t * t)
        hit, nrm = surface_point_front(bvh, cx_c, cz_c)
        cy_c = (hit.y if hit else -0.052) - 0.0018
        r_c = 0.0021
        ring = []
        for j in range(6):
            a = j * 2.0 * math.pi / 6.0
            ring.append(bm.verts.new((cx_c, cy_c + math.cos(a) * r_c, cz_c + math.sin(a) * r_c)))
        chain_rings.append(ring)
    for i in range(len(chain_rings) - 1):
        for j in range(6):
            jn = (j + 1) % 6
            bm.faces.new((chain_rings[i][j], chain_rings[i][jn], chain_rings[i + 1][jn], chain_rings[i + 1][j]))
    new_obj_from_bm(bm, "GoldCollarChain", "gold")

    # --- 7. LEFT-WRIST ANTIQUE GOLD WATCH (+X wrist) ---
    l_wrist_verts = [v.co for v in high_obj.data.vertices if v.co.x > 0.20 and 0.862 < v.co.z < 0.878]
    if l_wrist_verts:
        wx_c = 0.5 * (min(v.x for v in l_wrist_verts) + max(v.x for v in l_wrist_verts))
        wy_c = 0.5 * (min(v.y for v in l_wrist_verts) + max(v.y for v in l_wrist_verts))
        rx_w = 0.5 * (max(v.x for v in l_wrist_verts) - min(v.x for v in l_wrist_verts)) + 0.0025
        ry_w = 0.5 * (max(v.y for v in l_wrist_verts) - min(v.y for v in l_wrist_verts)) + 0.0025
        bm = bmesh.new()
        r_bot, r_top = [], []
        for k in range(20):
            a = k * 2.0 * math.pi / 20.0
            px_w = wx_c + math.cos(a) * rx_w
            py_w = wy_c + math.sin(a) * ry_w
            r_bot.append(bm.verts.new((px_w, py_w, 0.863)))
            r_top.append(bm.verts.new((px_w, py_w, 0.875)))
        for k in range(20):
            kn = (k + 1) % 20
            bm.faces.new((r_bot[k], r_bot[kn], r_top[kn], r_top[k]))
        new_obj_from_bm(bm, "WatchBandGold", "gold", is_right_arm=False)

        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.0155, radius2=0.0145, depth=0.0065)
        rot_w = Euler((0.0, math.radians(90), math.radians(-18)), 'XYZ').to_matrix().to_4x4()
        w_pos = Vector((wx_c + rx_w + 0.001, wy_c - 0.012, 0.869))
        for v in bm.verts:
            v.co = (Matrix.Translation(w_pos) @ rot_w) @ v.co
        new_obj_from_bm(bm, "WatchBezelGold", "gold", is_right_arm=False)

        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, segments=20, radius1=0.0122, radius2=0.0122, depth=0.0020)
        d_pos = w_pos + Vector((0.0032, -0.0010, 0.0))
        for v in bm.verts:
            v.co = (Matrix.Translation(d_pos) @ rot_w) @ v.co
        new_obj_from_bm(bm, "WatchDial", "watch_dial", is_right_arm=False)

    # --- 8. RIGHT-HAND ANTIQUE GOLD SIGNET RING (-X hand, attached to HeroArmR!) ---
    rh_verts = [v.co for v in high_obj.data.vertices if v.co.x < -0.25 and 0.780 < v.co.z < 0.805 and -0.055 < v.co.y < -0.005]
    if rh_verts:
        r_min_x = min(v.x for v in rh_verts)
        r_med_y = float(np.median([v.y for v in rh_verts]))
        ring_pos = Vector((r_min_x + 0.006, r_med_y - 0.006, 0.791))
        bm = bmesh.new()
        segs_u, segs_v = 18, 10
        R, r_t = 0.0098, 0.0026
        grid = []
        for i in range(segs_u):
            au = i * 2.0 * math.pi / segs_u
            ring_v = []
            for j in range(segs_v):
                av = j * 2.0 * math.pi / segs_v
                rr = R + r_t * math.cos(av)
                vx = rr * math.cos(au)
                vy = rr * math.sin(au)
                vz = r_t * math.sin(av) * (1.55 if math.cos(au) < -0.5 else 1.0)
                ring_v.append(bm.verts.new(ring_pos + Vector((vx, vy, vz))))
            grid.append(ring_v)
        for i in range(segs_u):
            inext = (i + 1) % segs_u
            for j in range(segs_v):
                jnext = (j + 1) % segs_v
                bm.faces.new((grid[i][j], grid[inext][j], grid[inext][jnext], grid[i][jnext]))
        new_obj_from_bm(bm, "RightSignetRing", "gold", is_right_arm=True)

    # --- 9. THREE MICROSCOPIC BURIED PELVIS PRIMITIVES (GunMetal, Gold90s, PolymerGrip) ---
    for pi, pname in enumerate(["GunMetal", "Gold90s", "PolymerGrip"]):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=0.0012)
        for v in bm.verts:
            v.co += Vector((0.0, 0.025 + pi * 0.002, 0.900))
        new_obj_from_bm(bm, f"HiddenProp_{pname}", pname, is_right_arm=False)

    print(f"[ACCESSORIES] Created {len(created)} sculpted 3D detail objects")
    return created


def setup_pbr_materials(suit_tex_path, skin_tex_path, iris_tex_path):
    suit_img = bpy.data.images.load(suit_tex_path)
    skin_img = bpy.data.images.load(skin_tex_path)
    iris_img = bpy.data.images.load(iris_tex_path)
    for im in (suit_img, skin_img, iris_img):
        im.pack()

    def build_mat(name, base_rgba, roughness=0.72, metallic=0.0, tex_img=None):
        mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
        mat.use_nodes = True
        nt = mat.node_tree
        nt.nodes.clear()
        out = nt.nodes.new("ShaderNodeOutputMaterial")
        bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
        bsdf.inputs["Roughness"].default_value = roughness
        bsdf.inputs["Metallic"].default_value = metallic
        if "Specular IOR Level" in bsdf.inputs:
            bsdf.inputs["Specular IOR Level"].default_value = 0.28 if roughness > 0.6 else 0.55

        if tex_img is not None:
            tex = nt.nodes.new("ShaderNodeTexImage")
            tex.image = tex_img
            mix = nt.nodes.new("ShaderNodeMix")
            mix.data_type = 'RGBA'
            mix.blend_type = 'MULTIPLY'
            mix.inputs["Factor"].default_value = 1.0
            mix.inputs["B"].default_value = base_rgba
            nt.links.new(tex.outputs["Color"], mix.inputs["A"])
            nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
        else:
            bsdf.inputs["Base Color"].default_value = base_rgba

        nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
        return mat

    # Rich deep 90s ruby crimson wool & satin lapel contrast
    build_mat("crimson", (0.360, 0.018, 0.028, 1.0), roughness=0.80, metallic=0.0, tex_img=suit_img)
    build_mat("lapel", (0.215, 0.011, 0.018, 1.0), roughness=0.58, metallic=0.0, tex_img=suit_img)
    # Warm healthy Fair skin (calibrated cleanly for BOTH Cycles AND Raylib Finish.Anatomy!)
    build_mat("skin", (0.620, 0.365, 0.248, 1.0), roughness=0.56, metallic=0.0, tex_img=skin_img)
    build_mat("hair", (0.018, 0.012, 0.011, 1.0), roughness=0.62, metallic=0.0, tex_img=suit_img)
    build_mat("brow", (0.015, 0.010, 0.009, 1.0), roughness=0.70, metallic=0.0, tex_img=None)
    build_mat("sclera", (0.80, 0.78, 0.75, 1.0), roughness=0.20, metallic=0.0, tex_img=None)
    build_mat("iris", (0.14, 0.065, 0.032, 1.0), roughness=0.14, metallic=0.0, tex_img=iris_img)
    build_mat("pupil", (0.004, 0.004, 0.005, 1.0), roughness=0.08, metallic=0.0, tex_img=None)
    build_mat("catchlight", (1.0, 1.0, 1.0, 1.0), roughness=0.05, metallic=0.0, tex_img=None)
    build_mat("shirt", (0.88, 0.85, 0.80, 1.0), roughness=0.74, metallic=0.0, tex_img=suit_img)
    build_mat("tie", (0.011, 0.012, 0.016, 1.0), roughness=0.38, metallic=0.0, tex_img=suit_img)
    build_mat("pocket_square", (0.86, 0.78, 0.62, 1.0), roughness=0.46, metallic=0.0, tex_img=None)
    build_mat("gold", (0.82, 0.60, 0.20, 1.0), roughness=0.28, metallic=0.85, tex_img=None)
    build_mat("watch_dial", (0.86, 0.84, 0.78, 1.0), roughness=0.24, metallic=0.15, tex_img=None)
    build_mat("trousers", (0.022, 0.024, 0.030, 1.0), roughness=0.76, metallic=0.0, tex_img=suit_img)
    build_mat("shoes", (0.009, 0.010, 0.013, 1.0), roughness=0.22, metallic=0.06, tex_img=suit_img)
    build_mat("GunMetal", (0.12, 0.13, 0.15, 1.0), roughness=0.35, metallic=0.8, tex_img=None)
    build_mat("Gold90s", (0.82, 0.60, 0.20, 1.0), roughness=0.28, metallic=0.85, tex_img=None)
    build_mat("PolymerGrip", (0.04, 0.04, 0.05, 1.0), roughness=0.7, metallic=0.0, tex_img=None)
    print("[MATERIALS] All 19 PBR materials configured & textures packed")


def join_objects(objs, name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    res = bpy.context.view_layer.objects.active
    res.name = name
    return res


def is_right_arm_point(v):
    """Exact armhole seam aligned with our bisect plane (-0.164, 0.05, 1.30), normal (1, 0, -0.03)!"""
    x, y, z = v.x, v.y, v.z
    if z < 0.715 or z > 1.47:
        return False
    if z < 0.90:
        return x < -0.196
    elif z < 1.12:
        t = (z - 0.90) / 0.22
        x_cut = -0.196 + t * 0.032
        return x < x_cut
    else:
        # Exact bisect plane: (x + 0.164) - 0.03 * (z - 1.30) < 0
        return (x + 0.164) - 0.03 * (z - 1.30) < 0.0


def close_open_armhole(bm, crimson_mat_idx, uv_layer, cap_uv, inward_shift_x=0.0):
    """Close any open boundary loops created by the armhole split with clean crimson cap faces & valid UVs!"""
    boundary_edges = [e for e in bm.edges if e.is_boundary]
    if not boundary_edges:
        return
    ret = bmesh.ops.holes_fill(bm, edges=boundary_edges, sides=0)
    new_faces = ret.get("faces", [])
    if new_faces:
        # Poke each cap face to its centroid (optionally slightly domed) for clean shading!
        poke_ret = bmesh.ops.poke(bm, faces=new_faces)
        for v in poke_ret.get("verts", []):
            v.co.x += inward_shift_x
        for f in poke_ret.get("faces", []):
            f.material_index = crimson_mat_idx
            f.smooth = True
            for loop in f.loops:
                loop[uv_layer].uv = cap_uv


def create_shoulder_cap_bm(bm, center, rx, ry, rz, crimson_mat_idx, uv_layer, cap_uv):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=1.0)
    verts = ret["verts"]
    for v in verts:
        v.co = Vector((
            center.x + v.co.x * rx,
            center.y + v.co.y * ry,
            center.z + v.co.z * rz,
        ))
    vert_set = set(verts)
    for f in bm.faces:
        if all(v in vert_set for v in f.verts):
            f.material_index = crimson_mat_idx
            f.smooth = True
            for loop in f.loops:
                loop[uv_layer].uv = cap_uv


def prune_unused_material_slots(obj):
    orig_indices = [p.material_index for p in obj.data.polygons]
    used_slots = sorted(set(orig_indices))
    old_to_new = {old: new for new, old in enumerate(used_slots)}
    used_mats = [obj.data.materials[old] for old in used_slots]
    obj.data.materials.clear()
    for m in used_mats:
        obj.data.materials.append(m)
    for p, old_i in zip(obj.data.polygons, orig_indices):
        p.material_index = old_to_new[old_i]
    obj.data.update()


def split_body_and_right_arm(low_obj, acc_objs, cap_uv):
    # 1. HeroFull
    full_parts = []
    for src in [low_obj] + acc_objs:
        dup = src.copy()
        dup.data = src.data.copy()
        bpy.context.collection.objects.link(dup)
        full_parts.append(dup)
    hero_full = join_objects(full_parts, "HeroFull")

    # 2. Split BodyBase & ArmBase
    body_base = low_obj.copy()
    body_base.data = low_obj.data.copy()
    body_base.name = "BodyBase"
    bpy.context.collection.objects.link(body_base)

    arm_base = low_obj.copy()
    arm_base.data = low_obj.data.copy()
    arm_base.name = "ArmBase"
    bpy.context.collection.objects.link(arm_base)

    bm_b = bmesh.new()
    bm_b.from_mesh(body_base.data)
    uv_b = bm_b.loops.layers.uv.verify()
    arm_faces_b = [f for f in bm_b.faces if is_right_arm_point(f.calc_center_median())]
    bmesh.ops.delete(bm_b, geom=arm_faces_b, context='FACES')
    # Watertight crimson shoulder cap closing the exact armhole boundary loop (ZERO black hole!)
    close_open_armhole(bm_b, crimson_mat_idx=0, uv_layer=uv_b, cap_uv=cap_uv, inward_shift_x=+0.008)
    bmesh.ops.recalc_face_normals(bm_b, faces=bm_b.faces[:])
    bm_b.normal_update()
    bm_b.to_mesh(body_base.data)
    bm_b.free()

    bm_a = bmesh.new()
    bm_a.from_mesh(arm_base.data)
    uv_a = bm_a.loops.layers.uv.verify()
    body_faces_a = [f for f in bm_a.faces if not is_right_arm_point(f.calc_center_median())]
    bmesh.ops.delete(bm_a, geom=body_faces_a, context='FACES')
    # Close arm boundary loop + add smooth tailored crimson deltoid shoulder dome centered on pivot with valid bright crimson UV!
    close_open_armhole(bm_a, crimson_mat_idx=0, uv_layer=uv_a, cap_uv=cap_uv, inward_shift_x=+0.018)
    create_shoulder_cap_bm(
        bm_a, center=RIGHT_SHOULDER_PIVOT_BL + Vector((0.016, 0.002, -0.006)),
        rx=0.056, ry=0.068, rz=0.078, crimson_mat_idx=0, uv_layer=uv_a, cap_uv=cap_uv
    )
    bmesh.ops.recalc_face_normals(bm_a, faces=bm_a.faces[:])
    bm_a.normal_update()
    bm_a.to_mesh(arm_base.data)
    bm_a.free()

    body_parts = [body_base]
    arm_parts = [arm_base]
    for acc in acc_objs:
        dup = acc.copy()
        dup.data = acc.data.copy()
        bpy.context.collection.objects.link(dup)
        if acc.get("is_right_arm", False):
            arm_parts.append(dup)
        else:
            body_parts.append(dup)

    hero_body = join_objects(body_parts, "HeroBody")
    hero_arm_r = join_objects(arm_parts, "HeroArmR")

    for v in hero_arm_r.data.vertices:
        v.co -= RIGHT_SHOULDER_PIVOT_BL
    hero_arm_r.data.update()

    prune_unused_material_slots(hero_arm_r)
    print(f"[SPLIT] HeroFull={len(hero_full.data.polygons)}p | HeroBody={len(hero_body.data.polygons)}p | HeroArmR={len(hero_arm_r.data.polygons)}p (mats={[m.name for m in hero_arm_r.data.materials]})")
    return hero_full, hero_body, hero_arm_r


def export_glb(obj, out_path):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_materials='EXPORT',
        export_image_format='AUTO',
    )
    sz_kb = os.path.getsize(out_path) / 1024.0
    print(f"[EXPORT] {out_path} ({sz_kb:.1f} KB)")


def distribute_glbs(files_dict):
    for d in OUT_DIRS:
        os.makedirs(d, exist_ok=True)
        for fname, src_path in files_dict.items():
            dst = os.path.join(d, fname)
            shutil.copy2(src_path, dst)
            print(f"[COPY] -> {dst}")


def main():
    high_obj = load_and_normalize_high()
    low_obj = build_decimated_low(high_obj, target_tris=30500)
    segment_materials(low_obj)
    unwrap_dual_atlas(low_obj)

    suit_tex_path, skin_tex_path, iris_tex_path, cap_uv = generate_baked_textures(high_obj, low_obj)
    acc_objs = add_sculpted_accessories(high_obj, cap_uv)
    setup_pbr_materials(suit_tex_path, skin_tex_path, iris_tex_path)

    hero_full, hero_body, hero_arm_r = split_body_and_right_arm(low_obj, acc_objs, cap_uv)

    p_full = os.path.join(WORK_DIR, "char_partner_90s.glb")
    p_body = os.path.join(WORK_DIR, "char_partner_90s_body.glb")
    p_arm = os.path.join(WORK_DIR, "char_partner_90s_arm_r.glb")

    export_glb(hero_full, p_full)
    export_glb(hero_body, p_body)
    export_glb(hero_arm_r, p_arm)

    distribute_glbs({
        "char_partner_90s.glb": p_full,
        "char_partner_90s_body.glb": p_body,
        "char_partner_90s_arm_r.glb": p_arm,
    })

    def bl_to_gltf(v):
        return (v.x, v.z, -v.y)

    pivot_g = bl_to_gltf(RIGHT_SHOULDER_PIVOT_BL)
    palm_g = bl_to_gltf(RIGHT_HAND_PALM_BL)
    grip_off_g = (palm_g[0] - pivot_g[0], palm_g[1] - pivot_g[1], palm_g[2] - pivot_g[2])
    hat_g = bl_to_gltf(HAT_ANCHOR_BL)
    eye_g = bl_to_gltf(EYE_ANCHOR_BL)
    print("=== EXACT GLTF Y-UP ANCHORS ===")
    print(f"  RightShoulderPivot  = ({pivot_g[0]:+.4f}, {pivot_g[1]:+.4f}, {pivot_g[2]:+.4f})")
    print(f"  RightHandGripOffset = ({grip_off_g[0]:+.4f}, {grip_off_g[1]:+.4f}, {grip_off_g[2]:+.4f})")
    print(f"  HatAnchor           = ({hat_g[0]:+.4f}, {hat_g[1]:+.4f}, {hat_g[2]:+.4f})")
    print(f"  EyeAnchor           = ({eye_g[0]:+.4f}, {eye_g[1]:+.4f}, {eye_g[2]:+.4f})")
    print(f"  AimPitchDeg         = {AIM_PITCH_DEG:+.1f}")


if __name__ == "__main__":
    main()
