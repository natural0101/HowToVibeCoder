import bpy, bmesh, math, os, shutil, random
from mathutils import Vector, Matrix, Euler

PROJ = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder"
TEX_DIR = os.path.join(PROJ, "scratch", "hat_textures")
OUT_MODELS = os.path.join(PROJ, "assets", "models")
BIN_TARGETS = [
    os.path.join(PROJ, "bin", "Release", "net10.0", "assets", "models"),
    os.path.join(PROJ, "bin", "Debug", "net10.0", "assets", "models"),
    os.path.join(PROJ, "bin", "Release", "net10.0", "win-x64", "assets", "models"),
]
os.makedirs(OUT_MODELS, exist_ok=True)

# Blender coords convention:
#   Forward = -Y  (exports as glTF +Z)
#   Up      = +Z  (exports as glTF +Y)
#   Left    = +X  (exports as glTF +X)
#
# Anchors on char_partner_90s.glb (glTF Y-up):
#   HatAnchor = (0.0, 1.635, -0.007) -> Blender (0.0, +0.007, 1.635)
#   EyeAnchor = (0.0, 1.568,  0.098) -> Blender (0.0, -0.098, 1.568)

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def make_mat(name, color=(0.8, 0.8, 0.8, 1.0), metallic=0.0, roughness=0.5,
             tex_name=None, alpha_blend=False, emission=(0.0, 0.0, 0.0), emission_strength=0.0):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission_strength > 0.0:
        bsdf.inputs["Emission Color"].default_value = (*emission, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    if tex_name:
        tpath = os.path.join(TEX_DIR, tex_name)
        img = bpy.data.images.load(tpath, check_existing=True)
        img.pack()
        tex_node = nt.nodes.new("ShaderNodeTexImage")
        tex_node.image = img
        nt.links.new(tex_node.outputs["Color"], bsdf.inputs["Base Color"])
        if alpha_blend:
            nt.links.new(tex_node.outputs["Alpha"], bsdf.inputs["Alpha"])
    elif alpha_blend and len(color) == 4:
        bsdf.inputs["Alpha"].default_value = color[3]
    if alpha_blend:
        if hasattr(mat, "blend_method"):
            mat.blend_method = 'BLEND'
        if hasattr(mat, "surface_render_method"):
            mat.surface_render_method = 'BLENDED'
    return mat

def finish_mesh_obj(name, bm, mat, smooth=True, uv_mode="box", uv_scale=4.0):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    uv_layer = bm.loops.layers.uv.verify()
    for f in bm.faces:
        n = f.normal
        ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
        for loop in f.loops:
            co = loop.vert.co
            if uv_mode == "cyl":
                ang = math.atan2(co.y, co.x)
                u = (ang / (2.0 * math.pi)) + 0.5
                v = co.z * uv_scale + 0.5
                loop[uv_layer].uv = (u, v)
            elif uv_mode == "sphere":
                r = max(1e-5, co.length)
                u = (math.atan2(co.y, co.x) / (2.0 * math.pi)) + 0.5
                v = math.acos(max(-1.0, min(1.0, co.z / r))) / math.pi
                loop[uv_layer].uv = (u, v)
            elif uv_mode == "front":
                loop[uv_layer].uv = (co.x * uv_scale + 0.5, co.z * uv_scale + 0.5)
            else:
                if az >= ax and az >= ay:
                    u, v = co.x * uv_scale + 0.5, co.y * uv_scale + 0.5
                elif ax >= ay:
                    u, v = co.y * uv_scale + 0.5, co.z * uv_scale + 0.5
                else:
                    u, v = co.x * uv_scale + 0.5, co.z * uv_scale + 0.5
                loop[uv_layer].uv = (u, v)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [smooth] * len(mesh.polygons))
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj

def make_tube_along_points(pts, radius, segs=12, cap=True, radius_fn=None):
    bm = bmesh.new()
    rings = []
    n = len(pts)
    prev_up = Vector((0, 0, 1))
    for i, p in enumerate(pts):
        p = Vector(p)
        if i == 0:
            t = (Vector(pts[1]) - p).normalized()
        elif i == n - 1:
            t = (p - Vector(pts[i - 1])).normalized()
        else:
            t = (Vector(pts[i + 1]) - Vector(pts[i - 1])).normalized()
        if t.length < 1e-6:
            t = Vector((0, 0, 1))
        ref = Vector((1, 0, 0)) if abs(t.dot(prev_up)) > 0.96 else prev_up
        side = t.cross(ref).normalized()
        up = side.cross(t).normalized()
        prev_up = up
        r = radius_fn(i / max(1, n - 1)) if radius_fn else radius
        ring = []
        for s in range(segs):
            a = 2.0 * math.pi * s / segs
            v = p + (side * math.cos(a) + up * math.sin(a)) * r
            ring.append(bm.verts.new(v))
        rings.append(ring)
    for i in range(n - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(segs):
            s2 = (s + 1) % segs
            bm.faces.new((r0[s], r0[s2], r1[s2], r1[s]))
    if cap and len(rings) >= 2:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    return bm

def make_closed_tube(pts, radius, segs=12):
    bm = bmesh.new()
    rings = []
    n = len(pts)
    prev_up = Vector((0, 0, 1))
    for i in range(n):
        p = Vector(pts[i])
        p_prev = Vector(pts[(i - 1) % n])
        p_next = Vector(pts[(i + 1) % n])
        t = (p_next - p_prev).normalized()
        if t.length < 1e-6:
            t = Vector((1, 0, 0))
        ref = Vector((1, 0, 0)) if abs(t.dot(prev_up)) > 0.96 else prev_up
        side = t.cross(ref).normalized()
        up = side.cross(t).normalized()
        prev_up = up
        ring = []
        for s in range(segs):
            a = 2.0 * math.pi * s / segs
            v = p + (side * math.cos(a) + up * math.sin(a)) * radius
            ring.append(bm.verts.new(v))
        rings.append(ring)
    for i in range(n):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for s in range(segs):
            s2 = (s + 1) % segs
            bm.faces.new((r0[s], r0[s2], r1[s2], r1[s]))
    return bm

def make_ellipsoid_bm(rx, ry, rz, center=(0,0,0), u_segs=28, v_segs=18, deform_fn=None):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u_segs, v_segments=v_segs, radius=1.0)
    cx, cy, cz = center
    for v in bm.verts:
        x, y, z = v.co.x * rx, v.co.y * ry, v.co.z * rz
        if deform_fn:
            x, y, z = deform_fn(x, y, z)
        v.co = Vector((x + cx, y + cy, z + cz))
    return bm

def shift_all_z(dz):
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.location.z += dz

def export_glb(filename):
    objs = [o for o in bpy.data.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    active = bpy.context.view_layer.objects.active
    active.name = os.path.splitext(filename)[0]
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    n_mats = len(active.data.materials)
    assert n_mats >= 2, f"{filename} has only {n_mats} materials!"

    out_path = os.path.join(OUT_MODELS, filename)
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        export_yup=True,
        export_texcoords=True,
        export_normals=True,
        export_materials='EXPORT',
        export_apply=True,
    )
    sz = os.path.getsize(out_path)
    for bdir in BIN_TARGETS:
        if os.path.isdir(bdir):
            shutil.copy2(out_path, os.path.join(bdir, filename))
    print(f"EXPORTED {filename}: {sz:,} bytes, mats={n_mats}, tris={len(active.data.polygons)}")


# ==============================================================================
# 1. HAT_TINFOIL — Шапка из фольги
# ==============================================================================
def build_hat_tinfoil():
    reset_scene()
    mat_foil = make_mat("TinfoilSilver", (0.86, 0.89, 0.93, 1.0), metallic=0.92, roughness=0.25, tex_name="tex_tinfoil.png")
    mat_foil_inner = make_mat("TinfoilInner", (0.55, 0.58, 0.63, 1.0), metallic=0.80, roughness=0.42, tex_name="tex_tinfoil.png")
    mat_antenna = make_mat("TinfoilAntenna", (0.95, 0.97, 1.0, 1.0), metallic=0.96, roughness=0.16, tex_name="tex_tinfoil.png")

    rng = random.Random(101)
    bm = bmesh.new()
    n_ang = 24
    n_h = 11
    rings = []
    for hi in range(n_h):
        t = hi / (n_h - 1)
        z = -0.026 + t * 0.220  # snug low hairline (-0.026) up to +0.194
        if t < 0.38:
            scale = 1.0 - 0.12 * (t / 0.38)
        else:
            u = (t - 0.38) / 0.62
            scale = 0.88 * ((1.0 - u) ** 1.05) + 0.075
        rx = 0.106 * scale
        ry = 0.123 * scale
        ring = []
        for ai in range(n_ang):
            a = 2.0 * math.pi * ai / n_ang
            fold = 1.0 + 0.055 * math.cos(6.0 * a + t * 0.6) * (0.4 + 0.6 * math.sin(t * math.pi))
            crinkle = 1.0 + (rng.uniform(-0.044, 0.044) if 0 < hi < n_h - 1 else rng.uniform(-0.02, 0.02))
            dz = rng.uniform(-0.005, 0.005) if 0 < hi < n_h - 1 else 0.0
            lean_x = 0.011 * (t ** 2)
            lean_y = 0.013 * (t ** 2)
            x = rx * fold * crinkle * math.cos(a) + lean_x
            y = ry * fold * crinkle * math.sin(a) + lean_y
            ring.append(bm.verts.new((x, y, z + dz)))
        rings.append(ring)
    for hi in range(n_h - 1):
        r0, r1 = rings[hi], rings[hi + 1]
        for ai in range(n_ang):
            a2 = (ai + 1) % n_ang
            bm.faces.new((r0[ai], r0[a2], r1[a2], r1[ai]))
    top_v = bm.verts.new((0.013, 0.015, 0.200))
    for ai in range(n_ang):
        bm.faces.new((rings[-1][ai], rings[-1][(ai + 1) % n_ang], top_v))
    finish_mesh_obj("TinfoilCone", bm, mat_foil, smooth=False, uv_mode="cyl", uv_scale=3.5)

    # Inner shadow cavity shell
    bm_in = bmesh.new()
    r_bot = [bm_in.verts.new((0.095 * math.cos(2*math.pi*i/n_ang), 0.112 * math.sin(2*math.pi*i/n_ang), -0.024)) for i in range(n_ang)]
    r_mid = [bm_in.verts.new((0.086 * math.cos(2*math.pi*i/n_ang), 0.102 * math.sin(2*math.pi*i/n_ang), 0.048)) for i in range(n_ang)]
    for ai in range(n_ang):
        a2 = (ai + 1) % n_ang
        bm_in.faces.new((r_bot[a2], r_bot[ai], r_mid[ai], r_mid[a2]))
    cap_in = bm_in.verts.new((0, 0, 0.064))
    for ai in range(n_ang):
        bm_in.faces.new((r_mid[(ai + 1) % n_ang], r_mid[ai], cap_in))
    finish_mesh_obj("TinfoilInner", bm_in, mat_foil_inner, smooth=False)

    # Thick crumpled upward-folded rim cuff hugging snugly around forehead/hair!
    rim_pts = []
    n_rim = 36
    for i in range(n_rim):
        a = 2.0 * math.pi * i / n_rim
        wob_r = 1.0 + 0.06 * math.sin(5 * a + 0.4) + 0.04 * math.cos(9 * a) + rng.uniform(-0.025, 0.025)
        wob_z = 0.005 * math.sin(4 * a) + rng.uniform(-0.004, 0.004)
        rim_pts.append((0.109 * wob_r * math.cos(a), 0.126 * wob_r * math.sin(a), -0.009 + wob_z))
    bm_rim = make_closed_tube(rim_pts, radius=0.019, segs=7)
    for v in bm_rim.verts:
        v.co.x += rng.uniform(-0.0028, 0.0028)
        v.co.y += rng.uniform(-0.0028, 0.0028)
        v.co.z += rng.uniform(-0.0032, 0.0032)
    finish_mesh_obj("TinfoilRimCuff", bm_rim, mat_foil, smooth=False, uv_mode="cyl", uv_scale=5.0)

    # Chunkier readable twisted foil spiral antenna on top!
    ant_pts = []
    steps = 64
    for i in range(steps):
        u = i / (steps - 1)
        ang = u * 3.25 * 2.0 * math.pi
        rad = 0.017 * (1.0 - 0.58 * u) + 0.004
        z = 0.193 + u * 0.088
        x = 0.012 + rad * math.cos(ang)
        y = 0.014 + rad * math.sin(ang)
        ant_pts.append((x, y, z))
    bm_ant = make_tube_along_points(ant_pts, radius=0.0054, segs=7, cap=True)
    finish_mesh_obj("TinfoilSpiral", bm_ant, mat_antenna, smooth=False, uv_mode="cyl", uv_scale=8.0)

    bm_tip = make_ellipsoid_bm(0.013, 0.013, 0.013, center=(0.012, 0.014, 0.286), u_segs=8, v_segs=6)
    for v in bm_tip.verts:
        v.co += Vector((rng.uniform(-0.0022, 0.0022), rng.uniform(-0.0022, 0.0022), rng.uniform(-0.0022, 0.0022)))
    finish_mesh_obj("TinfoilTipBall", bm_tip, mat_antenna, smooth=False)

    export_glb("hat_tinfoil.glb")


# ==============================================================================
# 2. HAT_USHANKA — Ушанка 90-х
# ==============================================================================
def build_hat_ushanka():
    reset_scene()
    mat_fur = make_mat("UshankaFur", (0.24, 0.18, 0.15, 1.0), metallic=0.0, roughness=0.92, tex_name="tex_ushanka_fur.png")
    mat_cloth = make_mat("UshankaCloth", (0.24, 0.22, 0.18, 1.0), metallic=0.0, roughness=0.86, tex_name="tex_ushanka_cloth.png")
    mat_brass = make_mat("UshankaBrassStar", (0.90, 0.70, 0.24, 1.0), metallic=0.88, roughness=0.26, tex_name="tex_brass.png")
    mat_cord = make_mat("UshankaCord", (0.12, 0.09, 0.08, 1.0), metallic=0.0, roughness=0.85)

    def crown_deform(x, y, z):
        if z < 0:
            z *= 0.25
        return (x, y, z)
    bm_crown = make_ellipsoid_bm(0.106, 0.122, 0.116, center=(0, 0.002, 0.000), u_segs=28, v_segs=18, deform_fn=crown_deform)
    finish_mesh_obj("UshankaCrown", bm_crown, mat_cloth, smooth=True, uv_mode="sphere", uv_scale=3.0)

    def fur_puff(rx, ry, rz, center, rot=(0,0,0), seed=1):
        r = random.Random(seed)
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=16, radius=1.0)
        rot_m = Euler(rot, 'XYZ').to_matrix().to_4x4()
        for v in bm.verts:
            p = Vector((v.co.x * rx, v.co.y * ry, v.co.z * rz))
            bump = 1.0 + 0.045 * math.sin(v.co.x * 7.0 + seed) * math.cos(v.co.z * 6.0) + r.uniform(-0.02, 0.02)
            p *= bump
            p = (rot_m @ p) + Vector(center)
            v.co = p
        return bm

    # Wider snug plush front fur visor flap seamlessly meeting the earflaps!
    bm_front = bmesh.new()
    bmesh.ops.create_uvsphere(bm_front, u_segments=28, v_segments=16, radius=1.0)
    rng = random.Random(202)
    for v in bm_front.verts:
        nx, ny, nz = v.co.x, v.co.y, v.co.z
        ang = nx * 1.28  # ±73 deg so it meets earflaps seamlessly!
        r_arc = 0.121 + ny * 0.026
        z = 0.042 + nz * 0.064
        r_arc += 0.006 * max(0.0, nz)
        bump = 1.0 + 0.03 * math.sin(ang * 9.0) * math.cos(nz * 7.0) + rng.uniform(-0.012, 0.012)
        x = r_arc * bump * math.sin(ang) * 0.94
        y = -r_arc * bump * math.cos(ang) * 1.05
        v.co = Vector((x, y, z))
    finish_mesh_obj("UshankaFrontVisor", bm_front, mat_fur, smooth=True, uv_mode="front", uv_scale=4.0)

    # Thick rear fur neck roll
    bm_back = bmesh.new()
    bmesh.ops.create_uvsphere(bm_back, u_segments=24, v_segments=14, radius=1.0)
    for v in bm_back.verts:
        nx, ny, nz = v.co.x, v.co.y, v.co.z
        ang = nx * 1.38
        r_arc = 0.118 + ny * 0.024
        z = 0.018 + nz * 0.066
        x = r_arc * math.sin(ang) * 0.94
        y = r_arc * math.cos(ang) * 1.06
        v.co = Vector((x, y, z))
    finish_mesh_obj("UshankaBackFur", bm_back, mat_fur, smooth=True, uv_mode="cyl", uv_scale=4.0)

    # Left & Right plush dangling earflaps
    for side in (-1, 1):
        bm_ear = fur_puff(
            rx=0.025, ry=0.058, rz=0.100,
            center=(side * 0.116, 0.004, -0.026),
            rot=(0.0, side * math.radians(-14.0), 0.0),
            seed=210 + side
        )
        for v in bm_ear.verts:
            if side * v.co.x < 0.094:
                v.co.x = side * 0.094
            if v.co.z < -0.048:
                flare = (-0.048 - v.co.z) * 0.35
                v.co.x += side * flare
        finish_mesh_obj(f"UshankaEarflap_{side}", bm_ear, mat_fur, smooth=True, uv_mode="box", uv_scale=5.0)

        cord_pts = [
            (side * 0.132, 0.004, -0.112),
            (side * 0.138, -0.002, -0.138),
            (side * 0.134, 0.004, -0.160),
        ]
        bm_cord = make_tube_along_points(cord_pts, radius=0.0038, segs=6, cap=True)
        finish_mesh_obj(f"UshankaCord_{side}", bm_cord, mat_cord, smooth=True)

    # Crisp 3D beveled 5-pointed vintage brass star emblem centered on front visor!
    bm_star = bmesh.new()
    star_cy = -0.154
    star_cz = 0.048
    r_out = 0.026
    r_in = 0.0108
    ring_verts = []
    for k in range(10):
        ang = math.radians(90.0 + k * 36.0)
        rr = r_out if k % 2 == 0 else r_in
        vx = rr * math.cos(ang)
        vz = star_cz + rr * math.sin(ang)
        vy = star_cy + 0.003 * (abs(vx) / r_out)
        ring_verts.append(bm_star.verts.new((vx, vy, vz)))
    v_front = bm_star.verts.new((0.0, star_cy - 0.0095, star_cz))
    v_back = bm_star.verts.new((0.0, star_cy + 0.006, star_cz))
    for k in range(10):
        k2 = (k + 1) % 10
        bm_star.faces.new((ring_verts[k], ring_verts[k2], v_front))
        bm_star.faces.new((ring_verts[k2], ring_verts[k], v_back))
    finish_mesh_obj("UshankaBrassStar", bm_star, mat_brass, smooth=False, uv_mode="front", uv_scale=12.0)

    export_glb("hat_ushanka.glb")


# ==============================================================================
# 3. HAT_CROWN — Корона Бургер-Босса (Snug 7-point retro foil cardboard crown!)
# ==============================================================================
def build_hat_crown():
    reset_scene()
    mat_gold = make_mat("CrownGoldFoil", (0.95, 0.76, 0.22, 1.0), metallic=0.82, roughness=0.25, tex_name="tex_crown_gold.png")
    mat_inner = make_mat("CrownCardboardIn", (0.68, 0.54, 0.24, 1.0), metallic=0.18, roughness=0.72)
    mat_ruby = make_mat("CrownRuby", (0.88, 0.10, 0.14, 1.0), metallic=0.15, roughness=0.14)
    mat_emerald = make_mat("CrownEmerald", (0.10, 0.74, 0.28, 1.0), metallic=0.15, roughness=0.14)

    n_pts = 7
    segs_per_point = 6
    n_rad = n_pts * segs_per_point
    n_h = 8
    rng = random.Random(303)

    bm = bmesh.new()
    outer_rings, inner_rings = [], []
    thick = 0.0045
    rad_jitter = [rng.uniform(-0.0022, 0.0022) for _ in range(n_rad)]

    for hi in range(n_h):
        t = hi / (n_h - 1)
        o_ring, i_ring = [], []
        for ai in range(n_rad):
            ang = -math.pi / 2.0 + 2.0 * math.pi * ai / n_rad
            phase = (ai % segs_per_point) / float(segs_per_point)
            peak_val = 2.0 * abs(phase - 0.5)
            top_z = 0.074 + 0.064 * peak_val  # seated lower snugly: valleys 0.074, peaks 0.138!
            z = -0.025 + t * (top_z - (-0.025))
            flare = 1.0 + 0.22 * (t ** 1.15)
            crease = 0.0032 * (peak_val - 0.5) * t + rad_jitter[ai] * (0.4 + 0.6 * t)
            rx = 0.095 * flare + crease
            ry = 0.114 * flare + crease
            x_out, y_out = rx * math.cos(ang), ry * math.sin(ang)
            x_in, y_in = (rx - thick) * math.cos(ang), (ry - thick) * math.sin(ang)
            o_ring.append(bm.verts.new((x_out, y_out, z)))
            i_ring.append(bm.verts.new((x_in, y_in, z)))
        outer_rings.append(o_ring)
        inner_rings.append(i_ring)

    uv_layer = bm.loops.layers.uv.verify()
    for hi in range(n_h - 1):
        for ai in range(n_rad):
            a2 = (ai + 1) % n_rad
            f_out = bm.faces.new((outer_rings[hi][ai], outer_rings[hi][a2], outer_rings[hi+1][a2], outer_rings[hi+1][ai]))
            f_out.material_index = 0
            u0, u1 = ai / float(n_rad), (ai + 1) / float(n_rad)
            v0, v1 = hi / float(n_h - 1), (hi + 1) / float(n_h - 1)
            for loop, uv in zip(f_out.loops, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]):
                loop[uv_layer].uv = uv
            f_in = bm.faces.new((inner_rings[hi][a2], inner_rings[hi][ai], inner_rings[hi+1][ai], inner_rings[hi+1][a2]))
            f_in.material_index = 1
            for loop in f_in.loops:
                loop[uv_layer].uv = (0.5, 0.5)

    for ai in range(n_rad):
        a2 = (ai + 1) % n_rad
        f_top = bm.faces.new((outer_rings[-1][ai], outer_rings[-1][a2], inner_rings[-1][a2], inner_rings[-1][ai]))
        f_top.material_index = 0
        f_bot = bm.faces.new((inner_rings[0][ai], inner_rings[0][a2], outer_rings[0][a2], outer_rings[0][ai]))
        f_bot.material_index = 0
        for f in (f_top, f_bot):
            for loop in f.loops:
                loop[uv_layer].uv = (0.5, 0.95)

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("CrownBody")
    bm.to_mesh(mesh)
    bm.free()
    mesh.polygons.foreach_set("use_smooth", [False] * len(mesh.polygons))
    mesh.materials.append(mat_gold)
    mesh.materials.append(mat_inner)
    bpy.context.scene.collection.objects.link(bpy.data.objects.new("CrownBody", mesh))

    for p in range(n_pts):
        ang = -math.pi / 2.0 + 2.0 * math.pi * p / n_pts
        cx = 0.101 * math.cos(ang)
        cy = 0.120 * math.sin(ang)
        cz = 0.008
        bm_j = make_ellipsoid_bm(0.011, 0.008, 0.013, center=(0, 0, 0), u_segs=10, v_segs=8)
        rot_z = Matrix.Rotation(ang + math.pi / 2.0, 4, 'Z')
        for v in bm_j.verts:
            v.co = (rot_z @ v.co) + Vector((cx, cy, cz))
        finish_mesh_obj(f"CrownJewel_{p}", bm_j, mat_ruby if (p % 2 == 0) else mat_emerald, smooth=False)

    export_glb("hat_crown.glb")


# ==============================================================================
# 4. HAT_TRAFFIC_CONE — Дорожный конус (Snug directly on hair crown, zero cuff!)
# ==============================================================================
def build_hat_traffic_cone():
    reset_scene()
    mat_orange = make_mat("TrafficOrange", (0.96, 0.34, 0.05, 1.0), metallic=0.0, roughness=0.44, tex_name="tex_traffic_orange.png")
    mat_white = make_mat("TrafficReflectiveWhite", (0.97, 0.98, 1.0, 1.0), metallic=0.10, roughness=0.20,
                         tex_name="tex_reflective_white.png", emission=(0.95, 0.97, 1.0), emission_strength=0.18)
    mat_rubber = make_mat("TrafficDarkRubber", (0.11, 0.11, 0.12, 1.0), metallic=0.0, roughness=0.78)

    # Seat square base directly & snugly onto the hair crown (z=+0.018), jaunty 8° tilt — NO grey pancake!
    # Also slightly shorter cone (0.295m instead of 0.35m) so the ENTIRE cone + top rim frames cleanly in wardrobe view!
    tilt_mat = Matrix.Translation((0.004, 0.003, 0.017)) @ Euler((math.radians(-4.5), math.radians(8.5), math.radians(12.0)), 'XYZ').to_matrix().to_4x4()

    def apply_tilt(obj):
        obj.matrix_world = tilt_mat @ obj.matrix_world

    def make_rounded_sq_plate(half_w, z0, z1, bevel_r=0.024):
        bm = bmesh.new()
        bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts:
            v.co.x *= half_w * 2.0
            v.co.y *= half_w * 2.0
            v.co.z = z0 if v.co.z < 0 else z1
        vert_edges = [e for e in bm.edges if abs(e.verts[0].co.z - e.verts[1].co.z) > 1e-4]
        bmesh.ops.bevel(bm, geom=vert_edges, offset=bevel_r, segments=4, affect='EDGES', clamp_overlap=True)
        top_edges = [e for e in bm.edges if abs(e.verts[0].co.z - z1) < 1e-4 and abs(e.verts[1].co.z - z1) < 1e-4]
        bmesh.ops.bevel(bm, geom=top_edges, offset=0.004, segments=2, affect='EDGES', clamp_overlap=True)
        return bm

    # Weighted dark rubber bottom lip + stepped orange plinth (slightly recessed center underside so hair fits cleanly!)
    bm_pad = make_rounded_sq_plate(0.110, 0.0, 0.012, bevel_r=0.025)
    apply_tilt(finish_mesh_obj("ConeRubberBase", bm_pad, mat_rubber, smooth=True))

    bm_plinth = make_rounded_sq_plate(0.102, 0.010, 0.032, bevel_r=0.021)
    apply_tilt(finish_mesh_obj("ConeOrangePlinth", bm_plinth, mat_orange, smooth=True))

    # Main tapering orange cone (z=0.028 to 0.300, base r=0.088 -> top r=0.025)
    def cone_r(z):
        t = (z - 0.028) / (0.300 - 0.028)
        return 0.088 * (1.0 - t) + 0.025 * t

    bm_cone = bmesh.new()
    segs = 32
    zs = [0.028, 0.052, 0.125, 0.200, 0.265, 0.296, 0.300]
    rings = []
    for z in zs:
        r = cone_r(z) + (0.008 if z == 0.028 else 0.0)
        rings.append([bm_cone.verts.new((r * math.cos(2*math.pi*i/segs), r * math.sin(2*math.pi*i/segs), z)) for i in range(segs)])
    for k in range(len(rings) - 1):
        for i in range(segs):
            i2 = (i + 1) % segs
            bm_cone.faces.new((rings[k][i], rings[k][i2], rings[k+1][i2], rings[k+1][i]))
    r_inner = 0.017
    ring_in_top = [bm_cone.verts.new((r_inner * math.cos(2*math.pi*i/segs), r_inner * math.sin(2*math.pi*i/segs), 0.300)) for i in range(segs)]
    ring_in_dep = [bm_cone.verts.new((r_inner * math.cos(2*math.pi*i/segs), r_inner * math.sin(2*math.pi*i/segs), 0.265)) for i in range(segs)]
    for i in range(segs):
        i2 = (i + 1) % segs
        bm_cone.faces.new((rings[-1][i], rings[-1][i2], ring_in_top[i2], ring_in_top[i]))
        bm_cone.faces.new((ring_in_top[i], ring_in_top[i2], ring_in_dep[i2], ring_in_dep[i]))
    c_bot = bm_cone.verts.new((0, 0, 0.265))
    for i in range(segs):
        bm_cone.faces.new((ring_in_dep[i], ring_in_dep[(i + 1) % segs], c_bot))
    apply_tilt(finish_mesh_obj("ConeBody", bm_cone, mat_orange, smooth=True, uv_mode="cyl", uv_scale=3.0))

    # TWO crisp raised bright white reflective bands!
    for bi, (zb, zt) in enumerate([(0.105, 0.155), (0.188, 0.228)]):
        bm_b = bmesh.new()
        rb_o, rt_o = cone_r(zb) + 0.0018, cone_r(zt) + 0.0018
        rb_i, rt_i = cone_r(zb) - 0.0005, cone_r(zt) - 0.0005
        bo = [bm_b.verts.new((rb_o * math.cos(2*math.pi*i/segs), rb_o * math.sin(2*math.pi*i/segs), zb)) for i in range(segs)]
        to = [bm_b.verts.new((rt_o * math.cos(2*math.pi*i/segs), rt_o * math.sin(2*math.pi*i/segs), zt)) for i in range(segs)]
        bi_r = [bm_b.verts.new((rb_i * math.cos(2*math.pi*i/segs), rb_i * math.sin(2*math.pi*i/segs), zb)) for i in range(segs)]
        ti_r = [bm_b.verts.new((rt_i * math.cos(2*math.pi*i/segs), rt_i * math.sin(2*math.pi*i/segs), zt)) for i in range(segs)]
        for i in range(segs):
            i2 = (i + 1) % segs
            bm_b.faces.new((bo[i], bo[i2], to[i2], to[i]))
            bm_b.faces.new((to[i], to[i2], ti_r[i2], ti_r[i]))
            bm_b.faces.new((bi_r[i], bi_r[i2], bo[i2], bo[i]))
        apply_tilt(finish_mesh_obj(f"ConeReflectiveBand_{bi}", bm_b, mat_white, smooth=True, uv_mode="cyl", uv_scale=6.0))

    export_glb("hat_traffic_cone.glb")


# ==============================================================================
# 5. HAT_COWBOY — Ковбойская шляпа
# ==============================================================================
def build_hat_cowboy():
    reset_scene()
    mat_leather = make_mat("CowboyLeather", (0.24, 0.14, 0.09, 1.0), metallic=0.0, roughness=0.45, tex_name="tex_cowboy_leather.png")
    mat_band = make_mat("CowboyBraidedBand", (0.48, 0.30, 0.16, 1.0), metallic=0.15, roughness=0.50, tex_name="tex_cowboy_band.png")
    mat_brass = make_mat("CowboyBrassBuckle", (0.90, 0.72, 0.28, 1.0), metallic=0.92, roughness=0.22, tex_name="tex_brass.png")

    bm_brim = bmesh.new()
    n_ang = 48
    n_rad = 10
    top_rings, bot_rings = [], []
    thick = 0.0055
    for ri in range(n_rad):
        u = ri / (n_rad - 1)
        rx = 0.094 + u * 0.114
        ry = 0.112 + u * 0.118
        tr, br = [], []
        for ai in range(n_ang):
            a = 2.0 * math.pi * ai / n_ang
            ca, sa = math.cos(a), math.sin(a)
            side_curl = (ca * ca) * (u ** 1.60) * 0.058
            fb_dip = -(sa * sa) * (u ** 1.25) * (0.020 if sa < 0 else 0.013)
            z = -0.010 + side_curl + fb_dip + (0.003 if ri == n_rad - 1 else 0.0)
            x, y = rx * ca, ry * sa
            tr.append(bm_brim.verts.new((x, y, z + thick * 0.5)))
            br.append(bm_brim.verts.new((x, y, z - thick * 0.5)))
        top_rings.append(tr)
        bot_rings.append(br)
    for ri in range(n_rad - 1):
        for ai in range(n_ang):
            a2 = (ai + 1) % n_ang
            bm_brim.faces.new((top_rings[ri][ai], top_rings[ri][a2], top_rings[ri+1][a2], top_rings[ri+1][ai]))
            bm_brim.faces.new((bot_rings[ri][a2], bot_rings[ri][ai], bot_rings[ri+1][ai], bot_rings[ri+1][a2]))
    for ai in range(n_ang):
        a2 = (ai + 1) % n_ang
        bm_brim.faces.new((top_rings[-1][ai], top_rings[-1][a2], bot_rings[-1][a2], bot_rings[-1][ai]))
        bm_brim.faces.new((bot_rings[0][ai], bot_rings[0][a2], top_rings[0][a2], top_rings[0][ai]))
    finish_mesh_obj("CowboyBrim", bm_brim, mat_leather, smooth=True, uv_mode="box", uv_scale=3.2)

    bm_cr = bmesh.new()
    n_h = 14
    rings = []
    for hi in range(n_h):
        t = hi / (n_h - 1)
        z_base = -0.018 + math.sin(t * math.pi * 0.5) * 0.132
        r_prof = math.cos(max(0.0, (t - 0.55) / 0.45) * math.pi * 0.5) if t > 0.55 else (1.0 - 0.14 * t)
        r_prof = max(0.06, r_prof)
        rx, ry = 0.098 * r_prof, 0.116 * r_prof
        ring = []
        for ai in range(n_ang):
            a = 2.0 * math.pi * ai / n_ang
            ca, sa = math.cos(a), math.sin(a)
            x, y, z = rx * ca, ry * sa, z_base
            # Deeper expressive cattleman's front side pinches & center crown crease!
            front_w = max(0.0, -sa)
            pinch_h = math.exp(-((t - 0.66) / 0.22) ** 2)
            pinch_a = math.exp(-((abs(ca) - 0.66) / 0.28) ** 2)
            x *= (1.0 - front_w * pinch_h * pinch_a * 0.032 * 1.2)
            if t > 0.55:
                crease_w = math.exp(-((x / 0.044) ** 2))
                crease_len = max(0.0, 1.0 - (y / 0.106) ** 2)
                z -= 0.032 * crease_w * crease_len * ((t - 0.55) / 0.45)
            ring.append(bm_cr.verts.new((x, y, z)))
        rings.append(ring)
    for hi in range(n_h - 1):
        for ai in range(n_ang):
            a2 = (ai + 1) % n_ang
            bm_cr.faces.new((rings[hi][ai], rings[hi][a2], rings[hi+1][a2], rings[hi+1][ai]))
    c_top = bm_cr.verts.new((0.0, 0.0, 0.084))
    for ai in range(n_ang):
        bm_cr.faces.new((rings[-1][ai], rings[-1][(ai + 1) % n_ang], c_top))
    finish_mesh_obj("CowboyCrown", bm_cr, mat_leather, smooth=True, uv_mode="cyl", uv_scale=3.5)

    band_pts = [(0.100 * math.cos(2*math.pi*i/n_ang), 0.118 * math.sin(2*math.pi*i/n_ang), 0.000) for i in range(n_ang)]
    finish_mesh_obj("CowboyHatband", make_closed_tube(band_pts, radius=0.0085, segs=10), mat_band, smooth=True, uv_mode="cyl", uv_scale=8.0)

    b_pts = [(0.0, -0.012, -0.009), (0.0, 0.012, -0.009), (0.0, 0.012, 0.009), (0.0, -0.012, 0.009)]
    bm_buck = make_closed_tube(b_pts, radius=0.0032, segs=8)
    for v in bm_buck.verts:
        v.co += Vector((0.104, -0.020, 0.000))
    finish_mesh_obj("CowboyBrassBuckle", bm_buck, mat_brass, smooth=True)

    export_glb("hat_cowboy.glb")


# ==============================================================================
# 6. HAT_CHEF — Колпак повара
# ==============================================================================
def build_hat_chef():
    reset_scene()
    mat_white = make_mat("ChefCottonWhite", (0.97, 0.96, 0.94, 1.0), metallic=0.0, roughness=0.78, tex_name="tex_chef_cotton.png")
    mat_hem = make_mat("ChefStarchedHem", (0.91, 0.90, 0.87, 1.0), metallic=0.0, roughness=0.72, tex_name="tex_chef_cotton.png")

    n_ang = 80
    bm_band = bmesh.new()
    zs = [-0.026, -0.018, -0.008, 0.050, 0.058]  # sits snugly down over forehead hairline!
    o_rings, i_rings = [], []
    for zi, z in enumerate(zs):
        flare = 1.0 + 0.03 * ((z + 0.026) / 0.084)
        rib = 0.0022 if zi in (1, 3) else 0.0
        rx, ry = 0.098 * flare + rib, 0.115 * flare + rib
        o_rings.append([bm_band.verts.new((rx * math.cos(2*math.pi*i/n_ang), ry * math.sin(2*math.pi*i/n_ang), z)) for i in range(n_ang)])
        i_rings.append([bm_band.verts.new(((rx - 0.006) * math.cos(2*math.pi*i/n_ang), (ry - 0.006) * math.sin(2*math.pi*i/n_ang), z)) for i in range(n_ang)])
    for k in range(len(zs) - 1):
        for i in range(n_ang):
            i2 = (i + 1) % n_ang
            bm_band.faces.new((o_rings[k][i], o_rings[k][i2], o_rings[k+1][i2], o_rings[k+1][i]))
            bm_band.faces.new((i_rings[k][i2], i_rings[k][i], i_rings[k+1][i], i_rings[k+1][i2]))
    for i in range(n_ang):
        i2 = (i + 1) % n_ang
        bm_band.faces.new((i_rings[0][i], i_rings[0][i2], o_rings[0][i2], o_rings[0][i]))
    finish_mesh_obj("ChefHeadband", bm_band, mat_hem, smooth=True, uv_mode="cyl", uv_scale=4.0)

    bm_puff = bmesh.new()
    n_h = 20
    rings = []
    n_pleats = 20
    for hi in range(n_h):
        t = hi / (n_h - 1)
        if t < 0.55:
            u = t / 0.55
            swell = 1.0 + 0.48 * math.sin(u * math.pi * 0.5)
            z = 0.054 + u * 0.106  # 0.054 -> 0.160
        else:
            u = (t - 0.55) / 0.45
            swell = 1.48 * math.cos(u * math.pi * 0.495)
            z = 0.160 + math.sin(u * math.pi * 0.5) * 0.072  # 0.160 -> 0.232 (frames cleanly!)
        pleat_amp = 0.0105 * math.sin(t * math.pi) * (1.0 - 0.35 * t)
        lean_x = 0.008 * (t ** 1.6)
        lean_y = 0.011 * (t ** 1.6)
        ring = []
        for ai in range(n_ang):
            a = 2.0 * math.pi * ai / n_ang
            pleat = math.cos(n_pleats * a)
            rx = max(0.006, 0.097 * swell + pleat_amp * pleat)
            ry = max(0.006, 0.111 * swell + pleat_amp * pleat)
            dz = 0.004 * pleat * math.sin(t * math.pi) * t
            ring.append(bm_puff.verts.new((rx * math.cos(a) + lean_x, ry * math.sin(a) + lean_y, z + dz)))
        rings.append(ring)
    for hi in range(n_h - 1):
        for ai in range(n_ang):
            a2 = (ai + 1) % n_ang
            bm_puff.faces.new((rings[hi][ai], rings[hi][a2], rings[hi+1][a2], rings[hi+1][ai]))
    top_v = bm_puff.verts.new((0.008, 0.011, 0.234))
    for ai in range(n_ang):
        bm_puff.faces.new((rings[-1][ai], rings[-1][(ai + 1) % n_ang], top_v))
    finish_mesh_obj("ChefPleatedPuff", bm_puff, mat_white, smooth=True, uv_mode="cyl", uv_scale=3.0)

    export_glb("hat_chef.glb")


# ==============================================================================
# 7. HAT_PIRATE — Пиратская треуголка
# ==============================================================================
def build_hat_pirate():
    reset_scene()
    mat_leather = make_mat("PirateWeatheredLeather", (0.17, 0.13, 0.11, 1.0), metallic=0.0, roughness=0.58, tex_name="tex_pirate_leather.png")
    mat_braid = make_mat("PirateGoldBraid", (0.88, 0.69, 0.24, 1.0), metallic=0.86, roughness=0.28, tex_name="tex_pirate_braid.png")
    mat_cockade = make_mat("PirateCrimsonCockade", (0.64, 0.10, 0.12, 1.0), metallic=0.0, roughness=0.68)

    def dome_def(x, y, z):
        if z < 0:
            z *= 0.22
        return (x, y, z)
    bm_cr = make_ellipsoid_bm(0.100, 0.116, 0.102, center=(0, 0, -0.006), u_segs=32, v_segs=18, deform_fn=dome_def)
    finish_mesh_obj("PirateCrown", bm_cr, mat_leather, smooth=True, uv_mode="sphere", uv_scale=3.0)

    bm_brim = bmesh.new()
    n_ang = 72
    n_rad = 10
    top_rings, bot_rings = [], []
    outer_edge_pts = []
    thick = 0.0055

    for ri in range(n_rad):
        u = ri / (n_rad - 1)
        tr, br = [], []
        for ai in range(n_ang):
            a = 2.0 * math.pi * ai / n_ang
            c3 = math.cos(3.0 * (a + math.pi / 2.0))
            corner_w = 0.5 * (1.0 + c3)
            side_w = 1.0 - corner_w

            r_in_x, r_in_y = 0.095, 0.112
            r_base = 1.0 / math.sqrt((math.cos(a) / r_in_x) ** 2 + (math.sin(a) / r_in_y) ** 2)
            brim_w = 0.036 + 0.096 * (corner_w ** 1.45)
            if math.sin(a) < -0.5:
                brim_w += 0.012 * ((-math.sin(a) - 0.5) / 0.5)

            r_cur = r_base + brim_w * (u * (1.0 - 0.28 * side_w * (u ** 1.5)))
            z_cur = -0.014 + (0.098 * side_w + 0.046 * corner_w) * (u ** 1.35)
            if u > 0.7:
                z_cur -= 0.008 * corner_w * ((u - 0.7) / 0.3)

            x, y = r_cur * math.cos(a), r_cur * math.sin(a)
            if ri == n_rad - 1:
                outer_edge_pts.append((x, y, z_cur))

            nx, ny, nz = -math.cos(a) * side_w * 0.75, -math.sin(a) * side_w * 0.75, 1.0 - 0.45 * side_w
            nv = Vector((nx, ny, nz)).normalized() * (thick * 0.5)
            tr.append(bm_brim.verts.new(Vector((x, y, z_cur)) + nv))
            br.append(bm_brim.verts.new(Vector((x, y, z_cur)) - nv))
        top_rings.append(tr)
        bot_rings.append(br)

    for ri in range(n_rad - 1):
        for ai in range(n_ang):
            a2 = (ai + 1) % n_ang
            bm_brim.faces.new((top_rings[ri][ai], top_rings[ri][a2], top_rings[ri+1][a2], top_rings[ri+1][ai]))
            bm_brim.faces.new((bot_rings[ri][a2], bot_rings[ri][ai], bot_rings[ri+1][ai], bot_rings[ri+1][a2]))
    for ai in range(n_ang):
        a2 = (ai + 1) % n_ang
        bm_brim.faces.new((top_rings[-1][ai], top_rings[-1][a2], bot_rings[-1][a2], bot_rings[-1][ai]))
        bm_brim.faces.new((bot_rings[0][ai], bot_rings[0][a2], top_rings[0][a2], top_rings[0][ai]))
    finish_mesh_obj("PirateTricorneBrim", bm_brim, mat_leather, smooth=True, uv_mode="box", uv_scale=3.5)

    finish_mesh_obj("PirateGoldBraid", make_closed_tube(outer_edge_pts, radius=0.0056, segs=10), mat_braid, smooth=True, uv_mode="cyl", uv_scale=12.0)

    bm_cock = make_ellipsoid_bm(0.018, 0.007, 0.022, center=(0, 0, 0), u_segs=16, v_segs=10)
    cock_m = Matrix.Translation((0.108, -0.072, 0.044)) @ Euler((math.radians(10), math.radians(-18), math.radians(52)), 'XYZ').to_matrix().to_4x4()
    for v in bm_cock.verts:
        v.co = cock_m @ v.co
    finish_mesh_obj("PirateCockade", bm_cock, mat_cockade, smooth=True)

    bm_btn = make_ellipsoid_bm(0.0075, 0.005, 0.0075, center=(0, 0, 0), u_segs=12, v_segs=8)
    btn_m = Matrix.Translation((0.112, -0.075, 0.044)) @ Euler((math.radians(10), math.radians(-18), math.radians(52)), 'XYZ').to_matrix().to_4x4()
    for v in bm_btn.verts:
        v.co = btn_m @ v.co
    finish_mesh_obj("PirateCockadeButton", bm_btn, mat_braid, smooth=True)

    export_glb("hat_pirate.glb")


# ==============================================================================
# 8. HAT_FROG_HEAD — Джаба-жаба на голове
# ==============================================================================
def build_hat_frog_head():
    reset_scene()
    mat_green = make_mat("FrogEmeraldShell", (0.14, 0.66, 0.28, 1.0), metallic=0.06, roughness=0.22, tex_name="tex_frog_skin.png")
    mat_belly = make_mat("FrogCreamBelly", (0.78, 0.90, 0.72, 1.0), metallic=0.0, roughness=0.45)
    mat_gold_eye = make_mat("FrogGoldenIris", (0.98, 0.74, 0.10, 1.0), metallic=0.22, roughness=0.14,
                            emission=(0.95, 0.68, 0.08), emission_strength=0.15)
    mat_pupil = make_mat("FrogPupilBlack", (0.02, 0.02, 0.03, 1.0), metallic=0.0, roughness=0.08)
    mat_glint = make_mat("FrogEyeGlint", (1.0, 1.0, 1.0, 1.0), metallic=0.0, roughness=0.05,
                         emission=(1.0, 1.0, 1.0), emission_strength=0.6)
    mat_brass = make_mat("FrogWindupBrass", (0.92, 0.73, 0.24, 1.0), metallic=0.92, roughness=0.20, tex_name="tex_brass.png")

    def body_deform(x, y, z):
        if z < 0:
            z *= 0.64
        if y < 0:
            x *= 1.0 + 0.08 * math.sin(min(1.0, abs(y) / 0.09) * math.pi)
        if y < -0.055 and abs(z + 0.008) < 0.012:
            y += 0.004 * (1.0 - abs(z + 0.008) / 0.012)
        return (x, y, z)

    bm_body = make_ellipsoid_bm(0.082, 0.094, 0.062, center=(0, -0.002, 0.067), u_segs=32, v_segs=22, deform_fn=body_deform)
    finish_mesh_obj("FrogBody", bm_body, mat_green, smooth=True, uv_mode="sphere", uv_scale=2.5)

    bm_belly = make_ellipsoid_bm(0.066, 0.074, 0.032, center=(0, -0.016, 0.047), u_segs=24, v_segs=14)
    finish_mesh_obj("FrogBelly", bm_belly, mat_belly, smooth=True)

    smile_pts = []
    for i in range(15):
        t = (i / 14.0) * 2.0 - 1.0
        ang = t * 0.78
        smile_pts.append((0.066 * math.sin(ang), -0.090 * math.cos(ang) - 0.004, 0.057 + 0.008 * (t * t)))
    finish_mesh_obj("FrogSmile", make_tube_along_points(smile_pts, radius=0.0026, segs=6, cap=True), mat_pupil, smooth=True)

    for side in (-1, 1):
        ex, ey, ez = side * 0.044, -0.052, 0.121
        finish_mesh_obj(f"FrogEyeHood_{side}", make_ellipsoid_bm(0.026, 0.026, 0.027, center=(ex, ey, ez), u_segs=22, v_segs=14), mat_green, smooth=True)
        gx, gy, gz = ex + side * 0.005, ey - 0.012, ez + 0.002
        finish_mesh_obj(f"FrogGoldIris_{side}", make_ellipsoid_bm(0.020, 0.018, 0.020, center=(gx, gy, gz), u_segs=20, v_segs=14), mat_gold_eye, smooth=True)
        finish_mesh_obj(f"FrogPupil_{side}", make_ellipsoid_bm(0.0115, 0.007, 0.0095, center=(gx + side * 0.002, gy - 0.014, gz), u_segs=16, v_segs=10), mat_pupil, smooth=True)
        finish_mesh_obj(f"FrogGlint_{side}", make_ellipsoid_bm(0.0042, 0.0032, 0.0042, center=(gx - 0.004, gy - 0.0185, gz + 0.0055), u_segs=10, v_segs=8), mat_glint, smooth=True)

    for side in (-1, 1):
        bm_thigh = make_ellipsoid_bm(0.032, 0.046, 0.032, center=(side * 0.072, 0.036, 0.051), u_segs=18, v_segs=12)
        rot_t = Matrix.Translation((side * 0.072, 0.036, 0.051)) @ Euler((math.radians(15), math.radians(side * 18), math.radians(side * -18)), 'XYZ').to_matrix().to_4x4() @ Matrix.Translation((-side * 0.072, -0.036, -0.051))
        for v in bm_thigh.verts:
            v.co = rot_t @ v.co
        finish_mesh_obj(f"FrogThigh_{side}", bm_thigh, mat_green, smooth=True)
        finish_mesh_obj(f"FrogHindFoot_{side}", make_ellipsoid_bm(0.022, 0.026, 0.010, center=(side * 0.088, 0.016, 0.022), u_segs=14, v_segs=10), mat_green, smooth=True)

        arm_pts = [(side * 0.062, -0.044, 0.049), (side * 0.066, -0.070, 0.034), (side * 0.056, -0.086, 0.020)]
        finish_mesh_obj(f"FrogFrontArm_{side}", make_tube_along_points(arm_pts, radius=0.013, segs=10, cap=True), mat_green, smooth=True)
        for ti, ang_off in enumerate((-0.42, 0.0, 0.42)):
            tx, ty = side * 0.056 + math.sin(ang_off) * 0.014, -0.086 - math.cos(ang_off) * 0.016
            finish_mesh_obj(f"FrogToe_{side}_{ti}", make_tube_along_points([(side * 0.056, -0.084, 0.021), (tx, ty, 0.015)], radius=0.0055, segs=8, cap=True), mat_green, smooth=True)

    # Clockwork brass wind-up key angled 36° around shaft so double-loop bow reads clearly from BOTH front 3/4 AND side!
    key_pivot = Matrix.Translation((0.0, 0.060, 0.099)) @ Euler((math.radians(-44.0), 0.0, math.radians(36.0)), 'XYZ').to_matrix().to_4x4()
    bm_col = make_ellipsoid_bm(0.015, 0.015, 0.008, center=(0, 0, 0.002), u_segs=16, v_segs=8)
    for v in bm_col.verts:
        v.co = key_pivot @ v.co
    finish_mesh_obj("FrogKeyCollar", bm_col, mat_brass, smooth=True)

    bm_shaft = make_tube_along_points([(0, 0, 0.0), (0, 0, 0.058)], radius=0.0064, segs=12, cap=True)
    for v in bm_shaft.verts:
        v.co = key_pivot @ v.co
    finish_mesh_obj("FrogKeyShaft", bm_shaft, mat_brass, smooth=True)

    for side in (-1, 1):
        loop_pts = [key_pivot @ Vector((side * (0.022 + 0.019 * math.cos(2*math.pi*i/24)), 0.0, 0.072 + 0.022 * math.sin(2*math.pi*i/24))) for i in range(24)]
        finish_mesh_obj(f"FrogKeyLoop_{side}", make_closed_tube(loop_pts, radius=0.0060, segs=10), mat_brass, smooth=True)

    export_glb("hat_frog_head.glb")


# ==============================================================================
# SHARED EYEWEAR HELPERS — Snug nose-bridge fit & clean ear hook clearance!
# ==============================================================================
def build_temple_points(side, start_x, start_y, start_z):
    return [
        (side * start_x, start_y, start_z),
        (side * max(start_x + 0.011, 0.085), start_y + 0.022, start_z + 0.001),
        (side * 0.0942, 0.056, start_z + 0.001),
        (side * 0.0942, 0.096, start_z + 0.000),
        (side * 0.0910, 0.114, start_z - 0.006),
        (side * 0.0860, 0.125, start_z - 0.020),
        (side * 0.0830, 0.129, start_z - 0.032),
    ]

def make_curved_lens_mesh(pts_2d, thickness, y_offset, side, wrap_deg=8.0):
    bm = bmesh.new()
    n = len(pts_2d)
    rot_wrap = Matrix.Rotation(math.radians(-side * wrap_deg), 4, 'Z')
    cx = sum(p[0] for p in pts_2d) / n
    cz = sum(p[1] for p in pts_2d) / n
    front_v, back_v = [], []
    rim_pts = []
    for px, pz in pts_2d:
        dx = (px - cx) / 0.032
        dz = (pz - cz) / 0.026
        bulge = -0.0020 * max(0.0, 1.0 - 0.45 * (dx*dx + dz*dz))
        p_w = (rot_wrap @ Vector((px - cx, bulge, pz - cz))) + Vector((cx, y_offset, cz))
        rim_pts.append(p_w)
        front_v.append(bm.verts.new(p_w + Vector((0, -thickness * 0.5, 0))))
        back_v.append(bm.verts.new(p_w + Vector((0, +thickness * 0.5, 0))))
    c_f = bm.verts.new(Vector((cx, y_offset - 0.0020 - thickness * 0.5, cz)))
    c_b = bm.verts.new(Vector((cx, y_offset - 0.0020 + thickness * 0.5, cz)))
    for i in range(n):
        i2 = (i + 1) % n
        bm.faces.new((front_v[i], front_v[i2], c_f))
        bm.faces.new((back_v[i2], back_v[i], c_b))
        bm.faces.new((front_v[i2], front_v[i], back_v[i], back_v[i2]))
    return bm, rim_pts


# ==============================================================================
# 9. EYE_AVIATORS — Авиаторы 90-х
# ==============================================================================
def build_eye_aviators():
    reset_scene()
    mat_gold = make_mat("AviatorBrushedGold", (0.92, 0.74, 0.28, 1.0), metallic=0.94, roughness=0.20, tex_name="tex_brass.png")
    mat_lens = make_mat("AviatorSmokedAmber", (0.20, 0.11, 0.04, 1.0), metallic=0.22, roughness=0.06, tex_name="tex_aviator_lens.png")
    mat_tip = make_mat("AviatorEarTip", (0.18, 0.10, 0.05, 1.0), metallic=0.0, roughness=0.35, tex_name="tex_tortoiseshell.png")

    def aviator_contour(side, n=32):
        pts = []
        cx = side * 0.0360
        cz = 0.000
        for i in range(n):
            a = 2.0 * math.pi * i / n
            lx = math.cos(a) * 0.0278
            lz = math.sin(a) * 0.0225
            if lz > 0.014:
                lz = 0.014 + (lz - 0.014) * 0.45
            outer = lx * side
            if lz < 0:
                droop = 0.0068 * (0.5 + 0.5 * (outer / 0.0278)) * (-lz / 0.0225)
                lz -= droop
                if outer < 0:
                    lx -= side * (-outer) * 0.22 * (-lz / 0.0225)
            pts.append((cx + lx, cz + lz))
        return pts

    for side in (-1, 1):
        c2d = aviator_contour(side, 32)
        bm_lens, rim_pts = make_curved_lens_mesh(c2d, thickness=0.0022, y_offset=-0.0046, side=side, wrap_deg=8.0)
        uv_layer = bm_lens.loops.layers.uv.verify()
        bmesh.ops.recalc_face_normals(bm_lens, faces=bm_lens.faces)
        for f in bm_lens.faces:
            for loop in f.loops:
                u = (loop.vert.co.x * side - 0.008) / 0.058
                v = 1.0 - (loop.vert.co.z - (-0.028)) / 0.052
                loop[uv_layer].uv = (max(0.0, min(1.0, u)), max(0.0, min(1.0, 1.0 - v)))
        mesh = bpy.data.meshes.new(f"AviatorLens_{side}")
        bm_lens.to_mesh(mesh)
        bm_lens.free()
        mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
        mesh.materials.append(mat_lens)
        bpy.context.scene.collection.objects.link(bpy.data.objects.new(f"AviatorLens_{side}", mesh))

        finish_mesh_obj(f"AviatorRim_{side}", make_closed_tube(rim_pts, radius=0.0019, segs=8), mat_gold, smooth=True)

        tpts = build_temple_points(side, start_x=0.064, start_y=-0.002, start_z=0.008)
        finish_mesh_obj(f"AviatorTemple_{side}", make_tube_along_points(tpts[:5], radius=0.0017, segs=8, cap=True), mat_gold, smooth=True)
        finish_mesh_obj(f"AviatorEarTip_{side}", make_tube_along_points(tpts[4:], radius=0.0027, segs=8, cap=True), mat_tip, smooth=True)
        finish_mesh_obj(f"AviatorPad_{side}", make_tube_along_points([(side * 0.011, -0.004, 0.004), (side * 0.009, 0.002, 0.000)], radius=0.0012, segs=6, cap=True), mat_gold, smooth=True)

    # Iconic DOUBLE NOSE BRIDGE aligned right along brow height (+0.0175) & bridge (+0.0085)!
    finish_mesh_obj("AviatorSweatbar", make_tube_along_points([(-0.024, -0.0068, 0.0175), (0.0, -0.0076, 0.0180), (0.024, -0.0068, 0.0175)], radius=0.0019, segs=8, cap=True), mat_gold, smooth=True)
    finish_mesh_obj("AviatorLowerBridge", make_tube_along_points([(-0.013, -0.0062, 0.0080), (0.0, -0.0075, 0.0110), (0.013, -0.0062, 0.0080)], radius=0.0017, segs=8, cap=True), mat_gold, smooth=True)

    export_glb("eye_aviators.glb")


# ==============================================================================
# 10. EYE_MATRIX — Тёмные «Матрица»
# ==============================================================================
def build_eye_matrix():
    reset_scene()
    mat_obsidian = make_mat("MatrixObsidianLens", (0.022, 0.025, 0.030, 1.0), metallic=0.38, roughness=0.04)
    mat_steel = make_mat("MatrixDarkSteel", (0.20, 0.21, 0.25, 1.0), metallic=0.94, roughness=0.20)

    def matrix_contour(side, n=28):
        pts = []
        cx = side * 0.0360
        cz = 0.0045
        for i in range(n):
            a = 2.0 * math.pi * i / n
            lx = math.cos(a) * 0.0290
            lz = math.sin(a) * 0.0132
            outer = lx * side
            lz += 0.0024 * (outer / 0.0290)
            if lz > 0.0095:
                lz = 0.0095 + (lz - 0.0095) * 0.55
            pts.append((cx + lx, cz + lz))
        return pts

    for side in (-1, 1):
        c2d = matrix_contour(side, 28)
        bm_lens, _ = make_curved_lens_mesh(c2d, thickness=0.0032, y_offset=-0.0052, side=side, wrap_deg=12.0)
        finish_mesh_obj(f"MatrixLens_{side}", bm_lens, mat_obsidian, smooth=True)

        finish_mesh_obj(f"MatrixLug_{side}", make_ellipsoid_bm(0.0045, 0.0035, 0.0030, center=(side * 0.064, -0.001, 0.0065), u_segs=8, v_segs=6), mat_steel, smooth=True)
        tpts = build_temple_points(side, start_x=0.065, start_y=-0.001, start_z=0.0065)
        finish_mesh_obj(f"MatrixTemple_{side}", make_tube_along_points(tpts, radius=0.0018, segs=8, cap=True), mat_steel, smooth=True)

    bridge = [(-0.011, -0.0055, 0.0075), (-0.005, -0.0075, 0.0105), (0.0, -0.0080, 0.0110), (0.005, -0.0075, 0.0105), (0.011, -0.0055, 0.0075)]
    finish_mesh_obj("MatrixBridge", make_tube_along_points(bridge, radius=0.0020, segs=8, cap=True), mat_steel, smooth=True)

    export_glb("eye_matrix.glb")


# ==============================================================================
# 11. EYE_ACCOUNTANT — Очки бухгалтера
# ==============================================================================
def build_eye_accountant():
    reset_scene()
    mat_tort = make_mat("AccountantTortoiseshell", (0.28, 0.14, 0.06, 1.0), metallic=0.0, roughness=0.25, tex_name="tex_tortoiseshell.png")
    mat_glass = make_mat("AccountantClearGlass", (0.82, 0.90, 0.96, 0.36), metallic=0.05, roughness=0.05,
                         tex_name="tex_accountant_glass.png", alpha_blend=True)
    mat_rivet = make_mat("AccountantSilverRivet", (0.88, 0.90, 0.94, 1.0), metallic=0.92, roughness=0.20)

    def rect_rounded_contour(side, w=0.0258, h=0.0172, rad=0.0052):
        cx = side * 0.0360
        cz = 0.0025  # raised cleanly so top acetate frame sits snugly right over brow!
        corners = [
            (w - rad, h - rad, 0.0, math.pi * 0.5),
            (-(w - rad), h - rad, math.pi * 0.5, math.pi),
            (-(w - rad), -(h - rad), math.pi, math.pi * 1.5),
            (w - rad, -(h - rad), math.pi * 1.5, math.pi * 2.0),
        ]
        pts = []
        for x0, z0, a0, a1 in corners:
            for k in range(5):
                a = a0 + (a1 - a0) * (k / 4.0)
                pts.append((cx + x0 + rad * math.cos(a), cz + z0 + rad * math.sin(a)))
        return pts

    for side in (-1, 1):
        c2d = rect_rounded_contour(side)
        bm_lens, rim_pts = make_curved_lens_mesh(c2d, thickness=0.0018, y_offset=-0.0046, side=side, wrap_deg=4.0)
        uv_layer = bm_lens.loops.layers.uv.verify()
        bmesh.ops.recalc_face_normals(bm_lens, faces=bm_lens.faces)
        for f in bm_lens.faces:
            for loop in f.loops:
                u = (loop.vert.co.x * side - 0.010) / 0.054
                v = (loop.vert.co.z - (-0.016)) / 0.038
                loop[uv_layer].uv = (max(0.0, min(1.0, u)), max(0.0, min(1.0, v)))
        mesh = bpy.data.meshes.new(f"AccountantGlass_{side}")
        bm_lens.to_mesh(mesh)
        bm_lens.free()
        mesh.polygons.foreach_set("use_smooth", [True] * len(mesh.polygons))
        mesh.materials.append(mat_glass)
        bpy.context.scene.collection.objects.link(bpy.data.objects.new(f"AccountantGlass_{side}", mesh))

        finish_mesh_obj(f"AccountantFrame_{side}", make_closed_tube(rim_pts, radius=0.0042, segs=10), mat_tort, smooth=True, uv_mode="front", uv_scale=8.0)
        finish_mesh_obj(f"AccountantLug_{side}", make_ellipsoid_bm(0.0075, 0.0040, 0.0048, center=(side * 0.065, -0.004, 0.0125), u_segs=10, v_segs=8), mat_tort, smooth=True, uv_mode="front", uv_scale=8.0)
        finish_mesh_obj(f"AccountantRivet_{side}", make_ellipsoid_bm(0.0024, 0.0018, 0.0014, center=(side * 0.065, -0.0078, 0.0125), u_segs=8, v_segs=6), mat_rivet, smooth=True)

        tpts = build_temple_points(side, start_x=0.068, start_y=-0.003, start_z=0.0115)
        finish_mesh_obj(f"AccountantTemple_{side}", make_tube_along_points(tpts, radius=0.0036, segs=10, cap=True, radius_fn=lambda u: 0.0038 * (1.0 - 0.25 * u)), mat_tort, smooth=True, uv_mode="box", uv_scale=8.0)

    bridge = [(-0.014, -0.0052, 0.0115), (-0.006, -0.0065, 0.0145), (0.0, -0.0070, 0.0150), (0.006, -0.0065, 0.0145), (0.014, -0.0052, 0.0115)]
    finish_mesh_obj("AccountantBridge", make_tube_along_points(bridge, radius=0.0043, segs=10, cap=True), mat_tort, smooth=True, uv_mode="front", uv_scale=8.0)

    export_glb("eye_accountant.glb")


# ==============================================================================
# 12. EYE_DEALWITHIT — Пиксельные «Deal With It»
# ==============================================================================
def build_eye_dealwithit():
    reset_scene()
    mat_black = make_mat("DealWithItBlack", (0.035, 0.035, 0.04, 1.0), metallic=0.04, roughness=0.62)
    mat_white = make_mat("DealWithItGlintWhite", (0.98, 0.99, 1.0, 1.0), metallic=0.0, roughness=0.25,
                         emission=(0.95, 0.97, 1.0), emission_strength=0.22)

    px = 0.0060
    thick_y = 0.0062
    y_front = -0.0064
    z_top = 0.0168

    rows = [
        "#########################",
        ".##WW######...##WW######.",
        "..##WW####.....##WW####..",
        "...######.......######...",
        "....####.........####....",
    ]
    bm_blk = bmesh.new()
    bm_wht = bmesh.new()

    def add_box(bm, cx, cy, cz, sx, sy, sz):
        m = Matrix.Translation((cx, cy, cz)) @ Matrix.Diagonal((sx, sy, sz, 1.0))
        bmesh.ops.create_cube(bm, size=1.0, matrix=m)

    for ri, rstr in enumerate(rows):
        cz = z_top - ri * px
        for ci, ch in enumerate(rstr):
            if ch == '.':
                continue
            cx = (ci - 12) * px
            add_box(bm_blk, cx, y_front, cz, px * 1.002, thick_y, px * 1.002)
            if ch == 'W':
                add_box(bm_wht, cx, y_front - thick_y * 0.5 - 0.0007, cz, px * 0.96, 0.0014, px * 0.96)

    for side in (-1, 1):
        steps = [
            (side * 0.078, -0.003, z_top, px * 1.6, px * 1.4, px * 1.0),
            (side * 0.088,  0.007, z_top, px * 1.6, px * 2.2, px * 1.0),
            (side * 0.095,  0.056, z_top, px * 0.9, 0.092,    px * 1.0),
            (side * 0.094,  0.106, z_top - px * 0.6, px * 0.9, px * 1.6, px * 1.2),
            (side * 0.090,  0.114, z_top - px * 1.8, px * 0.9, px * 1.4, px * 1.6),
            (side * 0.086,  0.120, z_top - px * 3.2, px * 0.9, px * 1.2, px * 1.8),
        ]
        for cx, cy, cz, sx, sy, sz in steps:
            add_box(bm_blk, cx, cy, cz, sx, sy, sz)

    finish_mesh_obj("DealWithItBody", bm_blk, mat_black, smooth=False)
    finish_mesh_obj("DealWithItWhiteGlints", bm_wht, mat_white, smooth=False)

    export_glb("eye_dealwithit.glb")


if __name__ == "__main__":
    build_hat_tinfoil()
    build_hat_ushanka()
    build_hat_crown()
    build_hat_traffic_cone()
    build_hat_cowboy()
    build_hat_chef()
    build_hat_pirate()
    build_hat_frog_head()
    build_eye_aviators()
    build_eye_matrix()
    build_eye_accountant()
    build_eye_dealwithit()
    print("ALL_12_GLBS_V2_BUILT_AND_COPIED")
