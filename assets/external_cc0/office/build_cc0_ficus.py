import bpy
import bmesh
import math
import os
from pathlib import Path
from mathutils import Vector, Matrix, Euler

ROOT = Path(__file__).resolve().parents[3]
TEXTURES = ROOT / "assets/external_cc0/textures"
OFFICE_CC0 = ROOT / "assets/external_cc0/office"
OUT_GLB = OFFICE_CC0 / "FicusPlant_CC0.glb"

def clear_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def rl_pos(p):
    return Vector((p[0], -p[2], p[1]))

def make_mat(name, rgba, metallic=0.05, roughness=0.45):
    mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    if bsdf:
        bsdf.inputs["Base Color"].default_value = rgba
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = roughness
    return mat

def make_cc0_wood_bark_mat():
    mat = make_mat("CC0_FicusBark", (0.22, 0.14, 0.09, 1.0), metallic=0.0, roughness=0.72)
    wood_col = TEXTURES / "Wood092_2K_Color.jpg"
    wood_rgh = TEXTURES / "Wood092_2K_Roughness.jpg"
    if wood_col.exists():
        import numpy as np
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        bsdf = nodes.get("Principled BSDF")
        tex = nodes.new("ShaderNodeTexImage")
        img = bpy.data.images.load(str(wood_col))
        img.scale(512, 512)
        px = np.empty(len(img.pixels), dtype=np.float32)
        img.pixels.foreach_get(px)
        px.reshape(-1, 4)[:, :3] *= (0.34, 0.22, 0.14)
        stained = bpy.data.images.new("StainedFicusBark", 512, 512)
        stained.pixels.foreach_set(px)
        stained.pack()
        tex.image = stained
        links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if wood_rgh.exists():
            rtex = nodes.new("ShaderNodeTexImage")
            rimg = bpy.data.images.load(str(wood_rgh))
            rimg.scale(512, 512)
            rimg.pack()
            rimg.colorspace_settings.name = "Non-Color"
            rtex.image = rimg
            links.new(rtex.outputs["Color"], bsdf.inputs["Roughness"])
    return mat

def add_tapered_tube(name, pts_radii, mat, sides=18):
    verts = []
    faces = []
    for p, r in pts_radii:
        center = rl_pos(p)
        for i in range(sides):
            a = i * math.tau / sides
            verts.append(center + Vector((r * math.cos(a), r * math.sin(a), 0.0)))
    for k in range(len(pts_radii) - 1):
        b0 = k * sides
        b1 = (k + 1) * sides
        for i in range(sides):
            inext = (i + 1) % sides
            faces.append((b0 + i, b0 + inext, b1 + inext, b1 + i))
    faces.append(tuple(reversed(range(sides))))
    last = (len(pts_radii) - 1) * sides
    faces.append(tuple(range(last, last + sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        poly.use_smooth = True
        for loop_idx in poly.loop_indices:
            v = mesh.vertices[mesh.loops[loop_idx].vertex_index].co
            uv.data[loop_idx].uv = (math.atan2(v.y, v.x) / math.tau + 0.5, v.z * 0.8)
    return obj

def add_branch_curve(name, p0, p_ctrl, p1, r0, r1, mat, steps=10, sides=10):
    v0 = rl_pos(p0)
    vc = rl_pos(p_ctrl)
    v1 = rl_pos(p1)
    pts = []
    for i in range(steps + 1):
        t = i / steps
        pt = (1 - t)**2 * v0 + 2 * (1 - t) * t * vc + t**2 * v1
        rad = r0 * (1 - t) + r1 * t
        pts.append((pt, rad))
    verts = []
    faces = []
    for idx, (pt, rad) in enumerate(pts):
        if idx < len(pts) - 1:
            tang = (pts[idx + 1][0] - pt).normalized()
        else:
            tang = (pt - pts[idx - 1][0]).normalized()
        up = Vector((0, 0, 1)) if abs(tang.z) < 0.9 else Vector((1, 0, 0))
        right = tang.cross(up).normalized()
        ortho = right.cross(tang).normalized()
        for s in range(sides):
            a = s * math.tau / sides
            verts.append(pt + (right * math.cos(a) + ortho * math.sin(a)) * rad)
    for k in range(steps):
        b0 = k * sides
        b1 = (k + 1) * sides
        for s in range(sides):
            sn = (s + 1) % sides
            faces.append((b0 + s, b0 + sn, b1 + sn, b1 + s))
    faces.append(tuple(range(last := steps * sides, last + sides)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new(name="UVMap")
    for poly in mesh.polygons:
        poly.use_smooth = True
        for loop_idx in poly.loop_indices:
            v = mesh.vertices[mesh.loops[loop_idx].vertex_index].co
            uv.data[loop_idx].uv = (v.x + 0.5, v.z)
    return obj

def create_leaf_blade_geometry(length=0.22, width=0.11, arch=0.045, cup=0.016):
    # Double-sided thickness arched botanical Ficus / Fiddle-Leaf blade (smooth shaded)
    # Profile along local Y (from base 0 to tip length), width along local X, thickness along local Z
    stations = [
        (0.00, 0.008,  0.000, 0.004),  # petiole stem base
        (0.14, 0.010,  0.006, 0.004),  # petiole end
        (0.28, 0.55,   0.016, 0.003),  # lower leaf swell
        (0.52, 1.00,   0.022, 0.0028), # max leaf blade width
        (0.76, 0.82,   0.012, 0.0022), # upper taper
        (0.92, 0.36,  -0.008, 0.0016), # acuminate tip transition
        (1.00, 0.02,  -0.024, 0.0010), # drip tip
    ]
    verts = []
    faces = []
    # 5 vertices across each station (left edge, left mid, center midrib, right mid, right edge) x top & bottom
    x_fracs = [-1.0, -0.5, 0.0, 0.5, 1.0]
    n_x = len(x_fracs)
    for u, w_f, z_arch, half_t in stations:
        y = u * length
        w = w_f * (width * 0.5)
        z_base = z_arch * (arch / 0.022)
        # Top surface (5 verts) + Bottom surface (5 verts)
        for sign_z in (1.0, -1.0):
            for xf in x_fracs:
                x = xf * w
                # Midrib ridge at xf == 0, slight upward cup toward edges
                lat_cup = cup * (xf * xf) * w_f
                midrib = (0.0025 if xf == 0.0 else 0.0) * sign_z
                edge_taper = 0.35 if abs(xf) > 0.9 else 1.0
                z = z_base + lat_cup + sign_z * half_t * edge_taper + midrib
                verts.append(Vector((x, y, z)))
    stride = n_x * 2
    for k in range(len(stations) - 1):
        b0 = k * stride
        b1 = (k + 1) * stride
        # Top surface quads
        for i in range(n_x - 1):
            faces.append((b0 + i, b0 + i + 1, b1 + i + 1, b1 + i))
        # Bottom surface quads (reversed winding)
        for i in range(n_x - 1):
            faces.append((b0 + n_x + i + 1, b0 + n_x + i, b1 + n_x + i, b1 + n_x + i + 1))
        # Left & right edge stitch
        faces.append((b0 + n_x, b0, b1, b1 + n_x))
        faces.append((b0 + n_x - 1, b0 + stride - 1, b1 + stride - 1, b1 + n_x - 1))
    return verts, faces

def build_cc0_ficus():
    clear_scene()
    m_pot_slate = make_mat("PlanterArchitecturalCeramic", (0.19, 0.22, 0.27, 1.0), metallic=0.15, roughness=0.34)
    m_pot_brass = make_mat("PlanterBrassCollar", (0.76, 0.56, 0.24, 1.0), metallic=0.88, roughness=0.22)
    m_pot_terra = make_mat("PlanterWarmFluted", (0.64, 0.42, 0.28, 1.0), metallic=0.05, roughness=0.46)
    m_soil      = make_mat("PottingSoilDark", (0.14, 0.10, 0.07, 1.0), roughness=0.88)
    m_pebble    = make_mat("RiverPebble", (0.48, 0.46, 0.43, 1.0), roughness=0.52)
    m_bark      = make_cc0_wood_bark_mat()
    m_leaf_deep = make_mat("FicusLeafDeep", (0.07, 0.29, 0.14, 1.0), roughness=0.28)
    m_leaf_mid  = make_mat("FicusLeafRich", (0.11, 0.39, 0.18, 1.0), roughness=0.26)
    m_leaf_lush = make_mat("FicusLeafFresh", (0.17, 0.49, 0.22, 1.0), roughness=0.24)
    m_leaf_young= make_mat("FicusLeafTip", (0.24, 0.58, 0.25, 1.0), roughness=0.22)

    # 1. Architectural 64-sided tapered planter pot with brass inlay collar & saucer
    add_tapered_tube("PlanterSaucer", [
        ((0, 0.00, 0), 0.29),
        ((0, 0.04, 0), 0.305),
        ((0, 0.05, 0), 0.285),
    ], m_pot_slate, sides=64)

    add_tapered_tube("PlanterBody", [
        ((0, 0.04, 0), 0.265),
        ((0, 0.18, 0), 0.305),
        ((0, 0.38, 0), 0.355),
        ((0, 0.52, 0), 0.375),
        ((0, 0.56, 0), 0.380),
        ((0, 0.565, 0), 0.345),
        ((0, 0.525, 0), 0.340),
    ], m_pot_terra, sides=64)

    add_tapered_tube("PlanterBrassBand", [
        ((0, 0.47, 0), 0.371),
        ((0, 0.505, 0), 0.378),
    ], m_pot_brass, sides=64)

    add_tapered_tube("SoilBed", [
        ((0, 0.50, 0), 0.335),
        ((0, 0.532, 0), 0.338),
    ], m_soil, sides=40)

    # Dark slate river pebbles on top of the soil
    m_pebble_dark = make_mat("DarkRiverPebble", (0.24, 0.25, 0.27, 1.0), roughness=0.48)
    for i in range(14):
        ang = i * 2.39996
        rad = 0.10 + (i / 14.0) * 0.19
        px = rad * math.cos(ang)
        pz = rad * math.sin(ang)
        bpy.ops.mesh.primitive_uv_sphere_add(segments=12, ring_count=8, radius=0.022, location=rl_pos((px, 0.535, pz)))
        peb = bpy.context.active_object
        peb.scale = (1.15, 0.85, 0.38)
        peb.rotation_euler = (0, 0, ang)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
        peb.data.materials.append(m_pebble_dark if i % 2 == 0 else m_pebble)
        for poly in peb.data.polygons:
            poly.use_smooth = True

    # 2. Multi-stem braided organic hardwood Ficus trunk + secondary branches
    stems = [
        # (base, mid, top, r_base, r_top)
        ((-0.032, 0.51, -0.018), ( 0.038, 0.98,  0.028), ( 0.00, 1.54,  0.00), 0.044, 0.018),
        (( 0.035, 0.51, -0.014), (-0.042, 0.94,  0.022), (-0.15, 1.42,  0.12), 0.038, 0.015),
        (( 0.000, 0.51,  0.036), ( 0.022, 0.92, -0.042), ( 0.16, 1.46, -0.13), 0.036, 0.015),
    ]
    for s_idx, (p0, pc, p1, r0, r1) in enumerate(stems):
        add_branch_curve(f"BraidedTrunk_{s_idx}", p0, pc, p1, r0, r1, m_bark, steps=14, sides=12)

    branch_specs = [
        # (start, ctrl, tip, r0, r1)
        (( 0.01, 1.05,  0.01), (-0.16, 1.18,  0.12), (-0.30, 1.28,  0.22), 0.020, 0.007),
        (( 0.02, 1.10, -0.01), ( 0.18, 1.22, -0.10), ( 0.32, 1.32, -0.20), 0.020, 0.007),
        ((-0.02, 1.14, -0.02), (-0.14, 1.26, -0.16), (-0.27, 1.36, -0.28), 0.019, 0.007),
        (( 0.01, 1.18,  0.02), ( 0.16, 1.30,  0.14), ( 0.28, 1.40,  0.24), 0.019, 0.007),
        ((-0.12, 1.34,  0.09), (-0.24, 1.46,  0.03), (-0.35, 1.54, -0.03), 0.016, 0.006),
        (( 0.13, 1.36, -0.10), ( 0.24, 1.48,  0.03), ( 0.34, 1.58,  0.10), 0.016, 0.006),
        ((-0.09, 1.40, -0.09), (-0.14, 1.56, -0.20), (-0.20, 1.68, -0.28), 0.015, 0.006),
        (( 0.07, 1.42,  0.09), ( 0.14, 1.58,  0.19), ( 0.21, 1.70,  0.24), 0.015, 0.006),
        (( 0.00, 1.48,  0.00), (-0.09, 1.66,  0.09), (-0.14, 1.80,  0.14), 0.014, 0.005),
        (( 0.00, 1.50,  0.00), ( 0.09, 1.68, -0.09), ( 0.13, 1.82, -0.13), 0.014, 0.005),
        (( 0.00, 1.54,  0.00), ( 0.01, 1.74,  0.01), ( 0.00, 1.90,  0.00), 0.014, 0.005),
    ]
    for b_idx, (p0, pc, p1, r0, r1) in enumerate(branch_specs):
        add_branch_curve(f"FicusBranch_{b_idx}", p0, pc, p1, r0, r1, m_bark, steps=10, sides=10)

    # 3. Sculpted botanical leaf whorls (195 broad curved individual leaves with midribs, ZERO spheres!)
    base_v, base_f = create_leaf_blade_geometry(length=0.28, width=0.168, arch=0.052, cup=0.020)
    leaf_mats = [m_leaf_deep, m_leaf_mid, m_leaf_lush, m_leaf_young]

    # Collect leaf vertices/faces per material for ultra-clean batching
    batched_verts = {i: [] for i in range(len(leaf_mats))}
    batched_faces = {i: [] for i in range(len(leaf_mats))}

    def add_leaf_instance(origin_rl, yaw_rad, pitch_rad, roll_rad, scale_f, mat_idx):
        pos_bl = rl_pos(origin_rl)
        rot_mat = Euler((pitch_rad, roll_rad, yaw_rad), 'XYZ').to_matrix().to_4x4()
        v_list = batched_verts[mat_idx]
        f_list = batched_faces[mat_idx]
        v_offset = len(v_list)
        for v in base_v:
            v_list.append(pos_bl + (rot_mat @ (v * scale_f)))
        for f in base_f:
            f_list.append(tuple(v_offset + idx for idx in f))

    # Populate dense multi-tiered botanical foliage clusters along all branches & inner crown
    golden_angle = 2.39996323
    # A) Outer & Mid spiral canopy from Y = 1.04m to Y = 1.98m
    n_spiral = 128
    for i in range(n_spiral):
        u = i / (n_spiral - 1)
        y = 1.04 + u * 0.92
        envelope = math.sin(min(1.0, u * 1.12 + 0.14) * math.pi)
        # Alternate between inner core leaves and outer canopy leaves for rich volume
        inner_factor = 0.52 if (i % 3 == 0) else 1.0
        r_canopy = (0.06 + 0.31 * envelope + 0.035 * math.sin(i * 1.7)) * inner_factor
        theta = i * golden_angle
        ox = r_canopy * math.cos(theta)
        oz = r_canopy * math.sin(theta)
        az_bl = math.atan2(-oz, ox) - math.pi * 0.5
        pitch = math.radians(-16.0 + u * 50.0 + 9.0 * math.sin(i * 2.3))
        roll = math.radians(11.0 * math.cos(i * 1.9))
        scl = (1.16 - 0.32 * u) * (0.92 + 0.18 * ((i * 7) % 5) / 4.0)
        mat_i = 0 if (u < 0.32 or inner_factor < 0.7) else (1 if u < 0.64 else (2 if u < 0.86 else 3))
        add_leaf_instance((ox, y, oz), az_bl, pitch, roll, scl, mat_i)

    # B) Terminal branch rosette whorls at each branch midpoint and tip
    for b_idx, (p0, pc, p1, _, _) in enumerate(branch_specs):
        for w in range(6):
            ang = b_idx * 1.3 + w * (math.tau / 6.0)
            az_bl = ang - math.pi * 0.5
            pitch = math.radians(8.0 + (w % 3) * 14.0)
            roll = math.radians((w - 2.5) * 6.0)
            scl = 0.94 if w < 4 else 0.78
            mat_i = 1 if w < 2 else (2 if w < 4 else 3)
            add_leaf_instance(p1 if w % 2 == 0 else pc, az_bl, pitch, roll, scl, mat_i)

    for mat_idx, lmat in enumerate(leaf_mats):
        if not batched_verts[mat_idx]:
            continue
        m_data = bpy.data.meshes.new(f"FicusFoliage_{mat_idx}")
        m_data.from_pydata(batched_verts[mat_idx], [], batched_faces[mat_idx])
        m_data.update()
        l_obj = bpy.data.objects.new(f"FicusFoliage_{mat_idx}", m_data)
        bpy.context.collection.objects.link(l_obj)
        m_data.materials.append(lmat)
        for poly in m_data.polygons:
            poly.use_smooth = True

    # Join & clean mesh for export
    mesh_objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    bpy.ops.object.select_all(action='DESELECT')
    for o in mesh_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = mesh_objs[0]
    bpy.ops.object.join()
    obj = bpy.context.active_object
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    for poly in obj.data.polygons:
        poly.use_smooth = True
    obj.data.update()

    os.makedirs(OFFICE_CC0, exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=str(OUT_GLB),
        export_format='GLB',
        use_selection=True,
        export_apply=True,
        export_normals=True,
        export_materials='EXPORT'
    )
    print("Exported CC0 Ficus:", OUT_GLB, os.path.getsize(OUT_GLB), "Polys:", len(obj.data.polygons))

if __name__ == "__main__":
    build_cc0_ficus()
