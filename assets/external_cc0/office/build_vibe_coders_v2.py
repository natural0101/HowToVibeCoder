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
        ao = max(0.58, min(1.0, 1.0 - (occ / n_rays) * 0.48))
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


def build_head_geom(bm, M, xf=Matrix.Identity(4)):
    tmp = bmesh.new()
    # Neck transition dome
    add_sphere(tmp, (0, 0.0, 0.020), (0.052, 0.052, 0.040), M["skin"], 14, 10)
    # Stylized sculpted human head (chin -> cheeks -> temples -> cranium)
    add_loft(tmp, [
        ((0, -0.018, 0.042), 0.042, 0.055),  # chin
        ((0, -0.016, 0.072), 0.074, 0.086),  # jaw
        ((0, -0.010, 0.118), 0.090, 0.102),  # cheekbones
        ((0, -0.005, 0.165), 0.095, 0.108),  # brow/temples
        ((0,  0.002, 0.212), 0.092, 0.104),  # upper forehead
        ((0,  0.006, 0.252), 0.068, 0.078),  # crown
        ((0,  0.006, 0.268), 0.024, 0.028),
    ], M["skin"], segs=24)
    # Sculpted nose bridge & tip
    add_box(tmp, (0, -0.108, 0.126), (0.020, 0.026, 0.036), M["skin"],
            bevel=0.008, segs=2, rot=Euler((math.radians(-16), 0, 0)), smooth=True)
    # Ears
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.094, 0.004, 0.132), (0.016, 0.022, 0.030), M["skin"], 12, 10)
    # Expressive eyes (white sclera + dark pupils + sculpted eyebrows)
    for s in (-1, 1):
        add_sphere(tmp, (s * 0.036, -0.092, 0.148), (0.015, 0.010, 0.012), M["ivory"], 12, 8)
        add_sphere(tmp, (s * 0.036, -0.100, 0.148), (0.008, 0.006, 0.009), M["iris"], 10, 8)
        add_box(tmp, (s * 0.036, -0.098, 0.168), (0.030, 0.010, 0.008), M["hair"],
                bevel=0.003, segs=1, rot=Euler((0, s * math.radians(-7), 0)), smooth=True)
    # Subtle relaxed smile/mouth line
    add_box(tmp, (0, -0.094, 0.088), (0.034, 0.008, 0.006), M["iris"], bevel=0.002, segs=1, smooth=True)

    # Layered stylized messy 2026 startup hair (cap + sculpted overlapping locks & fringe)
    add_sphere(tmp, (0, 0.014, 0.198), (0.102, 0.112, 0.088), M["hair"], 20, 14)
    add_sphere(tmp, (0, 0.036, 0.154), (0.098, 0.092, 0.072), M["hair"], 18, 12)  # back hair volume
    # Sculpted wavy front fringe tufts & messy crown locks
    tufts = [
        ((-0.056, -0.078, 0.214), (0.032, 0.044, 0.026), (25, 18, -20)),
        ((-0.022, -0.094, 0.218), (0.036, 0.048, 0.028), (32,  8,  -8)),
        (( 0.018, -0.096, 0.214), (0.038, 0.050, 0.028), (35, -10, 12)),
        (( 0.058, -0.076, 0.208), (0.032, 0.042, 0.026), (26, -22, 22)),
        ((-0.012, -0.040, 0.264), (0.052, 0.062, 0.030), (14,  10, -8)),
        (( 0.034, -0.028, 0.260), (0.048, 0.058, 0.028), (12, -14, 14)),
        ((-0.086, -0.022, 0.172), (0.022, 0.042, 0.040), (10,  12, -8)),
        (( 0.086, -0.022, 0.172), (0.022, 0.042, 0.040), (10, -12,  8)),
    ]
    for pos, rad, rot_deg in tufts:
        add_sphere(tmp, pos, rad, M["hair"], 12, 8, rot=Euler(tuple(math.radians(a) for a in rot_deg)))

    # Stylish cyber/startup glasses (frames + glowing tinted lenses + bridge + temples)
    for s in (-1, 1):
        # Frame rim
        add_box(tmp, (s * 0.040, -0.107, 0.148), (0.054, 0.008, 0.036), M["coal"], bevel=0.003, segs=1)
        # Glossy tinted lens (teal -> dynamically tinted by c.GlassesColor!)
        add_box(tmp, (s * 0.040, -0.110, 0.148), (0.044, 0.006, 0.027), M["teal"], bevel=0.002, segs=1)
        # Temple arm back to ear
        add_box(tmp, (s * 0.076, -0.054, 0.150), (0.007, 0.112, 0.008), M["coal"], bevel=0.002, segs=1)
    add_box(tmp, (0, -0.108, 0.153), (0.028, 0.007, 0.007), M["coal"], bevel=0.001, segs=1)

    # Chunky Over-Ear Studio Headphones (padded headband + yokes + thick earcups + subtle teal ring)
    band_pts = []
    for i in range(13):
        a = math.radians(-84 + i * 14)
        band_pts.append((0.116 * math.sin(a), 0.006, 0.155 + 0.132 * math.cos(a)))
    add_tube(tmp, band_pts, 0.015, M["coal"], segs=10, cap=True)
    for s in (-1, 1):
        # Cushioned earcup
        add_cyl(tmp, (s * 0.112, 0.006, 0.136), 0.042, 0.036, 0.036, M["coal"],
                segs=20, rot=Euler((0, math.radians(90), 0)))
        # Subtle glowing studio accent ring on outer earcup shell
        add_cyl(tmp, (s * 0.129, 0.006, 0.136), 0.026, 0.024, 0.006, M["teal"],
                segs=18, rot=Euler((0, math.radians(90), 0)))

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_arm_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Articulated arm with pivot at shoulder ball joint (0,0,0). Natural sloped human deltoid, zero boxy pads."""
    tmp = bmesh.new()
    # Smooth tapered deltoid cap that blends seamlessly into sloped hoodie shoulder
    add_sphere(tmp, (0, 0, -0.005), (0.055, 0.057, 0.057), M["blue"], 16, 12)
    # Relaxed hoodie upper sleeve -> elbow -> slightly forward forearm sleeve
    add_loft(tmp, [
        ((0.0,        0.000, -0.015), 0.054, 0.056),
        ((side*0.016,-0.006, -0.125), 0.052, 0.053),
        ((side*0.022,-0.022, -0.240), 0.048, 0.050, 12),
        ((side*0.018,-0.056, -0.350), 0.044, 0.046, 18),
        ((side*0.012,-0.084, -0.445), 0.037, 0.038, 18),
    ], M["blue"], segs=16)
    # Ribbed hoodie cuff (navy trim)
    add_loft(tmp, [
        ((side*0.012,-0.084, -0.442), 0.038, 0.039, 18),
        ((side*0.010,-0.094, -0.482), 0.032, 0.033, 18),
    ], M["navy"], segs=14)
    # Stylized sculpted hand
    add_sphere(tmp, (side*0.010, -0.100, -0.496), (0.024, 0.022, 0.022), M["skin"], 12, 8)
    add_box(tmp, (side*0.010, -0.110, -0.532), (0.030, 0.048, 0.058), M["skin"],
            bevel=0.010, segs=2, rot=Euler((math.radians(16), 0, 0)), smooth=True)
    add_box(tmp, (-side*0.010, -0.128, -0.520), (0.017, 0.020, 0.034), M["skin"],
            bevel=0.006, segs=1, rot=Euler((math.radians(32), side*math.radians(18), 0)), smooth=True)

    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_thigh_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Relaxed-fit 2026 jogger/denim thigh with balanced human proportions (pivot at hip 0,0,0 -> knee Z=-0.42)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0), (0.086, 0.088, 0.084), M["denim"], 16, 12)
    add_loft(tmp, [
        ((0, 0.000, -0.015), 0.086, 0.088),
        ((0,-0.005, -0.155), 0.080, 0.084),
        ((0,-0.007, -0.305), 0.072, 0.075),
        ((0,-0.004, -0.415), 0.065, 0.068),
    ], M["denim"], segs=16)
    add_sphere(tmp, (0, -0.004, -0.420), (0.065, 0.067, 0.064), M["denim"], 14, 10)
    bmesh.ops.transform(tmp, matrix=xf, verts=tmp.verts)
    _merge_bmesh(tmp, bm)


def build_shin_geom(bm, M, side, xf=Matrix.Identity(4)):
    """Relaxed-fit tapered jogger calf + chunky modern sneaker (pivot at knee 0,0,0 -> flat sole Z=-0.48)."""
    tmp = bmesh.new()
    add_sphere(tmp, (0, 0, 0), (0.065, 0.067, 0.064), M["denim"], 14, 10)
    add_loft(tmp, [
        ((0, 0.000, -0.010), 0.065, 0.067),
        ((0, 0.006, -0.135), 0.062, 0.065),
        ((0, 0.008, -0.275), 0.053, 0.056),
        ((0, 0.006, -0.362), 0.045, 0.047),
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


def main():
    export_part("coder_torso.glb",   ["blue", "navy", "skin", "ivory", "coal", "denim"], lambda bm, M: build_torso_geom(bm, M))
    export_part("coder_head.glb",    ["skin", "hair", "ivory", "iris", "teal", "coal"],  lambda bm, M: build_head_geom(bm, M))
    export_part("coder_arm_l.glb",   ["blue", "navy", "skin"],                           lambda bm, M: build_arm_geom(bm, M, +1))
    export_part("coder_arm_r.glb",   ["blue", "navy", "skin"],                           lambda bm, M: build_arm_geom(bm, M, -1))
    export_part("coder_thigh_l.glb", ["denim"],                                          lambda bm, M: build_thigh_geom(bm, M, +1))
    export_part("coder_thigh_r.glb", ["denim"],                                          lambda bm, M: build_thigh_geom(bm, M, -1))
    export_part("coder_shin_l.glb",  ["denim", "navy", "ivory", "sole", "coal"],         lambda bm, M: build_shin_geom(bm, M, +1))
    export_part("coder_shin_r.glb",  ["denim", "navy", "ivory", "sole", "coal"],         lambda bm, M: build_shin_geom(bm, M, -1))
    build_assembled_standing()
    build_assembled_seated()
    print("ALL 10 HIGH-CRAFT VIBE CODER GLBs BUILT!")


if __name__ == "__main__":
    main()
