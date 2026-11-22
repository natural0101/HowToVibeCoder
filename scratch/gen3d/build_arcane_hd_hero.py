import bpy
import bmesh
import math
import os
import shutil
import json
import struct
from collections import deque
import numpy as np
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

ROOT = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
BODY_SRC_GLB = os.path.join(ROOT, r"scratch\gen3d\hunyuan_hero_shape.glb")
HEAD_HD_GLB = os.path.join(ROOT, r"scratch\gen3d\hunyuan_head_hd.glb")
CONCEPT_HEAD_IMG = os.path.join(ROOT, r"scratch\gen3d\hero_head_closeup.png")
WORK_DIR = os.path.join(ROOT, r"scratch\gen3d")
OUT_DIRS = [
    os.path.join(ROOT, r"assets\models"),
    os.path.join(ROOT, r"bin\Release\net10.0\assets\models"),
    os.path.join(ROOT, r"bin\Debug\net10.0\assets\models"),
    os.path.join(ROOT, r"bin\Release\net10.0\win-x64\assets\models"),
]

TARGET_HEIGHT = 1.745

# Exact unchanged Body/Right-Arm contract (Blender Z-up -> glTF Y-up: (x, z, -y))
RIGHT_SHOULDER_PIVOT_BL = Vector((-0.194, 0.052, 1.352))
RIGHT_HAND_PALM_BL = Vector((-0.258, -0.032, 0.802))
AIM_PITCH_DEG = -76.0

# Exact HD Head -> Body alignment transform
HEAD_SCALE = 0.2735
HEAD_RAW_CX = -0.0131          # Raw nose/face symmetry X
HEAD_TY = +0.0220              # Head center Y aligned to body head center (+0.009)
HEAD_RAW_TOP_Z = 0.9615
HEAD_TZ = TARGET_HEIGHT - HEAD_RAW_TOP_Z * HEAD_SCALE  # = 1.48203 -> top at exact Z = 1.7450

# Exact sub-pixel calibrated Ortho projection matching hunyuan_head_hd.glb <-> hero_head_closeup.png
ORTHO_SCALE = 2.39750
ORTHO_CAM_X = +0.02550         # Locks image nose/eye midpoint (x=495.5) onto 3D nose tip (-0.0131)
ORTHO_CAM_Z = +0.05200         # Locks image eye centers (y=669) onto 3D eye sockets (z_raw=0.419)

# Exact Eye Centers in Body Blender Z-up
EYE_HALF_IPD = 0.0330
EYE_Z = 1.5960


def body_to_raw(x_b, y_b, z_b):
    return (
        x_b / HEAD_SCALE + HEAD_RAW_CX,
        (y_b - HEAD_TY) / HEAD_SCALE,
        (z_b - HEAD_TZ) / HEAD_SCALE,
    )


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


def box_blur_2d(arr, r=4):
    k = 2 * r + 1
    p = np.pad(arr, ((r, r), (r, r)), mode='edge')
    cs = np.cumsum(p, axis=0)
    v = (cs[k:, :] - cs[:-k, :]) / k
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
    print(f"[SAVE] {path} ({w}x{h})")


def load_concept_head():
    """
    Load hero_head_closeup.png and extract a 100% HOLE-FREE hair mask via warm-skin vs cool-hair
    color separation + BFS flood-fill from top of crown (NEVER touches eyebrows, NEVER leaves highlight holes!).
    """
    im = bpy.data.images.load(CONCEPT_HEAD_IMG)
    w, h = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape((h, w, 4))[:, :, :3].copy()
    bpy.data.images.remove(im, do_unlink=True)

    bg = px[10, 10]
    fg_mask = np.linalg.norm(px - bg[None, None, :], axis=-1) > 0.070
    lum = 0.299 * px[:, :, 0] + 0.587 * px[:, :, 1] + 0.114 * px[:, :, 2]
    warmth = px[:, :, 0] - px[:, :, 2]  # Skin is warm peach (>0.14); hair & specular highlights are cool/neutral (<0.10)!

    yy, xx = np.mgrid[0:h, 0:w]
    # Candidate hair pixels: foreground, above ear-lobe line (y >= 645), NOT warm peach skin!
    # Also exclude a tight brow box only below the forehead swoop tip (y < 716, x in [412..580])
    in_brow_protect = (yy < 716) & (xx >= 412) & (xx <= 580)
    hair_candidate = fg_mask & (yy >= 645) & (warmth < 0.115) & (lum < 0.72) & (~in_brow_protect)
    # Top crown above y=810 is always hair
    hair_candidate |= (fg_mask & (yy >= 810))

    # BFS flood-fill strictly from top-of-crown hair seeds -> guaranteed 1 connected hole-free hair region, ZERO eyebrow bleed!
    visited = np.zeros((h, w), dtype=bool)
    q = deque()
    for sy in range(840, 895, 2):
        for sx in range(440, 560, 2):
            if hair_candidate[sy, sx] and not visited[sy, sx]:
                visited[sy, sx] = True
                q.append((sy, sx))
    while q:
        cy, cx = q.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w and not visited[ny, nx] and hair_candidate[ny, nx]:
                visited[ny, nx] = True
                q.append((ny, nx))

    # Fill any tiny interior specular-highlight holes completely enclosed inside hair (flood-fill exterior!)
    ext = np.zeros((h, w), dtype=bool)
    q2 = deque([(0, 0)])
    ext[0, 0] = True
    while q2:
        cy, cx = q2.popleft()
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            ny, nx = cy + dy, cx + dx
            if 0 <= ny < h and 0 <= nx < w and not ext[ny, nx] and not visited[ny, nx]:
                ext[ny, nx] = True
                q2.append((ny, nx))
    hair_solid = ~ext

    # Light smooth pass on the mask edge
    hair_mask = box_blur_2d(hair_solid.astype(np.float32), r=2) > 0.48
    # Dilate hair mask ONLY by 4px for skin projection exclusion (and protect eyebrows explicitly!)
    hair_dilated = (box_blur_2d(hair_mask.astype(np.float32), r=4) > 0.08) & (~((yy < 722) & (xx >= 404) & (xx <= 588)))

    # Compute exact lit midtone skin RGB in concept
    skin_zone = (
        fg_mask
        & (~hair_dilated)
        & (xx >= 415) & (xx <= 575)
        & (yy >= 515) & (yy <= 775)
        & (px[:, :, 0] > 0.58)
        & (px[:, :, 0] > px[:, :, 1] + 0.08)
        & (px[:, :, 1] > px[:, :, 2] + 0.04)
    )
    skin_base_srgb = np.percentile(px[skin_zone], 75, axis=0).astype(np.float32)
    print(f"[CONCEPT] Hole-free BFS hair_px={int(hair_mask.sum())} | skin_base_srgb={skin_base_srgb.round(4)}")
    return px, fg_mask, hair_mask, hair_dilated, skin_base_srgb


def sample_bilinear(img, u, v):
    h, w = img.shape[:2]
    fx = np.clip(u * (w - 1), 0.0, w - 1.001)
    fy = np.clip(v * (h - 1), 0.0, h - 1.001)
    x0 = fx.astype(np.int32)
    y0 = fy.astype(np.int32)
    x1 = np.minimum(x0 + 1, w - 1)
    y1 = np.minimum(y0 + 1, h - 1)
    dx = fx - x0
    dy = fy - y0
    if img.ndim == 3:
        dx = dx[..., None]
        dy = dy[..., None]
    c00 = img[y0, x0]
    c10 = img[y0, x1]
    c01 = img[y1, x0]
    c11 = img[y1, x1]
    return (c00 * (1 - dx) + c10 * dx) * (1 - dy) + (c01 * (1 - dx) + c11 * dx) * dy


def bisect_local(bm, filter_fn, plane_co, plane_no):
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


def load_body_and_hd_head():
    bpy.ops.wm.read_factory_settings(use_empty=True)

    bpy.ops.import_scene.gltf(filepath=BODY_SRC_GLB)
    body_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    assert len(body_objs) == 1
    body_high = body_objs[0]
    body_high.name = "BodyHigh"
    bpy.context.view_layer.objects.active = body_high
    body_high.select_set(True)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    verts = np.array([list(v.co) for v in body_high.data.vertices], dtype=np.float64)
    min_z, max_z = verts[:, 2].min(), verts[:, 2].max()
    scale = TARGET_HEIGHT / (max_z - min_z)
    head = verts[(verts[:, 2] - min_z) * scale > 1.50]
    hcx = 0.5 * (head[:, 0].min() + head[:, 0].max())
    hcy = 0.5 * (head[:, 1].min() + head[:, 1].max())

    for v in body_high.data.vertices:
        v.co.x = (v.co.x - hcx) * scale
        v.co.y = (v.co.y - hcy) * scale
        v.co.z = (v.co.z - min_z) * scale
    body_high.data.update()
    for p in body_high.data.polygons:
        p.use_smooth = True

    before = set(bpy.context.scene.objects)
    bpy.ops.import_scene.gltf(filepath=HEAD_HD_GLB)
    head_objs = [o for o in bpy.context.scene.objects if o not in before and o.type == 'MESH']
    assert len(head_objs) == 1
    head_high = head_objs[0]
    head_high.name = "HeadHDHigh"
    bpy.ops.object.select_all(action='DESELECT')
    head_high.select_set(True)
    bpy.context.view_layer.objects.active = head_high
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    for v in head_high.data.vertices:
        v.co.x = (v.co.x - HEAD_RAW_CX) * HEAD_SCALE
        v.co.y = v.co.y * HEAD_SCALE + HEAD_TY
        v.co.z = v.co.z * HEAD_SCALE + HEAD_TZ
    head_high.data.update()
    for p in head_high.data.polygons:
        p.use_smooth = True

    print(f"[LOAD] BodyHigh={len(body_high.data.polygons)}p | HeadHDHigh={len(head_high.data.polygons)}p")
    return body_high, head_high


def build_body_low_headless(body_high, target_tris=24500):
    low_obj = body_high.copy()
    low_obj.data = body_high.data.copy()
    low_obj.name = "BodyLow"
    bpy.context.collection.objects.link(low_obj)

    vg = low_obj.vertex_groups.new(name="DetailKeep")
    for v in low_obj.data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        w = 0.44
        if z > 1.02 and y < 0.03 and abs(x) < 0.20:
            w = 0.72
        elif abs(x) > 0.195 and 0.715 < z < 0.91:
            w = 0.76
        elif z < 0.14:
            w = 0.42
        vg.add([v.index], w, 'REPLACE')

    bpy.ops.object.select_all(action='DESELECT')
    low_obj.select_set(True)
    bpy.context.view_layer.objects.active = low_obj

    ratio = min(0.95, target_tris / max(1, len(low_obj.data.polygons)))
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

    # Razor-straight tailored seam cuts
    bisect_local(bm, lambda c: abs(c.x) < 0.205 and 0.78 < c.z < 0.85, (0, 0, 0.818), (0, 0, 1))
    bisect_local(bm, lambda c: abs(c.x) > 0.195 and 0.84 < c.z < 0.93, (0, 0, 0.896), (0, 0, 1))
    bisect_local(bm, lambda c: abs(c.x) > 0.195 and 0.84 < c.z < 0.92, (0, 0, 0.874), (0, 0, 1))
    # Blazer lapel V-opening cuts
    bisect_local(bm, lambda c: c.y < 0.025 and 1.24 < c.z < 1.47 and -0.02 < c.x < 0.10, (0.004, 0, 1.262), (1.0, 0, -0.3265))
    bisect_local(bm, lambda c: c.y < 0.025 and 1.24 < c.z < 1.47 and -0.10 < c.x < 0.02, (-0.004, 0, 1.262), (-1.0, 0, -0.3265))
    # Clean collar rim cut at Z = 1.456 (keeps full blazer shoulder/collar stand intact!)
    bisect_local(bm, lambda c: abs(c.x) < 0.14 and 1.43 < c.z < 1.48, (0, 0, 1.456), (0, 0, 1))
    # Shoe/trouser break
    bisect_local(bm, lambda c: 0.04 < c.z < 0.11, (0, -0.05, 0.078), (0, 0.22, 1.0))
    # Right armhole cut plane
    bisect_local(bm, lambda c: 1.12 < c.z < 1.46 and -0.22 < c.x < -0.11, (-0.164, 0.05, 1.30), (1.0, 0, -0.03))

    # Delete old head & neck above Z = 1.456!
    bm.faces.ensure_lookup_table()
    old_head_faces = [f for f in bm.faces if f.calc_center_median().z > 1.4565]
    bmesh.ops.delete(bm, geom=old_head_faces, context='FACES')

    # CAP the open top neck hole of the body cleanly with a flat interior plate at Z=1.448 so ZERO dark hollow void ever shows!
    bm.edges.ensure_lookup_table()
    top_b_edges = [e for e in bm.edges if e.is_boundary and e.verts[0].co.z > 1.430]
    if top_b_edges:
        ret = bmesh.ops.holes_fill(bm, edges=top_b_edges, sides=0)
        new_fs = ret.get("faces", [])
        if new_fs:
            poke = bmesh.ops.poke(bm, faces=new_fs)
            for v in poke.get("verts", []):
                v.co.z = 1.442

    # Recess body faces strictly inside front V-opening so our crisp 3D Shirt/Collar/Tie sits cleanly proud!
    bm.verts.ensure_lookup_table()
    for v in bm.verts:
        if v.co.y < 0.015 and 1.263 < v.co.z <= 1.457:
            v_half_w = 0.004 + 0.3265 * (v.co.z - 1.262)
            if abs(v.co.x) < v_half_w - 0.001:
                v.co.y += 0.0065

    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00015)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    bm.to_mesh(low_obj.data)
    bm.free()

    for p in low_obj.data.polygons:
        p.use_smooth = True
    low_obj.data.update()
    print(f"[BODY LOW] Headless + watertight neck-cap: verts={len(low_obj.data.vertices)} polys={len(low_obj.data.polygons)}")
    return low_obj


def segment_body_materials(low_obj):
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

        if (z - 0.078) + 0.22 * (y + 0.05) < 0.0:
            labels[i] = mat_idx["shoes"]
            continue

        if 0.715 < z < 0.905 and abs(x) > 0.196:
            if z < 0.874:
                labels[i] = mat_idx["skin"]
            elif z < 0.896:
                labels[i] = mat_idx["shirt"]
            else:
                labels[i] = mat_idx["crimson"]
            continue

        if z < 0.818:
            labels[i] = mat_idx["trousers"]
            continue

        if front_facing and z >= 1.262 and abs(x) < 0.090:
            v_half_w = 0.004 + 0.3265 * (z - 1.262)
            if abs(x) < v_half_w:
                labels[i] = mat_idx["shirt"]
                continue

        # Top interior neck cap stays blazer lapel/crimson so zero light or black gap leaks!
        if z > 1.440 and abs(x) < 0.115 and n.z > 0.55:
            labels[i] = mat_idx["lapel"]
            continue

        if front_facing and (
            point_in_poly(x, z, lapel_right_negX)
            or point_in_poly(x, z, lapel_left_posX)
            or point_in_poly(x, z, flap_right)
            or point_in_poly(x, z, flap_left)
        ):
            labels[i] = mat_idx["lapel"]
            continue

        if 1.425 < z < 1.458 and abs(x) < 0.105 and y > 0.005:
            labels[i] = mat_idx["lapel"]
            continue

        labels[i] = mat_idx["crimson"]

    for i, poly in enumerate(mesh.polygons):
        poly.material_index = int(labels[i])


def unwrap_body_and_hands(body_low):
    bpy.ops.object.select_all(action='DESELECT')
    body_low.select_set(True)
    bpy.context.view_layer.objects.active = body_low

    if not body_low.data.uv_layers:
        body_low.data.uv_layers.new(name="UVMap")

    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.mesh.select_all(action='DESELECT')
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in body_low.data.polygons:
        p.select = (p.material_index == 2)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(58), island_margin=0.015, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')

    uv_layer = body_low.data.uv_layers.active
    for p in body_low.data.polygons:
        if p.material_index == 2:
            for li in p.loop_indices:
                u, v = uv_layer.data[li].uv
                uv_layer.data[li].uv = (0.858 + u * 0.132, 0.025 + v * 0.950)

    for p in body_low.data.polygons:
        p.select = (p.material_index != 2)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(58), island_margin=0.005, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')


def classify_hd_hair_vertices(coords_body, hair_mask_2d):
    """
    100% HOLE-FREE, EAR-SAFE, ZERO-SAWTOOTH 3D hair mask on HD head vertices!
    - Front forehead & swoop lock: exact BFS hole-free hair_mask_2d from hero_head_closeup.png!
    - Temples / sides / above ears / behind ears / nape: smooth continuous 3D anatomical curve with ZERO bald patches!
    """
    xr = coords_body[:, 0] / HEAD_SCALE + HEAD_RAW_CX
    yr = (coords_body[:, 1] - HEAD_TY) / HEAD_SCALE
    zr = (coords_body[:, 2] - HEAD_TZ) / HEAD_SCALE
    xc = xr - HEAD_RAW_CX  # symmetric centered X

    u = 0.5 + (xr - ORTHO_CAM_X) / ORTHO_SCALE
    v = 0.5 + (zr - ORTHO_CAM_Z) / ORTHO_SCALE
    hair_img_val = sample_bilinear(hair_mask_2d.astype(np.float32), u, v)
    in_img = (u > 0.22) & (u < 0.78) & (v > 0.40) & (v < 0.97)

    # 1. Exact front hairline + iconic swoop lock + temples from hole-free BFS image mask!
    front_hair = (yr < 0.02) & in_img & (hair_img_val > 0.45)

    # 2. Continuous upper-temple & side-crown cap (guarantees ZERO bald spots on sides above ears!)
    # Everything above zr = 0.525 behind the forehead plane (yr > -0.18) is solid hair!
    # Plus smooth tapered sideburn descending right in front of the ear (yr in [-0.15, -0.02], |xc| > 0.225, zr > 0.435)!
    temple_cap = (yr > -0.18) & (zr > 0.528)
    sideburn_taper = (np.abs(xc) > 0.225) & (yr > -0.16) & (yr < 0.02) & (zr > 0.440)

    # 3. Back & behind-ear hairline dipping smoothly to clean tapered masculine nape!
    # Behind ear (yr > 0.05): drops smoothly from zr=0.525 over ear to zr=0.185 at center back nape!
    t_back = np.clip((yr - 0.04) / 0.14, 0.0, 1.0)
    t_smooth = t_back * t_back * (3.0 - 2.0 * t_back)
    z_nape = 0.525 * (1.0 - t_smooth) + (0.185 + 0.72 * (xc * xc)) * t_smooth
    back_hair = (yr >= 0.02) & (zr > z_nape)

    # 4. Top of crown is always solid hair
    top_hair = zr > 0.750

    # 5. Strict EAR PROTECTION (keeps outer ear 100% skin, never sliced by hair!)
    is_ear = (np.abs(xc) > 0.258) & (yr > -0.14) & (yr < 0.165) & (zr > 0.255) & (zr < 0.525)

    is_hair = (front_hair | temple_cap | sideburn_taper | back_hair | top_hair) & (~is_ear)
    return is_hair


def build_hd_head_and_hair_meshes(head_high, hair_mask_2d):
    # Step A: Isolate clean Head+Neck from 788k-face HD bust (zero bust collar bumps!)
    head_iso = head_high.copy()
    head_iso.data = head_high.data.copy()
    head_iso.name = "HeadHDIsolated"
    bpy.context.collection.objects.link(head_iso)

    bm = bmesh.new()
    bm.from_mesh(head_iso.data)
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()

    # Cut cleanly right ABOVE the HD bust's shirt collar flare so the neck column is 100% smooth anatomy!
    to_del = []
    for f in bm.faces:
        c = f.calc_center_median()
        xr, yr, zr = body_to_raw(c.x, c.y, c.z)
        xc = xr - HEAD_RAW_CX
        # Front throat goes down to zr=-0.035; sides/back cut cleanly at zr=+0.025..+0.09 above bust collar!
        z_cut = -0.025 + 0.24 * max(0.0, yr + 0.08) + 1.15 * (xc * xc)
        if zr < z_cut or (zr < 0.20 and abs(xc) > 0.25):
            to_del.append(f)
    bmesh.ops.delete(bm, geom=to_del, context='FACES')

    # Keep only largest connected component
    bm.faces.ensure_lookup_table()
    visited = set()
    components = []
    for f in bm.faces:
        if f.index in visited:
            continue
        stack = [f]
        comp = []
        visited.add(f.index)
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf.index not in visited:
                        visited.add(nf.index)
                        stack.append(nf)
        components.append(comp)
    components.sort(key=len, reverse=True)
    if len(components) > 1:
        bmesh.ops.delete(bm, geom=[f for comp in components[1:] for f in comp], context='FACES')

    # Smooth bottom neck boundary ring + extrude cleanly straight down into our 3D white spread collar!
    bm.edges.ensure_lookup_table()
    b_edges = [e for e in bm.edges if e.is_boundary]
    b_verts = list({v for e in b_edges for v in e.verts})
    if b_edges:
        for _ in range(14):
            bmesh.ops.smooth_vert(bm, verts=b_verts, factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        cur_edges = b_edges
        cur_verts = b_verts
        neck_cx = 0.0
        neck_cy = float(np.mean([v.co.y for v in cur_verts]))
        # Smooth anatomical neck column extending down to Z=1.408 (deep inside collar!)
        for step_z, rx_t, ry_t in [(1.466, 0.060, 0.058), (1.448, 0.061, 0.059), (1.428, 0.062, 0.060), (1.408, 0.060, 0.058)]:
            ret = bmesh.ops.extrude_edge_only(bm, edges=cur_edges)
            new_edges = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMEdge)]
            new_verts = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]
            for v in new_verts:
                dx = v.co.x - neck_cx
                dy = v.co.y - neck_cy
                ang = math.atan2(dy, dx)
                # Blend gently toward clean anatomical neck ellipse (removes any tiny HD bust collar kink!)
                ex_t = neck_cx + math.cos(ang) * rx_t
                ey_t = neck_cy + math.sin(ang) * ry_t
                v.co.x = 0.35 * v.co.x + 0.65 * ex_t
                v.co.y = 0.35 * v.co.y + 0.65 * ey_t
                v.co.z = min(v.co.z - 0.008, step_z)
            cur_edges = new_edges
            cur_verts = new_verts
        bmesh.ops.contextual_create(bm, geom=cur_edges)
        # Smooth the entire lower neck column (z < 1.474) so there are ZERO vertical creases above the white collar!
        bm.verts.ensure_lookup_table()
        neck_smooth_v = [v for v in bm.verts if v.co.z < 1.474 and v.co.z > 1.412]
        for _ in range(10):
            bmesh.ops.smooth_vert(bm, verts=neck_smooth_v, factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=False)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.normal_update()
    bm.to_mesh(head_iso.data)
    bm.free()
    for p in head_iso.data.polygons:
        p.use_smooth = True
    head_iso.data.update()
    print(f"[HEAD ISO] Clean smooth neck isolated: verts={len(head_iso.data.vertices)} polys={len(head_iso.data.polygons)}")

    bm_bvh = bmesh.new()
    bm_bvh.from_mesh(head_iso.data)
    head_bvh = BVHTree.FromBMesh(bm_bvh)
    bm_bvh.free()

    # Classify vertices of head_iso into Hair vs Skin
    iso_coords = np.array([list(v.co) for v in head_iso.data.vertices], dtype=np.float64)
    v_is_hair = classify_hd_hair_vertices(iso_coords, hair_mask_2d)

    # =========================================================================
    # STEP B: BUILD 100% HOLE-FREE RAZOR-SMOOTH 3D HAIR CAP (HeadHair_Low)
    # =========================================================================
    hair_obj = head_iso.copy()
    hair_obj.data = head_iso.data.copy()
    hair_obj.name = "HeadHair_Low"
    bpy.context.collection.objects.link(hair_obj)

    bm_h = bmesh.new()
    bm_h.from_mesh(hair_obj.data)
    bm_h.verts.ensure_lookup_table()
    bm_h.faces.ensure_lookup_table()

    # Fill any tiny interior non-hair islands on the 3D hair mesh before deleting!
    # Keep faces with >=2 hair verts, keep largest component, then fill any interior boundary holes!
    non_hair_faces = [f for f in bm_h.faces if sum(1 for v in f.verts if v_is_hair[v.index]) < 2]
    bmesh.ops.delete(bm_h, geom=non_hair_faces, context='FACES')

    # Keep ONLY largest connected hair component
    bm_h.faces.ensure_lookup_table()
    visited = set()
    comps = []
    for f in bm_h.faces:
        if f.index in visited:
            continue
        stack = [f]
        comp = []
        visited.add(f.index)
        while stack:
            cur = stack.pop()
            comp.append(cur)
            for e in cur.edges:
                for nf in e.link_faces:
                    if nf.index not in visited:
                        visited.add(nf.index)
                        stack.append(nf)
        comps.append(comp)
    comps.sort(key=len, reverse=True)
    if len(comps) > 1:
        bmesh.ops.delete(bm_h, geom=[f for c in comps[1:] for f in c], context='FACES')

    # Detect ALL boundary loops of the hair cap: keep the ONE outer hairline perimeter, FILL any small interior holes!
    bm_h.edges.ensure_lookup_table()
    b_edges_all = [e for e in bm_h.edges if e.is_boundary]
    # Group boundary edges into connected loops
    e_visited = set()
    b_loops = []
    for e in b_edges_all:
        if e.index in e_visited:
            continue
        loop = []
        stack = [e]
        e_visited.add(e.index)
        while stack:
            cur_e = stack.pop()
            loop.append(cur_e)
            for v in cur_e.verts:
                for ne in v.link_edges:
                    if ne.is_boundary and ne.index not in e_visited:
                        e_visited.add(ne.index)
                        stack.append(ne)
        b_loops.append(loop)
    b_loops.sort(key=len, reverse=True)
    print(f"[HEAD HAIR] Boundary loops detected: {len(b_loops)} (outer={len(b_loops[0]) if b_loops else 0}, holes={len(b_loops)-1})")
    if len(b_loops) > 1:
        hole_edges = [e for lp in b_loops[1:] for e in lp]
        ret_fill = bmesh.ops.holes_fill(bm_h, edges=hole_edges, sides=0)
        if ret_fill.get("faces"):
            bmesh.ops.triangulate(bm_h, faces=ret_fill["faces"])

    # Trim any tiny 1-face boundary teeth
    for _ in range(3):
        bm_h.faces.ensure_lookup_table()
        spikes = [f for f in bm_h.faces if sum(1 for e in f.edges if e.is_boundary) >= 2]
        if not spikes:
            break
        bmesh.ops.delete(bm_h, geom=spikes, context='FACES')

    # Smooth hairline boundary + 4-ring neighborhood on the 0.4mm high-res surface (C2 Bezier smoothness!)
    bm_h.edges.ensure_lookup_table()
    bm_h.verts.ensure_lookup_table()
    border_verts = {v for e in bm_h.edges if e.is_boundary for v in e.verts}
    ring_verts = set(border_verts)
    for _ in range(4):
        nxt = set(ring_verts)
        for v in ring_verts:
            for e in v.link_edges:
                nxt.add(e.other_vert(v))
        ring_verts = nxt
    ring_list = list(ring_verts)

    for _it in range(30):
        bmesh.ops.smooth_vert(bm_h, verts=ring_list, factor=0.55, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for v in ring_list:
            loc, _, _, _ = head_bvh.find_nearest(v.co)
            if loc is not None:
                v.co = loc

    bm_h.normal_update()
    bm_h.verts.ensure_lookup_table()
    bm_h.edges.ensure_lookup_table()
    b_set = {v.index for e in bm_h.edges if e.is_boundary for v in e.verts}
    r1_set = set(b_set)
    for e in bm_h.edges:
        if e.verts[0].index in b_set or e.verts[1].index in b_set:
            r1_set.add(e.verts[0].index)
            r1_set.add(e.verts[1].index)

    # Build BVH of our exact smoothed hair cap so HeadSkin_Low can deflate cleanly underneath it!
    hair_high_bvh = BVHTree.FromBMesh(bm_h)

    bm_h.to_mesh(hair_obj.data)
    bm_h.free()
    hair_obj.data.update()

    # Decimate hair cap cleanly to ~5,400 tris while locking hairline border & forehead swoop lock!
    vg_h = hair_obj.vertex_groups.new(name="HairBorderKeep")
    for v in hair_obj.data.vertices:
        w = 0.28
        if v.index in b_set:
            w = 1.00
        elif v.index in r1_set:
            w = 0.90
        elif v.co.y < -0.04 and v.co.z > 1.62:
            w = 0.85
        vg_h.add([v.index], w, 'REPLACE')

    bpy.ops.object.select_all(action='DESELECT')
    hair_obj.select_set(True)
    bpy.context.view_layer.objects.active = hair_obj
    h_ratio = min(0.95, 5400.0 / max(1, len(hair_obj.data.polygons)))
    mod_h = hair_obj.modifiers.new(name="DecimateHair", type='DECIMATE')
    mod_h.decimate_type = 'COLLAPSE'
    mod_h.ratio = h_ratio
    mod_h.vertex_group = "HairBorderKeep"
    mod_h.invert_vertex_group = True
    mod_h.vertex_group_factor = 0.72
    mod_h.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="DecimateHair")

    # Post-decimation: final hairline smooth + crisp 3D volume lift (+1.8mm bulk, +0.6mm rim) + inward-tucked solid edge!
    bm_h2 = bmesh.new()
    bm_h2.from_mesh(hair_obj.data)
    bm_h2.edges.ensure_lookup_table()
    bm_h2.verts.ensure_lookup_table()
    b_edges2 = [e for e in bm_h2.edges if e.is_boundary]
    b_verts2 = {v for e in b_edges2 for v in e.verts}
    for _ in range(10):
        bmesh.ops.smooth_vert(bm_h2, verts=list(b_verts2), factor=0.45, use_axis_x=True, use_axis_y=True, use_axis_z=True)
        for v in b_verts2:
            loc, _, _, _ = head_bvh.find_nearest(v.co)
            if loc is not None:
                v.co = loc

    bm_h2.normal_update()
    r1_v2 = {e.other_vert(v) for v in b_verts2 for e in v.link_edges} - b_verts2
    r2_v2 = {e.other_vert(v) for v in r1_v2 for e in v.link_edges} - b_verts2 - r1_v2
    for v in bm_h2.verts:
        if v in b_verts2:
            lift = 0.0007
        elif v in r1_v2:
            lift = 0.0013
        elif v in r2_v2:
            lift = 0.0017
        else:
            lift = 0.0019
        # Extra crisp 3D stand-off on the iconic forehead swoop lock so its pointed tip casts shadow & never kisses forehead skin!
        if v.co.y < -0.045 and v.co.z > 1.610 and -0.005 < v.co.x < 0.048:
            lift += 0.0016
        v.co += v.normal * lift

    skull_center = Vector((0.0, 0.010, 1.625))
    ret_ext = bmesh.ops.extrude_edge_only(bm_h2, edges=b_edges2)
    ext_verts = [g for g in ret_ext["geom"] if isinstance(g, bmesh.types.BMVert)]
    for v in ext_verts:
        inward = (skull_center - v.co).normalized()
        v.co += inward * 0.0040

    bmesh.ops.triangulate(bm_h2, faces=bm_h2.faces[:])
    bmesh.ops.recalc_face_normals(bm_h2, faces=bm_h2.faces[:])
    bm_h2.normal_update()
    bm_h2.to_mesh(hair_obj.data)
    bm_h2.free()

    for p in hair_obj.data.polygons:
        p.use_smooth = True
    hair_mat = bpy.data.materials.get("hair") or bpy.data.materials.new("hair")
    hair_obj.data.materials.clear()
    hair_obj.data.materials.append(hair_mat)
    hair_obj.data.update()

    bpy.ops.object.select_all(action='DESELECT')
    hair_obj.select_set(True)
    bpy.context.view_layer.objects.active = hair_obj
    if not hair_obj.data.uv_layers:
        hair_obj.data.uv_layers.new(name="UVMap")
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(56), island_margin=0.008, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')
    print(f"[HEAD HAIR] Watertight 3D Hair Cap built: verts={len(hair_obj.data.vertices)} polys={len(hair_obj.data.polygons)}")

    # =========================================================================
    # STEP C: BUILD HEAD SKIN MESH (HeadSkin_Low) — DEFLATED UNDER HAIR CAP SO SKIN NEVER POKES THROUGH!
    # =========================================================================
    skin_obj = head_iso.copy()
    skin_obj.data = head_iso.data.copy()
    skin_obj.name = "HeadSkin_Low"
    bpy.context.collection.objects.link(skin_obj)

    bm_s = bmesh.new()
    bm_s.from_mesh(skin_obj.data)
    bm_s.verts.ensure_lookup_table()
    bm_s.faces.ensure_lookup_table()

    # Compute topological distance (in rings) keyed by BMVert object directly (immune to index shifts after delete!)
    skin_verts = {v for v in bm_s.verts if not v_is_hair[v.index]}
    ring_dist = {v: 0 for v in skin_verts}
    q_r = deque(skin_verts)
    while q_r:
        v = q_r.popleft()
        d = ring_dist[v]
        if d >= 22:
            continue
        for e in v.link_edges:
            ov = e.other_vert(v)
            if ov not in ring_dist:
                ring_dist[ov] = d + 1
                q_r.append(ov)

    # Deflate scalp vertices (rings 4..16 strictly under hair cap, EXCLUDING ears!) BEFORE deleting deep crown!
    for v in bm_s.verts:
        if abs(v.co.x) > 0.062 and 1.52 < v.co.z < 1.64:
            continue  # 100% protect sculpted ears!
        d = ring_dist.get(v, 0)
        if d >= 4:
            t_in = min(1.0, (d - 3) / 10.0)
            inward = (skull_center - v.co).normalized()
            v.co += inward * (0.0048 * t_in)

    # Delete deep hidden scalp (> 16 rings deep inside hair cap, NEVER deleting ear faces!)
    deep_faces = [
        f for f in bm_s.faces
        if all(ring_dist.get(v, 99) > 16 for v in f.verts)
        and not (abs(f.calc_center_median().x) > 0.062 and 1.52 < f.calc_center_median().z < 1.64)
    ]
    if deep_faces:
        bmesh.ops.delete(bm_s, geom=deep_faces, context='FACES')

    # Gently recess almond eye openings inward along +Y by 5.2mm BEFORE decimation!
    for v in bm_s.verts:
        if v.co.y < -0.04 and 1.582 < v.co.z < 1.610:
            for ex in (-EYE_HALF_IPD, +EYE_HALF_IPD):
                sgn = 1.0 if ex > 0 else -1.0
                dx = (v.co.x - ex)
                dz = (v.co.z - EYE_Z) - 0.055 * dx * sgn
                r2 = (dx / 0.0136) ** 2 + (dz / 0.0046) ** 2
                if r2 < 1.35:
                    t = max(0.0, 1.0 - r2 / 1.35)
                    v.co.y += 0.0052 * (t * t * (3.0 - 2.0 * t))

    bm_s.normal_update()
    bm_s.to_mesh(skin_obj.data)
    bm_s.free()
    skin_obj.data.update()

    # Decimate HeadSkin_Low cleanly to ~10,000 tris with maximum fidelity on eyes, nose, lips, brows, AND EARS!
    vg_s = skin_obj.vertex_groups.new(name="FaceDetailKeep")
    for v in skin_obj.data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        w = 0.30
        if y < -0.025 and abs(x) < 0.085 and 1.48 < z < 1.67:
            w = 0.92
            if 1.575 < z < 1.615 and abs(x) < 0.055:
                w = 1.00
            elif 1.515 < z < 1.575 and abs(x) < 0.038:
                w = 0.98
        elif abs(x) > 0.064 and 1.53 < z < 1.635:
            w = 0.92  # Crisp sculpted ear helix & concha!
        vg_s.add([v.index], w, 'REPLACE')

    bpy.ops.object.select_all(action='DESELECT')
    skin_obj.select_set(True)
    bpy.context.view_layer.objects.active = skin_obj
    s_ratio = min(0.95, 10200.0 / max(1, len(skin_obj.data.polygons)))
    mod_s = skin_obj.modifiers.new(name="DecimateSkin", type='DECIMATE')
    mod_s.decimate_type = 'COLLAPSE'
    mod_s.ratio = s_ratio
    mod_s.vertex_group = "FaceDetailKeep"
    mod_s.invert_vertex_group = True
    mod_s.vertex_group_factor = 0.65
    mod_s.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="DecimateSkin")

    for p in skin_obj.data.polygons:
        p.use_smooth = True
    skin_mat = bpy.data.materials.get("skin") or bpy.data.materials.new("skin")
    skin_obj.data.materials.clear()
    skin_obj.data.materials.append(skin_mat)
    skin_obj.data.update()

    # Dedicated 100% CONTIGUOUS SEAMLESS Front-Face + Side/Neck/Ear UV layout (ZERO normal-threshold sliver holes!)
    if not skin_obj.data.uv_layers:
        skin_obj.data.uv_layers.new(name="UVMap")

    # Pure spatial front-face region (NO normal threshold -> zero isolated crease triangles sent to Smart UV!)
    front_set = {
        p.index for p in skin_obj.data.polygons
        if (p.center.y < -0.006) and (p.center.z > 1.478) and (abs(p.center.x) < 0.078)
    }
    # Morphological closure on mesh adjacency: fill any tiny enclosed non-front hole & keep single main front component!
    bm_uv = bmesh.new()
    bm_uv.from_mesh(skin_obj.data)
    bm_uv.faces.ensure_lookup_table()
    # 2 passes of majority-neighbor smoothing on front_set eliminates all stray 1-tri notches along the border
    for _ in range(2):
        add_f, rem_f = set(), set()
        for f in bm_uv.faces:
            nbrs = [nf.index for e in f.edges for nf in e.link_faces if nf.index != f.index]
            if not nbrs:
                continue
            f_cnt = sum(1 for ni in nbrs if ni in front_set)
            if f.index not in front_set and f_cnt >= 2:
                add_f.add(f.index)
            elif f.index in front_set and f_cnt == 0:
                rem_f.add(f.index)
        front_set = (front_set | add_f) - rem_f
    bm_uv.free()

    front_poly_mask = [(p.index in front_set) for p in skin_obj.data.polygons]

    bpy.ops.object.select_all(action='DESELECT')
    skin_obj.select_set(True)
    bpy.context.view_layer.objects.active = skin_obj
    for p, is_f in zip(skin_obj.data.polygons, front_poly_mask):
        p.select = (not is_f)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_mode(type='FACE')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.015, correct_aspect=True, scale_to_bounds=True)
    bpy.ops.object.mode_set(mode='OBJECT')

    uv_layer = skin_obj.data.uv_layers.active
    for p, is_f in zip(skin_obj.data.polygons, front_poly_mask):
        if not is_f:
            for li in p.loop_indices:
                u, v = uv_layer.data[li].uv
                uv_layer.data[li].uv = (0.582 + u * 0.258, 0.018 + v * 0.964)

    for p, is_f in zip(skin_obj.data.polygons, front_poly_mask):
        if is_f:
            for li in p.loop_indices:
                co = skin_obj.data.vertices[skin_obj.data.loops[li].vertex_index].co
                u_norm = np.clip((co.x + 0.088) / 0.176, 0.0, 1.0)
                v_norm = np.clip((co.z - 1.472) / 0.238, 0.0, 1.0)
                uv_layer.data[li].uv = (0.015 + u_norm * 0.550, 0.025 + v_norm * 0.950)

    print(f"[HEAD SKIN] Built: verts={len(skin_obj.data.vertices)} polys={len(skin_obj.data.polygons)}")
    return head_iso, skin_obj, hair_obj


def make_ao_mat(name, distance, samples=24):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    ao = nt.nodes.new("ShaderNodeAmbientOcclusion")
    ao.samples = samples
    ao.only_local = True
    ao.inputs["Distance"].default_value = distance
    nt.links.new(ao.outputs["AO"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def make_relief_mat(name, light_dir=(0.22, -0.46, 0.86)):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    dot = nt.nodes.new("ShaderNodeVectorMath")
    dot.operation = 'DOT_PRODUCT'
    dot.inputs[1].default_value = light_dir
    nt.links.new(geo.outputs["Normal"], dot.inputs[0])
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = -1.0
    mr.inputs["From Max"].default_value = 1.0
    mr.inputs["To Min"].default_value = 0.44
    mr.inputs["To Max"].default_value = 1.00
    nt.links.new(dot.outputs["Value"], mr.inputs["Value"])
    nt.links.new(mr.outputs["Result"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def make_pos_mat(name, ranges=((-0.40, 0.40), (-0.25, 0.25), (0.0, 1.80))):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(geo.outputs["Position"], sep.inputs["Vector"])
    for axis, (lo, hi) in zip(("X", "Y", "Z"), ranges):
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


def make_normal_mat(name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    emit = nt.nodes.new("ShaderNodeEmission")
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    comb = nt.nodes.new("ShaderNodeCombineXYZ")
    nt.links.new(geo.outputs["Normal"], sep.inputs["Vector"])
    for axis in ("X", "Y", "Z"):
        mr = nt.nodes.new("ShaderNodeMapRange")
        mr.inputs["From Min"].default_value = -1.0
        mr.inputs["From Max"].default_value = 1.0
        mr.inputs["To Min"].default_value = 0.0
        mr.inputs["To Max"].default_value = 1.0
        nt.links.new(sep.outputs[axis], mr.inputs["Value"])
        nt.links.new(mr.outputs["Result"], comb.inputs[axis])
    nt.links.new(comb.outputs["Vector"], emit.inputs["Color"])
    emit.inputs["Strength"].default_value = 1.0
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return mat


def bake_pass(high_obj, low_obj, src_mat, img_name, size, face_filter=None, cage=0.012, max_dist=0.040, margin=16, self_bake=False):
    prev_hide = {}
    for o in bpy.context.scene.objects:
        prev_hide[o.name] = (o.hide_render, o.hide_viewport)
        o.hide_render = True
        o.hide_viewport = True

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

    if self_bake:
        # Bake Position/Normal directly from low_obj itself -> 100.000% ZERO ray misses or cracks!
        tmat = src_mat.copy()
        tmat.name = f"_SelfMat_{img_name}"
        nt = tmat.node_tree
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bake_img
        tex.select = True
        nt.nodes.active = tex
        tmp_obj.data.materials.clear()
        tmp_obj.data.materials.append(tmat)
        for p in tmp_obj.data.polygons:
            p.material_index = 0
        bpy.ops.object.select_all(action='DESELECT')
        tmp_obj.select_set(True)
        bpy.context.view_layer.objects.active = tmp_obj
        bpy.context.scene.render.bake.use_selected_to_active = False
    else:
        high_obj.hide_render = False
        high_obj.hide_viewport = False
        high_obj.data.materials.clear()
        high_obj.data.materials.append(src_mat)

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
        bpy.context.scene.render.bake.cage_extrusion = cage
        bpy.context.scene.render.bake.max_ray_distance = max_dist

    bpy.context.scene.render.bake.margin = margin
    bpy.context.scene.render.bake.margin_type = 'ADJACENT_FACES'
    bpy.ops.object.bake(type='EMIT')

    arr = np.array(bake_img.pixels[:], dtype=np.float32).reshape((size, size, 4))[:, :, :3].copy()

    bpy.data.objects.remove(tmp_obj, do_unlink=True)
    bpy.data.materials.remove(tmat, do_unlink=True)
    bpy.data.images.remove(bake_img, do_unlink=True)

    for o in bpy.context.scene.objects:
        if o.name in prev_hide:
            o.hide_render, o.hide_viewport = prev_hide[o.name]

    print(f"[BAKE] {img_name} ({size}x{size}) min={arr.min():.3f} mean={arr.mean():.3f} max={arr.max():.3f}")
    return arr


def generate_all_textures(body_high, body_low, head_iso, skin_obj, hair_obj, concept_px, fg_mask, hair_dilated, skin_base_srgb):
    enable_cycles_gpu(samples=32)

    mat_ao_tight = make_ao_mat("_BakeAOTight", 0.085)
    mat_ao_wide = make_ao_mat("_BakeAOWide", 0.34)
    mat_ao_face_t = make_ao_mat("_BakeFaceAOTight", 0.025, samples=28)
    mat_ao_face_w = make_ao_mat("_BakeFaceAOWide", 0.110, samples=28)
    mat_relief = make_relief_mat("_BakeRelief", (0.22, -0.46, 0.86))
    mat_hair_rel = make_relief_mat("_BakeHairRelief", (0.18, -0.35, 0.92))
    mat_head_pos = make_pos_mat("_BakeHeadPos", ((-0.15, 0.15), (-0.15, 0.15), (1.38, 1.78)))
    mat_nrm = make_normal_mat("_BakeNrm")

    # 1. SUIT / TROUSERS / SHOES 2048x2048 (clean cage=0.015 + ray-miss protection -> spotless white & crimson blazer!)
    suit_ao_t = bake_pass(body_high, body_low, mat_ao_tight, "suit_ao_t", 2048, face_filter=lambda m: m != 2, cage=0.015, max_dist=0.045)[:, :, 0]
    suit_ao_w = bake_pass(body_high, body_low, mat_ao_wide, "suit_ao_w", 2048, face_filter=lambda m: m != 2, cage=0.015, max_dist=0.045)[:, :, 0]
    suit_rel = bake_pass(body_high, body_low, mat_relief, "suit_rel", 2048, face_filter=lambda m: m != 2, cage=0.015, max_dist=0.045)[:, :, 0]

    valid_suit = (suit_rel > 0.10) & (suit_ao_t > 0.08)
    suit_ao_t_f = np.where(valid_suit, suit_ao_t, 0.92)
    suit_ao_w_f = np.where(valid_suit, suit_ao_w, 0.92)
    suit_rel_f = np.where(valid_suit, suit_rel, 0.88)

    rel_blur = box_blur_2d(suit_rel_f, r=8)
    crease_pop = np.clip((suit_rel_f - rel_blur) * 1.35, -0.14, 0.14)
    ao_mix = np.clip(0.45 * suit_ao_t_f + 0.30 * suit_ao_w_f + 0.25 * suit_rel_f, 0.0, 1.0)
    suit_luma = np.clip(0.44 + 0.58 * np.power(ao_mix, 1.30) + crease_pop, 0.44, 1.00)

    yy, xx = np.mgrid[0:2048, 0:2048].astype(np.float32)
    twill = 0.011 * np.sin((xx + yy) * 1.55) + 0.006 * np.cos((xx - yy) * 2.25)
    suit_luma = np.where(valid_suit, np.clip(suit_luma + twill, 0.44, 1.00), 0.90)
    suit_rgb = np.stack([suit_luma, suit_luma, suit_luma], axis=-1)
    suit_tex_path = os.path.join(WORK_DIR, "hero_suit_ao_2k.png")
    save_png(suit_rgb, suit_tex_path)

    bright_ys, bright_xs = np.where((suit_luma > 0.88) & valid_suit)
    cap_uv = (float(bright_xs[len(bright_xs) // 2]) / 2048.0, float(bright_ys[len(bright_xs) // 2]) / 2048.0) if len(bright_xs) else (0.5, 0.5)

    # 2. SCULPTED HD HAIR 1024x1024 BAKED WAVE AO + STRAND HIGHLIGHT TEXTURE
    hair_ao_t = bake_pass(head_iso, hair_obj, mat_ao_face_t, "hair_ao_t", 1024, cage=0.012, max_dist=0.035)[:, :, 0]
    hair_ao_w = bake_pass(head_iso, hair_obj, mat_ao_face_w, "hair_ao_w", 1024, cage=0.012, max_dist=0.035)[:, :, 0]
    hair_rel = bake_pass(head_iso, hair_obj, mat_hair_rel, "hair_rel", 1024, cage=0.012, max_dist=0.035)[:, :, 0]

    valid_hair = (hair_rel > 0.10) & (hair_ao_t > 0.05)
    h_ao_t = np.where(valid_hair, hair_ao_t, 0.85)
    h_ao_w = np.where(valid_hair, hair_ao_w, 0.85)
    h_rel = np.where(valid_hair, hair_rel, 0.78)
    h_blur = box_blur_2d(h_rel, r=5)
    h_pop = np.clip((h_rel - h_blur) * 2.40, -0.28, 0.38)
    h_luma = np.clip(0.28 + 0.76 * np.power(0.45 * h_ao_t + 0.25 * h_ao_w + 0.30 * h_rel, 1.35) + h_pop, 0.18, 1.00)
    hair_rgb = np.stack([h_luma * 0.92, h_luma * 0.95, np.clip(h_luma * 1.08, 0.0, 1.0)], axis=-1)
    hair_rgb = np.where(valid_hair[:, :, None], hair_rgb, 0.58)
    hair_tex_path = os.path.join(WORK_DIR, "hero_hair_ao_1k.png")
    save_png(hair_rgb, hair_tex_path)

    # 3. ARCANE 2048x2048 PIXEL-SHARP PROJECTED FACE + HIGH-TO-LOW AO + HANDS SKIN ATLAS
    # High-to-low AO uses clean cage=0.014 (zero missed-ray cracks!); Position & Normal use self_bake=True (100% continuous!)
    f_ao_t = bake_pass(head_iso, skin_obj, mat_ao_face_t, "face_ao_t", 2048, cage=0.014, max_dist=0.040, margin=20)[:, :, 0]
    f_ao_w = bake_pass(head_iso, skin_obj, mat_ao_face_w, "face_ao_w", 2048, cage=0.014, max_dist=0.040, margin=20)[:, :, 0]
    f_rel = bake_pass(head_iso, skin_obj, mat_relief, "face_rel", 2048, cage=0.014, max_dist=0.040, margin=20)[:, :, 0]
    f_pos = bake_pass(head_iso, skin_obj, mat_head_pos, "face_pos", 2048, margin=20, self_bake=True)
    f_nrm = bake_pass(head_iso, skin_obj, mat_nrm, "face_nrm", 2048, margin=20, self_bake=True)

    hand_ao_t = bake_pass(body_high, body_low, mat_ao_tight, "hand_ao_t", 2048, face_filter=lambda m: m == 2, cage=0.014, max_dist=0.040, margin=12)[:, :, 0]
    hand_ao_w = bake_pass(body_high, body_low, mat_ao_wide, "hand_ao_w", 2048, face_filter=lambda m: m == 2, cage=0.014, max_dist=0.040, margin=12)[:, :, 0]
    hand_rel = bake_pass(body_high, body_low, mat_relief, "hand_rel", 2048, face_filter=lambda m: m == 2, cage=0.014, max_dist=0.040, margin=12)[:, :, 0]

    wx = f_pos[:, :, 0] * 0.30 - 0.15
    wy = f_pos[:, :, 1] * 0.30 - 0.15
    wz = f_pos[:, :, 2] * 0.40 + 1.38
    nx = f_nrm[:, :, 0] * 2.0 - 1.0
    ny = f_nrm[:, :, 1] * 2.0 - 1.0
    nz = f_nrm[:, :, 2] * 2.0 - 1.0

    # Valid texel mask comes from 100%-hit self_bake f_pos (f_pos[:,:,2] > 0.01); AO ray-miss guard falls back to 0.92!
    valid_head = f_pos[:, :, 2] > 0.01
    ao_hit = valid_head & (f_rel > 0.10) & (f_ao_t > 0.04)
    h_t = np.where(ao_hit, f_ao_t, 0.92)
    h_w = np.where(ao_hit, f_ao_w, 0.92)
    h_r = np.where(ao_hit, f_rel, 0.88)
    h_r_blur = box_blur_2d(h_r, r=6)
    h_cav = np.clip((h_r - h_r_blur) * 1.15, -0.10, 0.10)

    # Rich neutral Cycles sculpted base (ears, temples, jaw sides, smooth silky neck!)
    ao_face = np.clip(0.45 * h_t + 0.30 * h_w + 0.25 * h_r, 0.0, 1.0)
    neutral_l = np.clip(0.58 + 0.40 * np.power(ao_face, 1.18) + h_cav, 0.48, 0.97)
    # Extra-smooth silky shading on the lower neck tube right above white collar (wz < 1.488) -> ZERO vertical streaks!
    neck_t = np.clip((1.488 - wz) / 0.018, 0.0, 1.0)
    neutral_l = neutral_l * (1.0 - neck_t) + np.clip(0.76 + 0.16 * h_w, 0.72, 0.93) * neck_t
    base_sculpt_rgb = np.stack([
        np.clip(neutral_l * 1.012, 0.0, 1.0),
        np.clip(neutral_l * 0.984, 0.0, 1.0),
        np.clip(neutral_l * 0.962, 0.0, 1.0),
    ], axis=-1)

    # Project hero_head_closeup.png via sub-pixel calibrated ortho mapping!
    xr = wx / HEAD_SCALE + HEAD_RAW_CX
    yr = (wy - HEAD_TY) / HEAD_SCALE
    zr = (wz - HEAD_TZ) / HEAD_SCALE
    u_img = 0.5 + (xr - ORTHO_CAM_X) / ORTHO_SCALE
    v_img = 0.5 + (zr - ORTHO_CAM_Z) / ORTHO_SCALE

    sampled_srgb = sample_bilinear(concept_px, u_img, v_img)
    sampled_fg = sample_bilinear(fg_mask.astype(np.float32), u_img, v_img)
    sampled_hair = sample_bilinear(hair_dilated.astype(np.float32), u_img, v_img)

    # Neutralize skin tone with soft highlight knee (eliminates harsh white streak over eyebrow ridge!)
    ratio_rgb = sampled_srgb / np.maximum(skin_base_srgb[None, None, :], 0.05)
    # Soft highlight roll-off above 0.94 so highlights look painterly & natural, never clipped neon white!
    neutral_proj = ratio_rgb * 0.925
    over = np.maximum(neutral_proj - 0.90, 0.0)
    neutral_proj = np.where(neutral_proj > 0.90, 0.90 + 0.08 * np.tanh(over / 0.15), neutral_proj)

    # Keep dark Arcane linework (brows, lashes, nostrils, mouth line) deep & razor-sharp across all 6 skin tones!
    proj_lum = 0.299 * sampled_srgb[:, :, 0] + 0.587 * sampled_srgb[:, :, 1] + 0.114 * sampled_srgb[:, :, 2]
    dark_t = np.clip((0.40 - proj_lum) / 0.32, 0.0, 1.0)
    dark_mult = 1.0 - 0.30 * (dark_t ** 1.25)
    neutral_proj = np.clip(neutral_proj * dark_mult[:, :, None], 0.03, 0.985)

    # Reinforce subtle 3DCycles AO
    ao_reinf = np.clip(0.80 + 0.22 * ao_face, 0.74, 1.01)
    neutral_proj = np.clip(neutral_proj * ao_reinf[:, :, None], 0.03, 0.985)

    # Smooth front-to-side blend (zero UV stretching on ears/sides, zero dark hair bleed!)
    w_nrm = np.clip((-ny - 0.16) / 0.30, 0.0, 1.0)
    w_nrm = w_nrm * w_nrm * (3.0 - 2.0 * w_nrm)
    w_y = np.clip((0.015 - wy) / 0.035, 0.0, 1.0)
    w_z = np.clip((wz - 1.484) / 0.024, 0.0, 1.0)
    w_ear = np.clip((0.072 - np.abs(wx)) / 0.014, 0.0, 1.0)
    w_proj = w_nrm * w_y * w_z * w_ear * np.clip(sampled_fg, 0.0, 1.0) * (1.0 - np.clip(sampled_hair, 0.0, 1.0))

    face_rgb = base_sculpt_rgb * (1.0 - w_proj[:, :, None]) + neutral_proj * w_proj[:, :, None]

    # Deep dark orbital lash border strictly inside recessed eye sockets behind 3D eyeballs
    eye_pocket_dark = np.zeros((2048, 2048), dtype=np.float32)
    for ex in (-EYE_HALF_IPD, +EYE_HALF_IPD):
        sgn = 1.0 if ex > 0 else -1.0
        dx = wx - ex
        dz = (wz - EYE_Z) - 0.055 * dx * sgn
        r2 = (dx / 0.0132) ** 2 + (dz / 0.0044) ** 2
        m = np.clip((1.12 - r2) / 0.32, 0.0, 1.0) * (wy < -0.04) * valid_head
        eye_pocket_dark = np.maximum(eye_pocket_dark, m)
    face_rgb = face_rgb * (1.0 - 0.88 * eye_pocket_dark[:, :, None])

    # Composite body hands in right-hand UV strip (U > 0.848)
    valid_hand = (hand_rel > 0.05) & (xx / 2048.0 > 0.848)
    ha_t = np.where(valid_hand, hand_ao_t, 0.94)
    ha_w = np.where(valid_hand, hand_ao_w, 0.94)
    ha_r = np.where(valid_hand, hand_rel, 0.90)
    h_ao = np.clip(0.45 * ha_t + 0.30 * ha_w + 0.25 * ha_r, 0.0, 1.0)
    hl = np.clip(0.60 + 0.38 * np.power(h_ao, 1.22), 0.52, 0.98)
    hand_rgb = np.stack([
        np.clip(hl * (1.0 + 0.03 * (1.0 - h_ao)), 0.0, 1.0),
        np.clip(hl * (1.0 - 0.05 * (1.0 - h_ao)), 0.0, 1.0),
        np.clip(hl * (1.0 - 0.08 * (1.0 - h_ao)), 0.0, 1.0),
    ], axis=-1)

    skin_final = np.full((2048, 2048, 3), 0.90, dtype=np.float32)
    skin_final = np.where(valid_head[:, :, None], np.clip(face_rgb, 0.03, 0.99), skin_final)
    skin_final = np.where(valid_hand[:, :, None], np.clip(hand_rgb, 0.40, 0.99), skin_final)

    skin_tex_path = os.path.join(WORK_DIR, "hero_face_skin_2k.png")
    save_png(skin_final, skin_tex_path)

    # 4. RADIAL IRIS 256x256
    N = 256
    gy, gx = np.mgrid[0:N, 0:N].astype(np.float32)
    ux = (gx / (N - 1)) * 2.0 - 1.0
    uy = (gy / (N - 1)) * 2.0 - 1.0
    rad = np.sqrt(ux * ux + uy * uy)
    ang = np.arctan2(uy, ux)
    fibers = 0.78 + 0.15 * np.sin(ang * 28.0 + rad * 7.0) + 0.09 * np.cos(ang * 46.0 - rad * 11.0)
    limbal = 1.0 - 0.80 * np.clip((rad - 0.66) / 0.34, 0.0, 1.0) ** 1.35
    collarette = 1.0 + 0.26 * np.exp(-((rad - 0.40) / 0.14) ** 2)
    iris_val = np.clip(fibers * limbal * collarette, 0.08, 1.0)
    top_sh = 1.0 - 0.42 * np.clip((uy - 0.15) / 0.85, 0.0, 1.0)
    iris_val *= top_sh
    iris_rgb = np.stack([iris_val, iris_val, iris_val], axis=-1)
    iris_tex_path = os.path.join(WORK_DIR, "hero_iris_256.png")
    save_png(iris_rgb, iris_tex_path)

    for m in (mat_ao_tight, mat_ao_wide, mat_ao_face_t, mat_ao_face_w, mat_relief, mat_hair_rel, mat_head_pos, mat_nrm):
        bpy.data.materials.remove(m, do_unlink=True)

    return suit_tex_path, skin_tex_path, hair_tex_path, iris_tex_path, cap_uv


def build_surface_bvh(obj):
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm)
    bm.free()
    return bvh


def ray_front(bvh, x, z, y_start=-0.35):
    loc, norm, idx, dist = bvh.ray_cast(Vector((x, y_start, z)), Vector((0.0, 1.0, 0.0)), 0.70)
    return loc, norm


def add_hd_eyes_collar_and_accessories(body_high, head_iso, skin_obj, cap_uv):
    body_bvh = build_surface_bvh(body_high)
    head_bvh = build_surface_bvh(head_iso)
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

    # 1. REAL NORMAL-SIZED ARCANE 3D EYES (26mm wide sclera window + 11.5mm iris!)
    for side_tag, sx in [("L", +EYE_HALF_IPD), ("R", -EYE_HALF_IPD)]:
        sgn = 1.0 if sx > 0 else -1.0
        hit_hd, _ = ray_front(head_bvh, sx, EYE_Z)
        ey_front = (hit_hd.y if hit_hd else -0.067) + 0.0008
        ry_eye = 0.0130
        ey_center = ey_front + ry_eye
        yaw_out = math.radians(-8.0 * sgn)
        rot_eye = Euler((0.0, 0.0, yaw_out), 'XYZ').to_matrix().to_4x4()
        base_m = Matrix.Translation((sx, ey_center, EYE_Z)) @ rot_eye

        # Sclera
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1.0)
        for v in bm.verts:
            v.co.x *= 0.0148
            v.co.y *= ry_eye
            v.co.z *= 0.0086
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyeSclera_{side_tag}", "sclera")

        # 11.5mm Iris
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=14, radius=0.00575)
        for v in bm.verts:
            v.co.y *= 0.22
            r_xy = math.sqrt(v.co.x * v.co.x + v.co.z * v.co.z)
            v.co.y += (r_xy * r_xy) / (2.0 * 0.018)
            v.co.y -= (ry_eye - 0.0005)
            v.co.z += 0.0004
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyeIris_{side_tag}", "iris", planar_uv=True)

        # Pupil
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=10, radius=0.00225)
        for v in bm.verts:
            v.co.y *= 0.22
            v.co.y -= (ry_eye + 0.0005)
            v.co.z += 0.0004
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyePupil_{side_tag}", "pupil")

        # Catchlight
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=8, radius=0.00085)
        for v in bm.verts:
            v.co.x += 0.0017
            v.co.z += 0.0016
            v.co.y *= 0.30
            v.co.y -= (ry_eye + 0.00095)
            v.co = base_m @ v.co
        new_obj_from_bm(bm, f"EyeCatchlight_{side_tag}", "catchlight")

        # Sleek Upper Eyelash Line
        bm = bmesh.new()
        lash_rings = []
        for k in range(15):
            t = -1.0 + 2.0 * (k / 14.0)
            lx_loc = t * 0.0138
            lz_loc = 0.0044 * math.sqrt(max(0.0, 1.0 - 0.88 * t * t)) + 0.0007 * t * sgn + 0.0003
            wx_l = sx + lx_loc
            wz_l = EYE_Z + lz_loc
            hit_l, _ = ray_front(head_bvh, wx_l, wz_l)
            wy_l = (hit_l.y if hit_l else ey_front) - 0.0006
            thick = 0.00125 * (1.0 - 0.38 * (t * t))
            ring = []
            for j in range(6):
                a = j * 2.0 * math.pi / 6.0
                ring.append(bm.verts.new((wx_l, wy_l + math.cos(a) * thick * 1.15, wz_l + math.sin(a) * thick * 0.80)))
            lash_rings.append(ring)
        for i in range(len(lash_rings) - 1):
            for j in range(6):
                jn = (j + 1) % 6
                bm.faces.new((lash_rings[i][j], lash_rings[i][jn], lash_rings[i + 1][jn], lash_rings[i + 1][j]))
        bm.faces.new(lash_rings[0][::-1])
        bm.faces.new(lash_rings[-1])
        new_obj_from_bm(bm, f"UpperLash_{side_tag}", "brow")

    # 2. RAZOR-CLEAN 3D WHITE SPREAD COLLAR + SHIRT V-BIB + TAILORED BLAZER COLLAR STAND + WINDSOR TIE!
    neck_band = np.array([list(v.co) for v in head_iso.data.vertices if 1.448 < v.co.z < 1.468])
    ncx = 0.0
    ncy = float(0.5 * (neck_band[:, 1].min() + neck_band[:, 1].max()))
    nrx = float(0.5 * (neck_band[:, 0].max() - neck_band[:, 0].min()))
    nry = float(0.5 * (neck_band[:, 1].max() - neck_band[:, 1].min()))
    print(f"[NECK FIT] Smooth neck @ Z=1.458: center=({ncx:+.4f},{ncy:+.4f}) rx={nrx:.4f} ry={nry:.4f}")

    # 2a. Solid 3D White Shirt V-Front Bib
    bm = bmesh.new()
    rows_z = np.linspace(1.248, 1.456, 15)
    cols_u = np.linspace(-1.0, 1.0, 13)
    grid_f, grid_b = [], []
    for rz in rows_z:
        v_half = 0.008 + 0.345 * max(0.0, rz - 1.255)
        rf, rb = [], []
        for cu in cols_u:
            px_s = cu * v_half
            hit, _ = ray_front(body_bvh, px_s, rz)
            edge_tuck = 0.0065 * (cu * cu) ** 1.4
            py_s = (hit.y if hit else -0.055) + 0.0008 + edge_tuck
            rf.append(bm.verts.new((px_s, py_s, rz)))
            rb.append(bm.verts.new((px_s, py_s + 0.009, rz)))
        grid_f.append(rf)
        grid_b.append(rb)
    for r in range(len(rows_z) - 1):
        for c in range(len(cols_u) - 1):
            bm.faces.new((grid_f[r][c], grid_f[r][c + 1], grid_f[r + 1][c + 1], grid_f[r + 1][c]))
            bm.faces.new((grid_b[r][c + 1], grid_b[r][c], grid_b[r + 1][c], grid_b[r + 1][c + 1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    new_obj_from_bm(bm, "ShirtVFrontBib", "shirt")

    # 2b. Solid 3D White Spread Dress Collar wrapping snugly around grafted HD neck!
    bm = bmesh.new()
    segs_c = 44
    ring_specs = [
        (1.425, 0.000, 0.003, 0.003),
        (1.480, -0.020, 0.0025, 0.0025),
        (1.477, -0.022, 0.0075, 0.0070),
        (1.452, -0.026, 0.0130, 0.0110),
        (1.424, -0.022, 0.0110, 0.0090),
    ]
    c_rings = []
    for zb, z_drop, ex, ey in ring_specs:
        ring = []
        for k in range(segs_c):
            a = -math.pi + 2.0 * math.pi * (k / segs_c)
            front_w = math.exp(-((a / 0.55) ** 2))
            point_w = math.exp(-(((abs(a) - 0.48) / 0.28) ** 2))
            z_cur = zb + z_drop * front_w - (0.016 * point_w if ex > 0.010 else 0.0)
            flare = 1.0 + (0.14 * point_w if ex > 0.009 else 0.0)
            px_c = ncx + math.sin(a) * (nrx + ex) * flare
            py_c = ncy - math.cos(a) * (nry + ey) * flare
            if abs(a) < 0.22 and ex > 0.005:
                py_c += 0.006 * (1.0 - abs(a) / 0.22)
            ring.append(bm.verts.new((px_c, py_c, z_cur)))
        c_rings.append(ring)
    for ri in range(len(c_rings) - 1):
        for k in range(segs_c):
            kn = (k + 1) % segs_c
            bm.faces.new((c_rings[ri][k], c_rings[ri][kn], c_rings[ri + 1][kn], c_rings[ri + 1][k]))
    for k in range(segs_c):
        kn = (k + 1) % segs_c
        bm.faces.new((c_rings[-1][k], c_rings[-1][kn], c_rings[0][kn], c_rings[0][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    new_obj_from_bm(bm, "SpreadDressCollar", "shirt")

    # 2c. Tailored 3D Blazer Outer Collar Stand (wraps cleanly around sides & back of white collar -> ZERO dark gap!)
    bm = bmesh.new()
    segs_j = 33
    # Sweep from right front lapel (-145 deg -> +145 deg around the back, leaving crisp front V open!)
    angles_j = np.linspace(math.radians(46.0), math.radians(314.0), segs_j)  # 0 = front (-Y), 180 = back (+Y)
    j_specs = [
        # (z_top_back, z_front_taper, rad_x_add, rad_y_add)
        (1.430, -0.020, 0.008, 0.008),  # Inner tuck behind white collar
        (1.464, -0.022, 0.011, 0.010),  # Crisp blazer collar top edge (1cm below white shirt collar!)
        (1.460, -0.024, 0.022, 0.018),  # Outer blazer collar roll
        (1.432, -0.025, 0.044, 0.030),  # Smooth shoulder flange blending seamlessly into blazer shoulders!
        (1.418, -0.022, 0.036, 0.024),  # Bottom tuck inside torso
    ]
    j_rings = []
    for zb, zf, ex, ey in j_specs:
        ring = []
        for a in angles_j:
            # 0 at front ends (a=46 or 314), 1 at back (a=180)
            frontness = max(0.0, math.cos(a))
            z_cur = zb + zf * (frontness ** 1.2)
            px_j = ncx + math.sin(a) * (nrx + ex)
            py_j = ncy - math.cos(a) * (nry + ey)
            ring.append(bm.verts.new((px_j, py_j, z_cur)))
        j_rings.append(ring)
    for ri in range(len(j_rings) - 1):
        for k in range(segs_j - 1):
            bm.faces.new((j_rings[ri][k], j_rings[ri][k + 1], j_rings[ri + 1][k + 1], j_rings[ri + 1][k]))
    for k in range(segs_j - 1):
        bm.faces.new((j_rings[-1][k], j_rings[-1][k + 1], j_rings[0][k + 1], j_rings[0][k]))
    # Cap both front ends
    bm.faces.new([j_rings[ri][0] for ri in range(len(j_rings))])
    bm.faces.new([j_rings[ri][-1] for ri in reversed(range(len(j_rings)))])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    new_obj_from_bm(bm, "BlazerCollarStand", "lapel")

    # 2d. Crisp 3D Black Silk Windsor Tie Knot & Tapered Blade
    bm = bmesh.new()
    tie_stations = [
        (1.454, 0.0155, 0.0095, 0.0080),
        (1.443, 0.0145, 0.0105, 0.0092),
        (1.430, 0.0100, 0.0075, 0.0068),
        (1.418, 0.0125, 0.0055, 0.0050),
        (1.385, 0.0160, 0.0050, 0.0046),
        (1.345, 0.0190, 0.0048, 0.0044),
        (1.300, 0.0205, 0.0045, 0.0042),
        (1.260, 0.0180, 0.0040, 0.0038),
    ]
    t_rings = []
    for tz, hw, th, fwd in tie_stations:
        hit, _ = ray_front(body_bvh, 0.0, tz)
        base_y = (hit.y if hit else -0.056) - fwd
        pts = [
            (0.0, base_y - th * 0.62, tz),
            (hw * 0.72, base_y - th * 0.42, tz),
            (hw, base_y, tz),
            (hw * 0.85, base_y + th * 0.55, tz),
            (0.0, base_y + th * 0.65, tz),
            (-hw * 0.85, base_y + th * 0.55, tz),
            (-hw, base_y, tz),
            (-hw * 0.72, base_y - th * 0.42, tz),
        ]
        t_rings.append([bm.verts.new(p) for p in pts])
    for i in range(len(t_rings) - 1):
        for k in range(8):
            kn = (k + 1) % 8
            bm.faces.new((t_rings[i][k], t_rings[i][kn], t_rings[i + 1][kn], t_rings[i + 1][k]))
    bm.faces.new(t_rings[0][::-1])
    bm.faces.new(t_rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    new_obj_from_bm(bm, "WindsorSilkTie", "tie")

    # 2e. Antique Gold Tie Bar
    tz_bar = 1.320
    hit_tb, _ = ray_front(body_bvh, 0.0, tz_bar)
    tb_y = (hit_tb.y if hit_tb else -0.068) - 0.0082
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co.x *= 0.042
        v.co.y *= 0.0045
        v.co.z *= 0.0052
        v.co += Vector((0.0, tb_y, tz_bar))
    new_obj_from_bm(bm, "GoldTieBar", "gold")

    # 2f. Subtle Antique Gold Collar Chain
    bm = bmesh.new()
    chain_rings = []
    for k in range(19):
        t = -1.0 + 2.0 * (k / 18.0)
        cx_c = t * 0.050
        cz_c = 1.416 + 0.032 * (t * t)
        hit_ch, _ = ray_front(body_bvh, cx_c, cz_c)
        cy_c = (hit_ch.y if hit_ch else -0.052) - 0.0032 + 0.004 * (t * t)
        r_c = 0.0023
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

    # 3. EXISTING TAILORED BLAZER ACCESSORIES (100% preserved!)
    hit_ps, nrm_ps = ray_front(body_bvh, 0.095, 1.300)
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

    button_coords = [
        (-0.078, 1.212), (+0.072, 1.212),
        (-0.040, 1.118), (+0.038, 1.118),
        (-0.038, 1.028), (+0.038, 1.028),
    ]
    for bi, (bx, bz) in enumerate(button_coords):
        hit, nrm = ray_front(body_bvh, bx, bz)
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

    l_wrist_verts = [v.co for v in body_high.data.vertices if v.co.x > 0.20 and 0.862 < v.co.z < 0.878]
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

    rh_verts = [v.co for v in body_high.data.vertices if v.co.x < -0.25 and 0.780 < v.co.z < 0.805 and -0.055 < v.co.y < -0.005]
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

    for pi, pname in enumerate(["GunMetal", "Gold90s", "PolymerGrip"]):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=0.0012)
        for v in bm.verts:
            v.co += Vector((0.0, 0.025 + pi * 0.002, 0.900))
        new_obj_from_bm(bm, f"HiddenProp_{pname}", pname, is_right_arm=False)

    print(f"[ACCESSORIES] Created {len(created)} sculpted 3D objects")
    return created


def setup_pbr_materials(suit_tex_path, skin_tex_path, hair_tex_path, iris_tex_path):
    suit_img = bpy.data.images.load(suit_tex_path)
    skin_img = bpy.data.images.load(skin_tex_path)
    hair_img = bpy.data.images.load(hair_tex_path)
    iris_img = bpy.data.images.load(iris_tex_path)
    for im in (suit_img, skin_img, hair_img, iris_img):
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

    build_mat("crimson", (0.360, 0.018, 0.028, 1.0), roughness=0.80, metallic=0.0, tex_img=suit_img)
    build_mat("lapel", (0.215, 0.011, 0.018, 1.0), roughness=0.58, metallic=0.0, tex_img=suit_img)
    build_mat("skin", (0.640, 0.378, 0.258, 1.0), roughness=0.54, metallic=0.0, tex_img=skin_img)
    build_mat("hair", (0.019, 0.014, 0.014, 1.0), roughness=0.54, metallic=0.0, tex_img=hair_img)
    build_mat("brow", (0.014, 0.010, 0.009, 1.0), roughness=0.68, metallic=0.0, tex_img=None)
    build_mat("sclera", (0.86, 0.84, 0.81, 1.0), roughness=0.16, metallic=0.0, tex_img=None)
    build_mat("iris", (0.16, 0.075, 0.036, 1.0), roughness=0.12, metallic=0.0, tex_img=iris_img)
    build_mat("pupil", (0.004, 0.004, 0.005, 1.0), roughness=0.06, metallic=0.0, tex_img=None)
    build_mat("catchlight", (1.0, 1.0, 1.0, 1.0), roughness=0.04, metallic=0.0, tex_img=None)
    build_mat("shirt", (0.90, 0.88, 0.84, 1.0), roughness=0.70, metallic=0.0, tex_img=None)
    build_mat("tie", (0.012, 0.013, 0.018, 1.0), roughness=0.34, metallic=0.0, tex_img=None)
    build_mat("pocket_square", (0.86, 0.78, 0.62, 1.0), roughness=0.46, metallic=0.0, tex_img=None)
    build_mat("gold", (0.82, 0.60, 0.20, 1.0), roughness=0.28, metallic=0.85, tex_img=None)
    build_mat("watch_dial", (0.86, 0.84, 0.78, 1.0), roughness=0.24, metallic=0.15, tex_img=None)
    build_mat("trousers", (0.022, 0.024, 0.030, 1.0), roughness=0.76, metallic=0.0, tex_img=suit_img)
    build_mat("shoes", (0.009, 0.010, 0.013, 1.0), roughness=0.22, metallic=0.06, tex_img=suit_img)
    build_mat("GunMetal", (0.12, 0.13, 0.15, 1.0), roughness=0.35, metallic=0.8, tex_img=None)
    build_mat("Gold90s", (0.82, 0.60, 0.20, 1.0), roughness=0.28, metallic=0.85, tex_img=None)
    build_mat("PolymerGrip", (0.04, 0.04, 0.05, 1.0), roughness=0.7, metallic=0.0, tex_img=None)


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
        return (x + 0.164) - 0.03 * (z - 1.30) < 0.0


def close_open_armhole(bm, crimson_mat_idx, uv_layer, cap_uv, inward_shift_x=0.0):
    boundary_edges = [e for e in bm.edges if e.is_boundary and e.verts[0].co.x < -0.11 and e.verts[0].co.z < 1.445]
    if not boundary_edges:
        return
    ret = bmesh.ops.holes_fill(bm, edges=boundary_edges, sides=0)
    new_faces = ret.get("faces", [])
    if new_faces:
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


def split_and_assemble_hero(body_low, skin_obj, hair_obj, acc_objs, cap_uv):
    full_parts = []
    for src in [body_low, skin_obj, hair_obj] + acc_objs:
        dup = src.copy()
        dup.data = src.data.copy()
        bpy.context.collection.objects.link(dup)
        full_parts.append(dup)
    hero_full = join_objects(full_parts, "HeroFull")
    prune_unused_material_slots(hero_full)

    body_base = body_low.copy()
    body_base.data = body_low.data.copy()
    body_base.name = "BodyBase"
    bpy.context.collection.objects.link(body_base)

    arm_base = body_low.copy()
    arm_base.data = body_low.data.copy()
    arm_base.name = "ArmBase"
    bpy.context.collection.objects.link(arm_base)

    bm_b = bmesh.new()
    bm_b.from_mesh(body_base.data)
    uv_b = bm_b.loops.layers.uv.verify()
    arm_faces_b = [f for f in bm_b.faces if is_right_arm_point(f.calc_center_median())]
    bmesh.ops.delete(bm_b, geom=arm_faces_b, context='FACES')
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
    for src in [skin_obj, hair_obj]:
        dup = src.copy()
        dup.data = src.data.copy()
        bpy.context.collection.objects.link(dup)
        body_parts.append(dup)

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
    prune_unused_material_slots(hero_body)

    hero_arm_r = join_objects(arm_parts, "HeroArmR")
    for v in hero_arm_r.data.vertices:
        v.co -= RIGHT_SHOULDER_PIVOT_BL
    hero_arm_r.data.update()
    prune_unused_material_slots(hero_arm_r)

    print(f"[ASSEMBLE] HeroFull={len(hero_full.data.polygons)}p | HeroBody={len(hero_body.data.polygons)}p | HeroArmR={len(hero_arm_r.data.polygons)}p")
    return hero_full, hero_body, hero_arm_r


def measure_exact_head_anchors(head_iso, hair_obj):
    head_bvh = build_surface_bvh(head_iso)
    hit_bridge, _ = ray_front(head_bvh, 0.0, EYE_Z)
    bridge_y = hit_bridge.y if hit_bridge else -0.0858
    # Eyewear: calibrated on v1 QC -> centered dead-on eyes, zero clipping, temple arms resting on ears!
    eye_anchor_bl = Vector((0.0, bridge_y - 0.008, EYE_Z))

    # Hats: lowered 22mm from v1 so Crown, Ushanka, Cowboy, Frog sit SNUGLY on brow/crown with ZERO floating!
    hv = np.array([list(v.co) for v in hair_obj.data.vertices])
    crown_top_z = float(hv[:, 2].max())
    upper = hv[hv[:, 2] > 1.66]
    crown_cy = float(0.5 * (upper[:, 1].min() + upper[:, 1].max()))
    hat_anchor_bl = Vector((0.0, crown_cy + 0.004, crown_top_z - 0.072))

    return hat_anchor_bl, eye_anchor_bl


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


def verify_glb_contract(path):
    with open(path, "rb") as f:
        f.seek(12)
        length = struct.unpack("<I", f.read(4))[0]
        assert f.read(4) == b"JSON"
        doc = json.loads(f.read(length))
    mats = [m["name"] for m in doc.get("materials", [])]
    prims = [doc["materials"][p["material"]]["name"] for m in doc["meshes"] for p in m["primitives"]]
    prop_count = sum(1 for n in prims if n in ("GunMetal", "Gold90s", "PolymerGrip"))
    print(f"[CONTRACT] {os.path.basename(path)}: prims={len(prims)} propCount={prop_count} mats={mats}")
    return prop_count, mats


def distribute_glbs(files_dict):
    for d in OUT_DIRS:
        os.makedirs(d, exist_ok=True)
        for fname, src_path in files_dict.items():
            dst = os.path.join(d, fname)
            shutil.copy2(src_path, dst)


def main():
    concept_px, fg_mask, hair_mask_2d, hair_dilated, skin_base_srgb = load_concept_head()
    body_high, head_high = load_body_and_hd_head()
    body_low = build_body_low_headless(body_high, target_tris=24500)
    segment_body_materials(body_low)
    unwrap_body_and_hands(body_low)

    head_iso, skin_obj, hair_obj = build_hd_head_and_hair_meshes(head_high, hair_mask_2d)

    suit_tex_path, skin_tex_path, hair_tex_path, iris_tex_path, cap_uv = generate_all_textures(
        body_high, body_low, head_iso, skin_obj, hair_obj,
        concept_px, fg_mask, hair_dilated, skin_base_srgb
    )

    acc_objs = add_hd_eyes_collar_and_accessories(body_high, head_iso, skin_obj, cap_uv)
    setup_pbr_materials(suit_tex_path, skin_tex_path, hair_tex_path, iris_tex_path)

    hat_anchor_bl, eye_anchor_bl = measure_exact_head_anchors(head_iso, hair_obj)
    hero_full, hero_body, hero_arm_r = split_and_assemble_hero(body_low, skin_obj, hair_obj, acc_objs, cap_uv)

    p_full = os.path.join(WORK_DIR, "char_partner_90s.glb")
    p_body = os.path.join(WORK_DIR, "char_partner_90s_body.glb")
    p_arm = os.path.join(WORK_DIR, "char_partner_90s_arm_r.glb")

    export_glb(hero_full, p_full)
    export_glb(hero_body, p_body)
    export_glb(hero_arm_r, p_arm)

    c_full, _ = verify_glb_contract(p_full)
    c_body, _ = verify_glb_contract(p_body)
    verify_glb_contract(p_arm)
    assert c_full == 3 and c_body == 3, f"Expected partnerPropMeshes=3, got full={c_full} body={c_body}"

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
    hat_g = bl_to_gltf(hat_anchor_bl)
    eye_g = bl_to_gltf(eye_anchor_bl)

    anchors_json = {
        "RightShoulderPivot": [round(c, 4) for c in pivot_g],
        "RightHandGripOffset": [round(c, 4) for c in grip_off_g],
        "HatAnchor": [round(c, 4) for c in hat_g],
        "EyeAnchor": [round(c, 4) for c in eye_g],
        "AimPitchDeg": AIM_PITCH_DEG,
    }
    with open(os.path.join(WORK_DIR, "hd_anchors.json"), "w") as jf:
        json.dump(anchors_json, jf, indent=2)

    print("=== EXACT GLTF Y-UP ANCHORS (NEW HD HEAD v2) ===")
    print(f"  RightShoulderPivot  = ({pivot_g[0]:+.4f}, {pivot_g[1]:+.4f}, {pivot_g[2]:+.4f})")
    print(f"  RightHandGripOffset = ({grip_off_g[0]:+.4f}, {grip_off_g[1]:+.4f}, {grip_off_g[2]:+.4f})")
    print(f"  HatAnchor           = ({hat_g[0]:+.4f}, {hat_g[1]:+.4f}, {hat_g[2]:+.4f})")
    print(f"  EyeAnchor           = ({eye_g[0]:+.4f}, {eye_g[1]:+.4f}, {eye_g[2]:+.4f})")
    print(f"  AimPitchDeg         = {AIM_PITCH_DEG:+.1f}")


if __name__ == "__main__":
    main()
