"""Build sleek, minimalist, high-craft architectural elevator button panels in Blender 5.1.

Exports 3 clean PBR + BVHTree-AO GLBs (zero cluttered bitmap text, crisp chamfered geometry):
  1. assets/models/prop_elevator_panel_broken.glb  (compact 18x48cm hall station, red lock LED, scorched open lower slot + 3 bent copper wires)
  2. assets/models/prop_elevator_panel_fixed.glb   (compact 18x48cm hall station, teal active halo, sealed lower cover, green ready LED)
  3. assets/models/prop_elevator_cabin_panel.glb   (compact 20x54cm interior COP, smoked indicator + 6 sculpted round buttons + red alarm)

Coordinate convention (glTF Y-up, exported from Blender Z-up with export_yup=True):
  - Blender X = right, Blender Y = depth into wall (+Y back, -Y outward toward player -> glTF +Z outward!)
  Wait: Blender +Y maps to glTF -Z; Blender -Y maps to glTF +Z!
  So building the panel front facing Blender -Y makes the exported glTF front face +Z (matching yaw=0 for South side=+1, yaw=PI for North side=-1).
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

MODELS_DIR = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"


def srgb_to_lin(c):
    return tuple(pow(max(0.0, min(1.0, v)), 2.2) for v in c)


def make_mat(name, srgb, roughness=0.35, metallic=0.0, emission=None, emission_strength=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    m.use_backface_culling = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    lin = srgb_to_lin(srgb)

    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "COLOR_0"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs["A"].default_value = (*lin, 1.0)
    nt.links.new(vc.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])

    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None and emission_strength > 0:
        elin = srgb_to_lin(emission)
        bsdf.inputs["Emission Color"].default_value = (*elin, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def add_bevel_box(bm, center, size, bevel=0.004, segs=2, mat_idx=0, rot=None):
    ret = bmesh.ops.create_cube(bm, size=1.0)
    verts = ret["verts"]
    mat = Matrix.Diagonal((size[0], size[1], size[2], 1.0))
    bmesh.ops.transform(bm, matrix=mat, verts=verts)
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bev = bmesh.ops.bevel(
            bm, geom=edges, offset=bevel, offset_type='OFFSET',
            segments=segs, profile=0.5, affect='EDGES', clamp_overlap=True
        )
        verts = [v for v in ({v for f in bev["faces"] for v in f.verts} | set(verts)) if v.is_valid]
    if rot is not None:
        bmesh.ops.transform(bm, matrix=rot.to_matrix().to_4x4(), verts=verts)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center), verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = False
    return verts


def add_cyl_y(bm, center, r1, r2, depth, segs=28, mat_idx=0, smooth=True):
    """Cylinder aligned along Blender Y axis (protruding toward -Y)."""
    ret = bmesh.ops.create_cone(
        bm, cap_ends=True, cap_tris=False, segments=segs,
        radius1=r1, radius2=r2, depth=depth
    )
    verts = ret["verts"]
    rot = Euler((math.radians(90), 0, 0)).to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ rot, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def add_ring_y(bm, center, r_outer, r_inner, depth, segs=32, mat_idx=0):
    """Clean machined hollow washer/bezel ring aligned along Blender Y axis."""
    y_back = center[1] + depth * 0.5
    y_front = center[1] - depth * 0.5
    cx, cz = center[0], center[2]
    ring_bo, ring_fo, ring_bi, ring_fi = [], [], [], []
    for i in range(segs):
        a = 2.0 * math.pi * i / segs
        ca, sa = math.cos(a), math.sin(a)
        ring_bo.append(bm.verts.new((cx + r_outer * ca, y_back, cz + r_outer * sa)))
        ring_fo.append(bm.verts.new((cx + r_outer * ca, y_front, cz + r_outer * sa)))
        ring_bi.append(bm.verts.new((cx + r_inner * ca, y_back, cz + r_inner * sa)))
        ring_fi.append(bm.verts.new((cx + r_inner * ca, y_front, cz + r_inner * sa)))
    for i in range(segs):
        j = (i + 1) % segs
        faces = [
            bm.faces.new((ring_bo[i], ring_bo[j], ring_fo[j], ring_fo[i])),  # outer wall
            bm.faces.new((ring_fi[i], ring_fi[j], ring_bi[j], ring_bi[i])),  # inner wall
            bm.faces.new((ring_fo[i], ring_fo[j], ring_fi[j], ring_fi[i])),  # front face (-Y)
            bm.faces.new((ring_bi[i], ring_bi[j], ring_bo[j], ring_bo[i])),  # back face (+Y)
        ]
        for idx, f in enumerate(faces):
            f.material_index = mat_idx
            f.smooth = idx < 2


def add_arrow_3d(bm, center, width, height, depth, pointing_up=True, mat_idx=0):
    """Crisp sculpted 3D triangular chevron arrow extruded along Y."""
    cx, cy, cz = center
    s = 1.0 if pointing_up else -1.0
    # Outer triangle & inner hollow chevron for luxury architectural look
    pts = [
        (cx, cz + s * height * 0.52),
        (cx + width * 0.52, cz - s * height * 0.44),
        (cx + width * 0.24, cz - s * height * 0.44),
        (cx, cz + s * height * 0.04),
        (cx - width * 0.24, cz - s * height * 0.44),
        (cx - width * 0.52, cz - s * height * 0.44),
    ]
    y0, y1 = cy + depth * 0.5, cy - depth * 0.5
    front = [bm.verts.new((x, y1, z)) for x, z in pts]
    back = [bm.verts.new((x, y0, z)) for x, z in pts]
    # Split chevron cleanly into two convex quads so no concave n-gon issues!
    # Right wing: 0,1,2,3; Left wing: 0,3,4,5
    quads = [(0, 1, 2, 3), (0, 3, 4, 5)]
    for q in quads:
        ff = bm.faces.new([front[k] for k in q] if pointing_up else [front[k] for k in reversed(q)])
        bf = bm.faces.new([back[k] for k in reversed(q)] if pointing_up else [back[k] for k in q])
        ff.material_index = bf.material_index = mat_idx
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        sf = bm.faces.new((front[i], back[i], back[j], front[j]) if pointing_up else (front[j], back[j], back[i], front[i]))
        sf.material_index = mat_idx


def add_tube_curve(bm, points, radius, segs=8, mat_idx=0):
    """Smooth bent 3D wire along control points."""
    rings = []
    for idx, p in enumerate(points):
        p = Vector(p)
        if idx == 0:
            t = (Vector(points[1]) - p).normalized()
        elif idx == len(points) - 1:
            t = (p - Vector(points[idx - 1])).normalized()
        else:
            t = (Vector(points[idx + 1]) - Vector(points[idx - 1])).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        u = t.cross(up).normalized()
        v = t.cross(u).normalized()
        ring = []
        for s in range(segs):
            a = 2.0 * math.pi * s / segs
            ring.append(bm.verts.new(p + (u * math.cos(a) + v * math.sin(a)) * radius))
        rings.append(ring)
    for r_i in range(len(rings) - 1):
        r0, r1 = rings[r_i], rings[r_i + 1]
        for s in range(segs):
            sn = (s + 1) % segs
            f = bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            f.material_index = mat_idx
            f.smooth = True
    # End caps
    c0 = bm.faces.new(list(reversed(rings[0])))
    c1 = bm.faces.new(rings[-1])
    c0.material_index = c1.material_index = mat_idx


def add_recessed_window(bm, center, w, h, frame_t, depth, frame_mat, glass_mat):
    """Architectural recessed framed opening with smoked glass backing."""
    cx, cy, cz = center
    # Smoked glass recessed plate
    add_bevel_box(bm, (cx, cy + 0.002, cz), (w - frame_t * 1.4, depth * 0.4, h - frame_t * 1.4), bevel=0.001, segs=1, mat_idx=glass_mat)
    # 4 crisp chamfered frame rails protruding cleanly around the glass
    add_bevel_box(bm, (cx, cy - depth * 0.35, cz + h * 0.5 - frame_t * 0.5), (w, depth, frame_t), bevel=0.0015, segs=1, mat_idx=frame_mat)
    add_bevel_box(bm, (cx, cy - depth * 0.35, cz - h * 0.5 + frame_t * 0.5), (w, depth, frame_t), bevel=0.0015, segs=1, mat_idx=frame_mat)
    add_bevel_box(bm, (cx - w * 0.5 + frame_t * 0.5, cy - depth * 0.35, cz), (frame_t, depth, h - frame_t * 1.6), bevel=0.0015, segs=1, mat_idx=frame_mat)
    add_bevel_box(bm, (cx + w * 0.5 - frame_t * 0.5, cy - depth * 0.35, cz), (frame_t, depth, h - frame_t * 1.6), bevel=0.0015, segs=1, mat_idx=frame_mat)


def add_screw(bm, x, y, z, mat_head, mat_slot):
    add_cyl_y(bm, (x, y - 0.0015, z), 0.0052, 0.0046, 0.003, segs=12, mat_idx=mat_head)
    add_bevel_box(bm, (x, y - 0.0032, z), (0.0056, 0.0008, 0.0014), bevel=0.0, mat_idx=mat_slot)


def bake_ao_and_export(bm, mats, out_path):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("ElevatorPanelMesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("ElevatorPanel", mesh)
    bpy.context.collection.objects.link(obj)
    for m in mats:
        mesh.materials.append(m)

    # Bake clean 16-ray BVHTree ambient occlusion into COLOR_0 for crisp crevice depth
    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bm_ao.verts.ensure_lookup_table()
    bm_ao.faces.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm_ao)

    # Hemisphere Fibonacci directions
    dirs = []
    n_rays = 16
    phi = math.pi * (3.0 - math.sqrt(5.0))
    for i in range(n_rays):
        y = 1.0 - (i / float(n_rays - 1)) * 0.85
        r = math.sqrt(max(0.0, 1.0 - y * y))
        th = phi * i
        dirs.append(Vector((math.cos(th) * r, y, math.sin(th) * r)).normalized())

    vert_ao = []
    for v in bm_ao.verts:
        n = v.normal.normalized() if v.normal.length > 1e-6 else Vector((0, -1, 0))
        up = Vector((0, 0, 1)) if abs(n.z) < 0.9 else Vector((1, 0, 0))
        tx = n.cross(up).normalized()
        ty = n.cross(tx).normalized()
        occ = 0.0
        origin = v.co + n * 0.0015
        for d in dirs:
            ray = (tx * d.x + n * d.y + ty * d.z).normalized()
            hit = bvh.ray_cast(origin, ray, 0.065)
            if hit[0] is not None:
                occ += 1.0 - (hit[3] / 0.065)
        ao = max(0.54, min(1.0, 1.0 - (occ / n_rays) * 0.55))
        vert_ao.append(ao)
    bm_ao.free()

    col_attr = mesh.color_attributes.new(name="COLOR_0", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            vi = mesh.loops[li].vertex_index
            ao = vert_ao[vi]
            col_attr.data[li].color = (ao, ao, ao, 1.0)

    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.gltf(
        filepath=out_path,
        export_format='GLB',
        use_selection=True,
        export_yup=True,
        export_apply=True,
        export_attributes=True,
    )
    print(f"EXPORTED {out_path} ({len(mesh.polygons)} polys)")


def build_hall_panel(broken: bool, out_path: str):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()

    # Palette: sleek architectural brushed dark titanium + warm champagne brass + smoked obsidian glass
    m_titanium = 0
    m_brass = 1
    m_steel_btn = 2
    m_glass = 3
    m_dark_recess = 4
    m_led_primary = 5
    m_led_card = 6
    m_copper = 7
    m_wire_red = 8
    m_wire_blue = 9
    m_charred = 10

    led_col = (0.95, 0.22, 0.16) if broken else (0.34, 0.90, 0.82)
    card_col = (0.92, 0.18, 0.14) if broken else (0.28, 0.92, 0.58)

    mats = [
        make_mat("mat_titanium", (0.34, 0.37, 0.41), roughness=0.30, metallic=0.78),
        make_mat("mat_champagne_brass", (0.82, 0.70, 0.48), roughness=0.24, metallic=0.88),
        make_mat("mat_satin_steel", (0.86, 0.88, 0.91), roughness=0.18, metallic=0.86),
        make_mat("mat_smoked_glass", (0.04, 0.05, 0.07), roughness=0.08, metallic=0.05),
        make_mat("mat_dark_recess", (0.07, 0.08, 0.10), roughness=0.60, metallic=0.20),
        make_mat("mat_led_primary", led_col, roughness=0.18, metallic=0.0, emission=led_col, emission_strength=2.4),
        make_mat("mat_led_card", card_col, roughness=0.18, metallic=0.0, emission=card_col, emission_strength=2.2),
        make_mat("mat_copper", (0.82, 0.48, 0.24), roughness=0.28, metallic=0.92),
        make_mat("mat_wire_red", (0.72, 0.14, 0.12), roughness=0.45, metallic=0.0),
        make_mat("mat_wire_blue", (0.14, 0.34, 0.68), roughness=0.45, metallic=0.0),
        make_mat("mat_charred", (0.05, 0.045, 0.045), roughness=0.90, metallic=0.05),
    ]

    # Compact, human-proportioned luxury hall call monolith: 19cm wide x 48cm tall x 2.6cm deep.
    W, H, D = 0.19, 0.48, 0.026
    # Shadow-gap wall mounting backplate
    add_bevel_box(bm, (0.0, 0.008, 0.0), (W + 0.012, 0.014, H + 0.012), bevel=0.002, segs=1, mat_idx=m_dark_recess)
    # Main beveled dark-titanium monolith face (front sits at y = -0.015)
    add_bevel_box(bm, (0.0, -0.002, 0.0), (W, D, H), bevel=0.0055, segs=3, mat_idx=m_titanium)
    # Thin champagne-brass architectural reveal frame around outer face perimeter
    add_recessed_window(bm, (0.0, -0.014, 0.0), W - 0.014, H - 0.014, 0.0045, 0.004, m_brass, m_titanium)

    # 4 tiny countersunk stainless screws in corners
    for sx in (-0.074, 0.074):
        for sz in (-0.218, 0.218):
            add_screw(bm, sx, -0.015, sz, m_steel_btn, m_dark_recess)

    # 1. TOP: Clean recessed smoked-glass status indicator window (z = +0.150)
    add_recessed_window(bm, (0.0, -0.015, 0.152), 0.128, 0.078, 0.006, 0.008, m_brass, m_glass)
    if broken:
        # Clean minimalist fault symbol: glowing red diamond/bar + amber micro dot (no text clutter)
        add_bevel_box(bm, (0.0, -0.0165, 0.152), (0.068, 0.003, 0.010), bevel=0.001, segs=1, mat_idx=m_led_primary)
        add_cyl_y(bm, (0.044, -0.0168, 0.172), 0.0045, 0.0045, 0.003, segs=14, mat_idx=m_led_primary)
    else:
        # Clean illuminated upward chevron inside the smoked glass
        add_arrow_3d(bm, (0.0, -0.0165, 0.153), 0.036, 0.034, 0.003, pointing_up=True, mat_idx=m_led_primary)

    # 2. CENTER: Two tactile recessed brushed-steel elevator call buttons (UP z=+0.056, DOWN z=-0.022)
    for bz, up in ((0.054, True), (-0.026, False)):
        # Machined dark recess well
        add_cyl_y(bm, (0.0, -0.015, bz), 0.031, 0.031, 0.003, segs=32, mat_idx=m_dark_recess)
        # Champagne-brass outer collar ring
        add_ring_y(bm, (0.0, -0.0185, bz), 0.031, 0.0245, 0.007, segs=32, mat_idx=m_brass)
        # Thin glowing LED halo ring nestled cleanly inside the collar
        add_ring_y(bm, (0.0, -0.0205, bz), 0.0246, 0.0208, 0.006, segs=32, mat_idx=m_led_primary)
        # Tactile brushed stainless push button cap
        add_cyl_y(bm, (0.0, -0.0225, bz), 0.0208, 0.0195, 0.009, segs=32, mat_idx=m_steel_btn)
        # Sculpted 3D illuminated chevron embossed onto the steel push cap
        add_arrow_3d(bm, (0.0, -0.0276, bz), 0.018, 0.016, 0.0022, pointing_up=up, mat_idx=m_led_primary)

    # 3. LOWER: Minimalist obsidian RFID keycard pad (z = -0.140)
    add_recessed_window(bm, (0.0, -0.015, -0.132), 0.128, 0.096, 0.006, 0.009, m_brass, m_glass)
    # Sleek glowing horizontal LED status strip along top of the card reader
    add_bevel_box(bm, (0.0, -0.0205, -0.095), (0.088, 0.004, 0.0065), bevel=0.001, segs=1, mat_idx=m_led_card)
    # Minimal subtle brass contactless symbol ring on the glass pad
    add_ring_y(bm, (0.0, -0.0168, -0.138), 0.018, 0.015, 0.002, segs=24, mat_idx=m_brass)
    add_ring_y(bm, (0.0, -0.0168, -0.138), 0.009, 0.0065, 0.002, segs=20, mat_idx=m_brass)

    if broken:
        # Compact charred maintenance knock-out at very bottom (z = -0.205) with 3 neat bent copper wires
        add_bevel_box(bm, (0.0, -0.0165, -0.200), (0.096, 0.006, 0.036), bevel=0.002, segs=1, mat_idx=m_charred)
        # Tilted blown-loose tiny bottom trim plate
        add_bevel_box(
            bm, (0.010, -0.029, -0.228), (0.092, 0.004, 0.032), bevel=0.001, segs=1,
            mat_idx=m_titanium, rot=Euler((math.radians(52), 0, math.radians(-8)))
        )
        # 3 clean bent 3D wires emerging from the charred slot
        add_tube_curve(bm, [(-0.024, -0.014, -0.198), (-0.016, -0.028, -0.194), (-0.006, -0.034, -0.203)], 0.0024, 8, m_wire_red)
        add_tube_curve(bm, [(-0.006, -0.034, -0.203), (-0.002, -0.036, -0.207)], 0.0015, 6, m_copper)
        add_tube_curve(bm, [(0.024, -0.014, -0.202), (0.014, -0.029, -0.208), (0.005, -0.033, -0.203)], 0.0024, 8, m_wire_blue)
        add_tube_curve(bm, [(0.005, -0.033, -0.203), (0.002, -0.035, -0.201)], 0.0015, 6, m_copper)
        add_tube_curve(bm, [(-0.004, -0.014, -0.210), (0.006, -0.025, -0.215), (0.018, -0.027, -0.211)], 0.0020, 6, m_copper)
    else:
        # Flush closed stainless service cover + subtle green power dot
        add_bevel_box(bm, (0.0, -0.0175, -0.202), (0.096, 0.005, 0.032), bevel=0.0015, segs=1, mat_idx=m_steel_btn)
        add_cyl_y(bm, (0.0, -0.0202, -0.202), 0.0042, 0.0042, 0.002, segs=14, mat_idx=m_led_card)

    bake_ao_and_export(bm, mats, out_path)


def build_cabin_panel(out_path: str):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()

    m_titanium = 0
    m_brass = 1
    m_steel_btn = 2
    m_glass = 3
    m_dark_recess = 4
    m_led_teal = 5
    m_led_gold = 6
    m_led_red = 7

    mats = [
        make_mat("mat_cabin_steel", (0.36, 0.39, 0.43), roughness=0.28, metallic=0.80),
        make_mat("mat_cabin_brass", (0.82, 0.70, 0.48), roughness=0.24, metallic=0.88),
        make_mat("mat_cabin_btn", (0.88, 0.90, 0.93), roughness=0.18, metallic=0.86),
        make_mat("mat_cabin_glass", (0.04, 0.05, 0.07), roughness=0.08, metallic=0.05),
        make_mat("mat_cabin_dark", (0.07, 0.08, 0.10), roughness=0.60, metallic=0.20),
        make_mat("mat_cabin_teal", (0.34, 0.90, 0.82), roughness=0.18, metallic=0.0, emission=(0.34, 0.90, 0.82), emission_strength=2.4),
        make_mat("mat_cabin_gold", (0.94, 0.74, 0.36), roughness=0.18, metallic=0.0, emission=(0.94, 0.74, 0.36), emission_strength=1.9),
        make_mat("mat_cabin_red", (0.94, 0.22, 0.18), roughness=0.20, metallic=0.0, emission=(0.94, 0.22, 0.18), emission_strength=2.2),
    ]

    W, H, D = 0.21, 0.56, 0.026
    add_bevel_box(bm, (0.0, 0.008, 0.0), (W + 0.012, 0.014, H + 0.012), bevel=0.002, segs=1, mat_idx=m_dark_recess)
    add_bevel_box(bm, (0.0, -0.002, 0.0), (W, D, H), bevel=0.0055, segs=3, mat_idx=m_titanium)
    add_recessed_window(bm, (0.0, -0.014, 0.0), W - 0.014, H - 0.014, 0.0045, 0.004, m_brass, m_titanium)

    for sx in (-0.084, 0.084):
        for sz in (-0.258, 0.258):
            add_screw(bm, sx, -0.015, sz, m_steel_btn, m_dark_recess)

    # Top smoked-glass direction indicator
    add_recessed_window(bm, (0.0, -0.015, 0.190), 0.144, 0.080, 0.006, 0.008, m_brass, m_glass)
    add_arrow_3d(bm, (0.0, -0.0165, 0.192), 0.036, 0.034, 0.003, pointing_up=True, mat_idx=m_led_teal)

    # Minimal horizontal milled speaker slits (4 clean thin slots)
    for k in range(4):
        add_bevel_box(bm, (0.0, -0.0155, 0.125 - k * 0.008), (0.116, 0.003, 0.0032), bevel=0.0, mat_idx=m_dark_recess)

    # Clean 2x3 matrix of sculpted brushed-steel round floor buttons (z = +0.056, -0.016, -0.088)
    for r, bz in enumerate((0.054, -0.018, -0.090)):
        for c, bx in enumerate((-0.044, 0.044)):
            ring_mat = m_led_teal if (r == 0 and c == 0) else m_led_gold
            add_cyl_y(bm, (bx, -0.015, bz), 0.024, 0.024, 0.003, segs=28, mat_idx=m_dark_recess)
            add_ring_y(bm, (bx, -0.0182, bz), 0.024, 0.019, 0.006, segs=28, mat_idx=m_brass)
            add_ring_y(bm, (bx, -0.0200, bz), 0.0192, 0.0162, 0.005, segs=28, mat_idx=ring_mat)
            add_cyl_y(bm, (bx, -0.0218, bz), 0.0162, 0.0150, 0.008, segs=28, mat_idx=m_steel_btn)

    # Single compact crimson emergency stop button at bottom center
    add_cyl_y(bm, (0.0, -0.015, -0.188), 0.026, 0.026, 0.004, segs=28, mat_idx=m_dark_recess)
    add_ring_y(bm, (0.0, -0.019, -0.188), 0.026, 0.020, 0.008, segs=28, mat_idx=m_brass)
    add_cyl_y(bm, (0.0, -0.022, -0.188), 0.0195, 0.0175, 0.011, segs=28, mat_idx=m_led_red)

    bake_ao_and_export(bm, mats, out_path)


def main():
    build_hall_panel(True, os.path.join(MODELS_DIR, "prop_elevator_panel_broken.glb"))
    build_hall_panel(False, os.path.join(MODELS_DIR, "prop_elevator_panel_fixed.glb"))
    build_cabin_panel(os.path.join(MODELS_DIR, "prop_elevator_cabin_panel.glb"))
    print("ALL 3 SCULPTED ELEVATOR PANELS BUILT!")


if __name__ == "__main__":
    main()
