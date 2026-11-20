"""High-craft stylized 2026 Vibe Coder articulated 3D character builder (Blender 5.1).

Exports clean rigid-joint articulated body-part GLBs (ZERO vertex-shader stretching / bat-wings!)
plus full assembled standing (`char_vibe_coder.glb`) & seated (`char_vibe_coder_seated.glb`) hero models:
  - coder_torso.glb    (pivot: pelvis 0,0,0 -> world Y=0.90)  mats: [blue, navy, skin, ivory, coal, denim]
  - coder_head.glb     (pivot: neck   0,0,0 -> torso +0.54)   mats: [skin, hair, ivory, iris, teal, coal]
  - coder_arm_l.glb    (pivot: L shld 0,0,0 -> torso +0.205,+0.455) mats: [blue, navy, skin]
  - coder_arm_r.glb    (pivot: R shld 0,0,0 -> torso -0.205,+0.455) mats: [blue, navy, skin]
  - coder_thigh_l.glb  (pivot: L hip  0,0,0 -> world +0.105,+0.90)  mats: [denim]
  - coder_thigh_r.glb  (pivot: R hip  0,0,0 -> world -0.105,+0.90)  mats: [denim]
  - coder_shin_l.glb   (pivot: L knee 0,0,0 -> thigh -0.42)         mats: [denim, navy, ivory, sole, coal]
  - coder_shin_r.glb   (pivot: R knee 0,0,0 -> thigh -0.42)         mats: [denim, navy, ivory, sole, coal]
  - char_vibe_coder.glb        (full assembled standing, legacy 10-slot order: skin..coal)
  - char_vibe_coder_seated.glb (full assembled seated typing pose aligned to WorkstationScale=0.92 desk/chair)

Coordinate convention: Blender Z-up, front faces Blender -Y -> exports with export_yup=True to glTF +Z forward, +Y up.
Every GLB bakes 16-ray BVHTree Ambient Occlusion into COLOR_0 wired to Base Color Multiply.
"""
import math
import os
import bpy
import bmesh
from mathutils import Vector, Matrix, Euler
from mathutils.bvhtree import BVHTree

MODELS_DIR = r"C:\Users\se-20\.gemini\antigravity\scratch\HowToVibeCoder\assets\models"

# Palette in sRGB (blue & navy are neutral bright so dynamic C# HoodieColor tinting stays vivid & clean!)
PALETTE = {
    "skin":  ((0.88, 0.72, 0.62), 0.56, 0.0, None, 0.0),
    "hair":  ((0.15, 0.12, 0.10), 0.72, 0.0, None, 0.0),
    "blue":  ((0.88, 0.90, 0.94), 0.80, 0.0, None, 0.0),  # dynamically tinted by HoodieColor
    "denim": ((0.16, 0.21, 0.30), 0.84, 0.0, None, 0.0),
    "navy":  ((0.82, 0.85, 0.90), 0.78, 0.0, None, 0.0),  # dynamically tinted dark HoodieColor
    "sole":  ((0.92, 0.91, 0.89), 0.65, 0.0, None, 0.0),
    "ivory": ((0.94, 0.93, 0.90), 0.58, 0.0, None, 0.0),
    "iris":  ((0.10, 0.11, 0.14), 0.18, 0.0, None, 0.0),
    "teal":  ((0.78, 0.95, 0.96), 0.14, 0.25, (0.55, 0.92, 0.95), 0.65),  # tinted by GlassesColor
    "coal":  ((0.12, 0.13, 0.16), 0.42, 0.18, None, 0.0),
}


def srgb_to_lin(c):
    return tuple(pow(max(0.0, min(1.0, v)), 2.2) for v in c)


def make_mat(key):
    srgb, rough, met, em, em_str = PALETTE[key]
    m = bpy.data.materials.new(key)
    m.use_nodes = True
    m.use_backface_culling = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "COLOR_0"
    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = "RGBA"
    mix.blend_type = "MULTIPLY"
    mix.inputs["Factor"].default_value = 1.0
    mix.inputs["A"].default_value = (*srgb_to_lin(srgb), 1.0)
    nt.links.new(vc.outputs["Color"], mix.inputs["B"])
    nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = met
    if em is not None and em_str > 0:
        bsdf.inputs["Emission Color"].default_value = (*srgb_to_lin(em), 1.0)
        bsdf.inputs["Emission Strength"].default_value = em_str
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return m


def add_loft(bm, sections, mat_idx, segs=18, cap_bottom=True, cap_top=True, smooth=True):
    """Build smooth organic lofted volume from list of (center_xyz, rx, ry, rot_x_deg)."""
    rings = []
    for sec in sections:
        c = Vector(sec[0])
        rx, ry = sec[1], sec[2]
        pitch = math.radians(sec[3]) if len(sec) > 3 else 0.0
        rmat = Matrix.Rotation(pitch, 3, 'X')
        ring = []
        for i in range(segs):
            a = 2.0 * math.pi * i / segs
            local = Vector((rx * math.cos(a), ry * math.sin(a), 0.0))
            ring.append(bm.verts.new(c + rmat @ local))
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        for i in range(segs):
            j = (i + 1) % segs
            f = bm.faces.new((r0[i], r0[j], r1[j], r1[i]))
            f.material_index = mat_idx
            f.smooth = smooth
    if cap_bottom:
        fb = bm.faces.new(list(reversed(rings[0])))
        fb.material_index = mat_idx
        fb.smooth = smooth
    if cap_top:
        ft = bm.faces.new(rings[-1])
        ft.material_index = mat_idx
        ft.smooth = smooth


def add_sphere(bm, center, radii, mat_idx, u_seg=18, v_seg=12, rot=None, smooth=True):
    ret = bmesh.ops.create_uvsphere(bm, u_segments=u_seg, v_segments=v_seg, radius=1.0)
    verts = ret["verts"]
    mat = Matrix.Diagonal((radii[0], radii[1], radii[2], 1.0))
    if rot is not None:
        mat = rot.to_matrix().to_4x4() @ mat
    mat = Matrix.Translation(center) @ mat
    bmesh.ops.transform(bm, matrix=mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def add_box(bm, center, size, mat_idx, bevel=0.005, segs=2, rot=None, smooth=False):
    ret = bmesh.ops.create_cube(bm, size=1.0)
    verts = ret["verts"]
    bmesh.ops.transform(bm, matrix=Matrix.Diagonal((size[0], size[1], size[2], 1.0)), verts=verts)
    if bevel > 0:
        edges = list({e for v in verts for e in v.link_edges})
        bev = bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type='OFFSET',
                              segments=segs, profile=0.5, affect='EDGES', clamp_overlap=True)
        verts = [v for v in ({v for f in bev["faces"] for v in f.verts} | set(verts)) if v.is_valid]
    if rot is not None:
        bmesh.ops.transform(bm, matrix=rot.to_matrix().to_4x4(), verts=verts)
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center), verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def add_tube(bm, points, radii, mat_idx, segs=10, cap=True, smooth=True):
    """Tube along 3D polyline with per-point radius (scalar or list)."""
    if isinstance(radii, (int, float)):
        radii = [float(radii)] * len(points)
    rings = []
    for idx, p in enumerate(points):
        p = Vector(p)
        if idx == 0:
            t = (Vector(points[1]) - p).normalized()
        elif idx == len(points) - 1:
            t = (p - Vector(points[idx - 1])).normalized()
        else:
            t = (Vector(points[idx + 1]) - Vector(points[idx - 1])).normalized()
        up = Vector((0, 0, 1)) if abs(t.z) < 0.92 else Vector((1, 0, 0))
        u = t.cross(up).normalized()
        v = t.cross(u).normalized()
        r = radii[idx]
        ring = []
        for s in range(segs):
            a = 2.0 * math.pi * s / segs
            ring.append(bm.verts.new(p + (u * math.cos(a) + v * math.sin(a)) * r))
        rings.append(ring)
    for k in range(len(rings) - 1):
        r0, r1 = rings[k], rings[k + 1]
        for s in range(segs):
            sn = (s + 1) % segs
            f = bm.faces.new((r0[s], r0[sn], r1[sn], r1[s]))
            f.material_index = mat_idx
            f.smooth = smooth
    if cap:
        f0 = bm.faces.new(list(reversed(rings[0])))
        f1 = bm.faces.new(rings[-1])
        f0.material_index = f1.material_index = mat_idx


def add_cyl(bm, center, r1, r2, depth, mat_idx, segs=18, rot=None, smooth=True):
    ret = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs,
                                radius1=r1, radius2=r2, depth=depth)
    verts = ret["verts"]
    mat = Matrix.Identity(4)
    if rot is not None:
        mat = rot.to_matrix().to_4x4()
    bmesh.ops.transform(bm, matrix=Matrix.Translation(center) @ mat, verts=verts)
    for v in verts:
        for f in v.link_faces:
            f.material_index = mat_idx
            f.smooth = smooth
    return verts


def bake_ao_and_export(bm, mat_keys, out_path):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new("CoderPartMesh")
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new("CoderPart", mesh)
    bpy.context.collection.objects.link(obj)
    for k in mat_keys:
        mesh.materials.append(make_mat(k))

    # 16-ray BVHTree AO baked into COLOR_0
    bm_ao = bmesh.new()
    bm_ao.from_mesh(mesh)
    bm_ao.verts.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm_ao)

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
        orig = v.co + n * 0.0025
        for d in dirs:
            ray = (tx * d.x + n * d.y + ty * d.z).normalized()
            hit = bvh.ray_cast(orig, ray, 0.22)
            if hit[0] is not None:
                occ += 1.0 - (hit[3] / 0.22)
        ao = max(0.70, min(1.0, 1.0 - (occ / n_rays) * 0.36))
        vert_ao.append(ao)
    bm_ao.free()

    col_attr = mesh.color_attributes.new(name="COLOR_0", type='BYTE_COLOR', domain='CORNER')
    for poly in mesh.polygons:
        for li in poly.loop_indices:
            ao = vert_ao[mesh.loops[li].vertex_index]
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
    print(f"EXPORTED {os.path.basename(out_path)} ({len(mesh.polygons)} polys)")


# =============================================================================
# GEOMETRY BUILDERS (each adds into bm using a mat_map dict: key -> mat_idx)
# =============================================================================

def build_torso_geom(bm, M, xf=Matrix.Identity(4)):
    tmp = bmesh.new()
    # Pelvis seat / relaxed denim hips (snug natural hip width matching thighs at +/-0.088)
    add_loft(tmp, [
        ((0, 0.00, -0.055), 0.140, 0.100),
        ((0, 0.00,  0.005), 0.160, 0.116),
        ((0, 0.00,  0.050), 0.164, 0.120),
    ], M["denim"], segs=20)
    # Ribbed hoodie hem waistband (navy trim)
    add_loft(tmp, [
        ((0, -0.002, 0.036), 0.166, 0.122),
        ((0, -0.003, 0.064), 0.172, 0.128),
        ((0, -0.002, 0.088), 0.174, 0.130),
    ], M["navy"], segs=20)
    # Main relaxed tech hoodie torso with NATURAL sloped human shoulders (NO boxy robot shelf!)
    add_loft(tmp, [
        ((0, -0.002, 0.084), 0.174, 0.130),
        ((0, -0.006, 0.175), 0.178, 0.134),
        ((0, -0.008, 0.285), 0.180, 0.136),
        ((0, -0.004, 0.388), 0.182, 0.130),
        ((0,  0.002, 0.448), 0.158, 0.114),  # natural trapezoid shoulder slope!
        ((0,  0.005, 0.488), 0.118, 0.092),
        ((0,  0.006, 0.512), 0.092, 0.082),
    ], M["blue"], segs=22)
    # Subtle blended shoulder transitions tucked naturally inside sloped shoulders (+/-0.162, z=0.428)
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.156, 0.002, 0.428), (0.058, 0.060, 0.058), M["blue"], 16, 12)
    # Sculpted front kangaroo pocket with tapered side entries
    add_box(tmp, (0, -0.134, 0.170), (0.220, 0.028, 0.128), M["blue"], bevel=0.012, segs=2, smooth=True)
    add_box(tmp, (0, -0.141, 0.108), (0.214, 0.014, 0.014), M["navy"], bevel=0.004, segs=1, smooth=True)
    # Folded hood draped naturally behind the neck & collar rim
    add_loft(tmp, [
        ((0, 0.082, 0.415), 0.124, 0.064, 22),
        ((0, 0.098, 0.472), 0.138, 0.072, 12),
        ((0, 0.072, 0.526), 0.122, 0.068, -10),
        ((0, 0.026, 0.542), 0.098, 0.084, -22),
    ], M["blue"], segs=20)
    # Inner hood lining shadow / collar rim
    add_loft(tmp, [
        ((0, -0.002, 0.490), 0.094, 0.082),
        ((0, -0.004, 0.518), 0.084, 0.076),
    ], M["navy"], segs=18)
    # White crew-neck undershirt peek + neck column
    add_loft(tmp, [
        ((0, -0.004, 0.500), 0.072, 0.070),
        ((0, -0.004, 0.526), 0.064, 0.064),
    ], M["ivory"], segs=16)
    add_loft(tmp, [
        ((0, -0.002, 0.510), 0.054, 0.056),
        ((0, -0.002, 0.578), 0.050, 0.052),
    ], M["skin"], segs=16)
    # Two curved hoodie drawstrings + aglet tips
    for s in (-1, 1):
        add_tube(tmp, [
            (s * 0.042, -0.084, 0.486),
            (s * 0.048, -0.136, 0.432),
            (s * 0.044, -0.145, 0.362),
            (s * 0.040, -0.144, 0.310),
        ], 0.0050, M["ivory"], segs=8)
        add_cyl(tmp, (s * 0.040, -0.144, 0.302), 0.0060, 0.0052, 0.017, M["coal"], segs=8)
    # Modern 2026 startup crossbody tech sling strap cleanly hugging outside chest contour + slim back pouch
    add_tube(tmp, [
        (-0.108, -0.092, 0.482),
        (-0.042, -0.144, 0.395),
        ( 0.045, -0.152, 0.280),
        ( 0.135, -0.125, 0.165),
        ( 0.184, -0.020, 0.115),
        ( 0.175,  0.078, 0.130),
        ( 0.085,  0.146, 0.225),
        (-0.048,  0.148, 0.375),
        (-0.105,  0.088, 0.482),
        (-0.108, -0.092, 0.482),
    ], 0.011, M["coal"], segs=8, cap=False)
    add_box(tmp, (0.015, 0.156, 0.285), (0.168, 0.048, 0.215), M["coal"],
            bevel=0.015, segs=2, rot=Euler((0, math.radians(-18), 0)), smooth=True)

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_head_geom(bm, M, xf=Matrix.Identity(4), variant=0):
    """12 unmistakably distinct collectible 2026 Vibe Coder heads (`coder_head_0..11.glb`):
      0  (tab/stack):       Messy curly zoomer hair + over-ear studio headphones + rect glasses
      1  (div/smoothie):    Ribbed hipster knit beanie + round indie glasses + chin goatee + earbuds
      2  (weekend/leverage):Backwards startup baseball cap + wrap cyber-visor shades
      3  (prompt/framework):Pointed Tech-Wizard hat with glowing rune band + long wizard beard + round specs
      4  (call/deadline):   Slicked-back corporate pompadour + call-center boom-mic headset + sharp manager glasses
      5  (refactor):        Wild punk spiked mohawk + martial-arts headband with trailing tails + fiery shades
      6  (no-commits):      Silver-haired Zen guru with top man-bun, full beard + floating golden halo crown
      7  (skolkovo):        Full cyberpunk VR visor headset + dual glowing telemetry antennas
      8  (meetup):          Oversized luxury designer bucket hat + thick white-framed hypebeast shades
      9  (friday):          Chaotic spiked hair + glowing curved Devil Horns + fiery shades (Legendary!)
      10 (legacy):          Retro CRT Monitor Helmet with glowing terminal visor screen (Legendary!)
      11 (sb_chief/pm):     Peaked tactical security cap + dark aviator reflector shades + stern jaw
    """
    tmp = bmesh.new()
    # Neck transition dome
    add_sphere(tmp, (0, 0.0, 0.020), (0.050, 0.050, 0.038), M["skin"], 14, 10)

    if variant == 10:
        # Variant 10 (LEGENDARY): Retro CRT Monitor Helmet!
        add_box(tmp, (0, 0.015, 0.155), (0.255, 0.240, 0.220), M["ivory"], bevel=0.018, segs=2, smooth=True)
        add_box(tmp, (0, 0.140, 0.155), (0.175, 0.095, 0.155), M["ivory"], bevel=0.014, segs=2, smooth=True)  # CRT back housing
        # Recessed dark bezel & glowing matrix screen face (-Y)
        add_box(tmp, (0, -0.104, 0.160), (0.210, 0.014, 0.168), M["coal"], bevel=0.006, segs=1)
        add_box(tmp, (0, -0.111, 0.162), (0.184, 0.008, 0.142), M["teal"], bevel=0.004, segs=1)
        # Cute pixel-style glowing eyes (iris) + smile + padded neck gasket (hair) so all 6 slots match!
        add_cyl(tmp, (0, 0.005, 0.045), 0.072, 0.070, 0.022, M["hair"], segs=16)
        for s in (-1, 1):
            add_box(tmp, (s * 0.042, -0.116, 0.180), (0.022, 0.005, 0.030), M["iris"], bevel=0.001, segs=1)
        add_box(tmp, (0, -0.116, 0.132), (0.068, 0.005, 0.012), M["iris"], bevel=0.001, segs=1)
        # Power button LED on lower bezel + tiny retro antenna on top
        add_cyl(tmp, (0.078, -0.108, 0.068), 0.008, 0.008, 0.008, M["teal"], segs=10, rot=Euler((math.radians(90), 0, 0)))
        add_tube(tmp, [(0.06, 0.04, 0.262), (0.095, 0.05, 0.365)], 0.006, M["coal"], segs=8)
        add_sphere(tmp, (0.095, 0.05, 0.372), (0.014, 0.014, 0.014), M["teal"], 10, 8)
        bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
        _merge_bmesh(tmp, bm)
        return

    # Base stylized sculpted human head (slightly fuller, expressive indie proportions)
    add_loft(tmp, [
        ((0, -0.018, 0.042), 0.044, 0.056),
        ((0, -0.016, 0.072), 0.078, 0.090),
        ((0, -0.010, 0.118), 0.096, 0.106),
        ((0, -0.005, 0.165), 0.101, 0.112),
        ((0,  0.002, 0.212), 0.096, 0.107),
        ((0,  0.006, 0.250), 0.070, 0.080),
        ((0,  0.006, 0.266), 0.026, 0.030),
    ], M["skin"], segs=24)
    add_box(tmp, (0, -0.113, 0.125), (0.022, 0.026, 0.036), M["skin"],
            bevel=0.008, segs=2, rot=Euler((math.radians(-16), 0, 0)), smooth=True)
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.100, 0.004, 0.132), (0.016, 0.023, 0.031), M["skin"], 12, 10)
    brow_tilt = -12 if variant in (2, 5, 9, 11) else (6 if variant in (1, 3) else -4)
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.037, -0.096, 0.148), (0.016, 0.010, 0.013), M["ivory"], 12, 8)
        add_sphere(tmp, (s * 0.037, -0.104, 0.148), (0.009, 0.006, 0.010), M["iris"], 10, 8)
        add_box(tmp, (s * 0.037, -0.103, 0.171), (0.034, 0.010, 0.009), M["ivory"] if variant == 6 else M["hair"],
                bevel=0.003, segs=1, rot=Euler((0, s * math.radians(brow_tilt), 0)), smooth=True)
    add_box(tmp, (0, -0.099, 0.088), (0.036, 0.008, 0.007), M["iris"], bevel=0.002, segs=1, smooth=True)

    def add_rect_glasses(frame_mat=M["coal"], lens_mat=M["teal"]):
        for s in (-1, 1):
            add_box(tmp, (s * 0.042, -0.112, 0.148), (0.056, 0.008, 0.036), frame_mat, bevel=0.003, segs=1)
            add_box(tmp, (s * 0.042, -0.115, 0.148), (0.046, 0.006, 0.027), lens_mat, bevel=0.002, segs=1)
            add_box(tmp, (s * 0.079, -0.056, 0.150), (0.007, 0.114, 0.008), frame_mat, bevel=0.002, segs=1)
        add_box(tmp, (0, -0.113, 0.153), (0.030, 0.007, 0.007), frame_mat, bevel=0.001, segs=1)

    def add_round_glasses(frame_mat=M["coal"], lens_mat=M["teal"]):
        for s in (-1, 1):
            add_cyl(tmp, (s * 0.041, -0.112, 0.148), 0.028, 0.028, 0.007, frame_mat, segs=20, rot=Euler((math.radians(90), 0, 0)))
            add_cyl(tmp, (s * 0.041, -0.115, 0.148), 0.023, 0.023, 0.005, lens_mat, segs=20, rot=Euler((math.radians(90), 0, 0)))
            add_box(tmp, (s * 0.076, -0.056, 0.150), (0.006, 0.112, 0.006), frame_mat, bevel=0.002, segs=1)
        add_box(tmp, (0, -0.113, 0.152), (0.028, 0.006, 0.006), frame_mat, bevel=0.001, segs=1)

    if variant == 0:
        # 0: Messy zoomer hair + rect glasses + bold studio over-ear headphones
        add_sphere(tmp, (0, 0.014, 0.198), (0.107, 0.116, 0.090), M["hair"], 20, 14)
        add_sphere(tmp, (0, 0.036, 0.154), (0.102, 0.096, 0.074), M["hair"], 18, 12)
        for pos, rad, rot_deg in [
            ((-0.058, -0.082, 0.214), (0.034, 0.046, 0.028), (25, 18, -20)),
            ((-0.022, -0.098, 0.218), (0.038, 0.050, 0.030), (32,  8,  -8)),
            (( 0.018, -0.100, 0.214), (0.040, 0.052, 0.030), (35, -10, 12)),
            (( 0.060, -0.080, 0.208), (0.034, 0.044, 0.028), (26, -22, 22)),
            ((-0.012, -0.040, 0.266), (0.054, 0.064, 0.032), (14,  10, -8)),
            (( 0.034, -0.028, 0.262), (0.050, 0.060, 0.030), (12, -14, 14)),
        ]:
            add_sphere(tmp, pos, rad, M["hair"], 12, 8, rot=Euler(tuple(math.radians(a) for a in rot_deg)))
        add_rect_glasses()
        band_pts = [(0.128 * math.sin(math.radians(-84 + i * 14)), 0.006, 0.155 + 0.142 * math.cos(math.radians(-84 + i * 14))) for i in range(13)]
        add_tube(tmp, band_pts, 0.018, M["coal"], segs=10, cap=True)
        for s in (-1, 1):
            add_cyl(tmp, (s * 0.122, 0.006, 0.136), 0.048, 0.040, 0.040, M["coal"], segs=20, rot=Euler((0, math.radians(90), 0)))
            add_cyl(tmp, (s * 0.142, 0.006, 0.136), 0.032, 0.029, 0.008, M["teal"], segs=18, rot=Euler((0, math.radians(90), 0)))

    elif variant == 1:
        # 1: Low-profile slouchy cuffed hipster knit beanie + round glasses + goatee + earbuds
        add_sphere(tmp, (0, 0.038, 0.152), (0.100, 0.092, 0.068), M["hair"], 16, 10)
        add_sphere(tmp, (0, -0.076, 0.054), (0.032, 0.026, 0.022), M["hair"], 12, 8)
        beanie_xf = Matrix.Translation((0, 0.020, 0.205)) @ Euler((math.radians(22), 0, 0)).to_matrix().to_4x4()
        b_tmp = bmesh.new()
        add_loft(b_tmp, [
            ((0, 0.000, -0.026), 0.118, 0.126),  # thick folded ribbed knit cuff
            ((0, 0.000,  0.012), 0.122, 0.130),
            ((0, 0.004,  0.032), 0.110, 0.118),  # slouchy knit dome
            ((0, 0.016,  0.068), 0.098, 0.106),
            ((0, 0.026,  0.092), 0.064, 0.070),
            ((0, 0.030,  0.104), 0.028, 0.030),
        ], M["coal"], segs=22)
        # Bright cuff stripe + cute knit pom-pom on back slouch!
        add_loft(b_tmp, [((0, 0, -0.018), 0.124, 0.132), ((0, 0, 0.006), 0.125, 0.133)], M["teal"], segs=22, cap_bottom=False, cap_top=False)
        add_sphere(b_tmp, (0, 0.032, 0.114), (0.032, 0.032, 0.028), M["teal"], 12, 8)
        bmesh.ops.transform(b_tmp, matrix=beanie_xf, verts=b_tmp.verts)
        _merge_bmesh(b_tmp, tmp)
        add_round_glasses()
        for s in (-1, 1):
            add_cyl(tmp, (s * 0.104, -0.002, 0.124), 0.009, 0.007, 0.028, M["ivory"], segs=10)

    elif variant == 2:
        # 2: Backwards startup baseball cap + bold wrap cyber-visor shades
        add_sphere(tmp, (0, 0.012, 0.194), (0.104, 0.114, 0.082), M["hair"], 18, 12)
        for pos, rad in [((-0.038, -0.098, 0.198), (0.036, 0.046, 0.024)), ((0.012, -0.102, 0.196), (0.040, 0.048, 0.026)), ((0.054, -0.090, 0.194), (0.032, 0.042, 0.024))]:
            add_sphere(tmp, pos, rad, M["hair"], 12, 8)
        add_sphere(tmp, (0, 0.012, 0.214), (0.112, 0.120, 0.082), M["coal"], 22, 14)
        add_cyl(tmp, (0, 0.012, 0.296), 0.013, 0.013, 0.010, M["teal"], segs=12)
        add_box(tmp, (0, 0.155, 0.194), (0.160, 0.140, 0.016), M["coal"], bevel=0.006, segs=2, rot=Euler((math.radians(8), 0, 0)), smooth=True)
        add_box(tmp, (0, 0.160, 0.200), (0.144, 0.118, 0.008), M["teal"], bevel=0.002, segs=1, rot=Euler((math.radians(8), 0, 0)))
        add_box(tmp, (0, -0.114, 0.149), (0.148, 0.016, 0.038), M["coal"], bevel=0.004, segs=2)
        add_box(tmp, (0, -0.120, 0.149), (0.136, 0.010, 0.028), M["teal"], bevel=0.003, segs=1)
        for s in (-1, 1):
            add_box(tmp, (s * 0.082, -0.056, 0.151), (0.009, 0.118, 0.012), M["coal"], bevel=0.002, segs=1)

    elif variant == 3:
        # 3: Tech-Wizard! Pointed wizard hat with glowing rune band + long sculpted beard + round specs!
        add_sphere(tmp, (0, 0.032, 0.155), (0.098, 0.092, 0.078), M["hair"], 16, 12)
        # Sculpted long mystical beard
        add_loft(tmp, [
            ((0, -0.078,  0.095), 0.068, 0.036),
            ((0, -0.088,  0.045), 0.056, 0.034),
            ((0, -0.092, -0.015), 0.042, 0.028),
            ((0, -0.086, -0.065), 0.018, 0.016),
        ], M["hair"], segs=16)
        # Wide wizard brim + tall curved pointed cone hat + glowing teal rune band
        add_cyl(tmp, (0, 0.004, 0.214), 0.210, 0.200, 0.014, M["coal"], segs=24, rot=Euler((math.radians(8), 0, 0)))
        add_loft(tmp, [
            ((0,  0.004, 0.218), 0.110, 0.116),
            ((0,  0.012, 0.290), 0.088, 0.092),
            ((0,  0.032, 0.370), 0.056, 0.058),
            ((0,  0.068, 0.445), 0.026, 0.028),
            ((0,  0.105, 0.490), 0.008, 0.008),
        ], M["coal"], segs=20)
        add_loft(tmp, [((0, 0.005, 0.222), 0.114, 0.120), ((0, 0.008, 0.248), 0.108, 0.114)], M["teal"], segs=22, cap_bottom=False, cap_top=False)
        add_round_glasses()

    elif variant == 4:
        # 4: Corporate Call-Center Team Lead — tall slicked-back pompadour + boom-mic headset + sharp glasses
        add_sphere(tmp, (0, 0.022, 0.188), (0.096, 0.106, 0.078), M["hair"], 18, 12)
        add_loft(tmp, [
            ((0, -0.045, 0.218), 0.082, 0.088),
            ((0, -0.062, 0.268), 0.076, 0.098),  # proud slicked-back pompadour wave
            ((0, -0.030, 0.302), 0.054, 0.078),
        ], M["hair"], segs=18)
        add_rect_glasses()
        # One-sided call headset + thin boom microphone curving right in front of mouth!
        band_pts = [(0.108 * math.sin(math.radians(-75 + i * 15)), 0.004, 0.165 + 0.138 * math.cos(math.radians(-75 + i * 15))) for i in range(11)]
        add_tube(tmp, band_pts, 0.007, M["coal"], segs=8)
        add_cyl(tmp, (-0.104, 0.004, 0.136), 0.028, 0.024, 0.022, M["coal"], segs=16, rot=Euler((0, math.radians(90), 0)))
        add_tube(tmp, [(-0.104, -0.008, 0.124), (-0.094, -0.078, 0.098), (-0.032, -0.118, 0.088)], 0.005, M["coal"], segs=8)
        add_sphere(tmp, (-0.024, -0.120, 0.088), (0.012, 0.010, 0.010), M["teal"], 10, 8)  # glowing mic tip

    elif variant == 5:
        # 5: Refactoring Berserker — wild spiked punk mohawk + tied headband with fluttering tails + aggressive shades
        add_sphere(tmp, (0, 0.018, 0.188), (0.095, 0.104, 0.076), M["hair"], 16, 10)
        # Tall bold mohawk fin of sculpted spikes along center crown
        for k in range(6):
            z = 0.275 + 0.035 * math.sin(k * 0.6)
            y = -0.075 + k * 0.044
            add_cyl(tmp, (0, y, z), 0.024, 0.004, 0.095, M["hair"], segs=8, rot=Euler((math.radians(-18 + k * 10), 0, 0)))
        # Glowing headband wrapped around forehead + 2 dynamic trailing ribbon tails behind head
        add_loft(tmp, [((0, 0.0, 0.184), 0.100, 0.112), ((0, 0.0, 0.212), 0.101, 0.113)], M["teal"], segs=22, cap_bottom=False, cap_top=False)
        for s in (-1, 1):
            add_tube(tmp, [(s * 0.018, 0.112, 0.196), (s * 0.045, 0.175, 0.172), (s * 0.078, 0.228, 0.140)], 0.011, M["teal"], segs=6)
        add_box(tmp, (0, -0.108, 0.148), (0.132, 0.012, 0.028), M["coal"], bevel=0.004, segs=1)
        add_box(tmp, (0, -0.112, 0.148), (0.118, 0.008, 0.020), M["teal"], bevel=0.002, segs=1)

    elif variant == 6:
        # 6 (EPIC): Senior With Zero Commits — silver Zen guru with top man-bun, full silver beard + FLOATING GOLDEN HALO!
        add_sphere(tmp, (0, 0.018, 0.196), (0.100, 0.109, 0.084), M["ivory"], 18, 12)
        add_sphere(tmp, (0, 0.042, 0.285), (0.044, 0.044, 0.038), M["ivory"], 14, 10)  # top man-bun
        add_cyl(tmp, (0, 0.035, 0.264), 0.032, 0.032, 0.012, M["coal"], segs=14)       # bun tie
        add_loft(tmp, [((0, -0.068, 0.105), 0.072, 0.046), ((0, -0.078, 0.052), 0.062, 0.040), ((0, -0.076, 0.010), 0.036, 0.026)], M["ivory"], segs=16)
        # Floating glowing golden/teal Zen Halo ring hovering cleanly above crown!
        halo_pts = [(0.132 * math.cos(i * math.tau / 20), 0.132 * math.sin(i * math.tau / 20), 0.355) for i in range(21)]
        add_tube(tmp, halo_pts, 0.015, M["teal"], segs=10, cap=False)
        add_round_glasses(M["ivory"], M["teal"])

    elif variant == 7:
        # 7 (EPIC): Skolkovo Neuro-Shaman — full chunky VR Headset + dual glowing cyber antennas!
        add_sphere(tmp, (0, 0.016, 0.196), (0.106, 0.114, 0.088), M["hair"], 18, 12)
        # Chunky sculpted VR goggle box strapped across face
        add_box(tmp, (0, -0.115, 0.152), (0.205, 0.082, 0.094), M["coal"], bevel=0.016, segs=2, smooth=True)
        add_box(tmp, (0, -0.155, 0.152), (0.174, 0.016, 0.064), M["teal"], bevel=0.006, segs=1)
        # Head straps + top strap
        add_loft(tmp, [((0, 0.008, 0.136), 0.108, 0.118), ((0, 0.008, 0.168), 0.108, 0.118)], M["coal"], segs=20, cap_bottom=False, cap_top=False)
        # Dual sci-fi neuro antennas rising from temples with glowing orb tips
        for s in (-1, 1):
            add_tube(tmp, [(s * 0.104, -0.025, 0.175), (s * 0.138, -0.010, 0.345)], 0.009, M["coal"], segs=8)
            add_sphere(tmp, (s * 0.138, -0.010, 0.360), (0.024, 0.024, 0.024), M["teal"], 12, 8)

    elif variant == 8:
        # 8 (EPIC): Meetup Boss in Balenciaga — trendy oversized designer bucket hat + thick white hypebeast shades!
        add_sphere(tmp, (0, 0.032, 0.152), (0.100, 0.092, 0.068), M["hair"], 16, 10)
        # Sculpted wide sloped designer bucket hat (ivory/off-white luxury look + dark band)
        add_loft(tmp, [
            ((0, 0.0, 0.182), 0.185, 0.192),  # wide downward-sloping bucket brim
            ((0, 0.0, 0.214), 0.120, 0.128),
            ((0, 0.0, 0.292), 0.108, 0.114),  # flat-top bucket crown
            ((0, 0.0, 0.302), 0.095, 0.102),
        ], M["ivory"], segs=24)
        add_loft(tmp, [((0, 0.0, 0.215), 0.122, 0.130), ((0, 0.0, 0.238), 0.119, 0.127)], M["coal"], segs=24, cap_bottom=False, cap_top=False)
        add_rect_glasses(M["ivory"], M["teal"])

    elif variant == 9:
        # 9 (LEGENDARY): Friday 19:59 Prod Committer — chaotic hair + BOLD CURVED GLOWING DEVIL HORNS + fiery shades!
        add_sphere(tmp, (0, 0.014, 0.198), (0.107, 0.116, 0.088), M["hair"], 18, 12)
        for s in (-1, 1):
            # Bold curved devil horns sweeping up & forward from forehead!
            add_tube(tmp, [
                (s * 0.060, -0.050, 0.235),
                (s * 0.110, -0.062, 0.305),
                (s * 0.095, -0.088, 0.382),
                (s * 0.058, -0.104, 0.425),
            ], [0.028, 0.022, 0.015, 0.005], M["teal"], segs=10, cap=True)
        add_box(tmp, (0, -0.114, 0.148), (0.146, 0.014, 0.036), M["coal"], bevel=0.004, segs=1)
        add_box(tmp, (0, -0.120, 0.148), (0.134, 0.010, 0.026), M["teal"], bevel=0.003, segs=1)

    else:
        # 11: Security Chief / Toxic PM / Scrum Inquisitor — peaked security cap + dark reflector aviator shades
        add_sphere(tmp, (0, 0.024, 0.184), (0.096, 0.104, 0.072), M["hair"], 16, 10)
        add_loft(tmp, [
            ((0, 0.008, 0.205), 0.104, 0.112),
            ((0, 0.004, 0.268), 0.118, 0.122),  # tall peaked officer crown
            ((0, 0.000, 0.285), 0.106, 0.112),
        ], M["coal"], segs=22)
        # Glossy front peak visor (-Y) + metallic badge on front crown
        add_box(tmp, (0, -0.125, 0.198), (0.142, 0.095, 0.012), M["coal"], bevel=0.005, segs=2, rot=Euler((math.radians(-14), 0, 0)), smooth=True)
        add_cyl(tmp, (0, -0.114, 0.244), 0.018, 0.016, 0.010, M["teal"], segs=14, rot=Euler((math.radians(90), 0, 0)))
        add_rect_glasses(M["coal"], M["teal"])

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_upper_arm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Articulated upper arm with seamless tucked hoodie shoulder cap (pivot at shoulder 0,0,0 -> elbow Z=-0.255)."""
    tmp = bmesh.new()
    add_sphere(tmp, (-side * 0.014, 0, 0.002), (0.062, 0.062, 0.062), M["blue"], 16, 12)
    add_loft(tmp, [
        ((-side*0.010, 0.000, -0.008), 0.059, 0.060),
        (( side*0.008,-0.002, -0.130), 0.053, 0.054),
        (( side*0.014,-0.004, -0.255), 0.049, 0.050),
    ], M["blue"], segs=16)
    add_sphere(tmp, (side * 0.014, -0.004, -0.255), (0.048, 0.049, 0.048), M["blue"], 14, 10)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_forearm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Articulated forearm + ribbed cuff + sculpted hand (pivot at elbow 0,0,0 -> fingertip Z=-0.30)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.006), (0.047, 0.048, 0.047), M["blue"], 14, 10)
    add_loft(tmp, [
        ((0.0,       0.000, -0.006), 0.046, 0.047),
        ((0.0,      -0.004, -0.115), 0.043, 0.044),
        ((0.0,      -0.006, -0.205), 0.037, 0.038),
    ], M["blue"], segs=16)
    # Ribbed hoodie cuff
    add_loft(tmp, [
        ((0.0, -0.006, -0.202), 0.038, 0.039),
        ((0.0, -0.006, -0.238), 0.032, 0.033),
    ], M["navy"], segs=14)
    # Stylized sculpted hand
    add_sphere(tmp, (0.0, -0.006, -0.252), (0.024, 0.022, 0.022), M["skin"], 12, 8)
    add_box(tmp, (0.0, -0.010, -0.286), (0.028, 0.048, 0.056), M["skin"],
            bevel=0.010, segs=2, rot=Euler((math.radians(10), 0, 0)), smooth=True)
    add_box(tmp, (-side * 0.014, -0.026, -0.274), (0.016, 0.020, 0.032), M["skin"],
            bevel=0.006, segs=1, rot=Euler((math.radians(28), side * math.radians(16), 0)), smooth=True)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_arm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Assembled slightly-bent arm used for static hero standing/seated exports."""
    build_upper_arm_geom(bm, M, side, xf)
    elbow_xf = xf @ Matrix.Translation((side * 0.014, -0.004, -0.255)) @ Euler((math.radians(-24), 0, 0)).to_matrix().to_4x4()
    build_forearm_geom(bm, M, side, elbow_xf)


def build_thigh_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Relaxed-fit 2026 jogger/denim thigh with fabric knee overlap (pivot at hip 0,0,0 -> knee Z=-0.42)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.008), (0.088, 0.090, 0.086), M["denim"], 16, 12)
    add_loft(tmp, [
        ((0, 0.000, -0.012), 0.087, 0.089),
        ((0,-0.005, -0.155), 0.081, 0.085),
        ((0,-0.007, -0.310), 0.074, 0.077),
        ((0,-0.004, -0.430), 0.068, 0.071),
    ], M["denim"], segs=16)
    add_sphere(tmp, (0, -0.004, -0.422), (0.066, 0.068, 0.065), M["denim"], 14, 10)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_shin_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Relaxed-fit tapered jogger calf + chunky modern sneaker (pivot at knee 0,0,0 -> flat sole Z=-0.48)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0.008), (0.064, 0.066, 0.063), M["denim"], 14, 10)
    add_loft(tmp, [
        ((0, 0.000,  0.002), 0.065, 0.067),
        ((0, 0.006, -0.135), 0.063, 0.066),
        ((0, 0.008, -0.275), 0.054, 0.057),
        ((0, 0.006, -0.362), 0.046, 0.048),
    ], M["denim"], segs=16)
    add_loft(tmp, [
        ((0, 0.006, -0.358), 0.046, 0.048),
        ((0, 0.005, -0.384), 0.039, 0.040),
    ], M["navy"], segs=14)
    add_loft(tmp, [
        ((0, 0.005, -0.380), 0.036, 0.037),
        ((0, 0.004, -0.412), 0.036, 0.037),
    ], M["ivory"], segs=14)

    # Chunky retro-modern startup sneaker (sole bottom strictly at Z = -0.480)
    add_box(tmp, (0, -0.052, -0.473), (0.100, 0.246, 0.014), M["coal"], bevel=0.005, segs=1)
    add_box(tmp, (0, -0.052, -0.453), (0.104, 0.252, 0.030), M["sole"], bevel=0.009, segs=2, smooth=True)
    add_box(tmp, (0, -0.036, -0.420), (0.096, 0.198, 0.052), M["navy"], bevel=0.016, segs=2, smooth=True)
    add_sphere(tmp, (0, -0.124, -0.428), (0.046, 0.058, 0.026), M["ivory"], 14, 10)
    add_box(tmp, (0, -0.060, -0.396), (0.058, 0.048, 0.026), M["ivory"],
            bevel=0.008, segs=1, rot=Euler((math.radians(-28), 0, 0)), smooth=True)
    add_box(tmp, (0, -0.036, -0.424), (0.099, 0.072, 0.016), M["ivory"], bevel=0.004, segs=1)

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def _merge_bmesh(src, dst):
    vmap = {}
    for v in src.verts:
        vmap[v.index] = dst.verts.new(v.co)
    for f in src.faces:
        try:
            nf = dst.faces.new([vmap[v.index] for v in f.verts])
            nf.material_index = f.material_index
            nf.smooth = f.smooth
        except ValueError:
            pass
    src.free()


def export_part(filename, mat_keys, build_fn):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bm = bmesh.new()
    M = {k: i for i, k in enumerate(mat_keys)}
    build_fn(bm, M)
    bake_ao_and_export(bm, mat_keys, os.path.join(MODELS_DIR, filename))


def build_assembled_standing():
    """Assembled standing hero GLB (exact legacy 10-material slot order for ArchiveDossier & fallback)."""
    mat_keys = ["skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal"]
    def _build(bm, M):
        pelvis_z = 0.90
        build_torso_geom(bm, M, Matrix.Translation((0, 0, pelvis_z)))
        build_head_geom(bm, M, Matrix.Translation((0, 0, pelvis_z + 0.54)))
        build_arm_geom(bm, M, +1, Matrix.Translation(( 0.168, 0.0, pelvis_z + 0.432)) @ Euler((math.radians(6), math.radians(-5), 0)).to_matrix().to_4x4())
        build_arm_geom(bm, M, -1, Matrix.Translation((-0.168, 0.0, pelvis_z + 0.432)) @ Euler((math.radians(6), math.radians( 5), 0)).to_matrix().to_4x4())
        for s in (-1, 1):
            build_thigh_geom(bm, M, s, Matrix.Translation((s * 0.088, 0, pelvis_z)))
            build_shin_geom(bm, M, s, Matrix.Translation((s * 0.088, 0, pelvis_z - 0.42)))
    export_part("char_vibe_coder.glb", mat_keys, _build)


def build_assembled_seated():
    """Dedicated crisp seated typing pose aligned with WorkstationScale=0.92 chair (seat Y=0.47, desk Y=0.73)."""
    mat_keys = ["skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal"]
    def _build(bm, M):
        # Chair seat in prop_workstation_full is at local Y_gltf ~= 0.51; seated feet at SeatedFeet.
        # Build seated pose in local units (scaled by WorkstationScale=0.92 in C#):
        # Pelvis sits squarely on seat cushion at Z=0.56, shifted slightly back (Y=+0.10),
        # thighs angled horizontally forward toward desk (-Y), shins drop cleanly vertically to Z=0.0!
        pelvis = Vector((0.0, 0.14, 0.54))
        torso_xf = Matrix.Translation(pelvis) @ Euler((math.radians(-10), 0, 0)).to_matrix().to_4x4()
        build_torso_geom(bm, M, torso_xf)
        head_pos = torso_xf @ Vector((0, 0, 0.54))
        build_head_geom(bm, M, Matrix.Translation(head_pos) @ Euler((math.radians(-14), 0, 0)).to_matrix().to_4x4())
        # Arms reaching naturally forward & slightly inward over the mechanical keyboard!
        for s in (-1, 1):
            shld = torso_xf @ Vector((s * 0.168, 0.0, 0.432))
            arm_rot = Euler((math.radians(-58), s * math.radians(-8), s * math.radians(-10)))
            build_arm_geom(bm, M, s, Matrix.Translation(shld) @ arm_rot.to_matrix().to_4x4())
        for s in (-1, 1):
            hip = Vector((s * 0.094, 0.12, 0.53))
            thigh_rot = Euler((math.radians(-84), 0, s * math.radians(5)))
            thigh_xf = Matrix.Translation(hip) @ thigh_rot.to_matrix().to_4x4()
            build_thigh_geom(bm, M, s, thigh_xf)
            knee = thigh_xf @ Vector((0, 0, -0.42))
            # Adjust knee Z slightly so sole hits exactly Z=0.0
            knee_grounded = Vector((knee.x, knee.y, 0.48))
            build_shin_geom(bm, M, s, Matrix.Translation(knee_grounded) @ Euler((math.radians(6), 0, 0)).to_matrix().to_4x4())
    export_part("char_vibe_coder_seated.glb", mat_keys, _build)


def build_seated_body_headless():
    """Headless seated body GLB so Open-Space seated coders wear their EXACT captured collectible head variant!"""
    mat_keys = ["skin", "hair", "blue", "denim", "navy", "sole", "ivory", "iris", "teal", "coal"]
    def _build(bm, M):
        pelvis = Vector((0.0, 0.14, 0.54))
        torso_xf = Matrix.Translation(pelvis) @ Euler((math.radians(-10), 0, 0)).to_matrix().to_4x4()
        build_torso_geom(bm, M, torso_xf)
        for s in (-1, 1):
            shld = torso_xf @ Vector((s * 0.168, 0.0, 0.432))
            arm_rot = Euler((math.radians(-58), s * math.radians(-8), s * math.radians(-10)))
            build_arm_geom(bm, M, s, Matrix.Translation(shld) @ arm_rot.to_matrix().to_4x4())
        for s in (-1, 1):
            hip = Vector((s * 0.094, 0.12, 0.53))
            thigh_rot = Euler((math.radians(-84), 0, s * math.radians(5)))
            thigh_xf = Matrix.Translation(hip) @ thigh_rot.to_matrix().to_4x4()
            build_thigh_geom(bm, M, s, thigh_xf)
            knee = thigh_xf @ Vector((0, 0, -0.42))
            knee_grounded = Vector((knee.x, knee.y, 0.48))
            build_shin_geom(bm, M, s, Matrix.Translation(knee_grounded) @ Euler((math.radians(6), 0, 0)).to_matrix().to_4x4())
    export_part("coder_seated_body.glb", mat_keys, _build)


def main():
    export_part("coder_torso.glb",       ["blue", "navy", "skin", "ivory", "coal", "denim"], lambda bm, M: build_torso_geom(bm, M))
    export_part("coder_head.glb",        ["skin", "hair", "ivory", "iris", "teal", "coal"],  lambda bm, M: build_head_geom(bm, M, variant=0))
    for v in range(12):
        # Note: CRT Monitor Helmet (variant 10) doesn't use hair/iris, so keep exact consistent 6-slot order for all heads!
        export_part(f"coder_head_{v}.glb", ["skin", "hair", "ivory", "iris", "teal", "coal"], lambda bm, M, vv=v: build_head_geom(bm, M, variant=vv))
    export_part("coder_upper_arm_l.glb", ["blue"],                                           lambda bm, M: build_upper_arm_geom(bm, M, +1))
    export_part("coder_upper_arm_r.glb", ["blue"],                                           lambda bm, M: build_upper_arm_geom(bm, M, -1))
    export_part("coder_forearm_l.glb",   ["blue", "navy", "skin"],                           lambda bm, M: build_forearm_geom(bm, M, +1))
    export_part("coder_forearm_r.glb",   ["blue", "navy", "skin"],                           lambda bm, M: build_forearm_geom(bm, M, -1))
    export_part("coder_arm_l.glb",       ["blue", "navy", "skin"],                           lambda bm, M: build_arm_geom(bm, M, +1))
    export_part("coder_arm_r.glb",       ["blue", "navy", "skin"],                           lambda bm, M: build_arm_geom(bm, M, -1))
    export_part("coder_thigh_l.glb",     ["denim"],                                          lambda bm, M: build_thigh_geom(bm, M, +1))
    export_part("coder_thigh_r.glb",     ["denim"],                                          lambda bm, M: build_thigh_geom(bm, M, -1))
    export_part("coder_shin_l.glb",      ["denim", "navy", "ivory", "sole", "coal"],         lambda bm, M: build_shin_geom(bm, M, +1))
    export_part("coder_shin_r.glb",      ["denim", "navy", "ivory", "sole", "coal"],         lambda bm, M: build_shin_geom(bm, M, -1))
    build_assembled_standing()
    build_assembled_seated()
    build_seated_body_headless()
    print("ALL 12 UNIQUE COLLECTIBLE VIBE CODER HEADS + ARTICULATED PARTS BUILT!")


if __name__ == "__main__":
    main()
